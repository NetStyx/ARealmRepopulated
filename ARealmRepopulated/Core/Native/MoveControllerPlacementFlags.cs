using FFXIVClientStructs.FFXIV.Client.Game.Control.MoveControl;

namespace ARealmRepopulated.Core.Native;

public static unsafe class MoveControllerPlacementFlags {

    /// <summary>
    /// While bit 0x01 is cleared, the move controller update searches the collision below the character (from about 2 yalms above it)
    /// and puts it on the surface it finds, keeping X and Z. It sets the bit once that is done, so the placement only runs once
    /// after a character is created. Clearing it again repeats the placement on the next frame; setting it skips the placement.
    /// </summary>
    public const byte SurfacePlaced = 0x01;

    // ClientStructs knows this byte as MoveController.Flags438, but does not make it public.
    public static readonly ClientStructsOffset FlagsOffset = ClientStructsField.OffsetOf<MoveController>("Flags438");

    public static void SetSurfacePlaced(MoveController* controller, bool placed) {
        if (!FlagsOffset.IsAvailable)
            return;

        var flags = (byte*)controller + FlagsOffset.Offset;
        *flags = placed ? (byte)(*flags | SurfacePlaced) : (byte)(*flags & ~SurfacePlaced);
    }
}
