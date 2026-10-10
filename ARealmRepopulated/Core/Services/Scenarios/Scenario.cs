using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Core.Services.Scenarios;

public unsafe class Scenario(IPluginLog log) {

    public Guid ScenarioInstance { get; } = Guid.NewGuid();
    public List<ScenarioNpc> Npcs { get; set; } = [];
    public bool IsLooping { get; set; } = false;
    public TimeSpan DelayBetweenRuns { get; set; } = TimeSpan.Zero;

    private readonly ScenarioState _state = new();
    private double _currentDelay = 0;
    private bool _isWaitingForNextRun = false;
    
    public bool HasStarted
        => _state.CurrentScenarioSegment != 0;

    public bool IsFinished
        => HasStarted && Npcs.All(n => n.CurrentAction.IsEmpty);

    public bool IsSyncing
        => Npcs.All(n => n.CurrentAction.IsSync || n.CurrentAction.IsEmpty);
    
    public bool IsWaitingEndlessly
        => Npcs.Any(n => n.IsWaitingEndlessly);

    public bool IsEnding { get; set; }
    
    public bool FadeOut() {
        Npcs.ForEach(n => n.Actor.Fade(-0.10f));
        return Npcs.All(n => n.Actor.IsFadedOut());
    }

    public void WaitForNextRun(TimeSpan time) {

        if (!_isWaitingForNextRun) {
            _isWaitingForNextRun = true;
            log.Info($"[{ScenarioInstance.AsHexString()}] Scenario loop finished.");
            if (DelayBetweenRuns.TotalMilliseconds > 0) {
                log.Debug($"[{ScenarioInstance.AsHexString()}] Waiting {DelayBetweenRuns.TotalSeconds} seconds before next run.");
            }
        }

        _currentDelay += time.TotalMilliseconds;
        if (_currentDelay < DelayBetweenRuns.TotalMilliseconds) {
            return;
        }

        log.Info($"[{ScenarioInstance.AsHexString()}] Starting next scenario loop");
        _isWaitingForNextRun = false;
        _currentDelay = 0;
        _state.CurrentScenarioSegment = 0;
        Npcs.ForEach(n => n.Actor.Reset());
    }

    public void Advance(TimeSpan time) {
        if (IsSyncing) {
            log.Debug($"[{ScenarioInstance.AsHexString()}] [{_state.CurrentScenarioSegment}] Advancing to segment {_state.CurrentScenarioSegment + 1}");
            _state.CurrentScenarioSegment++;
        }

        Npcs.ForEach(n => n.Advance(_state, time));
    }

    public void Proximity(BattleChara* character) {
        Npcs.ForEach(n => n.Proximity(character));
    }
}

public class ScenarioState {
    public int CurrentScenarioSegment { get; set; } = 0;
}

public unsafe class ScenarioNpc(IPluginLog log) {

    public Guid ScenarioInstance { get; set; } = Guid.Empty;
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "";
    public int CurrentScenarioSegment { get; set; } = 0;
    public ScenarioNpcBehaviorData Behavior { get; set; } = new();
    public NpcActor Actor { get; set; } = null!;

    private readonly List<ScenarioNpcAction> _actions = [];

    private readonly Queue<ScenarioNpcAction> _scenarioActions = new();

    public ScenarioNpcActionExecution CurrentAction { get; private set; } = ScenarioNpcActionExecution.Default;
    
    public bool IsWaitingEndlessly => CurrentAction.Action switch {
        ScenarioNpcWaitingAction => CurrentAction.IsEndless,
        ScenarioNpcEmoteAction emote => CurrentAction.IsEndless && (emote.Loop || Actor.IsLoopingEmote(emote.Emote)),
        ScenarioNpcIdleAction idle => CurrentAction.IsEndless && idle.PoseState > 0,
        _ => false,
    };

    private readonly TimeSpan _proximityTimeout = TimeSpan.FromSeconds(15);
    private readonly float _proximityChatDistance = 10f;
    private readonly float _proximityLookDistance = 4f;

    public void SetActions(List<ScenarioNpcAction> actions) {
        var npcActions = actions.Where(a => a.Enabled).ToList();
        if (npcActions.Count == 0) {
            // an actor without actions waits endlessly, which also holds the other actors at their first sync.
            npcActions.Add(new ScenarioNpcWaitingAction());
        }

        // attach a sync node at the end to make sure the scenario actually finishes.
        if (npcActions.LastOrDefault() is not ScenarioNpcSyncAction) {
            npcActions.Add(new ScenarioNpcSyncAction());
        }

        _actions.Clear();
        _actions.AddRange(npcActions);
    }

