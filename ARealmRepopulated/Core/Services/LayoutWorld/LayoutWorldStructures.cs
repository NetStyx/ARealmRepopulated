using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Core.Services.LayoutWorld;

public readonly record struct SnapSearchQuery(
    Vector3 ReferencePosition,
    ChairMarkerObjectType TargetType,
    float SearchRadius
);

public unsafe class SnapSearchResult {
    public Vector3 SnapPosition { get; init; }
    public float SnapFacing { get; init; }
    public ILayoutInstance* LayoutInstance { get; init; }
}

public readonly record struct SnapPosition(
    nint LayoutInstance,
    Vector3 ObjectPosition,
    float ObjectFacing,
    Vector3 CalculatedSnapPosition,
    float CalculatedSnapFacing,
    ChairMarkerEnableFlags SelectedSide,
    float DistanceSquared,
    ChairMarkerEnableFlags EnabledSides,
    ChairMarkerObjectType CandidateType
);

internal readonly record struct SnapSideChoice(
    ChairMarkerEnableFlags Side = default,
    Vector3 SnapPosition = default,
    float SnapFacing = 0f,
    float DistanceSquared = 0f
);
