using System.Text;
using backend.model.DbModels;
using backend.model.RequestModels;
using backend.Controllers.V1;
using backend.service.Repository.Implementations;
using backend.service.Services.Implementations;
using backend.service.UnitOfWork;
using backend.tests.Helpers;
using backend.tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace backend.tests.Services;

public class ImportProfileRepositoryTests
{
    private static FormFile File(string name = "statement.csv")
    {
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,Coffee,350\nFooter");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    }
    private static ImportProfileRuleRequest Rule(string name = "Synthetic") => new() { Name = name, HeaderRow = 1, EndRow = 2, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 };
    private static ImportProfileRepository Repository(AppDbContext context) => new(new UnitOfWork<AppDbContext>(context), new StatementGridParser(), new StatementMaskingService(), new ImportProfileRuleValidator());
    [Fact]
    public async Task CreateAsync_CreatesVersionOneAndCurrentVersionFromMaskedFileData()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = Repository(context);
        var bytes = Encoding.UTF8.GetBytes("Metadata\nDate,Description ABCDE1234F,Debit\n02/04/2024,ABCDE1234F 9876543210 help@example.test,350\nFooter account 123456789012");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "statement.csv");
        var result = await repository.CreateAsync(file, new ImportProfileRuleRequest { Name = "Synthetic", HeaderRow = 2, EndRow = 3, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 });

