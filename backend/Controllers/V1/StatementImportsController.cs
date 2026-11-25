using backend.model.RequestModels;
using backend.model.ResponseModels;
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
            [FromForm] string? accountSID)
        {
            return Ok(await _service.AnalyzeAsync(file, sheetName, headerRow, transactionEndRow, profileSID, dateFormat, mappingJson, accountSID));
        }

        /// <summary>
        /// Commits validated statement rows as transactions within a tracked import batch.
        /// </summary>
        [HttpPost("commit")]
        [ProducesResponseType(typeof(StatementImportCommitResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Commit([FromBody] CommitStatementImportRequest request)
        {
            return Ok(await _service.CommitAsync(request));
        }
        #endregion

        #region Profiles
        /// <summary>
        /// Lists all active import profiles.
        /// </summary>
        [HttpGet("profiles")]
        [ProducesResponseType(typeof(IReadOnlyList<ImportProfileResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Profiles()
        {
            return Ok(await _service.GetProfilesAsync());
        }
        #endregion

        #region History & Undo
        /// <summary>
        /// Retrieves recent statement import batches.
        /// </summary>
        [HttpGet("history")]
        [ProducesResponseType(typeof(IReadOnlyList<ImportBatchResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> History()
        {
            return Ok(await _service.GetHistoryAsync());
        }

        /// <summary>
        /// Soft-deletes unmodified transactions from an import batch and marks it undone.
        /// </summary>
        [HttpPost("history/{batchSID}/undo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Undo(string batchSID) => await _service.UndoAsync(batchSID) ? Ok(new { success = true }) : NotFound();
        #endregion
    }
}
