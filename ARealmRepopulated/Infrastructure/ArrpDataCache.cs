using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;

namespace ARealmRepopulated.Infrastructure;

public class ArrpDataCache(IPluginLog log, IDataManager dataManager) {
    private ExcelSheet<TerritoryType> _territoryTypeSheet = null!;
    private ExcelSheet<Emote> _emoteTypeSheet = null!;
    private ExcelSheet<Item> _itemSheet = null!;
    private CharacterEditorData _characterEditorData = null!;
    private readonly List<ItemModelData> _itemModelData = [];

    public void Populate() {
        _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
        _emoteTypeSheet = dataManager.GetExcelSheet<Emote>();
        _itemSheet = dataManager.GetExcelSheet<Item>();

        log.Debug("Creating character editor structure");
        _characterEditorData = new CharacterEditorData();
        var _charaMakeSheet = dataManager.GetExcelSheet<CharaMakeType>();
        foreach (var charaRow in _charaMakeSheet) {

            var race = (NpcRace)charaRow.Race.Value.RowId;
            var tribe = (NpcTribe)charaRow.Tribe.Value.RowId;
            var gender = (NpcSex)charaRow.Gender;

            var raceData = _characterEditorData.Races.FirstOrDefault(x => x.Race == race && x.Tribe == tribe && x.Gender == gender);
            if (raceData == null) {
                _characterEditorData.Races.Add(raceData = new CharacterEditorRace { Race = race, Tribe = tribe, Gender = gender });
            }

            var hasBustSize = charaRow.CharaMakeStruct.FirstOrNull(x => x.Customize == (uint)CustomizeIndex.BustSize);
            var hasMuscleMass = charaRow.CharaMakeStruct.FirstOrNull(x => x.Customize == (uint)CustomizeIndex.MuscleMass);
            var hasTailEarShapes = charaRow.CharaMakeStruct.FirstOrNull(x => x.Customize == (uint)CustomizeIndex.TailShape);
            var hasFaces = charaRow.CharaMakeStruct.FirstOrNull(x => x.Customize == (uint)CustomizeIndex.Face);

            raceData.HasLipstick = race != NpcRace.Hrothgar;
            raceData.HasMuscleMass = hasMuscleMass != null;
            raceData.HasTailEarShapes = hasTailEarShapes != null;

        }
    }

    public List<Item> GetItems(Predicate<Item> a)
        => [.. _itemSheet.Where(i => a(i))];

    public Item? GetItem(uint itemID)
        => _itemSheet.GetRowOrDefault(itemID);

    public ItemModelData GetItemByModel(ItemSlots slot, ushort set, ushort baseValue, ushort variant) {

        if (baseValue < 2)
            return ItemModelData.Empty;

        var model = ItemModelData.CalculateModel(set, baseValue, variant);

        var modelCache = _itemModelData.FirstOrDefault(x => x.Slot == slot && x.Value == model);
        if (modelCache == null) {

            var searchSlot = slot;
            if (searchSlot == ItemSlots.MainHand || searchSlot == ItemSlots.OffHand)
                searchSlot = ItemSlots.Weapons;

            var itemModel = _itemSheet.FirstOrNull(i => ItemModelData.MatchesSlot(searchSlot, i) && i.ModelMain == model);
            itemModel ??= _itemSheet.FirstOrNull(i => ItemModelData.MatchesSlot(searchSlot, i) && i.ModelSub == model);

            _itemModelData.Add(modelCache = new ItemModelData { Value = model, Slot = slot, ModelSet = set, ModelBase = baseValue, ModelVariant = variant, Item = itemModel?.RowId ?? 0 });

            log.Debug($"Adding model cache entry: Slot {slot} / Item {modelCache.Item} / {model} : {set} - {baseValue} - {variant}");
        }

        return modelCache;
    }

    public Emote GetEmote(ushort emoteId)
        => _emoteTypeSheet.GetRow(emoteId);

    public List<Emote> GetEmotes()
        => [.. _emoteTypeSheet];

    public TerritoryType GetTerritoryType(ushort territoryTypeId)
        => _territoryTypeSheet.GetRow(territoryTypeId);

    public CharacterEditorData GetCharacterEditorData()
        => _characterEditorData;

}
