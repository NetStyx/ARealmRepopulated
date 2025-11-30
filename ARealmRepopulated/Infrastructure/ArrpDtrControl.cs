using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Windows;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARealmRepopulated.Infrastructure;

public class ArrpDtrControl(PluginConfig config, ScenarioOrchestrator manager, IClientState clientState, IDtrBar dalamudDtrBar) : IDisposable
{

    private IDtrBarEntry? _dtrBarEntry = null;

    public void Initialize()
    {
        clientState.Logout += OnLogout;
        clientState.Login += OnLogin;
        manager.OnOrchestrationsChanged += Manager_OrchestrationsChanged;
        if (clientState.IsLoggedIn)
        {
            OnLogin();
        }

    }

    private void Manager_OrchestrationsChanged()
        => UpdateDtrText();

    private void OnLogin()
    {        
        if ((_dtrBarEntry = dalamudDtrBar.Get("ARealmRepopulated Scenario Entry")) != null)
        {                        
            _dtrBarEntry.OnClick = (e) =>
            {
                var configWindow = Plugin.Services.GetRequiredService<ConfigWindow>();
                if (!configWindow.IsOpen)
                {
                    configWindow.Toggle();
                }
            };
        }
        UpdateDtrText();
        UpdateVisibility();
    }

    public void UpdateVisibility()
    {
        _dtrBarEntry?.Shown = config.ShowInDtrBar;
    }

    private void UpdateDtrText() {
        switch (manager.Orchestrations.Count)
        {
            case 0:
                _dtrBarEntry?.Text = $"\uE083 \uE043";
                break;
            default:
                _dtrBarEntry?.Text = $"\uE083 {manager.Orchestrations.Count}";
                break;
        }
    }

    private void OnLogout(int type, int code)
    {
        _dtrBarEntry?.Remove();
        _dtrBarEntry = null;
    }

    public void Dispose()
    {
        clientState.Login -= OnLogin;
        clientState.Logout -= OnLogout;
    }
}
