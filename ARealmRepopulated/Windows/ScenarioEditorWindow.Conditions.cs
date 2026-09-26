using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private const string AddConditionPopupId = "##arrpScenarioEditorAddCondition";
    private const int WeatherPickerRows = 6;

    private void DrawScenarioConditionsTab() {
        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);

        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_Conditions_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, loc["ScenarioEditor_Conditions_Add"])) {
            ImGui.OpenPopup(AddConditionPopupId);
        }

        using (var popup = ImRaii.Popup(AddConditionPopupId)) {
            if (popup.Success)
                DrawConditionSelection();
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        
        var bodyPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, ArrpGuiSpacing.ScrollBodyPadding);
        using var list = ImRaii.Child("##arrpScenarioTabConditionsBody", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding);
        bodyPadding.Dispose();

        if (!list.Success)
            return;

        if (ScenarioObject.Conditions.Count == 0) {
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_Conditions_Empty"]);
            return;
        }

        ScenarioCondition? conditionToRemove = null;
        for (var i = 0; i < ScenarioObject.Conditions.Count; i++) {
            using var id = ImRaii.PushId(i);

            if (DrawCondition(ScenarioObject.Conditions[i]))
                conditionToRemove = ScenarioObject.Conditions[i];
        }

        if (conditionToRemove != null)
            ScenarioObject.Conditions.Remove(conditionToRemove);
    }

    private void DrawConditionSelection() {
        foreach (var condition in _conditionUiRegistry.CreateAll()) {
            DrawConditionSelectionEntry(condition);
        }
    }

    private void DrawConditionSelectionEntry(ScenarioCondition condition) {
        if (ImGui.Selectable(_conditionUiRegistry.GetShortName(condition))) {
            ScenarioObject.Conditions.Add(condition);
        }
        ArrpGuiLayout.Tooltip(_conditionUiRegistry.GetHelp(condition));
    }
    
    private bool DrawCondition(ScenarioCondition condition) {
        var remove = false;

        ArrpGuiLayout.PanelHeader("##arrpConditionHeader",
            _conditionUiRegistry.GetIcon(condition),
            _conditionUiRegistry.GetShortName(condition),
            _conditionUiRegistry.GetSummary(condition),
            trailing: () => {
                DrawConditionNegateToggle(condition);
                ImGui.SameLine(0, ArrpGuiSpacing.ButtonGroupSpacing);
                remove = DrawConditionDeleteAction();
            });

        using (ImRaii.Disabled())
            ImGui.TextWrapped(_conditionUiRegistry.GetHelp(condition));

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        if (!condition.IsConfigured) {
            ArrpGuiLayout.IconNote(FontAwesomeIcon.ExclamationTriangle, ArrpGuiColors.ArrpYellow, loc["ScenarioEditor_Conditions_Unconfigured"]);
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        }

        _conditionUiRegistry.Draw(condition);

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        return remove;
    }

    private bool DrawConditionDeleteAction() {
        var remove = ImGuiComponents.IconButton("##arrpConditionDelete", FontAwesomeIcon.Trash);
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Conditions_Delete"]);
        return remove;
    }

    private void DrawEorzeaTimeCondition(ScenarioEorzeaTimeCondition condition) {
        ArrpGuiForm.Draw("##arrpConditionTimeForm", form => {

            form.Row(loc["ScenarioEditor_Conditions_CTime_Input_From"], () => {
                using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                var startHour = condition.StartHour;
                if (ImGui.SliderInt("##value", ref startHour, 0, 23, "%02d:00")) {
                    condition.StartHour = startHour;
                }
            });

            form.Row(loc["ScenarioEditor_Conditions_CTime_Input_Until"], () => {
                using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                var endHour = condition.EndHour;
                if (ImGui.SliderInt("##value", ref endHour, 0, 23, "%02d:00")) {
                    condition.EndHour = endHour;
                }
            }, help: loc["ScenarioEditor_Conditions_CTime_Input_UntilHint"]);
        });
    }

    private void DrawWeatherCondition(ScenarioWeatherCondition condition) {
        var weathers = dataCache.GetWeathersForTerritory(ScenarioObject.Location.Territory);

        using (var list = ImRaii.Child("##arrpConditionWeatherList", new Vector2(0, ImGui.GetFrameHeightWithSpacing() * WeatherPickerRows), true)) {
            if (list.Success) {
                foreach (var weather in weathers) {
                    var weatherId = (byte)weather.RowId;
                    var selected = condition.WeatherIds.Contains(weatherId);

                    if (!ImGui.Checkbox($"{weather.Name}##arrpConditionWeather{weatherId}", ref selected))
                        continue;

                    if (selected) {
                        condition.WeatherIds.Add(weatherId);
                    } else {
                        condition.WeatherIds.Remove(weatherId);
                    }
                }
            }
        }
    }

    private void DrawChanceCondition(ScenarioChanceCondition condition) {
        ArrpGuiForm.Draw("##arrpConditionChanceForm", form => {

            form.Row(loc["ScenarioEditor_Conditions_CChance_Input_Percent"], () => {
                using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                var percent = condition.Percent;
                if (ImGui.SliderFloat("##value", ref percent, 0f, 100f, "%.0f %%")) {
                    condition.Percent = Math.Clamp(percent, 0f, 100f);
                }
            });
        });
    }

    private void DrawConditionNegateToggle(ScenarioCondition condition) {
        var negate = condition.Negate;
        if (ImGui.Checkbox(loc["ScenarioEditor_Conditions_Input_Negate"], ref negate)) {
            condition.Negate = negate;
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Conditions_Input_NegateHint"]);
    }

    private void RegisterConditionUi() {
        _conditionUiRegistry.Register(
            create: () => new ScenarioEorzeaTimeCondition(),
            icon: FontAwesomeIcon.Clock,
            shortName: (c) => loc["ScenarioEditor_Conditions_CTime_Short"],
            help: (c) => loc["ScenarioEditor_Conditions_CTime_Desc"],
            summary: DescribeHourWindow,
            draw: DrawEorzeaTimeCondition
        );
        _conditionUiRegistry.Register(
            create: () => new ScenarioWeatherCondition(),
            icon: FontAwesomeIcon.CloudSun,
            shortName: (c) => loc["ScenarioEditor_Conditions_CWeather_Short"],
            help: (c) => loc["ScenarioEditor_Conditions_CWeather_Desc"],
            summary: DescribeWeathers,
            draw: DrawWeatherCondition
        );
        _conditionUiRegistry.Register(
            create: () => new ScenarioChanceCondition(),
            icon: FontAwesomeIcon.Dice,
            shortName: (c) => loc["ScenarioEditor_Conditions_CChance_Short"],
            help: (c) => loc["ScenarioEditor_Conditions_CChance_Desc"],
            summary: (c) => $"{(c.Negate ? 100f - c.Percent : c.Percent):0} %",
            draw: DrawChanceCondition
        );
    }

    private string DescribeHourWindow(ScenarioEorzeaTimeCondition condition) {
        var window = $"{condition.StartHour:00}:00 - {condition.EndHour:00}:00";
        return condition.Negate ? loc["ScenarioEditor_Conditions_CTime_SummaryNegated", window] : window;
    }

    private List<ScenarioCondition> ConfiguredConditions()
        => [.. ScenarioObject.Conditions.Where(c => c.IsConfigured)];
    
    private string DescribeConditions(List<ScenarioCondition> conditions)
        => string.Join(", ", conditions.Select(_conditionUiRegistry.GetSummary));

    private string DescribeWeathers(ScenarioWeatherCondition condition) {
        if (condition.WeatherIds.Count == 0)
            return string.Empty;

        var weathers = string.Join(", ", condition.WeatherIds
            .Select(dataCache.GetWeather)
            .Where(w => w != null)
            .Select(w => w!.Value.Name.ToString()));

        return condition.Negate ? loc["ScenarioEditor_Conditions_CWeather_SummaryNegated", weathers] : weathers;
    }
}

