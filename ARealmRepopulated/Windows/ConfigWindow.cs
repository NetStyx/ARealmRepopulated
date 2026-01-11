using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public class ConfigWindow(
    IPluginLog log,
    IClientState state,
    ArrpTranslation loc,
    PluginConfig _config,
    ScenarioFileManager _fileManager,
    DebugOverlay _debugOverlay,
    ArrpEventService eventService,
    ArrpDtrControl dtrControl,
    ArrpDataCache dataCache) : ADalamudWindow("###ARealmRepopulatedConfigWindow") {

    protected override void SetWindowOptions() {
        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoCollapse;
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.CollapsedCondition = ImGuiCond.None;
        loc.OnLocalizationChanged += UpdateWindowTitle;
        UpdateWindowTitle();
    }

    public override void OnOpen() {
        base.OnOpen();

        if (_config.EnableScenarioDebugOverlay)
            _debugOverlay.Hook();
    }

    public override void OnClose() {
        base.OnClose();
        _debugOverlay.Unhook();
    }

    private void UpdateWindowTitle()
        => this.WindowName = $"{loc["ListWnd_Title"]}###ARealmRepopulatedConfigWindow";

    public override void Draw() {

        if (ImGui.BeginChild("", new Vector2(0, -50), border: false, flags: ImGuiWindowFlags.NoResize)) {

            if (ImGui.BeginTabBar("", ImGuiTabBarFlags.NoTooltip)) {
                if (ImGui.BeginTabItem(loc["ListWnd_Scenario_Title"], ImGuiTabItemFlags.NoTooltip)) {
                    try {
                        ScenarioTab();
                    } catch (System.Exception ex) {
                        ImGui.TextColored(ImGuiColors.DalamudRed, $"Error loading scenarios: {ex.Message}");
                        log.Error(ex, "huh");
                    }
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(loc["ListWnd_Options_Title"], ImGuiTabItemFlags.NoTooltip)) {
                    OptionsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            ImGui.EndChild();
        }

        ImGui.Separator();
        if (ImGui.BeginTable("##WindowControlTable", 3)) {
            ImGui.TableSetupColumn("##configWindowControlStrech", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##configWindowControlClose", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##configWindowControlPadding", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            if (ImGui.Button(loc["ListWnd_Close"])) {
                this.IsOpen = false;
            }
            ImGui.EndTable();
        }

    }

    private void OptionsTab() {
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var autoLoadScenarios = _config.AutoLoadScenarios;
        if (ImGui.Checkbox(loc["ListWnd_Options_AutoLoad_Option"], ref autoLoadScenarios)) {
            _config.AutoLoadScenarios = autoLoadScenarios;
            _config.Save();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Options_AutoLoad_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var showDtrEntry = _config.ShowInDtrBar;
        if (ImGui.Checkbox(loc["ListWnd_Options_DtrBar_Option"], ref showDtrEntry)) {
            _config.ShowInDtrBar = showDtrEntry;
            _config.Save();

            dtrControl.UpdateVisibility();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Options_DtrBar_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var scenarioDebugOverlay = _config.EnableScenarioDebugOverlay;
        if (ImGui.Checkbox(loc["ListWnd_Options_DebugOverlay_Option"], ref scenarioDebugOverlay)) {
            if (scenarioDebugOverlay) {
                _debugOverlay.Hook();
            } else {
                _debugOverlay.Unhook();
            }

            _config.EnableScenarioDebugOverlay = scenarioDebugOverlay;
            _config.Save();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Options_DebugOverlay_Desc"]);
    }

    private string _searchScenarioText = string.Empty;
    private void ScenarioTab() {

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Scenario_Desc"]);
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();

        if (ImGui.BeginTable("##SearchTable", 1)) {
            // Add search box for territories when able.
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##SearchScenarios", loc["ListWnd_Scenario_Search_Placeholder"], ref _searchScenarioText);
            ImGui.EndTable();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        if (ImGui.BeginTable("##AvailableScenarioTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY)) {
            ImGui.TableSetupColumn("##scenarioRefreshHeader", ImGuiTableColumnFlags.WidthFixed, 25);
            ImGui.TableSetupColumn("##scenarioLocationHeader", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioTitleHeader", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##scenarioToolbarHeader", ImGuiTableColumnFlags.WidthFixed);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            DrawCenteredHeaderCell(0, () => {
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Recycle)) {
                    _fileManager.ScanScenarioFiles();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_Scan_Desc"]);
            });
            DrawCenteredHeaderCell(1, () => ImGui.Text("Location"));
            DrawCenteredHeaderCell(2, () => ImGui.Text("Scenario"));
            DrawCenteredHeaderCell(3, () => {
                using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
                    if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Plus)) {
                        Plugin.Services.GetService<ScenarioEditorWindow>()!.CreateScenario();
                    }
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_Add_Desc"]);

                ImGui.SameLine(0, 5);
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FolderOpen)) {
                    var targetPath = _fileManager.ScenarioPath;
                    if (!Directory.Exists(targetPath))
                        Directory.CreateDirectory(targetPath);

                    Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_OpenFolder_Desc"]);
            });

            var scenarioIndex = 0;
            var scenarioFiles = _fileManager.GetScenarioFiles()
                .Where(s => s.MetaData.Title.Contains(_searchScenarioText, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(s => s.MetaData.Location.Territory == state.TerritoryType ? 0 : 1)
                .ThenBy(s => s.MetaData.Location.Territory)
                .ThenBy(s => s.MetaData.Title)
                .ToList();

            scenarioFiles.ForEach((s) => {

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var scenarioEnabled = s.MetaData.Enabled;
                if (ImGui.Checkbox($"##scenarioEnableButton{scenarioIndex}", ref scenarioEnabled)) {
                    var file = _fileManager.LoadScenarioFile(s.FilePath);
                    if (file != null) {
                        file.Enabled = scenarioEnabled;
                        _fileManager.StoreScenarioFile(file, s.FilePath);
                    }
                }

                ImGui.TableNextColumn();

                var territoryData = dataCache.GetTerritoryType((ushort)s.MetaData.Location.Territory);
                var placeName = territoryData.PlaceName.Value.Name.ToString();
                var zoneName = territoryData.PlaceNameZone.Value.Name.ToString();
                var isInCorrectLocation = eventService.CurrentLocation.IsInSameLocation(s.MetaData.Location);

                if (ImGuiComponents.IconButton($"##scenarioMapButton{scenarioIndex}", Dalamud.Interface.FontAwesomeIcon.MapMarker)) {
                    unsafe {
                        AgentMap.Instance()->OpenMap(territoryData.Map.Value.RowId, territoryData.RowId);
                    }
                }
                ImGui.SameLine(0, 10);
                using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpGreen, isInCorrectLocation)) {
                    ImGui.Text(placeName);
                }
                ImGui.TableNextColumn();

                ImGui.Text(s.MetaData.Title);
                if (!string.IsNullOrWhiteSpace(s.MetaData.Description)) {
                    ImGuiComponents.HelpMarker(s.MetaData.Description);
                }

                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton($"##scenarioEditButton{scenarioIndex}", Dalamud.Interface.FontAwesomeIcon.Wrench)) {
                    Plugin.Services.GetService<ScenarioEditorWindow>()!.EditScenario(s.FilePath);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_EditScenario_Desc"]);

                ImGui.SameLine(0, 5);
                var deletePopupId = $"{loc["ListWnd_Scenario_Popup_DeleteScenario_Title"]}##ConfirmDelete{scenarioIndex}";
                if (ImGuiComponents.IconButton($"##scenarioDeleteButton{scenarioIndex}", Dalamud.Interface.FontAwesomeIcon.Trash)) {
                    ImGui.OpenPopup(deletePopupId);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_DeleteScenario_Desc"]);

                ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                if (ImGui.BeginPopupModal(deletePopupId, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) {

                    ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
                    ImGui.TextWrapped(loc["ListWnd_Scenario_Popup_DeleteScenario_Desc", s.MetaData.Title]);
                    ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
                    ImGui.Separator();
                    ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

                    if (ImGui.Button(loc["ListWnd_Scenario_Popup_DeleteScenario_Accept"], new Vector2(120, 0))) {
                        _fileManager.RemoveScenarioFile(s.FilePath);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SetItemDefaultFocus();
                    ImGui.SameLine();
                    if (ImGui.Button(loc["ListWnd_Scenario_Popup_DeleteScenario_Decline"], new Vector2(120, 0))) {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                scenarioIndex++;
            });

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private static void DrawCenteredHeaderCell(int column, Action draw) {
        ImGui.TableSetColumnIndex(column);
        ImGui.PushID(column);
        ArrpGuiAlignment.Center();
        draw();
        ImGui.PopID();
    }
}
