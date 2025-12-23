using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Infrastructure;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Npcs;

[PluginInterface]
public unsafe class NpcServices(IObjectTable objectTable, IPluginLog log, ArrpGameHooks hooks) : IDisposable {

    public List<NpcActor> Actors { get; private set; } = [];

    private readonly Lock _npcServicesLock = new();

    public unsafe bool TrySpawnNpc([NotNullWhen(true)] out NpcActor? character) {

        using var _ = _npcServicesLock.EnterScope();

        if (!TryCreateNewCharacter(out var battleCharacter)) {
            character = null;
            return false;
        }

        if (!TryCreateObjectReference(battleCharacter, out var gameObjectInterface)) {
            character = null;
            return false;
        }

        battleCharacter->ObjectKind = ObjectKind.BattleNpc;
        battleCharacter->BattleNpcSubKind = (BattleNpcSubKind)4;
        battleCharacter->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;

        var npcActor = Plugin.Services.GetRequiredService<NpcActor>();
        npcActor.Initialize(battleCharacter);
        Actors.Add(npcActor);

        character = npcActor;
        return true;
    }

    public unsafe void DespawnNpc(NpcActor npcObject) {

        using var _ = _npcServicesLock.EnterScope();

        var go = npcObject.Address.AsGameObject();

        log.Debug($"Despawning NPC '{go->GetName()}' at {npcObject.Address:X}");
        var objectManager = ClientObjectManager.Instance();
        var index = objectManager->GetIndexByObject(go);
        if (index >= 0) {
            log.Debug($"Deleting gameobject at index {index}");
            objectManager->DeleteObjectByIndex((ushort)index, 0);
        } else {
            log.Warning($"Failed to find index for {go->GetName()}");
        }
    }

    private bool TryCreateNewCharacter(out BattleChara* resultCharacter) {
        resultCharacter = null;

        var objectManager = ClientObjectManager.Instance();
        var objectIndex = objectManager->CreateBattleCharacter();
        if (objectIndex == 0xffffffff)
            return false;

        var gameObject = objectManager->GetObjectByIndex((ushort)objectIndex);
        if (gameObject == null)
            return false;

        var battleCharacter = (BattleChara*)gameObject;
        battleCharacter->CharacterSetup.SetupBNpc(0);

        resultCharacter = battleCharacter;
        return true;
    }

    private bool TryCreateObjectReference(BattleChara* character, [NotNullWhen(true)] out IGameObject? gameObject) {
        var newGameObject = objectTable.CreateObjectReference((nint)character);
        if (newGameObject != null) {
            gameObject = newGameObject;
            return true;
        }

        gameObject = null;
        return false;
    }

    public void ClearNpcs() {
        Actors.ToList().ForEach(DespawnNpc);
        Actors.Clear();
    }

    public void Initialize() {
        hooks.OnCharacterDestroyed += Hooks_CharacterDestroyed;
    }

    private void Hooks_CharacterDestroyed(Character* chara) {
        var actor = Actors.Where(x => (Character*)x.Address == chara).FirstOrDefault();
        if (actor != null) {
            Actors.Remove(actor);
        }
    }

    public void Dispose() {
        GC.SuppressFinalize(this);

        hooks.OnCharacterDestroyed -= Hooks_CharacterDestroyed;
        ClearNpcs();
    }
}
