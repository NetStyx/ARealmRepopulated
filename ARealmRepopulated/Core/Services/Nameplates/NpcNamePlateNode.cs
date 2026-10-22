using ARealmRepopulated.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Diagnostics.CodeAnalysis;

namespace ARealmRepopulated.Core.Services.Nameplates;

/// <summary>
/// A node for our actor names, linked into the nameplate addon. No collisions, no event flags, nothing. Looks like a clickable npc nameplate but isnt
/// </summary>
/// <remarks>
/// see /doc/research/nameplates.md#why-not-the-games-own-plates
/// </remarks>
public sealed unsafe class NpcNamePlateNode {
    private const ushort Width = 400;

    private readonly AtkTextNode* _node;
    private string _text = "";

    private NpcNamePlateNode(AtkTextNode* node)
        => _node = node;

    private AtkResNode* ResNode
        => (AtkResNode*)_node;
    
    public static NpcNamePlateNode? Create(AtkResNode* parent, uint nodeId, NpcNamePlateStyle style, ByteColor textColor, ByteColor edgeColor) {
        var node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        if (node == null)
            return null;

        node->NodeId = nodeId;
        node->Type = NodeType.Text;        
        node->NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.UseDepthBasedPriority;
        node->Width = Width;
        node->Height = (ushort)(style.FontSize * 2);
        node->OriginX = Width / 2f;
        node->OriginY = node->Height;

        node->AlignmentFontType = style.AlignmentFontType;
        node->FontSize = style.FontSize;
        node->TextFlags = style.TextFlags;
        node->CharSpacing = style.CharSpacing;
        node->LineSpacing = style.LineSpacing;
        node->SetAlignment(AlignmentType.Bottom);
        node->TextColor = textColor;
        node->EdgeColor = edgeColor;

        AtkNodeLinks.AppendChild(parent, (AtkResNode*)node);
        return new NpcNamePlateNode(node);
    }

    public void Show(string text, System.Numerics.Vector3 screenPosition, float scale, byte alpha) {
        if (_text != text) {
            _text = text;
            _node->SetText(text);
        }

        ResNode->SetScale(scale, scale);
        ResNode->SetPositionFloat(screenPosition.X - (Width / 2f), screenPosition.Y - _node->Height);
        ResNode->Depth = screenPosition.Z;
        ResNode->SetAlpha(alpha);
        ResNode->ToggleVisibility(true);
    }

    public void Hide()
        => ResNode->ToggleVisibility(false);

    public void SetColors(ByteColor textColor, ByteColor edgeColor) {
        _node->TextColor = textColor;
        _node->EdgeColor = edgeColor;
    }

    public void Destroy()
        => ResNode->Destroy(true);
}

/// <summary>
/// Small wrapper around the name plate style properties. Taken from the first nameplate in the addon and used as template for our own.
/// </summary>
public readonly record struct NpcNamePlateStyle(
    byte AlignmentFontType,
    byte FontSize,
    TextFlags TextFlags,
    byte CharSpacing,
    byte LineSpacing,
    float Scale) {

    public static unsafe bool TryRead(AddonNamePlate* addon, [NotNullWhen(true)] out NpcNamePlateStyle? style) {
        style = null;

        var namePlates = addon->NamePlateObjectArray;
        if (namePlates == null || namePlates[0].NameText == null)
            return false;
        
        var template = namePlates[0].NameText;
        var scale = 1f;
        for (var node = (AtkResNode*)template; node != null && node != (AtkResNode*)namePlates[0].RootComponentNode; node = node->ParentNode) {
            scale *= node->ScaleX;
        }

        style = new NpcNamePlateStyle(template->AlignmentFontType, template->FontSize, template->TextFlags, template->CharSpacing, template->LineSpacing, scale);
        return true;
    }
}
