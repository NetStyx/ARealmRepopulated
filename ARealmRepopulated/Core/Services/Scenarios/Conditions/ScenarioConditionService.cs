using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public unsafe class ScenarioConditionService {

    public IngameConditionSnapshot TakeSnapshot() {
        var framework = Framework.Instance();
        var envManager = EnvManager.Instance();

        return new(
            framework != null ? EorzeaTime.ToTimeOfDay(framework->ClientTime.EorzeaTime) : TimeOnly.MinValue,
            envManager != null ? envManager->ActiveWeather : (byte)0);
    }
}
