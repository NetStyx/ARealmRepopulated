using ARealmRepopulated.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ARealmRepopulated.Core.Services.Nameplates;

/// <summary>
/// Our own node under the nameplate addon root. Makes it easier to manage our nodes.
/// </summary>
public sealed unsafe class NpcNamePlateContainer {

    // The game uses low numbers for its own nodes. So we use "ARRP" in ascii as our base which sits well above what the game uses for nameplate layouts.
    private const uint NodeIdBase = 0x41525250;

    private readonly AtkUnitBase* _addon;
    private readonly AtkResNode* _node;
    private readonly List<NpcNamePlateNode> _texts = [];

    private NpcNamePlateContainer(AtkUnitBase* addon, AtkResNode* node) {
        _addon = addon;
        _node = node;
    }

    public static NpcNamePlateContainer? Create(AtkUnitBase* addon) {
        var node = IMemorySpace.GetUISpace()->Create<AtkResNode>();
        if (node == null)
            return null;

        node->NodeId = NodeIdBase;
        node->Type = NodeType.Res;
        node->NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible;

        AtkNodeLinks.AppendChild(addon->RootNode, node);
        addon->UldManager.UpdateDrawNodeList();
        return new NpcNamePlateContainer(addon, node);
    }

    public NpcNamePlateNode? GetOrCreateText(int index, NpcNamePlateStyle style, ByteColor textColor, ByteColor edgeColor) {
        if (index < _texts.Count)
            return _texts[index];

        var text = NpcNamePlateNode.Create(_node, NodeIdBase + 1 + (uint)index, style, textColor, edgeColor);
        if (text == null)
            return null;

        _addon->UldManager.UpdateDrawNodeList();
        _texts.Add(text);
        return text;
    }

    public void HideFrom(int index) {
        for (var i = index; i < _texts.Count; i++) {
            _texts[i].Hide();
        }
    }

    public void SetColors(ByteColor textColor, ByteColor edgeColor)
        => _texts.ForEach(t => t.SetColors(textColor, edgeColor));

    /// <summary>
    /// Takes the container out of the addon and frees it with all its texts.
    /// </summary>
    public void Destroy() {
        // the order of these steps is important to not run into use-after-free problems.
        AtkNodeLinks.Unlink(_node);
        _addon->UldManager.UpdateDrawNodeList();

        _texts.ForEach(t => t.Destroy());
        _texts.Clear();
        _node->Destroy(true);
    }
}
