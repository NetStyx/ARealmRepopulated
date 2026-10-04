using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ARealmRepopulated.Core.Services.Npcs;

/// <summary>
/// Owns the lifetime of our actors: creation and destruction either by us or by the game.
/// </summary>
/// <remarks>
/// As the repsonisbilities on who should do what got clouded more and more, the following rule is now in place:
/// This service manages only the livetime of the actor, his properties are managed by the actor itself.
/// <code>
///   NpcSpawnOptions                   fixed values for one actor across its entire lifetime.
///     NpcServices.TrySpawnNpc         creates the object via the games apis, calls NpcActor.Initialize, registers it within the service, raises OnActorCreated.
///       NpcActor.Initialize           applies invariants, then the options. 
///       NpcActor.Reset                restores the actor to the state it had after initialize.
///       NpcActor.Set*()               runtime changes only: movement, rotation, emote handling, appearance changes, etc.
///     NpcServices.DespawnNpc          removes the actor from the service, deletes the game object, raises OnActorDestroyed.
/// </code>
/// </remarks>
public unsafe class NpcServices(IServiceProvider serviceProvider, IObjectTable objectTable, IPluginLog log, IGameInteropProvider interopProvider, ArrpDataCache dataCache) : IDisposable {

    public List<NpcActor> Actors { get; private set; } = [];

    public delegate void ActorEventDelegate(Character* chara);

    /// <summary>
    /// Raised when one of our actors has been created and set up, before it is drawn.
    /// </summary>
    public event ActorEventDelegate? OnActorCreated;

    /// <summary>
    /// Raised when one of our actors is destroyed, whether we despawn it or the game removes it by itself, e.g. on a zone change.
    /// </summary>
    public event ActorEventDelegate? OnActorDestroyed;

    private readonly Lock _npcServicesLock = new();

    /// <summary>
    /// Client::Game::Character::BattleChara.Dtor() taken from the virtual table instead of hooking the general finalizer. 
    /// We do not need to see all characters, and because every actor we spawn is a BattleChara, hooking this should be enough.
    /// </summary>
    private Hook<BattleCharaDtorDelegate>? _battleCharaDtorHook;
    private delegate GameObject* BattleCharaDtorDelegate(BattleChara* character, byte freeFlags);

    /// <summary>
    /// Client::Game::Character::VfxContainer.LoadCharacterSound()
    /// </summary>
    private Hook<LoadCharacterSoundDelegate>? _loadCharacterSoundHook;
    private delegate nint LoadCharacterSoundDelegate(VfxContainer* container, int soundNumber, int unk2, nint unkGameObject, ulong autoRelease, int weaponDataIndex, int unk6, ulong unk7);

    public void Initialize() {
        _battleCharaDtorHook = interopProvider.HookFromAddress<BattleCharaDtorDelegate>((nint)BattleChara.StaticVirtualTablePointer->Dtor, BattleCharaDtorDetour);
        _battleCharaDtorHook.Enable();

        _loadCharacterSoundHook = interopProvider.HookFromAddress<LoadCharacterSoundDelegate>((nint)VfxContainer.Addresses.LoadCharacterSound.Value, LoadCharacterSoundDetour);
        _loadCharacterSoundHook.Enable();

        if (!MoveControllerPlacementFlags.FlagsOffset.IsAvailable) {
            log.Warning("MoveController.Flags438 not found in ClientStructs, the snap to surface setting has no effect");
        }
    }

    public unsafe bool TrySpawnNpc(NpcSpawnOptions options, [NotNullWhen(true)] out NpcActor? character) {

        using var _ = _npcServicesLock.EnterScope();
        
        if (objectTable.LocalPlayer == null) {
            character = null;
            return false;
        }

        if (!TryCreateNewCharacter(out var battleCharacter)) {
            character = null;
            return false;
        }

        if (!TryCreateObjectReference(battleCharacter, out var gameObjectInterface)) {
            DeleteGameObject((GameObject*)battleCharacter);
            character = null;
            return false;
        }

        var npcActor = serviceProvider.GetRequiredService<NpcActor>();
        npcActor.Initialize(battleCharacter, options);

        Actors.Add(npcActor);
        OnActorCreated?.Invoke((Character*)battleCharacter);

        character = npcActor;
        return true;
    }

    public unsafe void DespawnNpc(NpcActor npcObject) {

        using var _ = _npcServicesLock.EnterScope();

        if (npcObject.Address == IntPtr.Zero)
            return;

        log.Debug($"Despawning NPC at {npcObject.Address:X}");
        // deleting runs the dtor right away, and the dtor hook raises OnActorDestroyed only while the actor is still listed
        DeleteGameObject(npcObject.Address.AsGameObject());

        Actors.Remove(npcObject);
        npcObject.Release();
    }

    public void ClearNpcs() {
        Actors.ToList().ForEach(DespawnNpc);
        Actors.Clear();
    }    

    private void DeleteGameObject(GameObject* go) {
        var objectManager = ClientObjectManager.Instance();
        var index = objectManager->GetIndexByObject(go);
        if (index >= 0) {
            log.Debug($"Deleting gameobject at index {index}");
            objectManager->DeleteObjectByIndex((ushort)index, 0);
        } else {
            log.Warning($"Failed to find index for gameobject at {(nint)go:X}");
        }
    }

