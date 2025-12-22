using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Threading;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager;

namespace ARealmRepopulated.Windows;

public class DebugOverlay(IDalamudPluginInterface pluginInterface, IObjectTable objectTable, IClientState clientState, IGameGui gui) : IDisposable {

    private readonly Lock _scenarioAccessLock = new();
    private readonly List<ScenarioEditorWindow> _openEditors = [];

    private uint? _imguiColorBlack = null!;
    private uint? _imguiColorRed = null!;
    private uint? _imguiColorGreen = null!;

    private Matrix4x4 _gizmoMatrix = Matrix4x4.Identity;
    private Vector3 _gizmoScale = Vector3.One;

    private Vector3 _npcTrace = Vector3.Zero;

    public void AddEditor(ScenarioEditorWindow scenarioObject) {
        using var _ = _scenarioAccessLock.EnterScope();
        _openEditors.Add(scenarioObject);
    }

    public void RemoveEditor(ScenarioEditorWindow scenarioObject) {
        using var _ = _scenarioAccessLock.EnterScope();
        _openEditors.Remove(scenarioObject);
    }

    public void Hook()
        => pluginInterface.UiBuilder.Draw += Draw;

    public void Unhook()
        => pluginInterface.UiBuilder.Draw -= Draw;

    private void Draw() {
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);

        if (!ImGui.Begin("###ScenarioDebugOverlay",
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoNav)
        ) {
            return;
        }

        _imguiColorBlack ??= ImGui.GetColorU32(new Vector4(0, 0, 0, 255));
        _imguiColorRed ??= ImGui.GetColorU32(new Vector4(255, 0, 0, 255));
        _imguiColorGreen ??= ImGui.GetColorU32(new Vector4(0, 255, 0, 255));

        List<ScenarioEditorWindow> snapshot;
        using (var _ = _scenarioAccessLock.EnterScope()) {
            snapshot = [.. _openEditors];
        }
        snapshot.ForEach(DrawScenarioDebugInfo);

        DrawNpcTrace();

