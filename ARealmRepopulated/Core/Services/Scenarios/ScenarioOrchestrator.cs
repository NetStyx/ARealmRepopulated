using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Scenarios;

public unsafe class ScenarioOrchestrator(IFramework framework, IPluginLog pluginLog, IObjectTable objectTable, IServiceProvider serviceProvider, ScenarioFileManager fileManager, PluginConfig config, NpcServices npcServices, ArrpGameHooks hooks, ArrpEventService eventService) : IDisposable {

    private readonly Lock _scenarioActionLock = new();
    private const float ProximityCheckInterval = 0.5f;
    private float _lastProximityCheck = 0f;

    public List<Orchestration> Orchestrations { get; set; } = [];
    public event Action? OnOrchestrationsChanged;

    private void Game_CharacterDestroyed(Character* chara) {
        using var lockScope = _scenarioActionLock.EnterScope();
        foreach (var orchestration in Orchestrations) {
            var scenarioNpc = orchestration.Scenario.Npcs.FirstOrDefault(n => (Character*)n.Actor.Address == chara);
            if (scenarioNpc != null) {
                pluginLog.Verbose($"Character finalization in progress. Removing character {scenarioNpc.Actor.Address:X} from scenario");
                orchestration.Scenario.Npcs.Remove(scenarioNpc);
            }
        }
    }

    private void EventService_OnTerritoryReady(LocationData territory) {
        if (config.AutoLoadScenarios) {
            Load(eventService.CurrentLocation);
        }
    }

    private void ScenarioFileManager_ScenarioFileChanged(ScenarioFileData metaData)
        => LoadFile(metaData);

    private void ScenarioFileManager_ScenarioFileRemoved(ScenarioFileData metaData)
        => UnloadFile(metaData);

    private void Framework_Update(IFramework framework)
        => AdvanceScenarios(framework.UpdateDelta);

    private void Load(LocationData locationData) {
        Unload();

        pluginLog.Info($"Loading scenarios for territory {locationData.TerritoryType}");
        fileManager.GetScenarioFilesByTerritory(locationData).ForEach(LoadFile);
    }

    private void UnloadFile(ScenarioFileData data) {
        var orchestration = Orchestrations.FirstOrDefault(o => o.Hash == data.FileHash);
        if (orchestration == null)
            return;

        using var lockScope = _scenarioActionLock.EnterScope();
        UnloadOrchestration(orchestration);
        Orchestrations.Remove(orchestration);
        OnOrchestrationsChanged?.Invoke();
    }

    private void LoadFile(ScenarioFileData data) {

        if (!eventService.CurrentLocation.IsInSameLocation(data.MetaData.Location))
            return;

        UnloadFile(data);

        if (fileManager.LoadScenarioFile(data) is ScenarioData scenarioData) {

            if (!scenarioData.Enabled)
                return;

            using var lockScope = _scenarioActionLock.EnterScope();

            var scenarioInstance = ParseScenarioData(scenarioData);
            pluginLog.Info("Created orchestration instance {InstanceName} for scenario {FileName}", [scenarioInstance.ScenarioInstance.AsHexString(), data.FileName]);
            Orchestrations.Add(new Orchestration { Scenario = scenarioInstance, Hash = data.FileHash });
            OnOrchestrationsChanged?.Invoke();
        }
    }

