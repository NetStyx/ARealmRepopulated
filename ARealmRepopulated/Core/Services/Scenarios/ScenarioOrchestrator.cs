using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Scenarios;

public unsafe class ScenarioOrchestrator(IFramework framework, IPluginLog pluginLog, IClientState clientState, ScenarioFileManager fileManager, PluginConfig config, NpcServices npcServices, ArrpGameHooks hooks, ArrpEventService eventService) : IDisposable
{

    private readonly Lock _scenarioActionLock = new();
    private const float ProximityCheckInterval = 0.5f;
    private float _lastProximityCheck = 0f;
    private ushort _currentTerritory = 0;

    public List<Orchestration> Orchestrations { get; set; } = [];
    public event Action? OrchestrationsChanged;

    private void Game_CharacterDestroyed(Character* chara)
    {
        using var lockScope = _scenarioActionLock.EnterScope();
        foreach (var orchestration in Orchestrations)
        {
            var scenarioNpc = orchestration.Scenario.Npcs.FirstOrDefault(n => (Character*)n.Actor.Address == chara);
            if (scenarioNpc != null)
            {
                pluginLog.Debug($"Character finalization in progress. Removing character {scenarioNpc.Actor.Address:X} from scenario");
                orchestration.Scenario.Npcs.Remove(scenarioNpc);
            }
        }
    }

    private void EventService_OnTerritoryReady(ushort territoryId)
    {
        pluginLog.Info($"Territory {territoryId} ready");
        if (_currentTerritory != territoryId)
        {
            _currentTerritory = territoryId;
            if (config.AutoLoadScenarios)
            {
                Load(territoryId);
            }
        }
    }

    private void ScenarioFileManager_ScenarioFileChanged(ScenarioFileData metaData)
        => LoadFile(metaData);

    private void ScenarioFileManager_ScenarioFileRemoved(ScenarioFileData metaData)
        => UnloadFile(metaData);

    private void Framework_Update(IFramework framework)
        => AdvanceScenarios(framework.UpdateDelta);

    private void Load(ushort territoryId)
    {
        Unload();

        pluginLog.Info($"Loading scenarios for territory {territoryId}");
        fileManager.GetScenarioFilesByTerritory(territoryId).ForEach(LoadFile);
    }

    private void UnloadFile(ScenarioFileData data)
    {
        var orchestration = Orchestrations.FirstOrDefault(o => o.Hash == data.FileHash);
        if (orchestration == null)
            return;

        using var lockScope = _scenarioActionLock.EnterScope();
        UnloadOrchestration(orchestration);
        Orchestrations.Remove(orchestration);
        OrchestrationsChanged?.Invoke();
    }

    private void LoadFile(ScenarioFileData data)
    {
        if (data.MetaData.TerritoryId != _currentTerritory)
            return;

        UnloadFile(data);

        if (fileManager.LoadScenarioFile(data) is ScenarioData scenarioData)
        {

            if (!scenarioData.Enabled)
                return;

            using var lockScope = _scenarioActionLock.EnterScope();
            Orchestrations.Add(new Orchestration { Scenario = ParseScenarioData(scenarioData), Hash = data.FileHash });
            OrchestrationsChanged?.Invoke();
        }
    }

    private Scenario ParseScenarioData(ScenarioData data)
    {
        var scenario = new Scenario
        {
            IsLooping = data.Looping,
            DelayBetweenRuns = TimeSpan.FromSeconds(data.LoopDelay)
        };
        foreach (var scenarioNpc in data.Npcs)
        {
            if (!npcServices.TrySpawnNpc(out var npc))
                throw new InvalidOperationException($"Could not spawn all npcs.");

            npc.SetPosition(scenarioNpc.Position, isDefault: true);
            npc.SetRotation(scenarioNpc.Rotation, isDefault: true);
            if (!string.IsNullOrEmpty(scenarioNpc.Appearance))
            {
                npc.SetAppearance(scenarioNpc.Appearance);
            }
            else
            {
                npc.SetDefaultAppearance();

            }
            var scenarioNpcObject = new ScenarioNpc { Actor = npc };
            if (scenarioNpc.Actions.Count > 0) {
                foreach (var npcAction in scenarioNpc.Actions)
                {
                    scenarioNpcObject.AddAction(npcAction);
                }
                
                // attach a sync node at the end to make sure the scenario actually finishes.
                if (scenarioNpc.Actions.LastOrDefault() is not ScenarioNpcSyncAction)
                {
                    scenarioNpcObject.AddAction(new ScenarioNpcSyncAction());
                }

            }

            npc.Draw();
            scenario.Npcs.Add(scenarioNpcObject);
        }
        return scenario;
    }

    private void AdvanceScenarios(TimeSpan time)
    {
        if (clientState.LocalPlayer == null)
            return;

        using var lockScope = _scenarioActionLock.EnterScope();

        _lastProximityCheck += (float)time.TotalSeconds;
        if (_lastProximityCheck > ProximityCheckInterval)
        {
            Orchestrations.ForEach(s => s.Scenario.Proximity(clientState.LocalPlayer!.Position));
            _lastProximityCheck = 0f;
        }

        var removableList = new List<Orchestration>();
        foreach (var orchestration in Orchestrations)
        {
            if (!orchestration.Scenario.IsFinished)
            {
                orchestration.Scenario.Advance(time);
                continue;
            }

            if (!orchestration.Scenario.IsLooping)
            {
                removableList.Add(orchestration);
                continue;
            }

            orchestration.Scenario.WaitForNextRun(time);

        }

        removableList.ForEach(UnloadOrchestration);
    }

    public void Unload()
    {
        if (Orchestrations.Count == 0)
            return;

        pluginLog.Info("Unloading scenarios");

        using var lockScope = _scenarioActionLock.EnterScope();
        Orchestrations.ForEach(UnloadOrchestration);
        Orchestrations.Clear();
        OrchestrationsChanged?.Invoke();
    }

    private void UnloadOrchestration(Orchestration orchestration)
    {
        for (var i = orchestration.Scenario.Npcs.Count - 1; i >= 0; i--)
        {
            var npc = orchestration.Scenario.Npcs[i];
            orchestration.Scenario.Npcs.Remove(npc);
            npcServices.DespawnNpc(npc.Actor);
        }
    }

    public void Reload()
    {
        Load(_currentTerritory);
    }

    public void Initialize()
    {
        hooks.CharacterDestroyed += Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished += EventService_OnTerritoryReady;
        framework.Update += Framework_Update;
        fileManager.OnScenarioFileChanged += ScenarioFileManager_ScenarioFileChanged;
        fileManager.OnScenarioFileRemoved += ScenarioFileManager_ScenarioFileRemoved;
    }


    public void Dispose()
    {
        hooks.CharacterDestroyed -= Game_CharacterDestroyed;
        eventService.OnTerritoryLoadFinished -= EventService_OnTerritoryReady;
        framework.Update -= Framework_Update;
        fileManager.OnScenarioFileRemoved -= ScenarioFileManager_ScenarioFileRemoved;
        fileManager.OnScenarioFileChanged -= ScenarioFileManager_ScenarioFileChanged;

        GC.SuppressFinalize(this);
        Unload();
    }
}

public class Orchestration
{
    public required string Hash { get; set; }
    public required Scenario Scenario { get; set; }
}


public static unsafe class ScenarioManagerExtensions
{
    public static ScenarioNpc? GetScenarioNpcByAddress(this ScenarioOrchestrator manager, Character* actor)
    {
        return manager.Orchestrations.SelectMany(o => o.Scenario.Npcs).FirstOrDefault(n => (BattleChara*)n.Actor.Address == actor);
    }
}
