using System.Text;
using backend.common;
using backend.Controllers.V1;
using backend.service.Services.Implementations;
using backend.tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace backend.tests.Services;

public class StatementGridParserTests
{
    private static FormFile TextFile(byte[] bytes, string name = "statement.csv") => new(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private static FormFile Fixture(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(StatementFixtureGenerator.DirectoryPath, name));
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    }

    [Fact]
    public async Task ParseAsync_XlsxFixture_ReadsMultiSheetDatesAndSecondTable()
    {
        var sheets = await new StatementGridParser().ParseAsync(Fixture("synthetic-statement.xlsx"));
        Assert.Equal(["Overview", "Transactions"], sheets.Select(sheet => sheet.Name));
        var transactions = sheets.Single(sheet => sheet.Name == "Transactions");
        Assert.Equal("2024-04-02", transactions.Rows.Single(row => row.RowNumber == 6).Cells[0]);
    }

    [Fact]
    public async Task ParseAsync_XlsFixture_ReadsTransactionsSheet()
    {
        var sheets = await new StatementGridParser().ParseAsync(Fixture("synthetic-statement.xls"));
        Assert.Contains(sheets, sheet => sheet.Name == "Transactions");
        Assert.Equal("2024-04-02", sheets.Single(sheet => sheet.Name == "Transactions").Rows.Single(row => row.RowNumber == 6).Cells[0]);
    }
    [Fact]
    public async Task ParseAsync_ReadsCsvRegardlessOfFileExtension()
    {
        var parser = new StatementGridParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Date,Description,Debit\n02-04-2024,Coffee,120"));
        var file = new FormFile(stream, 0, stream.Length, "file", "statement.bin");

        var sheets = await parser.ParseAsync(file);

        Assert.Equal("CSV", sheets.Single().Name);
        Assert.Equal(2, sheets.Single().Rows.Count);
    }

    [Fact]
    public async Task ParseAsync_TreatsTextContentAsDelimitedDataDespiteXlsxExtension()
    {
        var parser = new StatementGridParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a workbook"));
        var file = new FormFile(stream, 0, stream.Length, "file", "statement.xlsx");

        var sheets = await parser.ParseAsync(file);
        Assert.Equal("CSV", sheets.Single().Name);
    }

    [Fact]
    public void MatchHeader_UsesOrderedCellsAndAmbiguousDateHelper()
    {
        var parser = new StatementGridParser();

        var match = parser.MatchHeader(["Tran Date", "Particulars", "DR"], ["Tran Date", "Particulars", "DR", "CR"]);

        Assert.True(match.IsMatch);
        Assert.True(parser.HasAmbiguousDayMonthDates(["02-04-2024", "11/12/2024"]));
    }

