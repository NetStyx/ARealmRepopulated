using System.IO;

namespace ARealmRepopulated.Data.Supplementals;

public class BNpcLinkParser {

    public static BNpcLinkParser Instance { get; } = new BNpcLinkParser();

    public Dictionary<uint, List<uint>> BaseIdToNameIds { get; private set; } = [];
    public Dictionary<uint, List<uint>> NameIdToBaseIds { get; private set; } = [];

    private BNpcLinkParser() {

        var assembly = typeof(BNpcLinkParser).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(f => f.EndsWith("BNpcLink.csv"));
        using var stream = assembly.GetManifestResourceStream(resourceName!);
        using var reader = new StreamReader(stream!);
        var data = reader.ReadToEnd();

        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
            var parts = line.Trim().Split(',');

            if (parts.Length < 2 || !uint.TryParse(parts[0], out var nameId) || !uint.TryParse(parts[1], out var baseId)) {
                continue;
            }

            // BaseId → NameIds
            if (!BaseIdToNameIds.TryGetValue(baseId, out var nameIdList)) {
                nameIdList = [];
                BaseIdToNameIds[baseId] = nameIdList;
            }
            nameIdList.Add(nameId);

            // NameId → BaseIds
            if (!NameIdToBaseIds.TryGetValue(nameId, out var baseIdList)) {
                baseIdList = [];
                NameIdToBaseIds[nameId] = baseIdList;
            }
            baseIdList.Add(baseId);
        }
    }

    public List<uint> GetNamesFromBase(uint baseId) {
        return BaseIdToNameIds.TryGetValue(baseId, out var nameIds) ? nameIds : new List<uint>();
    }

    public List<uint> GetBasesFromName(uint nameId) {
        return NameIdToBaseIds.TryGetValue(nameId, out var baseIds) ? baseIds : new List<uint>();
    }
}
