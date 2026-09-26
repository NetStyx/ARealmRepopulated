using ARealmRepopulated.Core.Services.Chat;
using ARealmRepopulated.Core.Services.LayoutWorld;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using static ARealmRepopulated.Core.Services.Npcs.NpcAppearanceService;

namespace ARealmRepopulated.Core.Services.Npcs;

public unsafe class NpcActor(
    IFramework framework,
    IObjectTable objectTable,
    ArrpDataCache dataCache,
    LayoutWorldService envService,
    NpcAppearanceService appearanceService,
    ChatBubbleService cbs) {

    public const float RunningSpeed = 6.3f;
    public const float WalkingSpeed = 2.5f;
    public const float TurningSpeed = 6.3f;    
    public const float SprintingSpeed = RunningSpeed * 1.5f;

    private const ulong NoTargetId = 0xE0000000;

    private bool _isReady = false;
    private BattleChara* _actor = null;
    private NpcAppearanceData? _appearance = null;
    private NpcSpawnOptions _spawnOptions = NpcSpawnOptions.Default;

    private Vector3 _emoteOffset = Vector3.Zero;
    private Vector3 _drawOffset = Vector3.Zero;

    public IntPtr Address { get => new(_actor); }

    public void Initialize(BattleChara* actorPointer, NpcSpawnOptions spawnOptions) {
        _actor = actorPointer;
        _spawnOptions = spawnOptions;

        var localPlayer = (BattleChara*)objectTable.LocalPlayer!.Address;
        this.SetRotationFrom(localPlayer);
        this.SetPositionFrom(localPlayer);
    }

    public void Release()
        => _actor = null;

    public bool IsReleased
        => _actor == null;

    public bool IsReady() {
        if (_actor->Timeline.TimelineSequencer.TimelineIds[0] == 3) {
            _isReady = true;
        }
        return _isReady;
    }

    public void Spawn() {
        _actor->Alpha = 1.0f;
        _actor->EnableDraw();
        
        SetExtendedAppearance();        
    }

    public void Despawn() {
        _actor->DisableDraw();
        _isReady = false;
    }

    public void SetName(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            name = $"ARRP {_actor->ObjectIndex}";
        }

        var nameBytes = ActorName.Encode(name);
        for (var x = 0; x < nameBytes.Length; x++) {
            _actor->Name[x] = nameBytes[x];
        }
        _actor->Name[nameBytes.Length] = 0;
    }

    public Vector3 GetPosition()
        => _actor->Position;

    public float GetRotation()
        => _actor->Rotation;

    public void ResetRotation()
        => SetRotation(_actor->DefaultRotation);

    public void ResetPosition()
        => SetPosition(_actor->DefaultPosition);

    public CharacterModes GetMode()
        => _actor->Mode;

    public void SetMode(CharacterModes mode, byte param = 0)
        => _actor->SetMode(mode, param);

    public void ResetMode()
        => SetMode(CharacterModes.Normal);

    public void Fade(float degree)
        => _actor->Alpha = System.Math.Clamp(_actor->Alpha + degree, 0, 1);

    public bool IsFadedOut()
       => _actor->Alpha == 0;

    public void SetPositionFrom(BattleChara* targetCharacter)
        => SetPosition(targetCharacter->Position);

    public void SetPosition(Vector3 position, bool isDefault = false) {
        _actor->SetPosition(position.X, position.Y, position.Z);
        if (isDefault) {
            _actor->DefaultPosition = position;
        }
    }
    
    public void SetDrawOffset(Vector3 drawOffset) {
        _drawOffset = drawOffset;
        ResetDrawOffset();
    }

    public void ResetDrawOffset()
        => _actor->SetDrawOffset(_drawOffset.X, _drawOffset.Y, _drawOffset.Z);

    public void SetRotationFrom(BattleChara* target)
        => SetRotation(target->Rotation);

    public void SetRotation(float rotation, bool isDefault = false) {
        _actor->SetRotation(rotation);
        if (isDefault) {
            _actor->DefaultRotation = rotation;
        }
    }

    public void SetRotationToward(Vector3 target)
        => SetRotation(_actor->Position.DirectionTo(target));

    public void SetRotationToward(GameObject* target)
        => SetRotation(_actor->Position.DirectionTo(target->Position));

    public float GetDistanceTo(GameObject* target)
        => GetDistanceTo(target->Position);

    public float GetDistanceTo(Vector3 target)
        => Vector3.Distance(_actor->Position, target);

    public bool CanTrack()
        => _actor->LookAt.Controller.ParamCount > 0;

    // See the bottom of the file for a bit research into the soft target.
    public void LookAt(BattleChara* target) {
        var targetId = target->GetGameObjectId();
        if (_actor->SoftTargetId != targetId) {
            _actor->SoftTargetId = targetId;
        }
    }

    public void LookAtNothing() {
        if (_actor->SoftTargetId != NoTargetId) {
            _actor->SoftTargetId = NoTargetId;
        }
    }

    public void PlayTimeline(ushort timelineId)
        => appearanceService.PlayTimeline(_actor, timelineId);

    public bool IsPlayingTimeline(ushort timelineId)
        => appearanceService.IsPlayingTimeline(_actor, timelineId);

    public void PlayEmote(ushort emoteid, bool interactWithLayout = false) {
        var emoteEntry = dataCache.GetEmote(emoteid);
        var keepDrawOffset = interactWithLayout || _actor->DrawOffset != _drawOffset;

        if (interactWithLayout && emoteEntry.InteractsWithLayout(out var layoutInteraction)) {
            if (layoutInteraction.LayoutInteractionEmoteId != emoteEntry.RowId)
                emoteEntry = dataCache.GetEmote(layoutInteraction.LayoutInteractionEmoteId);

            if (envService.CheckSnapableLayout((Character*)_actor, 2f, layoutInteraction.LayoutObjectTarget, out var snapResult)) {

                if (layoutInteraction.LayoutObjectTarget == LayoutTarget.Chair) {
                    _emoteOffset = new Vector3(snapResult.SnapPosition.X, _actor->Position.Y, snapResult.SnapPosition.Z);
                }

                _actor->SetPosition(snapResult.SnapPosition.X, snapResult.SnapPosition.Y, snapResult.SnapPosition.Z);
                _actor->SetRotation(snapResult.SnapFacing);
            }
        }

        // the only emote that currently has a cancel emote is the sitting emote. So.. if this returns true, it means we are executing sitting and standing up,
        // which means we should also undo any snap chenanigans we did to the position of the npc.
        if (appearanceService.IsCancelEmote(_actor, emoteEntry)) {
            if (_emoteOffset != Vector3.Zero) {
                _actor->Position = _emoteOffset.Forward(_actor->Rotation, 0.42f);
                ResetDrawOffset();
                _emoteOffset = Vector3.Zero;
            }
            SetMode(CharacterModes.Normal);
        }

        appearanceService.PlayEmote(_actor, emoteEntry);

        // we control the position of the emote execution by hand so we need to put the draw offset back onto the offset of the actor for emotes that change it,
        // but only if we're not trying to interact with a layout or if a previous action hasn't already changed the draw offset        
        if (!keepDrawOffset) {
            ResetDrawOffset();
        }
    }

    public void SetPose(PoseType poseType, byte poseState)
        => appearanceService.SetPose(_actor, poseType, poseState);

    public void HoldEmotePose(ushort emoteid, byte poseState)
        => appearanceService.HoldEmotePose(_actor, dataCache.GetEmote(emoteid), poseState);

    public bool IsPlayingEmote(ushort emoteid)
        => appearanceService.IsPlayingEmote(_actor, emoteid);

    public bool IsPlayingEmote(ushort emoteid, byte poseState) {
        var poseEmote = dataCache.GetPoseStateEmote(emoteid, poseState);
        return IsPlayingEmote(emoteid) || (poseEmote != 0 && IsPlayingEmote(poseEmote));
    }

    public bool IsLoopingEmote(ushort emoteid)
        => dataCache.GetEmote(emoteid).IsLooping();

    public void SetMovementAnimation(Animations animation, float animationSpeed = 1f)
        => appearanceService.SetMovementAnimation(_actor, animation, animationSpeed);
    
    public void SetMovementMotion(float travelSpeed) {
        var motion = MovementMotion.Select(travelSpeed);
        SetMovementAnimation(motion.Animation, motion.AnimationSpeed);
    }

    public Animations GetAnimation()
        => appearanceService.GetAnimation(_actor);            

    public void SetAppearance(NpcAppearanceData appearanceFile) {
        // if the appearance management is set to external, we will always use the default appearance for the npc actor, regardless of what is passed in here.
        if (_spawnOptions.AppearanceManagement == AppearanceManagement.External) {
            _appearance = DefaultAppearance();
            // the voice is kept from the given file though, as external plugins do not set one.
            _appearance.Voice = appearanceFile.Voice;
        } else {
            _appearance = appearanceFile;
        }

        appearanceService.Apply((Character*)_actor, _appearance);
    }

    public void SetExtendedAppearance(){
        // same as for the default appearance, dont apply extended appearance if an external plugin is supposed to manage it.
        if (_appearance != null && _spawnOptions.AppearanceManagement == AppearanceManagement.Internal) {
            appearanceService.ApplyExtendedAppearance((Character*)_actor, _appearance);
        }
    }

    public void SetDefaultAppearance()
        => SetAppearance(DefaultAppearance());

    private static NpcAppearanceData DefaultAppearance()
        => NpcAppearanceData.FromResource("DefaultHumanFemale.json")!;

    public void Talk(string text, float playTime = 3f)
        => cbs.Talk((Character*)_actor, text, playTime);

    public unsafe void Draw() {
        framework.RunOnTick(() => {
            if (_actor == null)
                return;

            if (_actor->IsReadyToDraw()) {
                Spawn();
            } else {
                Draw();
            }
        });
    }

}

