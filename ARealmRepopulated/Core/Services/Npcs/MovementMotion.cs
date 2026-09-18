namespace ARealmRepopulated.Core.Services.Npcs;

/// <summary>
/// Helper class that provides a mapping from a desired travel speed to the motion that best matches it.
/// </summary>
public static class MovementMotion {

    public const float MinCustomSpeed = 0.1f;
    public const float MaxCustomSpeed = 20f;
    public const float DefaultCustomSpeed = NpcActor.WalkingSpeed;
    
    private static readonly float WalkToRunCrossover = MathF.Sqrt(NpcActor.WalkingSpeed * NpcActor.RunningSpeed);
    private static readonly float RunToSprintCrossover = MathF.Sqrt(NpcActor.RunningSpeed * NpcActor.SprintingSpeed);

    public static float ClampSpeed(float speed)
        => float.IsNaN(speed) ? DefaultCustomSpeed : Math.Clamp(speed, MinCustomSpeed, MaxCustomSpeed);
    
    public static MovementMotionSelection Select(float speed) {
        var travelSpeed = ClampSpeed(speed);

        var (animation, naturalSpeed) = travelSpeed < WalkToRunCrossover
            ? (NpcAppearanceService.Animations.Walking, NpcActor.WalkingSpeed)
            : travelSpeed < RunToSprintCrossover
                ? (NpcAppearanceService.Animations.Running, NpcActor.RunningSpeed)
                : (NpcAppearanceService.Animations.Sprinting, NpcActor.SprintingSpeed);

        return new MovementMotionSelection {
            Animation = animation,
            AnimationSpeed = travelSpeed / naturalSpeed
        };
    }
}

public readonly struct MovementMotionSelection {
    public NpcAppearanceService.Animations Animation { get; init; }
    
    public float AnimationSpeed { get; init; }
}
