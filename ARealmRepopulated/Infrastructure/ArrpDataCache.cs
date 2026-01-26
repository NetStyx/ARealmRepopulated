using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Lumina.Text.ReadOnly;

namespace ARealmRepopulated.Infrastructure;

public class ArrpDataCache(IPluginLog log, IDataManager dataManager) {
    private ExcelSheet<TerritoryType> _territoryTypeSheet = null!;
    private ExcelSheet<Emote> _emoteTypeSheet = null!;
    private ExcelSheet<Item> _itemSheet = null!;
    private readonly List<ItemModelData> _itemModelData = [];

    public void Populate() {
        _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
        _emoteTypeSheet = dataManager.GetExcelSheet<Emote>();
        _itemSheet = dataManager.GetExcelSheet<Item>();
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

            var itemModel = _itemSheet.FirstOrNull(i => i.IsSlottableAs(searchSlot) && i.ModelMain == model);
            itemModel ??= _itemSheet.FirstOrNull(i => i.IsSlottableAs(searchSlot) && i.ModelSub == model);

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

}

public class ArrpCharacterCreationData(IPluginLog log, IDataManager dataManager) {

    private CharacterEditorData _characterEditorData = null!;
    private ExcelSheet<CharaMakeType> _charaMakeSheet = null!;
    private ExcelSheet<CharaMakeName> _charaNameSheet = null!;

    public void Populate() {
        _charaMakeSheet = dataManager.GetExcelSheet<CharaMakeType>();
        _charaNameSheet = dataManager.GetExcelSheet<CharaMakeName>();

        log.Debug("Creating character editor structure");
        _characterEditorData = new CharacterEditorData();
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

    public (string FirstName, string LastName) GenerateRandomName(NpcRace race = NpcRace.Unknown, NpcTribe tribe = NpcTribe.Unknown, NpcSex gender = NpcSex.Male) {

        race = race == NpcRace.Unknown ? NpcRace.Hyur : race;
        tribe = tribe == NpcTribe.Unknown ? NpcTribe.Highlander : tribe;

        var rows = _charaNameSheet.ToList();
        if (rows.Count == 0)
            return ("Unknown", "Unknown");

        static List<string> Collect(IEnumerable<CharaMakeName> r, Func<CharaMakeName, ReadOnlySeString> selector)
            => [.. r.Select(x => selector(x).ToString()).Where(s => !string.IsNullOrWhiteSpace(s))];

        string Pick(List<string> list)
            => list.Count == 0 ? "Unknown" : list[Random.Shared.Next(list.Count)];

        (string first, string last) FromColumns(
            Func<CharaMakeName, ReadOnlySeString> firstCol,
            Func<CharaMakeName, ReadOnlySeString> lastCol) {
            return (Pick(Collect(rows, firstCol)), Pick(Collect(rows, lastCol)));
        }

        (string first, string last) LalafellPlainsfolk() {
            var firstStart = Collect(rows, r => r.LalafellPlainsfolkFirstNameStart);
            var lastStart = Collect(rows, r => r.LalafellPlainsfolkLastNameStart);
            var ends = Collect(rows, r => r.LalafellPlainsfolkEndOfNames);

            return (
                Pick(firstStart) + Pick(ends),
                Pick(lastStart) + Pick(ends)
            );
        }

        return (race, tribe) switch {
            (NpcRace.Hyur, NpcTribe.Midlander) => gender == NpcSex.Female
                ? FromColumns(r => r.HyurMidlanderFemale.ToString(), r => r.HyurMidlanderLastName)
                : FromColumns(r => r.HyurMidlanderMale, r => r.HyurMidlanderLastName),

            (NpcRace.Hyur, NpcTribe.Highlander) => gender == NpcSex.Female
                ? FromColumns(r => r.HyurHighlanderFemale, r => r.HyurHighlanderLastName)
                : FromColumns(r => r.HyurHighlanderMale, r => r.HyurHighlanderLastName),

            (NpcRace.Elezen, NpcTribe.Wildwood) => gender == NpcSex.Female
                ? FromColumns(r => r.ElezenFemale, r => r.ElezenWildwoodLastName)
                : FromColumns(r => r.ElezenMale, r => r.ElezenWildwoodLastName),

            (NpcRace.Elezen, NpcTribe.Duskwight) => gender == NpcSex.Female
                ? FromColumns(r => r.ElezenFemale, r => r.ElezenDuskwightLastName)
                : FromColumns(r => r.ElezenMale, r => r.ElezenDuskwightLastName),

            (NpcRace.Miqote, NpcTribe.SeekerOfTheSun) => gender == NpcSex.Female
                ? FromColumns(r => r.MiqoteSunFemale, r => r.MiqoteSunFemaleLastName)
                : FromColumns(r => r.MiqoteSunMale, r => r.MiqoteSunMaleLastName),

            (NpcRace.Miqote, NpcTribe.KeeperOfTheMoon) => gender == NpcSex.Female
                ? FromColumns(r => r.MiqoteMoonFemale, r => r.MiqoteMoonLastname)
                : FromColumns(r => r.MiqoteMoonMale, r => r.MiqoteMoonLastname),

            (NpcRace.Lalafel, NpcTribe.Plainsfolk) => LalafellPlainsfolk(),

            (NpcRace.Lalafel, NpcTribe.Dunesfolk) => gender == NpcSex.Female
                ? FromColumns(r => r.LalafellDunesfolkFemale, r => r.LalafellDunesfolkFemaleLastName)
                : FromColumns(r => r.LalafellDunesfolkMale, r => r.LalafellDunesfolkMaleLastName),

            (NpcRace.Roegadyn, NpcTribe.Helions) => gender == NpcSex.Female
                ? FromColumns(r => r.RoegadynSeaWolfFemale, r => r.RoegadynSeaWolfFemaleLastName)
                : FromColumns(r => r.RoegadynSeaWolfMale, r => r.RoegadynSeaWolfMaleLastName),

            (NpcRace.Roegadyn, NpcTribe.Hellsguard)
                => FromColumns(r => r.RoegadynHellsguardFirstName,
                               gender == NpcSex.Female
                                   ? r => r.RoegadynHellsguardFemaleLastName
                                   : r => r.RoegadynHellsguardMaleLastName),

            (NpcRace.AuRa, NpcTribe.Raen) => gender == NpcSex.Female
                ? FromColumns(r => r.AuRaRaenFemale, r => r.AuRaRaenLastName)
                : FromColumns(r => r.AuRaRaenMale, r => r.AuRaRaenLastName),

            (NpcRace.AuRa, NpcTribe.Xaela) => gender == NpcSex.Female
                ? FromColumns(r => r.AuRaXaelaFemale, r => r.AuRaXaelaLastName)
                : FromColumns(r => r.AuRaXaelaMale, r => r.AuRaXaelaLastName),

            (NpcRace.Hrothgar, NpcTribe.Helions) => FromColumns(r => r.HrothgarHellionsFirstName, r => r.HrothgarHellionsLastName),
            (NpcRace.Hrothgar, NpcTribe.TheLost) => FromColumns(r => r.HrothgarLostFirstName, r => r.HrothgarLostLastName),

            (NpcRace.Viera, NpcTribe.Rava) => FromColumns(r => r.VieraFirstName, r => r.VieraRavaLastName),
            (NpcRace.Viera, NpcTribe.Veena) => FromColumns(r => r.VieraFirstName, r => r.VieraVeenaLastName),

            _ => FromColumns(r => r.HyurHighlanderMale, r => r.HyurHighlanderLastName),
        };

    }

}
