using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Reflection;
using SceneCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera;

namespace ARealmRepopulated.Core.Services.Nameplates;

/// <summary>
/// Service to create nameplates for our actors.
/// </summary>
/// <remarks>
/// The game only builds nameplates for objects it could target and a nameplate itself allows for hover and click.
/// Our nodes are plain text without collision or events so the actors stay out of the targeting system.
/// see /doc/research/nameplates.md
/// </remarks>
public unsafe class NpcNamePlateService(
    IAddonLifecycle addonLifecycle,
    IFramework framework,
    IGameConfig gameConfig,
    IObjectTable objectTable,
    NpcServices npcServices) : IDisposable {
    
    private static readonly string NamePlateAddon = typeof(AddonNamePlate).GetCustomAttribute<AddonAttribute>()!.AddonIdentifiers.First();    
    private const float MaxPlateDrawDistanceSq = 2500;

    private AtkUnitBase* _addon = null;
    private NpcNamePlateStyle _style;
    private ByteColor _textColor;
    private ByteColor _edgeColor;
    private bool _npcNamesShown = true;
    private NpcNamePlateContainer? _container = null;

    public void Initialize() {
        addonLifecycle.RegisterListener(AddonEvent.PreUpdate, NamePlateAddon, OnNamePlateUpdate);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, NamePlateAddon, OnNamePlateFinalize);
        gameConfig.UiConfigChanged += OnUiConfigChanged;
    }

    private void OnNamePlateFinalize(AddonEvent type, AddonArgs args)
        => Detach();

    private void OnUiConfigChanged(object? sender, ConfigChangeEvent change) {
        if (change.Option is UiConfigOption.NamePlateDispTypeNpc or UiConfigOption.NamePlateColorNpc or UiConfigOption.NamePlateEdgeNpc)
            ReadNamePlateSettings();
    }

    private void ReadNamePlateSettings() {
        _npcNamesShown = !gameConfig.TryGet(UiConfigOption.NamePlateDispTypeNpc, out uint displayType) || displayType == 0;
        _textColor = new ByteColor { RGBA = (gameConfig.TryGet(UiConfigOption.NamePlateColorNpc, out uint textColor) ? textColor : 0xFFFFFFFF) | 0xFF000000 };
        _edgeColor = new ByteColor { RGBA = (gameConfig.TryGet(UiConfigOption.NamePlateEdgeNpc, out uint edgeColor) ? edgeColor : 0xFF000000) | 0xFF000000 };
        _container?.SetColors(_textColor, _edgeColor);
    }

    /// <summary>
    /// Places our nodes the way AddonNamePlate places its plates.
    /// </summary>
    /// <remarks>
    // /doc/research/nameplates.md#placing-the-plates-on-screen
    /// </remarks>
    private void OnNamePlateUpdate(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (_addon != addon) {
            Detach();
            if (!TryAttach(addon))
                return;
        }

        var shown = 0;
        var player = objectTable.LocalPlayer;
        var camera = CameraManager.Instance()->GetActiveCamera();
        var numbers = NamePlateNumberArray.Instance();
        if (player != null && camera != null && camera->CameraBase.SceneCamera.RenderCamera != null && numbers != null) {
            var rootScale = _addon->RootNode->ScaleX;
            var plateScale = AtkUnitBase.GetGlobalUIScale() * (rootScale / _addon->Scale) * numbers->NamePlateSize / 100f * _style.Scale;

            foreach (var actor in npcServices.Actors) {
                var showName = actor.DisplayNameSetting == NpcNameDisplay.Always || (actor.DisplayNameSetting == NpcNameDisplay.GameDefault && _npcNamesShown);
                if (!showName || string.IsNullOrWhiteSpace(actor.DisplayName) || actor.IsReleased || !actor.IsDrawn() || actor.GetAlpha() <= 0)
                    continue;

                var position = (System.Numerics.Vector3)actor.GetNamePlatePosition();
                var x = position.X - player.Position.X;
                var z = position.Z - player.Position.Z;
                var distanceSquared = (x * x) + (z * z);
                if (distanceSquared > MaxPlateDrawDistanceSq || !TryProjectToScreen(&camera->CameraBase.SceneCamera, position, out var screenPosition))
                    continue;

                var node = _container!.GetOrCreateText(shown, _style, _textColor, _edgeColor);
                if (node == null)
                    break;

                var distanceScale = CalculateNamePlateScale(distanceSquared);
                node.Show(actor.DisplayName, screenPosition, Math.Max(distanceScale, 0.05f) * plateScale, (byte)(distanceScale * actor.GetAlpha() * 255));
                shown++;
            }
        }

        _container!.HideFrom(shown);
    }

    /// <summary>
    /// Projects a world position into screen pixels the way the game does for its nameplates.
    /// </summary>
    /// <remarks>    
    /// see /doc/research/nameplates.md#emiyas-projection-magic
    /// </remarks>
    private static bool TryProjectToScreen(SceneCamera* camera, System.Numerics.Vector3 position, out System.Numerics.Vector3 screenPosition) {
        screenPosition = default;
        
        System.Numerics.Matrix4x4 view = camera->ViewMatrix;
        view.M14 = 0;
        view.M24 = 0;
        view.M34 = 0;
        view.M44 = 1;
        System.Numerics.Matrix4x4 projection = camera->RenderCamera->ProjectionMatrix;

        var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(position, 1f), view * projection);
        if (clip.W <= 0)
            return false;

        var viewport = clip / clip.W;
        if (viewport.X is < -1 or > 1 || viewport.Y is < -1 or > 1 || viewport.Z is < 0 or > 1)
            return false;

        var device = Device.Instance();
        screenPosition = new System.Numerics.Vector3(
            MathF.Round((viewport.X + 1) * device->Width * 0.5f),
            MathF.Round((1 - viewport.Y) * device->Height * 0.5f),
            viewport.Z);
        return true;
    }

    /// <summary>
    /// The size the game gives a nameplate by the squared ground distance to the player, see UI3DModule.CalculateNamePlateScale.
    /// </summary>
    /// <remarks>
    /// see /doc/research/nameplates.md#size-fade-and-depth
    /// </remarks>
    private static float CalculateNamePlateScale(float distanceSquared) {
        if (distanceSquared > MaxPlateDrawDistanceSq)
            return 0;
        if (distanceSquared > 2304)
            return (50 - ((distanceSquared - 2304) / 196 * 50)) / 100;
        if (distanceSquared < 25)
            return 1;
        return (100 - ((distanceSquared - 25) / 2279 * 50)) / 100;
    }
    
    private bool TryAttach(AtkUnitBase* addon) {
        if (addon->RootNode == null)
            return false;

        if (!NpcNamePlateStyle.TryRead((AddonNamePlate*)addon, out var style))
            return false;

        var container = NpcNamePlateContainer.Create(addon);
        if (container == null)
            return false;

        _addon = addon;
        _container = container;
        _style = style.Value;
        ReadNamePlateSettings();
        return true;
    }

    private void Detach() {
        _container?.Destroy();
        _container = null;
        _addon = null;
    }

    public void Dispose() {
        GC.SuppressFinalize(this);

        gameConfig.UiConfigChanged -= OnUiConfigChanged;
        addonLifecycle.UnregisterListener(AddonEvent.PreUpdate, NamePlateAddon, OnNamePlateUpdate);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, NamePlateAddon, OnNamePlateFinalize);
        
        framework.RunOnFrameworkThread(Detach).Wait();
    }
}
