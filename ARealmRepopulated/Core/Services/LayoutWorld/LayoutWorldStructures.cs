using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Runtime.InteropServices;

namespace ARealmRepopulated.Core.Services.LayoutWorld;

[Flags]
public enum SnapSideMask : byte {
    None = 0,

    Base = 1 << 0,       // 1, object facing
    Right = 1 << 1,      // 2, object facing + 90°
    Opposite = 1 << 2,   // 4, object facing + 180°
    Left = 1 << 3,       // 8, object facing - 90°

    All = Base | Right | Opposite | Left,
}

public readonly record struct SnapSearchQuery(
    Vector3 ReferencePosition,
    LayoutTarget TargetType,
    float SearchRadius
);

public unsafe class SnapSearchResult {
    public Vector3 SnapPosition { get; init; }
    public float SnapFacing { get; init; }
    public ILayoutInstance* LayoutInstance { get; init; }
}

public readonly record struct SnapSideChoice(
    SnapSideMask Side = SnapSideMask.None,
    Vector3 SnapPosition = default,
    float SnapFacing = 0f,
    float DistanceSquared = 0f
);

public readonly record struct SnapLayoutCandidate(
    nint LayoutInstance,
    Vector3 Position,
    Quaternion Rotation,
    float Facing,
    SnapSideMask AllowedSideMask,
    byte CandidateType
);

public readonly record struct CalculatedSnapPosition(
    nint LayoutInstance,
    Vector3 ObjectPosition,
    float ObjectFacing,
    Vector3 SnapPosition,
    float SnapFacing,
    SnapSideMask SideMask,
    float DistanceSquared
);

// FUN_140e10980:
[StructLayout(LayoutKind.Explicit)]
public unsafe struct SnapLayoutInstance {
    [FieldOffset(0x00)] public ILayoutInstance Base;

    // bVar1 = *(byte *)(candidate + 0x70)
    [FieldOffset(0x70)] public SnapSideMask AllowedSideMask;

    // *(char *)(candidate + 0x74) == candidateType
    [FieldOffset(0x74)] public byte Type;
}
