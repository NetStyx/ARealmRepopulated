using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;

public interface IPathSegment {
    float Length { get; }
    float Speed { get; }
    void Evaluate(float t, out Vector3 position, out Vector3 tangent); // t in [0,1]
    float GetTForDistance(float distanceOnSegment);

}
