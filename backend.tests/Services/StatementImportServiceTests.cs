using backend.common;
using backend.model.DbModels;
using backend.model.RequestModels;
using backend.service.Repository.Implementations;
using backend.service.Services.Implementations;
using backend.service.UnitOfWork;
using backend.tests.Helpers;
using backend.tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Security.Cryptography;
using Xunit;

namespace backend.tests.Services;

public class StatementImportServiceTests : IAsyncDisposable
{
    private readonly SqliteTestDatabase _database;

    public StatementImportServiceTests()
    {
        _database = SqliteTestDatabase.CreateAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    private static StatementImportService CreateService(AppDbContext context) => new(new UnitOfWork<AppDbContext>(context));
    private static ImportProfileRepository CreateProfileRepository(AppDbContext context) => new(new UnitOfWork<AppDbContext>(context), new StatementGridParser(), new StatementMaskingService(), new ImportProfileRuleValidator());

    [Fact]
    public async Task CommitAsync_WhenNoRowsSelected_RejectsWithoutCreatingBatch()
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.CommitAsync(new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = "2026-11-21T00:00:00.000Z", Description = "Groceries", Debit = 500, Include = false }]
        }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(await context.ImportBatches.ToListAsync());
    }

    [Fact]
    public async Task CommitAsync_WhenClientSuppliesLegacyProfileBody_RejectsWithoutPersistingIt()
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.CommitAsync(new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            SaveProfile = new SaveImportProfileRequest { Name = "Untrusted profile", FileType = "csv" },
            Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = "2026-11-21T00:00:00.000Z", Description = "Groceries", Debit = 500, Include = true }]
        }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(await context.ImportProfiles.ToListAsync());
        Assert.Empty(await context.ImportBatches.ToListAsync());
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-11-21")]
    public async Task CommitAsync_WhenClientSuppliesNonIsoDate_RejectsWithoutWritingTransactions(string date)
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.CommitAsync(new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = date, Description = "Groceries", Debit = 500, Include = true }]
        }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(await context.Transactions.ToListAsync());
        Assert.Empty(await context.ImportBatches.ToListAsync());
    }

    [Fact]
    public async Task CommitAsync_WhenDescriptionExceedsLimit_RejectsWithoutWritingTransactions()
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.CommitAsync(new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = "2026-11-21T00:00:00.000Z", Description = new string('x', 101), Debit = 500, Include = true }]
        }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStatementHasFooterBeforeAnotherTable_ExcludesFooterRows()
    {
        const string csv = "Account holder: Example\nDate,Description,Debit,Credit\n2026-11-21,Coffee,120,\nCharge breakup for this account\nPeriod,Charge Type,Debit,Credit\n2026-11,Monthly charge,100,100";
        await using var context = _database.CreateContext();
        var service = CreateService(context);
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var file = new FormFile(stream, 0, stream.Length, "file", "statement.csv");

        var result = await service.AnalyzeAsync(file, null, null, null, null, "yyyy-MM-dd", null, null);

        Assert.Equal(2, result.HeaderRow);
        Assert.Equal(3, result.TransactionEndRow);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Coffee", row.Description);
        Assert.Empty(row.Issues);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(csv))).ToLowerInvariant(), result.FileHash);
    }

    [Fact]
    public async Task AnalyzeAndCommitAsync_MaskStatementMetadataBeforeReturningOrPersistingIt()
    {
        const string csv = "Account number: 123456789012\nContact: help@example.test\nPhone: +91 9876543210\nDate,Description,Debit\n2026-11-21,Coffee,120";
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var analysis = await service.AnalyzeAsync(new FormFile(stream, 0, stream.Length, "file", "statement.csv"), null, null, null, null, "yyyy-MM-dd", null, null);

        var responseMetadata = string.Join(' ', analysis.Metadata.Select(pair => $"{pair.Key}:{pair.Value}"));
        Assert.DoesNotContain("123456789012", responseMetadata);
        Assert.DoesNotContain("help@example.test", responseMetadata);
        Assert.DoesNotContain("9876543210", responseMetadata);
        var returnedSample = string.Join(' ', analysis.Sample.TopRows.SelectMany(row => row.Values));
        Assert.DoesNotContain("123456789012", returnedSample);
        Assert.DoesNotContain("help@example.test", returnedSample);
        Assert.DoesNotContain("9876543210", returnedSample);

        await service.CommitAsync(new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            Metadata = new Dictionary<string, string> { ["Account number"] = "123456789012", ["Contact"] = "help@example.test", ["Phone"] = "+91 9876543210" },
            Rows = analysis.Rows
        });

        var persistedMetadata = Assert.Single(context.ImportBatches).StatementMetadataJson;
        Assert.DoesNotContain("123456789012", persistedMetadata);
        Assert.DoesNotContain("help@example.test", persistedMetadata);
        Assert.DoesNotContain("9876543210", persistedMetadata);
    }

    [Theory]
    [InlineData("synthetic-statement.xlsx", "xlsx")]
    [InlineData("synthetic-statement.xls", "xls")]
    public async Task AnalyzeAsync_UsesTheSharedParserForWorkbookFormats(string fixtureName, string expectedFileType)
    {
        await using var context = _database.CreateContext();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(StatementFixtureGenerator.DirectoryPath, fixtureName));
        await using var stream = new MemoryStream(bytes);

        var analysis = await CreateService(context).AnalyzeAsync(new FormFile(stream, 0, bytes.Length, "file", fixtureName), "Transactions", 5, 7, null, "dd-MM-yyyy", null, null);

        Assert.Equal(expectedFileType, analysis.FileType);
        Assert.Equal("Transactions", analysis.SelectedSheet);
        Assert.Equal(5, analysis.HeaderRow);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMislabeledWorkbookContentWithoutLeakingCellValues()
    {
        await using var context = _database.CreateContext();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(StatementFixtureGenerator.DirectoryPath, "synthetic-statement.xlsx"));
        await using var stream = new MemoryStream(bytes);

        var exception = await Assert.ThrowsAsync<HttpStatusCodeException>(() => CreateService(context).AnalyzeAsync(new FormFile(stream, 0, bytes.Length, "file", "statement.csv"), null, null, null, null, null, null, null));

        Assert.Equal(400, exception.StatusCode);
        Assert.DoesNotContain("ABCDE1234F", exception.Message);
        Assert.DoesNotContain("help@example.test", exception.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesColumnIndexesFromTheCurrentVersionedProfile()
    {
        await using var context = _database.CreateContext();
        var createService = CreateProfileRepository(context);
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit,Credit\n02/04/2024,Coffee,350,\n03/04/2024,Salary,,1000\nFooter");
        await using var source = new MemoryStream(bytes);
        var profile = await createService.CreateAsync(new FormFile(source, 0, bytes.Length, "file", "statement.csv"), new ImportProfileRuleRequest { Name = "Indexed profile", HeaderRow = 1, EndRow = 3, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2, Credit = 3 });
        await using var importStream = new MemoryStream(bytes);

        var analysis = await CreateService(context).AnalyzeAsync(new FormFile(importStream, 0, bytes.Length, "file", "statement.csv"), null, null, null, profile.ImportProfileSID, null, null, null);

        Assert.Equal("Date", analysis.Mapping.Date);
        Assert.Equal("Description", analysis.Mapping.Description);
        Assert.Equal("Debit", analysis.Mapping.Debit);
        Assert.Equal("Credit", analysis.Mapping.Credit);
        Assert.Equal(2, analysis.Rows.Count);
        Assert.All(analysis.Rows, row => Assert.Empty(row.Issues));

        var listed = Assert.Single(await CreateService(context).GetProfilesAsync());
        Assert.Equal("Date", listed.Mapping.Date);
        Assert.Equal("Description", listed.Mapping.Description);
    }

    [Fact]
    public async Task AnalyzeAsync_MasksIncomingHeadersBeforeMatchingTheSavedSignature()
    {
        await using var context = _database.CreateContext();
        var createService = CreateProfileRepository(context);
        var originalBytes = Encoding.UTF8.GetBytes("Bank statement\nDate,Description ABCDE1234F,Debit\n02/04/2024,Coffee,350\nFooter");
        await using var originalStream = new MemoryStream(originalBytes);
        var profile = await createService.CreateAsync(new FormFile(originalStream, 0, originalBytes.Length, "file", "statement.csv"), new ImportProfileRuleRequest { Name = "Masked header profile", HeaderRow = 2, EndRow = 3, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 });
        var shiftedBytes = Encoding.UTF8.GetBytes("New metadata\nBank statement\nDate,Description ABCDE1234F,Debit\n02/04/2024,Coffee,350\nFooter");
        await using var shiftedStream = new MemoryStream(shiftedBytes);

        var analysis = await CreateService(context).AnalyzeAsync(new FormFile(shiftedStream, 0, shiftedBytes.Length, "file", "statement.csv"), null, null, null, profile.ImportProfileSID, null, null, null);

        Assert.Equal(3, analysis.HeaderRow);
        Assert.Single(analysis.Rows);
        Assert.Equal("Coffee", analysis.Rows[0].Description);
    }

    [Fact]
    public async Task CommitAsync_RecordsTheSelectedProfileVersionOnTheImportBatch()
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var createService = CreateProfileRepository(context);
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,Coffee,350\nFooter");
        await using var source = new MemoryStream(bytes);
        var profile = await createService.CreateAsync(new FormFile(source, 0, bytes.Length, "file", "statement.csv"), new ImportProfileRuleRequest { Name = "Batch profile", HeaderRow = 1, EndRow = 2, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 });

        await CreateService(context).CommitAsync(new CommitStatementImportRequest { AccountSID = account.AccountSID, FileName = "statement.csv", FileType = "csv", ProfileSID = profile.ImportProfileSID, Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = "2024-04-02T00:00:00.000Z", Description = "Coffee", Debit = 350, Include = true }] });

        var batch = Assert.Single(context.ImportBatches);
        var version = Assert.Single(context.ImportProfileVersions);
        Assert.Equal(version.ImportProfileVersionID, batch.ProfileVersionID);
    }

    [Fact]
    public async Task CommitAsync_PersistsHashesAndRejectsAnActiveRepeatUpload()
    {
        var account = await CreateAccountAsync();
        await using var context = _database.CreateContext();
        var service = CreateService(context);
        var request = new CommitStatementImportRequest
        {
            AccountSID = account.AccountSID,
            FileName = "statement.csv",
            FileType = "csv",
            FileHash = new string('a', 64),
            Rows = [new StatementImportRow { RowNumber = 2, TransactionDate = "2024-04-02T00:00:00.000Z", Description = "Coffee", Debit = 350, Balance = 1000, Include = true }]
        };

        await service.CommitAsync(request);
        var repeat = await Assert.ThrowsAsync<HttpStatusCodeException>(() => service.CommitAsync(request));

        Assert.Equal(400, repeat.StatusCode);
        var batch = Assert.Single(context.ImportBatches);
        var transaction = Assert.Single(context.Transactions);
        Assert.Equal(new string('a', 64), batch.FileHash);
        Assert.Matches("^[a-f0-9]{64}$", transaction.RowHash);
    }

    private async Task<Account> CreateAccountAsync()
    {
        await using var context = _database.CreateContext();
        var account = new Account
        {
            AccountSID = Guid.NewGuid().ToString(),
            AccountName = "Import test account",
            Status = Constants.StatusType.Active
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

}
