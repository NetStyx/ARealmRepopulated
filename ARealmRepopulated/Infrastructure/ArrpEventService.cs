using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ARealmRepopulated.Infrastructure;

public class ArrpEventService : IDisposable {
    private readonly IFramework _framework;
    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IPluginLog _log;

    private bool _isTerritoryReady = true;

    public event Action<LocationData>? OnTerritoryLoadFinished;

    private bool IsBetweenZones =>
        _condition[ConditionFlag.BetweenAreas] ||
        _condition[ConditionFlag.BetweenAreas51];

    public bool IsTerritoryReady => _isTerritoryReady;

    public ArrpEventService(IFramework framework, IObjectTable objectTable, IClientState clientState, ICondition condition, IPluginLog log) {
        _clientState = clientState;
        _framework = framework;
        _condition = condition;
        _objectTable = objectTable;
        _log = log;

        framework.Update += Framework_Update;
        clientState.TerritoryChanged += ClientState_TerritoryChanged;
        clientState.Login += ClientState_Login;
    }

    public void Arm()
        => _isTerritoryReady = false;

    private void ClientState_Login()
        => _isTerritoryReady = false;

    private void ClientState_TerritoryChanged(ushort obj)
        => _isTerritoryReady = false;

    private void Framework_Update(IFramework framework)
        => TerritoryCheck();

    private unsafe void TerritoryCheck() {
        if (_isTerritoryReady) {
            return;
        }

        if (_objectTable.LocalPlayer != null && _clientState.TerritoryType != 0 && !IsBetweenZones) {
            _isTerritoryReady = true;
            _log.Debug($"Territory changed to {_clientState.TerritoryType}. Zone ready.");


            LocationData zoneData;
            var housingManager = HousingManager.Instance();
            if (housingManager != null) {
                zoneData = new LocationData(_clientState.TerritoryType, housingManager->GetCurrentDivision(), housingManager->GetCurrentWard(), housingManager->GetCurrentPlot());
            } else {
                zoneData = new LocationData(_clientState.TerritoryType, 0, -1, -1);
            }

            /*
            if (HousingManager.Instance()->IsInside()) {
            _log.Debug("Ward " + housingManager->);
            _log.Debug("Ward " + );
            _log.Debug("Plot " + );
            _log.Debug("Division " + );
            _log.Debug("HouseId " + housingManager->GetCurrentHouseId().);
            _log.Debug("Indoor HID " + housingManager->GetCurrentIndoorHouseId().Id);
            }
            */

            OnTerritoryLoadFinished?.Invoke(zoneData);
        }
    }

    public void Dispose() {
        _framework.Update -= Framework_Update;
        _clientState.TerritoryChanged -= ClientState_TerritoryChanged;
        _clientState.Login -= ClientState_Login;
    }
}


public record LocationData(ushort TerritoryType, byte HousingDivision, sbyte HousingWard, sbyte HousingPlot);
