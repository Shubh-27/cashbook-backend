using backend.model.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace backend.Helper
{
    public static class DatabaseServiceExtension
    {
        /// <summary>
        /// Registers AppDbContext with the correct connection string based on environment.
        /// Usage in Program.cs: DatabaseServiceExtension.AddDatabase(builder);
        /// </summary>
        public static void AddDatabase(WebApplicationBuilder builder)
        {
            string dbConnectionString;

            if (!builder.Environment.IsDevelopment())
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbFolder = Path.Combine(appDataPath, "BankApp");
                Directory.CreateDirectory(dbFolder);
                var dbFilePath = Path.Combine(dbFolder, "cashbook.db");
                dbConnectionString = $"Data Source={dbFilePath};Cache=Shared;Pooling=True;";
            }
            else
            {
                dbConnectionString = builder.Configuration["DefaultConnection"] ?? string.Empty;

                if (string.IsNullOrEmpty(dbConnectionString))
                {
                    dbConnectionString = "Data Source=cashbook.db;Cache=Shared;Pooling=True;";
                }
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(dbConnectionString);
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment()); // only in dev
            }, ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Handles EF Core migrations safely on startup.
        /// Covers 3 cases: fresh DB, existing DB with no migration history, and normal incremental migrations.
        /// Usage in Program.cs: DatabaseServiceExtension.ApplyMigrations(app);
        /// </summary>
        public static void ApplyMigrations(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                var pendingMigrations = db.Database.GetPendingMigrations().ToList();
                var appliedMigrations = db.Database.GetAppliedMigrations().ToList();

                // check if database has ANY tables using EF Core's infrastructure
                var databaseCreator = db.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
                var hasTables = databaseCreator.HasTables();

                if (!hasTables)
                {
                    logger.LogInformation("Fresh database detected. Running migrations.");
                    db.Database.Migrate();
                    return;
                }

                if (appliedMigrations.Count == 0 && pendingMigrations.Any())
                {
                    logger.LogWarning(
                        "Database exists but migration history missing. Seeding migration history.");

                    SeedMigrationHistory(db, pendingMigrations, logger);
                    return;
                }

                if (pendingMigrations.Count != 0)
                {
                    logger.LogInformation(
                        "Applying {Count} pending migrations: {Migrations}",
                        pendingMigrations.Count,
                        string.Join(", ", pendingMigrations));

                    db.Database.Migrate();
                }
                else
                {
                    logger.LogInformation("Database is up to date.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration failed.");
                throw;
            }
        }

        // -----------------------------------------------------------------------

        private static void SeedMigrationHistory(AppDbContext db, List<string> pendingMigrations, ILogger logger)
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();

            try
            {
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

                var productVersion = typeof(AppDbContext).Assembly.GetName().Version?.ToString() ?? "1.0.0";

                foreach (var migration in pendingMigrations)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = $"""
                        INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ('{migration}', '{productVersion}');
                        """;
                    insertCmd.ExecuteNonQuery();

                    logger.LogInformation("Marked migration as applied: {Migration}", migration);
                }
            }
            finally
            {
                connection.Close();
            }
        }
    }
}