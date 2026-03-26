namespace LanBot.Domain;

public static class NameNormalization
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var collapsedWhitespace = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsedWhitespace.ToUpperInvariant();
    }
}
