using ARealmRepopulated.Data.Appearance;
using Shouldly;
using System.Linq;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class AppearanceRollerTests {

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
    public void Roll_OnlyMixesHairFaceAndBodyWithinTheRolledClan() {
        for (var i = 0; i < 200; i++) {
            var rolled = AppearanceRoller.Roll(new NpcAppearanceData(), People, People, AppearanceRollLocks.None)!;

            var clan = People.Where(p => p.Tribe == rolled.Tribe && p.Sex == rolled.Sex).ToList();
            clan.ShouldContain(p => p.HairStyle == rolled.HairStyle);
            clan.ShouldContain(p => p.Face == rolled.Face);
            clan.ShouldContain(p => p.SkinColor == rolled.SkinColor);
            People.Where(p => p.Sex == rolled.Sex).ShouldContain(p => p.Body!.ModelId == rolled.Body!.ModelId);
        }
    }

    [Theory]
    [InlineData(AppearanceRollLocks.Clan)]
    [InlineData(AppearanceRollLocks.Hair)]
    [InlineData(AppearanceRollLocks.Face)]
    [InlineData(AppearanceRollLocks.Body)]
    public void Roll_WithAClanBoundLock_StaysInTheClan(AppearanceRollLocks lockedGroup) {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);

        for (var i = 0; i < 50; i++) {
            var rolled = AppearanceRoller.Roll(current, People, People, lockedGroup)!;

            rolled.Tribe.ShouldBe(NpcTribe.Highlander);
            rolled.Sex.ShouldBe(NpcSex.Male);
        }
    }

    [Fact]
    public void Roll_KeepsTheLockedGroups() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);

        var rolled = AppearanceRoller.Roll(current, People, People, AppearanceRollLocks.Hair | AppearanceRollLocks.Outfit)!;

        rolled.HairStyle.ShouldBe((byte)9);
        rolled.Body!.ModelId.ShouldBe((ushort)9);
        rolled.Face.ShouldBe((byte)5);
    }

    [Fact]
    public void Roll_DropsOnlyTheExactColorsOfRolledGroups() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);
        current.ExtendedAppearance = new NpcExtendedAppearance { HairColor = new(1, 0, 0), SkinColor = new(0, 1, 0) };

        var rolled = AppearanceRoller.Roll(current, People, People, AppearanceRollLocks.Hair)!;

        rolled.ExtendedAppearance!.HairColor.ShouldNotBeNull();
        rolled.ExtendedAppearance.SkinColor.ShouldBeNull();
    }

    [Fact]
    public void Roll_AlwaysEndsWithANormalBody() {
        var current = Person(NpcTribe.Highlander, NpcSex.Male, 9);
        current.BodyType = NpcBodyType.Young;

        AppearanceRoller.Roll(current, People, People, AppearanceRollLocks.Clan)!.BodyType.ShouldBe(NpcBodyType.Normal);
    }

    [Fact]
    public void Roll_WithoutAnyoneOfTheLockedClan_ReturnsNull() {
        var current = Person(NpcTribe.Veena, NpcSex.Male, 9);

        AppearanceRoller.Roll(current, People, People, AppearanceRollLocks.Clan).ShouldBeNull();
    }

    [Fact]
    public void Roll_TakesClanAndOutfitFromThePeopleAndTheLookFromEveryone() {
        var local = Person(NpcTribe.Midlander, NpcSex.Female, 1);
        var relative = Person(NpcTribe.Midlander, NpcSex.Female, 2);

        var rolled = AppearanceRoller.Roll(new NpcAppearanceData(), [local], [relative], AppearanceRollLocks.None)!;

        rolled.Tribe.ShouldBe(NpcTribe.Midlander);
        rolled.Body!.ModelId.ShouldBe((ushort)1);
        rolled.Face.ShouldBe((byte)2);
    }

    [Fact]
    public void Roll_WithALockedClanThatLivesElsewhere_KeepsTheLocalOutfit() {
        var current = Person(NpcTribe.Veena, NpcSex.Male, 9);
        var relative = Person(NpcTribe.Veena, NpcSex.Male, 7);

        var rolled = AppearanceRoller.Roll(current, People, [relative], AppearanceRollLocks.Clan)!;

        rolled.Face.ShouldBe((byte)7);
        rolled.Body!.ModelId.ShouldBeOneOf((ushort)3, (ushort)5);
    }

    [Fact]
    public void Roll_WithNobodyOfTheGenderAmongThePeople_TakesTheOutfitFromTheClan() {
        var current = Person(NpcTribe.Midlander, NpcSex.Female, 9);
        var relative = Person(NpcTribe.Midlander, NpcSex.Female, 7);

        var rolled = AppearanceRoller.Roll(current, [Person(NpcTribe.Midlander, NpcSex.Male, 3)], [relative], AppearanceRollLocks.Clan)!;

        rolled.Body!.ModelId.ShouldBe((ushort)7);
    }

}
