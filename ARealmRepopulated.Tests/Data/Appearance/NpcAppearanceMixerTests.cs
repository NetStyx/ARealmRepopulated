using ARealmRepopulated.Data.Appearance;
using Shouldly;
using System.Linq;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class NpcAppearanceMixerTests {

    private static NpcAppearanceData Person(NpcTribe tribe, NpcSex sex, byte marker)
        => new() {
            Race = NpcRace.Hyur,
            Tribe = tribe,
            Sex = sex,
            BodyType = NpcBodyType.Normal,
            HairStyle = marker,
            Face = marker,
            SkinColor = marker,
            Body = new EquipmentModel { ModelId = marker }
        };

    private static readonly NpcAppearanceData[] People = [
        Person(NpcTribe.Midlander, NpcSex.Female, 1),
        Person(NpcTribe.Midlander, NpcSex.Female, 2),
        Person(NpcTribe.Midlander, NpcSex.Male, 3),
        Person(NpcTribe.Highlander, NpcSex.Female, 4),
        Person(NpcTribe.Highlander, NpcSex.Male, 5)
    ];

    [Fact]
    public void Mix_OnlyMixesHairFaceAndBodyWithinTheRolledClan() {
        for (var i = 0; i < 200; i++) {
            var mixed = NpcAppearanceMixer.Mix(new NpcAppearanceData(), People, People, AppearanceMixLocks.None)!;

            var clan = People.Where(p => p.Tribe == mixed.Tribe && p.Sex == mixed.Sex).ToList();
            clan.ShouldContain(p => p.HairStyle == mixed.HairStyle);
            clan.ShouldContain(p => p.Face == mixed.Face);
            clan.ShouldContain(p => p.SkinColor == mixed.SkinColor);
            People.Where(p => p.Sex == mixed.Sex).ShouldContain(p => p.Body!.ModelId == mixed.Body!.ModelId);
        }
    }

    [Theory]
    [InlineData(AppearanceMixLocks.Clan)]
    [InlineData(AppearanceMixLocks.Hair)]
    [InlineData(AppearanceMixLocks.Face)]
    [InlineData(AppearanceMixLocks.Body)]
    public void Mix_WithAClanBoundLock_StaysInTheClan(AppearanceMixLocks lockedGroup) {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);

        for (var i = 0; i < 50; i++) {
            var mixed = NpcAppearanceMixer.Mix(current, People, People, lockedGroup)!;

            mixed.Tribe.ShouldBe(NpcTribe.Highlander);
            mixed.Sex.ShouldBe(NpcSex.Male);
        }
    }

    [Fact]
    public void Mix_KeepsTheLockedGroups() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);

        var mixed = NpcAppearanceMixer.Mix(current, People, People, AppearanceMixLocks.Hair | AppearanceMixLocks.Outfit)!;

        mixed.HairStyle.ShouldBe((byte)9);
        mixed.Body!.ModelId.ShouldBe((ushort)9);
        mixed.Face.ShouldBe((byte)5);
    }

    [Fact]
    public void Mix_DropsOnlyTheExactColorsOfRolledGroups() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);
        current.ExtendedAppearance = new NpcExtendedAppearance { HairColor = new(1, 0, 0), SkinColor = new(0, 1, 0) };

        var mixed = NpcAppearanceMixer.Mix(current, People, People, AppearanceMixLocks.Hair)!;

        mixed.ExtendedAppearance!.HairColor.ShouldNotBeNull();
        mixed.ExtendedAppearance.SkinColor.ShouldBeNull();
    }

    [Fact]
    public void Mix_AlwaysEndsWithANormalBody() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);
        current.BodyType = NpcBodyType.Young;

        NpcAppearanceMixer.Mix(current, People, People, AppearanceMixLocks.Clan)!.BodyType.ShouldBe(NpcBodyType.Normal);
    }

    [Fact]
    public void Mix_WithoutAnyoneOfTheLockedClan_ReturnsNull() {
        var current = Person(NpcTribe.Veena, NpcSex.Male, 9);

        NpcAppearanceMixer.Mix(current, People, People, AppearanceMixLocks.Clan).ShouldBeNull();
    }

    [Fact]
    public void Mix_TakesClanAndOutfitFromThePeopleAndTheLookFromEveryone() {
        var local = Person(NpcTribe.Midlander, NpcSex.Female, 1);
        var relative = Person(NpcTribe.Midlander, NpcSex.Female, 2);

        var mixed = NpcAppearanceMixer.Mix(new NpcAppearanceData(), [local], [relative], AppearanceMixLocks.None)!;

        mixed.Tribe.ShouldBe(NpcTribe.Midlander);
        mixed.Body!.ModelId.ShouldBe((ushort)1);
        mixed.Face.ShouldBe((byte)2);
    }

    [Fact]
    public void Mix_WithALockedClanThatLivesElsewhere_KeepsTheLocalOutfit() {
        var current = Person(NpcTribe.Veena, NpcSex.Male, 9);
        var relative = Person(NpcTribe.Veena, NpcSex.Male, 7);

        var mixed = NpcAppearanceMixer.Mix(current, People, [relative], AppearanceMixLocks.Clan)!;

        mixed.Face.ShouldBe((byte)7);
        mixed.Body!.ModelId.ShouldBeOneOf((ushort)3, (ushort)5);
    }

    [Fact]
    public void Mix_WithNobodyOfTheGenderAmongThePeople_TakesTheOutfitFromTheClan() {
        var current = Person(NpcTribe.Midlander, NpcSex.Female, 9);
        var relative = Person(NpcTribe.Midlander, NpcSex.Female, 7);

        var mixed = NpcAppearanceMixer.Mix(current, [Person(NpcTribe.Midlander, NpcSex.Male, 3)], [relative], AppearanceMixLocks.Clan)!;

        mixed.Body!.ModelId.ShouldBe((ushort)7);
    }

}
