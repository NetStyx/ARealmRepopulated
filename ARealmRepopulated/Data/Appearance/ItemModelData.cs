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

public class ItemModelData {

    public static readonly ItemModelData Empty = new();

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
