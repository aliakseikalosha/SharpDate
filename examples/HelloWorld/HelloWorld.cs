// Port of the Playdate SDK's C_API/Examples/Hello World.

using System.Runtime.CompilerServices;

namespace HelloWorld;

public sealed class HelloWorldGame : Game
{
    private const string FontPath = "/System/Fonts/Asheville-Sans-14-Bold.pft";
    private const int TextWidth = 86;
    private const int TextHeight = 16;

    private Font _font;
    private int _x = (PD.LCD_COLUMNS - TextWidth) / 2;
    private int _y = (PD.LCD_ROWS - TextHeight) / 2;
    private int _dx = 1;
    private int _dy = 2;

#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new HelloWorldGame());
#endif

    protected override void Initialize()
    {
        _font = Font.Load(FontPath);
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.SetFont(_font);
        Graphics.DrawText("Hello World!", _x, _y);

        _x += _dx;
        _y += _dy;

        if (_x < 0 || _x > PD.LCD_COLUMNS - TextWidth)
            _dx = -_dx;

        if (_y < 0 || _y > PD.LCD_ROWS - TextHeight)
            _dy = -_dy;

        Sys.DrawFPS(0, 0);
        return true;
    }
}
