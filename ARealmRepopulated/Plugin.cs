using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services;
using ARealmRepopulated.Core.Services.Chat;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Infrastructure;
using ARealmRepopulated.Windows;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System.Globalization;

namespace ARealmRepopulated;

public sealed class Plugin : IDalamudPlugin {
    public static ServiceProvider Services { get; private set; } = null!;

    private WindowSystem Windows { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private OnboardingWindow OnboardingWindow { get; init; }

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
            .AddSingleton<NpcAppearanceDataParser>()
            .AddSingleton<ScenarioOrchestrator>()
            .AddSingleton<ScenarioFileManager>()
            .AddSingleton<ScenarioMigrator>()
            .AddSingleton<PluginConfigMigration>()
            .AddSingleton<DebugOverlay>()
            .AddSingleton<ArrpGuiEmotePicker>()
            .AddSingleton<FileDialogManager>()
            .AddWindow<ConfigWindow>()
            .AddWindow<OnboardingWindow>()
            .AddTransientWindow<ScenarioEditorWindow>()
            .AddTransient<NpcActor>()
            .AddTransient<Scenario>()
            .AddTransient<ScenarioNpc>();

        Services = serviceDescriptors.BuildServiceProvider();
        Services.GetRequiredService<PluginConfigMigration>().Migrate();

        Windows = Services.GetRequiredService<WindowSystem>();
        ConfigWindow = Services.GetRequiredService<ConfigWindow>();
        OnboardingWindow = Services.GetRequiredService<OnboardingWindow>();

        var fileDialogManager = Services.GetRequiredService<FileDialogManager>();

        pluginInterface.UiBuilder.Draw += () => {
            Windows.Draw();
            fileDialogManager.Draw();
        };
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        Services.GetRequiredService<ArrpDataCache>().Populate();
        Services.GetRequiredService<ArrpCharacterCreationData>().Populate();
        Services.GetRequiredService<ScenarioMigrator>().Initialize();
        Services.GetRequiredService<ArrpDtrControl>().Initialize();
        Services.GetRequiredService<ChatCommands>().Initialize();
        Services.GetRequiredService<ScenarioOrchestrator>().Initialize();
        Services.GetRequiredService<NpcServices>().Initialize();
        Services.GetRequiredService<NpcAppearanceDataParser>().Initialize();
        Services.GetRequiredService<ScenarioFileManager>().StartMonitoring();
        Services.GetRequiredService<ChatBubbleService>();
        Services.GetRequiredService<ArrpTranslation>().SetLocale(CultureInfo.GetCultureInfo("en-us"));

        // set the event service to do a territory check cycle
        var eventService = Services.GetRequiredService<ArrpEventService>();
        if (!Services.GetRequiredService<PluginConfig>().OnboardingCompleted) {
            eventService.OnTerritoryLoadFinished += RunOnboarding;
        }
        Services.GetRequiredService<ArrpEventService>().Arm();

    }

    private void RunOnboarding(LocationData _) {
        Services.GetRequiredService<ArrpEventService>().OnTerritoryLoadFinished -= RunOnboarding;
        OnboardingWindow.Toggle();
    }

    public void Dispose() {
        Windows.RemoveAllWindows();
        Services.Dispose();
    }

    public void ToggleConfigUI()
        => ConfigWindow.Toggle();
}
