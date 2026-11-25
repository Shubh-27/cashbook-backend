using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Services.Implementations;
using Xunit;

namespace backend.tests.Services;

public class ImportProfileRuleValidatorTests
{
    private static StatementWorksheetGrid Grid() => new() { Name = "CSV", Rows = [new() { RowNumber = 1, Cells = ["Date", "Description", "Debit", "", ""] }, new() { RowNumber = 2, Cells = ["02/04/2024", "Coffee", "350"] }, new() { RowNumber = 3, Cells = ["03/04/2024", "Tea", "100"] }] };
    private static ImportProfileRuleRequest Valid() => new() { Name = "Bank", HeaderRow = 1, EndRow = 3, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 };
    private static IReadOnlyList<ImportProfileRuleProblem> Problems(ImportProfileRuleRequest rule, bool ambiguous = false) => new ImportProfileRuleValidator().Validate(rule, Grid(), ambiguous);

    [Fact] public void ValidRule_HasNoProblems() => Assert.Empty(Problems(Valid()));
    [Fact] public void NameRule_HasCode() { var r = Valid(); r.Name = " "; Assert.Contains(Problems(r), p => p.Code == "name_required"); }
    [Fact] public void RowRules_HaveCodes() { var r = Valid(); r.HeaderRow = 3; r.EndRow = 2; var p = Problems(r); Assert.Contains(p, x => x.Code == "row_order_invalid"); r.HeaderRow = 0; Assert.Contains(Problems(r), x => x.Code == "header_row_invalid"); }
    [Fact] public void RequiredMappings_HaveCodes() { var r = Valid(); r.Date = null; r.Description = null; r.Debit = null; var p = Problems(r); Assert.Contains(p, x => x.Code == "date_required"); Assert.Contains(p, x => x.Code == "description_required"); Assert.Contains(p, x => x.Code == "amount_mapping_required"); }
    [Fact] public void AmountXor_HasCode() { var r = Valid(); r.Amount = 3; Assert.Contains(Problems(r), p => p.Code == "amount_mapping_conflict"); }
    [Fact] public void IndexRules_HaveCodes() { var r = Valid(); r.Notes = 9; r.Balance = 2; var p = Problems(r); Assert.Contains(p, x => x.Code == "column_index_invalid"); Assert.Contains(p, x => x.Code == "column_index_duplicate"); }
    [Fact] public void AmbiguousDate_HasCode() { var r = Valid(); r.DateFormat = null; Assert.Contains(Problems(r, true), p => p.Code == "date_format_required"); }
    [Fact] public void DateRowRule_HasCode() { var r = Valid(); r.DateFormat = "yyyy-MM-dd"; Assert.Contains(Problems(r), p => p.Code == "date_row_missing"); }
    [Fact] public void SecondaryMarker_HasCode() { var r = Valid(); r.SecondaryTableMarkerRow = 1; Assert.Contains(Problems(r), p => p.Code == "secondary_marker_invalid"); }
    [Fact] public void MultipleProblems_AreAllReturned() { var r = Valid(); r.Name = ""; r.Date = null; r.Description = null; var p = Problems(r); Assert.Contains(p, x => x.Code == "name_required"); Assert.Contains(p, x => x.Code == "date_required"); Assert.Contains(p, x => x.Code == "description_required"); }
    [Fact] public void IsoAndDayFirstDates_PassDateRule() { var r = Valid(); r.DateFormat = null; var iso = Grid(); iso.Rows[1].Cells[0] = "2024-04-02"; Assert.DoesNotContain(new ImportProfileRuleValidator().Validate(r, iso, false), p => p.Code == "date_row_missing"); Assert.DoesNotContain(Problems(Valid()), p => p.Code == "date_row_missing"); }
    [Fact] public void HeaderTrailingBlankCells_CountForIndexes() { var r = Valid(); r.Notes = 4; Assert.DoesNotContain(Problems(r), p => p.Code == "column_index_invalid"); }
}
