using System.Runtime.InteropServices;

namespace ARealmRepopulated.Core.Native;

/// <summary>
/// This one i still dont quite get: We have two scales, this model scale and the base scale, which are multiplied together.
/// It does not matter much to me why it is that way, but curios nontheless.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x2A8)]
public struct CharacterBaseScale {
    [FieldOffset(0x2A4)] public float ModelScale;
}
