using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

/// <summary>
/// The earlier versions used an height multiplier to scale together with another scale. The game now derives these now from the customize data.
/// </summary>
[ScenarioMigration(Version = 5, Description = "Merge height multiplier into actor scale")]
public class V5ScenarioMigration(ArrpDataCache dataCache) : IScenarioMigration {

    public readonly record struct V4Customize(int ModelCharaId, NpcTribe Tribe, NpcSex Sex, NpcBodyType BodyType, byte Height);

    private const string HeightMultiplierKey = "HeightMultiplier";

    public void Upgrade(JsonObject jsonObject)
        => FoldHeightMultipliers(jsonObject, c => dataCache.GetModelScale(c.ModelCharaId, c.Tribe, c.Sex, c.BodyType, c.Height));

    public static void FoldHeightMultipliers(JsonObject jsonObject, Func<V4Customize, float?> gameModelScale) {

        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            if (npc?["Appearance"] is not JsonObject appearance)
                continue;

            // a missing value was read as the old default of 1f and applied, only an explicit null left the games model scale alone
            var heightMultiplier = appearance.TryGetPropertyValue(HeightMultiplierKey, out var heightNode) ? heightNode.GetFloatOrNull() : 1f;
            appearance.Remove(HeightMultiplierKey);
            if (heightMultiplier is not { } multiplier || !float.IsFinite(multiplier) || multiplier <= 0)
                continue;

            // older files hold these enums as names or as numbers, depending on which version last saved them, and a missing value was read as the enum's default. 
            // A value version 4 could not read either never loaded, so there is no size to keep.
            if (appearance["Tribe"].GetEnumOrNull<NpcTribe>(defaultWhenMissing: true) is not { } tribe
                || appearance["Sex"].GetEnumOrNull<NpcSex>(defaultWhenMissing: true) is not { } sex
                || appearance["BodyType"].GetEnumOrNull<NpcBodyType>(defaultWhenMissing: true) is not { } bodyType)
                continue;

            // version 4 applied a missing height as 0 and a missing scale as 1
            var customize = new V4Customize(appearance["ModelCharaId"].GetIntOrNull() ?? 0, tribe, sex, bodyType, appearance["Height"].GetByteOrNull() ?? 0);
            if (gameModelScale(customize) is not { } gameScale)
                continue;

            appearance["Scale"] = (appearance["Scale"].GetFloatOrNull() ?? 1f) * multiplier / gameScale;
        }
    }
}
