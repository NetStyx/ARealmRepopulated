using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Supplementals;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Infrastructure;

public record NpcLookup(string Name, uint BaseId, uint ModelId, byte ModelType) {
    public ENpcBase? EventNpc { get; init; }
    public BNpcBase? BattleNpc { get; init; }
}

/// <summary>
/// Wrapper class for the existing population of the game. Covers who, where and appearance.
/// </summary>
public class ArrpNpcData(ArrpModelData modelData, IDataManager dataManager) {
    private readonly ExcelSheet<BNpcBase> _bnpcBaseSheet = dataManager.GetExcelSheet<BNpcBase>();
    private readonly ExcelSheet<BNpcName> _bnpcNameSheet = dataManager.GetExcelSheet<BNpcName>();
    private readonly ExcelSheet<ENpcBase> _enpcBaseSheet = dataManager.GetExcelSheet<ENpcBase>();
    private readonly ExcelSheet<ENpcResident> _enpcResidentSheet = dataManager.GetExcelSheet<ENpcResident>();
    private readonly ExcelSheet<Level> _levelSheet = dataManager.GetExcelSheet<Level>();
    private readonly ExcelSheet<ModelChara> _modelCharaSheet = dataManager.GetExcelSheet<ModelChara>();
    private List<ENpcBase>? _humanENpcs;
    private Dictionary<(NpcTribe Tribe, NpcSex Gender, NpcBodyType BodyType, bool YoungNpcRace), CharacterBodyTypeLooks>? _bodyTypeLooks;
    private HashSet<(int ModelCharaId, NpcTribe Tribe, NpcSex Gender, NpcBodyType BodyType)>? _modelBodies;
    private List<NpcLookup>? _npcs;
    private ILookup<uint, uint>? _residentIds;

    // A ModelChara row picks its animation set through AnimationVariant. Set 1 is considered standard and works for every body.
    // The other sets often hold an idle animation for a single race. If we assign this to any other race ... that model T-poses.
    // The animation files cant tell us which bodies (the combination of ModelCharaId, NpcTribe, NpcSex, NpcBodyType) are safe because several bodies borrow animation files from another race.
    // That leaves the npcs themself. The only way to determine if a body counts as covered is by checking if one of the games own npcs uses it on that model.
    public bool HasAnimationsFor(int modelCharaId, NpcTribe tribe, NpcSex sex, NpcBodyType bodyType) {
        if (_modelCharaSheet.GetRowOrDefault((uint)modelCharaId)?.AnimationVariant is null or 1)
            return true;

        _modelBodies ??= [
            .. _enpcBaseSheet.Where(n => n.ModelChara.RowId != 0)
                .Select(n => ((int)n.ModelChara.RowId, (NpcTribe)n.Tribe.RowId, (NpcSex)n.Gender, (NpcBodyType)n.BodyType)),
            .. _bnpcBaseSheet.Where(n => n.ModelChara.RowId != 0 && n.BNpcCustomize.RowId != 0)
                .Select(n => ((int)n.ModelChara.RowId, (NpcTribe)n.BNpcCustomize.Value.Tribe.RowId, (NpcSex)n.BNpcCustomize.Value.Gender, (NpcBodyType)n.BNpcCustomize.Value.BodyType))
        ];
        return _modelBodies.Contains((modelCharaId, tribe, sex, bodyType));
    }

    private bool IsHumanENpc(ENpcBase npc)
        => HasHumanLook(npc) && npc.Invisibility == 0;

    private bool HasHumanLook(ENpcBase npc)
        => npc.Race.RowId != 0 && npc.Tribe.RowId != 0 && modelData.IsHumanModel((int)npc.ModelChara.RowId);

    // Old and Young only exist for some clans, and a young character without a skeleton (a Rava, for one) takes the client down.
    // The games own validation runs too late for that and only checks the range
    public bool IsBodyTypeAvailable(NpcTribe tribe, NpcSex gender, NpcBodyType bodyType)
        => bodyType == NpcBodyType.Normal || GetBodyTypeLooks(tribe, gender, bodyType, false) != null;

    public CharacterBodyTypeLooks? GetBodyTypeLooks(NpcAppearanceData appearance)
        => GetBodyTypeLooks(appearance.Tribe, appearance.Sex, appearance.BodyType, appearance.UsesYoungNpcRace());

