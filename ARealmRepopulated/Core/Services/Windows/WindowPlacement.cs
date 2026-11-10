using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace ARealmRepopulated.Core.Services.Windows;

/// <summary>
/// Opens editor windows beside the main window and then cascades them downwards.
/// </summary>
public class WindowPlacement {

    private readonly List<Window> _placed = [];
    private Vector2 _anchorPosition;
    private Vector2 _anchorSize;
    private float _workAreaRight;
    private bool _isAnchorOpen;

    public void Update() {
        _anchorPosition = ImGui.GetWindowPos();
        _anchorSize = ImGui.GetWindowSize();

        var viewport = ImGui.GetWindowViewport();
        _workAreaRight = viewport.WorkPos.X + viewport.WorkSize.X;

        _isAnchorOpen = true;
    }

    public void Release()
        => _isAnchorOpen = false;

    public void Place(Window window, bool matchHeight = false) {
        _placed.RemoveAll(w => !w.IsOpen || w == window);
        if (!_isAnchorOpen)
            return;

        var step = ImGui.GetFrameHeightWithSpacing() * _placed.Count;
        if (matchHeight && window.Size is { } size)
            window.Size = size with { Y = (_anchorSize.Y - step) / ImGuiHelpers.GlobalScale };

        var width = (window.Size?.X ?? 0f) * ImGuiHelpers.GlobalScale;
        var onLeft = _anchorPosition.X + _anchorSize.X + width > _workAreaRight;
        window.Position = onLeft
            ? new Vector2(_anchorPosition.X - width - step, _anchorPosition.Y + step)
            : new Vector2(_anchorPosition.X + _anchorSize.X + step, _anchorPosition.Y + step);

        window.PositionCondition = ImGuiCond.Once;
        _placed.Add(window);
    }
}