        Assert.True(result.Success);
        var profile = Assert.Single(context.ImportProfiles);
        var version = Assert.Single(context.ImportProfileVersions);
        Assert.Equal(1, version.VersionNumber); Assert.Equal(version.ImportProfileVersionID, profile.CurrentVersionID);
        Assert.Equal("csv", profile.FileType); Assert.Equal(2, version.HeaderRowIndexHint); Assert.Equal(3, version.HeaderCellCount);
        Assert.DoesNotContain("ABCDE1234F", version.SampleJson); Assert.DoesNotContain("9876543210", version.SampleJson); Assert.DoesNotContain("help@example.test", version.SampleJson);
        Assert.DoesNotContain("123456789012", version.FooterMarker);
        Assert.DoesNotContain("ABCDE1234F", version.HeaderSignature);
        Assert.DoesNotContain("123456789012", version.FooterSignature);
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicateNameTogetherWithOtherRuleProblems()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = Repository(context);
        await repository.CreateAsync(File(), Rule());
        var invalid = Rule(); invalid.Date = null;
        var result = await repository.CreateAsync(File(), invalid);
        Assert.Contains(result.Problems, problem => problem.Code == "name_duplicate");
        Assert.Contains(result.Problems, problem => problem.Code == "date_required");
        Assert.Single(context.ImportProfiles);
    }

    [Theory]
    [InlineData("synthetic-statement.xlsx", "xlsx")]
    [InlineData("synthetic-statement.xls", "xls")]
    public async Task CreateAsync_AcceptsWorkbookFormats(string fixtureName, string expectedFileType)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var bytes = await System.IO.File.ReadAllBytesAsync(Path.Combine(StatementFixtureGenerator.DirectoryPath, fixtureName));
        await using var stream = new MemoryStream(bytes);
        var result = await Repository(context).CreateAsync(new FormFile(stream, 0, bytes.Length, "file", fixtureName), new ImportProfileRuleRequest { Name = fixtureName, WorksheetName = "Transactions", HeaderRow = 5, EndRow = 7, DateFormat = "dd-MM-yyyy", Date = 0, Description = 1, Debit = 2, Credit = 3 });
        Assert.True(result.Success);
        Assert.Equal(expectedFileType, Assert.Single(context.ImportProfiles).FileType);
    }

    [Fact]
    public async Task CreateAsync_CapturesFooterAfterEndRowWhenItIsOutsideStoredSampleBands()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var lines = new List<string> { "Date,Description,Debit" };
        for (var row = 2; row <= 130; row++) lines.Add(row == 81 ? "Footer hidden marker 123456789012,," : $"02/04/2024,Transaction {row},1");
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines));
        await using var stream = new MemoryStream(bytes);
        var result = await Repository(context).CreateAsync(new FormFile(stream, 0, bytes.Length, "file", "long.csv"), new ImportProfileRuleRequest { Name = "Long", HeaderRow = 1, EndRow = 80, DateFormat = "dd/MM/yyyy", Date = 0, Description = 1, Debit = 2 });
        Assert.True(result.Success);
        var version = Assert.Single(context.ImportProfileVersions);
        Assert.Contains("footer hidden marker", version.FooterSignature);
        Assert.DoesNotContain("123456789012", version.FooterSignature);
        Assert.DoesNotContain("\"row_number\":81", version.SampleJson);
    }

    [Fact]
    public async Task CreateAsync_RollsBackProfileWhenVersionInsertFails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_profile_version BEFORE INSERT ON ImportProfileVersions BEGIN SELECT RAISE(ABORT, 'forced'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => Repository(context).CreateAsync(File(), Rule("Rollback")));
        context.ChangeTracker.Clear();
        Assert.Empty(await context.ImportProfiles.ToListAsync());
    }

    [Fact]
    public async Task CreateEndpoint_IgnoresClientSuppliedSampleAndRowsProperties()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var controller = new ImportProfilesController(null!, Repository(context));
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,Coffee,350\nFooter");
        await using var stream = new MemoryStream(bytes);
        const string rule = "{\"name\":\"Endpoint\",\"header_row\":1,\"end_row\":2,\"date_format\":\"dd/MM/yyyy\",\"date\":0,\"description\":1,\"debit\":2,\"sample\":[\"unmasked\"],\"rows\":[\"unmasked\"]}";
        var action = await controller.Create(new FormFile(stream, 0, bytes.Length, "file", "statement.csv"), rule, CancellationToken.None);
        Assert.IsType<OkObjectResult>(action);
        var version = Assert.Single(context.ImportProfileVersions);
        Assert.DoesNotContain("unmasked", version.SampleJson);
    }

    [Fact]
    public async Task CreateEndpoint_AcceptsSnakeCaseRuleFromTheFrontend()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var controller = new ImportProfilesController(null!, Repository(context));
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,Coffee,350\nFooter");
        await using var stream = new MemoryStream(bytes);
        const string rule = "{\"name\":\"Snake\",\"header_row\":1,\"end_row\":2,\"date_format\":\"dd/MM/yyyy\",\"date\":0,\"description\":1,\"debit\":2}";

        var action = await controller.Create(new FormFile(stream, 0, bytes.Length, "file", "statement.csv"), rule, CancellationToken.None);

        Assert.IsType<OkObjectResult>(action);
        Assert.Equal("Snake", Assert.Single(context.ImportProfiles).Name);
    }

    [Fact]
    public async Task ReadService_ReturnsCurrentVersionRuleAndMaskedSample()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,ABCDE1234F 9876543210,350\nFooter");
        await using var stream = new MemoryStream(bytes);
        var created = await Repository(context).CreateAsync(new FormFile(stream, 0, bytes.Length, "file", "statement.csv"), Rule("Read"));
        var response = await Repository(context).GetAsync(created.ImportProfileSID!, null);
        Assert.NotNull(response); Assert.Equal(1, response.VersionNumber); Assert.Equal(0, response.Date); Assert.Equal(1, response.Description);
        Assert.DoesNotContain("ABCDE1234F", System.Text.Json.JsonSerializer.Serialize(response.Sample));
    }

    [Fact]
    public async Task UpdateWithFile_CreatesVersionTwoAndLeavesVersionOneUnchanged()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var created = await Repository(context).CreateAsync(File(), Rule("Versioned"));
        var first = Assert.Single(context.ImportProfileVersions); var firstJson = first.SampleJson;
        var bytes = Encoding.UTF8.GetBytes("Date,Description,Debit\n02/04/2024,Updated,350\nFooter");
        await using var stream = new MemoryStream(bytes);
        var updated = await Repository(context).UpdateAsync(created.ImportProfileSID!, new FormFile(stream, 0, bytes.Length, "file", "updated.csv"), Rule("Versioned"));
        Assert.True(updated.Success); Assert.Equal(2, updated.VersionNumber);
        var versions = context.ImportProfileVersions.OrderBy(item => item.VersionNumber).ToList();
        Assert.Equal(2, versions.Count); Assert.Equal(firstJson, versions[0].SampleJson); Assert.Equal(2, versions[1].VersionNumber);
        Assert.Equal(versions[1].ImportProfileVersionID, Assert.Single(context.ImportProfiles).CurrentVersionID);
    }

    [Fact]
    public async Task UpdateWithoutFile_CreatesVersionAndRejectsLayoutChange()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var created = await Repository(context).CreateAsync(File(), Rule("Metadata"));
        var metadata = Rule("Metadata renamed"); metadata.Debit = null; metadata.Amount = 2;
        var updated = await Repository(context).UpdateAsync(created.ImportProfileSID!, null, metadata);
        Assert.True(updated.Success); Assert.Equal(2, updated.VersionNumber);
        var invalid = Rule("Metadata renamed"); invalid.HeaderRow = 2; invalid.EndRow = 2;
        var rejected = await Repository(context).UpdateAsync(created.ImportProfileSID!, null, invalid);
        Assert.Contains(rejected.Problems, problem => problem.Code == "source_layout_locked");
    }

    [Fact]
    public async Task ReadService_CanReturnAnImmutableHistoricalVersion()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var created = await Repository(context).CreateAsync(File(), Rule("Historical"));
        var updatedRule = Rule("Historical");
        updatedRule.Debit = null;
        updatedRule.Amount = 2;
        await Repository(context).UpdateAsync(created.ImportProfileSID!, null, updatedRule);

        var response = await Repository(context).GetAsync(created.ImportProfileSID!, 1);

        Assert.NotNull(response);
        Assert.Equal(1, response.VersionNumber);
        Assert.Equal(2, response.Debit);
        Assert.Null(response.Amount);
    }

    [Fact]
    public async Task ConcurrentMetadataUpdates_AssignDistinctVersionNumbers()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        var created = await Repository(seedContext).CreateAsync(File(), Rule("Concurrent"));

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstRule = Rule("Concurrent first");
        var secondRule = Rule("Concurrent second");
        var firstTask = Repository(firstContext).UpdateAsync(created.ImportProfileSID!, null, firstRule);
        var secondTask = Repository(secondContext).UpdateAsync(created.ImportProfileSID!, null, secondRule);

        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.All(results, result => Assert.True(result.Success));
        await using var verificationContext = database.CreateContext();
        var versions = await verificationContext.ImportProfileVersions
            .Where(item => item.Profile.ImportProfileSID == created.ImportProfileSID)
            .OrderBy(item => item.VersionNumber)
            .Select(item => item.VersionNumber)
            .ToListAsync();
        Assert.Equal([1, 2, 3], versions);
    }
}
