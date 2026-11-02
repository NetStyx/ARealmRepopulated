using ARealmRepopulated.Data.Appearance;
using Shouldly;

namespace ARealmRepopulated.Tests.Data.Appearance;

public class NpcAppearanceDataTests {

    private static NpcAppearanceData Dressed => new() {
        Face = 1,
        Body = new EquipmentModel { ModelId = 5, Stain0 = 1 },
        ExtendedAppearance = new NpcExtendedAppearance { MuscleTone = 1f }
    };

    [Fact]
    public void Copy_ComparesEqualUntilTheSourceChanges() {
        var appearance = Dressed;
        var copy = appearance.Copy();

        (appearance == copy).ShouldBeTrue();

        appearance.Face = 2;

        (appearance == copy).ShouldBeFalse();
    }

    [Fact]
    public void Equality_NoticesADyeChange() {
        var appearance = Dressed;
        var copy = appearance.Copy();

        appearance.Body = appearance.Body! with { Stain0 = 2 };

        (appearance == copy).ShouldBeFalse();
    }

    [Fact]
    public void Copy_StaysAsItWasWhenAnExactColorChanges() {
        var appearance = Dressed;
        var copy = appearance.Copy();

        appearance.ExtendedAppearance!.MuscleTone = 2f;

        copy.ExtendedAppearance!.MuscleTone.ShouldBe(1f);
        (appearance == copy).ShouldBeFalse();
    }

    // The left hand of paired fists has no variant. Read as gear it has no set, and the weapon never loads.
    [Fact]
    public void DecodeModel_WeaponWithoutVariant_IsNotReadAsGear() {
        const ulong leftFist = 0x12262;

        ItemModelData.DecodeModel(ItemSlots.OffHand, leftFist).ShouldBe(new ItemModelParts(8802, 1, 0));
        ItemModelData.DecodeModel(ItemSlots.Body, leftFist).ShouldBe(new ItemModelParts(0, 8802, 1));
    }
}