    [Fact]
    public async Task ParseAsync_ParsesTabDelimitedTextNamedXlsAndRejectsHtml()
    {
        var parser = new StatementGridParser();
        await using var tabStream = new MemoryStream(Encoding.UTF8.GetBytes("Date\tDescription\tDebit\n02-04-2024\tCoffee\t350"));
        var tabFile = new FormFile(tabStream, 0, tabStream.Length, "file", "bank-export.xls");
        var sheets = await parser.ParseAsync(tabFile);
        Assert.Equal(["Date", "Description", "Debit"], sheets.Single().Rows[0].Cells);

        await using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes("  <table><tr><td>Date</td></tr></table>"));
        var htmlFile = new FormFile(htmlStream, 0, htmlStream.Length, "file", "bank-export.xls");
        var exception = await Assert.ThrowsAsync<StatementParsingException>(() => parser.ParseAsync(htmlFile));
        Assert.Equal("html_spreadsheet", exception.Code);
    }

    [Fact]
    public async Task ParseAsync_DecodesUtf16LeBomTabDelimitedText()
    {
        var text = "Date\tDescription\n02-04-2024\tCoffee";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        var sheets = await new StatementGridParser().ParseAsync(TextFile(bytes, "export.xls"));
        Assert.Equal(["Date", "Description"], sheets.Single().Rows[0].Cells);
    }

    [Fact]
    public async Task ParseAsync_DecodesUtf16BeBomTabDelimitedText()
    {
        var text = "Date\tDescription\n02-04-2024\tCoffee";
        var bytes = Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray();
        var sheets = await new StatementGridParser().ParseAsync(TextFile(bytes, "export.xls"));
        Assert.Equal(["Date", "Description"], sheets.Single().Rows[0].Cells);
    }

    [Fact]
    public async Task ParseAsync_DecodesUtf8BomCommaDelimitedText()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Date,Description\n02-04-2024,Coffee")).ToArray();
        var sheets = await new StatementGridParser().ParseAsync(TextFile(bytes));
        Assert.Equal(["Date", "Description"], sheets.Single().Rows[0].Cells);
    }

    [Fact]
    public async Task ParseAsync_FallsBackToWindows1252ForAccentedMerchant()
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes("Date,Description\n02-04-2024,Café");
        var sheets = await new StatementGridParser().ParseAsync(TextFile(bytes));
        Assert.Equal("Café", sheets.Single().Rows[1].Cells[1]);
    }

    [Fact]
    public async Task ParseAsync_UsesStrictUtf8ForRupeeText()
    {
        var sheets = await new StatementGridParser().ParseAsync(TextFile(Encoding.UTF8.GetBytes("Date,Description\n02-04-2024,₹ Coffee")));
        Assert.Equal("₹ Coffee", sheets.Single().Rows[1].Cells[1]);
    }

    [Fact]
    public async Task ParseAsync_FallsBackWhenInvalidUtf8AppearsAfterHeader()
    {
        var bytes = Encoding.ASCII.GetBytes("Date,Description\n02-04-2024,Caf").Concat(new byte[] { 0xE9 }).ToArray();
        var sheets = await new StatementGridParser().ParseAsync(TextFile(bytes));
        Assert.Equal("Café", sheets.Single().Rows[1].Cells[1]);
    }

    [Theory]
    [InlineData(";", "Date;Description\n02-04-2024;Coffee")]
    [InlineData("|", "Date|Description\n02-04-2024|Coffee")]
    [InlineData("\t", "Date\tDescription\n02-04-2024\tCoffee")]
    public async Task ParseAsync_DetectsDelimiterAfterDecoding(string _, string text)
    {
        var sheets = await new StatementGridParser().ParseAsync(TextFile(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(["Date", "Description"], sheets.Single().Rows[0].Cells);
    }

    [Fact]
    public void MaskCell_MasksIndianStatementIdentifiers()
    {
        var masking = new StatementMaskingService();
        var value = masking.MaskCell("PAN ABCDE1234F; A/c 123456789012; +91 9876543210; me@example.com; CIF: CUST123456");

        Assert.DoesNotContain("ABCDE1234F", value);
        Assert.DoesNotContain("123456789012", value);
        Assert.DoesNotContain("9876543210", value);
        Assert.DoesNotContain("me@example.com", value);
    }

    [Fact]
    public async Task Preview_ReportsHeaderFooterSecondTableAndAmbiguousDates()
    {
        const string csv = "\nSynthetic statement\nDate,Description,Debit,Credit\n02-04-2024,Coffee,350,\n11-12-2024,Salary,,1000\nCharges section for account\nDate,Charge,Amount\n01-01-2024,Fee,10";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var file = new FormFile(stream, 0, stream.Length, "file", "synthetic.csv");
        var service = new ImportProfilePreviewService(new StatementGridParser(), new StatementMaskingService());

        var preview = await service.PreviewAsync(file, null);

        Assert.Equal(3, preview.DetectedHeaderRow);
        Assert.Equal(8, preview.TotalRows);
        Assert.Contains(6, preview.FooterCandidateRows);
        Assert.Contains(7, preview.SecondaryTableCandidateRows);
        Assert.True(preview.HasAmbiguousDateFormat);
    }

    [Fact]
    public async Task Preview_ReturnsFirstAndLastHundredRowsWithoutOverlap()
    {
        var csv = "Date,Description\n" + string.Join('\n', Enumerable.Range(1, 250).Select(index => $"02-04-2024,Transaction {index}"));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var file = new FormFile(stream, 0, stream.Length, "file", "synthetic.csv");

        var preview = await new ImportProfilePreviewService(new StatementGridParser(), new StatementMaskingService()).PreviewAsync(file, null);

        Assert.Equal(100, preview.FirstRows.Count);
        Assert.Equal(100, preview.LastRows.Count);
        Assert.Equal(100, preview.FirstRows[^1].RowNumber);
        Assert.Equal(152, preview.LastRows[0].RowNumber);
        Assert.Empty(preview.FirstRows.Select(row => row.RowNumber).Intersect(preview.LastRows.Select(row => row.RowNumber)));
    }

    [Fact]
    public async Task Preview_XlsxFixture_MasksIdentifiersAndReturnsCandidates()
    {
        var preview = await new ImportProfilePreviewService(new StatementGridParser(), new StatementMaskingService()).PreviewAsync(Fixture("synthetic-statement.xlsx"), "Transactions");
        Assert.Equal(5, preview.DetectedHeaderRow); Assert.Contains(8, preview.FooterCandidateRows); Assert.Contains(9, preview.SecondaryTableCandidateRows);
        Assert.DoesNotContain(preview.FirstRows.SelectMany(row => row.Cells), cell => cell.Contains("ABCDE1234F") || cell.Contains("9876543210") || cell.Contains("help@example.test"));
    }

    [Fact]
    public async Task PreviewAsync_RejectsCommittedFixtureBytesWithFakeSupportedExtensionsWithoutLeakingCells()
    {
        var controller = new ImportProfilesController(new ImportProfilePreviewService(new StatementGridParser(), new StatementMaskingService()), null!);
        var xlsxPath = Path.Combine(Path.GetTempPath(), $"cashbook-{Guid.NewGuid():N}.xls");
        var xlsPath = Path.Combine(Path.GetTempPath(), $"cashbook-{Guid.NewGuid():N}.xlsx");
        try
        {
            await File.WriteAllBytesAsync(xlsxPath, await File.ReadAllBytesAsync(Path.Combine(StatementFixtureGenerator.DirectoryPath, "synthetic-statement.xlsx")));
            await File.WriteAllBytesAsync(xlsPath, await File.ReadAllBytesAsync(Path.Combine(StatementFixtureGenerator.DirectoryPath, "synthetic-statement.xls")));
            await using var xlsxStream = File.OpenRead(xlsxPath);
            await using var xlsStream = File.OpenRead(xlsPath);
            var xlsxAsXls = new FormFile(xlsxStream, 0, xlsxStream.Length, "file", "statement.xls");
            var xlsAsXlsx = new FormFile(xlsStream, 0, xlsStream.Length, "file", "statement.xlsx");

            var xlsxException = await Assert.ThrowsAsync<backend.common.StatementParsingException>(() => controller.Preview(xlsxAsXls, null, CancellationToken.None));
            var csvException = await Assert.ThrowsAsync<backend.common.StatementParsingException>(() => controller.Preview(xlsAsXlsx, null, CancellationToken.None));

            Assert.Equal("unsupported_file", xlsxException.Code);
            Assert.Equal("unsupported_file", csvException.Code);
            Assert.DoesNotContain("ABCDE1234F", xlsxException.Message);
            Assert.DoesNotContain("help@example.test", xlsxException.Message);
            Assert.DoesNotContain("ABCDE1234F", csvException.Message);
            Assert.DoesNotContain("help@example.test", csvException.Message);
        }
        finally
        {
            if (File.Exists(xlsxPath)) File.Delete(xlsxPath);
            if (File.Exists(xlsPath)) File.Delete(xlsPath);
        }
    }
}
