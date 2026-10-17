using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services.Changelog;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public class ConfigWindow(
    IServiceProvider serviceProvider,
    IDalamudPluginInterface pluginInterface,
    IPluginLog log,
    IClientState state,
    IObjectTable objectTable,
    ChangelogService changelogService,
    ArrpTranslation loc,
    PluginConfig _config,
    ScenarioFileManager _fileManager,
    ScenarioOrchestrator _orchestrator,
    DebugOverlay _debugOverlay,
    ArrpEventService eventService,
    ArrpDtrControl dtrControl,
    ArrpDataCache dataCache) : ADalamudWindow("###ARealmRepopulatedConfigWindow"), IDisposable {

    protected override void SetWindowOptions() {
        Size = new Vector2(750, 630);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoCollapse;
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.CollapsedCondition = ImGuiCond.None;
        loc.OnLocalizationChanged += UpdateWindowTitle;
        UpdateWindowTitle();
    }

    public void Dispose() {
        loc.OnLocalizationChanged -= UpdateWindowTitle;
        GC.SuppressFinalize(this);
    }

    private void UpdateWindowTitle()
        => this.WindowName = $"{loc["ListWnd_Title"]}###ARealmRepopulatedConfigWindow";

    public override void Draw() {
        DrawMainContent();

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        using var t = ImRaii.Table("##WindowControlTable", 3, ImGuiTableFlags.None);
        if (!t.Success)
            return;

        ImGui.TableSetupColumn("##configWindowControlStrech", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##configWindowControlClose", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##configWindowControlPaddingRight", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        DrawScenarioObjectConsumption();

        ImGui.TableNextColumn();
        if (ImGui.Button(loc["ListWnd_Close"])) {
            this.IsOpen = false;
        }
    }

    private bool? ScenarioConditionState(ScenarioFileData file) {
        if (_orchestrator.Orchestrations.FirstOrDefault(o => o.Hash == file.FileHash) is not { } orchestration)
            return null;

        return orchestration.Data.Conditions.Any(c => c.IsConfigured) ? orchestration.AreConditionsMet : null;
    }

    private void DrawScenarioConditionBadge(bool areConditionsMet) {
        ArrpGuiLayout.Badge(Dalamud.Interface.FontAwesomeIcon.Filter,
            areConditionsMet ? ArrpGuiColors.TextColor : ArrpGuiColors.NoteColor,
            loc[areConditionsMet ? "ListWnd_Scenario_ConditionsMet" : "ListWnd_Scenario_ConditionsNotMet"]);

        ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
    }

    private void DrawScenarioObjectConsumption() {
        var totalLoadedScenarios = _orchestrator.ActiveOrchestrations.Count();
        var totalEnabledScenarios = _fileManager.GetScenarioFiles().Count(s => s.MetaData.Enabled && eventService.CurrentLocation.IsInSameLocation(s.MetaData.Location));

        var totalLoadedNpcs = _orchestrator.ActiveOrchestrations.Sum(o => o.Scenario!.Npcs.Count);
        var softLimit = _config.ActorSoftLimit;

        var totalClientObjects = objectTable.ClientObjects.Count();
        var hardLimit = _config.ActorHardLimit;

        var statusColor = totalLoadedNpcs >= softLimit - 10
            ? ArrpGuiColors.ArrpRed
            : totalLoadedNpcs >= softLimit - 50
                ? ArrpGuiColors.ArrpYellow
                : ArrpGuiColors.ArrpGreen;

        using var objectCountTable = ImRaii.Table("##configWindowObjectConsumptionTable", 4);
        if (!objectCountTable.Success)
            return;

        ImGui.TableSetupColumn("##configWindowStatsScenarios", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##configWindowStatsConsumption", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##configWindowStatsSoftLimit", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##configWindowStatsHardLimit", ImGuiTableColumnFlags.WidthFixed);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        ImGui.Text($"{loc["ListWnd_ObjectConsumption_ScenarioCount"]}:  {totalLoadedScenarios} / {totalEnabledScenarios}       {loc["ListWnd_ObjectConsumption_SoftLimit"]}:");

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, statusColor)) {
            ImGui.Text($"{totalLoadedNpcs} / {softLimit}");
        }

        ImGui.TableNextColumn();
        ArrpGuiAlignment.Center();

        ImGuiComponents.HelpMarker($"{loc["ListWnd_ObjectConsumption_HardLimit"]} {totalClientObjects} / {hardLimit}");
    }

    private void DrawMainContent() {
        using var child = ImRaii.Child("##configWindowMainContentArea", new Vector2(0, -55), border: false, flags: ImGuiWindowFlags.NoResize);
        if (!child.Success)
            return;

        using var tabBar = ImRaii.TabBar("##configWindowMainTabBar", ImGuiTabBarFlags.NoTooltip);
        if (!tabBar.Success)
            return;

        using (var scenarioTab = ImRaii.TabItem(loc["ListWnd_Scenario_Title"], ImGuiTabItemFlags.NoTooltip)) {
            if (scenarioTab.Success) {
                try {
                    ScenarioTab();
                } catch (System.Exception ex) {
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"Error loading scenarios: {ex.Message}");
                    log.Error(ex, "huh");
                }
            }
        }

        using (var optionsTab = ImRaii.TabItem(loc["ListWnd_Options_Title"], ImGuiTabItemFlags.NoTooltip)) {
            if (optionsTab.Success) {
                OptionsTab();
            }
        }

        using var infoTab = ImRaii.TabItem(loc["ListWnd_Info_Title"], ImGuiTabItemFlags.NoTooltip);
        if (infoTab.Success) {
            InfoTab();
        }
    }

    private Version? _selectedChangelogVersion;    private void InfoTab() {
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        var entries = changelogService.Changelog.OrderByDescending(c => c.Version).ToList();
        var currentVersion = typeof(Plugin).Assembly.GetName().Version ?? new Version();
        var selected = entries.FirstOrDefault(c => c.Version == _selectedChangelogVersion);

        DrawInfoList(entries, selected, currentVersion);

        ImGui.SameLine();
        using var detail = ImRaii.Child("##infoDetail", Vector2.Zero);
        if (!detail.Success)
            return;

        if (selected is null) {
            DrawHelp();
        } else {
            DrawChangelogEntry(selected);
        }
    }
    
    private void DrawInfoList(List<ChangelogEntry> entries, ChangelogEntry? selected, Version currentVersion) {        
        var guideHeading = loc["ListWnd_Info_Guide"];
        var changelogHeading = loc["ListWnd_Info_Changelog"];
        var helpLabel = loc["ListWnd_Info_Help"];                

        using var list = ImRaii.ListBox("##infoEntries", new Vector2(100f, -1));
        if (!list.Success)
            return;

        ImGui.TextDisabled(guideHeading);
        ImGui.Separator();

        if (ImGui.Selectable($"{helpLabel}##infoHelp", selected is null)) {
            _selectedChangelogVersion = null;
        }

        if (entries.Count == 0)
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        ImGui.TextDisabled(changelogHeading);
        ImGui.Separator();

        foreach (var entry in entries) {
            var isCurrent = entry.Version == currentVersion;
            using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpGreen, isCurrent)) {
                if (ImGui.Selectable($"{entry.Version}##changelogVersion", entry == selected)) {
                    _selectedChangelogVersion = entry.Version;
                }
            }

            if (isCurrent && entry != entries[^1]) {
                ImGui.Separator();
            }
        }
    }

    private void DrawHelp() {
        ArrpGuiLayout.HeaderRow("##infoHelpHeader", () => ImGui.TextColored(ArrpGuiColors.ArrpYellow, loc["ListWnd_Info_Help_Title"]));
        ImGui.TextWrapped(loc["ListWnd_Info_Help_Intro"]);

        ArrpGuiLayout.SectionHeader(loc["ListWnd_Info_Help_QuickStart"]);
        string[] quickStartSteps = [
            "ListWnd_Info_Help_QuickStart_Create",
            "ListWnd_Info_Help_QuickStart_Edit",
            "ListWnd_Info_Help_QuickStart_Load",
            "ListWnd_Info_Help_QuickStart_Command",
        ];
        foreach (var step in quickStartSteps) {
            ImGui.Bullet();
            ImGui.TextWrapped(loc[step]);
        }

        ArrpGuiLayout.SectionHeader(loc["ListWnd_Info_Help_Wiki"]);
        ImGui.TextWrapped(loc["ListWnd_Info_Help_Wiki_Desc"]);
    
        if (pluginInterface.Manifest.RepoUrl is { Length: > 0 } repoUrl) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Book, loc["ListWnd_Info_Help_OpenWiki"])) {
                Util.OpenLink($"{repoUrl.TrimEnd('/')}/wiki");
            }
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
    }

    private static void DrawChangelogEntry(ChangelogEntry entry) {
        ArrpGuiLayout.HeaderRow("##changelogDetailHeader", () => ImGui.TextColored(ArrpGuiColors.ArrpYellow, entry.Title));

        if (!string.IsNullOrWhiteSpace(entry.Description)) {
            ImGui.TextWrapped(entry.Description);
        }

        foreach (var section in entry.Sections) {
            ArrpGuiLayout.SectionHeader(section.Title);
            foreach (var change in section.Changes) {
                ImGui.Bullet();
                ImGui.TextWrapped(change);
            }
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
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
            _config.EnableScenarioDebugOverlay = scenarioDebugOverlay;
            _config.Save();

            _debugOverlay.ValidateHook();
        }
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Options_DebugOverlay_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        var actorSoftLimit = _config.ActorSoftLimit;

        ImGui.Text(loc["ListWnd_Options_SoftLimit_Option"]);
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Options_SoftLimit_Desc", _config.ActorHardLimit]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        ImGui.Text(PluginConfig.ActorSoftLimitMin.ToString());
        ImGui.SameLine();

        if (ImGui.SliderInt("##sliderSoftLimit", ref actorSoftLimit, PluginConfig.ActorSoftLimitMin, PluginConfig.ActorSoftLimitMax)) {
            _config.ActorSoftLimit = actorSoftLimit;
            _config.Save();
        }
        ImGui.SameLine();
        ImGui.Text(PluginConfig.ActorSoftLimitMax.ToString());

    }

    private string _searchScenarioText = string.Empty;
    private bool _displayCurrentLocationOnly = true;
    private void ScenarioTab() {

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Scenario_Desc"]);
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();

        using (var t = ImRaii.Table("##SearchTable", 1)) {
            if (!t.Success)
                return;
            // Add search box for territories when able.
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##SearchScenarios", loc["ListWnd_Scenario_Search_Placeholder"], ref _searchScenarioText);
        }

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        using (var scenarioTable = ImRaii.Table("##AvailableScenarioTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) {
            if (!scenarioTable.Success)
                return;

            ImGui.TableSetupColumn("##scenarioTitleHeader", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##scenarioLocationHeader", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("##scenarioToolbarHeader", ImGuiTableColumnFlags.WidthFixed);

            ImGui.TableSetupScrollFreeze(0, 1);            
            ImGui.TableNextRow();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));

            ArrpGuiHelper.DrawCenteredHeaderCell(0, () => {                
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetTreeNodeToLabelSpacing());
                using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpYellow, _orchestrator.HasPendingChanges)) {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Recycle)) {
                        _orchestrator.Reload();
                    }
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(_orchestrator.HasPendingChanges
                        ? $"{loc["ListWnd_Scenario_Action_Scan_Desc"]}\n\n{loc["ListWnd_Scenario_Action_Scan_Pending"]}"
                        : loc["ListWnd_Scenario_Action_Scan_Desc"]);
                }

                ImGui.SameLine();
                ImGui.Text(loc["ListWnd_Scenario_Header_Title"]);
            });
            ArrpGuiHelper.DrawCenteredHeaderCell(1, () => {
                ImGui.Checkbox("##scenarioLocationHeaderScoped", ref _displayCurrentLocationOnly);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Header_Location_Scoped_Desc"]);
                }

                ImGui.SameLine();
                ImGui.Text(loc["ListWnd_Scenario_Header_Location"]);
            });
            ArrpGuiHelper.DrawCenteredHeaderCell(2, () => {
                using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                        serviceProvider.GetService<ScenarioEditorWindow>()!.CreateScenario();
                    }
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_Add_Desc"]);

                ImGui.SameLine(0, 5);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.FolderOpen)) {
                    var targetPath = _fileManager.ScenarioPath;
                    if (!Directory.Exists(targetPath))
                        Directory.CreateDirectory(targetPath);

                    Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(loc["ListWnd_Scenario_Action_OpenFolder_Desc"]);
            });

            var scenarioFiles = _fileManager.GetScenarioFiles()
                .Where(s => !_displayCurrentLocationOnly || eventService.CurrentLocation.IsInSameLocation(s.MetaData.Location))
                .OrderBy(s => s.MetaData.Location.Territory == state.TerritoryType ? 0 : 1)
                .ThenBy(s => s.MetaData.Location.Territory)
                .ThenBy(s => s.MetaData.Title)
                .Select(s => new GroupedScenarioFile(s, s.MetaData.GroupSegments()))
                .Where(f => f.File.MetaData.Title.Contains(_searchScenarioText, StringComparison.InvariantCultureIgnoreCase)
                    || string.Join('/', f.Segments).Contains(_searchScenarioText, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            DrawScenarioGroupLevel(scenarioFiles, 0);
        }
    }

    private void DrawScenarioGroupLevel(List<GroupedScenarioFile> files, int depth) {
        var groups = files
            .Where(f => f.Segments.Length > depth)
            .GroupBy(f => f.Segments[depth], StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.InvariantCultureIgnoreCase);

        foreach (var group in groups) {
            var groupFiles = group.ToList();
            using var id = ImRaii.PushId(group.Key);
            if (!DrawGroupRow(group.Key, groupFiles))
                continue;

            using var indent = ImRaii.PushIndent();
            DrawScenarioGroupLevel(groupFiles, depth + 1);
        }

        foreach (var file in files.Where(f => f.Segments.Length == depth)) {
            DrawScenarioRow(file.File);
        }
    }
    
    private bool DrawGroupRow(string name, List<GroupedScenarioFile> groupFiles) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        // uwu whats this? a state storage in imgui? 
        var storage = ImGui.GetStateStorage();
        var openKey = ImGui.GetID("##open");
        if (DrawRowHighlight())
            storage.SetBool(openKey, !storage.GetBool(openKey));

        var isOpen = !string.IsNullOrEmpty(_searchScenarioText) || storage.GetBool(openKey);
        var startX = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.TextDisabled((isOpen ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString());
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + ImGui.GetTreeNodeToLabelSpacing());

        var enabledCount = groupFiles.Count(f => f.File.MetaData.Enabled);
        var groupEnabled = enabledCount == groupFiles.Count;
        ImGuiP.PushItemFlag(ImGuiItemFlags.MixedValue, enabledCount > 0 && !groupEnabled);
        if (ImGui.Checkbox("##scenarioGroupEnableButton", ref groupEnabled)) {
            groupFiles.Where(f => f.File.MetaData.Enabled != groupEnabled).ToList().ForEach(f => SetScenarioEnabled(f.File, groupEnabled));
        }
        ImGuiP.PopItemFlag();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ListWnd_Scenario_Group_Enable_Desc"]);

        ImGui.SameLine();
        ImGui.Text(name);
        ImGui.SameLine();
        ImGui.TextDisabled($"({groupFiles.Count})");

        return isOpen;
    }

    private void DrawScenarioRow(ScenarioFileData s) {
        using var id = ImRaii.PushId(s.FilePath);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawRowHighlight();
        
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetTreeNodeToLabelSpacing());
        var scenarioEnabled = s.MetaData.Enabled;
        if (ImGui.Checkbox("##scenarioEnableButton", ref scenarioEnabled)) {
            SetScenarioEnabled(s, scenarioEnabled);
        }

        ImGui.SameLine();
        if (_orchestrator.Orchestrations.FirstOrDefault(o => o.Hash == s.FileHash) is { Scenario: { Statistics.IsLoopingTooFast: true } fastLoopingScenario }) {
            ArrpGuiLayout.Badge(FontAwesomeIcon.ExclamationTriangle, ArrpGuiColors.ArrpYellow,
                loc["ListWnd_Scenario_LoopingTooFast", fastLoopingScenario.Statistics.AverageRunTime.TotalSeconds.ToString("0.00")]);
            ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
        }

        var conditionState = ScenarioConditionState(s);
        if (conditionState.HasValue) {
            DrawScenarioConditionBadge(conditionState.Value);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.NoteColor, conditionState == false)) {
            ImGui.Text(s.MetaData.Title);
        }

        if (!string.IsNullOrWhiteSpace(s.MetaData.Description)) {
            ImGuiComponents.HelpMarker(s.MetaData.Description);
        }

        ImGui.TableNextColumn();
        var territoryData = dataCache.GetTerritoryType((ushort)s.MetaData.Location.Territory);
        if (ImGuiComponents.IconButton("##scenarioMapButton", FontAwesomeIcon.MapMarker)) {
            unsafe {
                AgentMap.Instance()->OpenMap(territoryData.Map.Value.RowId, territoryData.RowId);
            }
        }
        ImGui.SameLine(0, 10);
        using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpGreen, eventService.CurrentLocation.IsInSameLocation(s.MetaData.Location))) {
            var placeName = territoryData.PlaceName.Value.Name.ToString();
            if (placeName.Length <= 15) {
                ImGui.Text(placeName);
            } else {
                ImGui.Text($"{placeName[..15]}...");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(placeName);
            }
        }

        ImGui.TableNextColumn();
        if (ImGuiComponents.IconButton("##scenarioEditButton", FontAwesomeIcon.Wrench)) {
            serviceProvider.GetService<ScenarioEditorWindow>()!.EditScenario(s.FilePath);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ListWnd_Scenario_Action_EditScenario_Desc"]);

        ImGui.SameLine(0, 5);
        var deletePopupId = $"{loc["ListWnd_Scenario_Popup_DeleteScenario_Title"]}##ConfirmDelete";
        if (ImGuiComponents.IconButton("##scenarioDeleteButton", FontAwesomeIcon.Trash)) {
            ImGui.OpenPopup(deletePopupId);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ListWnd_Scenario_Action_DeleteScenario_Desc"]);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        using var modal = ImRaii.PopupModal(deletePopupId, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        if (modal.Success) {
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
        }
    }

    private static bool DrawRowHighlight() {
        var cursor = ImGui.GetCursorPos();
        bool isClicked;
        using (ImRaii.PushColor(ImGuiCol.HeaderHovered, Vector4.Zero).Push(ImGuiCol.HeaderActive, Vector4.Zero)) {
            isClicked = ImGui.Selectable("##row", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0, ImGui.GetFrameHeight()));
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped))
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(ImGuiCol.HeaderHovered));

        ImGui.SetCursorPos(cursor);
        return isClicked;
    }

    private void SetScenarioEnabled(ScenarioFileData s, bool enabled) {
        if (_fileManager.LoadScenarioFile(s.FilePath) is not { } file)
            return;

        file.Enabled = enabled;
        _fileManager.StoreScenarioFile(file, s.FilePath);
    }

    private readonly record struct GroupedScenarioFile(ScenarioFileData File, string[] Segments);
}
