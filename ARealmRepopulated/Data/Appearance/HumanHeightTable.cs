using System.Buffers.Binary;
using System.IO;

namespace ARealmRepopulated.Data.Appearance;

public readonly record struct ModelScaleComponents(byte Height, float ScaleFactor);

/// <summary>
/// human.cmp stores the size ranges of each race. The logic here is copied from the games 'GetHumanModelScale' (which is unfortunately not defined in cs)
/// See /doc/research/model-scale.md.
/// </summary>
/// <remarks>
/// The whole implementation is currently only needed because the .chara files might give me the a computed value as HeightMultiplier instead of 
/// the 0 - 100 height range, so it has to be turned back into a height. Maybe if i ever get around to implementing a proper character editor 
/// this will be more useful.
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

    public ModelScaleComponents ReverseModelScale(NpcTribe tribe, NpcSex sex, NpcBodyType bodyType, float modelScale) {
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
        var step = (max - min) / 100f;
        var position = step > 0 ? (modelScale - min) / step : (modelScale > max ? 100f : 0f);

        var height = (byte)Math.Clamp(MathF.Round(position), 0f, 100f);
        var fitted = ((max - min) * (height / 100f)) + min;
        var scaleFactor = MathF.Abs(modelScale - fitted) <= step / 2 ? 1f : modelScale / fitted;

        return new ModelScaleComponents(height, scaleFactor);
    }
}
