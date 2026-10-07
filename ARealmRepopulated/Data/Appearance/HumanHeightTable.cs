using System.Buffers.Binary;
using System.IO;

namespace ARealmRepopulated.Data.Appearance;

/// <summary>
/// The size ranges from human.cmp with the logic copied from the games 'GetHumanModelScale' (which is unfortunately not defined in cs)
/// See /doc/research/model-scale.md.
/// </summary>
/// <remarks>
/// The whole implementation is currently only needed because the .chara files gives me the computed value instead of the 1 - 100 range.
/// Maybe if i ever get around to implementing a proper character editor this will be more useful.
/// </remarks>
public class HumanHeightTable {

    public const string GamePath = "chara/xls/charaMake/human.cmp";

    private const int TableStart = 0x2C800;
    private const int EntrySize = 0x38;
    private const int RaceCount = 8;
    private const int EntriesPerRace = 10;

    private readonly byte[] _data;

    public HumanHeightTable(byte[] cmpData) {
        if (cmpData.Length < TableStart + (RaceCount * EntriesPerRace * EntrySize))
            throw new InvalidDataException($"{GamePath} ({cmpData.Length} bytes) is smaller then the calculated table size.");

        _data = cmpData;
    }

    public float GetModelScale(NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, byte height) {
        if (height == byte.MaxValue)
            return 1f;

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
