using backend.common.Models;
using backend.model.Models;
using backend.model.Models.Views;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using ClosedXML.Excel;
using System.Text.Json;
using System.IO.Compression;
using backend.common.Extensions;
using Microsoft.EntityFrameworkCore;

namespace backend.service.Repository.Implementation
{
    public class ExportRepository : IExportRepository
    {
        #region Variables & Constructor
        private readonly IUnitOfWork _unitOfWork;
        public ExportRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Export Transactions (Excel/CSV)
        public async Task<(byte[] FileContents, string ContentType, string FileName)> ExportTransactionsAsync(ExportRequestModel request)
        {
            // 1. Resolve which accounts are in scope and determine export mode
            var context = await ResolveAccountContextAsync(request);

            // 2. Fetch data – always strip description filter (we group by it instead)
            var filtersToApply = request.Filters?.Where(f =>
                !(f.Key == "description_sid" || f.Key == "DescriptionSID")
            ).ToList() ?? new List<FilterRequestModel>();

            var query = _unitOfWork.GetRepository<VwTransactionsList>().AsQueryable(enableTracking: false);
            query = query.ApplyFilters(filtersToApply);
            query = query.OrderBy(t => t.TransactionDate);

            var transactions = await query.ToListAsync();

            // 3. Financial year label
            var fy = GetFinancialYearString(request);

            // 4. Route by mode
            switch (context.Mode)
            {
                // ── Single account ──────────────────────────────────────────────
                case AccountExportMode.Single:
                {
                    var acc = context.Accounts.First();
                    var (bytes, name) = GenerateSingleAccountExcelExport(
                        transactions, acc.AccountName, acc.AccountNumber, acc.BankName, fy, 0, request.SeparateSheets, request.MergeDescriptions);
                    if (!string.IsNullOrWhiteSpace(request.ExcelName))
                        name = $"{request.ExcelName}.xlsx";
                    return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
                }

                // ── Multiple accounts, merged into one file ──────────────────────
                case AccountExportMode.MultiMerge:
                {
                    var (bytes, name) = GenerateMergedMultiAccountExport(transactions, request, fy);
                    if (!string.IsNullOrWhiteSpace(request.ExcelName))
                        name = $"{request.ExcelName}.xlsx";
                    return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
                }

                // ── Multiple accounts, one file per account in a ZIP ─────────────
                case AccountExportMode.MultiZip:
                default:
                {
                    var (bytes, name) = GenerateAllAccountsZipExport(transactions, request, fy);
                    if (!string.IsNullOrWhiteSpace(request.ExcelName))
                        name = $"{request.ExcelName}.zip";
                    return (bytes, "application/zip", name);
                }
            }
        }
        #endregion

        #region Excel Generation Helpers
        private (byte[] Bytes, string FileName) GenerateAllAccountsZipExport(List<VwTransactionsList> transactions, ExportRequestModel request, string fy)
        {
            var accountGroups = transactions.GroupBy(x => new { x.AccountSID, x.AccountName, x.AccountNumber, x.BankName }).ToList();
            
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var count = 0;
                foreach (var group in accountGroups)
                {
                    var (excelBytes, excelName) = GenerateSingleAccountExcelExport(group.ToList(), group.Key.AccountName, group.Key.AccountNumber, group.Key.BankName, fy, count, request.SeparateSheets, request.MergeDescriptions);
                    var entry = archive.CreateEntry(excelName);
                    using var entryStream = entry.Open();
                    entryStream.Write(excelBytes, 0, excelBytes.Length);
                    count++;
                }
            }

            var fileName = $"Cashbook_AllAccounts_{fy}.zip";
            return (zipStream.ToArray(), fileName);
        }

