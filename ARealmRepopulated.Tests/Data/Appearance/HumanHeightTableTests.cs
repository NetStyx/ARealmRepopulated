using ARealmRepopulated.Data.Appearance;
using Shouldly;
using System;
using System.Buffers.Binary;
using System.IO;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class HumanHeightTableTests {

    private const int TableStart = 0x2C800;
    private const int TableEnd = 0x2D980;
    private const int EntrySize = 0x38;

    private static byte[] BuildCmp(int entry, float maleMin, float maleMax, float femaleMin, float femaleMax) {
        var data = new byte[TableEnd];
        var offset = TableStart + (entry * EntrySize);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset), maleMin);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 4), maleMax);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 0x10), femaleMin);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 0x14), femaleMax);
        return data;
    }

    [Theory]
    [InlineData(1.0f, 0)]
    [InlineData(1.1f, 50)]
    [InlineData(1.2f, 100)]
    [InlineData(1.1013f, 51)]
    public void ReverseModelScale_InsideTheRange_FindsTheHeightAndKeepsTheScale(float modelScale, byte height) {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        var components = table.ReverseModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, modelScale);

        components.Height.ShouldBe(height);
        components.ScaleFactor.ShouldBe(1f);
    }

    [Fact]
    public void ReverseModelScale_ForMale_ReadsTheMalePair() {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        table.ReverseModelScale(NpcTribe.Highlander, NpcSex.Male, NpcBodyType.Normal, 0.55f).Height.ShouldBe((byte)50);
    }

    [Fact]
    public void ReverseModelScale_TakesRaceAndClanFromTheTribe() {
        // Xaela: sixth race, second clan, young body type -> 5 * 10 + (4 - 1) * 2 + 1
        var table = new HumanHeightTable(BuildCmp(57, 0.5f, 0.6f, 0.7f, 0.9f));

        var components = table.ReverseModelScale(NpcTribe.Xaela, NpcSex.Female, NpcBodyType.Young, 0.8f);

        components.Height.ShouldBe((byte)50);
        components.ScaleFactor.ShouldBe(1f);
    }

    [Fact]
    public void ReverseModelScale_WithUnknownBodyType_UsesNormal() {
        var table = new HumanHeightTable(BuildCmp(0, 0.9f, 1.1f, 0.8f, 1.0f));

        var components = table.ReverseModelScale(NpcTribe.Midlander, NpcSex.Female, NpcBodyType.Unknown, 0.9f);

        components.Height.ShouldBe((byte)50);
        components.ScaleFactor.ShouldBe(1f);
    }

    [Theory]
    [InlineData(0.5f, 0, 0.5f)]
    [InlineData(1.8f, 100, 1.5f)]
    public void ReverseModelScale_BeyondTheRange_ClampsAndLeavesTheRestForTheScale(float modelScale, byte height, float scaleFactor) {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        var components = table.ReverseModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, modelScale);

        components.Height.ShouldBe(height);
        components.ScaleFactor.ShouldBe(scaleFactor, 0.0001f);
    }

    [Fact]
    public void ReverseModelScale_WithoutRange_LeavesTheDifferenceForTheScale() {
        var table = new HumanHeightTable(BuildCmp(0, 0.9f, 0.9f, 0.8f, 0.8f));

        var components = table.ReverseModelScale(NpcTribe.Midlander, NpcSex.Female, NpcBodyType.Normal, 1.6f);

        components.Height.ShouldBe((byte)100);
        components.ScaleFactor.ShouldBe(2f, 0.0001f);
    }

    [Fact]
    public void Ctor_WithTruncatedFile_Throws() {
        Should.Throw<InvalidDataException>(() => new HumanHeightTable(new byte[TableEnd - 1]));
    }
}
