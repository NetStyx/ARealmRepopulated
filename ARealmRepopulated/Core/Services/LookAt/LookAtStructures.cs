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