// Character->SoftTargetId
//
// After observing some concerning discussions about the soft target and how the game uses it, i did a short investigation of the system:
// The path for actors on the clientobject table (i>200) appears to be safe. As a precausion i am using the field directly which is enough to let the lookupcontainer do its thing. 
// The setter would store the same field, but during duty recorder playback it also syncs the TargetSystem for the replays perspective character (TargetSystem+0xA0; ClientStructs names it IdleCamTarget?), 
// and that sync ends in the TargetSystems change notification, which sends the target to the server. I did not verify whether that packet actually goes out during playback. 
// Anyway: Our actors will never be the local character, but to be safe we avoid calling the setter at all by using the field.
//
// The game reads the field back in a few places. LookAt reads it for other characters too. The one network adjacent reader is the
// TargetSystems duty recorder perspective switch below, and that one needs the player to select the character first.
//
//   FUN_1408985e0(longlong param_1,char param_2,char param_3) = 
//      IsLocalPlayer(GameObject* obj, bool respectReplay, bool includeGPoseClone)
//
//   void Character::SetSoftTargetId(Character *this, GameObjectId id) {
//     if (!<IsLocalPlayer>(this, 1, 0)) {
//       if (this->TargetId == id) id = 0xe0000000;
//       this->SoftTargetId = id;
//       if ((ContentsReplayManager.PlaybackControls & InPlayback) != 0 && *(TargetSystem+0xA0) == this) {
//         if (TargetSystem::GetSoftTargetObjectId(&TargetSystem_Instance) != this->SoftTargetId)        
//           <TargetSystem_SetSoftTargetById>(&TargetSystem_Instance);
//       }
//     }
//   }
// - Character.GetSoftTargetId: only the local player answers from TargetSystem, anyone else returns the field.
//   <IsLocalPlayer> compares against Control.LocalPlayer ... and our actors are never the local player.
//
//   GameObjectId Character::GetSoftTargetId(Character *this) {
//     if (<IsLocalPlayer>(this, 1, 0))
//       return TargetSystem::GetSoftTargetObjectId(&TargetSystem_Instance);
//     return this->SoftTargetId;
//   }
//
// - LookAtContainer.UpdateLookAt, run each frame from GameObjectManager.UpdateLookAt. Its target lookup only asks
//   TargetSystem for the local player; NPCs of type PC (1) and BattleNpc (2) use the characters own ids, and
//   everything else falls through to the behaviour container, which our actors never fill.
//
//   GameObjectId <LookAtContainer_ResolveTarget>(LookAtContainer *this) {
//     if (!<IsLocalPlayer>(this->Owner, 1, 0)) {
//       if (this->Owner->GetObjectKind() == 1) {
//         if (!<lookAtSuppressed>(this->Owner)) {
//           id = Character::GetSoftTargetId(this->Owner);
//           if (id != 0xe0000000) return id;
//           return Character::GetTargetId(this->Owner);
//         }
//       } else if (this->Owner->GetObjectKind() == 2) {
//         /* minion owned by the local player: look at the owner */
//         id = Character::GetSoftTargetId(this->Owner);
//         if (id != 0xe0000000) return id;
//         id = Character::GetTargetId(this->Owner);
//         if (id != 0xe0000000) return id;
//       }
//     } else if (!<lookAtSuppressed>(this->Owner)) {
//       return TargetSystem::GetTargetObjectId2(&TargetSystem_Instance);
//     }
//     return <BehaviourContainer_GetTarget>(this->Owner + 0x1cf0);
//   }
//
//   UpdateLookAt then resolves that id against the local object table and hands it to the character's own
//   CharacterLookAtController as a target param, which then drives the head tracking. Nothing on that path sends or queues a packet.
//
//     target = GameObjectManager::ObjectArrays::GetObjectByGameObjectId(&objects, id);
//     param.vtbl = &CharacterLookAtTargetParam; 
//     param.Type = 1; 
//     param.TargetId = target->GetGameObjectId();
//     <CharacterLookAtController_SetParam>(&this->Controller, &param, slot, 0);
//
// - UI3DModule.UpdateGameObjects: reads only the soft target of the local player, or of the duty recorder perspective character (TargetSystem+0xA0)
//     player = <GetLocalOrReplayPerspectiveCharacter>(1);
//     softTarget = Character::GetSoftTargetId(player);
//
// - TargetSystem: only in duty recorder mode (target mode 5), selecting a character makes it the replay's perspective character, and the game takes over
//   that characters TargetId and SoftTargetId as the TargetSystems hard and soft target. Normal targeting never reads a characters SoftTargetId.
//   Both setters can end in the target change notification that sends the target to the server, so this is the only reader that is somewhat network adjacent.
//
//     if (<CurrentTargetMode>(this) == 5 /* ContentsReplay */) {
//       if (*(this+0xA0) != selected && <CanSelect>(...)) {
//         *(this+0xA0) = selected;
//         chara = GameObject::GetAsCharacter(selected);
//         <TargetSystem_SetHardTargetById>(this, Character::GetTargetId(chara), 1);
//         <TargetSystem_SetSoftTargetById>(this, Character::GetSoftTargetId(chara));
//         ...
//       }
//     }
//   It is the only writer of TargetSystem+0xA0, and it needs the player to be able to select our actor in the first place. Our actors are non-targetable,
//   so this path never runs for them, and the SetSoftTargetId sync above can't either. Making them targetable would open both, which is one more reason never to.
// 
//   Ah, and honorable mention to CharacterSetupContainer.CopyFromCharacter which probably also copies the targets.