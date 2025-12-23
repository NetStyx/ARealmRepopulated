using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.Services;
using ARealmRepopulated.Core.Services.Chat;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Infrastructure;
using ARealmRepopulated.Windows;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace ARealmRepopulated;

public sealed class Plugin : IDalamudPlugin {
    public static ServiceProvider Services { get; private set; } = null!;

    private WindowSystem Windows { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface) {
        var serviceDescriptors = pluginInterface.Create<DalamudDiWrapper>()?.CreateServiceCollection()
            ?? throw new InvalidOperationException("Could not create dalamud service wrapper");

        serviceDescriptors
            .AddSingleton(this)
            .AddSingleton(new WindowSystem("ARealmRepopulated"))
            .AddSingleton<ChatCommands>()
            .AddSingleton<ChatBubbleService>()
            .AddSingleton<NpcServices>()
            .AddSingleton<NpcAppearanceService>()
            .AddSingleton<ScenarioOrchestrator>()
            .AddSingleton<ScenarioFileManager>()
            .AddSingleton<ScenarioMigrator>()
            .AddSingleton<PluginConfigMigration>()
            .AddSingleton<DebugOverlay>()
            .AddSingleton<ArrpGuiEmotePicker>()
            .AddWindow<ConfigWindow>()
            .AddTransientWindow<ScenarioEditorWindow>()
            .AddTransient<NpcActor>();

        Services = serviceDescriptors.BuildServiceProvider();
        Services.GetRequiredService<PluginConfigMigration>().Migrate();

        Windows = Services.GetRequiredService<WindowSystem>();
        ConfigWindow = Services.GetRequiredService<ConfigWindow>();

        pluginInterface.UiBuilder.Draw += () => Windows.Draw();
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        Services.GetRequiredService<ArrpDataCache>().Populate();
        Services.GetRequiredService<ScenarioMigrator>().Initialize();
        Services.GetRequiredService<ArrpDtrControl>().Initialize();
        Services.GetRequiredService<ChatCommands>().Initialize();
        Services.GetRequiredService<ScenarioOrchestrator>().Initialize();
        Services.GetRequiredService<NpcServices>().Initialize();
        Services.GetRequiredService<ScenarioFileManager>().StartMonitoring();
        Services.GetRequiredService<ChatBubbleService>();

        // set the event service to do a territory check cycle
        Services.GetRequiredService<ArrpEventService>().Arm();
    }

    public void Dispose() {
        Windows.RemoveAllWindows();
        Services.Dispose();
    }

    public void ToggleConfigUI()
        => ConfigWindow.Toggle();
}
