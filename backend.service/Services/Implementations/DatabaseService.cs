using backend.common;
using backend.model.DbModels;
using backend.service.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.service.Services.Implementations
{
    public class DatabaseService : IDatabaseService
    {
        #region Variables & Constructor
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(AppDbContext context, ILogger<DatabaseService> logger)
        {
            _context = context;
            _logger = logger;
        }
        #endregion

        #region Export Database
        public async Task<(byte[] FileBytes, string ContentType, string FileName)> ExportDatabaseAsync()
        {
            var connectionString = _context.Database.GetDbConnection().ConnectionString;
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                _logger.LogWarning("Database file not found at: {DbPath}", dbPath);
                throw new HttpStatusCodeException(404, "Database file not found on server.");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.db");

            try
            {
                // Safely backup using VACUUM INTO
                // This creates a consistent copy of the database even if it's currently being written to.
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = $"VACUUM INTO '{tempPath}'";
                    await command.ExecuteNonQueryAsync();
                }

                var fileBytes = await File.ReadAllBytesAsync(tempPath);
                var fileName = $"bank_{DateTime.Now:yyyyMMdd_HHmmss}.db";

                return (fileBytes, "application/octet-stream", fileName);
            }
            finally
            {
                // Cleanup temp file
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        #endregion

        #region Import Database
        public async Task ImportDatabaseAsync(Stream fileStream, string fileName)
        {
            if (fileStream == null || fileStream.Length == 0)
                throw new HttpStatusCodeException(400, "No file uploaded.");

            if (!Path.GetExtension(fileName).Equals(".db", StringComparison.OrdinalIgnoreCase))
                throw new HttpStatusCodeException(400, "Invalid file type. Only .db files are allowed.");

            var connectionString = _context.Database.GetDbConnection().ConnectionString;
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (string.IsNullOrEmpty(dbPath))
                throw new HttpStatusCodeException(400, "Database path could not be determined.");

            // 1. Copy the incoming file to a temp/staging location
            var tempUploadPath = Path.Combine(Path.GetTempPath(), $"import_{Guid.NewGuid()}.db");
            using (var stream = new FileStream(tempUploadPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(stream);
            }

            // 2. Validate SQLite file and attempt migration against the staged copy
            try
            {
                using (var testConnection = new SqliteConnection($"Data Source={tempUploadPath};Pooling=False;"))
                {
                    await testConnection.OpenAsync();
                    using var command = testConnection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                    using var reader = await command.ExecuteReaderAsync();
                    if (!reader.HasRows)
                    {
                        throw new HttpStatusCodeException(400, "The uploaded file does not appear to be a valid SQLite database or is empty.");
                    }
                }

                // Run migrations against the staged copy to validate compatibility
                var stagingOptions = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={tempUploadPath};Pooling=False;")
                    .Options;

                using (var stagingContext = new AppDbContext(stagingOptions))
                {
                    await stagingContext.Database.MigrateAsync();
                }
            }
            catch (HttpStatusCodeException)
            {
                TryDeleteFile(tempUploadPath);
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempUploadPath);
                _logger.LogError(ex, "Database validation or migration failed on staged file {FileName}", fileName);
                throw new HttpStatusCodeException(400, $"Database validation or migration failed on uploaded file: {ex.Message}");
            }

            // 3. Close connection pools to allow file replacement
            SqliteConnection.ClearAllPools();

            // Ensure the target directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var backupPath = dbPath + ".bak";
            if (File.Exists(dbPath))
            {
                // Create a safety backup of the current live DB
                File.Copy(dbPath, backupPath, true);
                _logger.LogInformation("Existing database backed up to {BackupPath}", backupPath);
            }

            try
            {
                // Replace live database with validated & migrated staged copy
                File.Copy(tempUploadPath, dbPath, true);
                _logger.LogInformation("Live database successfully replaced with validated database from {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replace database file. Restoring backup if available.");
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, dbPath, true);
                }
                throw new HttpStatusCodeException(500, $"Failed to replace database file: {ex.Message}");
            }
            finally
            {
                TryDeleteFile(tempUploadPath);
                SqliteConnection.ClearAllPools();
            }

            _logger.LogInformation("Database successfully restored and ready from {FileName}", fileName);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Suppress secondary IO exception during cleanup
            }
        }
        #endregion
    }
}

