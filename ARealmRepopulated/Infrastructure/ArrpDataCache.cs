using ARealmRepopulated.Core.Services.LayoutWorld;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Supplementals;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Lumina.Text.ReadOnly;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ARealmRepopulated.Infrastructure;

public partial class ArrpDataCache(IPluginLog log, IDataManager dataManager) {
    private ExcelSheet<TerritoryType> _territoryTypeSheet = null!;
    private ExcelSheet<Emote> _emoteTypeSheet = null!;
    private ExcelSheet<ActionTimeline> _actionTimelineSheet = null!;
    private ExcelSheet<Item> _itemSheet = null!;
    private ExcelSheet<BNpcBase> _bnpcBaseSheet = null!;
    private ExcelSheet<BNpcName> _bnpcNameSheet = null!;
    private readonly List<ItemModelData> _itemModelData = [];
    private Dictionary<PoseType, ushort[]> _poseStateEmotes = [];

    public void Populate() {
        _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
        _actionTimelineSheet = dataManager.GetExcelSheet<ActionTimeline>();
        _emoteTypeSheet = dataManager.GetExcelSheet<Emote>();
        _itemSheet = dataManager.GetExcelSheet<Item>();
        _bnpcBaseSheet = dataManager.GetExcelSheet<BNpcBase>();
        _bnpcNameSheet = dataManager.GetExcelSheet<BNpcName>();

        _poseStateEmotes = BuildPoseStateEmotes();
    }

    private const uint PoseEmoteCategory = 4;

    /// if its stupid but it works, its not stupid. Maybe someone has found a better way to correlate this but for now, this stays.    
    /// a pose variant animation is named after the pose it belongs to: emote/pose03_loop is the third standing pose, 
    /// emote/j_pose02_loop the second one while sitting on the ground. That holds true unless square decides to change the naming convention.
    [GeneratedRegex(@"^emote/(?:([bsjl])_)?pose(\d+)_loop$", RegexOptions.Compiled)]
    private static partial Regex PoseTimelineRegex();
    [GeneratedRegex(@"^ornament_sp/m(\d+)/onm_pose(\d+)_loop$", RegexOptions.Compiled)]
    private static partial Regex OrnamentPoseTimelineRegex();

    private static readonly Regex PoseTimelineExpression = PoseTimelineRegex();
    private static readonly Regex OrnamentPoseTimelineExpression = OrnamentPoseTimelineRegex();

    private static readonly Dictionary<string, PoseType> PoseTimelinePrefixes = new() {
        { "", PoseType.Idle },
        { "b", PoseType.WeaponDrawn },
        { "s", PoseType.Sit },
        { "j", PoseType.GroundSit },
        { "l", PoseType.Doze }
    };

    private static readonly Dictionary<string, PoseType> OrnamentPoseModels = new() {
        { "6001", PoseType.Umbrella },
        { "6016", PoseType.Accessory }
    };

    public static bool TryParsePoseTimeline(string timelineKey, out PoseType poseType, out int poseIndex) {

        poseType = PoseType.Idle;
        poseIndex = 0;

        if (PoseTimelineExpression.Match(timelineKey) is { Success: true } pose)
            return PoseTimelinePrefixes.TryGetValue(pose.Groups[1].Value, out poseType)
                && int.TryParse(pose.Groups[2].Value, out poseIndex)
                && poseIndex > 0;

        if (OrnamentPoseTimelineExpression.Match(timelineKey) is { Success: true } ornamentPose)
            return OrnamentPoseModels.TryGetValue(ornamentPose.Groups[1].Value, out poseType)
                && int.TryParse(ornamentPose.Groups[2].Value, out poseIndex)
                && poseIndex > 0;

        return false;
    }

    private Dictionary<PoseType, ushort[]> BuildPoseStateEmotes() {

        var poses = new Dictionary<PoseType, SortedDictionary<int, ushort>>();

        foreach (var emote in _emoteTypeSheet) {
            if (emote.EmoteCategory.RowId != PoseEmoteCategory || emote.ActionTimeline.Count == 0)
                continue;

            var loopTimeline = emote.ActionTimeline[0];
            if (!loopTimeline.IsValid || !TryParsePoseTimeline(loopTimeline.Value.Key.ToString(), out var poseType, out var poseIndex))
                continue;

            if (!poses.TryGetValue(poseType, out var variants))
                poses[poseType] = variants = [];

            variants[poseIndex] = (ushort)emote.RowId;
        }

        var poseStateEmotes = new Dictionary<PoseType, ushort[]>();
        foreach (var (poseType, variants) in poses) {
            poseStateEmotes[poseType] = [0, .. variants.Values];
        }

        log.Verbose($"Discovered pose variants: {string.Join(", ", poseStateEmotes.Select(p => $"{p.Key}={p.Value.Length}"))}");

        return poseStateEmotes;
    }

