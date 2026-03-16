using backend.model.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class DatabaseController : BaseController
    {
        #region Variables & Constructor
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(AppDbContext context, ILogger<DatabaseController> logger)
        {
            _context = context;
            _logger = logger;
        }
        #endregion

        #region Export Database
        /// <summary>
        /// Exports the current SQLite database file as a downloadable file. This endpoint creates a consistent backup of the database even if it's currently being written to, using the VACUUM INTO command. The exported file is named with a timestamp for easy identification. If the database file cannot be found or an error occurs during export, appropriate error responses are returned.
        /// </summary>
        /// <returns>A downloadable file containing the database backup.</returns>
        [HttpGet("export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportDatabase()
        {
            try
            {
                var connectionString = _context.Database.GetDbConnection().ConnectionString;
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var dbPath = builder.DataSource;

                if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
                {
                    _logger.LogWarning("Database file not found at: {DbPath}", dbPath);
                    return NotFound("Database file not found on server.");
                }

                var tempPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.db");

                // Safely backup using VACUUM INTO
                // This creates a consistent copy of the database even if it's currently being written to.
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"VACUUM INTO '{tempPath}'";
                        await command.ExecuteNonQueryAsync();
                    }
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempPath);

                // Cleanup temp file
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }

                return File(fileBytes, "application/octet-stream", $"bank_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting database");
                return StatusCode(500, "Internal server error during export");
            }
        }
        #endregion

        #region Import Database
        /// <summary>
        /// Imports a SQLite database from the uploaded file and replaces the current database with its contents.
        /// </summary>
        /// <remarks>A backup of the existing database is created before replacement. Only .db files are
        /// accepted, and the uploaded file is validated as a SQLite database. After import, restarting the application
        /// may be necessary if issues occur. The method clears all SQLite connection pools to avoid file locks during
        /// replacement.</remarks>
        /// <param name="file">The uploaded file containing the SQLite database to import. Must be a non-empty file with a .db extension.</param>
        /// <returns>An IActionResult indicating the outcome of the import operation. Returns BadRequest if the file is invalid
        /// or content validation fails; returns Ok if the database is successfully restored; returns StatusCode(500)
        /// for internal errors.</returns>
        [HttpPost("import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ImportDatabase(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!Path.GetExtension(file.FileName).Equals(".db", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Invalid file type. Only .db files are allowed.");

            try
            {
                var connectionString = _context.Database.GetDbConnection().ConnectionString;
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var dbPath = builder.DataSource;

                if (string.IsNullOrEmpty(dbPath))
                    return BadRequest("Database path could not be determined.");

                // Validate the uploaded file by trying to open it as a SQLite database
                var tempUploadPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
                using (var stream = new FileStream(tempUploadPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                try
                {
                    using var testConnection = new SqliteConnection($"Data Source={tempUploadPath}");
                    await testConnection.OpenAsync();
                    using var command = testConnection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                    using var reader = await command.ExecuteReaderAsync();
                    if (!reader.HasRows)
                    {
                        throw new Exception("The uploaded file does not appear to be a valid SQLite database or is empty.");
                    }
                }
                catch (Exception ex)
                {
                    if (System.IO.File.Exists(tempUploadPath)) System.IO.File.Delete(tempUploadPath);
                    return BadRequest($"Content validation failed: {ex.Message}");
                }

                // Close all connections to the database to allow replacement
                // This is crucial for SQLite as it might have file locks
                SqliteConnection.ClearAllPools();

                // Ensure the target directory exists
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Replace the database file
                if (System.IO.File.Exists(dbPath))
                {
                    // Create a safety backup of the current DB just in case
                    var backupPath = dbPath + ".bak";
                    System.IO.File.Copy(dbPath, backupPath, true);
                    _logger.LogInformation("Existing database backed up to {BackupPath}", backupPath);
                }

                System.IO.File.Copy(tempUploadPath, dbPath, true);
                System.IO.File.Delete(tempUploadPath);

                _logger.LogInformation("Database successfully restored from {FileName}", file.FileName);

                return Ok(new { message = "Database restored successfully. Please restart the application if you encounter any issues." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing database");
                return StatusCode(500, $"Internal server error during import: {ex.Message}");
            }
        }
        #endregion
    }
}
