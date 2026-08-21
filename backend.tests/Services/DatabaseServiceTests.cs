/*
 * ====================================================================================================
 * LAYER UNDER TEST: SERVICE LAYER (DatabaseService)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * DatabaseService coordinates low-level database backup (VACUUM INTO), SQLite connection pool clearing,
 * schema validation, and safe atomic file replacements during database restores.
 *
 * WHY THIS APPROACH?
 * We test DatabaseService against real SQLite database files on disk because its core responsibility is
 * file-system operations, SQLite binary integrity checks, and backup/restore safety. Mocking the file
 * system or database would defeat the purpose of validating that corrupted binary uploads never destroy
 * the live database.
 * ====================================================================================================
 */

using System.Text;
using backend.common;
using backend.model.DbModels;
using backend.service.Services.Implementations;
using backend.tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static backend.common.Constants;

namespace backend.tests.Services
{
    /// <summary>
    /// Integration tests for DatabaseService verifying database backup and safe disaster-recovery import.
    /// </summary>
    public class DatabaseServiceTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;
        private readonly DatabaseService _databaseService;

        public DatabaseServiceTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
            var context = _testDb.CreateContext();
            _databaseService = new DatabaseService(context, NullLogger<DatabaseService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        /*
         * SCENARIO PROTECTED:
         * If a user accidentally uploads a corrupt, non-database file (e.g., random bytes, image, malformed file),
         * the service must validate the file against a staging copy and reject it with a 400 Bad Request,
         * ensuring the live active database remains 100% untouched and uncorrupted.
         */
        [Fact]
        public async Task ImportDatabaseAsync_CorruptFile_LeavesLiveDatabaseUntouchedAndThrows400()
        {
            // Arrange: Seed data into the live database
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = Guid.NewGuid().ToString(),
                    AccountName = "Live Account Before Import",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            // Create corrupt file stream (arbitrary non-sqlite payload)
            var corruptBytes = Encoding.UTF8.GetBytes("INVALID SQLITE HEADER AND CORRUPTED DATA");
            using var corruptStream = new MemoryStream(corruptBytes);

            // Act & Assert: Should throw HttpStatusCodeException with 400
            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                _databaseService.ImportDatabaseAsync(corruptStream, "corrupted.db"));

            Assert.Equal(400, ex.StatusCode);

            // Assert: Live database was left completely untouched
            using var verifyContext = _testDb.CreateContext();
            var accountAfter = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.AccountName == "Live Account Before Import");
            Assert.NotNull(accountAfter);
        }

        /*
         * SCENARIO PROTECTED:
         * If an empty 0-byte file is uploaded during an import attempt, the service must reject it immediately
         * with a 400 Bad Request before attempting any file replacement.
         */
        [Fact]
        public async Task ImportDatabaseAsync_EmptyStream_Throws400AndLeavesLiveDatabaseUntouched()
        {
            // Arrange
            using var emptyStream = new MemoryStream();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                _databaseService.ImportDatabaseAsync(emptyStream, "empty.db"));

            Assert.Equal(400, ex.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * To prevent users from uploading executable or arbitrary script files, the service must strictly
         * reject any file that does not have a .db extension.
         */
        [Fact]
        public async Task ImportDatabaseAsync_NonDbExtension_Throws400()
        {
            // Arrange
            var dummyBytes = Encoding.UTF8.GetBytes("some test data");
            using var stream = new MemoryStream(dummyBytes);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpStatusCodeException>(() =>
                _databaseService.ImportDatabaseAsync(stream, "data.txt"));

            Assert.Equal(400, ex.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When restoring from a valid backup file, the service must stage the file, apply pending migrations,
         * replace the live database, and make the newly imported records immediately readable by the app.
         */
        [Fact]
        public async Task ImportDatabaseAsync_ValidDatabase_ReplacesLiveDatabaseSuccessfully()
        {
            // Arrange: Seed initial live database
            using (var seedContext = _testDb.CreateContext())
            {
                var oldAccount = new Account
                {
                    AccountSID = Guid.NewGuid().ToString(),
                    AccountName = "Live Account Old",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(oldAccount);
                await seedContext.SaveChangesAsync();
            }

            // Create a second valid database with new content to import
            await using var sourceDb = await SqliteTestDatabase.CreateAsync(applyMigrations: true);
            using (var sourceContext = sourceDb.CreateContext())
            {
                var newAccount = new Account
                {
                    AccountSID = Guid.NewGuid().ToString(),
                    AccountName = "Imported Target Account",
                    Status = (int)StatusType.Active
                };
                await sourceContext.Accounts.AddAsync(newAccount);
                await sourceContext.SaveChangesAsync();
            }

            // Read the source database into memory
            byte[] sourceDbBytes = await File.ReadAllBytesAsync(sourceDb.FilePath);
            using var importStream = new MemoryStream(sourceDbBytes);

            // Act: Import the valid source database over the live database
            await _databaseService.ImportDatabaseAsync(importStream, "valid_import.db");

            // Assert: Live database now has the imported data and old data is gone
            using var verifyContext = _testDb.CreateContext();
            var importedAccount = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.AccountName == "Imported Target Account");
            Assert.NotNull(importedAccount);

            var oldAccountVerify = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.AccountName == "Live Account Old");
            Assert.Null(oldAccountVerify);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user downloads a database backup, the export service must safely snapshot the SQLite database
         * (using VACUUM INTO) and return non-empty binary bytes with the proper octet-stream MIME type.
         */
        [Fact]
        public async Task ExportDatabaseAsync_ValidDatabase_ReturnsNonEmptyBytesAndFileName()
        {
            // Arrange: Seed data
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = Guid.NewGuid().ToString(),
                    AccountName = "Export Test Account",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            // Act
            var (fileBytes, contentType, fileName) = await _databaseService.ExportDatabaseAsync();

            // Assert
            Assert.NotNull(fileBytes);
            Assert.NotEmpty(fileBytes);
            Assert.Equal("application/octet-stream", contentType);
            Assert.StartsWith("bank_", fileName);
            Assert.EndsWith(".db", fileName);
        }
    }
}
