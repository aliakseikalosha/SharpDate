// Port of the Playdate SDK's C_API/Examples/Exposure.
// main.lua (from the SDK example) runs the game; C# adds a fast playdate.graphics.image.popcount().

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exposure;

public sealed unsafe class ExposureGame : Game
{
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new ExposureGame());

    // The Lua script drives the game; we only provide a function to it.
    protected override bool UsesUpdateCallback => false;

    protected override void InitializeLua()
    {
        if (!Lua.AddFunction(&ImagePopcount, "playdate.graphics.image.popcount", out string error))
            Sys.Log($"addFunction failed, {error}");
    }

    // Returns the number of white pixels in an image.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ImagePopcount(void* L)
    {
        Bitmap bitmap = Lua.GetBitmap(1);
        bitmap.GetData(out int width, out int height, out int rowBytes, out _, out byte* data);

        int count = 0;
        for (int y = 0; y < height; ++y)
        {
            uint* row = (uint*)&data[rowBytes * y];
            for (int x = 0; x < width; x += 32)
                count += BitOperations.PopCount(row[x / 32]);
        }

        Lua.PushInt(count);
        return 1;
    }
}
