using ARealmRepopulated.Data.Appearance;
using Shouldly;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class CharacterEditorClanTests {

    private static CharacterEditorClan Clan => new() {
        Menus = [new CharacterEditorMenu { Customize = CustomizeIndex.HairStyle, Options = [new(1, 0), new(2, 0)] }]
    };

    [Fact]
    public void FitOptions_ReplacesWhatNothingOfTheBodyTypeWears() {
        var appearance = new NpcAppearanceData { BodyType = NpcBodyType.Normal, HairStyle = 203 };

        Clan.ClampOptions(appearance, new CharacterBodyTypeLooks(NpcBodyType.Normal, [], [150], []));

        appearance.HairStyle.ShouldBe((byte)1);
    }

    [Fact]
    public void FitOptions_KeepsWhatOnlyNpcsOfTheBodyTypeWear() {
        var appearance = new NpcAppearanceData { BodyType = NpcBodyType.Normal, HairStyle = 150 };

        Clan.ClampOptions(appearance, new CharacterBodyTypeLooks(NpcBodyType.Normal, [], [150], []));

        appearance.HairStyle.ShouldBe((byte)150);
    }

    [Fact]
    public void FitOptions_LimitsOldAndYoungToTheLooksOfTheirNpcs() {
        var appearance = new NpcAppearanceData { BodyType = NpcBodyType.Young, HairStyle = 2 };

        Clan.ClampOptions(appearance, new CharacterBodyTypeLooks(NpcBodyType.Young, [], [7], []));

        appearance.HairStyle.ShouldBe((byte)7);
    }
}
