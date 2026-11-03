using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Numerics;

namespace ARealmRepopulated.Core.Services.Npcs;

/// <summary>
/// Shows an appearance on a throwaway actor.
/// </summary>
public sealed unsafe class NpcActorPreview : IDisposable {

    private readonly NpcServices _npcServices;
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly ArrpEventService _eventService;
    private readonly IntervalTimer _checkTimer = new(TimeSpan.FromSeconds(0.25));

    private NpcActor? _actor;
    private Vector3? _position;
    private float _rotation;
    private NpcAppearanceData? _shownAppearance;
    private NpcAppearanceData? _settlingAppearance;

    public NpcActorPreview(NpcServices npcServices, IObjectTable objectTable, IFramework framework, ArrpEventService eventService) {
        _npcServices = npcServices;
        _objectTable = objectTable;
        _framework = framework;
        _eventService = eventService;

        _npcServices.OnActorDestroyed += NpcServices_ActorDestroyed;
        _eventService.OnCutsceneStarted += Hide;
        _checkTimer.Trigger();
    }

    public void Show(NpcAppearanceData appearance) {
        if (!_checkTimer.Tick(_framework.UpdateDelta))
            return;

        if (appearance == _shownAppearance)
            return;

        var hasSettled = appearance == _settlingAppearance || _actor == null;
        var snapshot = appearance.Copy();
        _settlingAppearance = snapshot;

        if (hasSettled) {
            _shownAppearance = snapshot;
            _framework.RunOnTick(() => Replace(snapshot));
        }
    }

    public void Hide() {
        if (_shownAppearance == null)
            return;

        _shownAppearance = null;
        _settlingAppearance = null;
        _position = null;
        _checkTimer.Trigger();
        _framework.RunOnTick(DespawnActor);
    }

    private void Replace(NpcAppearanceData appearance) {
        if (_shownAppearance != appearance)
            return;

        DespawnActor();

        if (!_eventService.CanSpawnActors || _objectTable.LocalPlayer is not { } player) {
            _shownAppearance = null;
            return;
        }

        if (_position == null) {
            _position = player.Position.Forward(player.Rotation, 2f);
            _rotation = player.Rotation + MathF.PI;
        }

        var options = new NpcSpawnOptions {
            Position = _position.Value,
            Rotation = _rotation,
            Appearance = appearance
        };

        if (_npcServices.TrySpawnNpc(options, out var actor)) {
            _actor = actor;
            actor.Draw();
        } else {
            _shownAppearance = null;
        }
    }

    private void DespawnActor() {
        if (_actor == null)
            return;

        var actor = _actor;
        _actor = null;
        _npcServices.DespawnNpc(actor);
    }

    private void NpcServices_ActorDestroyed(Character* character) {
        if (_actor == null || _actor.Address != (nint)character)
            return;

        _actor = null;
        _position = null;
        _shownAppearance = null;
    }

    public void Dispose() {
        _npcServices.OnActorDestroyed -= NpcServices_ActorDestroyed;
        _eventService.OnCutsceneStarted -= Hide;
        _shownAppearance = null;
        DespawnActor();
    }
}
