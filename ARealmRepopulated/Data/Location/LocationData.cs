using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ARealmRepopulated.Data.Location;

public record LocationData(uint Server = 0, uint TerritoryType = 0, uint HousingDivision = 0, int HousingWard = -1, int HousingPlot = -1, HousingTerritoryType HousingArea = HousingTerritoryType.None);

public static class LocationDataExtension {

    public static bool IsInSameLocation(this LocationData currentLocation, ScenarioLocation scenario) {

        if (scenario.Territory != currentLocation.TerritoryType)
            return false;

        var isInSameLocation = true;
        if (currentLocation.HousingArea != HousingTerritoryType.None) {
            isInSameLocation =
                scenario.Server == currentLocation.Server
                && scenario.HousingDivision == currentLocation.HousingDivision
                && scenario.HousingWard == currentLocation.HousingWard;

            if (currentLocation.HousingArea == HousingTerritoryType.Indoor) {
                isInSameLocation &= scenario.HousingPlot == currentLocation.HousingPlot;
            }
        }

        return isInSameLocation;
    }

    public static void UpdateScenarioLocation(this LocationData location, ScenarioLocation target) {
        target.Server = location.Server;
        target.Territory = location.TerritoryType;
        target.HousingDivision = location.HousingDivision;
        target.HousingWard = location.HousingWard;
        target.HousingPlot = location.HousingPlot;
        target.Server = location.Server;
    }

}