    public int GetPoseStateCount(PoseType poseType)
        => _poseStateEmotes.TryGetValue(poseType, out var emotes) ? emotes.Length : 1;

    public byte ClampPoseState(PoseType poseType, byte poseState)
        => (byte)Math.Clamp(poseState, 0, GetPoseStateCount(poseType) - 1);

    public ushort GetPoseStateEmote(PoseType poseType, byte poseState)
        => _poseStateEmotes.TryGetValue(poseType, out var emotes) && poseState > 0 && poseState < emotes.Length
            ? emotes[poseState]
            : (ushort)0;

    public ushort GetPoseStateEmote(ushort emoteId, byte poseState)
        => GetEmote(emoteId).TryGetPoseType(out var poseType) ? GetPoseStateEmote(poseType, poseState) : (ushort)0;

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

    public List<BNpcLookup> GetBNpcBases(int type, Predicate<string> pred) {

        return [.. BNpcLinkParser.Instance.NameIdToBaseIds.Keys.ToList()
            .Select(_bnpcNameSheet.GetRowOrDefault)
            .Where(n =>
                n != null
                && n.HasValue && n.Value.RowId != 0
                && pred(n.Value.Singular.ToString()))
            .SelectMany(n =>
                BNpcLinkParser.Instance.GetBasesFromName(n!.Value.RowId).Select(b => new BNpcLookup {
                    Name = n.GetValueOrDefault(),
                    Base = _bnpcBaseSheet.GetRowOrDefault(b).GetValueOrDefault()
                }))
            .Where(b =>
                (type == 0 || type == b.Base.ModelChara.Value.Type)
                && (b.Base.ModelChara.Value.Type == 1 || b.Base.ModelChara.Value.Model > 0)
                && (b.Base.BNpcCustomize.RowId > 0 || b.Base.NpcEquip.RowId > 0 || b.Base.ModelChara.RowId > 0)
            )
           ];
    }

}

public class BNpcLookup {
    public BNpcName Name { get; set; }
    public BNpcBase Base { get; set; }
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

    /// <summary>
    /// Emotes that park the actor in one of the game's pose types. Everything not listed here leaves
    /// the actor standing, which is <see cref="PoseType.Idle"/>    
    /// </summary>
    private static readonly Dictionary<uint, PoseType> EmotePoseType = new(){
        { 0xD, PoseType.Doze },
        { 0x58, PoseType.Doze },
        { 0x32, PoseType.Sit },
        { 0x34, PoseType.GroundSit }
    };

    public static PoseType GetPoseType(this Emote emote)
        => EmotePoseType.TryGetValue(emote.RowId, out var poseType) ? poseType : PoseType.Idle;
    
    public static bool TryGetPoseType(this Emote emote, out PoseType poseType)
        => EmotePoseType.TryGetValue(emote.RowId, out poseType);

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

public partial class ArrpCharacterCreationData(IPluginLog log, IDataManager dataManager) {

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

    [GeneratedRegex(@"^[A-Za-z]+(?:['-][A-Za-z]+)*$", RegexOptions.Compiled)]
    private static partial Regex CharacterNameRegex();

    /// <summary>
    /// Validates that the string given conforms to the naming rules of SQEX.
    /// See: https://support.na.square-enix.com/faqarticle.php?kid=67258&id=5382&page=1&sc=0
    /// Also: 20 Characters Max, One Empty Space to seperate first and last name, 2 to 15 characters each, no special characters except hypen and apostrophe, no numbers. No repeated special characters, no leading or trailing special characters.
    /// </summary>        
    public static bool IsValidPlayerName(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must not start or end with spaces
        if (name.StartsWith(' ') || name.EndsWith(' '))
            return false;

        // Must have exactly 2 parts: first name and last name
        var parts = name.Trim().Split(' ');
        if (parts.Length != 2)
            return false;

        var firstName = parts[0];
        var lastName = parts[1];

        // Each name must be between 2 and 15 characters
        if (firstName.Length is < 2 or > 15)
            return false;

        if (lastName.Length is < 2 or > 15)
            return false;

        // Combined maximum of 20 characters (including the space separator)
        if (name.Length > 20)
            return false;

        return CharacterNameRegex().IsMatch(firstName) && CharacterNameRegex().IsMatch(lastName);
    }

    public string GetRandomName() {
        var chars = "abcdefghijklmnopqrstuvwxyz";
        var stringChars = new char[15];
        var random = new Random(Guid.NewGuid().GetHashCode());

        for (var i = 0; i < stringChars.Length; i++) {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        stringChars[0] = char.ToUpper(stringChars[0]);

        return new string(stringChars);
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