        /// <summary>
        /// Merge = true: all selected accounts in one Excel file.
        /// SeparateSheets → one tab per account + one tab per description (merged across accounts).
        /// Single sheet  → account tables then description tables on one worksheet.
        /// </summary>
        private (byte[] Bytes, string FileName) GenerateMergedMultiAccountExport(List<VwTransactionsList> transactions, ExportRequestModel request, string fy)
        {
            using var workbook = new XLWorkbook();

            var accountGroups = transactions
                .GroupBy(x => new { x.AccountSID, x.AccountName, x.AccountNumber, x.BankName })
                .OrderBy(g => g.Key.AccountName)
                .ToList();

            var descriptionGroups = transactions
                .GroupBy(x => new { x.DescriptionSID, x.DescriptionName })
                .Where(g => !string.IsNullOrEmpty(g.Key.DescriptionName))
                .OrderBy(g => g.Key.DescriptionName)
                .ToList();

            if (request.SeparateSheets)
            {
                // One sheet per account
                foreach (var accGroup in accountGroups)
                {
                    BuildAccountSheet(workbook, accGroup.Key.AccountName, accGroup.Key.AccountNumber, accGroup.Key.BankName, accGroup.ToList(), fy);
                }

                // Description sheets — merged or separate depending on flag
                if (request.MergeDescriptions)
                {
                    BuildMergedDescriptionsSheet(workbook, descriptionGroups.Select(g => (g.Key.DescriptionName, g.ToList())).ToList(), fy);
                }
                else
                {
                    foreach (var grp in descriptionGroups)
                    {
                        BuildDescriptionSheet(workbook, grp.Key.DescriptionName, grp.ToList(), fy);
                    }
                }
            }
            else
            {
                string sheetName = GetUniqueSheetName(workbook, "All Accounts");
                var ws = workbook.Worksheets.Add(sheetName);
                int currentRow = 1;

                // Account tables
                foreach (var accGroup in accountGroups)
                {
                    WriteSheetHeaderInline(ws, ref currentRow, accGroup.Key.AccountName, fy, accGroup.Key.AccountNumber, accGroup.Key.BankName);
                    currentRow++;
                    WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: true);
                    WriteTransactionRows(ws, ref currentRow, accGroup.ToList(), isAccountLayout: true);
                    WriteFooterRow(ws, ref currentRow, accGroup.ToList());
                    currentRow += 3;
                }

                // Description tables (merged across all accounts)
                foreach (var grp in descriptionGroups)
                {
                    WriteSheetHeaderInline(ws, ref currentRow, grp.Key.DescriptionName, fy);
                    currentRow++;
                    WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: false);
                    WriteTransactionRows(ws, ref currentRow, grp.ToList(), isAccountLayout: false);
                    WriteFooterRow(ws, ref currentRow, grp.ToList());
                    currentRow += 3;
                }

