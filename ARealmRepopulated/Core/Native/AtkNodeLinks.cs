using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ARealmRepopulated.Core.Native;

/// <summary>
/// Links nodes we create ourselves into an addon node tree and takes them out again.
/// </summary>
public static unsafe class AtkNodeLinks {

    /// <summary>
    /// Appends the child as the last child of the parent. The caller rebuilds the draw node list.
    /// </summary>
    /// <remarks>
    /// The game links children backwards it seems. The first child has no next sibling, later ones hang off PrevSiblingNode. The caller is responsible for rebuilding the draw node list.
    /// </remarks>
    public static void AppendChild(AtkResNode* parent, AtkResNode* child) {        
        var last = parent->ChildNode;
        if (last == null) {
            parent->ChildNode = child;
        } else {
            while (last->PrevSiblingNode != null) {
                last = last->PrevSiblingNode;
            }
            last->PrevSiblingNode = child;
            child->NextSiblingNode = last;
        }
        child->ParentNode = parent;
        parent->ChildCount++;
    }

    /// <summary>
    /// Takes the node out of its parent. This needs to be done **before** destroying the node and the draw node list rebuilt, or the game draws freed memory.
    /// </summary>
    /// <remarks>
    /// Fun times.
    /// </remarks>
    public static void Unlink(AtkResNode* node) {
        var parent = node->ParentNode;
        if (node->PrevSiblingNode != null)
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        if (node->NextSiblingNode != null)
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;

        if (parent != null) {
            if (parent->ChildNode == node)
                parent->ChildNode = node->PrevSiblingNode;
            parent->ChildCount--;
        }

        node->ParentNode = null;
        node->PrevSiblingNode = null;
        node->NextSiblingNode = null;
    }
}
