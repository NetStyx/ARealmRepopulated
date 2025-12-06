using ARealmRepopulated.Data.Location;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ARealmRepopulated.Infrastructure;

public class ArrpEventService : IDisposable {
    private readonly IFramework _framework;
    private readonly IObjectTable _objectTable;
    private readonly IPlayerState _playerState;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IPluginLog _log;

    private bool _isTerritoryReady = true;

    public event Action<LocationData>? OnTerritoryLoadFinished;
    private bool IsBetweenZones =>
        _condition[ConditionFlag.BetweenAreas] ||
        _condition[ConditionFlag.BetweenAreas51];

    public bool IsTerritoryReady => _isTerritoryReady;
    public LocationData CurrentLocation { get; private set; } = new LocationData();

    public ArrpEventService(IFramework framework, IObjectTable objectTable, IClientState clientState, ICondition condition, IPlayerState player, IPluginLog log) {
        _clientState = clientState;
        _framework = framework;
        _condition = condition;
        _objectTable = objectTable;
        _playerState = player;
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
            CurrentLocation = RetrieveCurrentLocation();
            OnTerritoryLoadFinished?.Invoke(CurrentLocation);
        }
    }

    private unsafe LocationData RetrieveCurrentLocation() {
        LocationData zoneData;
        var housingManager = HousingManager.Instance();
        var serverId = (int)_playerState.CurrentWorld.RowId;
        if (housingManager != null) {
            zoneData = new LocationData(serverId, _clientState.TerritoryType, housingManager->GetCurrentDivision(), housingManager->GetCurrentWard(), housingManager->GetCurrentPlot(), housingManager->IsInside());
        } else {
            zoneData = new LocationData(serverId, _clientState.TerritoryType, -1, -1, -1, false);
        }
        return zoneData;
    }


    public void Dispose() {
        _framework.Update -= Framework_Update;
        _clientState.TerritoryChanged -= ClientState_TerritoryChanged;
        _clientState.Login -= ClientState_Login;
    }
}
