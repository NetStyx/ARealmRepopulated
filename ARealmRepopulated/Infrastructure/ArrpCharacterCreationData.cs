using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System.Text.RegularExpressions;

namespace ARealmRepopulated.Infrastructure;

public partial class ArrpCharacterCreationData(IPluginLog log, IDataManager dataManager, ArrpModelData modelData, ArrpHumanCmpFile humanCmp) {

    private CharacterEditorData _characterEditorData = null!;
    private ExcelSheet<CharaMakeType> _charaMakeSheet = null!;
    private ExcelSheet<CharaMakeName> _charaNameSheet = null!;
    private ExcelSheet<CharaMakeCustomize> _charaCustomizeSheet = null!;
    private ExcelSheet<Lobby> _lobbySheet = null!;
    private ExcelSheet<Tribe> _tribeSheet = null!;

    public void Populate() {
        _charaMakeSheet = dataManager.GetExcelSheet<CharaMakeType>();
        _charaNameSheet = dataManager.GetExcelSheet<CharaMakeName>();
        _charaCustomizeSheet = dataManager.GetExcelSheet<CharaMakeCustomize>();
        _lobbySheet = dataManager.GetExcelSheet<Lobby>();
        _tribeSheet = dataManager.GetExcelSheet<Tribe>();

        log.Debug("Creating character editor structure");
        _characterEditorData = new CharacterEditorData();
        foreach (var charaRow in _charaMakeSheet) {

            if (charaRow.Race.RowId == 0)
                continue;

            var race = (NpcRace)charaRow.Race.Value.RowId;
            var tribe = (NpcTribe)charaRow.Tribe.Value.RowId;
            var gender = (NpcSex)charaRow.Gender;

            var clanData = _characterEditorData.Clans.FirstOrDefault(x => x.Race == race && x.Tribe == tribe && x.Gender == gender);
            if (clanData == null) {
                _characterEditorData.Clans.Add(clanData = new CharacterEditorClan { Race = race, Tribe = tribe, Gender = gender });
            }

            clanData.Voices = [.. charaRow.VoiceStruct.Where(v => v != 0)];
            clanData.Menus = BuildMenus(charaRow);
            clanData.HighlightPalettes = humanCmp.Colors?.GetPalettes(CustomizeIndex.HighlightsColor, tribe, gender) ?? [];
            clanData.FacialFeatureIcons = [.. charaRow.FacialFeatureOption.Select(o => new uint[] {
                (uint)o.Option1, (uint)o.Option2, (uint)o.Option3, (uint)o.Option4, (uint)o.Option5, (uint)o.Option6, (uint)o.Option7
            })];
            clanData.Name = _tribeSheet.GetRowOrDefault((uint)tribe) is { } tribeRow
                ? (gender == NpcSex.Female ? tribeRow.Feminine : tribeRow.Masculine).ToString()
                : tribe.ToString();
        }

    }

    private List<CharacterEditorMenu> BuildMenus(CharaMakeType charaRow) {

        var tribe = (NpcTribe)charaRow.Tribe.RowId;
        var gender = (NpcSex)charaRow.Gender;

        var menus = new List<CharacterEditorMenu>();
        foreach (var entry in charaRow.CharaMakeStruct) {
            if (entry.Menu.RowId == 0)
                continue;

            // Voice is its own setting, and the sheet points iris size at the left eye color although the game keeps it in bit 7 of the eye shape.
            var index = (CustomizeIndex)entry.Customize;
            if (index is CustomizeIndex.Race or CustomizeIndex.EyeColorLeft)
                continue;

            var menu = new CharacterEditorMenu {
                Customize = index,
                Name = _lobbySheet.GetRowOrDefault(entry.Menu.RowId)?.Text.ToString() ?? index.ToString(),
                Kind = (CharacterEditorMenuKind)entry.SubMenuType,
                DefaultValue = entry.InitVal
            };

            switch (menu.Kind) {
                case CharacterEditorMenuKind.Options:
                case CharacterEditorMenuKind.Icons:
                    menu.Options = [.. Enumerable.Range(0, entry.SubMenuNum).Select(i => ReadOption(entry, i))];
                    menu.DefaultValue = entry.InitVal < menu.Options.Length ? menu.Options[entry.InitVal].Value : (byte)0;
                    break;
                case CharacterEditorMenuKind.Color:
                case CharacterEditorMenuKind.EyeColor:
                    menu.Palettes = humanCmp.Colors?.GetPalettes(index, tribe, gender) ?? [];
                    break;
                case CharacterEditorMenuKind.FacialFeatures:
                    // the five regular features fill the low bits, the two special ones (tattoos, limbal ring) follow
                    menu.FirstFeatureBit = entry.SubMenuNum == 5 ? 0 : 5;
                    menu.FeatureCount = entry.SubMenuNum;
                    break;
            }

            menus.Add(menu);
        }

        return menus;
    }

    // Hair and face paint keep a row of CharaMakeCustomize per option, the other menus list the icon and the value right in the sheet.
    private CharacterEditorOption ReadOption(CharaMakeType.CharaMakeStructStruct entry, int index) {
        var parameter = entry.SubMenuParam[index];
        if ((CustomizeIndex)entry.Customize is CustomizeIndex.HairStyle or CustomizeIndex.FacePaint) {
            return _charaCustomizeSheet.GetRowOrDefault(parameter) is { } customize
                ? new CharacterEditorOption(customize.FeatureID, customize.Icon)
                : new CharacterEditorOption((byte)index, 0);
        }

        var value = index < entry.SubMenuGraphic.Count ? entry.SubMenuGraphic[index] : (byte)index;
        return new CharacterEditorOption(value, (CharacterEditorMenuKind)entry.SubMenuType == CharacterEditorMenuKind.Icons ? parameter : 0);
    }

    public IReadOnlyList<CharacterEditorClan> GetClans()
        => _characterEditorData.Clans;

    public CharacterEditorClan? GetClan(NpcTribe tribe, NpcSex gender)
        => _characterEditorData.Clans.FirstOrDefault(x => x.Tribe == tribe && x.Gender == gender);

    // Monsters and other models without a clan have none of the options of the character editor.
    public bool IsCustomizable(NpcAppearanceData appearance)
        => modelData.IsHumanModel(appearance.ModelCharaId) && appearance.Tribe != NpcTribe.Unknown;

    public byte[] GetVoices(NpcRace race, NpcTribe tribe, NpcSex gender)
        => _characterEditorData.Clans.FirstOrDefault(x => x.Race == race && x.Tribe == tribe && x.Gender == gender)?.Voices ?? [];

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

            (NpcRace.Roegadyn, NpcTribe.SeaWolf) => gender == NpcSex.Female
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
