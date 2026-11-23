namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// Methods for functionality that i dont know where else to put.
/// </summary>
public static class ArrpEntityExtensions {

    public static string[] Segments(string? path)
        => (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Normalize(string? path)
        => string.Join('/', Segments(path));
}
