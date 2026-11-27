using backend.Extensions;
using backend.model.TenantDB;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace backend.tests.Services;

public class SqliteConnectionConfigurationTests
{
    [Fact]
    public async Task NormalApplicationConnection_DisablesSensitiveDataLogging()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cashbook_{Guid.NewGuid():N}.db");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DefaultConnection"] = $"Data Source={path}" })
                .Build();
            var services = new ServiceCollection();
            services.AddDatabase(configuration, true);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var options = db.GetService<IDbContextOptions>();
            var coreOptions = options.Extensions.OfType<CoreOptionsExtension>().Single();

            Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var temporaryFile = path + suffix;
                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
            }
        }
    }

    [Fact]
    public async Task NormalApplicationConnection_UsesBusyTimeoutAndWal()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cashbook_{Guid.NewGuid():N}.db");
        try
        {
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DefaultConnection"] = $"Data Source={path}" }).Build();
                var services = new ServiceCollection(); services.AddDatabase(configuration, true);
                await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.OpenConnectionAsync(); var connection = db.Database.GetDbConnection();
                await using var timeout = connection.CreateCommand(); timeout.CommandText = "PRAGMA busy_timeout;"; Assert.True(Convert.ToInt32(await timeout.ExecuteScalarAsync()) >= 5000);
                await using var journal = connection.CreateCommand(); journal.CommandText = "PRAGMA journal_mode;"; Assert.Equal("wal", Convert.ToString(await journal.ExecuteScalarAsync())?.ToLowerInvariant());
                await db.Database.CloseConnectionAsync();
                await connection.DisposeAsync();
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var temporaryFile = path + suffix;
                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
                Assert.False(File.Exists(temporaryFile));
            }
        }
    }
}