        ImGui.End();
    }

    private void DrawNpcTrace() {
        if (_npcTrace == Vector3.Zero)
            return;

        gui.WorldToScreen(_npcTrace, out var screenPosition);
        gui.WorldToScreen(objectTable.LocalPlayer?.Position ?? Vector3.One, out var playerScreenPosition);

        var drawing = ImGui.GetWindowDrawList();
        drawing.AddLine(playerScreenPosition, screenPosition, GetFinishColor(), 2f);
        drawing.AddCircle(screenPosition, 8f, GetDefaultColor(), (float)2f);
        drawing.AddCircleFilled(screenPosition, 6f, GetFinishColor());
    }


    private void DrawScenarioDebugInfo(ScenarioEditorWindow data) {

        if (data.ScenarioObject.Location.Territory != clientState.TerritoryType)
            return;

        var drawing = ImGui.GetWindowDrawList();
        foreach (var npcs in data.ScenarioObject.Npcs) {

            if (data.SelectedScenarioNpc != npcs)
                continue;

            var renderStartPosition = gui.WorldToScreen(npcs.Position, out var startingPosition);
            if (renderStartPosition) {
                drawing.AddCircle(startingPosition, 8f, GetStartColor(), (float)3f);
                drawing.AddCircleFilled(startingPosition, 5f, GetDefaultColor());

                //DrawGizmo(npcs);
                if (data.SelectedScenarioNpcAction == null) {
                    var npcPosition = new Vector3(npcs.Position.X, npcs.Position.Y, npcs.Position.Z);
                    var npcRotation = npcs.Rotation;

                    if (DrawGizmo($"##DebugMoveGizmo{npcs.GetHashCode()}", ref npcPosition, ref npcRotation)) {
                        npcs.Position = new(npcPosition.X, npcPosition.Y, npcPosition.Z);
                        npcs.Rotation = npcRotation;
                    }
                }


            }

            var fromPoint = startingPosition;

            foreach (var action in npcs.Actions) {

                var isSelectedAction = data.SelectedScenarioNpcAction == action;
                var targetColor = isSelectedAction ? GetFinishColor() : GetDefaultColor();

                if (action is ScenarioNpcPathAction pathAction) {
                    foreach (var target in pathAction.Points) {
                        var renderMoveTarget = gui.WorldToScreen(target.Point, out var moveTarget);
                        if (renderMoveTarget) {
                            drawing.AddCircleFilled(moveTarget, 5f, targetColor);

                            if (data.SelectedPathMovementPoint == target) {
                                var movePosition = target.Point.AsVector();
                                var moveRotation = 0f;
                                if (DrawGizmo($"##DebugPathGizmo{target.GetHashCode()}", ref movePosition, ref moveRotation, ImGuizmoOperation.Translate)) {
                                    target.Point = movePosition.AsCsVector();
                                }
                            }
                        }

                        if (fromPoint != Vector2.Zero) {
                            drawing.AddLine(fromPoint, moveTarget, targetColor);
                        }
                        fromPoint = moveTarget;
                    }

                }


                if (action is ScenarioNpcMovementAction moveAction) {



                    var renderMoveTarget = gui.WorldToScreen(moveAction.TargetPosition, out var moveTarget);
                    if (renderMoveTarget) {
                        drawing.AddCircleFilled(moveTarget, 5f, targetColor);

                        if (isSelectedAction) {
                            var movePosition = new Vector3(moveAction.TargetPosition.X, moveAction.TargetPosition.Y, moveAction.TargetPosition.Z);
                            var moveRotation = 0f;
                            if (DrawGizmo($"##DebugMoveGizmo{moveAction.GetHashCode()}", ref movePosition, ref moveRotation, ImGuizmoOperation.Translate)) {
                                moveAction.TargetPosition = new(movePosition.X, movePosition.Y, movePosition.Z);
                            }
                        }

                    }

                    if (fromPoint != Vector2.Zero) {
                        drawing.AddLine(fromPoint, moveTarget, targetColor);
                    }
                    fromPoint = moveTarget;
                }
            }

            if (startingPosition != fromPoint) {
                drawing.AddCircle(fromPoint, 8f, GetFinishColor(), (float)3f);
            }
        }

    }

    private unsafe bool DrawGizmo(string id, ref Vector3 position, ref float roation, ImGuizmoOperation mode = ImGuizmoOperation.RotateY | ImGuizmoOperation.Translate, float controlScale = 1.5f) {
        var wasModified = false;
        var pos = ImGui.GetWindowPos();
        var size = new Vector2(ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y);

        ImGuizmo.BeginFrame();

        GetPatchedProjections(out var viewMatrx, out var projectionMatrxix, out var cameraPosition);

        ImGuizmo.SetDrawlist();

        ImGuizmo.SetOrthographic(false);
        ImGuizmo.SetID((int)ImGui.GetID(id));
        ImGuizmo.Enable(true);

        var distance = MathF.Max(Vector3.Distance(cameraPosition, position), controlScale);
        ImGuizmo.SetGizmoSizeClipSpace(controlScale / distance);

        ImGuizmo.SetRect(pos.X, pos.Y, size.X, size.Y);

        var translation = position;
        var rotation = new Vector3(0, roation * (180f / MathF.PI), 0);

        ImGuizmo.RecomposeMatrixFromComponents(ref translation.X, ref rotation.X, ref _gizmoScale.X, ref _gizmoMatrix.M11);

        var snap = Vector3.Zero;
        if (Manipulate(ref viewMatrx.M11, ref projectionMatrxix.M11, mode, ImGuizmoMode.Local, ref _gizmoMatrix.M11, ref snap.X)) {
            // position stored at M41, M42, M43
            position = new Vector3(_gizmoMatrix.M41, _gizmoMatrix.M42, _gizmoMatrix.M43);
            // rotation stored at M31, M32, M33; Only X and Z matter for Yaw
            roation = MathF.Atan2(_gizmoMatrix.M31, _gizmoMatrix.M33);
            wasModified = true;
        }

        ImGuizmo.SetID(-1);

        return wasModified;
    }

    private unsafe bool Manipulate(ref float view, ref float proj, ImGuizmoOperation op, ImGuizmoMode mode, ref float matrix, ref float snap) {
        fixed (
            float* native_view = &view,
            native_proj = &proj,
            native_matrix = &matrix,
            native_snap = &snap) {
            return ImGuizmo.Manipulate(native_view, native_proj, op, mode, native_matrix, null, native_snap, null, null);
        }
    }

    private unsafe void GetPatchedProjections(out Matrix4x4 patchedViewMatrix, out Matrix4x4 patchedProjectionMatrix, out Vector3 cameraPos) {
        var sceneCamera = &CameraManager.Instance()->GetActiveCamera()->CameraBase.SceneCamera;
        var renderCam = sceneCamera->RenderCamera;
        var viewMatrix = sceneCamera->ViewMatrix;
        var projectionMatrix = renderCam->ProjectionMatrix;

        var far = renderCam->FarPlane;
        var near = renderCam->NearPlane;
        var clip = far / (far - near);

        projectionMatrix.M43 = -(clip * near);
        projectionMatrix.M33 = -((far + near) / (far - near));
        viewMatrix.M44 = 1.0f;

        patchedViewMatrix = viewMatrix;
        patchedProjectionMatrix = projectionMatrix;

        cameraPos = new Vector3(
            sceneCamera->Position.X,
            sceneCamera->Position.Y,
            sceneCamera->Position.Z
        );
    }

    public void Dispose() {
        pluginInterface.UiBuilder.Draw -= Draw;
        GC.SuppressFinalize(this);
    }

    internal void SetNpcTrace(Vector3 position)
        => _npcTrace = position;
    internal void ClearNpcTrace()
        => _npcTrace = Vector3.Zero;

    internal uint GetFinishColor()
        => _imguiColorRed.GetValueOrDefault(0);
    internal uint GetStartColor()
        => _imguiColorGreen.GetValueOrDefault(0);
    internal uint GetDefaultColor()
        => _imguiColorBlack.GetValueOrDefault(0);
}
