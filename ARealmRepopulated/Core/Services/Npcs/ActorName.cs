using System.Text;

namespace ARealmRepopulated.Core.Services.Npcs;

/// <summary>
/// Utility class for working with actor names.
/// The game stores the name as UTF-8 in a fixed 64 byte buffer, so every limit here is counted in bytes rather than characters. 
/// Why you ask? A latin letter takes one byte while a chinese character takes three and the naming rules differ between regions. 
/// </summary>
public static class ActorName {
    public const string IntegrationPrefix = "Arrp ";
    public const int MaxNameBytes = 63;    
    public const int MaxPrefixedNameBytes = 15;

    public static bool HasPrefix(string name)
        => name?.StartsWith(IntegrationPrefix, StringComparison.Ordinal) ?? false;

    public static string WithoutPrefix(string name)
        => HasPrefix(name) ? name[IntegrationPrefix.Length..] : (name ?? "");

    public static string Clean(string value) {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var namePart = new StringBuilder(value.Length);
        foreach (var character in value) {
            if (char.IsAsciiLetter(character)) {
                namePart.Append(namePart.Length == 0 ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            } else if (character is '-' or '\'' && namePart.Length > 0 && char.IsAsciiLetter(namePart[^1])) {
                namePart.Append(character);
            }
        }

        return namePart.ToString();
    }

    public static string Filter(string value)
        => TruncateToBytes(Clean(value), MaxPrefixedNameBytes).TrimEnd('-', '\'');
    

    public static string TruncateToBytes(string value, int maxBytes) {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0)
            return "";

        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            return value;

        var usedBytes = 0;
        var usedChars = 0;
        foreach (var rune in value.EnumerateRunes()) {
            if (usedBytes + rune.Utf8SequenceLength > maxBytes)
                break;

            usedBytes += rune.Utf8SequenceLength;
            usedChars += rune.Utf16SequenceLength;
        }

        return value[..usedChars];
    }

    public static byte[] Encode(string name)
        => Encoding.UTF8.GetBytes(TruncateToBytes(name, MaxNameBytes));
}
