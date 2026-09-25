using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios.Conditions;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Scenarios;

public unsafe class ScenarioOrchestrator(
    IFramework framework,
    IPluginLog pluginLog,
    IObjectTable objectTable,
    IServiceProvider serviceProvider,
    ScenarioFileManager fileManager,
    PluginConfig config,
    NpcServices npcServices,
    ArrpGameHooks hooks,
    ArrpEventService eventService,
    ScenarioConditionService conditionService) : IDisposable {

    private readonly Lock _scenarioActionLock = new();
    private const float ProximityCheckInterval = 0.5f;
    private const float ConditionCheckInterval = 1f;
    private float _lastProximityCheck = 0f;
    private float _lastConditionCheck = 0f;

    public List<Orchestration> Orchestrations { get; private set; } = [];
    public IEnumerable<Orchestration> ActiveOrchestrations => Orchestrations.Where(o => o.IsActive);
    public event Action? OnOrchestrationStateChanged;

    private void Game_CharacterDestroyed(Character* chara) {
        using var lockScope = _scenarioActionLock.EnterScope();
        foreach (var orchestration in Orchestrations) {
            if (orchestration.Scenario is not { } scenario)
                continue;

            var removed = scenario.Npcs.RemoveAll(n => n.Actor.IsReleased || (Character*)n.Actor.Address == chara);
            if (removed > 0) {
                pluginLog.Verbose($"Character finalization in progress. Removing character {(nint)chara:X} from scenario");
            }
        }
    }

    private void EventService_OnTerritoryReady(LocationData territory) {
        if (config.AutoLoadScenarios) {
            Load(eventService.CurrentLocation);
        }
    }

    private void EventService_OnCutsceneStarted() {
        if (Orchestrations.Count == 0)
            return;

        pluginLog.Info("Cutscene started. Unloading scenarios to free resources.");
        Unload();
    }

    private void EventService_OnCutsceneEnded() {
        if (!config.AutoLoadScenarios)
            return;

        if (!eventService.IsTerritoryReady) {
            pluginLog.Info("Cutscene ended but territory is not ready. Deferring reload to territory handler.");
            return;
        }

        pluginLog.Info("Cutscene ended. Reloading scenarios.");
        Load(eventService.CurrentLocation);
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
        Deactivate(orchestration);
        Orchestrations.Remove(orchestration);
        OnOrchestrationStateChanged?.Invoke();
    }

    private void LoadFile(ScenarioFileData data) {

        UnloadFile(data);

        if (!data.MetaData.Enabled) {
            pluginLog.Debug("Skipping load of scenario {FileName}: Scenario file is disabled", [data.FileName]);
            return;
        }

        if (!eventService.CurrentLocation.IsInSameLocation(data.MetaData.Location)) {
            pluginLog.Debug("Skipping load of scenario {FileName}: Location does not match", [data.FileName]);
            return;
        }

        if (eventService.IsInCutscene) {
            pluginLog.Debug("Skipping load of scenario {FileName}: Cutscene is running", [data.FileName]);
            return;
        }

        if (!eventService.IsTerritoryReady || eventService.IsBetweenZones || objectTable.LocalPlayer == null) {
            pluginLog.Debug("Skipping load of scenario {FileName}: Territory is not ready", [data.FileName]);
            return;
        }

        if (fileManager.LoadScenarioFile(data) is ScenarioData scenarioData) {

            if (scenarioData.Npcs.Count == 0) {
                pluginLog.Warning("Cannot load scenario {FileName}: No actors defined", [data.FileName]);
                return;
            }

            using var lockScope = _scenarioActionLock.EnterScope();

            if (scenarioData.Conditions.Count == 0) {
                pluginLog.Info("Registering scenario {FileName} without conditions", [data.FileName]);
            } else {
                pluginLog.Info("Registering scenario {FileName} with conditions [{Conditions}]",
                    [data.FileName, string.Join("; ", scenarioData.Conditions)]);
            }

            Orchestrations.Add(new Orchestration { Hash = data.FileHash, FileName = data.FileName, Data = scenarioData });
            _lastConditionCheck = ConditionCheckInterval + 1f; // force a condition check on next update
            OnOrchestrationStateChanged?.Invoke();
        }
    }

    private Scenario? ParseScenarioData(ScenarioData data) {

        var scenario = serviceProvider.GetRequiredService<Scenario>();
        scenario.IsLooping = data.Looping;
        scenario.DelayBetweenRuns = TimeSpan.FromSeconds(data.LoopDelay);

        var scenarioNpcIndex = 0;
        foreach (var scenarioNpc in data.Npcs) {

            var spawnOptions = new NpcSpawnOptions();
            if (scenarioNpc.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var actorName)) {
                spawnOptions.Kind = ObjectKind.Pc;
                spawnOptions.Name = actorName;
            }

            if (!npcServices.TrySpawnNpc(spawnOptions, out var npc)) {
                // the actors spawned up until now are orphaned
                scenario.Npcs.ForEach(n => npcServices.DespawnNpc(n.Actor));
                return null;
            }

            npc.SetPosition(scenarioNpc.Position, isDefault: true);
            npc.SetRotation(scenarioNpc.Rotation, isDefault: true);
            npc.SetDrawOffset(scenarioNpc.DrawOffset);

            if (scenarioNpc.Appearance != null) {
                npc.SetAppearance(scenarioNpc.Appearance);
            } else {
                npc.SetDefaultAppearance();

            }
            var scenarioNpcObject = serviceProvider.GetRequiredService<ScenarioNpc>();
            scenarioNpcObject.Actor = npc;
            scenarioNpcObject.Id = scenarioNpcIndex;
            scenarioNpcObject.Name = scenarioNpc.Name;
            scenarioNpcObject.Behavior = scenarioNpc.Behavior;
            scenarioNpcObject.ScenarioInstance = scenario.ScenarioInstance;
            scenarioNpcObject.SetActions(scenarioNpc.Actions);

            npc.Draw();
            scenario.Npcs.Add(scenarioNpcObject);
            scenarioNpcIndex++;
        }
        return scenario;
    }

    private bool TryActivate(Orchestration orchestration) {

        var spawnedActorCount = Orchestrations.Sum(o => o.Scenario?.Npcs.Count ?? 0);
        if (spawnedActorCount + orchestration.Data.Npcs.Count > config.ActorSoftLimit) {
            pluginLog.Warning("Cannot spawn scenario {FileName}: would exceed NPC limit ({Current}+{Required}/{Max})", [orchestration.FileName, spawnedActorCount, orchestration.Data.Npcs.Count, config.ActorSoftLimit]);
            return false;
        }

        var objectTableActorCount = objectTable.ClientObjects.Count();
        if (objectTableActorCount + orchestration.Data.Npcs.Count > config.ActorHardLimit) {
            pluginLog.Warning("Cannot spawn scenario {FileName}: would exceed game object limit ({Current}+{Required}/{Max})", [orchestration.FileName, objectTableActorCount, orchestration.Data.Npcs.Count, config.ActorHardLimit]);
            return false;
        }

        if (ParseScenarioData(orchestration.Data) is not Scenario scenarioInstance) {
            pluginLog.Warning("Cannot spawn scenario {FileName}: Could not spawn all actors", [orchestration.FileName]);
            return false;
        }

        orchestration.Scenario = scenarioInstance;
        pluginLog.Info("Created orchestration instance {InstanceName} for scenario {FileName}", [scenarioInstance.ScenarioInstance.AsHexString(), orchestration.FileName]);
        return true;
    }

    private void Deactivate(Orchestration orchestration) {
        if (orchestration.Scenario is not { } scenario)
            return;

        for (var i = scenario.Npcs.Count - 1; i >= 0; i--) {
            var npc = scenario.Npcs[i];
            scenario.Npcs.Remove(npc);
            npcServices.DespawnNpc(npc.Actor);
        }
        orchestration.Scenario = null;
    }

    private void AdvanceScenarios(TimeSpan time) {
        if (objectTable.LocalPlayer == null)
            return;

        using var lockScope = _scenarioActionLock.EnterScope();
        
        IngameConditionSnapshot? ingameSnapshot = null;
        _lastConditionCheck += (float)time.TotalSeconds;
        if (_lastConditionCheck > ConditionCheckInterval) {
            ingameSnapshot = conditionService.TakeIngameSnapshot();
            _lastConditionCheck = 0f;
        }

        _lastProximityCheck += (float)time.TotalSeconds;
        if (_lastProximityCheck > ProximityCheckInterval) {
            ActiveOrchestrations.ToList().ForEach(s => s.Scenario!.Proximity((BattleChara*)objectTable.LocalPlayer!.Address));
            _lastProximityCheck = 0f;
        }

        var removableList = new List<Orchestration>();
        var stateChanged = false;

        foreach (var orchestration in Orchestrations) {

            if (ingameSnapshot is { } snapshot) {
                orchestration.AreConditionsMet = conditionService.AreConditionsMet(orchestration.Data.Conditions, snapshot);
                if (!orchestration.IsActive && orchestration.AreConditionsMet && TryActivate(orchestration))
                    stateChanged = true;
            }

            // the scenario file was loaded, but the conditions are not met yet
            if (orchestration.Scenario is not { } scenario)
                continue;

            // Npc-less scenarios can happen if all NPCs were removed due to character destruction.
            if (scenario.Npcs.Count == 0) {
                removableList.Add(orchestration);
                continue;
            }

            // if the scenario is running, we advance it.
            if (!scenario.IsFinished) {
                scenario.Advance(time);
                continue;
            }

            // the run is finished and the scenario is not looping, so we unload it.
            if (!scenario.IsLooping) {
                pluginLog.Debug("Scenario finished and not looping. Unloading orchestration instance {InstanceName}", [scenario.ScenarioInstance.AsHexString()]);
                removableList.Add(orchestration);
                continue;
            } 
            
            // the scenario is looping, so we check if the conditions are still met before we start a new run.
            if (!orchestration.AreConditionsMet) {
                pluginLog.Debug("Conditions no longer met. Despawning actors of orchestration instance {InstanceName}", [scenario.ScenarioInstance.AsHexString()]);
                Deactivate(orchestration);
                stateChanged = true;
                continue;
            } 
            
            // the scenario is looping and the conditions are still met, so we wait for the next run.
            scenario.WaitForNextRun(time);            
        }

        if (removableList.Count > 0) {
            removableList.ForEach(r => {
                Deactivate(r);
                Orchestrations.Remove(r);
            });
            stateChanged = true;
        }

        if (stateChanged) {
            OnOrchestrationStateChanged?.Invoke();
        }
    }

    public void Unload() {
        if (Orchestrations.Count == 0)
            return;

        pluginLog.Info("Unloading current scenarios");

        using var lockScope = _scenarioActionLock.EnterScope();
        Orchestrations.ForEach(Deactivate);
        Orchestrations.Clear();
        OnOrchestrationStateChanged?.Invoke();
    }

    public void Reload() {
        Load(eventService.CurrentLocation);
    }

    public void Initialize() {
        hooks.OnCharacterDestroyed += Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished += EventService_OnTerritoryReady;
        eventService.OnCutsceneStarted += EventService_OnCutsceneStarted;
        eventService.OnCutsceneEnded += EventService_OnCutsceneEnded;
        framework.Update += Framework_Update;
        fileManager.OnScenarioFileChanged += ScenarioFileManager_ScenarioFileChanged;
        fileManager.OnScenarioFileRemoved += ScenarioFileManager_ScenarioFileRemoved;
    }

    public void Dispose() {
        hooks.OnCharacterDestroyed -= Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished -= EventService_OnTerritoryReady;
        eventService.OnCutsceneStarted -= EventService_OnCutsceneStarted;
        eventService.OnCutsceneEnded -= EventService_OnCutsceneEnded;
        framework.Update -= Framework_Update;
        fileManager.OnScenarioFileRemoved -= ScenarioFileManager_ScenarioFileRemoved;
        fileManager.OnScenarioFileChanged -= ScenarioFileManager_ScenarioFileChanged;

        GC.SuppressFinalize(this);
        Unload();
    }
}

public class Orchestration {
    public required string Hash { get; set; }
    public required string FileName { get; set; }
    public required ScenarioData Data { get; set; }    
    public Scenario? Scenario { get; set; }

    public bool IsActive => Scenario != null;
    
    public bool AreConditionsMet { get; set; }
}

public static unsafe class ScenarioManagerExtensions {
    public static ScenarioNpc? GetScenarioNpcByAddress(this ScenarioOrchestrator manager, Character* actor) {
        return manager.ActiveOrchestrations.SelectMany(o => o.Scenario!.Npcs).FirstOrDefault(n => (BattleChara*)n.Actor.Address == actor);
    }
}
