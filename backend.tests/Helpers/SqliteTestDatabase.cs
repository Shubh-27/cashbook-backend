using backend.model.DbModels;
using backend.service.Helpers;
using backend.service.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.tests.Helpers
{
    public class SqliteTestDatabase : IAsyncDisposable, IDisposable
    {
        public string FilePath { get; }
        public string ConnectionString => $"Data Source={FilePath};Pooling=False;Default Timeout=30;";

        private SqliteTestDatabase(string filePath)
        {
            FilePath = filePath;
        }

        public static async Task<SqliteTestDatabase> CreateAsync(bool applyMigrations = true)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"bank_test_{Guid.NewGuid():N}.db");
            var testDb = new SqliteTestDatabase(tempPath);

            if (applyMigrations)
            {
                using var context = testDb.CreateContext();
                await DatabaseMigrationHelper.MigrateDatabaseSafelyAsync(context);
            }

            return testDb;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(ConnectionString)
                .Options;

            return new AppDbContext(options);
        }

        public IUnitOfWork<AppDbContext> CreateUnitOfWork()
        {
            var context = CreateContext();
            return new UnitOfWork<AppDbContext>(context);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors for temp files in tests
            }
        }

        public async ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await Task.Yield();

            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors for temp files in tests
            }
        }
    }
}
