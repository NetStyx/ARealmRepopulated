using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// Everything related to retrieval of items or equip 
/// </summary>
public class ArrpItemData(IPluginLog log, IDataManager dataManager) {
    private readonly ExcelSheet<Item> _itemSheet = dataManager.GetExcelSheet<Item>();
    private readonly ExcelSheet<Glasses> _glassesSheet = dataManager.GetExcelSheet<Glasses>();
    private readonly ExcelSheet<Stain> _stainSheet = dataManager.GetExcelSheet<Stain>();
    private readonly ExcelSheet<ENpcBase> _enpcBaseSheet = dataManager.GetExcelSheet<ENpcBase>();
    private readonly ExcelSheet<NpcEquip> _npcEquipSheet = dataManager.GetExcelSheet<NpcEquip>();
    private readonly List<ItemModelData> _itemModelData = [];
    private readonly Dictionary<ItemSlots, List<Item>> _equippableItems = [];
    private readonly Dictionary<ItemSlots, List<ulong>> _npcOnlyModels = [];
    private List<Stain>? _stains;

    public Item? GetItem(uint itemID)
        => _itemSheet.GetRowOrDefault(itemID);

    public Glasses? GetGlasses(uint glassesId)
        => _glassesSheet.GetRowOrDefault(glassesId);

    public List<Glasses> GetNamedGlasses()
        => [.. _glassesSheet.Where(g => g.RowId != 0 && !g.Name.IsEmpty)];

    public List<Item> GetEquippableItems(ItemSlots slot) {
        if (!_equippableItems.TryGetValue(slot, out var items)) {
            _equippableItems[slot] = items = [.. _itemSheet.Where(i => i.ModelMain != 0 && !i.Name.IsEmpty && i.IsSlottableAs(slot))];
        }
        return items;
    }

    /// <summary>
    /// Equip models npcs wear which have neither icon nor name.
    /// </summary>
    public List<ulong> GetNpcOnlyModels(ItemSlots slot) {
        if (!_npcOnlyModels.TryGetValue(slot, out var models)) {
            var itemModels = GetEquippableItems(GetItemSearchSlot(slot))
                .SelectMany<Item, ulong>(i => [i.ModelMain, i.ModelSub])
                .ToHashSet();

            _npcOnlyModels[slot] = models = [.. _npcEquipSheet.Select(n => GetSlotModel(n, slot))
                .Concat(_enpcBaseSheet.Select(n => GetSlotModel(n, slot)))
                .Where(m => !itemModels.Contains(m))
                .Distinct()
                .Where(m => HasModel(slot, m))
                .OrderBy(m => ItemModelData.DecodeModel(slot, m))];
        }
        return models;
    }

    // The game loads whatever a key says, and surprise, many keys in the sheets lead nowhere: id 1 has a body and legs but no head or hands, 
    // and uint.MaxValue is how they clear a slot. See /doc/research/character-editor.md.
    private bool HasModel(ItemSlots slot, ulong model) {
        var parts = ItemModelData.DecodeModel(slot, model);
        return !parts.IsEmpty && ItemModelData.GetModelPaths(slot, parts).Any(dataManager.FileExists);
    }

    private static ulong GetSlotModel(NpcEquip equip, ItemSlots slot) => slot switch {
        ItemSlots.MainHand => equip.ModelMainHand,
        ItemSlots.OffHand => equip.ModelOffHand,
        ItemSlots.Head => equip.ModelHead,
        ItemSlots.Body => equip.ModelBody,
        ItemSlots.Hands => equip.ModelHands,
        ItemSlots.Legs => equip.ModelLegs,
        ItemSlots.Feet => equip.ModelFeet,
        ItemSlots.Ears => equip.ModelEars,
        ItemSlots.Neck => equip.ModelNeck,
        ItemSlots.Wrists => equip.ModelWrists,
        ItemSlots.LeftRing => equip.ModelLeftRing,
        ItemSlots.RightRing => equip.ModelRightRing,
        _ => 0
    };

    private static ulong GetSlotModel(ENpcBase npc, ItemSlots slot) => slot switch {
        ItemSlots.MainHand => npc.ModelMainHand,
        ItemSlots.OffHand => npc.ModelOffHand,
        ItemSlots.Head => npc.ModelHead,
        ItemSlots.Body => npc.ModelBody,
        ItemSlots.Hands => npc.ModelHands,
        ItemSlots.Legs => npc.ModelLegs,
        ItemSlots.Feet => npc.ModelFeet,
        ItemSlots.Ears => npc.ModelEars,
        ItemSlots.Neck => npc.ModelNeck,
        ItemSlots.Wrists => npc.ModelWrists,
        ItemSlots.LeftRing => npc.ModelLeftRing,
        ItemSlots.RightRing => npc.ModelRightRing,
        _ => 0
    };

    private static ItemSlots GetItemSearchSlot(ItemSlots slot)
        => slot.IsWeapon() ? ItemSlots.Weapons : slot;

    // Returns the list of stains in the dye window (AgentColorant), sorted by shade and sub order. Row 0 is its "No Color" cell. 
    // See /doc/research/character-editor.md.
    public List<Stain> GetStains()
        => _stains ??= [.. _stainSheet
            .Where(s => s.RowId != 0 && s.SubOrder != 0)
            .OrderBy(s => s.Shade)
            .ThenBy(s => s.SubOrder)];

    public Stain? GetStain(uint stainId)
        => _stainSheet.GetRowOrDefault(stainId);

    public ItemModelData GetItemByModel(ItemSlots slot, ushort set, ushort baseValue, ushort variant) {

        if (new ItemModelParts(set, baseValue, variant).IsEmpty)
            return ItemModelData.Empty;

        var model = ItemModelData.CalculateModel(set, baseValue, variant);

        var modelCache = _itemModelData.FirstOrDefault(x => x.Slot == slot && x.Value == model);
        if (modelCache == null) {

            var searchSlot = GetItemSearchSlot(slot);

            var itemModel = _itemSheet.FirstOrNull(i => i.ModelMain == model && i.IsSlottableAs(searchSlot));
            itemModel ??= _itemSheet.FirstOrNull(i => i.ModelSub == model && i.IsSlottableAs(searchSlot));

            _itemModelData.Add(modelCache = new ItemModelData { Value = model, Slot = slot, ModelSet = set, ModelBase = baseValue, ModelVariant = variant, Item = itemModel?.RowId ?? 0 });

            log.Debug($"Adding model cache entry: Slot {slot} / Item {modelCache.Item} / {model} : {set} - {baseValue} - {variant}");
        }

        return modelCache;
    }
}
