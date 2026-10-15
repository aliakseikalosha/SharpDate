# Sprites

`Sprite`, `Sprites` and the collision types. Full member list: [Sprite.md](../api/Sprite.md).

The sprite system (Lua `gfx.sprite`) keeps a display list of sprites, calls their update functions,
redraws only what changed, and moves sprites with collision detection. In SharpDate a sprite is a C#
object: derive from `Sprite` and override its callbacks.

## Sprite

| Member | Meaning |
|---|---|
| `new Sprite()` / subclass constructor | creates the native sprite; not visible until `Add()` |
| `Add()` / `Remove()` | put on / take off the display list |
| `Dispose()` | remove and free; safe from inside `Update`; don't use the sprite afterwards |
| `Update()` *(override)* | called every frame by `Sprites.UpdateAndDrawSprites()` |
| `Image`, `SetImage(image, flip)` | the bitmap drawn (not owned by the sprite) |
| `MoveTo(x, y)`, `MoveBy(dx, dy)`, `X`, `Y`, `GetPosition` | position of the center point (no collision checks) |
| `SetCenter(cx, cy)` | which point `MoveTo` places: fractions of the size, default 0.5, 0.5; 0, 0 = top-left |
| `Bounds`, `SetSize(w, h)` | rectangle in world coordinates / size for sprites without an image |
| `ZIndex` | draw order (higher on top), -32768..32767 |
| `Visible`, `UpdatesEnabled`, `Opaque`, `IgnoresDrawOffset` | drawing and update flags |
| `ImageFlip`, `DrawMode` | how the image is drawn |
| `Tag` | a number 0-255 you choose, e.g. a sprite kind |
| `SetClipRect`, `SetStencilImage`, `SetStencilPattern`, `ClearStencil` | per-sprite clipping and stencils |
| `Tilemap` | use a `TileMap` as the image |
| `HasDrawFunction` + `Draw(bounds, drawRect)` *(override)* | custom drawing instead of an image |
| `MarkDirty()`, `MarkDirtyRect(rect)` | force a redraw (custom-drawn sprites) |
| `CollideRect`, `ClearCollideRect()`, `CollisionsEnabled` | collision shape, relative to the bounds' top-left |
| `HasCollisionResponse` + `CollisionResponse(other)` *(override)* | how to react to each sprite hit |
| `MoveWithCollisions(x, y, out ax, out ay)` | move with collision resolution; returns a `CollisionList` |
| `CheckCollisions(...)` | the same without moving |
| `OverlappingSprites()` | sprites overlapping now |
| `Sprite.FromHandle(LCDSprite*)` | the C# object for a native sprite |

Lua features that have no C counterpart: collision groups (`setGroups`/`setCollidesWithGroups`) —
use `Tag` and decide in `CollisionResponse`; `setAnimator`, `setRedrawsOnImageChange`,
`getAllSprites` (keep your own `List<Sprite>`), `setBackgroundDrawingCallback` (see the background
sprite below).

### A moving sprite

```csharp
public sealed class Ball : Sprite
{
    private float _dx = 3, _dy = 2;

    public Ball(Bitmap image)
    {
        Image = image;            // the sprite takes its size from the image
        MoveTo(200, 120);         // the center of the sprite goes to (200, 120)
        ZIndex = 10;
        Add();                    // now it's on screen
    }

    // Called by Sprites.UpdateAndDrawSprites() every frame.
    protected override void Update()
    {
        GetPosition(out float x, out float y);
        if (x < 10 || x > 390) _dx = -_dx;
        if (y < 10 || y > 230) _dy = -_dy;
        MoveBy(_dx, _dy);
    }
}

public sealed class BallGame : Game
{
    private Bitmap _ballImage;

    protected override void Initialize()
    {
        // The bitmap belongs to us, not to the sprites: several sprites can share it.
        _ballImage = Bitmap.Create(20, 20, SolidColor.Clear);
        Graphics.PushContext(_ballImage);
        Graphics.FillEllipse(0, 0, 20, 20, 0, 0, SolidColor.Black);
        Graphics.PopContext();

        for (int i = 0; i < 5; i++)
            new Ball(_ballImage).MoveBy(i * 10, i * 7);   // the sprite system keeps them alive
    }

    public override bool Update()
    {
        Sprites.UpdateAndDrawSprites();   // update every sprite, redraw what changed
        return true;
    }
}
```

## Collisions

Give a sprite a `CollideRect` (relative to its top-left corner) and move it with
`MoveWithCollisions`. For every sprite in the way, the moving sprite's `CollisionResponse(other)`
decides what happens:

