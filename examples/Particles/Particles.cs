// Port of the Playdate SDK's C_API/Examples/Particles: a custom object type implemented in C#,
// exposed to Lua (main.lua from the SDK example drives the UI).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Particles;

public struct Particle
{
    public float X;
    public int Y;
    public int W;
    public int H;
    public int Speed;
    public float Drift;
    public int Type;
}

public sealed class ParticleSystem
{
    public Particle[] Items = System.Array.Empty<Particle>();
    public int Count;

    public void SetCount(int count)
    {
        int startIndex = count > Count ? Count : count;
        System.Array.Resize(ref Items, count);
        Count = count;

        Random random = ParticlesGame.Random;
        for (int i = startIndex; i < count; i++)
        {
            ref Particle particle = ref Items[i];
            particle.X = random.Next(441) - 20.0f;
            particle.Y = random.Next(241) - 240;
            particle.W = 19;
            particle.H = 21;
            particle.Type = random.Next(4);
            particle.Speed = random.Next(4) + 1;
            particle.Drift = (random.Next(5) - 2.0f) / 10.0f;
        }
    }
}

public sealed unsafe class ParticlesGame : Game
{
    private const string ClassName = "particlelib.particles";

    public static Random Random;
    private static readonly Bitmap[] s_flakes = new Bitmap[4];

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new ParticlesGame());

    protected override bool UsesUpdateCallback => false;

    protected override void InitializeLua()
    {
        var methods = new LuaFunction[]
        {
            new LuaFunction("__gc", &Dealloc),
            new LuaFunction("new", &NewObject),
            new LuaFunction("setNumberOfParticles", &SetNumberOfParticles),
            new LuaFunction("particleCountInRect", &ParticleCountInRect),
            new LuaFunction("update", &Update),
            new LuaFunction("draw", &Draw),
        };

        if (!Lua.RegisterClass(ClassName, methods, out string error))
            Sys.Log($"registerClass failed, {error}");

        Random = new Random((int)Sys.SecondsSinceEpoch);

        // load particle images
        for (int i = 0; i < 4; i++)
            s_flakes[i] = Bitmap.Load($"images/snowflake{i + 1}");
    }

    private static ParticleSystem Arg(int pos) => Lua.GetArgManagedObject<ParticleSystem>(pos, ClassName);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Dealloc(void* L)
    {
        Lua.ReleaseManagedObject(1, ClassName);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NewObject(void* L)
    {
        var particles = new ParticleSystem();
        particles.SetCount(Lua.GetArgInt(1));
        Lua.PushManagedObject(particles, ClassName);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SetNumberOfParticles(void* L)
    {
        Arg(1).SetCount(Lua.GetArgInt(2));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ParticleCountInRect(void* L)
    {
        ParticleSystem p = Arg(1);
        int x = Lua.GetArgInt(2);
        int y = Lua.GetArgInt(3);
        int w = Lua.GetArgInt(4);
        int h = Lua.GetArgInt(5);

        int result = 0;
        for (int i = 0; i < p.Count; i++)
        {
            ref Particle particle = ref p.Items[i];
            if (!(particle.Y >= y + h || particle.Y + particle.H <= y || particle.X >= x + w || particle.X + particle.W <= x))
                result += 1;
        }

        Lua.PushInt(result);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Update(void* L)
    {
        ParticleSystem p = Arg(1);
        for (int i = 0; i < p.Count; i++)
        {
            ref Particle particle = ref p.Items[i];
            particle.Drift += (Random.Next(5) - 2.0f) / 10.0f;
            particle.Y += particle.Speed;
            particle.X += particle.Drift;

            if (particle.Y > 240) // if the particle is off the screen, move it back to the top
            {
                particle.Y = -22;
                particle.X = Random.Next(441) - 20;
                particle.Drift = (Random.Next(5) - 2.0f) / 10.0f;
            }
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Draw(void* L)
    {
        ParticleSystem p = Arg(1);
        Graphics.Clear(SolidColor.Black);

        for (int i = 0; i < p.Count; i++)
        {
            ref Particle particle = ref p.Items[i];
            Graphics.SetDrawMode(DrawMode.Inverted);
            Graphics.DrawBitmap(s_flakes[particle.Type], (int)MathF.Round(particle.X), particle.Y);
        }
        return 0;
    }
}
