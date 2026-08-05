using Dalamud.Plugin.Services;
using System.IO;

namespace ARealmRepopulated.Core.Services.Changelog;

public class ChangelogService(IPluginLog log) {

    public List<ChangelogEntry> Changelog { get; init; } = [];

    public void Initialize() {
        try {
            TryParseVersions();
        } catch (Exception ex) {
            log.Error(ex, "Error occurred while initializing changelog");
        }
    }

    private void TryParseVersions() {
        var assembly = typeof(ChangelogService).Assembly;

        var prefix = $"{assembly.GetName().Name}.Data.Changelogs.";
        var suffix = $".md";

        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(suffix))
            .ToList();

        foreach (var changelogEntry in resourceNames) {

            using var stream = assembly.GetManifestResourceStream(changelogEntry);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);

            var versionString = changelogEntry.AsSpan(prefix.Length, changelogEntry.Length - prefix.Length - suffix.Length).ToString();
            var version = Version.TryParse(versionString, out var parsedVersion) ? parsedVersion : new Version(0, 0, 0, 0);

            var parsedChangelog = ChangelogParser.Parse(version, reader.ReadToEnd());
            log.Debug("Found changelog for version {Version}", versionString);

            Changelog.Add(parsedChangelog);
        }
    }

}