| `CollisionResponseType` | Effect | Typical use |
|---|---|---|
| `Slide` | stop moving into the other sprite, keep moving along it | walls, floors |
| `Freeze` *(default)* | stop at the contact point | arrows, projectiles that stick |
| `Overlap` | pass through; only reported | coins, triggers, damage zones |
| `Bounce` | reflect off it | balls |

`MoveWithCollisions` returns a `CollisionList`, a native array of `SpriteCollisionInfo`: dispose it
(use `using`). Each entry has `OtherSprite` (your C# object), `ResponseType`, `Normal` (-1/0/1 per
axis: `Normal.Y == -1` means you landed on top of the other sprite), `Touch`, `Move`, `Ti` (0-1 along
the move) and the two rectangles. `Overlaps` is true if the sprites already overlapped before the move.

```csharp
public sealed class Wall : Sprite
{
    public Wall(float x, float y, float w, float h)
    {
        SetCenter(0, 0);                        // position = top-left corner
        SetSize(w, h);                          // no image: the size comes from SetSize
        CollideRect = new PDRect(0, 0, w, h);   // collide over the whole sprite
        MoveTo(x, y);
        Tag = 1;
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
        => Graphics.FillRect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, Pattern.Gray50);
}

public sealed class Coin : Sprite
{
    public Coin(float x, float y)
    {
        SetSize(10, 10);
        CollideRect = new PDRect(0, 0, 10, 10);
        MoveTo(x, y);
        Tag = 2;
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
        => Graphics.DrawEllipse((int)bounds.X, (int)bounds.Y, 10, 10, 2, 0, 0, SolidColor.Black);
}

public sealed class Hero : Sprite
{
    public int Coins;
    public bool OnGround;
    private float _vy;

    public Hero()
    {
        SetSize(16, 24);
        CollideRect = new PDRect(2, 0, 12, 24);  // a bit narrower than the sprite
        MoveTo(50, 50);
        Add();
    }

    // Opt in to per-sprite responses (otherwise every collision is Freeze).
    protected override bool HasCollisionResponse => true;

    protected override CollisionResponseType CollisionResponse(Sprite other)
        => other is Coin ? CollisionResponseType.Overlap : CollisionResponseType.Slide;

    protected override void Update()
    {
        float dx = Sys.IsPressed(Buttons.Left) ? -3 : Sys.IsPressed(Buttons.Right) ? 3 : 0;
        if (OnGround && Sys.JustPressed(Buttons.A))
            _vy = -8;                                 // jump
        _vy = MathF.Min(_vy + 0.5f, 8);               // gravity

        GetPosition(out float x, out float y);
        OnGround = false;

        // Move and resolve collisions. The list is a native array: 'using' frees it.
        using CollisionList hits = MoveWithCollisions(x + dx, y + _vy, out _, out _);
        for (int i = 0; i < hits.Count; i++)
        {
            ref SpriteCollisionInfo hit = ref hits[i];
            if (hit.OtherSprite is Coin coin)
            {
                Coins++;
                coin.Dispose();                       // removing other sprites here is fine
            }
            else if (hit.Normal.Y == -1)              // we hit the top of a wall: standing on it
            {
                OnGround = true;
                _vy = 0;
            }
            else if (hit.Normal.Y == 1)               // bumped our head
                _vy = 0;
        }
    }

    // Custom drawing, since this sprite has no image.
    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
        => Graphics.FillRect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, SolidColor.Black);
}

public sealed class PlatformGame : Game
{
    private Hero _hero;

    protected override void Initialize()
    {
        new Wall(0, 220, 400, 20);                    // floor
        new Wall(150, 160, 100, 10);                  // platform
        for (int i = 0; i < 5; i++)
            new Coin(170 + i * 15, 140);
        _hero = new Hero();
    }

    public override bool Update()
    {
        Sprites.UpdateAndDrawSprites();
        Graphics.DrawText("Coins: " + _hero.Coins, 4, 4);
        return true;
    }
}
```

`CheckCollisions` answers "what would happen if I moved there?" without moving. A sprite without a
collide rect, or with `CollisionsEnabled = false`, never collides.

## Custom drawing and a background

A sprite with `HasDrawFunction => true` is drawn by its `Draw(bounds, drawRect)` override, in screen
coordinates: `bounds` is where the sprite is, `drawRect` the part being redrawn. It needs a size
(`SetSize` or `Bounds`). Call `MarkDirty()` when what it draws changes.

Lua's `setBackgroundDrawingCallback` is a full-screen sprite at the lowest Z index:

