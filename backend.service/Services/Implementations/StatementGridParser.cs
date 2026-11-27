using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using backend.common;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Services.Interfaces;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Implementations;

public sealed partial class StatementGridParser : IStatementGridParser
{
    public const long DefaultMaxFileBytes = 10 * 1024 * 1024;
    public const int DefaultMaxWorksheets = 20;
    public const int DefaultMaxRows = 25_000;
    public const int DefaultMaxColumns = 200;
    public const long DefaultMaxXlsxDecompressedBytes = 50 * 1024 * 1024;
    public const double HeaderMatchThreshold = 0.80;

    static StatementGridParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public async Task<IReadOnlyList<StatementWorksheetGrid>> ParseAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        (await ParseWithMetadataAsync(file, cancellationToken)).Grids;

    public async Task<StatementParseResult> ParseWithMetadataAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0) throw new StatementParsingException("empty_file", "Choose a non-empty statement file.");
        if (file.Length > DefaultMaxFileBytes) throw new StatementParsingException("file_too_large", "Statement files must be 10 MB or smaller.");

        using var buffer = new MemoryStream((int)Math.Min(file.Length, DefaultMaxFileBytes));
        await using (var source = file.OpenReadStream())
            await source.CopyToAsync(buffer, cancellationToken);

        buffer.Position = 0;
        var hashBytes = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        buffer.Position = 0;
        var type = await DetectTypeFromStreamAsync(buffer, file.FileName, cancellationToken);

        buffer.Position = 0;
        IReadOnlyList<StatementWorksheetGrid> grids = type switch
        {
            "csv" => [new StatementWorksheetGrid { Name = "CSV", Rows = ReadCsvFromBytes(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) }],
            "xls" or "xlsx" => ReadWorkbookFromStream(buffer, cancellationToken),
            _ => throw new StatementParsingException("unsupported_file", "The file content is not CSV, XLS, or XLSX.")
        };

        return new StatementParseResult(type, fileHash, grids);
    }

    private static void ValidateBinaryExtension(string fileName, string detectedType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var expectedType = extension switch { ".csv" => "csv", ".xls" => "xls", ".xlsx" => "xlsx", _ => null };
        // Text exports are parsed by content despite a misleading .xls/.xlsx suffix.
        // Genuine workbook bytes must match their supported binary extension.
        if (detectedType != "csv" && expectedType != null && expectedType != detectedType)
            throw new StatementParsingException("unsupported_file", "The file extension does not match the detected file content.");
    }

    public HeaderMatchResult MatchHeader(IReadOnlyList<string> savedHeaderCells, IReadOnlyList<string> candidateHeaderCells)
    {
        var expected = savedHeaderCells.Select(NormalizeHeader).Where(value => value.Length > 0).ToList();
        if (expected.Count == 0) return new HeaderMatchResult();
        var actual = candidateHeaderCells.Select(NormalizeHeader).ToList();
        var inOrder = 0;
        var cursor = 0;
        foreach (var cell in expected)
        {
            var index = actual.FindIndex(cursor, value => value == cell);
            if (index >= 0) { inOrder++; cursor = index + 1; }
        }
        var positionMatches = expected.Select((cell, index) => index < actual.Count && actual[index] == cell).Count(match => match);
        var confidence = Math.Max((double)inOrder / expected.Count, (double)positionMatches / expected.Count);
        return new HeaderMatchResult { Confidence = confidence, IsMatch = confidence >= HeaderMatchThreshold };
    }

    public bool HasAmbiguousDayMonthDates(IEnumerable<string> values)
    {
        var parsed = values.Select(value => Regex.Match(value.Trim(), @"^(\d{1,2})[-/](\d{1,2})[-/]\d{2,4}$"))
            .Where(match => match.Success).ToList();
        return parsed.Count > 0 && parsed.All(match => int.Parse(match.Groups[1].Value) <= 12 && int.Parse(match.Groups[2].Value) <= 12);
    }

    public async Task<string> DetectFileTypeAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        return await DetectTypeFromStreamAsync(stream, file.FileName, cancellationToken);
    }

    private static async Task<string> DetectTypeFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        var read = await stream.ReadAsync(header, cancellationToken);
        string detected;
        if (read >= 8 && header.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })) detected = "xls";
        else if (read >= 4 && header[..4].SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var decompressed = archive.Entries.Sum(entry => entry.Length);
            if (decompressed > DefaultMaxXlsxDecompressedBytes) throw new StatementParsingException("xlsx_too_large", "The XLSX workbook exceeds the decompressed-size limit.");
            if (archive.GetEntry("[Content_Types].xml") != null && archive.GetEntry("xl/workbook.xml") != null) detected = "xlsx";
            else throw new StatementParsingException("invalid_zip", "The ZIP file is not a valid XLSX workbook.");
        }
        else
        {
            stream.Position = 0;
            var text = await ReadTextAsync(stream, cancellationToken);
            var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
                throw new StatementParsingException("html_spreadsheet", "This HTML spreadsheet must be opened in Excel and saved as .xlsx or .csv.");
            detected = "csv";
        }
        ValidateBinaryExtension(fileName, detected);
        return detected;
    }

    private static List<StatementGridRow> ReadCsvFromBytes(ReadOnlySpan<byte> bytes)
    {
        var text = DecodeTextBytes(bytes);
        return ParseCsv(text).Select((cells, index) => new StatementGridRow { RowNumber = index + 1, Cells = ValidateRow(cells) }).ToList();
    }

    private static IReadOnlyList<StatementWorksheetGrid> ReadWorkbookFromStream(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var sheets = new List<StatementWorksheetGrid>();
        do
        {
            if (sheets.Count == DefaultMaxWorksheets) throw new StatementParsingException("too_many_worksheets", $"Workbooks may contain at most {DefaultMaxWorksheets} worksheets.");
            var rows = new List<StatementGridRow>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count == DefaultMaxRows) throw new StatementParsingException("too_many_rows", $"Worksheets may contain at most {DefaultMaxRows} rows.");
                if (reader.FieldCount > DefaultMaxColumns) throw new StatementParsingException("too_many_columns", $"Worksheets may contain at most {DefaultMaxColumns} columns.");
                rows.Add(new StatementGridRow { RowNumber = rows.Count + 1, Cells = ValidateRow(Enumerable.Range(0, reader.FieldCount).Select(index => FormatCell(reader.GetValue(index))).ToList()) });
            }
            sheets.Add(new StatementWorksheetGrid { Name = reader.Name, Rows = rows });
        } while (reader.NextResult());
        foreach (var sheet in sheets) { sheet.TotalRows = sheet.Rows.Count; sheet.TotalColumns = sheet.Rows.Count == 0 ? 0 : sheet.Rows.Max(row => row.Cells.Count); }
        return sheets;
    }

    private static List<string> ValidateRow(List<string> cells)
    {
        if (cells.Count > DefaultMaxColumns) throw new StatementParsingException("too_many_columns", $"Worksheets may contain at most {DefaultMaxColumns} columns.");
        return cells;
    }

    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static List<List<string>> ParseCsv(string text)
    {
        var delimiter = DetectDelimiter(text);
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"') { if (quoted && index + 1 < text.Length && text[index + 1] == '"') { cell.Append(character); index++; } else quoted = !quoted; }
            else if (character == delimiter && !quoted) { row.Add(cell.ToString()); cell.Clear(); }
            else if ((character == '\n' || character == '\r') && !quoted) { if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++; row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(character);
        }
        if (quoted) throw new StatementParsingException("invalid_csv", "The CSV file has an unclosed quoted value.");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        if (rows.Count > DefaultMaxRows) throw new StatementParsingException("too_many_rows", $"Worksheets may contain at most {DefaultMaxRows} rows.");
        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return new[] { ',', '\t', ';', '|' }.OrderByDescending(delimiter => line.Count(character => character == delimiter)).First();
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return DecodeTextBytes(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private static string DecodeTextBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) return Encoding.Unicode.GetString(bytes[2..]);
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return Encoding.UTF8.GetString(bytes[3..]);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(bytes); }
    }

    public static string NormalizeHeader(string? value) => WhitespaceRegex().Replace((value ?? string.Empty).Trim().ToLowerInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
