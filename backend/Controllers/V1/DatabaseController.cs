using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class DatabaseController : BaseController
    {
        #region Variables & Constructor
        private readonly IDatabaseService _databaseService;

        public DatabaseController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }
        #endregion

        #region Export Database
        /// <summary>
        /// Exports the current SQLite database file as a downloadable file using VACUUM INTO for a safe, consistent backup.
        /// </summary>
        /// <returns>A downloadable .db file containing the database backup.</returns>
        [HttpGet("export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportDatabase()
        {
            var (fileBytes, contentType, fileName) = await _databaseService.ExportDatabaseAsync();
            return File(fileBytes, contentType, fileName);
        }
        #endregion

        #region Import Database
        /// <summary>
        /// Imports a SQLite database from an uploaded file, creating a safety backup and applying pending migrations.
        /// </summary>
        /// <param name="file">The uploaded SQLite .db file to import.</param>
        /// <returns>An IActionResult indicating the outcome of the import operation.</returns>
        [HttpPost("import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportDatabase(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            using var stream = file.OpenReadStream();
            await _databaseService.ImportDatabaseAsync(stream, file.FileName);

            return Ok(new { message = "Database restored and updated successfully." });
        }
        #endregion
    }
}