```csharp
public sealed class Background : Sprite
{
    private int _offset;

    public Background()
    {
        SetCenter(0, 0);
        Bounds = new PDRect(0, 0, 400, 240);
        ZIndex = short.MinValue;      // behind everything
        IgnoresDrawOffset = true;     // a fixed backdrop, even with a camera offset
        Add();
    }

    protected override bool HasDrawFunction => true;

    protected override void Draw(PDRect bounds, PDRect drawRect)
    {
        // Only redraw the area the sprite system asks for.
        Graphics.SetClipRect((int)drawRect.X, (int)drawRect.Y, (int)drawRect.Width, (int)drawRect.Height);
        for (int y = 0; y < 240; y += 20)
            Graphics.DrawLine(0, (y + _offset) % 240, 400, (y + _offset) % 240, 1, SolidColor.Black);
        Graphics.ClearClipRect();
    }

    protected override void Update()
    {
        _offset = (_offset + 1) % 20;
        MarkDirty();                  // the picture changed: redraw it
    }
}
```

## Sprites (the whole system)

| Member | Meaning |
|---|---|
| `Sprites.UpdateAndDrawSprites()` | update all sprites, then draw — call once per frame (Lua `gfx.sprite.update()`) |
| `Sprites.DrawSprites()` | draw without updating |
| `Sprites.Count` | sprites in the display list |
| `Sprites.RemoveAllSprites()` | empty the display list (sprites are not freed) |
| `Sprites.SetAlwaysRedraw(true)` | redraw everything each frame (faster with many moving sprites) |
| `Sprites.AddDirtyRect(rect)` | force a screen area to be redrawn |
| `Sprites.SetClipRectsInRange(rect, z1, z2)` / `ClearClipRectsInRange` | clip all sprites in a Z range (e.g. a HUD layer) |
| `Sprites.ResetCollisionWorld()` | rebuild collision data |
| `Sprites.QuerySpritesAtPoint(x, y)` | sprites whose collide rect contains the point |
| `Sprites.QuerySpritesInRect(x, y, w, h)` | ... that intersect the rectangle |
| `Sprites.QuerySpritesAlongLine(x1, y1, x2, y2)` | ... crossed by the line |
| `Sprites.QuerySpriteInfoAlongLine(...)` | ... with entry/exit points (`SpriteQueryList`) |
| `Sprites.AllOverlappingSprites()` | every overlapping pair (0 & 1, 2 & 3, ...) |

Queries return native lists; dispose them.

```csharp
public sealed class LaserGame : Game
{
    private Bitmap _box;

    protected override void Initialize()
    {
        _box = Bitmap.Create(20, 20, SolidColor.Black);   // one image shared by all targets
        for (int i = 0; i < 6; i++)
        {
            var target = new Sprite();          // plain sprites work for simple objects
            target.Image = _box;
            target.CollideRect = new PDRect(0, 0, 20, 20);
            target.MoveTo(60 + i * 55, 60 + (i % 3) * 50);
            target.Add();
        }
    }

    public override bool Update()
    {
        Sprites.UpdateAndDrawSprites();

        // A laser from the left edge toward the crank direction.
        float radians = (Sys.CrankAngle - 90) * MathF.PI / 180;
        float x2 = 400 * MathF.Cos(radians), y2 = 120 + 400 * MathF.Sin(radians);
        Graphics.DrawLine(0, 120, (int)x2, (int)y2, 1, SolidColor.Black);

        // Which targets does the beam cross, and where does it first touch each one?
        using SpriteQueryList hits = Sprites.QuerySpriteInfoAlongLine(0, 120, x2, y2);
        for (int i = 0; i < hits.Count; i++)
        {
            CollisionPoint p = hits[i].EntryPoint;
            Graphics.FillEllipse((int)p.X - 3, (int)p.Y - 3, 6, 6, 0, 0, SolidColor.XOR);
        }

        // Sprites under a point (e.g. a cursor).
        using SpriteList under = Sprites.QuerySpritesAtPoint(200, 120);
        Graphics.DrawText(under.Count > 0 ? "on target" : "", 4, 4);
        return true;
    }
}
```

## Lifetime rules

- A sprite stays alive while it is registered with the sprite system, even if no C# variable refers
  to it. `Dispose()` ends that: it removes the sprite, frees it, and lets the GC collect the object.
- After `Dispose()`, don't call methods on the sprite; check your own flag if other code may still
  hold it (e.g. two bullets hitting the same enemy in one frame).
- The sprite's `Image` is not freed by the sprite; dispose bitmaps when no sprite uses them.
- `CollisionList`, `SpriteList` and `SpriteQueryList` are valid until disposed; don't keep them
  across frames.
