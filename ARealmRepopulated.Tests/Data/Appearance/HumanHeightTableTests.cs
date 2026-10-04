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

    [Fact]
    public void GetModelScale_WithHeightInRange_InterpolatesBetweenMinAndMax() {
        // Highlander is the second clan of the first race: entry 1
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        table.GetModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, 0).ShouldBe(1.0f);
        table.GetModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, 50).ShouldBe(1.1f, 0.0001f);
        table.GetModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, 100).ShouldBe(1.2f, 0.0001f);
    }

    [Fact]
    public void GetModelScale_ForMale_ReadsTheMalePair() {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        table.GetModelScale(NpcTribe.Highlander, NpcSex.Male, NpcBodyType.Normal, 100).ShouldBe(0.6f, 0.0001f);
    }

    [Fact]
    public void GetModelScale_WithMaxHeight_IsOne() {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        table.GetModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, 255).ShouldBe(1f);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(254)]
    public void GetModelScale_WithHeightAboveHundred_IsTheMinimum(byte height) {
        var table = new HumanHeightTable(BuildCmp(1, 0.5f, 0.6f, 1.0f, 1.2f));

        table.GetModelScale(NpcTribe.Highlander, NpcSex.Female, NpcBodyType.Normal, height).ShouldBe(1.0f);
    }

    [Fact]
    public void GetModelScale_TakesRaceAndClanFromTheTribe() {
        // Xaela: sixth race, second clan, young body type -> 5 * 10 + (4 - 1) * 2 + 1
        var table = new HumanHeightTable(BuildCmp(57, 0.5f, 0.5f, 0.7f, 0.7f));

        table.GetModelScale(NpcTribe.Xaela, NpcSex.Female, NpcBodyType.Young, 50).ShouldBe(0.7f);
    }

    [Fact]
    public void GetModelScale_WithUnknownBodyType_UsesNormal() {
        var table = new HumanHeightTable(BuildCmp(0, 0.9f, 0.9f, 0.8f, 0.8f));

        table.GetModelScale(NpcTribe.Midlander, NpcSex.Female, NpcBodyType.Unknown, 50).ShouldBe(0.8f);
    }

    [Fact]
    public void Ctor_WithTruncatedFile_Throws() {
        Should.Throw<InvalidDataException>(() => new HumanHeightTable(new byte[TableEnd - 1]));
    }
}