public sealed class ConditionUiRegistry {
    public class ConditionTypeDisplayObject {
        public FontAwesomeIcon Icon { get; set; } = FontAwesomeIcon.Question;
        public Func<ScenarioCondition> Create { get; set; } = null!;
        public Action<ScenarioCondition> Draw { get; set; } = (_) => { };
        public Func<ScenarioCondition, string> NameResolver { get; set; } = (_) => string.Empty;
        public Func<ScenarioCondition, string> HelpResolver { get; set; } = (_) => string.Empty;
        public Func<ScenarioCondition, string> SummaryResolver { get; set; } = (_) => string.Empty;
    }

    private readonly Dictionary<Type, ConditionTypeDisplayObject> _handlers = [];    
    private readonly List<Type> _registrationOrder = [];

    public void Register<T>(Func<T> create, FontAwesomeIcon icon, Func<T, string>? shortName = null, Func<T, string>? help = null, Func<T, string>? summary = null, Action<T>? draw = null) where T : ScenarioCondition {
        if (!_handlers.ContainsKey(typeof(T)))
            _registrationOrder.Add(typeof(T));

        _handlers[typeof(T)] = new ConditionTypeDisplayObject {
            Icon = icon,
            Create = () => create(),
            NameResolver = c => shortName != null ? shortName((T)c) : string.Empty,
            HelpResolver = c => help != null ? help((T)c) : string.Empty,
            SummaryResolver = c => summary != null ? summary((T)c) : string.Empty,
            Draw = c => draw?.Invoke((T)c)
        };
    }

    public IEnumerable<ScenarioCondition> CreateAll()
        => _registrationOrder.Select(t => _handlers[t].Create());

    public FontAwesomeIcon GetIcon(ScenarioCondition condition)
        => _handlers[condition.GetType()].Icon;

    public string GetShortName(ScenarioCondition condition)
        => _handlers[condition.GetType()].NameResolver(condition);

    public string GetHelp(ScenarioCondition condition)
        => _handlers[condition.GetType()].HelpResolver(condition);

    public string GetSummary(ScenarioCondition condition)
        => _handlers[condition.GetType()].SummaryResolver(condition);

    public void Draw(ScenarioCondition condition)
        => _handlers[condition.GetType()].Draw(condition);
}
