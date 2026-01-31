using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public class OnboardingWindow(
    IObjectTable objects,
    ArrpTranslation loc,
    ArrpCharacterCreationData charData,
    ArrpEventService eventService,
    ScenarioFileManager fileManager,
    Plugin plugin,
    PluginConfig config) : ADalamudWindow("###ARealmRepopulatedOnboardingWindow") {
    protected override void SetWindowOptions() {
        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoCollapse;
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.CollapsedCondition = ImGuiCond.None;
        this.OnWindowClosed += () => {
            config.OnboardingCompleted = true;
            config.Save();
        };
        loc.OnLocalizationChanged += UpdateWindowTitle;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
        => this.WindowName = $"{loc["OnboardingWnd_Title"]}###ARealmRepopulatedOnboardingWindow";

    private bool _demoNpcSpawned = false;
    public override void Draw() {

        if (ImGui.BeginChild("", new Vector2(0, -50), border: false, flags: ImGuiWindowFlags.NoResize)) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
            ImGui.TextWrapped(loc["OnboardingWnd_Header"]);
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled()) {
                ImGui.TextWrapped(loc["OnboardingWnd_Intro"]);
                ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
                ImGui.TextWrapped(loc["OnboardingWnd_SpawnDesc"]);
            }
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

            if (!_demoNpcSpawned) {
                if (ImGui.Button(loc["OnboardingWnd_Spawn"])) {
                    _demoNpcSpawned = true;
                    SpawnDemoScenario();
                }
            } else {
                ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.CheckCircle, loc["OnboardingWnd_SpawnSuccess"], defaultColor: ArrpGuiColors.ArrpGreen, hoveredColor: ArrpGuiColors.ArrpGreen);
            }

            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled()) {
                ImGui.TextWrapped(loc["OnboardingWnd_SettingsDesc"]);
                ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            }
            if (ImGui.Button(loc["OnboardingWnd_Settings"])) {
                plugin.ToggleConfigUI();
            }

            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled()) {
                ImGui.TextWrapped(loc["OnboardingWnd_Outro"]);
            }

            ImGui.EndChild();
        }

        ImGui.Separator();
        if (ImGui.BeginTable("##onboardingWindowControlTable", 3, ImGuiTableFlags.NoSavedSettings)) {
            ImGui.TableSetupColumn("##onboardingWindowControlTableStrech", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##onboardingWindowControlTableClose", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##onboardingWindowControlTablePadding", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            if (ImGui.Button(loc["OnboardingWnd_Close"])) {
                this.IsOpen = false;
            }
            ImGui.EndTable();
        }

    }

    private void SpawnDemoScenario() {
        var playerPos = objects.LocalPlayer!.Position;
        var playerYaw = objects.LocalPlayer.Rotation;

        var npcPos = playerPos.Forward(playerYaw, 1.5f);
        var npcYaw = npcPos.AngleTowards(playerPos);

        var scenario = new ScenarioData { Description = loc["OnboardingWnd_Scenario_Desc"], Title = loc["OnboardingWnd_Scenario_Title"], Looping = true, LoopDelay = 5 };
        eventService.CurrentLocation.UpdateScenarioLocation(scenario.Location);

        var introductionNpc = new ScenarioNpcData {
            Appearance = NpcAppearanceData.Default,
            Position = npcPos,
            Rotation = npcYaw,
        };
        var onboardingNpcName = charData.GenerateRandomName(introductionNpc.Appearance.Race, introductionNpc.Appearance.Tribe, introductionNpc.Appearance.Sex);
        introductionNpc.Name = $"{onboardingNpcName.FirstName} {onboardingNpcName.LastName}";

        introductionNpc.Actions.Add(new ScenarioNpcSpawnAction { });
        introductionNpc.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2 });
        introductionNpc.Actions.Add(new ScenarioNpcEmoteAction { Emote = 16, NpcTalk = loc["OnboardingWnd_Scenario_Npc_Greeting"] });
        introductionNpc.Actions.Add(new ScenarioNpcEmoteAction { Emote = 5, NpcTalk = loc["OnboardingWnd_Scenario_Npc_Intro", introductionNpc.Name], Loop = false, Duration = 7 });
        introductionNpc.Actions.Add(new ScenarioNpcWaitingAction { Duration = 7, NpcTalk = loc["OnboardingWnd_Scenario_Npc_Explain1"] });
        introductionNpc.Actions.Add(new ScenarioNpcWaitingAction { Duration = 7, NpcTalk = loc["OnboardingWnd_Scenario_Npc_Explain2"] });
        introductionNpc.Actions.Add(new ScenarioNpcEmoteAction { Emote = 18, NpcTalk = loc["OnboardingWnd_Scenario_Npc_Outro"] });
        introductionNpc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = playerPos.Forward(playerYaw, 10f), Speed = NpcSpeed.Running });
        introductionNpc.Actions.Add(new ScenarioNpcDespawnAction { });

        scenario.Npcs.Add(introductionNpc);

        fileManager.StoreScenarioFile(scenario);
    }
}
