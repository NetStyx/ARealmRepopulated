using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Appearance.Parser;
using Shouldly;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class CharaFileReaderTests {

    private const string MinimalChara = """
        { "Race": "Hyur", "Tribe": "Midlander", "Gender": "Feminine" }
        """;

    private static NpcAppearanceData ReadFixture()
        => CharaFileReader.Read(TestHelper.ReadEmbeddedResource("extended-appearance.chara"));

    [Fact]
    public void Read_WithCompleteFile_ReadsCustomizeValues() {
        var appearance = ReadFixture();

        appearance.Race.ShouldBe(NpcRace.Miqote);
        appearance.Tribe.ShouldBe(NpcTribe.KeeperOfTheMoon);
        appearance.Sex.ShouldBe(NpcSex.Female);
        appearance.BodyType.ShouldBe(NpcBodyType.Normal);
        appearance.Face.ShouldBe((byte)3);
        appearance.HairStyle.ShouldBe((byte)61);
        appearance.Highlights.ShouldBe((byte)128);
        appearance.HighlightsColor.ShouldBe((byte)104);
    }

    [Fact]
    public void Read_WithExtendedAppearance_ReadsTheShaderColours() {
        var extended = ReadFixture().ExtendedAppearance;

        extended.ShouldNotBeNull();
        extended.SkinColor.ShouldBe(new Vector3(0.25f, 0.5f, 0.75f));
        extended.MuscleTone.ShouldBe(1.25f);
        extended.MouthColor.ShouldBe(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        extended.HairColor.ShouldBe(new Vector3(0.6f, 0.7f, 0.8f));
        extended.HairHighlight.ShouldBe(new Vector3(0.9f, 0.95f, 1.0f));
        extended.RightEyeColor.ShouldBe(new Vector3(0.05f, 0.06f, 0.07f));
        extended.FeatureColor.ShouldBe(new Vector3(0.01f, 0.02f, 0.03f));
    }

    [Fact]
    public void Read_WithHeightMultiplier_ReadsTheModelScale() {
        // separate from the Height customize value, which the fixture sets to 42
        var appearance = ReadFixture();

        appearance.Height.ShouldBe((byte)42);
        appearance.HeightMultiplier.ShouldBe(1.02f);
    }

    [Fact]
    public void Read_WithOnlyHeightMultiplier_LeavesTheColourBlockUnset() {
        var appearance = CharaFileReader.Read("""
            { "Race": "Hyur", "Tribe": "Midlander", "Gender": "Feminine", "HeightMultiplier": 1.15 }
            """);

        appearance.HeightMultiplier.ShouldBe(1.15f);
        appearance.ExtendedAppearance.ShouldBeNull();
    }

    [Fact]
    public void Read_WithShaderColourAboveOne_KeepsItUnclamped() {
        // these are raw shader values, not a 0-1 colour picker - glowing eyes legitimately exceed 1
        var extended = ReadFixture().ExtendedAppearance;

        extended.ShouldNotBeNull();
        extended.LeftEyeColor.ShouldBe(new Vector3(2.5f, 3.5f, 4.5f));
    }

    [Fact]
    public void Read_WithoutExtendedAppearance_LeavesTheBlockUnset() {
        var appearance = CharaFileReader.Read(MinimalChara);

        appearance.ExtendedAppearance.ShouldBeNull();
    }

    [Fact]
    public void Read_WithFractionalTransparency_ReadsTheWholeFile() {
        // reading this as an integer used to throw and cost the entire import
        var appearance = ReadFixture();

        appearance.Transparency.ShouldBe(0.5f);
        appearance.Race.ShouldBe(NpcRace.Miqote);
    }

    [Fact]
    public void Read_WithGlassesId_ReadsTheNestedId() {
        var appearance = ReadFixture();

        appearance.Glasses.ShouldBe((ushort)3);
    }

    [Fact]
    public void Read_WithoutGlasses_LeavesGlassesUnset() {
        var appearance = CharaFileReader.Read(MinimalChara);

        appearance.Glasses.ShouldBeNull();
    }

    [Fact]
    public void Read_WithMissingOptionalEntries_ReadsWhatIsThere() {
        // one absent entry used to fail the whole file instead of just going unset
        var appearance = CharaFileReader.Read("""
            { "Race": "AuRa", "Tribe": "Xaela", "Gender": "Masculine", "Head": 7 }
            """);

        appearance.Race.ShouldBe(NpcRace.AuRa);
        appearance.Sex.ShouldBe(NpcSex.Male);
        appearance.Face.ShouldBe((byte)7);
        appearance.HairStyle.ShouldBeNull();
        appearance.Transparency.ShouldBeNull();
        appearance.Body.ShouldBeNull();
    }

    [Fact]
    public void Read_WithMissingRace_ThrowsNamingTheEntry() {
        var ex = Should.Throw<InvalidDataException>(()
            => CharaFileReader.Read("""{ "Tribe": "Midlander", "Gender": "Feminine" }"""));

        ex.Message.ShouldContain("Race");
    }

    [Fact]
    public void Read_WithUnknownGender_ThrowsNamingTheEntry() {
        var ex = Should.Throw<InvalidDataException>(()
            => CharaFileReader.Read("""{ "Race": "Hyur", "Tribe": "Midlander", "Gender": "Ambiguous" }"""));

        ex.Message.ShouldContain("Gender");
    }

    [Fact]
    public void Read_WithUnreadableJson_ThrowsInvalidData() {
        Should.Throw<InvalidDataException>(() => CharaFileReader.Read("not json at all"));
    }

    [Fact]
    public void Read_WithFacialFeatures_SetsTheBitmask() {
        var appearance = ReadFixture();

        // First | Third | LegacyTattoo
        appearance.FacialFeatures.ShouldBe((byte)(1 | 4 | 128));
    }

    [Fact]
    public void Read_WithCommaDecimalCulture_StillReadsShaderColours() {
        // the colours are stored as text with a decimal point; parsing them under a culture that uses
        // a comma as the decimal separator would otherwise read 0.25 as 25
        var previous = Thread.CurrentThread.CurrentCulture;
        try {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var extended = ReadFixture().ExtendedAppearance;

            extended.ShouldNotBeNull();
            extended.SkinColor.ShouldBe(new Vector3(0.25f, 0.5f, 0.75f));
            extended.MuscleTone.ShouldBe(1.25f);
        } finally {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Read_WithEquipment_ReadsModelAndDye() {
        var appearance = ReadFixture();

        appearance.Body.ShouldNotBeNull();
        appearance.Body.ModelId.ShouldBe((ushort)200);
        appearance.Body.Variant.ShouldBe((byte)3);
        appearance.Body.Stain0.ShouldBe((byte)44);

        appearance.MainHand.ShouldNotBeNull();
        appearance.MainHand.ModelSetId.ShouldBe((ushort)301);
        appearance.MainHand.Stain0.ShouldBe((byte)12);
    }
}
