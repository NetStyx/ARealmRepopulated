using System.Text;

namespace ARealmRepopulated.Core.Services.Changelog;

public static class ChangelogParser {
    public static ChangelogEntry Parse(Version version, string markdown) {
        ArgumentNullException.ThrowIfNull(markdown);

        ChangelogEntry? entry = null;
        ChangelogSection? currentSection = null;
        var description = new StringBuilder();

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++) {
            var line = lines[lineNumber].Trim();
            var sourceLineNumber = lineNumber + 1;

            if (line.Length == 0)
                continue;

            if (line.StartsWith("# ", StringComparison.Ordinal)) {
                if (entry is not null) {
                    throw new FormatException($"Line {sourceLineNumber}: only one '# ' changelog title is allowed.");
                }

                var title = line[2..].Trim();
                if (title.Length == 0) {
                    throw new FormatException($"Line {sourceLineNumber}: the changelog title cannot be empty.");
                }

                entry = new ChangelogEntry { Version = version, Title = title };
                continue;
            }

            if (entry is null)
                continue;

            if (line.StartsWith("## ", StringComparison.Ordinal)) {
                var title = line[3..].Trim();
                if (title.Length == 0) {
                    throw new FormatException($"Line {sourceLineNumber}: the section title cannot be empty.");
                }

                currentSection = new ChangelogSection { Title = title };
                entry.Sections.Add(currentSection);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal)) {
                if (currentSection is null) {
                    throw new FormatException($"Line {sourceLineNumber}: a change must appear below a '## ' section.");
                }

                var change = line[2..].Trim();
                if (change.Length == 0) {
                    throw new FormatException($"Line {sourceLineNumber}: a change cannot be empty.");
                }

                currentSection.Changes.Add(change);
                continue;
            }

            if (currentSection is not null) {
                throw new FormatException($"Line {sourceLineNumber}: plain text is only allowed before the first section.");
            }

            if (description.Length > 0)
                description.Append(' ');

            description.Append(line);
        }

        if (entry is null) {
            throw new FormatException("The changelog must contain exactly one '# ' title.");
        }

        entry.Description = description.ToString();
        return entry;
    }
}

public sealed class ChangelogEntry {
    public required Version Version { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<ChangelogSection> Sections { get; } = [];
}

public sealed class ChangelogSection {
    public required string Title { get; set; } = string.Empty;
    public List<string> Changes { get; } = [];
}
