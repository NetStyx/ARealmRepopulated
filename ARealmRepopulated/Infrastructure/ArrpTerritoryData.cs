using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// Wrapperclass for accessing the games locations ("territories") and environmental conditions.
/// </summary>
public class ArrpTerritoryData(IDataManager dataManager) {
    private readonly ExcelSheet<TerritoryType> _territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>();
    private readonly ExcelSheet<Weather> _weatherSheet = dataManager.GetExcelSheet<Weather>();

    public TerritoryType GetTerritoryType(ushort territoryTypeId) {
        return _territoryTypeSheet.GetRowOrDefault(territoryTypeId) ?? _territoryTypeSheet.First();
    }

    public string GetPlaceName(uint territoryTypeId) {
        var placeName = GetTerritoryType((ushort)territoryTypeId).PlaceName.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(placeName) ? territoryTypeId.ToString() : placeName;
    }

    public Weather? GetWeather(byte weatherId)
        => _weatherSheet.GetRowOrDefault(weatherId);

    public List<Weather> GetWeathersForTerritory(uint territoryTypeId) {
        var weatherRate = _territoryTypeSheet.GetRowOrDefault(territoryTypeId)?.WeatherRate.ValueNullable;
        if (weatherRate == null)
            return GetNamedWeathers();

        var weathers = weatherRate.Value.Weather
            .Where(w => w.RowId != 0)
            .Select(w => w.ValueNullable)
            .OfType<Weather>()
            .DistinctBy(w => w.RowId)
            .ToList();

        return weathers.Count > 0 ? weathers : GetNamedWeathers();
    }

    private List<Weather> GetNamedWeathers()
        => [.. _weatherSheet.Where(w => w.RowId != 0 && !w.Name.IsEmpty)];
}
