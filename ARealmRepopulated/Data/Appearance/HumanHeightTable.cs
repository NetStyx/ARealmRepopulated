using System.Buffers.Binary;
using System.IO;

namespace ARealmRepopulated.Data.Appearance;

/// <summary>
/// The size ranges from chara/xls/charaMake/human.cmp, looked up the way the game does when it sets up a human draw object.
/// See /doc/research/model-scale.md.
/// </summary>
/// <remarks>
/// One entry per race, body type and clan, each holding a min/max pair for males and one for females.
/// The customize Height (0-100) picks a point in between, 255 means a plain 1.0 and anything else above 100 the minimum.
/// </remarks>
public class HumanHeightTable {

    public const string GamePath = "chara/xls/charaMake/human.cmp";

    private const int TableStart = 0x2C800;
    private const int EntrySize = 0x38;
    private const int RaceCount = 8;
    private const int EntriesPerRace = 10; // 5 body types, 2 clans each

    private readonly byte[] _data;

    public HumanHeightTable(byte[] cmpData) {
        if (cmpData.Length < TableStart + (RaceCount * EntriesPerRace * EntrySize))
            throw new InvalidDataException($"{GamePath} ({cmpData.Length} bytes) is smaller then the calculated table size.");

        _data = cmpData;
    }

    public float GetModelScale(NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, byte height) {
        if (height == byte.MaxValue)
            return 1f;

        // the game derives race and clan from the tribe alone and falls back to the first race and body type for anything out of range
        var tribeIndex = (uint)tribe - 1;
        var race = tribeIndex >> 1;
        if (race >= RaceCount)
            race = 0;

        var body = (uint)bodyType;
        if (body - 1 >= 5)
            body = 1;

        var entry = (race * EntriesPerRace) + ((body - 1) * 2) + (tribeIndex & 1);
        var offset = TableStart + ((int)entry * EntrySize) + (sex == NpcSex.Male ? 0 : 0x10);

        var min = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(offset));
        var max = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(offset + 4));
        var position = height <= 100 ? height / 100f : 0f;

        return ((max - min) * position) + min;
    }
}
