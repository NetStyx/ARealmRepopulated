using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Data.Location;

public record LocationData(int Server = -1, int TerritoryType = -1, int HousingDivision = -1, int HousingWard = -1, int HousingPlot = -1, bool IsInsideHousing = false);

public static class LocationDataExtension {

    public static bool IsInSameLocation(this LocationData currentLocation, ScenarioLocation scenario) {

        if (scenario.Territory != currentLocation.TerritoryType)
            return false;

        if (currentLocation.IsInsideHousing) {
            if (scenario.Server != currentLocation.Server
                || scenario.HousingDivision != currentLocation.HousingDivision
                || scenario.HousingWard != currentLocation.HousingWard
                || scenario.HousingPlot != currentLocation.HousingPlot)
                return false;
        }

        return true;
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
