using Dalamud.Plugin.Services;
using System.IO;
using System.Reflection;
using System.Text;

namespace ARealmRepopulated.Data.Appearance;

public class NpcAppearanceDataParser(IServiceProvider serviceProvider, IPluginLog log) {

    private readonly SortedDictionary<int, IAppearanceFileParser> _parsers = [];

    public void Initialize() {
        var currentAssembly = typeof(NpcAppearanceDataParser).Assembly;
        var parserInterface = typeof(IAppearanceFileParser);

        log.Debug("Enumerating available scenario migrations");
        var parserTypes = currentAssembly.GetTypes().Where(t => t.IsAssignableTo(parserInterface) && t != parserInterface);
        foreach (var parserType in parserTypes) {
            if (Attribute.GetCustomAttribute(parserType, typeof(AppearanceParser)) is AppearanceParser parserAttribute &&
                ActivatorUtilities.CreateInstance(serviceProvider, parserType) is IAppearanceFileParser parserInstance) {

                log.Debug($"Found {parserInstance.GetType().Name}");
                _parsers.Add(parserAttribute.Priority, parserInstance);
            }
        }
    }

    public NpcAppearanceData? TryParseAppearanceFile(string filePath) {

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists) {
            log.Warning("The file '{File}' does not exist", [filePath]);
            return null;
        }

        byte[] fileData = [];
        try {
            fileData = File.ReadAllBytes(filePath);
        } catch (Exception ex) {
            log.Warning(ex, "The file '{File}' is not readable", [filePath]);
            return null;
        }

        var dedicatedParser = _parsers.Select(d => d.Value).FirstOrDefault(p => p.GetType().GetCustomAttribute<AppearanceParser>()?.Extension == fileInfo.Extension);
        if (dedicatedParser != null) {
            var result = dedicatedParser.TryParse(fileData);
            if (result == null) {
                log.Warning("The file '{File}' could not be parsed by {Parser}", [filePath, dedicatedParser.GetType().Name]);
            }

            return result;
        }

        return TryParseRawBytes(fileData);
    }

    public NpcAppearanceData? TryParseAppearanceData(string appearance) {

        byte[] stringData;
        try {
            stringData = Encoding.UTF8.GetBytes(appearance);
        } catch (Exception ex) {
            log.Warning(ex, "Could not parse clipboard contents");
            return null;
        }

        return TryParseRawBytes(stringData);
    }

    private NpcAppearanceData? TryParseRawBytes(byte[] data) {
        foreach (var parser in _parsers) {
            var result = parser.Value.TryParse(data);
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
    NpcAppearanceData? TryParse(byte[] fileData);
}

