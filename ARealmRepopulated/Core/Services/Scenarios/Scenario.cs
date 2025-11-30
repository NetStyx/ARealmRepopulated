using ARealmRepopulated.Core.Math;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Core.Services.Scenarios;
public unsafe class Scenario
{
    public List<ScenarioNpc> Npcs { get; set; } = [];
    public bool IsLooping { get; set; } = false;
    public TimeSpan DelayBetweenRuns { get; set; } = TimeSpan.Zero;

    private ScenarioState _state = new();
    private double _currentDelay = 0;

    public bool IsFinished
        => _state.CurrentScenarioSegment != 0 && Npcs.All(n => n.CurrentAction.IsEmpty);

    public bool IsSyncing
        => Npcs.All(n => n.CurrentAction.IsSync);

    public bool IsFirstRun
        => _state.CurrentScenarioSegment == 0;

    public void WaitForNextRun(TimeSpan time)
    {
        _currentDelay += time.TotalMilliseconds;
        if (_currentDelay < DelayBetweenRuns.TotalMilliseconds)
        {
            return;
        }

        _currentDelay = 0;
        _state.CurrentScenarioSegment = 0;
        Npcs.ForEach(n =>
        {
            n.Actor.ResetPosition();
            n.Actor.ResetRotation();
        });
    }

    public void Advance(TimeSpan time)
    {
        if (IsSyncing || IsFirstRun)
        {
            _state.CurrentScenarioSegment++;
        }

        Npcs.ForEach(n => n.Advance(_state, time));
    }

    public void Proximity(Vector3 playerPosition)
    {
        Npcs.ForEach(n => n.Proximity(playerPosition));
    }
}

public class ScenarioState
{
    public int CurrentScenarioSegment { get; set; } = 0;
}

public unsafe class ScenarioNpc
{
    public int Id { get; set; } = 0;

    public int CurrentScenarioSegment { get; set; } = 0;

    public NpcActor Actor { get; set; } = null!;

    private List<ScenarioNpcAction> _actions { get; set; } = [];

    private Queue<ScenarioNpcAction> _scenarioActions = new();

    public ScenarioNpcActionExecution CurrentAction { get; private set; } = ScenarioNpcActionExecution.Default;
    
    private TimeSpan _proximityTimeout = TimeSpan.FromSeconds(15);
    private float _proximityDistance = 10f;

    public void AddAction(params ScenarioNpcAction[] actions)
    {
        var scenarioKey = 1;
        foreach (var action in actions)
        {
            action.ScenarioKey = scenarioKey;
            if (action is ScenarioNpcSyncAction)
                scenarioKey++;
            _actions.Add(action);
        }
    }

