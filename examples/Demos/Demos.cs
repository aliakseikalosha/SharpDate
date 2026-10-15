// Launcher for the C# example games. The menu starts a demo by restarting the game with
// "demo=<name>" as launch arguments, so every demo begins from a clean state; the system menu's
// "Demo menu" item restarts back to this menu.

using System.Runtime.CompilerServices;

namespace Demos;

public sealed class DemoInfo
{
    public readonly string Name;
    public readonly string Description;
    public readonly Func<Game> Create;

    public DemoInfo(string name, string description, Func<Game> create)
    {
        Name = name;
        Description = description;
        Create = create;
    }
}

public static class Launcher
{
    private const string ArgPrefix = "demo=";

    public static readonly DemoInfo[] All =
    {
        new DemoInfo("Hello World", "Bouncing text", () => new HelloWorld.HelloWorldGame()),
        new DemoInfo("Life", "Game of Life on the frame buffer. A: randomize", () => new Life.LifeGame()),
        new DemoInfo("Tilemap", "Random tiles from an image table", () => new Tilemap.TilemapGame()),
        new DemoInfo("bach.mid", "MIDI file on a sampled piano", () => new BachMidi.BachMidiGame()),
        new DemoInfo("Sprite Game", "Shoot-em-up. D-pad, A/B fire, crank: enemies", () => new SpriteGame.SpriteGameMain()),
        new DemoInfo("Sprite Collisions", "Bouncing sprites and line queries", () => new SpriteCollisions.SpriteCollisionsGame()),
        new DemoInfo("3D", "Mini3D scenes. A: next scene, crank: roll", () => new Lib3DCSharp.Lib3DCSharpGame()),
        new DemoInfo("JSON", "Decodes test.json (output in the console)", () => new JsonExample.JsonGame()),
        new DemoInfo("Networking", "HTTP/TCP to the SDK's test server", () => new Networking.NetworkingGame()),
    };

    [ModuleInitializer]
    internal static void Register() => PD.Run(Create);

    private static Game Create()
    {
        string args = Sys.GetLaunchArgs(out _);
        if (args != null && args.StartsWith(ArgPrefix))
        {
            string name = args.Substring(ArgPrefix.Length);
            foreach (DemoInfo demo in All)
            {
                if (demo.Name == name)
                {
                    Sys.AddMenuItem("Demo menu", _ => Sys.RestartGame(""));
                    return demo.Create();
                }
            }
        }
        return new MenuGame();
    }

    public static void Launch(DemoInfo demo) => Sys.RestartGame(ArgPrefix + demo.Name);
}

public sealed class MenuGame : Game
{
    private const int Top = 34;
    private const int RowHeight = 19;

    private Font _titleFont;
    private Font _font;
    private int _selected;
    private float _crank;

    protected override void Initialize()
    {
        _titleFont = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");
        _font = Font.Load("/System/Fonts/Asheville-Sans-14-Light.pft");
        Display.RefreshRate = 30;
    }

    public override bool Update()
    {
        int count = Launcher.All.Length;

        Sys.GetButtonState(out _, out Buttons pushed, out _);
        if ((pushed & Buttons.Down) != 0)
            _selected = (_selected + 1) % count;
        if ((pushed & Buttons.Up) != 0)
            _selected = (_selected + count - 1) % count;

        // The crank scrolls too: one item per 30 degrees.
        _crank += Sys.CrankChange;
        while (_crank > 30) { _crank -= 30; _selected = Math.Min(_selected + 1, count - 1); }
        while (_crank < -30) { _crank += 30; _selected = Math.Max(_selected - 1, 0); }

        if ((pushed & Buttons.A) != 0)
            Launcher.Launch(Launcher.All[_selected]);

        Draw();
        return true;
    }

    private void Draw()
    {
        Graphics.Clear(SolidColor.White);

        Graphics.SetFont(_titleFont);
        Graphics.DrawText("SharpDate Demos", 12, 8);
        Graphics.DrawText("A: Start", 312, 8);
        Graphics.DrawLine(0, 28, 400, 28, 1, SolidColor.Black);

        Graphics.SetFont(_font);
        for (int i = 0; i < Launcher.All.Length; i++)
        {
            int y = Top + i * RowHeight;
            if (i == _selected)
            {
                Graphics.FillRoundRect(6, y - 1, 388, RowHeight, 4, SolidColor.Black);
                Graphics.SetDrawMode(DrawMode.FillWhite);
            }
            Graphics.DrawText(Launcher.All[i].Name, 16, y + 1);
            Graphics.SetDrawMode(DrawMode.Copy);
        }

        Graphics.DrawLine(0, 214, 400, 214, 1, SolidColor.Black);
        Graphics.DrawText(Launcher.All[_selected].Description, 12, 219);
    }
}
