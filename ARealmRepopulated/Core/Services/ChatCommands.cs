using ARealmRepopulated.Core.Services.Scenarios;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.Services;

public class ChatCommands(Plugin plugin, ICommandManager commandManager, ScenarioOrchestrator scenarioManager) : IDisposable {
    
    private const string ArrpCommand = "/arrp";
    private const string ReloadArgument = "reload";

    public void Initialize() {
        commandManager.AddHandler(ArrpCommand, new CommandInfo(HandleArrpCommand) {
            ShowInHelp = true, HelpMessage = $"Opens the configuration window\n{ArrpCommand} {ReloadArgument} → Reloads the scenarios in the current area"
        });
    }

    private void HandleArrpCommand(string command, string args) {
        if (args.Trim().Equals(ReloadArgument, StringComparison.OrdinalIgnoreCase)) {
            scenarioManager.Reload();
        } else {
            plugin.ToggleConfigUI();
        }
    }

    public void Dispose() {
        commandManager.RemoveHandler(ArrpCommand);
        GC.SuppressFinalize(this);
    }
}