    public void Advance(ScenarioState state, TimeSpan time)
    {
        if (CurrentAction.IsFinished || CurrentScenarioSegment != state.CurrentScenarioSegment)
        {
            CurrentScenarioSegment = state.CurrentScenarioSegment;
            CurrentAction = SetupNextAction();
        }

        if (CurrentAction.IsInfinite || CurrentAction.IsEmpty)
            return;

        switch (CurrentAction.Action)
        {
            case ScenarioNpcMovementAction movement:
                AdvanceMovement(movement, time);
                break;

            case ScenarioNpcRotationAction rotation:
                AdvanceRotation(rotation, time);
                break;

            case ScenarioNpcEmoteAction emote:
                AdvanceEmote(emote, time);
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

    public void Proximity(Vector3 player)
    {
        if (string.IsNullOrWhiteSpace(CurrentAction.Action.NpcTalk))
        {
            return;
        }

        if (CurrentAction.TargetDuration == 0)
        {
            if (DateTime.Now - CurrentAction.LastProximityAction < _proximityTimeout)
            {
                return;
            }
            CurrentAction.ProximityExecuted = false;
        }
        else if (CurrentAction.ProximityExecuted)
        {
            return;
        }

        if (Actor.GetDistanceTo(player) > _proximityDistance)
        {            
            CurrentAction.IsInProximity = false;                
            return;
        }

        CurrentAction.IsInProximity = true;
        CurrentAction.ProximityExecuted = true;

        float wordsPerMinute = 150;
        float wordsInText = CurrentAction.Action.NpcTalk.Split(' ').Length + 1;

        var duration = wordsInText / wordsPerMinute * 60;
        if (duration < 1)
            duration = 1;

        Actor.ShowTalkBubble(duration);
        CurrentAction.LastProximityAction = DateTime.Now;

    }

    private void AdvanceSpawn(ScenarioNpcSpawnAction action)
    {
        Actor.Spawn();
        CurrentAction.IsFinished = true;
    }

    private void AdvanceDespawn(ScenarioNpcDespawnAction action)
    {
        Actor.Fade(-0.10f);
        if (Actor.IsFadedOut())
        {
            Actor.Despawn();
            Actor.SetAnimation(NpcAppearanceService.Animations.Idle);
            CurrentAction.IsFinished = true;
        }
    }

    private void AdvanceEmote(ScenarioNpcEmoteAction action, TimeSpan delta)
    {
        if (action.Loop || CurrentAction.CurrentDuration == 0f)
        {
            Actor.PlayEmote(action.Emote, true);
        }

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        if (!action.Loop)
        {            
            if(!Actor.IsPlayingEmote(action.Emote) || (Actor.IsLoopingEmote(action.Emote) && CurrentAction.IsDurationExeeded))
            {
                CurrentAction.IsFinished = true; 
            }
        }
        else
        {
            if (CurrentAction.IsDurationExeeded)
            {
                CurrentAction.IsFinished = true;             
            }
        }        
    }

    private void AdvanceTimeline(ScenarioNpcTimelineAction action, TimeSpan delta)
    {
        if (CurrentAction.CurrentDuration == 0f)
        {
            CurrentAction.CurrentDuration = 0.1f;
            Actor.PlayTimeline(action.TimelineId);
        }
    }

    private void AdvanceMovement(ScenarioNpcMovementAction action, TimeSpan delta)
    {
        if (CurrentAction.CurrentDuration == 0f)
        {
            CurrentAction.CurrentDuration = 0.1f;
            Actor.SetAnimation(action.IsRunning ? NpcAppearanceService.Animations.Running : NpcAppearanceService.Animations.Walking);
        }

        var currentRotation = Actor.GetRotation();
        var targetRotation = Actor.GetPosition().DirectionTo(action.TargetPosition);
        if (!RotationExtension.AlmostEqual(currentRotation, targetRotation))
        {
            var rotationStep = NpcActor.TurningSpeed * (float)delta.TotalSeconds;
            var newRotation = RotationExtension.RotateToward(currentRotation, targetRotation, rotationStep);

            Actor.SetRotation(newRotation);
            return;
        }


        var currentPosition = Actor.GetPosition();
        var targetPosition = action.TargetPosition;
        var speeds = action.IsRunning ? NpcActor.RunningSpeed : NpcActor.WalkingSpeed;

        var distanceStep = speeds * (float)delta.TotalSeconds / Vector3.Distance(currentPosition, targetPosition);
        var newPosition = Vector3.Lerp(currentPosition, targetPosition, distanceStep);

        if (targetPosition.X == newPosition.X && targetPosition.Z == newPosition.Z)
        {
            Actor.SetPosition(action.TargetPosition);
            if (_scenarioActions.TryPeek(out var nextAction) && nextAction is not ScenarioNpcMovementAction) { 
                Actor.SetAnimation(NpcAppearanceService.Animations.Idle);
            }
            CurrentAction.IsFinished = true;
        }
        else
        {
            Actor.SetPosition(newPosition);
        }
    }

    private void AdvanceRotation(ScenarioNpcRotationAction action, TimeSpan delta)
    {
        if (CurrentAction == null)
            return;

        if (CurrentAction.CurrentDuration == 0f)
        {
            CurrentAction.CurrentDuration = 0.1f;
            Actor.SetAnimation(NpcAppearanceService.Animations.Turning);
        }

        var rotationStep = NpcActor.TurningSpeed * (float)delta.TotalSeconds;
        var newRotation = RotationExtension.RotateToward(Actor.GetRotation(), action.TargetRotation, rotationStep);

        Actor.SetRotation(newRotation);

        if (RotationExtension.AlmostEqual(newRotation, action.TargetRotation))
        {
            Actor.SetAnimation(NpcAppearanceService.Animations.Idle);
            CurrentAction.IsFinished = true;
        }
    }

    private void AdvanceSync(ScenarioState state, ScenarioNpcSyncAction action, TimeSpan delta)
    {
        if (CurrentAction == null)
            return;

        if (state.CurrentScenarioSegment == CurrentScenarioSegment)
            CurrentAction.IsFinished = true;
    }

    private void AdvanceTime(TimeSpan delta)
    {
        if (CurrentAction == null)
            return;

        CurrentAction.CurrentDuration += (float)delta.TotalSeconds;
        if (CurrentAction.CurrentDuration > CurrentAction.TargetDuration)
            CurrentAction.IsFinished = true;
    }

    private ScenarioNpcActionExecution SetupNextAction()
    {
        var execution = new ScenarioNpcActionExecution { Action = GetNextAction() };
        if (execution.IsEmpty)
        {
            return execution;
        }

        switch (execution.Action)
        {
            case ScenarioNpcMovementAction movement:
                execution.IsInfinite = false;
                break;
            case ScenarioNpcRotationAction rotation:
                execution.IsInfinite = false;
                break;

            case ScenarioNpcEmoteAction emote:
                execution.IsInfinite = false;
                execution.TargetDuration = emote.Duration;
                break;

            case ScenarioNpcTimelineAction timeline:
                execution.IsInfinite = false;
                break;

            case ScenarioNpcSpawnAction spawn:
                execution.IsInfinite = false;
                break;

            case ScenarioNpcDespawnAction despawn:
                execution.IsInfinite = false;
                break;

            case { Duration: > 0 } t:
                execution.IsInfinite = false;
                execution.TargetDuration = t.Duration;
                break;
        }

        return execution;
    }
    private ScenarioNpcAction GetNextAction()
    {
        if (_scenarioActions.Count == 0)
        {
            _actions
                .Where(a => a.ScenarioKey == CurrentScenarioSegment)
                .ToList()
                .ForEach(_scenarioActions.Enqueue);
        }

        if (_scenarioActions.TryDequeue(out var action))
        {
            return action;
        }
        else
        {
            return ScenarioNpcEmptyAction.Default;
        }
    }

}

public class ScenarioNpcActionExecution
{

    public static ScenarioNpcActionExecution Default => new() { Action = new ScenarioNpcEmptyAction() };

    public float TargetDuration { get; set; }
    public float CurrentDuration { get; set; }

    public bool IsDurationExeeded 
        => TargetDuration > 0 && CurrentDuration > 0 && CurrentDuration > TargetDuration;

    public bool IsInfinite { get; set; } = true;
    public bool IsFinished { get; set; } = false;

    public bool IsSync { get => Action is ScenarioNpcSyncAction; }
    public bool IsEmpty { get => Action is ScenarioNpcEmptyAction; }

    public DateTime LastProximityAction { get; set; } = DateTime.MinValue;
    public bool ProximityExecuted { get; set; } = false;
    public bool IsInProximity { get; set; } = false;

    public required ScenarioNpcAction Action { get; set; }
}
