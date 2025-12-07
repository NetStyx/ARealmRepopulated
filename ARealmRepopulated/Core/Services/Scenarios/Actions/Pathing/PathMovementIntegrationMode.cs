namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;

public enum PathMovementIntegrationMode {
    FastSingleSegment = 0,   // default: never cross a segment boundary in one update
    CrossSingleBoundary = 1  // can cross at most one segment per update
}