    /// <summary>
    /// Tries to create a new object on the client object table.
    /// </summary>
    /// <remarks>
    /// Initially, this method has called `CharacterSetup.SetupBNpc(0)` for all created characters. 
    /// This however caused the game client to crash whenever the character was spawned on a watery surface (like the beach in kugana residental area).
    /// </remarks>        
    private bool TryCreateNewCharacter(out BattleChara* resultCharacter) {
        resultCharacter = null;

        var objectManager = ClientObjectManager.Instance();
        var objectIndex = objectManager->CreateBattleCharacter();
        if (objectIndex == 0xffffffff)
            return false;

        var gameObject = objectManager->GetObjectByIndex((ushort)objectIndex);
        if (gameObject == null) {
            objectManager->DeleteObjectByIndex((ushort)objectIndex, 0);
            return false;
        }

        resultCharacter = (BattleChara*)gameObject;
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

    private NpcActor? FindActor(Character* chara) {
        using var _ = _npcServicesLock.EnterScope();
        return Actors.FirstOrDefault(x => (Character*)x.Address == chara);
    }

    private GameObject* BattleCharaDtorDetour(BattleChara* character, byte freeFlags) {
        if (character != null && FindActor((Character*)character) is { } actor) {
            OnActorDestroyed?.Invoke((Character*)character);

            using (_npcServicesLock.EnterScope()) {
                Actors.Remove(actor);
                actor.Release();
            }
        }

        return _battleCharaDtorHook!.Original(character, freeFlags);
    }

    /// <summary>
    /// The game reads the owners object kind right at the start of this call and drops emote voice lines for npc kinds.
    /// For our actors the kind reads as Pc for the length of this one call, so the game plays the line by itself.
    /// </summary>
    /// <remarks>
    /// The gate inside VfxContainer.LoadCharacterSound; GetObjectKind is a plain read of GameObject.ObjectKind:
    /// <code>
    ///   // emote voice lines
    ///   if (soundNumber - 0x27U &lt; 0xe) {                          
    ///     if (OwnerObject->GetObjectKind() == EventNpc) return 0;
    ///     if (OwnerObject->GetObjectKind() == BattleNpc) return 0;
    ///     if (this->field_0xe0 != 0) return 0;
    ///   }
    ///   ...
    ///   // the voice, or the model's sound pack
    ///   soundId = Vfx.VoiceId;                                      
    /// </code>
    /// </remarks>            
    private nint LoadCharacterSoundDetour(VfxContainer* container, int soundNumber, int unk2, nint unkGameObject, ulong autoRelease, int weaponDataIndex, int unk6, ulong unk7) {
        var owner = container->OwnerObject;

        // i loath inlining stuff like this, but it keeps detour code so nicely together ...
        nint _original() 
            => _loadCharacterSoundHook!.Original(container, soundNumber, unk2, unkGameObject, autoRelease, weaponDataIndex, unk6, unk7);

        bool _soundCheckPredicate(Character* owner, int soundNumber) {

            // the sound is outside the suppressed range, so the game plays it already; taken directly from the decompiled function
            if (soundNumber is < 0x27 or > 0x34)
                return false;            

            // the game only drops the line for npcs, everything else plays it already
            if (owner->ObjectKind is not (ObjectKind.BattleNpc or ObjectKind.EventNpc))
                return false;

            // without a voice there is no line to play
            if (owner->Vfx.VoiceId == 0)
                return false;

            // non-human models hold their sound pack in VoiceId, and the game plays no emote voice for those either
            if (!dataCache.IsHumanModel(owner->ModelContainer.ModelCharaId))
                return false;
            
            // finally, we should only care for our actors. Remember: this locks.
            return FindActor(owner) != null;
        }
        
        // no owner, nothing for us to do
        if (owner == null) {
            return _original();
        }

        // outside our scope, let the game handle it
        if (!_soundCheckPredicate(owner, soundNumber)) {
            return _original();
        }

        // swap the object kind to enable sounds on our spawned actors.
        var objectKind = owner->ObjectKind;
        owner->ObjectKind = ObjectKind.Pc;
        try {
            return _original();
        } finally {
            owner->ObjectKind = objectKind;
        }
    }

    public void Dispose() {
        GC.SuppressFinalize(this);

        // actors go first, the finalizer hook still has to see them leave
        ClearNpcs();
        
        _battleCharaDtorHook?.Dispose();
        _loadCharacterSoundHook?.Dispose();
    }
}

/// <summary>
/// Everything that is fixed for an actor over its lifetime. It is applied once on spawn, and the reset between loops returns to it.
/// </summary>
public class NpcSpawnOptions {
    public static NpcSpawnOptions Default => new();

    /// <summary>
    /// Default value for npcs
    /// </summary>
    public const ushort NoWorld = 0xFFFF;

    public ObjectKind Kind { get; set; } = ObjectKind.BattleNpc;
    public string Name { get; set; } = "";
    public ushort HomeWorld { get; set; } = NoWorld;
    public ushort CurrentWorld { get; set; } = NoWorld;

    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public Vector3 DrawOffset { get; set; }
    public bool SnapToSurface { get; set; } = true;

    public bool IsPublic { get; set; } = false;
    public AppearanceManagement AppearanceManagement { get; set; } = AppearanceManagement.Internal;
    public NpcAppearanceData? Appearance { get; set; }
}

public enum AppearanceManagement {
    /// <summary>
    /// The actors appearance is managed by this plugin.
    /// </summary>
    Internal,

    /// <summary>
    /// Another plugin manages the actors look. The actor then always spawns with the default appearance,
    /// to avoid introducing desyncs with other plugins.
    /// </summary>
    External
}
