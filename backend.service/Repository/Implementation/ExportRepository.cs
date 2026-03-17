using backend.common.Models;
using backend.model.Models;
using backend.model.Models.Views;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using ClosedXML.Excel;
using System.Text;

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
            // 1. Fetch data
            var transactions = await _unitOfWork.GetRepository<VwTransactionsList>().GetAllAsync(
                predicate: null,
                orderBy: x => x.OrderBy(t => t.TransactionDate),
                enableTracking: false
            );

            // 2. Financial year label
            var fy = GetFinancialYearString(request);

            // 3. Route to CSV or Excel
            if (request.ExportType.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var (csvBytes, csvName) = GenerateCsvExport(transactions.ToList(), request, fy);
                return (csvBytes, "text/csv", csvName);
            }
            else
            {
                var (excelBytes, excelName) = GenerateExcelExport(transactions.ToList(), request, fy);
                return (excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }

        #region Helpers for Export Transactions (Excel/CSV)

        #region Genrate CSV
        private (byte[] Bytes, string FileName) GenerateCsvExport(List<VwTransactionsList> transactions, ExportRequestModel request, string fy)
        {
            var isSelectedAccount = IsSelectedAccountExport(request, out var accountName, out var accountNumber, out var bankName);
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
        #endregion

        #region Genrate Excel
        private (byte[] Bytes, string FileName) GenerateExcelExport(List<VwTransactionsList> transactions, ExportRequestModel request, string fy)
        {
            using var workbook = new XLWorkbook();

            var isSelectedAccount = IsSelectedAccountExport(request, out var accountName, out var accountNumber, out var bankName);
            var isSelectedDescription = IsSelectedDescriptionExport(request, out var descriptionName);

            string fileName;

            if (isSelectedAccount)
            {
                // ── Case B: single account ── Type 1 layout (group by account)
                BuildAccountSheet(workbook, accountName, accountNumber, bankName, transactions, fy);
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
                    foreach (var grp in transactions.GroupBy(x => new { x.AccountSID, x.AccountName, x.AccountNumber, x.BankName }))
                        BuildAccountSheet(workbook, grp.Key.AccountName, grp.Key.AccountNumber, grp.Key.BankName, grp.ToList(), fy);

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
        #endregion

        #region Sheet & Row Builders for Excel
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

        private static void BuildSingleSheetCaseA(XLWorkbook wb, List<VwTransactionsList> transactions, string fy)
        {
            var ws = wb.Worksheets.Add("All Transactions");
            int currentRow = 1;

            // ── Type 1: one block per Account ────────────────────────────────────
            var accountGroups = transactions
                .GroupBy(x => new { x.AccountSID, x.AccountName, x.AccountNumber, x.BankName })
                .ToList();

            foreach (var grp in accountGroups)
            {
                var list = grp.ToList();

                WriteSheetHeaderInline(ws, ref currentRow, grp.Key.AccountName, fy, grp.Key.AccountNumber, grp.Key.BankName);
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
        #endregion

        #region Row & Sheet Writers for Excel
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

        #region Helpers for Export Transactions (Excel/CSV)
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

        private bool IsSelectedAccountExport(ExportRequestModel request, out string? accountName, out int? accountNumber, out string? bankName)
        {
            accountName = null;
            accountNumber = null;
            bankName = null;
            var filter = request.Filters?.FirstOrDefault(f =>
                f.Key == "account_sid" || f.Key == "AccountSID");

            if (filter != null && !string.IsNullOrEmpty(filter.Value?.ToString()))
            {
                var accountSid = filter.Value.ToString();
                var acc = _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(a => a.AccountSID == accountSid).Result;
                if (acc != null)
                {
                    accountName = acc.AccountName;
                    accountNumber = acc.AccountNumber;
                    bankName = acc.BankName;
                }
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
                var descriptionSid = filter.Value.ToString();
                var desc = _unitOfWork.GetRepository<Descriptions>().SingleOrDefaultAsync(d => d.DescriptionSID == descriptionSid).Result;
                if (desc != null) descriptionName = desc.DescriptionName;
                return true;
            }
            return false;
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

        private static string SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Export";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
        #endregion

        #endregion

        #endregion
    }
}
