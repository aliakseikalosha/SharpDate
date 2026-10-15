// Port of the Playdate SDK's C_API/Examples/Sprite Game: a simple shoot-em-up.
// Each sprite type of the C version (setUpdateFunction + tag) becomes a Sprite subclass.

using System.Runtime.CompilerServices;

namespace SpriteGame;

public sealed class SpriteGameMain : Game
{
    // Sprite tags used to identify sprites during collision detection.
    public const byte TagPlayer = 0;
    public const byte TagPlayerBullet = 1;
    public const byte TagEnemyPlane = 2;

    public static readonly Random Random = new Random();

    // game state
    public static int Score;
    public static int MaxBackgroundPlanes = 10;
    public static int BackgroundPlaneCount;
    public static int MaxEnemies = 10;
    public static int EnemyCount;

    // cached images
    public static Bitmap[] ExplosionImages;
    public static Bitmap BulletImage;
    public static Bitmap EnemyPlaneImage;
    public static Bitmap BackgroundPlaneImage;

    private Player _player;

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new SpriteGameMain());

    protected override void Initialize()
    {
        Display.SetRefreshRate(20);

        _ = new Background();
        _player = new Player(200, 180);
        PreloadImages();
    }

    public static Bitmap LoadImageAtPath(string path)
    {
        if (!Bitmap.TryLoad(path, out Bitmap image, out string error))
            Sys.Log($"Error loading image at path '{path}': {error}");
        return image;
    }

    private static void PreloadImages()
    {
        ExplosionImages = new Bitmap[8];
        for (int i = 0; i < 8; i++)
            ExplosionImages[i] = LoadImageAtPath($"images/explosion/{i + 1}");

        BulletImage = LoadImageAtPath("images/doubleBullet");
        EnemyPlaneImage = LoadImageAtPath("images/plane1");
        BackgroundPlaneImage = LoadImageAtPath("images/plane2");
    }

    private static void SpawnEnemyIfNeeded()
    {
        if (EnemyCount < MaxEnemies && Random.Next(120 / MaxEnemies) == 0)
            _ = new EnemyPlane();
    }

    private static void SpawnBackgroundPlaneIfNeeded()
    {
        if (BackgroundPlaneCount < MaxBackgroundPlanes && Random.Next(120 / MaxBackgroundPlanes) == 0)
            _ = new BackgroundPlane();
    }

    // cranking the crank changes the maximum number of enemy planes allowed
    private static void CheckCrank()
    {
        float change = Sys.CrankChange;

        if (change > 1)
        {
            MaxEnemies += 1;
            if (MaxEnemies > 119)
                MaxEnemies = 119;
            Sys.Log($"Maximum number of enemy planes: {MaxEnemies}");
        }
        else if (change < -1)
        {
            MaxEnemies -= 1;
            if (MaxEnemies < 1) // the C version allows 0 and then divides by it
                MaxEnemies = 1;
            Sys.Log($"Maximum number of enemy planes: {MaxEnemies}");
        }
    }

    private void CheckButtons()
    {
        Buttons pushed = Sys.ButtonsPushed;
        if ((pushed & Buttons.A) != 0 || (pushed & Buttons.B) != 0)
            _player.Fire();
    }

    public override bool Update()
    {
        CheckButtons();
        CheckCrank();

        SpawnEnemyIfNeeded();
        SpawnBackgroundPlaneIfNeeded();

        Sprites.UpdateAndDrawSprites();
        return true;
    }
}

// -- Background Sprite -- //

public sealed class Background : Sprite
{
    private readonly Bitmap _image;
    private readonly int _height;
    private int _y;

    public Background()
    {
        _image = SpriteGameMain.LoadImageAtPath("images/background");
        _height = _image.Height;
        Bounds = new PDRect(0, 0, 400, 240);
        ZIndex = 0;
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
    {
        Graphics.DrawBitmap(_image, 0, _y);
        Graphics.DrawBitmap(_image, 0, _y - _height);
    }

    protected override void Update()
    {
        _y += 1;
        if (_y > _height)
            _y = 0;
        MarkDirty();
    }
}

// -- Explosions -- //

public sealed class Explosion : Sprite
{
    public Explosion(float x, float y)
    {
        Image = SpriteGameMain.ExplosionImages[0];
        MoveTo(x, y);
        ZIndex = 2000;
        Add();
        Tag = 1; // using tag here for the frame number of the explosion animation
    }

    protected override void Update()
    {
        int frameNumber = Tag + 1;

        if (frameNumber > 7)
        {
            Dispose();
        }
        else
        {
            Tag = (byte)frameNumber;
            Image = SpriteGameMain.ExplosionImages[frameNumber];
        }
    }
}

// -- Enemy Planes -- //

public sealed class EnemyPlane : Sprite
{
    private readonly int _height;

