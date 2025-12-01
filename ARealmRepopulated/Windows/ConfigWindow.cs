using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.File;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public class ConfigWindow(    
    IPluginLog log,
    IClientState state,
    PluginConfig _config, 
    ScenarioFileManager _fileManager,  
    DebugOverlay _debugOverlay,
    ArrpDtrControl dtrControl,
    ArrpDataCache dataCache) : ADalamudWindow("ARealmRepopulated Configuration###ARealmRepopulatedConfigWindow"), IDisposable
{    

    protected override void SetWindowOptions() {
        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoCollapse;
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.CollapsedCondition = ImGuiCond.None;
    }

    public void Dispose() { }

    public override void Draw()
    {

        if (ImGui.BeginChild("", new Vector2(0, -50), border: false, flags: ImGuiWindowFlags.NoResize))
        {

            if (ImGui.BeginTabBar("", ImGuiTabBarFlags.NoTooltip))
            {
                if (ImGui.BeginTabItem("Scenarios", ImGuiTabItemFlags.NoTooltip))
                {

                    try
                    {
                        ScenarioTab();
                    }
                    catch (System.Exception ex)
                    {
                        ImGui.TextColored(ImGuiColors.DalamudRed, $"Error loading scenarios: {ex.Message}");
                        log.Error(ex, "huh");
                    }
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Options", ImGuiTabItemFlags.NoTooltip))
                {
                    OptionsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            ImGui.EndChild();
        }


        ImGui.Separator();
        if (ImGui.BeginTable("##WindowControlTable", 3))
        {
            ImGui.TableSetupColumn("##configWindowControlStrech", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##configWindowControlClose", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##configWindowControlPadding", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            if (ImGui.Button("Close Configuration"))
            {
                this.IsOpen = false;
            }
            ImGui.EndTable();
        }

    }

    private void OptionsTab()
    {
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var autoLoadScenarios = _config.AutoLoadScenarios;
        if (ImGui.Checkbox("Auto load scenarios", ref autoLoadScenarios))
        {
            _config.AutoLoadScenarios = autoLoadScenarios;
            _config.Save();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped("Automatically loads available scenarios on start. Also keeps track of the file status and reloads if changes to the files are detected.");

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var showDtrEntry = _config.ShowInDtrBar;
        if (ImGui.Checkbox("Show DTR Entry", ref showDtrEntry))
        {
            _config.ShowInDtrBar = showDtrEntry;
            _config.Save();

            dtrControl.UpdateVisibility();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped("Displays a clickable entry in the DTR bar. It provides quick access to the configuration menu and displays the count of loaded scenarios in the current area.");

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var scenarioDebugOverlay = _config.EnableScenarioDebugOverlay;
        if (ImGui.Checkbox("Enable debug overlay", ref scenarioDebugOverlay))
        {
            if (scenarioDebugOverlay)
            {
                _debugOverlay.Hook();
            } 
            else
            {
                _debugOverlay.Unhook();
            }

            _config.EnableScenarioDebugOverlay = scenarioDebugOverlay;
            _config.Save();            
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped("The debug overlay is used to display the scenario actor paths when editing a specific scenario. To give an example: Without it you dont have access to the drag and drop functionallities for movement nodes.\nYou can disable this option if you not plan on editing the scenarios.");
    }

    private string _searchScenarioText = string.Empty;
    private void ScenarioTab()
    {

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        using (ImRaii.Disabled())
            ImGui.TextWrapped("Manage your custom scenarios in this section. You can create, edit and delete scenarios as you like. Please be aware that having too many scenarios and actors at the same time in the same location might impact performance. The scenarios are stored as files in the 'scenarios' subfolder located in the plugin configuration directory.");
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();

        if (ImGui.BeginTable("##SearchTable", 1))
        {
            // Add search box for territories when able.
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##SearchScenarios", "Search ... ", ref _searchScenarioText);        
            ImGui.EndTable();
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        if (ImGui.BeginTable("##AvailableScenarioTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("##scenarioRefreshHeader", ImGuiTableColumnFlags.WidthFixed, 25);            
            ImGui.TableSetupColumn("##scenarioLocationHeader", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioTitleHeader", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##scenarioToolbarHeader", ImGuiTableColumnFlags.WidthFixed);
            
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();


            DrawCenteredHeaderCell(0, () => {
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Recycle))
                {
                    _fileManager.ScanScenarioFiles();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Rescan the available scenario files");
            });
            DrawCenteredHeaderCell(1, () => ImGui.Text("Location"));
            DrawCenteredHeaderCell(2, () => ImGui.Text("Scenario"));
            DrawCenteredHeaderCell(3, () => {                
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Plus))
                {
                    Plugin.Services.GetService<ScenarioEditorWindow>()!.CreateScenario();                    
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Add a new scenario");

                ImGui.SameLine(0, 5);
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FolderOpen))
                {
                    var targetPath = _fileManager.ScenarioPath;
                    if (!Directory.Exists(targetPath))
                        Directory.CreateDirectory(targetPath);

                    Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Opens the scenario file folder");
            });

            var scenarioIndex = 0;
            var scenarioFiles = _fileManager.GetScenarioFiles()
                .Where(s => s.MetaData.Title.Contains(_searchScenarioText, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(s => s.MetaData.TerritoryId == state.TerritoryType ? 0 : 1)
                .ThenBy(s => s.MetaData.TerritoryId)
                .ThenBy(s => s.MetaData.Title)
                .ToList();
            
            scenarioFiles.ForEach((s) =>
            {
                                
                ImGui.TableNextRow();                               
                ImGui.TableNextColumn();
                var scenarioEnabled = s.MetaData.Enabled;
                if (ImGui.Checkbox($"##scenarioEnableButton{scenarioIndex}", ref scenarioEnabled))
                {
                    var file = _fileManager.LoadScenarioFile(s.FilePath);
                    if (file != null)
                    {
                        file.Enabled = scenarioEnabled;
                        _fileManager.StoreScenarioFile(file, s.FilePath);
                    }
                }
                
                ImGui.TableNextColumn();

                var territoryData = dataCache.GetTerritoryType((ushort)s.MetaData.TerritoryId);
                
                var placeName = territoryData.PlaceName.Value.Name.ToString();                
                var zoneName = territoryData.PlaceNameZone.Value.Name.ToString();

                
                if (ImGuiComponents.IconButton($"##scenarioMapButton{scenarioIndex}", Dalamud.Interface.FontAwesomeIcon.MapMarker))
                {
                    unsafe
                    {
                        AgentMap.Instance()->OpenMap(territoryData.Map.Value.RowId, territoryData.RowId);
                    }
                }
                ImGui.SameLine(0,10);
                using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpGreen, s.MetaData.TerritoryId == state.TerritoryType))
                {
                    ImGui.Text(placeName);                                                                            
                }
                ImGui.TableNextColumn();

                ImGui.Text(s.MetaData.Title);
                if (!string.IsNullOrWhiteSpace(s.MetaData.Description))
                {
                    ImGuiComponents.HelpMarker(s.MetaData.Description);
                }

                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton($"##scenarioEditButton{scenarioIndex}",Dalamud.Interface.FontAwesomeIcon.Wrench))
                {
                    Plugin.Services.GetService<ScenarioEditorWindow>()!.EditScenario(s.FilePath);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Edit this scenario");

                ImGui.SameLine(0, 5);
                string deletePopupId = $"Confirm Delete##ConfirmDelete{scenarioIndex}";
                if (ImGuiComponents.IconButton($"##scenarioDeleteButton{scenarioIndex}", Dalamud.Interface.FontAwesomeIcon.Trash))
                {
                    ImGui.OpenPopup(deletePopupId);                    
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Delete this scenario");

                                
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                if (ImGui.BeginPopupModal(deletePopupId, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
                {

                    ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
                    ImGui.TextWrapped($"Are you sure you want to delete the scenario '{s.MetaData.Title}'");
                    ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
                    ImGui.Separator();
                    ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

                    if (ImGui.Button("Yes", new Vector2(120, 0)))
                    {
                        _fileManager.RemoveScenarioFile(s.FilePath);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SetItemDefaultFocus();
                    ImGui.SameLine();
                    if (ImGui.Button("No", new Vector2(120, 0)))
                    {
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

    private static void DrawCenteredHeaderCell(int column, Action draw)
    {
        ImGui.TableSetColumnIndex(column);
        ImGui.PushID(column);        
        ArrpGuiAlignment.Center();
        draw();
        ImGui.PopID();
    }

    
}
