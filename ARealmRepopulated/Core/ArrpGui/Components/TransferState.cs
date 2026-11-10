using Dalamud.Interface;
using Dalamud.Interface.Colors;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class TransferState {
    public bool? Result { get; private set; } = null;
    public DateTime Timeout { get; private set; } = DateTime.MinValue;
    public FontAwesomeIcon DefaultIcon { get; set; } = FontAwesomeIcon.Question;

    public void CheckState(out FontAwesomeIcon stateIcon, out Vector4? stateColor) {
        if (Result == null) {
            stateIcon = DefaultIcon;
            stateColor = null;
            return;
        }

        if (DateTime.Now > Timeout) {
            Result = null;
            Timeout = DateTime.MinValue;
            stateIcon = DefaultIcon;
            stateColor = null;
            return;
        }

        stateIcon = Result == true ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.ExclamationCircle;
        stateColor = Result == true ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed;
    }

    public void SetResult(bool state) {
        Result = state;
        Timeout = DateTime.Now.AddSeconds(2);
    }
}
