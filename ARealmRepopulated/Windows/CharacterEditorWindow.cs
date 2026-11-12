using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services.Characters;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public class CharacterEditorWindow(
    CharacterFileManager characterFileManager,
    ScenarioFileManager scenarioFileManager,
    ArrpGuiCharacterEditor characterEditor,
    NpcActorPreview appearancePreview,
    ArrpCharacterCreationData characterCreationData,
    ArrpTerritoryData territoryData,
    ArrpEventService eventService,
    IServiceProvider serviceProvider,
    WindowSystem windowSystem,
    WindowPlacement windowPlacement,
    ArrpTranslation loc) : ADalamudWindow("Character Editor###ARealmRepopulatedCharacterEditorWindow") {

    private CharacterData? _character = null;

    public Guid UniqueWindowId { get; } = Guid.NewGuid();

    private static float FooterHeight
        => ImGui.GetStyle().ItemSpacing.Y + 1f + ImGui.GetFrameHeightWithSpacing() + (ArrpGuiSpacing.FooterCellPadding.Y * 2f);

    public static void Open(WindowSystem windowSystem, IServiceProvider serviceProvider, Guid characterId) {
        if (windowSystem.Windows.OfType<CharacterEditorWindow>().FirstOrDefault(w => w.IsOpen && w.IsEditing(characterId)) is { } openEditor) {
            openEditor.BringToFront();
        } else {
            serviceProvider.GetRequiredService<CharacterEditorWindow>().EditCharacter(characterId);
        }
    }

    protected override void SetWindowOptions() {
        this.AllowPinning = false;
        this.AllowClickthrough = false;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(480, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.Size = new Vector2(640, 720);
        this.SizeCondition = ImGuiCond.Appearing;

        this.WindowName = $"Character Editor###{UniqueWindowId}";

        this.OnWindowClosed += appearancePreview.Hide;
        this.OnWindowSafeToRemove += appearancePreview.Dispose;
    }

    public void CreateCharacter() {
        _character = new CharacterData();
        var randomName = characterCreationData.GenerateRandomName(_character.Appearance.Race, _character.Appearance.Tribe, _character.Appearance.Sex);
        _character.Name = $"{randomName.FirstName} {randomName.LastName}";
        IsOpen = true;
        windowPlacement.Place(this, matchHeight: true);
    }

    public void EditCharacter(Guid characterId) {
        if (characterFileManager.GetCharacter(characterId) is not { } character)
            return;

        _character = character.Copy();
        IsOpen = true;
        windowPlacement.Place(this, matchHeight: true);
    }

    public bool IsEditing(Guid characterId)
        => _character?.Id == characterId;

    public override void Draw() {
        if (_character == null)
            return;

        DrawBody(_character);
        DrawFooter(_character);

        if (characterEditor.ShowPreview && !ArrpGuiCharacterEditor.IsExternallyManaged(_character)) {
            appearancePreview.Show(_character.Appearance);
        } else {
            appearancePreview.Hide();
        }
    }

    private void DrawBody(CharacterData character) {
        using var body = ImRaii.Child("##arrpCharacterEditorBody", new Vector2(0, -FooterHeight), true);
        if (!body.Success)
            return;

        var usedBy = scenarioFileManager.GetScenarioFiles()
            .Where(f => !f.IsBroken && f.MetaData.CharacterIds.Contains(character.Id))
            .OrderBy(f => f.MetaData.Title, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        var hasIntegrationName = character.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var integrationName);
        ArrpGuiLayout.PanelHeader("##arrpCharacterPanelHeader", FontAwesomeIcon.User, CharacterLabel(character), DescribeUsage(usedBy.Count),
            trailing: () => characterEditor.DrawPreviewToggle(character),
            titleSuffix: hasIntegrationName ? () => DrawIntegrationNameSuffix(integrationName) : null);

        DrawNameForm(character);
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        using var tabBar = ImRaii.TabBar("##arrpCharacterEditorTabs");
        if (!tabBar.Success)
            return;

        using (var overview = ImRaii.TabItem($"{loc["CharacterEditor_Tab_Overview"]}##arrpCharacterTabOverview", ImGuiTabItemFlags.NoTooltip)) {
            if (overview.Success) {
                ArrpGuiLayout.TabBody("##arrpCharacterTabOverviewBody", () => {
                    DrawUsageSection(usedBy);
                });
            }
        }

        characterEditor.DrawTabs(character);
    }

    private void DrawNameForm(CharacterData character) {
        ArrpGuiForm.Draw("##arrpCharacterNameForm", form => {
            form.Row(loc["ScenarioEditor_ActorData_General_Input_Name"], () => {
                var innerSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
                var name = character.Name;
                ImGui.SetNextItemWidth(-(ImGui.GetFrameHeight() + innerSpacing));
                if (ImGui.InputText("##value", ref name)) {
                    character.Name = name;
                }

                ImGui.SameLine(0, innerSpacing);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Random, new Vector2(ImGui.GetFrameHeight()))) {
                    var randomName = characterCreationData.GenerateRandomName(character.Appearance.Race, character.Appearance.Tribe, character.Appearance.Sex);
                    character.Name = $"{randomName.FirstName} {randomName.LastName}";
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_General_Input_NameRandom"]);
            });

            form.Row(loc["CharacterEditor_Input_Group"], () => {
                var innerSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
                var group = character.Group;
                ImGui.SetNextItemWidth(-(ImGui.GetFrameHeight() + innerSpacing));
                if (ImGui.InputTextWithHint("##value", loc["CharacterEditor_Input_Group_Hint"], ref group)) {
                    character.Group = group;
                }

                ImGui.SameLine(0, innerSpacing);
                using var combo = ImRaii.Combo("##groupSuggestions", string.Empty, ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft);
                if (!combo.Success)
                    return;

                var existingGroups = characterFileManager.GetCharacters()
                    .Select(c => ArrpEntityExtensions.Normalize(c.Group))
                    .Where(g => g.Length > 0)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .Order(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                if (existingGroups.Count == 0)
                    ImGui.TextDisabled(loc["ScenarioEditor_BaseData_Input_Group_None"]);

                foreach (var existing in existingGroups) {
                    if (ImGui.Selectable(existing, existing == character.Group)) {
                        character.Group = existing;
                    }
                }
            }, loc["CharacterEditor_Input_Group_Help"]);
        });
    }

    private static void DrawIntegrationNameSuffix(string integrationName) {
        ImGui.SameLine(0, 0);
        ImGui.Text(" [");
        ImGui.SameLine(0, 0);
        ImGui.TextColored(ArrpGuiColors.ArrpGreen, integrationName);
        ImGui.SameLine(0, 0);
        ImGui.Text("]");
    }

    private void DrawUsageSection(List<ScenarioFileData> usedBy) {
        ArrpGuiLayout.SectionHeader(loc["CharacterEditor_Overview_UsedIn"]);

        if (usedBy.Count == 0) {
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["CharacterEditor_Overview_UsedIn_None"]);
            return;
        }

        using var table = ImRaii.Table("##arrpCharacterUsageTable", 3, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##usageTitle", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##usageLocation", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##usageEdit", ImGuiTableColumnFlags.WidthFixed);

        foreach (var file in usedBy) {
            using var id = ImRaii.PushId(file.FilePath);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(file.MetaData.Title);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.ArrpGreen, eventService.CurrentLocation.IsInSameLocation(file.MetaData.Location))) {
                ImGui.Text(territoryData.GetPlaceName(file.MetaData.Location.Territory));
            }

            ImGui.TableNextColumn();
            if (ImGuiComponents.IconButton("##usageEditButton", FontAwesomeIcon.Wrench)) {
                ScenarioEditorWindow.Open(windowSystem, serviceProvider, file.FilePath);
            }
            ArrpGuiLayout.Tooltip(loc["ListWnd_Scenario_Action_EditScenario_Desc"]);
        }
    }

    private void DrawFooter(CharacterData character) {
        ImGui.Separator();
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.FooterCellPadding);
        using var table = ImRaii.Table("##arrpCharacterEditorFooter", 3, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##arrpFooterSpacer", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##arrpFooterCommit", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##arrpFooterGrip", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_Apply"])) {
            Save(character);
        }

        using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
            ImGui.SameLine(0, 5);
            if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_SaveClose"])) {
                Save(character);
                IsOpen = false;
            }
        }

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed)) {
            ImGui.SameLine(0, 5);
            if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_DiscardClose"])) {
                IsOpen = false;
            }
        }
    }

    private void Save(CharacterData character) {
        character.Group = ArrpEntityExtensions.Normalize(character.Group);
        characterFileManager.Save(character);
    }

    private string CharacterLabel(CharacterData character)
        => string.IsNullOrWhiteSpace(character.Name) ? loc["CharacterEditor_UnnamedCharacter"] : character.Name;

    private string DescribeUsage(int usage) => usage switch {
        0 => loc["CharacterEditor_Usage_None"],
        1 => loc["CharacterEditor_Usage_One"],
        _ => loc["CharacterEditor_Usage", usage]
    };
}
