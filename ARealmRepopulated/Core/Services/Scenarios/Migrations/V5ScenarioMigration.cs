using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

/// <summary>
/// In the previous versions the actors size is defined by two scales which was a huge mistake as it gave the users two ways to scale the same thing 
/// just because the game does the calculation that way. Now i have to resoncile this somehow. 
/// </summary>
[ScenarioMigration(Version = 5, Description = "Turn the height multiplier back into the Height")]
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
            
            if (dataCache.ReverseModelScale(appearance["ModelCharaId"].GetIntOrNull() ?? 0, tribe, sex, bodyType, multiplier) is not { } components)
                continue;
            
            appearance["Height"] = components.Height;
            appearance["Scale"] = (appearance["Scale"].GetFloatOrNull() ?? 1f) * components.ScaleFactor;
        }
    }
}
