using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Appearance.Parser;
using FFXIVClientStructs.FFXIV.Common.Math;
using Shouldly;
using System.Text.Json;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class NpcExtendedAppearanceTests {

    public static TheoryData<string> AllSerializerOptions => ["appearance", "scenario"];

    private static JsonSerializerOptions OptionsNamed(string name)
        => name == "scenario" ? ScenarioFileManager.ScenarioLoadSerializerOptions : NpcAppearanceData.SerializerOptions;

    private static NpcExtendedAppearance Sample => new() {
        SkinColor = new Vector3(0.25f, 0.5f, 0.75f),
        MuscleTone = 1.25f,
        MouthColor = new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
        HairColor = new Vector3(0.6f, 0.7f, 0.8f),
        HairHighlight = new Vector3(0.9f, 0.95f, 1.0f),
        LeftEyeColor = new Vector3(2.5f, 3.5f, 4.5f),
        RightEyeColor = new Vector3(0.05f, 0.06f, 0.07f),
        FeatureColor = new Vector3(0.01f, 0.02f, 0.03f),
    };

    [Theory]
    [MemberData(nameof(AllSerializerOptions))]
    public void Serialize_WritesTheVectorComponents(string optionsName) {
        var json = JsonSerializer.Serialize(Sample, OptionsNamed(optionsName));

        json.ShouldNotContain("\"SkinColor\":{}");
        json.ShouldContain("\"SkinColor\":{\"X\":0.25,\"Y\":0.5,\"Z\":0.75}");
    }

    [Theory]
    [MemberData(nameof(AllSerializerOptions))]
    public void RoundTrip_KeepsEveryColour(string optionsName) {
        var options = OptionsNamed(optionsName);
        var original = Sample;

        var back = JsonSerializer.Deserialize<NpcExtendedAppearance>(JsonSerializer.Serialize(original, options), options);

        back.ShouldNotBeNull();
        back.SkinColor.ShouldBe(original.SkinColor);
        back.MuscleTone.ShouldBe(original.MuscleTone);
        back.MouthColor.ShouldBe(original.MouthColor);
        back.HairColor.ShouldBe(original.HairColor);
        back.HairHighlight.ShouldBe(original.HairHighlight);
        back.LeftEyeColor.ShouldBe(original.LeftEyeColor);
        back.RightEyeColor.ShouldBe(original.RightEyeColor);
        back.FeatureColor.ShouldBe(original.FeatureColor);
    }

    [Fact]
    public void RoundTrip_KeepsUnsetColoursUnset() {
        var original = new NpcExtendedAppearance { MuscleTone = 1f };
        var options = NpcAppearanceData.SerializerOptions;

        var back = JsonSerializer.Deserialize<NpcExtendedAppearance>(JsonSerializer.Serialize(original, options), options);

        back.ShouldNotBeNull();
        back.SkinColor.ShouldBeNull();
        back.MouthColor.ShouldBeNull();
        back.MuscleTone.ShouldBe(1f);
    }

    [Theory]
    [MemberData(nameof(AllSerializerOptions))]
    public void RoundTrip_FromACharacterFile_KeepsTheColoursAndTheModelScale(string optionsName) {
        // the whole path a design travels: character file -> appearance -> scenario file -> appearance
        var options = OptionsNamed(optionsName);
        var imported = CharaFileReader.Read(TestHelper.ReadEmbeddedResource("extended-appearance.chara"));

        var back = JsonSerializer.Deserialize<NpcAppearanceData>(JsonSerializer.Serialize(imported, options), options);

        back.ShouldNotBeNull();
        back.HeightMultiplier.ShouldBe(1.02f);
        back.ExtendedAppearance.ShouldNotBeNull();
        back.ExtendedAppearance.SkinColor.ShouldBe(new Vector3(0.25f, 0.5f, 0.75f));
        back.ExtendedAppearance.HairColor.ShouldBe(new Vector3(0.6f, 0.7f, 0.8f));
    }
}
