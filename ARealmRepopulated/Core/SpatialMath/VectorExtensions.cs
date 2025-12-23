using CsMaths = FFXIVClientStructs.FFXIV.Common.Math;
using Numerics = System.Numerics;

namespace ARealmRepopulated.Core.SpatialMath;

public static class VectorExtensions {

    public static Numerics.Vector3 AsVector(this CsMaths.Vector3 vector)
        => new(vector.X, vector.Y, vector.Z);

    public static CsMaths.Vector3 AsCsVector(this Numerics.Vector3 vector)
        => new(vector.X, vector.Y, vector.Z);

}
