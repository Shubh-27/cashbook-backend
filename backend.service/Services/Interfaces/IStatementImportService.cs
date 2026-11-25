using backend.model.RequestModels;
using backend.model.ResponseModels;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Interfaces;

public interface IStatementImportService
{
    Task<StatementImportAnalysis> AnalyzeAsync(IFormFile file, string? sheetName, int? headerRow, int? transactionEndRow, string? profileSID, string? dateFormat, string? mappingJson, string? accountSID);
    Task<StatementImportCommitResult> CommitAsync(CommitStatementImportRequest request);
    Task<IReadOnlyList<ImportProfileResponse>> GetProfilesAsync();
    Task<IReadOnlyList<ImportBatchResponse>> GetHistoryAsync();
    Task<bool> UndoAsync(string batchSID);
}
