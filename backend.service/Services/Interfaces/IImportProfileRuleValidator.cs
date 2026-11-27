using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Services.Interfaces;

public interface IImportProfileRuleValidator
{
    IReadOnlyList<ImportProfileRuleProblem> Validate(ImportProfileRuleRequest rule, StatementWorksheetGrid grid, bool ambiguousDates);
    IReadOnlyList<ImportProfileRuleProblem> ValidateCoreRules(ImportProfileRuleRequest rule, int headerCellCount);
}
