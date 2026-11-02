using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Data.Appearance;

public enum ItemSlots {
    None = 0,
    MainHand = 1 << 0,
    OffHand = 1 << 1,
    Head = 1 << 2,
    Body = 1 << 3,
    Hands = 1 << 4,
    Waist = 1 << 5,
    Legs = 1 << 6,
    Feet = 1 << 7,
    Ears = 1 << 8,
    Neck = 1 << 9,
    Wrists = 1 << 10,
    RightRing = 1 << 11,
    LeftRing = 1 << 12,
    Glasses = 1 << 13,
    Weapons = MainHand | OffHand
}

public readonly record struct ItemModelParts(ushort ModelSet, ushort ModelBase, ushort ModelVariant) : IComparable<ItemModelParts> {

    public bool IsEmpty => ModelSet == 0 && ModelBase == 0;

    public int CompareTo(ItemModelParts other)
        => (ModelSet, ModelBase, ModelVariant).CompareTo((other.ModelSet, other.ModelBase, other.ModelVariant));
}

public class ItemModelData {

    public static readonly ItemModelData Empty = new();

    // Every code the game can put into a gear path. Builds in Human.SetupIdsAndVfxScale.
    //
    //   c0101e0085_top.mdl
    //   c------------------- Character
    //    01----------------- Race and sex. An odd number is male, the number after it female: 01 Midlander, 03 Highlander,
    //      ||                05 Elezen, 07 Miqote, 09 Roegadyn, 11 Lalafell, 13 Au Ra, 15 Hrothgar, 17 Viera. Only Hyur
    //      ||                has a pair per clan, the two clans of any other race share one. 91 to 94 are the Hyur pairs
    //      ||                with 9000 added, which Young Hyur npcs use (see NpcAppearanceService.GetSafeTailShape).
    //      01--------------- Body. 01 for 'Normal' and 'Old' alike, 04 for 'Young'. 02 and 05 belong to the body types 2 and 5,
    //        |||||           which no event npc uses and which have no skeleton in the game data, so they are only here
    //        |||||           to not miss a path.
    //        e0085---------- Gear id, with an a instead of the e on accessories (GetModelPaths)
    //             _top------ Slot (GetModelSuffix)
    //
    // The code derived here is for the first 4 digits (cXXXX). As a heads up: the game does not possess files for every combination. If it encounters
    // a code that does not exist, it will load the file of another code instead. It never looks for the file itself: each code has a table, its eqdp
    // file, that says per gear id and slot whether the code has a model of its own. Without one the game tries a single substitute:
    //   Adult: 0201 for women and 0101 for men, except Lalafell women 1101 and Hrothgar men 0901
    //   Young: 0104, except Roegadyn men 0901, Lalafell 1101 and Au Ra 1301
    // If the substitute has no model either, it ends at 0101 (CharacterUtility.GetEqpDataForAdults and GetEqpDataForChildren).
    private static readonly int[] ModelCodes = [.. Enumerable
            .Range(1, 18)
            .Concat([91, 92, 93, 94])
            .SelectMany(race => ((int[])[1, 2, 4, 5])
            .Select(body => (race * 100) + body))];

    public ulong Value { get; set; }
    public ItemSlots Slot { get; set; }
    public ushort ModelSet { get; set; }
    public ushort ModelBase { get; set; }
    public ushort ModelVariant { get; set; }

    public uint Item { get; set; }

    public static ulong CalculateModel(ushort modelSet, ushort modelBase, ushort modelVariant) {
        ulong result = modelSet;
        if (modelSet != 0) {
            result |= (ulong)modelBase << 16;
            result |= (ulong)modelVariant << 32;
        } else {
            result |= (ulong)modelBase;
            result |= (ulong)modelVariant << 16;
        }
        return result;
    }

    public static ItemModelParts DecodeModel(ItemSlots slot, ulong modelKey)
        => slot.IsWeapon() ? DecodeWeapon(modelKey) : DecodeEquipment(modelKey);

    public static ItemModelParts DecodeWeapon(ulong modelKey)
        => new((ushort)modelKey, (ushort)(modelKey >> 16), (ushort)(modelKey >> 32));

    public static ItemModelParts DecodeEquipment(ulong modelKey)
        => new(0, (ushort)modelKey, (ushort)(modelKey >> 16));

    /// <summary>
    /// The paths the game can load this model from, it builds them in Human.ResolveMdlPath and Weapon.ResolveMdlPath.
    /// </summary>
    public static IEnumerable<string> GetModelPaths(ItemSlots slot, ItemModelParts parts) {
        if (slot.IsWeapon())
            return [$"chara/weapon/w{parts.ModelSet:D4}/obj/body/b{parts.ModelBase:D4}/model/w{parts.ModelSet:D4}b{parts.ModelBase:D4}.mdl"];

        var id = parts.ModelBase;
        var suffix = GetModelSuffix(slot);
        return ModelCodes.Select(code => slot.IsAccessory()
            ? $"chara/accessory/a{id:D4}/model/c{code:D4}a{id:D4}_{suffix}.mdl"
            : $"chara/equipment/e{id:D4}/model/c{code:D4}e{id:D4}_{suffix}.mdl");
    }

    private static string GetModelSuffix(ItemSlots slot) => slot switch {
        ItemSlots.Head => "met",
        ItemSlots.Body => "top",
        ItemSlots.Hands => "glv",
        ItemSlots.Legs => "dwn",
        ItemSlots.Feet => "sho",
        ItemSlots.Ears => "ear",
        ItemSlots.Neck => "nek",
        ItemSlots.Wrists => "wrs",
        ItemSlots.RightRing => "rir",
        ItemSlots.LeftRing => "ril",
        _ => string.Empty
    };

    private static bool IsInCategory(Item i, Func<dynamic, int> categorySelector)
        => categorySelector(i.EquipSlotCategory.Value) == 1;

    public static bool Slottable(ItemSlots s, Item i) => s switch {
        ItemSlots.MainHand => IsInCategory(i, c => c.MainHand),
        ItemSlots.Head => IsInCategory(i, c => c.Head),
        ItemSlots.Body => IsInCategory(i, c => c.Body),
        ItemSlots.Hands => IsInCategory(i, c => c.Gloves),
        ItemSlots.Waist => IsInCategory(i, c => c.Waist),
        ItemSlots.Legs => IsInCategory(i, c => c.Legs),
        ItemSlots.Feet => IsInCategory(i, c => c.Feet),
        ItemSlots.OffHand => IsInCategory(i, c => c.OffHand),
        ItemSlots.Ears => IsInCategory(i, c => c.Ears),
        ItemSlots.Neck => IsInCategory(i, c => c.Neck),
        ItemSlots.Wrists => IsInCategory(i, c => c.Wrists),
        ItemSlots.RightRing => IsInCategory(i, c => c.FingerR),
        ItemSlots.LeftRing => IsInCategory(i, c => c.FingerL),
        ItemSlots.Weapons => IsInCategory(i, c => c.MainHand) || IsInCategory(i, c => c.OffHand),
        _ => false,
    };
}

public static class LumiaItemExtension {
    public static bool IsSlottableAs(this Item item, ItemSlots slot)
        => ItemModelData.Slottable(slot, item);
}

public static class ItemSlotExtensions {
    public static bool IsWeapon(this ItemSlots slot)
        => slot is ItemSlots.MainHand or ItemSlots.OffHand or ItemSlots.Weapons;

    public static bool IsAccessory(this ItemSlots slot)
        => slot is ItemSlots.Ears or ItemSlots.Neck or ItemSlots.Wrists or ItemSlots.RightRing or ItemSlots.LeftRing;
}
