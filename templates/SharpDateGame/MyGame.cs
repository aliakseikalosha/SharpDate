using System.Runtime.CompilerServices;

namespace MyGame;

/// <summary>
/// Starter game: move the ball with the d-pad, turn the crank to aim, press A to play a note.
/// </summary>
public sealed class MyGame : Game
{
    // Registers the game with SharpDate when the module loads.
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new MyGame());

    private Ball _ball;
    private Synth _synth;
    private Font _font;
    private int _notes;
    private bool _showHelp = true;

    protected override void Initialize()
    {
        Display.RefreshRate = 30;
        _font = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");

        _ball = new Ball(200, 120);

        _synth = Synth.Create(SoundWaveform.Square);
        _synth.SetVolume(0.3f);

        Sys.AddCheckmarkMenuItem("Help", _showHelp, item => _showHelp = item.Checked);
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
        {
            _synth.PlayMIDINote(60 + (_notes++ % 8) * 2, 1, 0.2f);
            _ball.Bounce();
        }

        Graphics.Clear(SolidColor.White);
        Sprites.UpdateAndDrawSprites();

        // Crank: draw a pointer from the ball in the crank's direction.
        float angle = (Sys.CrankAngle - 90) * MathF.PI / 180;
        _ball.GetPosition(out float x, out float y);
        Graphics.DrawLine((int)x, (int)y, (int)(x + MathF.Cos(angle) * 40), (int)(y + MathF.Sin(angle) * 40), 3, SolidColor.Black);

        Graphics.SetFont(_font);
        if (_showHelp)
            Graphics.DrawText("D-pad: move   Crank: aim   A: note", 20, 8);
        Graphics.DrawText($"Notes: {_notes}", 20, 216);
        Sys.DrawFPS(380, 0);
        return true;
    }
}

/// <summary>A sprite that draws itself and moves with the d-pad.</summary>
public sealed class Ball : Sprite
{
    private const int Radius = 12;
    private int _flash;

    public Ball(float x, float y)
    {
        SetSize(Radius * 2, Radius * 2);
        MoveTo(x, y);
        Add();
    }

    public void Bounce() => _flash = 6;

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
    {
        if (_flash > 0)
            Graphics.FillEllipse((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, 0, 0, SolidColor.Black);
        else
            Graphics.DrawEllipse((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, 3, 0, 0, SolidColor.Black);
    }

    protected override void Update()
    {
        Buttons held = Sys.ButtonsCurrent;
        float dx = 0, dy = 0;
        if ((held & Buttons.Left) != 0) dx -= 4;
        if ((held & Buttons.Right) != 0) dx += 4;
        if ((held & Buttons.Up) != 0) dy -= 4;
        if ((held & Buttons.Down) != 0) dy += 4;

        GetPosition(out float x, out float y);
        MoveTo(Math.Clamp(x + dx, Radius, 400 - Radius), Math.Clamp(y + dy, Radius + 24, 210 - Radius));

        if (_flash > 0)
            _flash--;
        MarkDirty();
    }
}