    public void Advance(ScenarioState state, TimeSpan time) {
        if (CurrentAction.IsFinished || CurrentScenarioSegment != state.CurrentScenarioSegment) {
            CurrentScenarioSegment = state.CurrentScenarioSegment;
            CurrentAction = SetupNextAction();
            log.Debug($"[{ScenarioInstance.AsHexString()}] [{CurrentScenarioSegment}] [{Id}:{Name}] Starting action '{CurrentAction.Action}'");
        }
        
        if (CurrentAction.IsSync || CurrentAction.IsEmpty)
            return;

        // after spawning it takes a few frames until the actor settled into a stable animation state
        if (CurrentAction.Action.RequiresReadyActor && !Actor.IsReady())
            return;

        switch (CurrentAction.Action) {
            case ScenarioNpcMovementAction movement:
                AdvanceSimpleMovement(movement, time);
                break;

            case ScenarioNpcPathAction pathMovement:
                AdvancePathMovement(pathMovement, time);
                break;

            case ScenarioNpcRotationAction rotation:
                AdvanceRotation(rotation, time);
                break;

            case ScenarioNpcEmoteAction emote:
                AdvanceEmote(emote, time);
                break;

            case ScenarioNpcIdleAction idle:
                AdvanceIdle(idle, time);
                break;

            case ScenarioNpcTimelineAction timeline:
                AdvanceTimeline(timeline, time);
                break;

            case ScenarioNpcSpawnAction spawn:
                AdvanceSpawn(spawn);
                break;

            case ScenarioNpcDespawnAction despawn:
                AdvanceDespawn(despawn);
                break;

            default:
                AdvanceTime(time);
                break;
        }

        CurrentAction.IsStarted = true;
    }

    public void Proximity(BattleChara* player) {

        var distance = Actor.GetDistanceTo(player->Position);

        // Checked before the chat distance, otherwise a player who leaves quickly is never released.
        if (Actor.CanTrack()) {
            if (Behavior.TrackPlayer && distance <= _proximityLookDistance) {
                Actor.LookAt(player);
            } else {
                Actor.LookAtNothing();
            }
        }

        if (distance > _proximityChatDistance) {
            CurrentAction.IsInProximity = false;
            return;
        }

        CurrentAction.IsInProximity = true;

        var proximityText = CurrentAction.Action.NpcTalk;
        if (string.IsNullOrWhiteSpace(proximityText)) {
            return;
        }

        if (CurrentAction.IsEndless) {
            if (DateTime.Now - CurrentAction.LastProximityAction < _proximityTimeout) {
                return;
            }
            CurrentAction.ProximityExecuted = false;
        } else if (CurrentAction.ProximityExecuted) {
            return;
        }

        CurrentAction.ProximityExecuted = true;

        float wordsPerMinute = 150;
        float wordsInText = proximityText.Split(' ').Length + 1;

        var duration = wordsInText / wordsPerMinute * 60;
        if (duration < 1)
            duration = 1;

        Actor.Talk(proximityText, duration);
        CurrentAction.LastProximityAction = DateTime.Now;

    }

    private void AdvanceSpawn(ScenarioNpcSpawnAction _) {
        Actor.Spawn();
        CurrentAction.IsFinished = true;
    }

    private void AdvanceDespawn(ScenarioNpcDespawnAction _) {
        Actor.Fade(-0.10f);
        if (Actor.IsFadedOut()) {
            Actor.Despawn();
            Actor.SetMovementAnimation(NpcAppearanceService.Animations.Idle);
            CurrentAction.IsFinished = true;
        }
    }

