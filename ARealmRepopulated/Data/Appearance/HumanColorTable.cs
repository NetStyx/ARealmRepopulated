using System.IO;
using System.Numerics;

namespace ARealmRepopulated.Data.Appearance;

/// <summary>
/// human.cmp stores, beside the heights (see <see cref="HumanHeightTable"/>), the color palettes of each clan. Offsets and values are copied from 'SetupCustomizeColors' (which also is not defined in cs :P),
/// the swatches from the color addons of the character creator. /doc/research/character-editor.md shows the way to reach the functions.
/// </summary>
public class HumanColorTable {

    private const int ColorSize = 4;
    private const int PageSize = 256;
    private const int LargePaletteSize = 192;
    private const int SmallPaletteSize = 96;
    private const int SecondPaletteValue = 128;

    private const int HairColorStep = 2;

    private const int EyePage = 0;
    private const int HighlightsPage = 1;
    private const int LipPage = 2;
    private const int TattooPage = 3;
    private const int FacePaintPage = 4;
    private const int SharedSwatchDistance = 9;

    private const int FirstClanPage = 18;
    private const int PagesPerClan = 5;
    private const int ClanSwatchDistance = 3;
    private const int PageCount = FirstClanPage + ((int)NpcTribe.Veena * 2 * PagesPerClan);

    private readonly record struct PaletteLocation(int Page, int SwatchPage, int Step = 1);

    private readonly byte[] _data;

    public HumanColorTable(byte[] cmpData) {
        if (cmpData.Length < PageCount * PageSize * ColorSize)
            throw new InvalidDataException($"human.cmp ({cmpData.Length} bytes) is smaller than the color table.");

        _data = cmpData;
    }

    public CharacterEditorColorPalette[] GetPalettes(CustomizeIndex index, NpcTribe tribe, NpcSex gender) {
        var location = Locate(index, tribe, gender);
        return index is CustomizeIndex.LipColorFurPattern or CustomizeIndex.FacePaintColor
            ? [Read(location, 0, SmallPaletteSize), Read(location, SecondPaletteValue, SmallPaletteSize)]
            : [Read(location, 0, LargePaletteSize)];
    }

    // The game takes any value and exploits this for a lot of npcs
    public CharacterEditorColor GetColor(CustomizeIndex index, NpcTribe tribe, NpcSex gender, byte value)
        => ReadColor(Locate(index, tribe, gender), value);

    private static PaletteLocation Locate(CustomizeIndex index, NpcTribe tribe, NpcSex gender) {
        var clan = (((int)tribe - 1) * 2) + (int)gender;
        var clanPage = FirstClanPage + (clan * PagesPerClan);
        return index switch {
            CustomizeIndex.SkinColor => new(clanPage, clanPage + ClanSwatchDistance),
            CustomizeIndex.HairColor => new(clanPage + 1, clanPage + 1 + ClanSwatchDistance, HairColorStep),
            CustomizeIndex.HighlightsColor => Shared(HighlightsPage),
            CustomizeIndex.FacialFeaturesColor => Shared(TattooPage),
            CustomizeIndex.LipColorFurPattern => Shared(LipPage),
            CustomizeIndex.FacePaintColor => Shared(FacePaintPage),
            _ => Shared(EyePage)
        };
    }

    private static PaletteLocation Shared(int page)
        => new(page, page + SharedSwatchDistance);

    private CharacterEditorColorPalette Read(PaletteLocation location, int firstValue, int count)
        => new([.. Enumerable.Range(firstValue, count).Select(value => ReadColor(location, (byte)value))]);

    private CharacterEditorColor ReadColor(PaletteLocation location, byte value) {
        var color = ReadEntry((location.Page * PageSize) + (value * location.Step));
        var swatch = ReadEntry((location.SwatchPage * PageSize) + value);
        return new CharacterEditorColor(value, color, swatch with { W = 1f });
    }

    private Vector4 ReadEntry(int entry) {
        var offset = entry * ColorSize;
        return new Vector4(_data[offset] / 255f, _data[offset + 1] / 255f, _data[offset + 2] / 255f, _data[offset + 3] / 255f);
    }
}
