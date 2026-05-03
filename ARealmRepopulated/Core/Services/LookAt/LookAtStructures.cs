using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Runtime.InteropServices;

namespace ARealmRepopulated.Core.Services.LookAt;

/// <summary>
/// Param structure to modify the look-at transition. Passing null does the job as well.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public unsafe struct CharacterLookAtUpdateParam {
    [FieldOffset(0x00)] public void* VTable;
    [FieldOffset(0x08)] public float TransitionParam;
    [FieldOffset(0x0C)] public int TargetSubType;
    [FieldOffset(0x10)] public byte Flags;
}

/// <summary>
/// Writeable override used to set the Unk var. Turns out it was not releaded to anything important.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x28)]
public unsafe struct CharacterLookAtTargetParamWriteable {
    [FieldOffset(0x00)] public CharacterLookAtTargetParam.CharacterLookAtTargetParamVirtualTable* VirtualTable;
    [FieldOffset(0x08)] public CharacterLookAtTargetParam.TargetInfoType Type;
    [FieldOffset(0x10)] public GameObjectId TargetId;
    [FieldOffset(0x20)] public int Unk;
}
