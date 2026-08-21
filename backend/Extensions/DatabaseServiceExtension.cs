using backend.model.DbModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace backend.Extensions
{
    public static class DatabaseServiceExtension
    {
        /// <summary>
        /// Retrieves the SQLite connection string based on environment variables, configuration, or environment defaults.
        /// </summary>
        public static string GetConnectionString(IConfiguration configuration, bool isDevelopment)
        {
            var customDbPath = Environment.GetEnvironmentVariable("DATABASE_PATH");
            if (!string.IsNullOrEmpty(customDbPath))
            {
                var dbFolder = Path.GetDirectoryName(customDbPath);
                if (!string.IsNullOrEmpty(dbFolder))
                {
                    Directory.CreateDirectory(dbFolder);
                }
                return $"Data Source={customDbPath};Cache=Shared;Pooling=True;";
            }

            if (!isDevelopment)
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbFolder = Path.Combine(appDataPath, "BankApp");
                Directory.CreateDirectory(dbFolder);
                var dbFilePath = Path.Combine(dbFolder, "cashbook.db");
                return $"Data Source={dbFilePath};Cache=Shared;Pooling=True;";
            }

            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? configuration["DefaultConnection"];

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Data Source=cashbook.db;Cache=Shared;Pooling=True;";
            }

            return connectionString;
        }

        /// <summary>
        /// Registers AppDbContext with the correct connection string based on environment.
        /// Usage in Program.cs: builder.Services.AddDatabase(builder.Configuration, builder.Environment.IsDevelopment());
        /// </summary>
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
        {
            var connectionString = GetConnectionString(configuration, isDevelopment);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connectionString);
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging(isDevelopment);
            }, ServiceLifetime.Scoped);

            return services;
        }

        /// <summary>
        /// Handles EF Core migrations safely on startup.
        /// Covers 3 cases: fresh DB, existing legacy DB with no migration history, and normal incremental migrations.
        /// Usage in Program.cs: DatabaseServiceExtension.ApplyMigrations(app);
        /// </summary>
        public static void ApplyMigrations(WebApplication app)
        {
            using var mscope = app.Services.CreateScope();
            var config = mscope.ServiceProvider.GetRequiredService<IConfiguration>();
            var autoMigrateStr = config["AutoMigrate"];
            bool autoMigrate = true;
            if (!string.IsNullOrEmpty(autoMigrateStr))
            {
                bool.TryParse(autoMigrateStr, out autoMigrate);
            }
            if (!autoMigrate) return;

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                backend.service.Helpers.DatabaseMigrationHelper.MigrateDatabaseSafely(db, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration failed.");
                throw;
            }
        }
    }
}
