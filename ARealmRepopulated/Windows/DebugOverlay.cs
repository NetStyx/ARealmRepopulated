using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using CameraManager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager;

namespace ARealmRepopulated.Windows;

public class DebugOverlay(IDalamudPluginInterface pluginInterface, IObjectTable objectTable, IClientState clientState, IGameGui gui, PluginConfig config) : IDisposable
{

    private readonly object _scenarioAccessLock = new();
    private readonly List<ScenarioEditorWindow> _openEditors = new();
    
    private readonly uint _imguiColorBlack = ImGui.GetColorU32(new Vector4(0, 0, 0, 255));
    private readonly uint _imguiColorRed = ImGui.GetColorU32(new Vector4(255, 0, 0, 255));
    private readonly uint _imguiColorGreen = ImGui.GetColorU32(new Vector4(0, 255, 0, 255));

    private Matrix4x4 _gizmoMatrix = Matrix4x4.Identity;
    private Vector3 _gizmoScale = Vector3.One;

    private Vector3 _npcTrace = Vector3.Zero;

    public unsafe void Initialize()
    {
        if (config.EnableScenarioDebugOverlay)
        {
            Hook();
        }
    }

    public void AddEditor(ScenarioEditorWindow scenarioObject) {
        lock (_scenarioAccessLock)
            _openEditors.Add(scenarioObject);
    }    

    public void RemoveEditor(ScenarioEditorWindow scenarioObject) {
        lock (_scenarioAccessLock)
            _openEditors.Remove(scenarioObject);
    }

    public void Hook()
        => pluginInterface.UiBuilder.Draw += Draw;
    
    public void Unhook()
        => pluginInterface.UiBuilder.Draw -= Draw;    

    private void Draw()
    {
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
        )
        {
            return;
        }

        List<ScenarioEditorWindow> snapshot;
        lock (_scenarioAccessLock)
        {
            snapshot = _openEditors.ToList();
        }
        snapshot.ForEach(DrawScenarioDebugInfo);

        DrawNpcTrace();

