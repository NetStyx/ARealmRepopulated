using ARealmRepopulated.Core.Services.Chat;
using ARealmRepopulated.Core.Services.LayoutWorld;
using ARealmRepopulated.Core.Services.LookAt;
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
    LookAtService lookAtService,
    NpcAppearanceService appearanceService,
    ChatBubbleService cbs) {

    public const float RunningSpeed = 6.3f;
    public const float WalkingSpeed = 2.5f;
    public const float TurningSpeed = 6.3f;

    private bool _isReady = false;
    private BattleChara* _actor = null;

    private Vector3 _emoteOffset = Vector3.Zero;

    private bool? _canTrack = null;

    public IntPtr Address { get => new(_actor); }

    public void Initialize(BattleChara* actorPointer) {
        _actor = actorPointer;

        var localPlayer = (BattleChara*)objectTable.LocalPlayer!.Address;
        this.SetRotationFrom(localPlayer);
        this.SetPositionFrom(localPlayer);
    }

    public bool IsReady() {
        if (_actor->Timeline.TimelineSequencer.TimelineIds[0] == 3) {
            _isReady = true;
        }
        return _isReady;
    }

    public void Spawn() {
        _actor->Alpha = 1.0f;
        _actor->EnableDraw();
    }

    public void Despawn() {
        _actor->DisableDraw();
        _isReady = false;
    }

    public void SetName(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            name = $"ARRP {_actor->ObjectIndex}";
        }

        for (var x = 0; x < name.Length; x++) {
            _actor->Name[x] = (byte)name[x];
        }
        _actor->Name[name.Length] = 0;
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

    public bool CanTrack() {
        _canTrack ??= lookAtService.CanLookAtSomething(_actor);
        return _canTrack.GetValueOrDefault(false);
    }

    public void LookAt(BattleChara* target) {
        if (!lookAtService.IsLookingAt(_actor, target)) {
            lookAtService.LookAt(_actor, target);
        }
    }

    public void LookAtNothing() {
        if (lookAtService.IsLookingAtSomething(_actor)) {
            lookAtService.LookAtNothing(_actor);
        }
    }

    public void PlayTimeline(ushort timelineId)
        => appearanceService.PlayTimeline(_actor, timelineId);

    public bool IsPlayingTimeline(ushort timelineId)
        => appearanceService.IsPlayingTimeline(_actor, timelineId);

    public void PlayEmote(ushort emoteid, bool interactWithLayout = false) {
        var emoteEntry = dataCache.GetEmote(emoteid);
        var keepDrawOffset = interactWithLayout || _actor->DrawOffset != Vector3.Zero;

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
                _actor->SetDrawOffset(0, 0, 0);
                _emoteOffset = Vector3.Zero;
            }
            SetMode(CharacterModes.Normal);
        }

        appearanceService.PlayEmote(_actor, emoteEntry);

        // we control the position of the emote execution by hand so we need to reset the draw offset for emotes that change it,
        // but only if we're not trying to interact with a layout or if a previous action hasn't already changed the draw offset        
        if (!keepDrawOffset) {
            _actor->SetDrawOffset(0, 0, 0);
        }
    }

    public bool IsPlayingEmote(ushort emoteid)
        => appearanceService.IsPlayingEmote(_actor, emoteid);

    public bool IsLoopingEmote(ushort emoteid)
        => appearanceService.IsRepeatingEmote(emoteid);

    public void SetMovementAnimation(Animations animation)
        => appearanceService.SetMovementAnimation(_actor, animation);

    public Animations GetAnimation()
        => appearanceService.GetAnimation(_actor);

    public void SetAppearance(NpcAppearanceData appearanceFile) {
        appearanceService.Apply((Character*)_actor, appearanceFile);
    }

    public void SetDefaultAppearance() {
        appearanceService.Apply((Character*)_actor, NpcAppearanceData.FromResource("DefaultHumanFemale.json")!);
    }

    public void Talk(string text, float playTime = 3f)
        => cbs.Talk((Character*)_actor, text, playTime);

    public unsafe void Draw() {
        framework.RunOnTick(() => {
            if (_actor->IsReadyToDraw()) {
                _actor->EnableDraw();
            } else {
                Draw();
            }
        });
    }

}
