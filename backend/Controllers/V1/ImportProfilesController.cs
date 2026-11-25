using System.Text.Json;
using backend.common;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using backend.service.Services.Implementations;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/import-profiles")]
    // Cashbook is a single-user app; add authorization and profile ownership before multi-user deployment.
    public class ImportProfilesController : BaseController
    {
        #region Variables & Constructor
        private static readonly JsonSerializerOptions RuleJsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly IImportProfilePreviewService _previewService;
        private readonly IImportProfileRepository _importProfileRepository;

        public ImportProfilesController(
            IImportProfilePreviewService previewService,
            IImportProfileRepository importProfileRepository)
        {
            _previewService = previewService;
            _importProfileRepository = importProfileRepository;
        }
        #endregion

        #region Get Profiles List
        /// <summary>
        /// Retrieves a paginated list of active import profiles based on search, filter, and sorting criteria.
        /// </summary>
        /// <param name="request">The search parameters used to filter, sort, and paginate import profiles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A paged result of active import profiles.</returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<ImportProfileResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request, CancellationToken cancellationToken)
        {
            return Ok(await _importProfileRepository.SearchAsync(request, cancellationToken));
        }
        #endregion

        #region Preview Statement
        /// <summary>
        /// Parses and masks a sample bank statement file for import profile configuration.
        /// </summary>
        /// <param name="file">The uploaded CSV, XLS, or XLSX statement file.</param>
        /// <param name="worksheetName">Optional worksheet name to preview.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A masked preview of the statement grid with detected header and footer candidates.</returns>
        [HttpPost("preview")]
        [RequestSizeLimit(StatementGridParser.DefaultMaxFileBytes)]
        [ProducesResponseType(typeof(ImportProfilePreviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Preview(IFormFile file, [FromForm] string? worksheetName, CancellationToken cancellationToken)
        {
            return Ok(await _previewService.PreviewAsync(file, worksheetName, cancellationToken));
        }
        #endregion

        #region Create Profile
        /// <summary>
        /// Creates a new versioned import profile from a sample statement file and rule definition.
        /// </summary>
        /// <param name="file">The sample statement file.</param>
        /// <param name="rule">JSON-serialized import profile rule request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created profile SID and version number, or validation problems.</returns>
        [HttpPost]
        [RequestSizeLimit(StatementGridParser.DefaultMaxFileBytes)]
        [ProducesResponseType(typeof(ImportProfileCreateResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(IFormFile file, [FromForm] string rule, CancellationToken cancellationToken)
        {
            var request = JsonSerializer.Deserialize<ImportProfileRuleRequest>(rule, RuleJsonOptions);
            if (request == null) return BadRequest(new { error = "rule_invalid", message = "Provide a valid profile rule." });

            var result = await _importProfileRepository.CreateAsync(file, request, cancellationToken);
            return result.Success ? Ok(result) : BadRequest(new { error = "validation_failed", problems = result.Problems });
        }
        #endregion

        #region Update Profile
        /// <summary>
        /// Updates an import profile by creating a new immutable version.
        /// </summary>
        /// <param name="profileSID">The unique identifier of the import profile.</param>
        /// <param name="file">Optional new sample statement file when changing layout.</param>
        /// <param name="rule">JSON-serialized import profile rule request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated profile SID and new version number, or validation problems.</returns>
        [HttpPut("{profileSID}")]
        [RequestSizeLimit(StatementGridParser.DefaultMaxFileBytes)]
        [ProducesResponseType(typeof(ImportProfileCreateResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(string profileSID, IFormFile? file, [FromForm] string rule, CancellationToken cancellationToken)
        {
            var request = JsonSerializer.Deserialize<ImportProfileRuleRequest>(rule, RuleJsonOptions);
            if (request == null) return BadRequest(new { error = "rule_invalid", message = "Provide a valid profile rule." });

            var result = await _importProfileRepository.UpdateAsync(profileSID, file, request, cancellationToken);
            return result.Success ? Ok(result) : BadRequest(new { error = "validation_failed", problems = result.Problems });
        }
        #endregion

        #region Get Profile
        /// <summary>
        /// Retrieves the current version of an import profile.
        /// </summary>
        /// <param name="profileSID">The unique identifier of the import profile.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The import profile version details, or NotFound.</returns>
        [HttpGet("{profileSID}")]
        [ProducesResponseType(typeof(ImportProfileVersionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string profileSID, CancellationToken cancellationToken)
        {
            var result = await _importProfileRepository.GetAsync(profileSID, null, cancellationToken);
            return result != null ? Ok(result) : NotFound();
        }

        /// <summary>
        /// Retrieves a specific historical version of an import profile.
        /// </summary>
        /// <param name="profileSID">The unique identifier of the import profile.</param>
        /// <param name="versionNumber">The version number to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The import profile version details, or NotFound.</returns>
        [HttpGet("{profileSID}/versions/{versionNumber:int}")]
        [ProducesResponseType(typeof(ImportProfileVersionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVersion(string profileSID, int versionNumber, CancellationToken cancellationToken)
        {
            var result = await _importProfileRepository.GetAsync(profileSID, versionNumber, cancellationToken);
            return result != null ? Ok(result) : NotFound();
        }
        #endregion

        #region Delete Profile
        /// <summary>
        /// Soft-deletes the import profile with the specified identifier.
        /// </summary>
        /// <param name="profileSID">The unique identifier of the import profile to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ok with a success indicator if deleted; otherwise NotFound.</returns>
        [HttpDelete("{profileSID}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string profileSID, CancellationToken cancellationToken)
        {
            var deleted = await _importProfileRepository.DeleteAsync(profileSID, cancellationToken);
            return deleted ? Ok(new { success = true }) : NotFound();
        }
        #endregion
    }
}
