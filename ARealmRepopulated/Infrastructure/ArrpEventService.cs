using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Infrastructure;
public class ArrpEventService : IDisposable
{
    private readonly IFramework _framework;
    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;

    private bool _isTerritoryReady = true;

    public event Action<ushort>? OnTerritoryLoadFinished;

    private bool IsBetweenZones =>
        _condition[ConditionFlag.BetweenAreas] ||
        _condition[ConditionFlag.BetweenAreas51];

    public bool IsTerritoryReady => _isTerritoryReady;

    public ArrpEventService(IFramework framework, IObjectTable objectTable, IClientState clientState, ICondition condition)
    {        
        _clientState = clientState;
        _framework = framework;
        _condition = condition;
        _objectTable = objectTable;

        framework.Update += Framework_Update;
        clientState.TerritoryChanged += ClientState_TerritoryChanged;
        clientState.Login += ClientState_Login;

        // initiate first check
        _isTerritoryReady = false;
    }

    private void ClientState_Login()
        => _isTerritoryReady = false;

    private void ClientState_TerritoryChanged(ushort obj)
        => _isTerritoryReady = false;

    private void Framework_Update(IFramework framework)
        => TerritoryCheck();

    private void TerritoryCheck()
    {
        if (_isTerritoryReady)
        {
            return;
        }

        if (_objectTable.LocalPlayer != null && _clientState.TerritoryType != 0 && !IsBetweenZones)
        {
            _isTerritoryReady = true;
            OnTerritoryLoadFinished?.Invoke(_clientState.TerritoryType);
        }
    }

    public void Dispose()
    {
        _framework.Update -= Framework_Update;
        _clientState.TerritoryChanged -= ClientState_TerritoryChanged;
        _clientState.Login -= ClientState_Login;
    }
}
