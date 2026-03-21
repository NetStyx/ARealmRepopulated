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
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ServiceProvider _services;

    private WindowSystem Windows { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private OnboardingWindow OnboardingWindow { get; init; }

    private readonly Action _drawAction;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        _pluginInterface = pluginInterface;

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

        _services = serviceDescriptors.BuildServiceProvider();
        _services.GetRequiredService<PluginConfigMigration>().Migrate();

        Windows = _services.GetRequiredService<WindowSystem>();
        ConfigWindow = _services.GetRequiredService<ConfigWindow>();
        OnboardingWindow = _services.GetRequiredService<OnboardingWindow>();

        var fileDialogManager = _services.GetRequiredService<FileDialogManager>();

        _drawAction = () => {
            Windows.Draw();
            fileDialogManager.Draw();
        };
        pluginInterface.UiBuilder.Draw += _drawAction;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        _services.GetRequiredService<ArrpDataCache>().Populate();
        _services.GetRequiredService<ArrpCharacterCreationData>().Populate();
        _services.GetRequiredService<ScenarioMigrator>().Initialize();
        _services.GetRequiredService<ArrpDtrControl>().Initialize();
        _services.GetRequiredService<ChatCommands>().Initialize();
        _services.GetRequiredService<ScenarioOrchestrator>().Initialize();
        _services.GetRequiredService<NpcServices>().Initialize();
        _services.GetRequiredService<NpcAppearanceDataParser>().Initialize();
        _services.GetRequiredService<ScenarioFileManager>().StartMonitoring();
        _services.GetRequiredService<ChatBubbleService>();
        _services.GetRequiredService<ArrpTranslation>().SetLocale(CultureInfo.GetCultureInfo("en-us"));

        // set the event service to do a territory check cycle
        var eventService = _services.GetRequiredService<ArrpEventService>();
        if (!_services.GetRequiredService<PluginConfig>().OnboardingCompleted) {
            eventService.OnTerritoryLoadFinished += RunOnboarding;
        }
        _services.GetRequiredService<ArrpEventService>().Arm();

    }

    private void RunOnboarding(LocationData _) {
        _services.GetRequiredService<ArrpEventService>().OnTerritoryLoadFinished -= RunOnboarding;
        OnboardingWindow.Toggle();
    }

    public void Dispose() {
        _pluginInterface.UiBuilder.Draw -= _drawAction;
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;

        Windows.RemoveAllWindows();
        _services.Dispose();
    }

    public void ToggleConfigUI()
        => ConfigWindow.Toggle();
}
