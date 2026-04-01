using System.Text.RegularExpressions;

namespace NoPremium2.Email;

public sealed class VoucherCodeExtractor
{
    // Matches: "Twój kod doładowujący: 5de8f8cc3506229fa4a"
    // The pattern uses . for Polish characters (ł, ó, etc.)
    private static readonly Regex Pattern = new(
        @"kod do.adowuj.cy:\s+.*?\b(?<code>[0-9a-fA-F]{10,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Extracts the voucher code from an email body. Returns null if not found.</summary>
    public string? ExtractFrom(string? emailBody)
    {
        if (string.IsNullOrWhiteSpace(emailBody)) return null;
        var match = Pattern.Match(emailBody);
        return match.Success ? match.Groups["code"].Value : null;
    }
}
