using System.Numerics;

namespace ARealmRepopulated.Data.Appearance;

public class CharacterEditorData {
    public List<CharacterEditorClan> Clans { get; set; } = [];
}

public class CharacterEditorNameGenerationData {
    public NpcRace Race { get; set; } = NpcRace.Unknown;
    public NpcTribe Tribe { get; set; } = NpcTribe.Unknown;
    public NpcSex Gender { get; set; } = NpcSex.Male;

    public List<string> FirstNames { get; set; } = [];
    public List<string> LastNames { get; set; } = [];
}

public class CharacterEditorClan {
    public NpcRace Race { get; set; } = NpcRace.Unknown;
    public NpcTribe Tribe { get; set; } = NpcTribe.Unknown;
    public NpcSex Gender { get; set; } = NpcSex.Male;
    public string Name { get; set; } = "";

    public List<CharacterEditorMenu> Menus { get; set; } = [];
    public uint[][] FacialFeatureIcons { get; set; } = [];
    public CharacterEditorColorPalette[] HighlightPalettes { get; set; } = [];

    public byte[] Voices { get; set; } = [];

    /// <summary>
    /// Fresh default values for everything the character creator offers this clan. Part of our safety net when switching between clans.
    /// </summary>
    public void ApplyDefaults(NpcAppearanceData appearance) {
        appearance.ResetModel();
        appearance.Race = Race;
        appearance.Tribe = Tribe;
        appearance.Sex = Gender;
        appearance.BodyType = NpcBodyType.Normal;
        appearance.ExtendedAppearance = null;
        appearance.Voice = Voices.Length > 0 ? Voices[0] : null;

        appearance.FacialFeatures = 0;
        appearance.Highlights = 0;
        appearance.HighlightsColor = 0;
        appearance.TailShape = 0;
        appearance.BustSize = 0;
        appearance.MuscleMass = 0;
        appearance.LipColorFurPattern = 0;
        appearance.FacePaint = 0;
        appearance.FacePaintColor = 0;
        appearance.Mouth = 0;
        appearance.EyeShape = 0;

        foreach (var menu in Menus) {
            if (menu.Kind == CharacterEditorMenuKind.FacialFeatures) {
                appearance.EnableFacialFeatures(menu.DefaultValue, menu.FirstFeatureBit);
            } else if (menu.Customize != CustomizeIndex.Height) {
                appearance.SetOption(menu.Customize, menu.DefaultValue);
            }

            if (menu.Customize == CustomizeIndex.EyeColorRight) {
                appearance.SetOption(CustomizeIndex.EyeColorLeft, menu.DefaultValue);
            }
        }
    }

    /// <summary>
    /// Sweeps invalid values from the appearance.
    /// No more headless actors.
    /// </summary>
    public void FitOptions(NpcAppearanceData appearance, CharacterBodyTypeLooks? looks) {
        foreach (var menu in Menus.Where(m => m.Customize is CustomizeIndex.Face or CustomizeIndex.HairStyle or CustomizeIndex.TailShape)) {
            var options = menu.GetBodyTypes(looks);
            var value = appearance.GetOption(menu.Customize);
            if (options.Length > 0 && !options.Any(o => o.Value == value) && looks?.GetValues(menu.Customize)?.Contains(value) != true) {
                appearance.SetOption(menu.Customize, options[0].Value);
            }
        }
    }
}

// See CharaMakeType.SubMenuType.
public enum CharacterEditorMenuKind : byte {
    Options = 0,
    Icons = 1,
    Color = 2,
    EyeColor = 3,
    FacialFeatures = 4,
    Slider = 5
}

/// <summary>
/// The games "menus" represent the different character editor options
/// </summary>
public class CharacterEditorMenu {
    public CustomizeIndex Customize { get; set; }
    public string Name { get; set; } = "";
    public CharacterEditorMenuKind Kind { get; set; }
    public byte DefaultValue { get; set; }

    public CharacterEditorOption[] Options { get; set; } = [];
    public CharacterEditorColorPalette[] Palettes { get; set; } = [];

    public int FirstFeatureBit { get; set; }
    public int FeatureCount { get; set; }

    public CharacterEditorOption[] GetBodyTypes(CharacterBodyTypeLooks? looks)
        => looks is { BodyType: not NpcBodyType.Normal } && looks.GetValues(Customize) is { } values ? AsIndexedOptions(values) : Options;

    public CharacterEditorOption[] GetSpecialBodyTypes(CharacterBodyTypeLooks? looks, byte current) {
        var options = GetBodyTypes(looks);
        var values = looks?.GetValues(Customize) ?? [];
        return AsIndexedOptions(values.Append(current).Distinct().Where(v => v != 0 && options.All(o => o.Value != v)).Order());
    }

    // There are no icons for the values of npcs, so these get numbers.
    private static CharacterEditorOption[] AsIndexedOptions(IEnumerable<byte> values)
        => [.. values.Select(v => new CharacterEditorOption(v, 0))];
}

public readonly record struct CharacterEditorOption(byte Value, uint Icon);

public record CharacterBodyTypeLooks(NpcBodyType BodyType, byte[] Faces, byte[] Hairstyles, byte[] TailShapes) {
    public byte[]? GetValues(CustomizeIndex index) => index switch {
        CustomizeIndex.Face => Faces,
        CustomizeIndex.HairStyle => Hairstyles,
        CustomizeIndex.TailShape => TailShapes,
        _ => null
    };
}

// Color is what the game renders, Swatch what the character creator shows for it.
public readonly record struct CharacterEditorColor(byte Value, Vector4 Color, Vector4 Swatch);

public record CharacterEditorColorPalette(CharacterEditorColor[] Colors);
