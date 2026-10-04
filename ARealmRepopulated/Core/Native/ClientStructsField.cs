using System.Runtime.InteropServices;

namespace ARealmRepopulated.Core.Native;

/// <summary>
/// Looks up the offset of a cs field by name. This way i dont need to create a hard coded address offset just because it was private or not fully reverse engineered.
/// This still breaks if the field it is renamed or removed from cs, but at least i dont have to tace adresses in the meantime.
/// </summary>
public static class ClientStructsField {
    public static ClientStructsOffset OffsetOf<T>(string fieldName) where T : unmanaged {
        try {
            return new ClientStructsOffset((int)Marshal.OffsetOf<T>(fieldName));
        } catch (ArgumentException) {
            return default;
        }
    }
}

public readonly struct ClientStructsOffset(int offset) {
    public int Offset { get; } = offset;

    // Returning a default strcut returns everything zeroed out - meaning even if the default here would be true, the default struct would return false. The more you know ~
    public bool IsAvailable { get; } = true;
}