                ws.Columns().AdjustToContents();
            }

            if (!workbook.Worksheets.Any())
                workbook.AddWorksheet("No Data");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var fileName = $"Cashbook_MergedAccounts_{fy}.xlsx";
            return (stream.ToArray(), fileName);
        }

        private (byte[] Bytes, string FileName) GenerateSingleSheetMultiAccountExport(List<VwTransactionsList> transactions, string fy)
        {
            using var workbook = new XLWorkbook();
            
            string sheetName = GetUniqueSheetName(workbook, "All Accounts");
            var ws = workbook.Worksheets.Add(sheetName);
            int currentRow = 1;

            var accountGroups = transactions.GroupBy(x => new { x.AccountSID, x.AccountName, x.AccountNumber, x.BankName }).ToList();
            
            foreach(var accGroup in accountGroups.OrderBy(g => g.Key.AccountName))
            {
                WriteSheetHeaderInline(ws, ref currentRow, accGroup.Key.AccountName, fy, accGroup.Key.AccountNumber, accGroup.Key.BankName);
                currentRow++; 
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: true);
                WriteTransactionRows(ws, ref currentRow, accGroup.ToList(), isAccountLayout: true);
                WriteFooterRow(ws, ref currentRow, accGroup.ToList());
                
                currentRow += 3;
            }

            var descriptionGroups = transactions.GroupBy(x => new { x.DescriptionSID, x.DescriptionName }).ToList();
            foreach (var grp in descriptionGroups.OrderBy(g => g.Key.DescriptionName))
            {
                if (string.IsNullOrEmpty(grp.Key.DescriptionName)) continue;

                WriteSheetHeaderInline(ws, ref currentRow, grp.Key.DescriptionName, fy);
                currentRow++;
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: false);
                WriteTransactionRows(ws, ref currentRow, grp.ToList(), isAccountLayout: false);
                WriteFooterRow(ws, ref currentRow, grp.ToList());
                
                currentRow += 3;
            }

            ws.Columns().AdjustToContents();

            if (!workbook.Worksheets.Any())
                workbook.AddWorksheet("No Data");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            
            var fileName = $"Cashbook_CombinedAccounts_{fy}.xlsx";
            return (stream.ToArray(), fileName);
        }

        private (byte[] Bytes, string FileName) GenerateSingleAccountExcelExport(List<VwTransactionsList> transactions, string? accountName, int? accountNumber, string? bankName, string fy, int count, bool separateSheets, bool mergeDescriptions = false)
        {
            using var workbook = new XLWorkbook();
            
            if (separateSheets)
            {
                // 1st Sheet: Full account transactions
                BuildAccountSheet(workbook, accountName, accountNumber, bankName, transactions, fy);
                
                // Description sheets — merged or separate depending on flag
                var descriptionGroups = transactions
                    .GroupBy(x => new { x.DescriptionSID, x.DescriptionName })
                    .Where(g => !string.IsNullOrEmpty(g.Key.DescriptionName))
                    .OrderBy(g => g.Key.DescriptionName)
                    .ToList();

                if (mergeDescriptions)
                {
                    BuildMergedDescriptionsSheet(workbook, descriptionGroups.Select(g => (g.Key.DescriptionName, g.ToList())).ToList(), fy);
                }
                else
                {
                    foreach (var grp in descriptionGroups)
                    {
                        BuildDescriptionSheet(workbook, grp.Key.DescriptionName, grp.ToList(), fy);
                    }
                }
            }
            else
            {
                // Everything in 1 sheet
                string sheetName = GetUniqueSheetName(workbook, accountName ?? "Account", accountNumber);
                var ws = workbook.Worksheets.Add(sheetName);
                int currentRow = 1;

                // Account Table
                WriteSheetHeaderInline(ws, ref currentRow, accountName, fy, accountNumber, bankName);
                currentRow++; // Gap before column headers
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: true);
                WriteTransactionRows(ws, ref currentRow, transactions, isAccountLayout: true);
                WriteFooterRow(ws, ref currentRow, transactions);

                // Description Tables
                var descriptionGroups = transactions.GroupBy(x => new { x.DescriptionSID, x.DescriptionName }).ToList();
                foreach (var grp in descriptionGroups.OrderBy(g => g.Key.DescriptionName))
                {
                    if (string.IsNullOrEmpty(grp.Key.DescriptionName)) continue;

                    currentRow += 3; // 3 row line spacing between tables

                    WriteSheetHeaderInline(ws, ref currentRow, grp.Key.DescriptionName, fy);
                    currentRow++; // Gap before column headers
                    WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: false);
                    WriteTransactionRows(ws, ref currentRow, grp.ToList(), isAccountLayout: false);
                    WriteFooterRow(ws, ref currentRow, grp.ToList());
                }

                ws.Columns().AdjustToContents();
            }

            if (!workbook.Worksheets.Any())
                workbook.AddWorksheet("No Data");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            
            var fileName = $"Cashbook_{SanitizeFileName(accountName, count, accountNumber)}_{fy}.xlsx";
            return (stream.ToArray(), fileName);
        }
        #endregion

        #region Sheet & Row Builders
        private static void BuildAccountSheet(XLWorkbook wb, string? accountName, int? accountNumber, string? bankName, List<VwTransactionsList> transactions, string fy)
        {
            string sheetName = GetUniqueSheetName(wb, accountName ?? "Account", accountNumber);
            var ws = wb.Worksheets.Add(sheetName);

            WriteSheetHeader(ws, sheetName, fy, bankName);

            int currentRow = WriteColumnHeaders(ws, isAccountLayout: true);

            ws.SheetView.FreezeRows(currentRow - 1); // freeze up to and including column header row

            WriteTransactionRows(ws, ref currentRow, transactions, isAccountLayout: true);
            WriteFooterRow(ws, ref currentRow, transactions);

            ws.Columns().AdjustToContents();
        }

        private static void BuildDescriptionSheet(XLWorkbook wb, string? descriptionName, List<VwTransactionsList> transactions, string fy)
        {
            string sheetName = GetUniqueSheetName(wb, descriptionName ?? "Description");
            var ws = wb.Worksheets.Add(sheetName);

            WriteSheetHeader(ws, descriptionName, fy);

            int currentRow = WriteColumnHeaders(ws, isAccountLayout: false);

            ws.SheetView.FreezeRows(currentRow - 1);

            WriteTransactionRows(ws, ref currentRow, transactions, isAccountLayout: false);
            WriteFooterRow(ws, ref currentRow, transactions);

            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// Writes all description groups sequentially on a single "Descriptions" sheet.
        /// Used when MergeDescriptions = true and SeparateSheets = true.
        /// </summary>
        private static void BuildMergedDescriptionsSheet(XLWorkbook wb, List<(string? DescriptionName, List<VwTransactionsList> Transactions)> groups, string fy)
        {
            if (groups.Count == 0) return;

            string sheetName = GetUniqueSheetName(wb, "Descriptions");
            var ws = wb.Worksheets.Add(sheetName);
            int currentRow = 1;

            foreach (var (descriptionName, transactions) in groups)
            {
                WriteSheetHeaderInline(ws, ref currentRow, descriptionName, fy);
                currentRow++;
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: false);
                WriteTransactionRows(ws, ref currentRow, transactions, isAccountLayout: false);
                WriteFooterRow(ws, ref currentRow, transactions);
                currentRow += 3;
            }

            ws.Columns().AdjustToContents();
        }

        private static void WriteSheetHeader(IXLWorksheet ws, string? title, string fy, string? bankName = null)
        {
            ws.Range(1, 1, 1, 3).Merge().Value = title ?? string.Empty;
            ws.Range(1, 1, 1, 3).Style.Font.Bold = true;
            ws.Range(1, 1, 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (!string.IsNullOrWhiteSpace(bankName))
            {
                ws.Range(1, 4, 1, 5).Merge().Value = $"FY {fy}";
                ws.Range(1, 4, 1, 5).Style.Font.Bold = true;
                ws.Range(1, 4, 1, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range(1, 6, 1, 7).Merge().Value = bankName ?? string.Empty;
                ws.Range(1, 6, 1, 7).Style.Font.Bold = true;
                ws.Range(1, 6, 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            else
            {
                ws.Range(1, 4, 1, 7).Merge().Value = $"FY {fy}";
                ws.Range(1, 4, 1, 7).Style.Font.Bold = true;
                ws.Range(1, 4, 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        private static void WriteSheetHeaderInline(IXLWorksheet ws, ref int currentRow, string? title, string fy, int? accountNumber = null, string? bankName = null)
        {
            if (accountNumber.HasValue)
                title = $"{title} ({accountNumber})";
            ws.Range(currentRow, 1, currentRow, 3).Merge().Value = title ?? string.Empty;
            ws.Range(currentRow, 1, currentRow, 3).Style.Font.Bold = true;
            ws.Range(currentRow, 1, currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (!string.IsNullOrWhiteSpace(bankName))
            {
                ws.Range(currentRow, 4, currentRow, 5).Merge().Value = $"FY {fy}";
                ws.Range(currentRow, 4, currentRow, 5).Style.Font.Bold = true;
                ws.Range(currentRow, 4, currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Range(currentRow, 6, currentRow, 7).Merge().Value = bankName ?? string.Empty;
                ws.Range(currentRow, 6, currentRow, 7).Style.Font.Bold = true;
                ws.Range(currentRow, 6, currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            else
            {
                ws.Range(currentRow, 4, currentRow, 7).Merge().Value = $"FY {fy}";
                ws.Range(currentRow, 4, currentRow, 7).Style.Font.Bold = true;
                ws.Range(currentRow, 4, currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            currentRow++;
        }

        private static int WriteColumnHeaders(IXLWorksheet ws, bool isAccountLayout)
        {
            int headerRow = 3; // Row 1 = sheet header, Row 2 = blank gap
            WriteColumnHeaderRow(ws, headerRow, isAccountLayout);
            return headerRow + 1;
        }

        private static void WriteColumnHeadersInline(IXLWorksheet ws, ref int currentRow, bool isAccountLayout)
        {
            WriteColumnHeaderRow(ws, currentRow, isAccountLayout);
            currentRow++;
        }

        private static void WriteColumnHeaderRow(IXLWorksheet ws, int row, bool isAccountLayout)
        {
            ws.Cell(row, 1).Value = "SR";
            ws.Cell(row, 2).Value = "Date";
            ws.Cell(row, 3).Value = isAccountLayout ? "Payment Detail (Desc)" : "Account Name";
            ws.Cell(row, 4).Value = "Debit";
            ws.Cell(row, 5).Value = "Credit";
            ws.Cell(row, 6).Value = "Balance";
            ws.Cell(row, 7).Value = "Remarks";

            var range = ws.Range(row, 1, row, 7);
            range.Style.Font.Bold = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private static void WriteTransactionRows(IXLWorksheet ws, ref int currentRow, List<VwTransactionsList> transactions, bool isAccountLayout)
        {
            int sr = 1;
            foreach (var t in transactions)
            {
                ws.Cell(currentRow, 1).Value = sr++;
                ws.Cell(currentRow, 2).Value = FormatDate(t.TransactionDate);
                ws.Cell(currentRow, 3).Value = isAccountLayout
                    ? t.DescriptionName
                    : (t.AccountNumber.HasValue ? $"{t.AccountName} ({t.AccountNumber})" : t.AccountName);
                ws.Cell(currentRow, 4).Value = t.Debit ?? 0;
                ws.Cell(currentRow, 5).Value = t.Credit ?? 0;
                ws.Cell(currentRow, 6).Value = string.Empty; // Balance – empty per transaction row
                ws.Cell(currentRow, 7).Value = t.Notes ?? string.Empty;

                var range = ws.Range(currentRow, 1, currentRow, 7);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                currentRow++;
            }
        }

        private static void WriteFooterRow(IXLWorksheet ws, ref int currentRow, List<VwTransactionsList> transactions)
        {
            double totalDebit = transactions.Sum(t => t.Debit ?? 0);
            double totalCredit = transactions.Sum(t => t.Credit ?? 0);
            double balance = totalCredit - totalDebit;

            ws.Cell(currentRow, 1).Value = string.Empty;
            ws.Cell(currentRow, 2).Value = string.Empty;
            ws.Cell(currentRow, 3).Value = string.Empty;
            ws.Cell(currentRow, 4).Value = totalDebit;
            ws.Cell(currentRow, 5).Value = totalCredit;
            ws.Cell(currentRow, 6).Value = balance;
            ws.Cell(currentRow, 7).Value = string.Empty;

            var range = ws.Range(currentRow, 1, currentRow, 7);
            range.Style.Font.Bold = true;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            currentRow++;
        }
        #endregion

        #region Common Helpers
        private static string FormatDate(string? rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return string.Empty;
            if (DateTime.TryParse(rawDate, out DateTime dt))
                return dt.ToString("dd-MMM-yyyy");
            return rawDate;
        }

        private static string GetUniqueSheetName(XLWorkbook wb, string baseName, int? accountNumber = null)
        {
            string safeName = SanitizeSheetName(string.IsNullOrWhiteSpace(baseName) ? "Sheet" : baseName);
            if (accountNumber.HasValue)
                safeName = $"{safeName} ({accountNumber.Value})";

            string finalName = safeName;
            int counter = 1;

            while (wb.Worksheets.Contains(finalName))
            {
                string suffix = $" ({counter})";
                finalName = safeName.Length + suffix.Length > 31
                    ? safeName.Substring(0, 31 - suffix.Length) + suffix
                    : safeName + suffix;
                counter++;
            }
            return finalName;
        }

        /// <summary>
        /// Resolves which accounts are in scope and returns the export routing mode.
        /// Handles both a single `equals` filter and a multi-select `in` filter.
        /// </summary>
        private async Task<AccountExportContext> ResolveAccountContextAsync(ExportRequestModel request)
        {
            var accountFilterKey = new[] { "account_sid", "AccountSID" };

            // Check for a single-account equals filter
            var equalsFilter = request.Filters?.FirstOrDefault(f =>
                accountFilterKey.Contains(f.Key) && f.Condition?.ToLower() == "equals");

            if (equalsFilter != null && !string.IsNullOrEmpty(equalsFilter.Value?.ToString()))
            {
                var sid = equalsFilter.Value.ToString()!;
                var acc = await _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(a => a.AccountSID == sid);
                if (acc != null)
                {
                    var entry = new AccountEntry(acc.AccountSID, acc.AccountName, acc.AccountNumber, acc.BankName);
                    return new AccountExportContext(AccountExportMode.Single, new List<AccountEntry> { entry });
                }
                // SID provided but not found – treat as all-accounts
            }

            // Check for multi-select `in` filter
            var inFilter = request.Filters?.FirstOrDefault(f =>
                accountFilterKey.Contains(f.Key) && f.Condition?.ToLower() == "in");

            if (inFilter?.Value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var sids = je.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (sids.Count == 1)
                {
                    // Single SID via `in` – treat identically to equals
                    var acc = await _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(a => a.AccountSID == sids[0]);
                    if (acc != null)
                    {
                        var entry = new AccountEntry(acc.AccountSID, acc.AccountName, acc.AccountNumber, acc.BankName);
                        return new AccountExportContext(AccountExportMode.Single, new List<AccountEntry> { entry });
                    }
                }
                else if (sids.Count > 1)
                {
                    var accounts = await _unitOfWork.GetRepository<Accounts>()
                        .AsQueryable(enableTracking: false)
                        .Where(a => sids.Contains(a.AccountSID))
                        .OrderBy(a => a.AccountName)
                        .ToListAsync();

                    var entries = accounts
                        .Select(a => new AccountEntry(a.AccountSID, a.AccountName, a.AccountNumber, a.BankName))
                        .ToList();

                    var mode = request.MergeAccounts ? AccountExportMode.MultiMerge : AccountExportMode.MultiZip;
                    return new AccountExportContext(mode, entries);
                }
            }

            // No account filter – export all accounts
            var allMode = request.MergeAccounts ? AccountExportMode.MultiMerge : AccountExportMode.MultiZip;
            return new AccountExportContext(allMode, new List<AccountEntry>());
        }

        private static string GetFinancialYearString(ExportRequestModel request)
        {
            var dateFilter = request.Filters?.FirstOrDefault(f => f.Type == "date");
            if (dateFilter?.From != null &&
                DateTime.TryParse(dateFilter.From.ToString(), out DateTime startDate))
            {
                int startYear = startDate.Month >= 4 ? startDate.Year : startDate.Year - 1;
                return $"{startYear % 100}-{(startYear + 1) % 100}";
            }
            return "AllTime";
        }

        private static string SanitizeSheetName(string name)
        {
            foreach (var c in new[] { '\\', '/', '?', '*', '[', ']', ':' })
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private static (string, int) SanitizeFileName(string? name, int counter, int? accountNumber = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                counter++;
                return ("Export", counter);
            }
            if (accountNumber.HasValue)
                name = $"{name} ({accountNumber.Value})";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return (name, counter);;
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
        #endregion

        #region Supporting Types
        private enum AccountExportMode { Single, MultiMerge, MultiZip }

        private record AccountEntry(string? AccountSID, string? AccountName, int? AccountNumber, string? BankName);

        private record AccountExportContext(AccountExportMode Mode, List<AccountEntry> Accounts);
        #endregion
    }
}
