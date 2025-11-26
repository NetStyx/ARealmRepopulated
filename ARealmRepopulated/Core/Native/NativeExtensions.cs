using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ARealmRepopulated.Core.Native;
public unsafe static class NativeExtensions
{

    public static GameObject* AsGameObject(this nint pointer)
        => (GameObject*)pointer;

    public static BattleChara* AsBattleCharacter(this nint pointer)
        => (BattleChara*)pointer;

    public static Character* AsCharacter(this nint pointer)
        => (Character*)pointer;


}
