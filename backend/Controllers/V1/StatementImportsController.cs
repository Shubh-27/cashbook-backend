using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/statement-imports")]
    public class StatementImportsController : BaseController
    {
        #region Variables & Constructor
        private readonly IStatementImportService _service;

        public StatementImportsController(IStatementImportService service)
        {
            _service = service;
        }
        #endregion

        #region Analyze & Commit
        /// <summary>
        /// Parses and analyzes a bank statement file for duplicate detection and column mapping.
        /// </summary>
        /// <param name="file">The uploaded CSV, XLS, or XLSX statement file.</param>
        /// <param name="sheetName">Optional worksheet name to analyze.</param>
        /// <param name="headerRow">Optional 1-based header row override.</param>
        /// <param name="transactionEndRow">Optional 1-based last transaction row override.</param>
        /// <param name="profileSID">Optional import profile identifier.</param>
        /// <param name="dateFormat">Optional date format override.</param>
        /// <param name="mappingJson">Optional JSON column mapping override.</param>
        /// <param name="accountSID">Optional target account identifier for duplicate detection.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The parsed statement analysis, suggested profile, and detected duplicates.</returns>
        [HttpPost("analyze")]
        [ProducesResponseType(typeof(StatementImportAnalysis), StatusCodes.Status200OK)]
        public async Task<IActionResult> Analyze(
            IFormFile file,
            [FromForm] string? sheetName,
            [FromForm] int? headerRow,
            [FromForm] int? transactionEndRow,
            [FromForm] string? profileSID,
            [FromForm] string? dateFormat,
            [FromForm] string? mappingJson,
            [FromForm] string? accountSID,
            CancellationToken cancellationToken)
        {
            return Ok(await _service.AnalyzeAsync(file, sheetName, headerRow, transactionEndRow, profileSID, dateFormat, mappingJson, accountSID, cancellationToken));
        }

        /// <summary>
        /// Commits validated statement rows as transactions within a tracked import batch.
        /// </summary>
        /// <param name="request">The validated statement rows and batch metadata to commit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Summary counts of imported, skipped, and invalid rows.</returns>
        [HttpPost("commit")]
        [ProducesResponseType(typeof(StatementImportCommitResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Commit([FromBody] CommitStatementImportRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _service.CommitAsync(request, cancellationToken));
        }
        #endregion

        #region Profiles
        /// <summary>
        /// Lists all active import profiles.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of active import profiles.</returns>
        [HttpGet("profiles")]
        [ProducesResponseType(typeof(IReadOnlyList<ImportProfileResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Profiles(CancellationToken cancellationToken)
        {
            return Ok(await _service.GetProfilesAsync(cancellationToken));
        }
        #endregion

        #region History & Undo
        /// <summary>
        /// Retrieves recent statement import batches.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of recent import batches.</returns>
        [HttpGet("history")]
        [ProducesResponseType(typeof(IReadOnlyList<ImportBatchResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> History(CancellationToken cancellationToken)
        {
            return Ok(await _service.GetHistoryAsync(cancellationToken));
        }

        /// <summary>
        /// Soft-deletes unmodified transactions from an import batch and marks it undone.
        /// </summary>
        /// <param name="batchSID">The unique identifier of the import batch to undo.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>OK if undone, or NotFound if the batch does not exist or was already undone.</returns>
        [HttpPost("history/{batchSID}/undo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Undo(string batchSID, CancellationToken cancellationToken)
            => await _service.UndoAsync(batchSID, cancellationToken) ? Ok(new { success = true }) : NotFound();
        #endregion
    }
}
