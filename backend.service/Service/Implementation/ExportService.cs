using System.Text;
using backend.common.Extensions;
using backend.common.Models;
using backend.model.Data;
using backend.model.Models.Views;
using backend.service.Service.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace backend.service.Service.Implementation
{
    public class ExportService : IExportService
    {
        private readonly AppDbContext _context;

        public ExportService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Public entry point (signature unchanged) ────────────────────────────
        public async Task<(byte[] FileContents, string ContentType, string FileName)> ExportTransactionsAsync(ExportRequestModel request)
        {
            // 1. Fetch data
            var query = _context.VwTransactionsList.AsNoTracking();
            query = query.ApplyFilters(request.Filters);
            query = query.OrderBy(x => x.TransactionDate);

            var transactions = await query.ToListAsync();

            // 2. Financial year label
            var fy = GetFinancialYearString(request);

            // 3. Route to CSV or Excel
            if (request.ExportType.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var (csvBytes, csvName) = GenerateCsvExport(transactions, request, fy);
                return (csvBytes, "text/csv", csvName);
            }
            else
            {
                var (excelBytes, excelName) = GenerateExcelExport(transactions, request, fy);
                return (excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }

        // ─── CSV (flat, unchanged behaviour) ─────────────────────────────────────
        private (byte[] Bytes, string FileName) GenerateCsvExport(
            List<VwTransactionsList> transactions,
            ExportRequestModel request,
            string fy)
        {
            var isSelectedAccount = IsSelectedAccountExport(request, out var accountName);
            var sb = new StringBuilder();

            sb.AppendLine("SR,Date,Account Name,Description Name,Debit,Credit,Balance,Remarks");

            int sr = 1;
            foreach (var t in transactions)
            {
                sb.AppendLine(
                    $"{sr++}," +
                    $"{FormatDate(t.TransactionDate)}," +
                    $"{EscapeCsv(t.AccountName)}," +
                    $"{EscapeCsv(t.DescriptionName)}," +
                    $"{t.Debit}," +
                    $"{t.Credit},," +
                    $"{EscapeCsv(t.Notes)}");
            }

            var fileName = isSelectedAccount
                ? $"Cashbook_{accountName}_{fy}.csv"
                : $"Cashbook_AllAccounts_{fy}.csv";

            return (Encoding.UTF8.GetBytes(sb.ToString()), fileName);
        }

        // ─── Excel main router ────────────────────────────────────────────────────
        private (byte[] Bytes, string FileName) GenerateExcelExport(
            List<VwTransactionsList> transactions,
            ExportRequestModel request,
            string fy)
        {
            using var workbook = new XLWorkbook();

            var isSelectedAccount    = IsSelectedAccountExport(request, out var accountName);
            var isSelectedDescription = IsSelectedDescriptionExport(request, out var descriptionName);

            string fileName;

            if (isSelectedAccount)
            {
                // ── Case B: single account ── Type 1 layout (group by account)
                BuildAccountSheet(workbook, accountName, transactions, fy);
                fileName = $"Cashbook_{SanitizeFileName(accountName)}_{fy}.xlsx";
            }
            else if (isSelectedDescription)
            {
                // ── Case B: single description ── Type 2 layout (group by description)
                BuildDescriptionSheet(workbook, descriptionName, transactions, fy);
                fileName = $"Cashbook_{SanitizeFileName(descriptionName)}_{fy}.xlsx";
            }
            else
            {
                // ── Case A: All Accounts + All Descriptions
                // Always exports Type 1 (by Account) AND Type 2 (by Description) together
                if (request.SeparateSheets)
                {
                    // Type 1 – one sheet per Account
                    foreach (var grp in transactions.GroupBy(x => new { x.AccountSID, x.AccountName }))
                        BuildAccountSheet(workbook, grp.Key.AccountName, grp.ToList(), fy);

                    // Type 2 – one sheet per Description
                    foreach (var grp in transactions.GroupBy(x => new { x.DescriptionSID, x.DescriptionName }))
                        BuildDescriptionSheet(workbook, grp.Key.DescriptionName, grp.ToList(), fy);
                }
                else
                {
                    // Single sheet: all Account blocks then all Description blocks
                    BuildSingleSheetCaseA(workbook, transactions, fy);
                }
                fileName = $"Cashbook_AllAccounts_AllDescriptions_{fy}.xlsx";
            }

            if (!workbook.Worksheets.Any())
                workbook.AddWorksheet("No Data");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return (stream.ToArray(), fileName);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  SHEET BUILDERS
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Type 1 layout – one sheet, grouped/titled by Account.
        /// Columns: SR | Date | Payment Detail (Desc) | Debit | Credit | Balance | Remarks
        /// </summary>
        private void BuildAccountSheet(
            XLWorkbook wb,
            string? accountName,
            List<VwTransactionsList> transactions,
            string fy)
        {
            string sheetName = GetUniqueSheetName(wb, accountName ?? "Account");
            var ws = wb.Worksheets.Add(sheetName);

            WriteSheetHeader(ws, accountName, fy);

            int currentRow = WriteColumnHeaders(ws, isAccountLayout: true);

            ws.SheetView.FreezeRows(currentRow - 1); // freeze up to and including column header row

            WriteTransactionRows(ws, ref currentRow, transactions, isAccountLayout: true);
            WriteFooterRow(ws, ref currentRow, transactions);

            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// Type 2 layout – one sheet, grouped/titled by Description.
        /// Columns: SR | Date | Account Name | Debit | Credit | Balance | Remarks
        /// </summary>
        private void BuildDescriptionSheet(
            XLWorkbook wb,
            string? descriptionName,
            List<VwTransactionsList> transactions,
            string fy)
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
        /// Case A, SeparateSheets=false.
        /// Single sheet: all Account blocks (Type 1) followed by all Description blocks (Type 2).
        /// 3 empty rows between every block.
        /// </summary>
        private void BuildSingleSheetCaseA(
            XLWorkbook wb,
            List<VwTransactionsList> transactions,
            string fy)
        {
            var ws = wb.Worksheets.Add("All Transactions");
            int currentRow = 1;

            // ── Type 1: one block per Account ────────────────────────────────────
            var accountGroups = transactions
                .GroupBy(x => new { x.AccountSID, x.AccountName })
                .ToList();

            foreach (var grp in accountGroups)
            {
                var list = grp.ToList();

                WriteSheetHeaderInline(ws, ref currentRow, grp.Key.AccountName, fy);
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: true);
                WriteTransactionRows(ws, ref currentRow, list, isAccountLayout: true);
                WriteFooterRow(ws, ref currentRow, list);

                currentRow += 3; // 3 empty rows separator
            }

            // ── Type 2: one block per Description ────────────────────────────────
            var descGroups = transactions
                .GroupBy(x => new { x.DescriptionSID, x.DescriptionName })
                .ToList();

            foreach (var grp in descGroups)
            {
                var list = grp.ToList();

                WriteSheetHeaderInline(ws, ref currentRow, grp.Key.DescriptionName, fy);
                WriteColumnHeadersInline(ws, ref currentRow, isAccountLayout: false);
                WriteTransactionRows(ws, ref currentRow, list, isAccountLayout: false);
                WriteFooterRow(ws, ref currentRow, list);

                currentRow += 3; // 3 empty rows separator
            }

            ws.Columns().AdjustToContents();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ROW WRITERS
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>Row 1 header for a dedicated sheet (accounts for rows 1-2 being the header band).</summary>
        private void WriteSheetHeader(IXLWorksheet ws, string? title, string fy)
        {
            // Col 1-3: Account/Description name
            ws.Range(1, 1, 1, 3).Merge().Value = title ?? string.Empty;
            ws.Range(1, 1, 1, 3).Style.Font.Bold = true;
            ws.Range(1, 1, 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            // Col 4-5: FY label
            ws.Range(1, 4, 1, 5).Merge().Value = $"FY {fy}";
            ws.Range(1, 4, 1, 5).Style.Font.Bold = true;
            ws.Range(1, 4, 1, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Col 6-7: title repeated right-aligned
            ws.Range(1, 6, 1, 7).Merge().Value = title ?? string.Empty;
            ws.Range(1, 6, 1, 7).Style.Font.Bold = true;
            ws.Range(1, 6, 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        /// <summary>Inline header block used inside a single-sheet export.</summary>
        private void WriteSheetHeaderInline(IXLWorksheet ws, ref int currentRow, string? title, string fy)
        {
            ws.Range(currentRow, 1, currentRow, 3).Merge().Value = title ?? string.Empty;
            ws.Range(currentRow, 1, currentRow, 3).Style.Font.Bold = true;
            ws.Range(currentRow, 1, currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            ws.Range(currentRow, 4, currentRow, 5).Merge().Value = $"FY {fy}";
            ws.Range(currentRow, 4, currentRow, 5).Style.Font.Bold = true;
            ws.Range(currentRow, 4, currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(currentRow, 6, currentRow, 7).Merge().Value = title ?? string.Empty;
            ws.Range(currentRow, 6, currentRow, 7).Style.Font.Bold = true;
            ws.Range(currentRow, 6, currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            currentRow++;
        }

        /// <summary>
        /// Writes column header row for a dedicated sheet.
        /// Returns the next available row after headers (i.e. first data row).
        /// </summary>
        private int WriteColumnHeaders(IXLWorksheet ws, bool isAccountLayout)
        {
            int headerRow = 3; // Row 1 = sheet header, Row 2 = blank gap
            WriteColumnHeaderRow(ws, headerRow, isAccountLayout);
            return headerRow + 1;
        }

        /// <summary>Writes column header row inline (single-sheet mode) and advances currentRow.</summary>
        private void WriteColumnHeadersInline(IXLWorksheet ws, ref int currentRow, bool isAccountLayout)
        {
            WriteColumnHeaderRow(ws, currentRow, isAccountLayout);
            currentRow++;
        }

        private void WriteColumnHeaderRow(IXLWorksheet ws, int row, bool isAccountLayout)
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
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        }

        /// <summary>Writes all transaction rows, SR resets to 1.</summary>
        private void WriteTransactionRows(
            IXLWorksheet ws,
            ref int currentRow,
            List<VwTransactionsList> transactions,
            bool isAccountLayout)
        {
            int sr = 1;
            foreach (var t in transactions)
            {
                ws.Cell(currentRow, 1).Value = sr++;
                ws.Cell(currentRow, 2).Value = FormatDate(t.TransactionDate);
                ws.Cell(currentRow, 3).Value = isAccountLayout ? t.DescriptionName : t.AccountName;
                ws.Cell(currentRow, 4).Value = t.Debit   ?? 0;
                ws.Cell(currentRow, 5).Value = t.Credit  ?? 0;
                ws.Cell(currentRow, 6).Value = string.Empty; // Balance – empty per transaction row
                ws.Cell(currentRow, 7).Value = t.Notes   ?? string.Empty;

                var range = ws.Range(currentRow, 1, currentRow, 7);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;

                currentRow++;
            }
        }

        /// <summary>
        /// Footer row: blank SR/Date/Detail | Sum Debit | Sum Credit | Credit-Debit | blank Remarks
        /// Styled bold with thin borders.
        /// </summary>
        private void WriteFooterRow(
            IXLWorksheet ws,
            ref int currentRow,
            List<VwTransactionsList> transactions)
        {
            double totalDebit  = transactions.Sum(t => t.Debit  ?? 0);
            double totalCredit = transactions.Sum(t => t.Credit ?? 0);
            double balance     = totalCredit - totalDebit;

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
            range.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;

            currentRow++;
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════════════

        private string FormatDate(string? rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return string.Empty;
            if (DateTime.TryParse(rawDate, out DateTime dt))
                return dt.ToString("dd-MMM-yyyy");
            return rawDate;
        }

        private string GetUniqueSheetName(XLWorkbook wb, string baseName)
        {
            string safeName  = SanitizeSheetName(string.IsNullOrWhiteSpace(baseName) ? "Sheet" : baseName);
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

        private bool IsSelectedAccountExport(ExportRequestModel request, out string? accountName)
        {
            accountName = null;
            var filter = request.Filters?.FirstOrDefault(f =>
                f.Key == "account_sid" || f.Key == "AccountSID");

            if (filter != null && !string.IsNullOrEmpty(filter.Value?.ToString()))
            {
                var acc = _context.Accounts.FirstOrDefault(a => a.AccountSID == filter.Value.ToString());
                if (acc != null) accountName = acc.AccountName;
                return true;
            }
            return false;
        }

        private bool IsSelectedDescriptionExport(ExportRequestModel request, out string? descriptionName)
        {
            descriptionName = null;
            var filter = request.Filters?.FirstOrDefault(f =>
                f.Key == "description_sid" || f.Key == "DescriptionSID");

            if (filter != null && !string.IsNullOrEmpty(filter.Value?.ToString()))
            {
                var desc = _context.Descriptions.FirstOrDefault(d => d.DescriptionSID == filter.Value.ToString());
                if (desc != null) descriptionName = desc.DescriptionName;
                return true;
            }
            return false;
        }

        private string GetFinancialYearString(ExportRequestModel request)
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

        private string SanitizeSheetName(string name)
        {
            foreach (var c in new[] { '\\', '/', '?', '*', '[', ']', ':' })
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private string SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Export";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
    }
}