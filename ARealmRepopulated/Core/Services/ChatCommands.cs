using ARealmRepopulated.Core.Services.Scenarios;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.Services;

public class ChatCommands(Plugin plugin, ICommandManager commandManager, ScenarioOrchestrator scenarioManager) : IDisposable {

    private const string OpenArrpCommand = "/arrp";
    private const string ReloadScenarioCommand = "/arrp reload";
    public void Initialize() {
        commandManager.AddHandler(OpenArrpCommand, new CommandInfo(OpenConfigWindow) { ShowInHelp = true, HelpMessage = "Opens the configuration window" });
        commandManager.AddHandler(ReloadScenarioCommand, new CommandInfo(ReloadScenarios) { ShowInHelp = true, HelpMessage = "Reloads the scenarios in the current area" });
    }

    private void ReloadScenarios(string _, string __) {
        scenarioManager.Reload();
    }

    private void OpenConfigWindow(string command, string args) {
        plugin.ToggleConfigUI();
    }

    public void Dispose() {
        commandManager.RemoveHandler(OpenArrpCommand);
        commandManager.RemoveHandler(ReloadScenarioCommand);
        GC.SuppressFinalize(this);
    }
}
