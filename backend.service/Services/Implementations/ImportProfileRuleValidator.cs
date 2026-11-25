using System.Globalization;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Services.Interfaces;

namespace backend.service.Services.Implementations;

public sealed class ImportProfileRuleValidator : IImportProfileRuleValidator
{
    public IReadOnlyList<ImportProfileRuleProblem> Validate(ImportProfileRuleRequest rule, StatementWorksheetGrid grid, bool ambiguousDates)
    {
        var problems = new List<ImportProfileRuleProblem>();
        // Persistence pass extension point: check active-profile name uniqueness here, not in this pure validator.
        if (rule.HeaderRow < 1 || rule.HeaderRow > grid.Rows.Count) problems.Add(new("header_row_invalid", "Select a header row inside the worksheet."));
        if (rule.EndRow < 1 || rule.EndRow > grid.Rows.Count) problems.Add(new("end_row_invalid", "Select an end row inside the worksheet."));
        if (rule.HeaderRow >= rule.EndRow) problems.Add(new("row_order_invalid", "Transactions must end after the header row."));
        var header = grid.Rows.FirstOrDefault(row => row.RowNumber == rule.HeaderRow);
        var count = header?.Cells.Count ?? 0; // Trailing blank cells count because parser preserves physical columns.
        problems.AddRange(ValidateCoreRules(rule, count));
        if (ambiguousDates && string.IsNullOrWhiteSpace(rule.DateFormat)) problems.Add(new("date_format_required", "Choose a date format for ambiguous dates."));
        if (rule.SecondaryTableMarkerRow.HasValue && (rule.SecondaryTableMarkerRow < 1 || rule.SecondaryTableMarkerRow > grid.Rows.Count || rule.SecondaryTableMarkerRow == rule.HeaderRow)) problems.Add(new("secondary_marker_invalid", "Choose a secondary-table marker inside the worksheet that is not the header row."));
        if (rule.Date.HasValue && rule.HeaderRow < rule.EndRow && rule.HeaderRow >= 1 && rule.EndRow <= grid.Rows.Count && rule.Date.Value >= 0 && rule.Date.Value < count && !grid.Rows.Where(row => row.RowNumber > rule.HeaderRow && row.RowNumber <= rule.EndRow).Any(row => ParsesDate(row.Cells.ElementAtOrDefault(rule.Date.Value), rule.DateFormat))) problems.Add(new("date_row_missing", "No transaction row has a date matching the selected format."));
        return problems;
    }

    public IReadOnlyList<ImportProfileRuleProblem> ValidateCoreRules(ImportProfileRuleRequest rule, int headerCellCount)
    {
        var problems = new List<ImportProfileRuleProblem>();
        if (string.IsNullOrWhiteSpace(rule.Name)) problems.Add(new("name_required", "Enter a profile name."));
        if (!rule.Date.HasValue) problems.Add(new("date_required", "Map the Date column."));
        if (!rule.Description.HasValue) problems.Add(new("description_required", "Map the Description column."));
        var hasAmount = rule.Amount.HasValue; var hasSplit = rule.Debit.HasValue || rule.Credit.HasValue;
        if (hasAmount && hasSplit) problems.Add(new("amount_mapping_conflict", "Use either Amount or Debit/Credit columns, not both."));
        if (!hasAmount && !hasSplit) problems.Add(new("amount_mapping_required", "Map an Amount, Debit, or Credit column."));
        var indexes = new int?[] { rule.Date, rule.Description, rule.Debit, rule.Credit, rule.Amount, rule.Balance, rule.Notes }
            .Where(index => index.HasValue)
            .Select(index => index!.Value)
            .ToList();
        if (indexes.Any(index => index < 0 || index >= headerCellCount)) problems.Add(new("column_index_invalid", "A mapped column is outside the header row."));
        if (indexes.Distinct().Count() != indexes.Count) problems.Add(new("column_index_duplicate", "Each mapped field must use a different column."));
        return problems;
    }

    private static bool ParsesDate(string? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || (!string.IsNullOrWhiteSpace(format) && DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    }
}
