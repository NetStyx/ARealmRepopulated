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

    public bool IsFinished
        => _state.CurrentScenarioSegment != 0 && Npcs.All(n => n.CurrentAction.IsEmpty);

    public bool IsSyncing
        => Npcs.All(n => n.CurrentAction.IsSync || n.CurrentAction.IsEmpty);

    public bool IsFirstRun
        => _state.CurrentScenarioSegment == 0;

    public void WaitForNextRun(TimeSpan time) {

        if (_currentDelay == 0) {
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
        _currentDelay = 0;
        _state.CurrentScenarioSegment = 0;
        Npcs.ForEach(n => {
            n.Actor.ResetPosition();
            n.Actor.ResetRotation();
            n.Actor.ResetDrawOffset();
        });
    }

    public void Advance(TimeSpan time) {
        if (IsSyncing || IsFirstRun) {
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

    private List<ScenarioNpcAction> _actions { get; set; } = [];

    private readonly Queue<ScenarioNpcAction> _scenarioActions = new();

    public ScenarioNpcActionExecution CurrentAction { get; private set; } = ScenarioNpcActionExecution.Default;

    private readonly TimeSpan _proximityTimeout = TimeSpan.FromSeconds(15);
    private readonly float _proximityChatDistance = 10f;
    private readonly float _proximityLookDistance = 4f;

    public void SetActions(List<ScenarioNpcAction> actions) {
        var npcActions = actions.Where(a => a.Enabled).ToList();
        if (actions.Count == 0) {
            // if no actions are defined, add a default wait action to prevent the scenario from immediately looping.
            npcActions.Add(new ScenarioNpcWaitingAction());
        }

        // attach a sync node at the end to make sure the scenario actually finishes.        
        if (npcActions.LastOrDefault() is not ScenarioNpcSyncAction) {
            npcActions.Add(new ScenarioNpcSyncAction());
        }

        _actions.Clear();
        AddAction([.. npcActions]);
    }

    public void AddAction(params ScenarioNpcAction[] actions) {        
        var scenarioKey = _actions.Count(a => a is ScenarioNpcSyncAction) + 1;
        foreach (var action in actions) {
            action.ScenarioKey = scenarioKey;
            if (action is ScenarioNpcSyncAction)
                scenarioKey++;
            _actions.Add(action);
        }
    }

    public void Advance(ScenarioState state, TimeSpan time) {
        if (CurrentAction.IsFinished || CurrentScenarioSegment != state.CurrentScenarioSegment) {
            CurrentScenarioSegment = state.CurrentScenarioSegment;
            CurrentAction = SetupNextAction();
            log.Debug($"[{ScenarioInstance.AsHexString()}] [{CurrentScenarioSegment}] [{Id}:{Name}] Starting action '{CurrentAction.Action}'");
        }

        if (CurrentAction.IsInfinite || CurrentAction.IsEmpty)
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

            case ScenarioNpcSyncAction sync:
                AdvanceSync(state, sync, time);
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

        if (CurrentAction.TargetDuration == 0) {
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
        // after spawning it takes a few frames until the actor is in a stable animation state. so lets wait here.
        if (!Actor.IsReady())
            return;

        if ((action.Loop && !Actor.IsPlayingEmote(action.Emote, action.PoseState)) || CurrentAction.CurrentDuration == 0f) {
            Actor.PlayEmote(action.Emote, action.InteractWithLayout);
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;

        Actor.HoldEmotePose(action.Emote, action.PoseState);

        var isLoopingEmote = Actor.IsLoopingEmote(action.Emote);

        if (!action.Loop) {
            if (!Actor.IsPlayingEmote(action.Emote, action.PoseState) || (isLoopingEmote && CurrentAction.IsDurationExeeded)) {
                CurrentAction.IsFinished = true;
                if (isLoopingEmote && !action.StayInEmotePose) {
                    Actor.ResetMode();
                }
            }
        } else {
            if (CurrentAction.IsDurationExeeded) {
                CurrentAction.IsFinished = true;
                if (isLoopingEmote && !action.StayInEmotePose) {
                    Actor.ResetMode();
                }
            }
        }
    }

    private void AdvanceIdle(ScenarioNpcIdleAction action, TimeSpan delta) {
        // a pose only sticks once the actor settled into its idle animation after spawning
        if (action.PoseState > 0 && !Actor.IsReady())
            return;

        if (CurrentAction.CurrentDuration == 0f) {
            Actor.ResetMode();
            Actor.SetPose(PoseType.Idle, action.PoseState);
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        
        if ((action.PoseState == 0 && CurrentAction.IsEndless) || CurrentAction.IsDurationExeeded) {
            CurrentAction.IsFinished = true;            
        }
    }

    private void AdvanceTimeline(ScenarioNpcTimelineAction action, TimeSpan delta) {
        if (CurrentAction.CurrentDuration == 0f) {
            Actor.SetMode(CharacterModes.None, 0);
            foreach (var timeline in action.ActionSlots) {
                Actor.PlayTimeline(timeline.TimelineId);
            }
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;

        if (CurrentAction.IsEndless) {            
            CurrentAction.IsFinished = !action.ActionSlots.Any(t => Actor.IsPlayingTimeline(t.TimelineId));
        } else if (CurrentAction.IsDurationExeeded) {
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

        if (CurrentAction.CurrentDuration == 0f)
            CurrentAction.CurrentDuration = 0.1f;

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
        if (CurrentAction == null)
            return;

        if (CurrentAction.CurrentDuration == 0f) {
            CurrentAction.CurrentDuration = 0.1f;
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

    private void AdvanceSync(ScenarioState state, ScenarioNpcSyncAction _, TimeSpan __) {
        if (CurrentAction == null)
            return;

        if (state.CurrentScenarioSegment == CurrentScenarioSegment)
            CurrentAction.IsFinished = true;
    }

    private void AdvanceTime(TimeSpan delta) {
        if (CurrentAction == null || CurrentAction.IsEndless)
            return;

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        if (CurrentAction.IsDurationExeeded)
            CurrentAction.IsFinished = true;
    }

    private ScenarioNpcActionExecution SetupNextAction() {

        var execution = new ScenarioNpcActionExecution { Action = GetNextAction() };
        if (execution.IsEmpty) {
            return execution;
        }

        switch (execution.Action) {
            case ScenarioNpcPathAction pathAction:
                execution.IsInfinite = false;
                execution.Pathfinder.Reset();

                var firstPoint = pathAction.Points.FirstOrDefault();
                if (firstPoint != null) {
                    // add the current actor position to the point iteration to not "warp" around                    
                    var pathPoints = pathAction.Points.Select(s => new PathSegmentPoint { Point = s.Point, Speed = PathMovementRuntime.ResolveSpeed(s) }).ToList();
                    pathPoints.Insert(0, new PathSegmentPoint { Point = Actor.GetPosition(), Speed = PathMovementRuntime.ResolveSpeed(firstPoint) });

                    execution.IsFinished = !execution.Pathfinder.Compile(pathPoints, pathAction.Tension, PathMovementIntegrationMode.CrossSingleBoundary);
                } else {
                    execution.Action.NpcTalk = "\uE040 Tell the scenario writer that there is a problem with my path \uE041";
                }

                break;

            case var t when t is ScenarioNpcTimelineAction || t is ScenarioNpcEmoteAction || t is ScenarioNpcIdleAction:
                execution.IsInfinite = false;
                execution.TargetDuration = t.Duration;
                break;

            case ScenarioNpcMovementAction:
            case ScenarioNpcRotationAction:
            case ScenarioNpcSpawnAction:
            case ScenarioNpcDespawnAction:
                execution.IsInfinite = false;
                break;

            case { Duration: > 0 } t:
                execution.IsInfinite = false;
                execution.TargetDuration = t.Duration;
                break;
        }

        return execution;
    }
    private ScenarioNpcAction GetNextAction() {
        if (_scenarioActions.Count == 0) {
            _actions
                .Where(a => a.ScenarioKey == CurrentScenarioSegment)
                .ToList()
                .ForEach(_scenarioActions.Enqueue);
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
    public static ScenarioNpcActionExecution Default => new() { Action = new ScenarioNpcEmptyAction() };

    public float TargetDuration { get; set; }
    public float CurrentDuration { get; set; }

    public bool IsDurationExeeded
        => TargetDuration > 0 && CurrentDuration > 0 && CurrentDuration > TargetDuration;
    public bool IsEndless
        => TargetDuration == 0;

    //TODO: Remove and just check if IsEndless equals true
    public bool IsInfinite { get; set; } = true;
    public bool IsFinished { get; set; } = false;

    public bool IsSync { get => Action is ScenarioNpcSyncAction; }
    public bool IsEmpty { get => Action is ScenarioNpcEmptyAction; }

    public DateTime LastProximityAction { get; set; } = DateTime.MinValue;
    public bool ProximityExecuted { get; set; } = false;
    public bool IsInProximity { get; set; } = false;
    public required ScenarioNpcAction Action { get; set; }

    public PathMovementRuntime Pathfinder { get; set; } = new PathMovementRuntime();

}
