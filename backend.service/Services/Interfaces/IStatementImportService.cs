using backend.model.RequestModel;
using backend.model.ResponseModel;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Interfaces
{
    public interface IStatementImportService
    {
        /// <summary>
        /// Parses and analyzes a bank statement file for duplicate detection and column mapping.
        /// </summary>
        Task<StatementImportAnalysis> AnalyzeAsync(
            IFormFile file,
            string? sheetName,
            int? headerRow,
            int? transactionEndRow,
            string? profileSID,
            string? dateFormat,
            string? mappingJson,
            string? accountSID,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits validated statement rows as transactions within a tracked import batch.
        /// </summary>
        Task<StatementImportCommitResult> CommitAsync(CommitStatementImportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all active import profiles.
        /// </summary>
        Task<IReadOnlyList<ImportProfileResponse>> GetProfilesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves recent statement import batches.
        /// </summary>
        Task<IReadOnlyList<ImportBatchResponse>> GetHistoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes unmodified transactions from an import batch and marks it undone.
        /// </summary>
        Task<bool> UndoAsync(string batchSID, CancellationToken cancellationToken = default);
    }
}
