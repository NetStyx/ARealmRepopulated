using System.Runtime.InteropServices;

namespace ARealmRepopulated.Core.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x9D)]
public struct GameObjectWorldFlags {

    /// <summary>
    /// Bit 0x40 makes a client-side object count for world interaction and makes the object visible to coliders 
    /// like those that are used to actuate doors. The game sets it on initialize only for network objects and 
    /// on actors it creates for cutscenes, so it is cleared on everything created through the ClientObjectManager.
    /// </summary>    
    public const byte InteractsWithWorld = 0x40;

    [FieldOffset(0x9C)] public byte Flags;
}
