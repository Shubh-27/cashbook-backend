using System.Text.RegularExpressions;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Implementations;

public sealed class ImportProfilePreviewService(IStatementGridParser parser, IStatementMaskingService masking) : IImportProfilePreviewService
{
    private const int PreviewFirstRowCount = 100;
    private const int PreviewLastRowCount = 100;

    public async Task<ImportProfilePreviewResponse> PreviewAsync(IFormFile file, string? worksheetName, CancellationToken cancellationToken = default)
    {
        var parsed = await parser.ParseWithMetadataAsync(file, cancellationToken);
        var grids = parsed.Grids;
        var selected = grids.FirstOrDefault(grid => string.Equals(grid.Name, worksheetName, StringComparison.OrdinalIgnoreCase)) ?? grids.First();
        var first = selected.Rows.Take(PreviewFirstRowCount).Select(Mask).ToList();
        var last = selected.Rows.TakeLast(PreviewLastRowCount).Where(row => row.RowNumber > PreviewFirstRowCount).Select(Mask).ToList();
        var header = FindHeader(selected.Rows);
        var footerCandidates = FindFooterCandidates(selected.Rows, header).Take(10).ToList();
        var dateValues = selected.Rows.SelectMany(row => row.Cells).Where(cell => Regex.IsMatch(cell.Trim(), @"^\d{1,2}[-/]\d{1,2}[-/]\d{2,4}$"));
        return new ImportProfilePreviewResponse
        {
            CellValueFormat = "formatted_text",
            FileType = parsed.FileType,
            Worksheets = grids.Select(grid => grid.Name).ToList(),
            SelectedWorksheet = selected.Name,
            FirstRows = first,
            LastRows = last,
            DividerAfterRow = first.Count == 0 ? null : first[^1].RowNumber,
            TotalRows = selected.Rows.Count,
            DetectedHeaderRow = header,
            FooterCandidateRows = footerCandidates,
            SecondaryTableCandidateRows = FindSecondaryTables(selected.Rows, header).Take(10).ToList(),
            HasAmbiguousDateFormat = parser.HasAmbiguousDayMonthDates(dateValues)
        };
    }

    private StatementGridRow Mask(StatementGridRow row) => new() { RowNumber = row.RowNumber, Cells = masking.MaskRow(row.Cells) };

    private static int? FindHeader(IReadOnlyList<StatementGridRow> rows) => rows.Take(PreviewFirstRowCount)
        .Select(row => new { row.RowNumber, Score = row.Cells.Sum(HeaderScore) })
        .Where(row => row.Score >= 3).OrderByDescending(row => row.Score).ThenBy(row => row.RowNumber).Select(row => (int?)row.RowNumber).FirstOrDefault();

    private static IEnumerable<int> FindFooterCandidates(IReadOnlyList<StatementGridRow> rows, int? headerRow)
    {
        if (!headerRow.HasValue) return [];
        return rows.Where(row => row.RowNumber > headerRow && row.Cells.Count(cell => !string.IsNullOrWhiteSpace(cell)) == 1)
            .Where(row => row.Cells.First(cell => !string.IsNullOrWhiteSpace(cell)).Trim().Length >= 12)
            .Select(row => row.RowNumber);
    }

    private static IEnumerable<int> FindSecondaryTables(IReadOnlyList<StatementGridRow> rows, int? headerRow) => rows
        .Where(row => !headerRow.HasValue || row.RowNumber > headerRow.Value)
        .Where(row => row.Cells.Sum(HeaderScore) >= 3)
        .Select(row => row.RowNumber);

    private static int HeaderScore(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        return normalized.Contains("date") || normalized.Contains("narration") || normalized.Contains("description") || normalized.Contains("particular") ? 2
            : normalized.Contains("debit") || normalized.Contains("credit") || normalized.Contains("balance") || normalized.Contains("amount") || normalized is "dr" or "cr" ? 1 : 0;
    }
}
