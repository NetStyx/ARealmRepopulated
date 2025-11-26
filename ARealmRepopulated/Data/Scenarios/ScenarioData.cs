using FFXIVClientStructs.FFXIV.Common.Math;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Scenarios;
public class ScenarioData : IScenarioMetaData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int TerritoryId { get; set; }
    public List<ScenarioNpcData> Npcs { get; set; } = [];
    public bool Looping { get; set; } = true;
    public float LoopDelay { get; set; } = 0f;
    public bool Enabled { get; set; } = true;
}

public class ScenarioFileMetaData : IScenarioMetaData
{
    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int TerritoryId { get; set; }
}

public interface IScenarioMetaData
{
    string Title { get; }
    string Description { get; }
    int TerritoryId { get; }    
    bool Enabled { get; }
}


public class ScenarioNpcData
{
    public string Name { get; set; } = "";
    public string Appearance { get; set; } = "";
    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public List<ScenarioNpcAction> Actions { get; set; } = [];

}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$action")]
[JsonDerivedType(typeof(ScenarioNpcSpawnAction), typeDiscriminator: "Spawn")]
[JsonDerivedType(typeof(ScenarioNpcDespawnAction), typeDiscriminator: "Despawn")]
[JsonDerivedType(typeof(ScenarioNpcWaitingAction), typeDiscriminator: "Waiting")]
[JsonDerivedType(typeof(ScenarioNpcMovementAction), typeDiscriminator: "Movement")]
[JsonDerivedType(typeof(ScenarioNpcRotationAction), typeDiscriminator: "Rotation")]
[JsonDerivedType(typeof(ScenarioNpcEmoteAction), typeDiscriminator: "Emote")]
[JsonDerivedType(typeof(ScenarioNpcSyncAction), typeDiscriminator: "Sync")]
public abstract class ScenarioNpcAction
{
    internal int ScenarioKey { get; set; } = 0;

    [DefaultValue("")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string NpcTalk { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Duration { get; set; } = 0f;
}

public class ScenarioNpcWaitingAction : ScenarioNpcAction
{
    // do nothing
}

public class ScenarioNpcSpawnAction : ScenarioNpcAction
{
    // do nothing
}

public class ScenarioNpcDespawnAction : ScenarioNpcAction
{
    // do nothing
}

public class ScenarioNpcMovementAction : ScenarioNpcAction
{
    public Vector3 TargetPosition { get; set; }
    public bool IsRunning { get; set; } = false;
}

public class ScenarioNpcRotationAction : ScenarioNpcAction
{
    public float TargetRotation { get; set; }
}

public class ScenarioNpcEmoteAction : ScenarioNpcAction
{
    public ushort Emote { get; set; }
    public bool Loop { get; set; }

    internal ushort TimelineId { get; set; }
}

public class ScenarioNpcTimelineAction : ScenarioNpcAction
{
    public ushort TimelineId { get; set; }
}

public class ScenarioNpcSyncAction : ScenarioNpcAction
{
}

public class ScenarioNpcEmptyAction : ScenarioNpcAction
{
    // Used when no further action could be determined
    public static ScenarioNpcEmptyAction Default { get; set; } = new ScenarioNpcEmptyAction();
}



