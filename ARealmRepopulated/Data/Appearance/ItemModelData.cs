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

    public static ulong CalculateModel(ushort set, ushort modelBase, ushort modelVariant) {
        ulong result = set;
        result |= (ulong)modelBase << (set != 0 ? 16 : 0);
        result |= (ulong)modelVariant << (set != 0 ? 32 : 16);
        return result;
    }

    public static bool MatchesSlot(ItemSlots s, Item i) => s switch {
        ItemSlots.MainHand => i.EquipSlotCategory.Value.MainHand == 1,
        ItemSlots.Head => i.EquipSlotCategory.Value.Head == 1,
        ItemSlots.Body => i.EquipSlotCategory.Value.Body == 1,
        ItemSlots.Hands => i.EquipSlotCategory.Value.Gloves == 1,
        ItemSlots.Waist => i.EquipSlotCategory.Value.Waist == 1,
        ItemSlots.Legs => i.EquipSlotCategory.Value.Legs == 1,
        ItemSlots.Feet => i.EquipSlotCategory.Value.Feet == 1,
        ItemSlots.OffHand => i.EquipSlotCategory.Value.OffHand == 1,
        ItemSlots.Ears => i.EquipSlotCategory.Value.Ears == 1,
        ItemSlots.Neck => i.EquipSlotCategory.Value.Neck == 1,
        ItemSlots.Wrists => i.EquipSlotCategory.Value.Wrists == 1,
        ItemSlots.RightRing => i.EquipSlotCategory.Value.FingerR == 1,
        ItemSlots.LeftRing => i.EquipSlotCategory.Value.FingerL == 1,
        ItemSlots.Weapons => i.EquipSlotCategory.Value.MainHand == 1 || i.EquipSlotCategory.Value.OffHand == 1,
        _ => false,
    };

}