    private CharacterBodyTypeLooks? GetBodyTypeLooks(NpcTribe tribe, NpcSex gender, NpcBodyType bodyType, bool youngNpcRace) {
        _bodyTypeLooks ??= GetHumanENpcs()
            .GroupBy(n => (
                Tribe: (NpcTribe)n.Tribe.RowId,
                Gender: (NpcSex)n.Gender,
                BodyType: (NpcBodyType)n.BodyType,
                YoungNpcRace: n.Race.RowId == (uint)NpcRace.Hyur && NpcAppearanceData.HasYoungNpcRaceBit(n.ExtraFeature1)))
            .ToDictionary(g => g.Key, g => new CharacterBodyTypeLooks(
                g.Key.BodyType,
                [.. g.Select(n => n.Face).Distinct().Order()],
                [.. g.Select(n => n.HairStyle).Distinct().Order()],
                [.. g.Select(n => n.ExtraFeature1).Distinct().Order()]));

        return _bodyTypeLooks.GetValueOrDefault((tribe, gender, bodyType, youngNpcRace));
    }

    public List<ENpcBase> GetHumanENpcs()
        => _humanENpcs ??= [.. _enpcBaseSheet.Where(IsHumanENpc)];

    // Level places the npcs of a zone, type 8 stands for an event npc.
    public List<ENpcBase> GetResidents(uint territoryId) {
        _residentIds ??= _levelSheet
            .Where(l => l.Type == 8 && l.Territory.RowId != 0)
            .ToLookup(l => l.Territory.RowId, l => l.Object.RowId);

        return [.. _residentIds[territoryId]
            .Distinct()
            .Select(_enpcBaseSheet.GetRowOrDefault)
            .OfType<ENpcBase>()
            .Where(IsHumanENpc)
            .DistinctBy(n => (n.Tribe.RowId, n.Gender, n.Face, n.HairStyle, n.HairColor, n.SkinColor, n.NpcEquip.RowId, n.ModelBody))];
    }

    public List<NpcLookup> GetNpcs()
        => _npcs ??= [.. GetEventNpcs().Concat(GetBattleNpcs()).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)];

    // The same person shows up once per quest stage, so only a different look gets its own entry. About half the people
    // are flagged invisible in the sheet, which says nothing about their look, so they stay in here.
    private IEnumerable<NpcLookup> GetEventNpcs()
        => _enpcBaseSheet
            .Where(n => HasHumanLook(n) || IsModelENpc(n))
            .Select(n => new NpcLookup(_enpcResidentSheet.GetRowOrDefault(n.RowId)?.Singular.ToString() ?? "", n.RowId, n.ModelChara.RowId, n.ModelChara.Value.Type) { EventNpc = n })
            .Where(n => !string.IsNullOrWhiteSpace(n.Name))
            .DistinctBy(n => (n.Name, n.ModelId, n.EventNpc!.Value.Tribe.RowId, n.EventNpc.Value.Face, n.EventNpc.Value.HairStyle, n.EventNpc.Value.NpcEquip.RowId, n.EventNpc.Value.ModelBody));

    private bool IsModelENpc(ENpcBase npc)
        => npc.Race.RowId == 0 && npc.Invisibility == 0 && npc.ModelChara.ValueNullable is { Type: not 1, Model: > 0 };

    private IEnumerable<NpcLookup> GetBattleNpcs()
        => BNpcLinkParser.Instance.NameIdToBaseIds.Keys.ToList()
            .Select(_bnpcNameSheet.GetRowOrDefault)
            .Where(n => n != null && n.Value.RowId != 0 && !string.IsNullOrWhiteSpace(n.Value.Singular.ToString()))
            .SelectMany(n => BNpcLinkParser.Instance.GetBasesFromName(n!.Value.RowId).Select(id => (Name: n.Value.Singular.ToString(), Base: _bnpcBaseSheet.GetRowOrDefault(id).GetValueOrDefault())))
            .Where(b =>
                (b.Base.ModelChara.Value.Type == 1 || b.Base.ModelChara.Value.Model > 0)
                && (b.Base.BNpcCustomize.RowId > 0 || b.Base.NpcEquip.RowId > 0 || b.Base.ModelChara.RowId > 0))
            .Select(b => new NpcLookup(b.Name, b.Base.RowId, b.Base.ModelChara.RowId, b.Base.ModelChara.Value.Type) { BattleNpc = b.Base });
}
