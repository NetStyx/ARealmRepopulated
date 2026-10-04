using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace ARealmRepopulated.Core.Native;

/// <summary>
/// This one i still dont quite get: We have two scales, this model scale and the base scale, which are multiplied together.
/// It does not matter much to me why it is that way, but curios nontheless.
/// </summary>
public static unsafe class CharacterBaseScale {

    // CharacterBase.ModelScale is private in client structs
    public static readonly ClientStructsOffset ModelScaleOffset = ClientStructsField.OffsetOf<CharacterBase>("ModelScale");

    public static float GetModelScale(CharacterBase* characterBase)
        => ModelScaleOffset.IsAvailable ? *(float*)((byte*)characterBase + ModelScaleOffset.Offset) : 1f;

    public static void SetModelScale(CharacterBase* characterBase, float scale) {
        if (ModelScaleOffset.IsAvailable) {
            *(float*)((byte*)characterBase + ModelScaleOffset.Offset) = scale;
        }
    }
}
