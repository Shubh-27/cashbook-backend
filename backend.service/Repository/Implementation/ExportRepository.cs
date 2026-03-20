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
            // 1. Determine if specific account is selected
            var accountInfo = await IsSelectedAccountExportAsync(request);
            var isSelectedAccount = accountInfo.IsSelected;
            var selectedAccountName = accountInfo.AccountName;
            var selectedAccountNumber = accountInfo.AccountNumber;
            var selectedBankName = accountInfo.BankName;
            
            // 2. Fetch data with filters
            // We ignore DescriptionSID filter if an account is selected (as per user request)
            // Actually, user said "ignore selected description" even for Case A/B generally if it means we group by description anyway.
            var filtersToApply = request.Filters?.Where(f => 
                !(f.Key == "description_sid" || f.Key == "DescriptionSID")
            ).ToList() ?? new List<FilterRequestModel>();

            var query = _unitOfWork.GetRepository<VwTransactionsList>().AsQueryable(enableTracking: false);
            query = query.ApplyFilters(filtersToApply);
            query = query.OrderBy(t => t.TransactionDate);

            var transactions = await query.ToListAsync();

            // 3. Financial year label
            var fy = GetFinancialYearString(request);

            // 4. Case A: ZIP of Excel files for all accounts
            if (!isSelectedAccount)
            {
                var (zipBytes, zipName) = GenerateAllAccountsZipExport(transactions, request, fy);
                if (!string.IsNullOrWhiteSpace(request.ExcelName))
                {
                    zipName = $"{request.ExcelName}.zip";
                }
                return (zipBytes, "application/zip", zipName);
            }
            
            // 5. Case B: Single Excel file with multiple description sheets
            var (excelBytes, excelName) = GenerateSingleAccountExcelExport(transactions, selectedAccountName, selectedAccountNumber, selectedBankName, fy, 0, request.SeparateSheets);
            if (!string.IsNullOrWhiteSpace(request.ExcelName))
            {
                excelName = $"{request.ExcelName}.xlsx";
            }
            return (excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
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
                    var (excelBytes, excelName) = GenerateSingleAccountExcelExport(group.ToList(), group.Key.AccountName, group.Key.AccountNumber, group.Key.BankName, fy, count, request.SeparateSheets);
                    var entry = archive.CreateEntry(excelName);
                    using var entryStream = entry.Open();
                    entryStream.Write(excelBytes, 0, excelBytes.Length);
                }
            }

            var fileName = $"Cashbook_AllAccounts_{fy}.zip";
            return (zipStream.ToArray(), fileName);
        }

        private (byte[] Bytes, string FileName) GenerateSingleAccountExcelExport(List<VwTransactionsList> transactions, string? accountName, int? accountNumber, string? bankName, string fy, int count, bool separateSheets)
        {
            using var workbook = new XLWorkbook();
            
            if (separateSheets)
            {
                // 1st Sheet: Full account transactions
                BuildAccountSheet(workbook, accountName, accountNumber, bankName, transactions, fy);
                
                // Rest of sheets: Group by description
                var descriptionGroups = transactions.GroupBy(x => new { x.DescriptionSID, x.DescriptionName }).ToList();
                foreach (var grp in descriptionGroups.OrderBy(g => g.Key.DescriptionName))
                {
                    if (string.IsNullOrEmpty(grp.Key.DescriptionName)) continue;
                    BuildDescriptionSheet(workbook, grp.Key.DescriptionName, grp.ToList(), fy);
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
                ws.Cell(currentRow, 3).Value = isAccountLayout ? t.DescriptionName : t.AccountName;
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

        private async Task<(bool IsSelected, string? AccountName, int? AccountNumber, string? BankName)> IsSelectedAccountExportAsync(ExportRequestModel request)
        {
            var filter = request.Filters?.FirstOrDefault(f =>
                f.Key == "account_sid" || f.Key == "AccountSID");

            if (filter != null && !string.IsNullOrEmpty(filter.Value?.ToString()))
            {
                var accountSid = filter.Value.ToString();
                var acc = await _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(a => a.AccountSID == accountSid);
                if (acc != null)
                {
                    return (true, acc.AccountName, acc.AccountNumber, acc.BankName);
                }
                return (true, null, null, null);
            }
            return (false, null, null, null);
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
    }
}
