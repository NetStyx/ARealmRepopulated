using ARealmRepopulated.Core.Math;
using ARealmRepopulated.Core.Services.Chat;
using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using static ARealmRepopulated.Core.Services.Npcs.NpcAppearanceService;

namespace ARealmRepopulated.Core.Services.Npcs;

public unsafe class NpcActor(IFramework framework, IObjectTable objectTable, NpcAppearanceService appearanceService, ChatBubbleService cbs) {

    public const float RunningSpeed = 6.3f;
    public const float WalkingSpeed = 2.5f;
    public const float TurningSpeed = 6.3f;

    private BattleChara* _actor = null;
    public IntPtr Address { get => new(_actor); }

    public void Initialize(BattleChara* actorPointer) {
        _actor = actorPointer;

        var localPlayer = (BattleChara*)objectTable.LocalPlayer!.Address;
        this.SetRotationFrom(localPlayer);
        this.SetPositionFrom(localPlayer);
        appearanceService.SetName((Character*)_actor);
    }

    public void Spawn() {
        _actor->Alpha = 1.0f;
        _actor->EnableDraw();
    }

    public void Despawn()
        => _actor->DisableDraw();

    public Vector3 GetPosition()
        => _actor->Position;

    public float GetRotation()
        => _actor->Rotation;

    public void ResetRotation()
        => SetRotation(_actor->DefaultRotation);

    public void ResetPosition()
        => SetPosition(_actor->DefaultPosition);

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

    public void PlayTimeline(ushort timelineId)
     => appearanceService.PlayTimeline(_actor, timelineId);

    public void PlayEmote(ushort emoteid, bool loop)
     => appearanceService.PlayEmote(_actor, emoteid);

    public bool IsPlayingEmote(ushort emoteid)
        => appearanceService.IsPlayingEmote(_actor, emoteid);

    public bool IsLoopingEmote(ushort emoteid)
        => appearanceService.IsRepeatingEmote(emoteid);

    public void SetAnimation(Animations animation) {
        appearanceService.SetAnimation(_actor, animation);
    }


    public void SetAppearance(string base64JsonData)
        => SetAppearance(NpcAppearanceFile.FromBase64(base64JsonData));


    public void SetAppearance(NpcAppearanceFile appearanceFile) {
        appearanceService.Apply((Character*)_actor, appearanceFile);
    }

    public void SetDefaultAppearance() {
        appearanceService.Apply((Character*)_actor, NpcAppearanceFile.FromResource("DefaultHumanFemale.json")!);
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
