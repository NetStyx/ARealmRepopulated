namespace ARealmRepopulated.Data.Appearance;

public class CharacterEditorData {
    public List<CharacterEditorRace> Races { get; set; } = [];
}

public class CharacterEditorNameGenerationData {
    public NpcRace Race { get; set; } = NpcRace.Unknown;
    public NpcTribe Tribe { get; set; } = NpcTribe.Unknown;
    public NpcSex Gender { get; set; } = NpcSex.Male;

    public List<string> FirstNames { get; set; } = [];
    public List<string> LastNames { get; set; } = [];
}

public class CharacterEditorRace {
    public NpcRace Race { get; set; } = NpcRace.Unknown;
    public NpcTribe Tribe { get; set; } = NpcTribe.Unknown;
    public NpcSex Gender { get; set; } = NpcSex.Male;

    public uint[] Faces { get; set; } = [];
    public uint[] FacePaints { get; set; } = [];
    public uint[] Eyebrows { get; set; } = [];
    public uint[] EyeShapes { get; set; } = [];
    public uint[] NoseShapes { get; set; } = [];
    public uint[] JawShapes { get; set; } = [];
    public uint[] MouthShapes { get; set; } = [];
    public uint[] LipColorsDark { get; set; } = [];
    public uint[] TailEarShapes { get; set; } = [];

    public bool HasLipstick { get; set; }
    public bool HasMuscleMass { get; set; }
    public bool HasTailEarShapes { get; set; }
    /*
    
    Set(GetListSize(row, CustomizeIndex.MuscleMass) > 0, CustomizeIndex.MuscleMass);    
    Set(GetListSize(row, CustomizeIndex.BustSize) > 0, CustomizeIndex.BustSize);    
    */
}

