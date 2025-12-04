using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Infrastructure;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Diagnostics.CodeAnalysis;


namespace ARealmRepopulated.Core.Services.Npcs;

[PluginInterface]
public unsafe class NpcServices(IObjectTable objectTable, ArrpGameHooks hooks) : IDisposable {

    public List<NpcActor> Actors { get; private set; } = [];

    private ulong _internalContentIdIndex = 0;
    public unsafe bool TrySpawnNpc([NotNullWhen(true)] out NpcActor? character) {
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
        battleCharacter->ContentId = 0x80000000000000u + (++_internalContentIdIndex);

        var npcActor = Plugin.Services.GetRequiredService<NpcActor>();
        npcActor.Initialize(battleCharacter);
        Actors.Add(npcActor);

        character = npcActor;
        return true;
    }

    public unsafe void DespawnNpc(NpcActor npcObject) {
        var go = npcObject.Address.AsGameObject();

        var objectManager = ClientObjectManager.Instance();
        var index = objectManager->GetIndexByObject(go);
        objectManager->DeleteObjectByIndex((ushort)index, 0);
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
        hooks.CharacterDestroyed += Hooks_CharacterDestroyed;
    }

    private void Hooks_CharacterDestroyed(Character* chara) {
        var actor = Actors.Where(x => (Character*)x.Address == chara).FirstOrDefault();
        if (actor != null) {
            Actors.Remove(actor);
        }
    }

    public void Dispose() {
        hooks.CharacterDestroyed -= Hooks_CharacterDestroyed;
        ClearNpcs();
    }
}
