using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Appearance;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Scenarios;

public class ScenarioData : IScenarioMetaData {
    public int Version { get; set; } = ScenarioMigrator.CurrentScenarioVersion;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ScenarioLocation Location { get; set; } = new();
    public List<ScenarioNpcData> Npcs { get; set; } = [];
    public bool Looping { get; set; } = true;
    public float LoopDelay { get; set; } = 0f;
    public bool Enabled { get; set; } = true;
}

public class ScenarioFileMetaData : IScenarioMetaData {
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ScenarioLocation Location { get; set; } = new();
}

public class ScenarioLocation {
    public int Territory { get; set; }
    public int Server { get; set; }
    public int HousingDivision { get; set; }
    public int HousingWard { get; set; }
    public int HousingPlot { get; set; }
}

public interface IScenarioMetaData {
    int Version { get; }
    string Title { get; }
    string Description { get; }
    ScenarioLocation Location { get; }
    bool Enabled { get; }
}

public class ScenarioNpcData {
    public string Name { get; set; } = "";
    public NpcAppearanceData Appearance { get; set; } = NpcAppearanceData.Default;
    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public List<ScenarioNpcAction> Actions { get; set; } = [];

}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$action")]
[JsonDerivedType(typeof(ScenarioNpcSpawnAction), typeDiscriminator: "Spawn")]
[JsonDerivedType(typeof(ScenarioNpcDespawnAction), typeDiscriminator: "Despawn")]
[JsonDerivedType(typeof(ScenarioNpcWaitingAction), typeDiscriminator: "Waiting")]
[JsonDerivedType(typeof(ScenarioNpcMovementAction), typeDiscriminator: "Movement")]
[JsonDerivedType(typeof(ScenarioNpcPathAction), typeDiscriminator: "Path")]
[JsonDerivedType(typeof(ScenarioNpcRotationAction), typeDiscriminator: "Rotation")]
[JsonDerivedType(typeof(ScenarioNpcEmoteAction), typeDiscriminator: "Emote")]
[JsonDerivedType(typeof(ScenarioNpcIdleAction), typeDiscriminator: "Idle")]
[JsonDerivedType(typeof(ScenarioNpcSyncAction), typeDiscriminator: "Sync")]
public abstract class ScenarioNpcAction {
    internal int ScenarioKey { get; set; } = 0;

    [DefaultValue("")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string NpcTalk { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Duration { get; set; } = 0f;

    [DefaultValue(true)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CanHaveTalk { get; set; } = true;

    [DefaultValue(true)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CanHaveDuration { get; set; } = true;

    public ScenarioNpcAction() { }
    public ScenarioNpcAction(bool canHaveTalk, bool canHaveDuration) {
        CanHaveTalk = canHaveTalk;
        CanHaveDuration = canHaveDuration;
    }
}

public enum NpcSpeed {
    Walking, // = 2.5f
    Running, // = 6.3f
    Custom // use CustomSpeed
}

public class ScenarioNpcWaitingAction : ScenarioNpcAction {
    // do nothing
    public override string ToString() => $"Waiting [Duration: {Duration}]";
}

public class ScenarioNpcSpawnAction() : ScenarioNpcAction(false, false) {
    // do nothing
    public override string ToString() => "Spawn";
}

public class ScenarioNpcDespawnAction() : ScenarioNpcAction(false, false) {
    // do nothing
    public override string ToString() => "Despawn";
}

public class ScenarioNpcMovementAction : ScenarioNpcAction {
    public Vector3 TargetPosition { get; set; }
    public NpcSpeed Speed { get; set; }

    public override string ToString() => $"Move [Duration: {Duration}; Speed: {Speed}; Target: {TargetPosition}]";
}

public class ScenarioNpcPathAction() : ScenarioNpcAction {
    public float Tension { get; set; } = 0.3f;
    public List<PathMovementPoint> Points { get; set; } = [];

    public override string ToString()
        => $"Path [Duration: {Duration}; Tension: {Tension}, Points: {Points.Count}]";
}

public class PathMovementPoint {
    public Vector3 Point { get; set; }
    public NpcSpeed Speed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float CustomSpeed { get; set; } = 0f;
}

public class ScenarioNpcRotationAction : ScenarioNpcAction {
    public float TargetRotation { get; set; }
    public override string ToString() => $"Rotate [Duration: {Duration}; Angle: {TargetRotation}]";
}

public class ScenarioNpcEmoteAction : ScenarioNpcAction {
    public ushort Emote { get; set; }
    public bool Loop { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StayInEmotePose { get; set; } = false;

    public override string ToString() => $"Emote [ID: {Emote}; Duration: {Duration}; Loop: {Loop}; StayInEmotePose: {StayInEmotePose}]";
}

public class ScenarioNpcIdleAction() : ScenarioNpcAction(false, false) {
    // do nothing
    public override string ToString() => $"Idle";
}

public class ScenarioNpcTimelineAction : ScenarioNpcAction {
    public ushort TimelineId { get; set; }

    public override string ToString() => $"Timeline [ID: {TimelineId}; Duration: {Duration}]";
}

public class ScenarioNpcSyncAction() : ScenarioNpcAction(false, false) {
    public override string ToString() => $"Sync";
}

public class ScenarioNpcEmptyAction() : ScenarioNpcAction(false, false) {
    // Used when no further action could be determined
    public static ScenarioNpcEmptyAction Default { get; set; } = new ScenarioNpcEmptyAction();

    public override string ToString() => $"Empty";
}