        ImGui.End();
    }

    private void DrawNpcTrace()
    {
        if (_npcTrace == Vector3.Zero)
            return;

        gui.WorldToScreen(_npcTrace, out Vector2 screenPosition);
        gui.WorldToScreen(objectTable.LocalPlayer?.Position ?? Vector3.One, out var playerScreenPosition);
        
        var drawing = ImGui.GetWindowDrawList();
        drawing.AddLine(playerScreenPosition, screenPosition, _imguiColorGreen, 2f);
        drawing.AddCircle(screenPosition, 8f, _imguiColorBlack, 2f);
        drawing.AddCircleFilled(screenPosition, 6f, _imguiColorGreen);        
    }


    private void DrawScenarioDebugInfo(ScenarioEditorWindow data)
    {

        if (data.ScenarioObject.TerritoryId != clientState.TerritoryType)
            return;

        ImDrawListPtr drawing = ImGui.GetWindowDrawList();
        foreach (var npcs in data.ScenarioObject.Npcs)
        {

            if (data.SelectedScenarioNpc != npcs)
                continue;
            
            bool renderStartPosition = gui.WorldToScreen(npcs.Position, out Vector2 startingPosition);
            if (renderStartPosition)
            {
                var rotation = npcs.Rotation;

                drawing.AddCircle(startingPosition, 8f, _imguiColorRed, 3f);
                drawing.AddCircleFilled(startingPosition, 5f, _imguiColorBlack);

                
                //DrawGizmo(npcs);
                if (data.SelectedScenarioNpcAction == null)
                {                
                    var npcPosition = new Vector3(npcs.Position.X, npcs.Position.Y, npcs.Position.Z);
                    var npcRotation = npcs.Rotation;

                    if (DrawGizmo(ref npcPosition, ref npcRotation)) {
                        npcs.Position = new(npcPosition.X, npcPosition.Y, npcPosition.Z);
                        npcs.Rotation = npcRotation;
                    }
                }


            }

            var fromPoint = startingPosition;

            foreach (var action in npcs.Actions)
            {

                
                if (action is ScenarioNpcMovementAction moveAction)
                {
                    bool isSelectedAction = data.SelectedScenarioNpcAction == moveAction;
                    var targetColor = isSelectedAction ? _imguiColorGreen : _imguiColorBlack;
                    

                    bool renderMoveTarget = gui.WorldToScreen(moveAction.TargetPosition, out Vector2 moveTarget);
                    if (renderMoveTarget)
                    {                                                
                        drawing.AddCircleFilled(moveTarget, 5f, targetColor);

                        if (isSelectedAction)
                        {
                            var movePosition = new Vector3(moveAction.TargetPosition.X, moveAction.TargetPosition.Y, moveAction.TargetPosition.Z);
                            var moveRotation = 0f;
                            if (DrawGizmo(ref movePosition, ref moveRotation, ImGuizmoOperation.Translate))
                            {
                                moveAction.TargetPosition = new(movePosition.X, movePosition.Y, movePosition.Z);
                            }
                        }

                    }

                    if (fromPoint != Vector2.Zero)
                    {
                        drawing.AddLine(fromPoint, moveTarget, targetColor);
                    }
                    fromPoint = moveTarget;
                }
            }
            
            if (startingPosition != fromPoint)
            {
                drawing.AddCircle(fromPoint, 8f, _imguiColorGreen, 3f);                
            }
        }
        
    }
    
    private unsafe bool DrawGizmo(ref Vector3 position, ref float roation, ImGuizmoOperation mode = ImGuizmoOperation.RotateY | ImGuizmoOperation.Translate)
    {
        var wasModified = false;
        var pos = ImGui.GetWindowPos();
        var size = new Vector2(ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y);

        ImGuizmo.BeginFrame();

        GetPatchedProjections(out var viewMatrx, out var projectionMatrxix, out var cameraPosition);

        ImGuizmo.SetDrawlist();        

        ImGuizmo.SetOrthographic(false);
        ImGuizmo.SetID((int)ImGui.GetID("ScenarioDebugOverlayGizmo"));
        ImGuizmo.Enable(true);
                
        const float baseClipSize = 1.5f;        
        float distance = MathF.Max(Vector3.Distance(cameraPosition, position), baseClipSize);        
        ImGuizmo.SetGizmoSizeClipSpace(baseClipSize / distance);
        

        ImGuizmo.SetRect(pos.X, pos.Y, size.X, size.Y);
        
        var translation = position;
        var rotation = new Vector3(0, roation * (180f / MathF.PI), 0);

        ImGuizmo.RecomposeMatrixFromComponents(ref translation.X, ref rotation.X, ref _gizmoScale.X, ref _gizmoMatrix.M11);

        var snap = Vector3.Zero;
        if (Manipulate(ref viewMatrx.M11, ref projectionMatrxix.M11, mode, ImGuizmoMode.Local, ref _gizmoMatrix.M11, ref snap.X))
        {
            // position stored at M41, M42, M43
            position = new Vector3(_gizmoMatrix.M41,_gizmoMatrix.M42,_gizmoMatrix.M43);
            // rotation stored at M31, M32, M33; Only X and Z matter for Yaw
            roation = MathF.Atan2(_gizmoMatrix.M31, _gizmoMatrix.M33);
            wasModified = true;
        }
        
        ImGuizmo.SetID(-1);

        return wasModified;
    }

    private unsafe bool Manipulate(ref float view, ref float proj, ImGuizmoOperation op, ImGuizmoMode mode, ref float matrix, ref float snap)
    {
        fixed (
            float* native_view = &view,
            native_proj = &proj,
            native_matrix = &matrix,
            native_snap = &snap)
        {
            return ImGuizmo.Manipulate(native_view, native_proj, op, mode, native_matrix, null, native_snap, null, null);
        }      
    }

    private unsafe void GetPatchedProjections(out Matrix4x4 patchedViewMatrix, out Matrix4x4 patchedProjectionMatrix, out Vector3 cameraPos)
    {
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

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
    }

    internal void SetNpcTrace(Vector3 position)
        => _npcTrace = position;
    internal void ClearNpcTrace()
        => _npcTrace = Vector3.Zero;
}
