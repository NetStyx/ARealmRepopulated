using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.IPC;

public static class IntegrationProvider {

    public const string ActorNameConfigKey = "Integration.General.Actor.Name";

    public static bool TryGetIntegrationProperty(this ScenarioNpcData npcData, string key, out string data, string defaultValue = "") {
        data = "";
        if (npcData == null) {
            return false;
        }

        if (npcData.AdditionalData.GetValueOrDefault(key, "") is string actorName && !string.IsNullOrEmpty(actorName)) {
            data = actorName;
        }

        return true;
    }

    public static bool SetIntegrationProperty(this ScenarioNpcData npcData, string key, string value) {
        if (npcData == null) {
            return false;
        }
        if (npcData.AdditionalData.ContainsKey(key) && string.IsNullOrWhiteSpace(value)) {
            npcData.AdditionalData.Remove(key);
        } else {
            npcData.AdditionalData[key] = value;
        }
        return true;
    }

}