    public EnemyPlane()
    {
        Bitmap image = SpriteGameMain.EnemyPlaneImage;
        int w = image.Width;
        _height = image.Height;
        Image = image;
        CollideRect = new PDRect(0, 0, w, _height);
        MoveTo(SpriteGameMain.Random.Next(400) - w / 2, -SpriteGameMain.Random.Next(30) - _height);
        ZIndex = 500;
        Add();
        Tag = SpriteGameMain.TagEnemyPlane;
        SpriteGameMain.EnemyCount += 1;
    }

    protected override bool HasCollisionResponse => true;

    protected override CollisionResponseType CollisionResponse(Sprite other)
    {
        Sys.Log("return Overlap from Enemy Plane!");
        return CollisionResponseType.Overlap;
    }

    protected override void Update()
    {
        GetPosition(out float x, out float y);
        int newY = (int)y + 4;

        if (newY > 400 + _height) // enemy plane flew offscreen
        {
            Dispose();
            SpriteGameMain.EnemyCount -= 1;
        }
        else
        {
            MoveTo(x, newY);
        }
    }

    public void Destroy()
    {
        GetPosition(out float x, out float y);
        _ = new Explosion(x, y);
        Dispose();
        SpriteGameMain.EnemyCount -= 1;
    }
}

// -- Background Planes -- //

public sealed class BackgroundPlane : Sprite
{
    private readonly int _height;

    public BackgroundPlane()
    {
        Bitmap image = SpriteGameMain.BackgroundPlaneImage;
        int w = image.Width;
        _height = image.Height;
        Image = image;
        MoveTo(SpriteGameMain.Random.Next(400) - w / 2, -_height);
        ZIndex = 100;
        Add();
        SpriteGameMain.BackgroundPlaneCount += 1;
    }

    protected override void Update()
    {
        GetPosition(out float x, out float y);
        int newY = (int)y + 2;

        if (newY > 400 + _height)
        {
            Dispose();
            SpriteGameMain.BackgroundPlaneCount -= 1;
        }
        else
        {
            MoveTo(x, newY);
        }
    }
}

// -- Player Plane -- //

public sealed class Bullet : Sprite
{
    private readonly int _height;

    public Bullet(PDRect playerBounds)
    {
        Bitmap image = SpriteGameMain.BulletImage;
        int w = image.Width;
        _height = image.Height;
        Image = image;
        CollideRect = new PDRect(0, 0, w, _height);
        MoveTo(playerBounds.X + playerBounds.Width / 2, playerBounds.Y);
        ZIndex = 999;
        Add();
        Tag = SpriteGameMain.TagPlayerBullet;
    }

    protected override bool HasCollisionResponse => true;

    protected override CollisionResponseType CollisionResponse(Sprite other) => CollisionResponseType.Overlap;

    protected override void Update()
    {
        GetPosition(out float x, out float y);
        int newY = (int)y - 20;

        if (newY < -_height) // bullet is offscreen, remove it
        {
            Dispose();
            return;
        }

        bool hit = false;
        using (CollisionList collisions = MoveWithCollisions(x, newY, out _, out _))
        {
            for (int i = 0; i < collisions.Count; i++)
            {
                if (collisions[i].OtherSprite is EnemyPlane enemy)
                {
                    enemy.Destroy();
                    hit = true;
                    SpriteGameMain.Score += 1;
                    Sys.Log($"Score: {SpriteGameMain.Score}");
                }
            }
        }

        if (hit)
            Dispose();
    }
}

public sealed class Player : Sprite
{
    public Player(int centerX, int centerY)
    {
        Bitmap image = SpriteGameMain.LoadImageAtPath("images/player");
        int w = image.Width;
        int h = image.Height;
        Image = image;
        CollideRect = new PDRect(5, 5, w - 10, h - 10);
        MoveTo(centerX, centerY);
        ZIndex = 1000;
        Add();
        Tag = SpriteGameMain.TagPlayer;
        SpriteGameMain.BackgroundPlaneCount += 1; // as in the C example
    }

    protected override bool HasCollisionResponse => true;

    protected override CollisionResponseType CollisionResponse(Sprite other) => CollisionResponseType.Overlap;

    public void Fire() => _ = new Bullet(Bounds);

    protected override void Update()
    {
        Buttons current = Sys.ButtonsCurrent;

        int dx = 0;
        int dy = 0;

        if ((current & Buttons.Up) != 0)
            dy = -4;
        else if ((current & Buttons.Down) != 0)
            dy = 4;

        if ((current & Buttons.Left) != 0)
            dx = -4;
        else if ((current & Buttons.Right) != 0)
            dx = 4;

        GetPosition(out float x, out float y);

        using CollisionList collisions = MoveWithCollisions(x + dx, y + dy, out _, out _);
        for (int i = 0; i < collisions.Count; i++)
        {
            if (collisions[i].OtherSprite is EnemyPlane enemy)
            {
                enemy.Destroy();
                SpriteGameMain.Score -= 1;
                Sys.Log($"Score: {SpriteGameMain.Score}");
            }
        }
    }
}
