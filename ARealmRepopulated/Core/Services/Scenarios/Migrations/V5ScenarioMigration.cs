using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

/// <summary>
/// In the previous versions the actors size is defined by two scales. 
/// It was an mistake to give users two different ways to scale the same thing just because the game does the calculation that way. 
/// Now i have to resoncile the two somehow. 
/// </summary>
[ScenarioMigration(Version = 5, Description = "Merge height multiplier into actor scale")]
public class V5ScenarioMigration(ArrpDataCache dataCache) : IScenarioMigration {

    private const string HeightMultiplierKey = "HeightMultiplier";

    public void Upgrade(JsonObject jsonObject) {

        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            if (npc?["Appearance"] is not JsonObject appearance)
                continue;

            var heightMultiplier = appearance[HeightMultiplierKey].GetFloatOrNull();
            appearance.Remove(HeightMultiplierKey);
            if (heightMultiplier is not { } multiplier || !float.IsFinite(multiplier) || multiplier <= 0)
                continue;
                        
            if (appearance["Tribe"].GetEnumOrNull<NpcTribe>(defaultWhenMissing: true) is not { } tribe
                || appearance["Sex"].GetEnumOrNull<NpcSex>(defaultWhenMissing: true) is not { } sex
                || appearance["BodyType"].GetEnumOrNull<NpcBodyType>(defaultWhenMissing: true) is not { } bodyType)
                continue;
            
            if (dataCache.GetModelScale(appearance["ModelCharaId"].GetIntOrNull() ?? 0, tribe, sex, bodyType, appearance["Height"].GetByteOrNull() ?? 0) is not { } gameScale)
                continue;

            appearance["Scale"] = (appearance["Scale"].GetFloatOrNull() ?? 1f) * multiplier / gameScale;
        }
    }
}
