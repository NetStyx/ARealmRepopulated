using ARealmRepopulated.Core.SpatialMath;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Diagnostics.CodeAnalysis;

namespace ARealmRepopulated.Core.Services.LayoutWorld;

public enum LayoutTarget : byte {
    Chair = 0,
    Bed = 1
}

/// <summary>
/// Service to manage functionality which allows interaction with the games in-world layout system.
/// </summary>
/// <remarks>
/// see /doc/research/layout-snapping.md
/// </remarks>
public unsafe class LayoutWorldService : IDisposable {
    
    private static readonly float[] SnapOffsets = [0.42f, 0.75f];

    public LayoutWorldService(IGameInteropProvider provider) {
        provider.InitializeFromAttributes(this);
    }

    /// <summary>
    /// Checks for an layout instance of the specified type within the search range of the character and calculates the nearest snap position.
    /// </summary>
    public bool CheckSnapableLayout(Character* character, float searchRange, LayoutTarget targetType, [NotNullWhen(true)] out SnapSearchResult? result) {
        result = null;

        var layoutWorld = FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutWorld.Instance();
        if (layoutWorld == null)
            return false;

        var searchQuery = new SnapSearchQuery(character->Position, targetType, searchRange);
        var snapCandidates = BuildSnapCandidateList(layoutWorld->ActiveLayout, searchQuery).Union(BuildSnapCandidateList(layoutWorld->GlobalLayout, searchQuery));

        var possibleCandidates = new List<SnapPosition>();
        foreach (var candidate in snapCandidates) {
            if (TryCalculateSnapPosition(candidate, character->Position, out var snapResult)) {
                possibleCandidates.Add(snapResult);
            }
        }

        if (possibleCandidates.Count > 0 && possibleCandidates.OrderBy(c => c.DistanceSquared).FirstOrDefault() is var closestSnapResult) {
            result = new SnapSearchResult { SnapPosition = closestSnapResult.ObjectPosition, SnapFacing = closestSnapResult.CalculatedSnapFacing, LayoutInstance = (ILayoutInstance*)closestSnapResult.LayoutInstance };
            return true;
        }

        return false;
    }

    private static List<SnapPosition> BuildSnapCandidateList(LayoutManager* layoutManager, SnapSearchQuery searchQuery) {

        if (layoutManager == null)
            return [];

        if (!layoutManager->InstancesByType.TryGetValue(InstanceType.ChairMarker, out var chairMarkerInstances, true))
            return [];

        if (chairMarkerInstances == null || chairMarkerInstances.IsNull)
            return [];

        var result = new List<SnapPosition>();
        foreach (var (id, instance) in *chairMarkerInstances.Value) {

            if (instance == null || instance.IsNull)
                continue;

            if (!TryReadSnapPosition(instance.Value, searchQuery, out var candidate))
                continue;

            result.Add(candidate);
        }

        return result;
    }

    private static bool TryReadSnapPosition(ILayoutInstance* instance, SnapSearchQuery searchQuery, out SnapPosition candidate) {
        candidate = default;

        if (instance == null)
            return false;

        var translationPtr = instance->GetTranslationImpl();
        if (translationPtr == null)
            return false;

        var rotationPtr = instance->GetRotationImpl();
        if (rotationPtr == null)
            return false;

        var position = *translationPtr;
        var rotation = *rotationPtr;
        var facing = RotationExtension.FacingFromRotation(rotation);

        if (!searchQuery.ReferencePosition.IsInCylinderRange(position, searchQuery.SearchRadius))
            return false;

        var snap = (SnapLayoutInstance*)instance;
        if (snap->Type != (byte)searchQuery.TargetType)
            return false;

        candidate = new SnapPosition((nint)instance, position, facing, default, 0f, SnapSideMask.None, float.MaxValue, snap->AllowedSideMask, snap->Type);
        return true;
    }

    private static bool TryCalculateSnapPosition(SnapPosition candidate, Vector3 npcPosition, out SnapPosition result) {
        ReadOnlySpan<SnapSideMask> sides = [SnapSideMask.Base, SnapSideMask.Right, SnapSideMask.Opposite, SnapSideMask.Left];

        var best = new SnapSideChoice(DistanceSquared: float.MaxValue);
        foreach (var side in sides) {
            if ((candidate.AllowedSideMask & side) == 0)
                continue;

            var snapFacing = FacingForSide(candidate.ObjectFacing, side);
            var snapPosition = candidate.ObjectPosition.Forward(snapFacing, SnapOffsets[0]);

            var distanceSq = npcPosition.DistanceSquaredTo(snapPosition);
            if (distanceSq >= best.DistanceSquared)
                continue;

            best = new SnapSideChoice(side, snapPosition, snapFacing, distanceSq);
        }

        if (best.Side == SnapSideMask.None) {
            result = default;
            return false;
        }

        result = candidate with {
            CalculatedSnapPosition = best.SnapPosition,
            CalculatedSnapFacing = best.SnapFacing,
            SelectedSide = best.Side,
            DistanceSquared = best.DistanceSquared
        };
        return true;
    }

    private static float FacingForSide(float objectFacing, SnapSideMask side) {
        var facing = objectFacing;

        facing += side switch {
            SnapSideMask.Right => MathF.PI / 2f,
            SnapSideMask.Opposite => MathF.PI,
            SnapSideMask.Left => -MathF.PI / 2f,
            _ => 0f,
        };

        return RotationExtension.NormalizeRadians(facing);
    }

    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}
