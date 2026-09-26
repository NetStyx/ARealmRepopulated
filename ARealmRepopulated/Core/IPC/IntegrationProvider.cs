using ARealmRepopulated.Data.Scenarios;
using System.Globalization;

namespace ARealmRepopulated.Core.IPC;

public static class IntegrationProvider {

    public const string ActorNameConfigKey = "Integration.General.Actor.Name";
    public const string ExternalAppearanceConfigKey = "Integration.General.Appearance.External";

    public static bool TryGetIntegrationProperty(this ScenarioNpcData npcData, string key, out string data, string defaultValue = "") {
        data = "";
        if (npcData == null) {
            return false;
        }

        data = npcData.AdditionalData.GetValueOrDefault(key, "");
        if (string.IsNullOrEmpty(data))
            return false;

        return true;
    }

    public static bool TryGetIntegrationProperty<T>(this ScenarioNpcData npcData, string key, out T data) where T : IParsable<T> {
        data = default!;
        return npcData.TryGetIntegrationProperty(key, out var value) && T.TryParse(value, CultureInfo.InvariantCulture, out data!);
    }

    public static bool SetIntegrationProperty(this ScenarioNpcData npcData, string key, string value) {
        if (npcData == null) {
            return false;
        }
        if (string.IsNullOrWhiteSpace(value)) {
            npcData.AdditionalData.Remove(key);
        } else {
            npcData.AdditionalData[key] = value;
        }

        return true;
    }

}
