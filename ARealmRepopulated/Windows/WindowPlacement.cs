using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public static class WindowPlacement {

    /// <summary>
    /// Opens an editor window beside the main window, one step down per editor already open.
    /// </summary>
    public static void PlaceBesideMainWindow(this WindowSystem windowSystem, Window window, bool matchHeight = false) {
        if (windowSystem.Windows.OfType<ConfigWindow>().FirstOrDefault() is not { IsOpen: true } mainWindow)
            return;

        // apparently window.Location is only the requested pos and not the actual pos.
        var anchor = ImGuiP.FindWindowByName(mainWindow.WindowName);
        if (anchor.IsNull || anchor.Viewport.IsNull)
            return;

        var anchorPosition = anchor.Pos;
        var anchorSize = anchor.SizeFull;
        var viewport = anchor.Viewport.ImGuiViewport;

        var workarea = viewport.WorkPos.X + viewport.WorkSize.X;
        var offset = ImGui.GetFrameHeightWithSpacing() * 2;
        var width = (window.Size?.X ?? 0f) * ImGuiHelpers.GlobalScale;
        var placeOnLeftSide = anchorPosition.X + anchorSize.X + width > workarea;

        window.Position = placeOnLeftSide
            ? new Vector2(anchorPosition.X - width - offset, anchorPosition.Y)
            : new Vector2(anchorPosition.X + anchorSize.X + offset, anchorPosition.Y);
        window.PositionCondition = ImGuiCond.Once;
    }
}