    private Scenario ParseScenarioData(ScenarioData data) {

        var scenario = serviceProvider.GetRequiredService<Scenario>();
        scenario.IsLooping = data.Looping;
        scenario.DelayBetweenRuns = TimeSpan.FromSeconds(data.LoopDelay);

        var scenarioNpcIndex = 0;
        foreach (var scenarioNpc in data.Npcs) {
            if (!npcServices.TrySpawnNpc(out var npc))
                throw new InvalidOperationException($"Could not spawn all npcs.");

            npc.SetPosition(scenarioNpc.Position, isDefault: true);
            npc.SetRotation(scenarioNpc.Rotation, isDefault: true);
            if (scenarioNpc.Appearance != null) {
                npc.SetAppearance(scenarioNpc.Appearance);
            } else {
                npc.SetDefaultAppearance();

            }
            var scenarioNpcObject = serviceProvider.GetRequiredService<ScenarioNpc>();
            scenarioNpcObject.ScenarioInstance = scenario.ScenarioInstance;
            scenarioNpcObject.Actor = npc;
            scenarioNpcObject.Id = scenarioNpcIndex;
            scenarioNpcObject.Name = scenarioNpc.Name;

            if (scenarioNpc.Actions.Count > 0) {
                foreach (var npcAction in scenarioNpc.Actions) {
                    scenarioNpcObject.AddAction(npcAction);
                }

                // attach a sync node at the end to make sure the scenario actually finishes.
                if (scenarioNpc.Actions.LastOrDefault() is not ScenarioNpcSyncAction) {
                    scenarioNpcObject.AddAction(new ScenarioNpcSyncAction());
                }

            }

            npc.Draw();
            scenario.Npcs.Add(scenarioNpcObject);
            scenarioNpcIndex++;
        }
        return scenario;
    }

    private void AdvanceScenarios(TimeSpan time) {
        if (objectTable.LocalPlayer == null)
            return;

        using var lockScope = _scenarioActionLock.EnterScope();

        _lastProximityCheck += (float)time.TotalSeconds;
        if (_lastProximityCheck > ProximityCheckInterval) {
            Orchestrations.ForEach(s => s.Scenario.Proximity(objectTable.LocalPlayer!.Position));
            _lastProximityCheck = 0f;
        }

        var removableList = new List<Orchestration>();
        foreach (var orchestration in Orchestrations) {

            if (!orchestration.Scenario.IsFinished) {
                orchestration.Scenario.Advance(time);
                continue;
            }

            if (!orchestration.Scenario.IsLooping) {
                pluginLog.Debug("Scenario finished and not looping. Unloading orchestration instance {InstanceName}", [orchestration.Scenario.ScenarioInstance.AsHexString()]);
                removableList.Add(orchestration);
                continue;
            }
            orchestration.Scenario.WaitForNextRun(time);
        }
        removableList.ForEach(UnloadOrchestration);
    }

    public void Unload() {
        if (Orchestrations.Count == 0)
            return;

        pluginLog.Info("Unloading current scenarios");

        using var lockScope = _scenarioActionLock.EnterScope();
        Orchestrations.ForEach(UnloadOrchestration);
        Orchestrations.Clear();
        OnOrchestrationsChanged?.Invoke();
    }

    private void UnloadOrchestration(Orchestration orchestration) {
        for (var i = orchestration.Scenario.Npcs.Count - 1; i >= 0; i--) {
            var npc = orchestration.Scenario.Npcs[i];
            orchestration.Scenario.Npcs.Remove(npc);
            npcServices.DespawnNpc(npc.Actor);
        }
    }

    public void Reload() {
        Load(eventService.CurrentLocation);
    }

    public void Initialize() {
        hooks.OnCharacterDestroyed += Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished += EventService_OnTerritoryReady;
        framework.Update += Framework_Update;
        fileManager.OnScenarioFileChanged += ScenarioFileManager_ScenarioFileChanged;
        fileManager.OnScenarioFileRemoved += ScenarioFileManager_ScenarioFileRemoved;
    }

    public void Dispose() {
        hooks.OnCharacterDestroyed -= Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished -= EventService_OnTerritoryReady;
        framework.Update -= Framework_Update;
        fileManager.OnScenarioFileRemoved -= ScenarioFileManager_ScenarioFileRemoved;
        fileManager.OnScenarioFileChanged -= ScenarioFileManager_ScenarioFileChanged;

        GC.SuppressFinalize(this);
        Unload();
    }
}

public class Orchestration {
    public required string Hash { get; set; }
    public required Scenario Scenario { get; set; }
}

public static unsafe class ScenarioManagerExtensions {
    public static ScenarioNpc? GetScenarioNpcByAddress(this ScenarioOrchestrator manager, Character* actor) {
        return manager.Orchestrations.SelectMany(o => o.Scenario.Npcs).FirstOrDefault(n => (BattleChara*)n.Actor.Address == actor);
    }
}
