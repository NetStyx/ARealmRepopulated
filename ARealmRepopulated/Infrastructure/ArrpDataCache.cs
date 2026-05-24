using ARealmRepopulated.Core.Services.LayoutWorld;
using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Lumina.Text.ReadOnly;
using System.Diagnostics.CodeAnalysis;

namespace ARealmRepopulated.Infrastructure;

public class ArrpDataCache(IPluginLog log, IDataManager dataManager) {
    private ExcelSheet<TerritoryType> _territoryTypeSheet = null!;
    private ExcelSheet<Emote> _emoteTypeSheet = null!;
    private ExcelSheet<ActionTimeline> _actionTimelineSheet = null!;
    private ExcelSheet<Item> _itemSheet = null!;
    private readonly List<ItemModelData> _itemModelData = [];

    public void Populate() {
        _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
        _actionTimelineSheet = dataManager.GetExcelSheet<ActionTimeline>();
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

    public ActionTimeline GetActionTimeline(ushort actionTimelineId)
        => _actionTimelineSheet.GetRow(actionTimelineId);

    public List<ActionTimeline> GetActionTimelines()
        => [.. _actionTimelineSheet];

    public Emote GetEmote(uint emoteId)
        => _emoteTypeSheet.GetRow(emoteId);

    public List<Emote> GetEmotes()
        => [.. _emoteTypeSheet];

    public TerritoryType GetTerritoryType(ushort territoryTypeId) {
        return _territoryTypeSheet.GetRowOrDefault(territoryTypeId) ?? _territoryTypeSheet.First();
    }

}

public static class EmoteExtensions {

    /// <summary>
    /// There are emotes which have interactions with nearby layout objects, such as sitting on a chair or lying on a bed.
    /// Notably: 
    /// 0xD (Doze -> If a bed is near, prevents execution and instead plays 0x58)
    /// 0x58 (Sleep -> If a bed is near, you lie on it)
    /// 0x32 (Sit -> If a sitable position is near, you sit on it)
    /// </summary>  
    private static readonly Dictionary<uint, EmoteLayoutInteraction> LayoutInteractionOverrides = new() {
        { 0xD, new EmoteLayoutInteraction(0xD, 0x58, LayoutTarget.Bed) },
        { 0x58, new EmoteLayoutInteraction(0x58, 0x58, LayoutTarget.Bed) },
        { 0x32, new EmoteLayoutInteraction(0x32, 0x32, LayoutTarget.Chair) }
    };

    public static bool IsLooping(this Emote emote) {
        if (!emote.EmoteMode.IsValid)
            return false;

        var emoteCondition = (CharacterModes)emote.EmoteMode.Value.ConditionMode;
        return emoteCondition == CharacterModes.EmoteLoop || emoteCondition == CharacterModes.InPositionLoop;
    }

    public static bool InteractsWithLayout(this Emote emote)
        => InteractsWithLayout(emote.RowId);

    public static bool InteractsWithLayout(uint emoteId) {
        return LayoutInteractionOverrides.ContainsKey(emoteId);
    }

    public static bool InteractsWithLayout(this Emote emote, [NotNullWhen(true)] out EmoteLayoutInteraction? layoutInteraction) {
        if (LayoutInteractionOverrides.TryGetValue(emote.RowId, out var emoteOverride)) {
            layoutInteraction = emoteOverride;
            return true;
        }

        layoutInteraction = null;
        return false;
    }

    public record EmoteLayoutInteraction(uint OriginalEmoteId, uint LayoutInteractionEmoteId, LayoutTarget LayoutObjectTarget);
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