    private void AdvanceEmote(ScenarioNpcEmoteAction action, TimeSpan delta) {
        if (!CurrentAction.IsStarted || (action.Loop && !Actor.IsPlayingEmote(action.Emote, action.PoseState))) {
            Actor.PlayEmote(action.Emote, action.InteractWithLayout);
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;

        Actor.HoldEmotePose(action.Emote, action.PoseState);

        var isLoopingEmote = Actor.IsLoopingEmote(action.Emote);

        if (!action.Loop) {
            if (!Actor.IsPlayingEmote(action.Emote, action.PoseState) || (isLoopingEmote && CurrentAction.IsDurationExceeded)) {
                CurrentAction.IsFinished = true;
                if (isLoopingEmote && !action.StayInEmotePose) {
                    Actor.ResetMode();
                }
            }
        } else {
            if (CurrentAction.IsDurationExceeded) {
                CurrentAction.IsFinished = true;
                if (isLoopingEmote && !action.StayInEmotePose) {
                    Actor.ResetMode();
                }
            }
        }
    }

    private void AdvanceIdle(ScenarioNpcIdleAction action, TimeSpan delta) {
        if (!CurrentAction.IsStarted) {
            Actor.ResetMode();
            Actor.SetPose(PoseType.Idle, action.PoseState);
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        
        if ((action.PoseState == 0 && CurrentAction.IsEndless) || CurrentAction.IsDurationExceeded) {
            CurrentAction.IsFinished = true;            
        }
    }

    private void AdvanceTimeline(ScenarioNpcTimelineAction action, TimeSpan delta) {
        if (!CurrentAction.IsStarted) {
            Actor.SetMode(CharacterModes.None, 0);
            foreach (var timeline in action.ActionSlots) {
                Actor.PlayTimeline(timeline.TimelineId);
            }
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;

        if (CurrentAction.IsEndless) {            
            CurrentAction.IsFinished = !action.ActionSlots.Any(t => Actor.IsPlayingTimeline(t.TimelineId));
        } else if (CurrentAction.IsDurationExceeded) {
            CurrentAction.IsFinished = true;
        }

    }

    private void AdvancePathMovement(ScenarioNpcPathAction action, TimeSpan delta) {

        if (!CurrentAction.Pathfinder.IsPathReady) {            
            if (CurrentAction.IsFinished)
                FinishMovement();
            return;
        }

        Actor.SetMovementMotion(CurrentAction.Pathfinder.CurrentSpeedValue);

        if (!CurrentAction.Pathfinder.IsUserReady) {
            var currentRotation = Actor.GetRotation();
            var targetRotation = Actor.GetPosition().DirectionTo((Vector3)CurrentAction.Pathfinder.FirstTargetPoint);
            if (!RotationExtension.AlmostEqual(currentRotation, targetRotation)) {
                var rotationStep = NpcActor.TurningSpeed * (float)delta.TotalSeconds;
                var newRotation = RotationExtension.RotateToward(currentRotation, targetRotation, rotationStep);

                Actor.SetRotation(newRotation);
                return;
            }

            CurrentAction.Pathfinder.IsUserReady = true;
        }

        CurrentAction.Pathfinder.Update((float)delta.TotalSeconds, out var nextPos, out var yaw);
        Actor.SetPosition(nextPos);
        Actor.SetRotation(yaw);

        if (CurrentAction.Pathfinder.IsFinished)
            FinishMovement();
    }

    private void FinishMovement() {
        if (!IsNextActionMovementRelated()) {
            Actor.SetMovementAnimation(NpcAppearanceService.Animations.Idle);
        }
        CurrentAction.IsFinished = true;
    }

    private void AdvanceSimpleMovement(ScenarioNpcMovementAction action, TimeSpan delta) {
        var travelSpeed = PathMovementRuntime.ResolveSpeed(action);

        Actor.SetMovementMotion(travelSpeed);

        var currentRotation = Actor.GetRotation();
        var targetRotation = Actor.GetPosition().DirectionTo(action.TargetPosition);
        if (!RotationExtension.AlmostEqual(currentRotation, targetRotation)) {
            var rotationStep = NpcActor.TurningSpeed * (float)delta.TotalSeconds;
            var newRotation = RotationExtension.RotateToward(currentRotation, targetRotation, rotationStep);

            Actor.SetRotation(newRotation);
            return;
        }

        var currentPosition = Actor.GetPosition();
        var targetPosition = action.TargetPosition;
        var distanceStep = travelSpeed * (float)delta.TotalSeconds / Vector3.Distance(currentPosition, targetPosition);
        var newPosition = Vector3.Lerp(currentPosition, targetPosition, distanceStep);

        if (targetPosition.X == newPosition.X && targetPosition.Z == newPosition.Z) {
            Actor.SetPosition(action.TargetPosition);
            if (!IsNextActionMovementRelated()) {
                Actor.SetMovementAnimation(NpcAppearanceService.Animations.Idle);
            }

            CurrentAction.IsFinished = true;
        } else {
            Actor.SetPosition(newPosition);
        }
    }

    private void AdvanceRotation(ScenarioNpcRotationAction action, TimeSpan delta) {
        if (!CurrentAction.IsStarted) {
            Actor.SetMovementAnimation(NpcAppearanceService.Animations.Walking);
        }

        var rotationStep = NpcActor.TurningSpeed * (float)delta.TotalSeconds;
        var newRotation = RotationExtension.RotateToward(Actor.GetRotation(), action.TargetRotation, rotationStep);

        Actor.SetRotation(newRotation);

        if (RotationExtension.AlmostEqual(newRotation, action.TargetRotation)) {
            if (!IsNextActionMovementRelated()) {
                Actor.SetMovementAnimation(NpcAppearanceService.Animations.Idle);
            }
            CurrentAction.IsFinished = true;
        }
    }

    private void AdvanceTime(TimeSpan delta) {
        if (CurrentAction.IsEndless)
            return;

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        if (CurrentAction.IsDurationExceeded)
            CurrentAction.IsFinished = true;
    }

    private ScenarioNpcActionExecution SetupNextAction() {

        var execution = new ScenarioNpcActionExecution { Action = GetNextAction() };
        if (execution.IsEmpty) {
            return execution;
        }

        switch (execution.Action) {
            case ScenarioNpcPathAction pathAction:
                execution.Pathfinder.Reset();

                var firstPoint = pathAction.Points.FirstOrDefault();
                if (firstPoint != null) {
                    // add the current actor position to the point iteration to not "warp" around
                    var pathPoints = pathAction.Points.Select(s => new PathSegmentPoint { Point = s.Point, Speed = PathMovementRuntime.ResolveSpeed(s) }).ToList();
                    pathPoints.Insert(0, new PathSegmentPoint { Point = Actor.GetPosition(), Speed = PathMovementRuntime.ResolveSpeed(firstPoint) });

                    execution.IsFinished = !execution.Pathfinder.Compile(pathPoints, pathAction.Tension, PathMovementIntegrationMode.CrossSingleBoundary);
                } else {
                    log.Warning($"[{ScenarioInstance.AsHexString()}] [{CurrentScenarioSegment}] [{Id}:{Name}] Skipping path action without points");
                    execution.IsFinished = true;
                }

                break;

            case ScenarioNpcWaitingAction 
                or ScenarioNpcTimelineAction 
                or ScenarioNpcEmoteAction 
                or ScenarioNpcIdleAction:
                execution.TargetDuration = execution.Action.Duration;
                break;
        }

        return execution;
    }
    private ScenarioNpcAction GetNextAction() {
        if (_scenarioActions.Count == 0) {
            // each sync closes a segment, so the segment of an action is the number of syncs before it plus one
            var segment = 1;
            foreach (var candidate in _actions) {
                if (segment == CurrentScenarioSegment)
                    _scenarioActions.Enqueue(candidate);
                if (candidate is ScenarioNpcSyncAction)
                    segment++;
            }
        }

        if (_scenarioActions.TryDequeue(out var action)) {
            return action;
        } else {
            return ScenarioNpcEmptyAction.Default;
        }
    }

    private bool IsNextActionMovementRelated() {

        if (_scenarioActions.TryPeek(out var nextAction) &&
            (nextAction is ScenarioNpcMovementAction
            || nextAction is ScenarioNpcPathAction
            || nextAction is ScenarioNpcRotationAction
            )) {
            return true;
        }

        return false;
    }
}

public class ScenarioNpcActionExecution {
    public static ScenarioNpcActionExecution Default => new() { Action = ScenarioNpcEmptyAction.Default };

    public float TargetDuration { get; set; }
    public float CurrentDuration { get; set; }

    public bool IsDurationExceeded
        => TargetDuration > 0 && CurrentDuration > TargetDuration;
    public bool IsEndless
        => TargetDuration == 0;

    public bool IsStarted { get; set; } = false;
    public bool IsFinished { get; set; } = false;

    public bool IsSync { get => Action is ScenarioNpcSyncAction; }
    public bool IsEmpty { get => Action is ScenarioNpcEmptyAction; }

    public DateTime LastProximityAction { get; set; } = DateTime.MinValue;
    public bool ProximityExecuted { get; set; } = false;
    public bool IsInProximity { get; set; } = false;
    public required ScenarioNpcAction Action { get; set; }

    public PathMovementRuntime Pathfinder { get; set; } = new PathMovementRuntime();

}
