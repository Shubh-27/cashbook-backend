using backend.model.DbModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace backend.service.Helpers
{
    public static class DatabaseMigrationHelper
    {
        public static void MigrateDatabaseSafely(AppDbContext db, ILogger? logger = null)
        {
            var pendingMigrations = db.Database.GetPendingMigrations().ToList();
            var appliedMigrations = db.Database.GetAppliedMigrations().ToList();

            var databaseCreator = db.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
            var hasTables = databaseCreator.HasTables();

            if (!hasTables)
            {
                logger?.LogInformation("Fresh database detected. Running migrations.");
                db.Database.Migrate();
                return;
            }

            if (appliedMigrations.Count == 0 && pendingMigrations.Any())
            {
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                bool hasAccountsTable = false;
                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Accounts';";
                    var count = Convert.ToInt64(checkCmd.ExecuteScalar());
                    hasAccountsTable = count > 0;
                }

                if (hasAccountsTable)
                {
                    logger?.LogWarning("Legacy database detected with existing tables but missing migration history. Baselining InitialCreate migration.");
                    BaselineInitialMigration(db, logger);
                }
            }

            var remainingPending = db.Database.GetPendingMigrations().ToList();
            if (remainingPending.Count != 0)
            {
                logger?.LogInformation(
                    "Applying {Count} pending migrations: {Migrations}",
                    remainingPending.Count,
                    string.Join(", ", remainingPending));

                db.Database.Migrate();
            }
            else
            {
                logger?.LogInformation("Database is up to date.");
            }
        }

        public static async Task MigrateDatabaseSafelyAsync(AppDbContext db, ILogger? logger = null)
        {
            var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
            var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();

            var databaseCreator = db.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
            var hasTables = databaseCreator.HasTables();

            if (!hasTables)
            {
                logger?.LogInformation("Fresh database detected. Running migrations.");
                await db.Database.MigrateAsync();
                return;
            }

            if (appliedMigrations.Count == 0 && pendingMigrations.Any())
            {
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                bool hasAccountsTable = false;
                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Accounts';";
                    var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
                    hasAccountsTable = count > 0;
                }

                if (hasAccountsTable)
                {
                    logger?.LogWarning("Legacy database detected with existing tables but missing migration history. Baselining InitialCreate migration.");
                    await BaselineInitialMigrationAsync(db, logger);
                }
            }

            var remainingPending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (remainingPending.Count != 0)
            {
                logger?.LogInformation(
                    "Applying {Count} pending migrations: {Migrations}",
                    remainingPending.Count,
                    string.Join(", ", remainingPending));

                await db.Database.MigrateAsync();
            }
            else
            {
                logger?.LogInformation("Database is up to date.");
            }
        }

        private static void BaselineInitialMigration(AppDbContext db, ILogger? logger = null)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            // Ensure __EFMigrationsHistory table exists
            using (var ensureCmd = connection.CreateCommand())
            {
                ensureCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId"    TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """;
                ensureCmd.ExecuteNonQuery();
            }

            var productVersion = typeof(AppDbContext).Assembly.GetName().Version?.ToString() ?? "10.0.5";
            const string initialMigration = "20260315212014_InitialCreate";

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $"""
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{initialMigration}', '{productVersion}');
                """;
            insertCmd.ExecuteNonQuery();

            logger?.LogInformation("Baselined initial migration in history: {Migration}", initialMigration);
        }

        private static async Task BaselineInitialMigrationAsync(AppDbContext db, ILogger? logger = null)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Ensure __EFMigrationsHistory table exists
            using (var ensureCmd = connection.CreateCommand())
            {
                ensureCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId"    TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """;
                await ensureCmd.ExecuteNonQueryAsync();
            }

            var productVersion = typeof(AppDbContext).Assembly.GetName().Version?.ToString() ?? "10.0.5";
            const string initialMigration = "20260315212014_InitialCreate";

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = $"""
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{initialMigration}', '{productVersion}');
                """;
            await insertCmd.ExecuteNonQueryAsync();

            logger?.LogInformation("Baselined initial migration in history: {Migration}", initialMigration);
        }
    }
}
