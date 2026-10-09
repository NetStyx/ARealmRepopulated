using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Appearance;
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
}
