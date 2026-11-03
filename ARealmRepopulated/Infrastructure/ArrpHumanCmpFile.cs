using ARealmRepopulated.Data.Appearance;
using Dalamud.Plugin.Services;
using System.IO;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// human.cmp holds the palettes of the character creator and the size ranges of each race.
/// </summary>
public class ArrpHumanCmpFile {

    private const string GamePath = "chara/xls/charaMake/human.cmp";

    public HumanColorTable? Colors { get; }
    public HumanHeightTable? Heights { get; }

    public ArrpHumanCmpFile(IPluginLog log, IDataManager dataManager) {
        var data = dataManager.GetFile(GamePath)?.Data ?? [];

        try {
            Colors = new HumanColorTable(data);
        } catch (InvalidDataException ex) {
            log.Error(ex, "Could not read the color table: the character editor will not show any color palettes");
        }

        try {
            Heights = new HumanHeightTable(data);
        } catch (InvalidDataException ex) {
            log.Error(ex, "Could not read the size table: imported and migrated actors will lose their height multiplier");
        }
    }
}
