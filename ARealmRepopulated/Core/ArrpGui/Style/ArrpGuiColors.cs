using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ARealmRepopulated.Core.ArrpGui.Style;

public static class ArrpGuiColors
{

    public static Vector4 ArrpGreen => new Vector4(0, 150, 0, 255).FromRgb();
    public static Vector4 ArrpRed => new Vector4(192, 0, 0, 255).FromRgb();

}

public static class ArrpGuiColorConverter
{
    public static Vector4 FromRgb(this Vector4 rgba)
        => new(rgba.X / 255f, rgba.Y / 255f, rgba.Z / 255f, rgba.W / 255f);
}
