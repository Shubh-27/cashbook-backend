using System.Text.RegularExpressions;
using backend.service.Services.Interfaces;

namespace backend.service.Services.Implementations;

public sealed partial class StatementMaskingService : IStatementMaskingService
{
    [GeneratedRegex(@"(?<![\w.+-])[\w.+-]+@[\w-]+(?:\.[\w-]+)+(?![\w.+-])", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b[A-Z]{5}[0-9]{4}[A-Z]\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PanRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+91[-\s]?)?[6-9]\d{9}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<!\d)\d{9,18}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex AccountNumberRegex();

    [GeneratedRegex(@"\b(?:customer\s*(?:id|no)|cust\s*(?:id|no)|cif)\s*[:#-]?\s*[A-Z0-9]{6,}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CustomerIdRegex();

    public string MaskCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var masked = EmailRegex().Replace(value, match => MaskEmail(match.Value));
        masked = PanRegex().Replace(masked, match => MaskKeepLast(match.Value, 4));
        masked = PhoneRegex().Replace(masked, match => MaskKeepLast(match.Value, 4));
        masked = AccountNumberRegex().Replace(masked, match => MaskKeepLast(match.Value, 4));
        return CustomerIdRegex().Replace(masked, match => MaskCustomerId(match.Value));
    }

    public List<string> MaskRow(IEnumerable<string> values) => values.Select(MaskCell).ToList();

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        var local = email[..at];
        return $"{(local.Length <= 2 ? "**" : local[..2] + new string('*', local.Length - 2))}{email[at..]}";
    }

    private static string MaskKeepLast(string value, int keep)
    {
        var visible = new string(value.Where(char.IsLetterOrDigit).TakeLast(keep).ToArray());
        return new string('*', Math.Max(4, value.Count(char.IsLetterOrDigit) - keep)) + visible;
    }

    private static string MaskCustomerId(string value)
    {
        var separator = value.LastIndexOfAny([':', '#', '-']);
        return separator >= 0 ? $"{value[..(separator + 1)]} {MaskKeepLast(value[(separator + 1)..].Trim(), 4)}" : "Customer ID: ****";
    }
}
