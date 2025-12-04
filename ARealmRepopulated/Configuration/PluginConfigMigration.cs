using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Configuration;

public class PluginConfigMigration(PluginConfig config, IPluginLog log) {

    public void Migrate() {

        var currentAssembly = typeof(PluginConfigMigration).Assembly;
        var migrationDictionary = new SortedDictionary<int, IConfigMigration>();
        var migrationInterface = typeof(IConfigMigration);

        log.Debug("Enumerating available version migrations");
        var migrationTypes = currentAssembly.GetTypes().Where(t => t.IsAssignableTo(migrationInterface) && t != migrationInterface);
        foreach (var migrationType in migrationTypes) {
            var migrationInstance = Activator.CreateInstance(migrationType) as IConfigMigration;
            var migrationAttribute = Attribute.GetCustomAttribute(migrationType, typeof(ConfigMigrationAttribute)) as ConfigMigrationAttribute;
            if (migrationInstance != null && migrationAttribute != null) {
                log.Debug($"Found {migrationInstance.GetType().Name} for version {migrationAttribute.Version}");
                migrationDictionary.Add(migrationAttribute.Version, migrationInstance);
            }
        }

        foreach (var kvp in migrationDictionary) {
            if (config.Version < kvp.Key) {
                log.Info($"Migrating configuration from version {config.Version} to version {kvp.Key}");
                kvp.Value.Upgrade(config);
                config.Version = kvp.Key;
                config.Save();
            }
        }
    }

}

public class ConfigMigrationAttribute : Attribute {
    public int Version { get; set; }
}
public interface IConfigMigration {
    void Upgrade(PluginConfig config);
}
