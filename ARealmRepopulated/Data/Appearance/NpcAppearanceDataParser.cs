using Dalamud.Plugin.Services;
using System.IO;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Data.Appearance;

public class NpcAppearanceDataParser(IPluginLog log) {

    private readonly SortedDictionary<int, IAppearanceFileParser> _parsers = [];

    public void Initialize() {
        var currentAssembly = typeof(NpcAppearanceDataParser).Assembly;
        var parserInterface = typeof(IAppearanceFileParser);

        log.Debug("Enumerating available scenario migrations");
        var parserTypes = currentAssembly.GetTypes().Where(t => t.IsAssignableTo(parserInterface) && t != parserInterface);
        foreach (var parserType in parserTypes) {
            if (Attribute.GetCustomAttribute(parserType, typeof(AppearanceParser)) is AppearanceParser parserAttribute &&
                Activator.CreateInstance(parserType) is IAppearanceFileParser parserInstance) {
                log.Debug($"Found {parserInstance.GetType().Name}");
                _parsers.Add(parserAttribute.Priority, parserInstance);
            }
        }
    }

    public NpcAppearanceData? TryParseAppearanceFile(string filePath) {

        if (!File.Exists(filePath))
            return null;

        try {
            return TryParseAppearanceData(File.ReadAllText(filePath));
        } catch (Exception ex) {
            log.Error(ex, "The file '{File}' does not exist or is not readable", [filePath]);
        }

        return null;
    }

    public NpcAppearanceData? TryParseAppearanceData(string appearanceData) {

        string rawData;
        JsonObject jsonData;
        try {
            var parsedSpan = new Span<byte>(new byte[4096]);
            if (Convert.TryFromBase64String(appearanceData, parsedSpan, out var writtenData)) {
                rawData = System.Text.Encoding.UTF8.GetString(parsedSpan[..writtenData]);
            } else {
                rawData = appearanceData;
            }
        } catch (Exception ex) {
            log.Error(ex, "Could not parse the given base64 structure");
            return null;
        }

        try {
            jsonData = JsonNode.Parse(rawData)?.AsObject() ?? [];
        } catch (Exception ex) {
            log.Error(ex, "Could not parse the given JSON structure");
            return null;
        }

        if (jsonData == null) {
            log.Error("Received no valid json object");
            return null;
        }

        foreach (var parser in _parsers) {

            var result = parser.Value.TryParse(jsonData);
            if (result != null)
                return result;
        }

        return null;
    }

}

[AttributeUsage(AttributeTargets.Class)]
public class AppearanceParser : Attribute {
    public string Extension { get; set; } = "";
    public int Priority { get; set; } = 0;
}

public interface IAppearanceFileParser {
    NpcAppearanceData? TryParse(JsonObject data);
}
