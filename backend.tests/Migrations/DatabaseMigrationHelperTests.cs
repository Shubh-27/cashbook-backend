using backend.model.DbModels;
using backend.service.Helpers;
using backend.tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.tests.Migrations
{
    public class DatabaseMigrationHelperTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;

        public DatabaseMigrationHelperTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: false).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        [Fact]
        public async Task MigrateDatabaseSafelyAsync_LegacyDatabaseWithoutMigrationHistory_BaselinesInitialCreateAndAppliesSubsequentMigrations()
        {
            // Arrange: Simulate a legacy database that has raw tables created without __EFMigrationsHistory
            using (var rawConnection = new SqliteConnection(_testDb.ConnectionString))
            {
                await rawConnection.OpenAsync();
                using var cmd = rawConnection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE "Accounts" (
                        "AccountID" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        "AccountSID" TEXT NULL,
                        "AccountName" TEXT NULL,
                        "AccountNumber" INTEGER NULL,
                        "BankName" TEXT NULL,
                        "CreatedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "CreatedByUserID" INTEGER NULL,
                        "LastModifiedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "LastModifiedByUserID" INTEGER NULL,
                        "Status" INTEGER NOT NULL DEFAULT 1
                    );

                    CREATE TABLE "Descriptions" (
                        "DescriptionID" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        "DescriptionSID" TEXT NULL,
                        "DescriptionName" TEXT NOT NULL,
                        "CreatedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "CreatedByUserID" INTEGER NULL,
                        "LastModifiedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "LastModifiedByUserID" INTEGER NULL,
                        "Status" INTEGER NOT NULL DEFAULT 1
                    );

                    CREATE TABLE "Users" (
                        "UserID" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        "UserSID" TEXT NULL,
                        "UserName" TEXT NOT NULL,
                        "Email" TEXT NOT NULL,
                        "Password" TEXT NOT NULL,
                        "FirstName" TEXT NOT NULL,
                        "MiddleName" TEXT NULL,
                        "LastName" TEXT NOT NULL,
                        "CreatedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "LastModifiedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "Status" INTEGER NOT NULL DEFAULT 1
                    );

                    CREATE TABLE "Transactions" (
                        "TransactionID" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        "TransactionSID" TEXT NULL,
                        "TransactionDate" TEXT NOT NULL,
                        "DescriptionID" INTEGER NULL,
                        "Debit" REAL NULL DEFAULT 0.0,
                        "Credit" REAL NULL DEFAULT 0.0,
                        "Balance" REAL NULL DEFAULT 0.0,
                        "Notes" TEXT NULL,
                        "AccountID" INTEGER NULL,
                        "CreatedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "CreatedByUserID" INTEGER NULL,
                        "LastModifiedDateTime" TEXT NULL DEFAULT CURRENT_TIMESTAMP,
                        "LastModifiedByUserID" INTEGER NULL,
                        "Status" INTEGER NOT NULL DEFAULT 1
                    );
                """;
                await cmd.ExecuteNonQueryAsync();
            }

            // Act: Run safe migration helper on the legacy database
            using (var context = _testDb.CreateContext())
            {
                await DatabaseMigrationHelper.MigrateDatabaseSafelyAsync(context);
            }

            // Assert: Verify migration history contains InitialCreate and all subsequent migrations
            using (var verifyContext = _testDb.CreateContext())
            {
                var appliedMigrations = (await verifyContext.Database.GetAppliedMigrationsAsync()).ToList();
                var pendingMigrations = (await verifyContext.Database.GetPendingMigrationsAsync()).ToList();

                Assert.Empty(pendingMigrations);
                Assert.Contains("20260315212014_InitialCreate", appliedMigrations);
                Assert.Contains("20260316195222_AddListViews", appliedMigrations);
                Assert.Contains("20260821151500_AddUniqueIndexToDescriptionName", appliedMigrations);
                Assert.Contains("20260821153000_AddPerformanceIndexes", appliedMigrations);

                // Assert: Verify views exist in sqlite_master
                var views = await GetSchemaObjectsAsync(verifyContext, "view");
                Assert.Contains("vw_transactions_list", views);
                Assert.Contains("vw_accounts_list", views);
                Assert.Contains("vw_descriptions_list", views);

                // Assert: Verify indexes exist in sqlite_master
                var indexes = await GetSchemaObjectsAsync(verifyContext, "index");
                Assert.Contains("IX_Descriptions_DescriptionName", indexes);
                Assert.Contains("IX_Transactions_AccountID", indexes);
                Assert.Contains("IX_Transactions_TransactionDate", indexes);
                Assert.Contains("IX_Transactions_Status_TransactionDate", indexes);

                // Assert: Views are queryable via EF Core without errors
                var txViewCount = await verifyContext.VwTransactionsList.CountAsync();
                Assert.Equal(0, txViewCount);
            }
        }

        [Fact]
        public async Task MigrateDatabaseSafelyAsync_FreshDatabase_AppliesAllMigrationsAndCreatesViewsAndIndexes()
        {
            // Act: Run safe migration helper on a completely fresh/empty database
            using (var context = _testDb.CreateContext())
            {
                await DatabaseMigrationHelper.MigrateDatabaseSafelyAsync(context);
            }

            // Assert: All migrations applied, views and indexes exist
            using (var verifyContext = _testDb.CreateContext())
            {
                var pendingMigrations = (await verifyContext.Database.GetPendingMigrationsAsync()).ToList();
                Assert.Empty(pendingMigrations);

                var views = await GetSchemaObjectsAsync(verifyContext, "view");
                Assert.Contains("vw_transactions_list", views);
                Assert.Contains("vw_accounts_list", views);
                Assert.Contains("vw_descriptions_list", views);

                var indexes = await GetSchemaObjectsAsync(verifyContext, "index");
                Assert.Contains("IX_Descriptions_DescriptionName", indexes);
                Assert.Contains("IX_Transactions_TransactionDate", indexes);
            }
        }

        [Fact]
        public async Task ImportProfileVersionMigration_EnforcesImmutabilityAndAllowsReimportAfterUndo()
        {
            await using var context = _testDb.CreateContext();
            await DatabaseMigrationHelper.MigrateDatabaseSafelyAsync(context);
            var account = new Account { AccountSID = Guid.NewGuid().ToString(), AccountName = "Test account", Status = 1 };
            var profile = new ImportProfile { ImportProfileSID = Guid.NewGuid().ToString(), Name = "Test profile", FileType = "CSV", HeaderRow = 1, HeaderSignature = "date|description", ColumnMappingJson = "{}", Status = 1 };
            context.AddRange(account, profile);
            await context.SaveChangesAsync();
            var version = new ImportProfileVersion { ProfileID = profile.ImportProfileID, VersionNumber = 1, FileType = "CSV", HeaderSignature = "date|description", HeaderRowIndexHint = 1, HeaderCellCount = 2, SampleJson = "{}", CreatedAt = DateTime.UtcNow.ToString("O") };
            context.ImportProfileVersions.Add(version);
            await context.SaveChangesAsync();

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE ImportProfileVersions SET FileType = 'XLS' WHERE ImportProfileVersionID = $id;";
            var parameter = update.CreateParameter(); parameter.ParameterName = "$id"; parameter.Value = version.ImportProfileVersionID; update.Parameters.Add(parameter);
            await Assert.ThrowsAnyAsync<Exception>(() => update.ExecuteNonQueryAsync());

            context.ImportBatches.Add(new ImportBatch { ImportBatchSID = Guid.NewGuid().ToString(), AccountID = account.AccountID, SourceFileName = "one.csv", FileType = "csv", FileHash = "hash", TotalRows = 1, Status = 1 });
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("UPDATE ImportBatches SET UndoneDateTime = '2026-01-01T00:00:00.0000000Z' WHERE FileHash = 'hash';");
            context.ImportBatches.Add(new ImportBatch { ImportBatchSID = Guid.NewGuid().ToString(), AccountID = account.AccountID, SourceFileName = "two.csv", FileType = "csv", FileHash = "hash", TotalRows = 1, Status = 1 });
            await context.SaveChangesAsync();
        }

        private static async Task<List<string>> GetSchemaObjectsAsync(AppDbContext context, string objectType)
        {
            var result = new List<string>();
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type = '{objectType}';";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }
    }
}
