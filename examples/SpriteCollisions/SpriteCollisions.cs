// Port of the Playdate SDK's C_API/Examples/Sprite Collisions.

using System.Runtime.CompilerServices;

namespace SpriteCollisions;

public sealed class SpriteCollisionsGame : Game
{
    private const int MaxSamples = 200;

    private Player _player1;
    private Player _player2;
    private float _rayRotation;

    private uint _lastFrameTime;
    private int _tickIndex;
    private int _tickSum = 50 * MaxSamples;
    private readonly int[] _tickList = new int[MaxSamples];
    private readonly char[] _fpsText = new char[2];

#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new SpriteCollisionsGame());
#endif

    protected override void Initialize()
    {
        Display.SetRefreshRate(50);

        _lastFrameTime = Sys.CurrentTimeMilliseconds;
        for (int i = 0; i < MaxSamples; i++)
            _tickList[i] = 50;

        _player1 = new Player(190, 223, 20, 20, velocityX: 60, velocityY: 50);
        _player2 = new Player(100, 40, 16, 16, velocityX: 20, velocityY: 30);

        int borderSize = 5;
        int displayWidth = 400;
        int displayHeight = 240;

        _ = new Block(0, 0, displayWidth, borderSize);
        _ = new Block(0, borderSize, borderSize, displayHeight - borderSize * 2);
        _ = new Block(displayWidth - borderSize, borderSize, borderSize, displayHeight - borderSize * 2);
        _ = new Block(0, displayHeight - borderSize, displayWidth, borderSize);

        var random = new Random((int)Sys.SecondsSinceEpoch);
        for (int i = 0; i < 6; i++)
        {
            _ = new Block(random.Next(270) + 50,
                          random.Next(100) + 50,
                          random.Next(30) + 10,
                          random.Next(90) + 10);
        }
    }

    private void DrawRays(Player player)
    {
        player.GetPosition(out float startX, out float startY);

        for (int extraAngle = 0; extraAngle < 360; extraAngle += 30)
        {
            float rads = (_rayRotation + extraAngle) * 3.1415f / 180.0f;
            int endX = (int)(MathF.Floor(500 * MathF.Cos(rads)) + startX);
            int endY = (int)(MathF.Floor(500 * MathF.Sin(rads)) + startY);

            using (SpriteQueryList results = Sprites.QuerySpriteInfoAlongLine(startX, startY, endX, endY))
            {
                for (int i = 0; i < results.Count; i++)
                {
                    ref SpriteQueryInfo info = ref results[i];
                    if (info.SpriteObject is not Player)
                    {
                        int r = (int)(6 - 10 * info.Ti1);
                        Graphics.DrawEllipse((int)info.EntryPoint.X - r, (int)info.EntryPoint.Y - r, r * 2, r * 2, 3, 0, 360, SolidColor.Black);
                        r = (int)(5 - 10 * info.Ti1);
                        Graphics.DrawEllipse((int)info.ExitPoint.X - r, (int)info.ExitPoint.Y - r, r * 2, r * 2, 1, 0, 360, SolidColor.Black);
                    }
                }
            }

            Graphics.DrawLine((int)startX, (int)startY, endX, endY, 1, SolidColor.Black);
        }
    }

    private float CalcAverageTick(int newTick)
    {
        _tickSum -= _tickList[_tickIndex];
        _tickSum += newTick;
        _tickList[_tickIndex++] = newTick;
        if (_tickIndex == MaxSamples)
            _tickIndex = 0;
        return (float)_tickSum / MaxSamples;
    }

    private void DrawFPS(int x, int y)
    {
        uint currentTime = Sys.CurrentTimeMilliseconds;
        float averageTick = CalcAverageTick((int)(currentTime - _lastFrameTime));
        int fps = (int)MathF.Floor(1000.0f / averageTick + 0.5f);
        _lastFrameTime = currentTime;

        _fpsText[0] = fps >= 10 ? (char)('0' + fps / 10 % 10) : ' ';
        _fpsText[1] = (char)('0' + fps % 10);
        Graphics.DrawText(_fpsText, x, y);
    }

    public override bool Update()
    {
        Sprites.UpdateAndDrawSprites();
        DrawRays(_player1);
        DrawRays(_player2);

        DrawFPS(8, 8);

        _rayRotation += 0.5f;
        return true;
    }
}

public sealed class Player : Sprite
{
    private const float Dt = 0.05f;

    private float _velocityX;
    private float _velocityY;

    public Player(int x, int y, int w, int h, float velocityX, float velocityY)
    {
        _velocityX = velocityX;
        _velocityY = velocityY;
        Bounds = new PDRect(x - w / 2, y - h / 2, w, h);
        CollideRect = new PDRect(0, 0, w, h);
        ZIndex = 1000;
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
        => Graphics.FillRect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, SolidColor.Black);

    protected override bool HasCollisionResponse => true;

    protected override CollisionResponseType CollisionResponse(Sprite other) => CollisionResponseType.Bounce;

    protected override void Update()
    {
        float dx = _velocityX * Dt;
        float dy = _velocityY * Dt;

        GetPosition(out float x, out float y);

        if (dx != 0 || dy != 0)
        {
            using CollisionList collisions = MoveWithCollisions(x + dx, y + dy, out _, out _);
            for (int i = 0; i < collisions.Count; i++)
            {
                ref SpriteCollisionInfo info = ref collisions[i];
                if (info.Normal.X != 0)
                    _velocityX = -_velocityX;
                if (info.Normal.Y != 0)
                    _velocityY = -_velocityY;
            }
        }
    }
}

public sealed class Block : Sprite
{
    public Block(int x, int y, int w, int h)
    {
        Bounds = new PDRect(x, y, w, h);
        CollideRect = new PDRect(0, 0, w, h);
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
        => Graphics.DrawRect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, SolidColor.Black);
}
