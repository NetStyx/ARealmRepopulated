using Dalamud.Plugin.Services;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios;

public class ScenarioMigrator(IServiceProvider serviceProvider, IPluginLog log) {

    public static int CurrentScenarioVersion { get; } = 6;

    private readonly SortedDictionary<int, IScenarioMigration> _migrationDictionary = [];

    public void Initialize() {
        var currentAssembly = typeof(ScenarioMigrator).Assembly;
        var migrationInterface = typeof(IScenarioMigration);

        log.Debug("Enumerating available scenario migrations");
        var migrationTypes = currentAssembly.GetTypes().Where(t => t.IsAssignableTo(migrationInterface) && t != migrationInterface);
        foreach (var migrationType in migrationTypes) {
            if (Attribute.GetCustomAttribute(migrationType, typeof(ScenarioMigrationAttribute)) is ScenarioMigrationAttribute migrationAttribute &&
                ActivatorUtilities.CreateInstance(serviceProvider, migrationType) is IScenarioMigration migrationInstance) {
                log.Debug($"Found {migrationInstance.GetType().Name} for version {migrationAttribute.Version}");
                _migrationDictionary.Add(migrationAttribute.Version, migrationInstance);
            }
        }
    }

    public ScenarioMigrationResult? Migrate(string json) {
        if (JsonNode.Parse(json) is not JsonObject root) {
            return null;
        }

        var version = root["Version"]?.GetValue<int>() ?? 0;
        if (version >= CurrentScenarioVersion) {
            return new ScenarioMigrationResult(root, false);
        }

        foreach (var kvp in _migrationDictionary) {
            if (version < kvp.Key) {
                log.Info($"Migrating scenario from version {version} to version {kvp.Key}");
                kvp.Value.Upgrade(root);

                version++;
                root["Version"] = version;
            }
        }
        return new ScenarioMigrationResult(root, true);
    }

}

public sealed record ScenarioMigrationResult(JsonObject Scenario, bool WasUpgraded);

[AttributeUsage(AttributeTargets.Class)]
public class ScenarioMigrationAttribute : Attribute {
    public int Version { get; set; }
    public string Description { get; set; } = "";
}

public interface IScenarioMigration {
    void Upgrade(JsonObject jsonObject);
}
