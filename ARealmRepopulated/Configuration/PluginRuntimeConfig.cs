namespace ARealmRepopulated.Configuration;

internal class PluginRuntimeConfig {

    public bool ModdingToolsInstalled => LoadedModdingPlugins.Count != 0;
    public List<string> LoadedModdingPlugins { get; } = [];

}
