using ARealmRepopulated.Data.Scenarios;
using Dalamud.Plugin.Services;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios;

public class ScenarioMigrator(IPluginLog log) {

    public static int CurrentScenarioVersion { get; } = 2;

    private readonly SortedDictionary<int, IScenarioMigration> _migrationDictionary = [];

    public void Initialize() {
        var currentAssembly = typeof(ScenarioMigrator).Assembly;
        var migrationInterface = typeof(IScenarioMigration);

        log.Debug("Enumerating available scenario migrations");
        var migrationTypes = currentAssembly.GetTypes().Where(t => t.IsAssignableTo(migrationInterface) && t != migrationInterface);
        foreach (var migrationType in migrationTypes) {
            if (Attribute.GetCustomAttribute(migrationType, typeof(ScenarioMigrationAttribute)) is ScenarioMigrationAttribute migrationAttribute &&
                Activator.CreateInstance(migrationType) is IScenarioMigration migrationInstance) {
                log.Debug($"Found {migrationInstance.GetType().Name} for version {migrationAttribute.Version}");
                _migrationDictionary.Add(migrationAttribute.Version, migrationInstance);
            }
        }
    }

    public bool Migrate(FileInfo fileInfo, [NotNullWhen(true)] out ScenarioFileMetaData? metaData) {

        var json = File.ReadAllText(fileInfo.FullName);
        var root = JsonNode.Parse(json)?.AsObject();
        if (root == null) {
            metaData = null;
            return false;
        }

        var version = root["Version"]?.GetValue<int>() ?? 0;
        if (version < CurrentScenarioVersion) {
            foreach (var kvp in _migrationDictionary) {
                if (version < kvp.Key) {
                    log.Info($"Migrating scenario from version {version} to version {kvp.Key}");
                    kvp.Value.Upgrade(root);

                    version++;
                    root["Version"] = version;
                }
            }

            File.WriteAllText(fileInfo.FullName, JsonSerializer.Serialize(root, ScenarioFileManager.ScenarioMetaSerializerOptions));
        }

        metaData = root.Deserialize<ScenarioFileMetaData>(ScenarioFileManager.ScenarioMetaSerializerOptions)
                ?? throw new InvalidDataException("File is not a valid scenario data format");
        return true;
    }

}

[AttributeUsage(AttributeTargets.Class)]
public class ScenarioMigrationAttribute : Attribute {
    public int Version { get; set; }
    public string Description { get; set; } = "";
}

public interface IScenarioMigration {
    void Upgrade(JsonObject jsonObject);
}
