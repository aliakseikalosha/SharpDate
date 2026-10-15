# Graphics

`Graphics`, colors and patterns, `Bitmap`, `BitmapTable`, `Font`, `TileMap`, `VideoPlayer`.
Full member lists: [Graphics](../api/Graphics.md), [Display / TileMap / VideoPlayer](../api/Display.md).

The screen is 400 x 240 pixels, 1 bit per pixel. Everything is drawn into the frame buffer, which the
system sends to the display after `Update` returns `true`. Unlike Lua, there is no "current color":
every drawing call takes its color.

## Graphics: drawing state

| Member | Meaning | Lua |
|---|---|---|
| `Clear(color)` | fill the screen (or the pushed bitmap) | `gfx.clear` |
| `SetDrawOffset(dx, dy)` | move everything drawn afterwards (camera) | `gfx.setDrawOffset` |
| `SetClipRect(x, y, w, h)` / `SetScreenClipRect` / `ClearClipRect()` | only draw inside a rectangle (world / screen coordinates) | `gfx.setClipRect` ... |
| `SetDrawMode(mode)` | how bitmaps and text are combined with the screen; returns the old mode | `gfx.setImageDrawMode` |
| `SetLineCapStyle(style)` | ends of thick lines | `gfx.setLineCapStyle` |
| `SetStencilImage(bitmap, tile)` / `ClearStencil()` | only draw where the stencil is white | `gfx.setStencilImage` |
| `PushContext(bitmap)` / `PopContext()` | draw into a bitmap instead of the screen | `gfx.pushContext` / `popContext` |
| `SetBackgroundColor(color)` | color behind display offsets and sprite dirty areas | `gfx.setBackgroundColor` |
| `SetFont(font)`, `SetTextTracking(px)`, `SetTextLeading(px)` | text settings | `gfx.setFont`, `font:setTracking`, `font:setLeading` |

All of this is reset at the start of each frame (the context stack is unwound, the clip rect cleared).

## Graphics: shapes

| Method | Draws |
|---|---|
| `DrawLine(x1, y1, x2, y2, width, color)` | a line `width` pixels thick |
| `DrawRect` / `FillRect(x, y, w, h, color)` | rectangle outline (1 px) / filled |
| `DrawRoundRect(x, y, w, h, radius, lineWidth, color)` / `FillRoundRect(..., radius, color)` | rounded rectangles |
| `DrawEllipse(x, y, w, h, lineWidth, startAngle, endAngle, color)` | ellipse inside the rectangle; equal angles = whole ellipse, otherwise an arc (degrees clockwise from 12 o'clock) |
| `FillEllipse(x, y, w, h, startAngle, endAngle, color)` | filled ellipse or pie wedge |
| `FillTriangle(x1, y1, x2, y2, x3, y3, color)` | filled triangle |
| `FillPolygon(coords, color, fillRule)` | filled polygon from `x0, y0, x1, y1, ...` |
| `SetPixel(x, y, color)` | one pixel (slow in bulk; see the frame buffer below) |

Lua functions without a direct C counterpart: `drawCircleAtPoint(x, y, r)` is
`DrawEllipse(x - r, y - r, 2 * r, 2 * r, 1, 0, 0, color)`; `drawArc` is `DrawEllipse` with angles;
`drawTriangle` and `drawPolygon` are a series of `DrawLine` calls; `setLineWidth` is the width
argument of `DrawLine`, `DrawEllipse` and `DrawRoundRect`.

```csharp
public sealed class ShapesDemo : Game
{
    // Polygon points: x0, y0, x1, y1, ... (a star)
    private static readonly int[] Star = { 300, 40, 315, 85, 360, 85, 323, 110, 337, 155, 300, 128, 263, 155, 277, 110, 240, 85, 285, 85 };
    private float _angle;

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);

        Graphics.DrawLine(10, 10, 150, 60, 3, SolidColor.Black);            // 3 px thick line
        Graphics.DrawRect(10, 80, 60, 40, SolidColor.Black);                // outline
        Graphics.FillRect(80, 80, 60, 40, Pattern.Gray50);                   // 50% dither
        Graphics.FillRoundRect(10, 140, 130, 40, 10, SolidColor.Black);     // radius 10

        // Circle of radius 30 centered at (200, 60): ellipses take the bounding rectangle.
        Graphics.DrawEllipse(170, 30, 60, 60, 2, 0, 0, SolidColor.Black);
        // Pie chart wedge growing every frame (angles clockwise from 12 o'clock).
        _angle = (_angle + 4) % 360;
        Graphics.FillEllipse(170, 120, 60, 60, 0, _angle, SolidColor.Black);

        Graphics.FillPolygon(Star, SolidColor.Black, PolygonFillRule.NonZero);
        Graphics.FillTriangle(250, 230, 300, 180, 350, 230, SolidColor.XOR); // inverts what's below
        return true;
    }
}
```

## Colors: SolidColor, LCDColor and Pattern

`SolidColor` is `Black`, `White`, `Clear` (draw nothing) or `XOR` (invert). Every drawing call takes an
`LCDColor`, which is either a solid color or a `Pattern`: an 8x8 tile of pixels plus an optional mask,
repeated over the shape (Lua `gfx.setPattern`). Bit 7 of each row byte is the leftmost pixel;
1 = white; in the mask 1 = opaque.

```csharp
public sealed class PatternDemo : Game
{
    // Diagonal stripes. Patterns live in native memory: keep them in fields, dispose when done.
    private readonly Pattern _stripes = new Pattern(0x11, 0x22, 0x44, 0x88, 0x11, 0x22, 0x44, 0x88);

    // Lua's setDitherPattern(0.5) equivalent: a 50% checkerboard, built in.
    private readonly Pattern _gray = Pattern.Gray50;

    // Black dots on a transparent background: the mask makes the 0 bits see-through.
    private readonly Pattern _dots = new Pattern(
        0xFF, 0xFF, 0xFF, 0xE7, 0xE7, 0xFF, 0xFF, 0xFF,     // pixels: a 2x2 black dot (0 bits) in white
        0x00, 0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00);    // mask: only the dot is opaque

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.FillRect(0, 0, 200, 240, _stripes);
        Graphics.FillEllipse(220, 20, 160, 100, 0, 0, _gray);
        Graphics.FillRect(100, 140, 200, 80, _dots);   // the stripes show through between the dots
        return true;
    }
}
```

`Pattern.FromBitmap(bitmap, x, y)` takes the pattern from an 8x8 area of a bitmap.

## Graphics: text

| Method | Meaning |
|---|---|
| `DrawText(text, x, y)` | draws one or more lines (`\n`), top-left at (x, y); returns the width |
| `DrawText("..."u8, x, y)` / `DrawText(span, x, y)` | the same without allocating a string |
| `DrawTextInRect(text, x, y, w, h, wrap, align)` | wrapped (`Word`, `Character`, `Clip`) and aligned (`Left`, `Center`, `Right`) |
| `GetTextWidth(font, text)` / `font.GetTextWidth(text)` | width of one line |
| `GetTextHeightForMaxWidth(font, text, maxWidth)` | height when wrapped |
| `Font.Load(path)`, `font.Height` | fonts: `/System/Fonts/...` or `.fnt` files in your Source folder |

Without `SetFont`, text uses the system font (Asheville Sans 14 Light). Use `SetDrawMode(DrawMode.FillWhite)`
to draw white text on a dark background.

```csharp
public sealed class TextDemo : Game
{
    private Font _title, _body;
    private int _score = 12345;

    protected override void Initialize()
    {
        _title = Font.Load("/System/Fonts/Asheville-Sans-24-Light.pft");
        _body = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);

        // Centered title: measure, then draw (Lua's drawTextAligned).
        Graphics.SetFont(_title);
        string title = "Space Duck";
        Graphics.DrawText(title, (400 - _title.GetTextWidth(title)) / 2, 10);

        // Wrapped paragraph inside a box.
        Graphics.SetFont(_body);
        string story = "The duck has escaped into orbit. Collect the bread before the oxygen runs out!";
        int height = Graphics.GetTextHeightForMaxWidth(_body, story, 300);
        Graphics.DrawRect(45, 55, 310, height + 10, SolidColor.Black);
        Graphics.DrawTextInRect(story, 50, 60, 300, height, TextWrappingMode.Word, TextAlignment.Center);

        // White text on black: change the draw mode for text, then restore it.
        Graphics.FillRect(0, 200, 400, 40, SolidColor.Black);
        DrawMode old = Graphics.SetDrawMode(DrawMode.FillWhite);
        Span<char> digits = stackalloc char[11];        // format the score without allocating a string
        int n = _score, start = digits.Length;
        do { digits[--start] = (char)('0' + n % 10); n /= 10; } while (n > 0);
        Graphics.DrawText(digits.Slice(start), 10, 210);
        Graphics.SetDrawMode(old);
        return true;
    }
}
```

## Bitmap

An image (Lua `gfx.image`). Load images from the pdx (`Source/images/player.png` compiles to
`images/player`), create blank ones, or copy the screen. Images you create or load must be disposed.

| Member | Meaning | Lua |
|---|---|---|
| `Bitmap.Load(path)` / `TryLoad` / `LoadInto` | load from the pdx | `gfx.image.new(path)` |
| `Bitmap.Create(w, h, color)` | blank image (`SolidColor.Clear` = transparent) | `gfx.image.new(w, h, color)` |
| `Draw(x, y, flip)` / `Graphics.DrawBitmap` | draw at top-left (x, y), optionally mirrored | `image:draw` |
| `DrawScaled(x, y, sx, sy)` | scaled (negative = mirrored) | `image:drawScaled` |
| `DrawRotated(x, y, degrees, cx, cy, sx, sy)` | rotated around the point (cx, cy) given as fractions (0.5, 0.5 = middle) | `image:drawRotated` |
| `Rotated(degrees, sx, sy)` | a new rotated/scaled image (faster to draw repeatedly) | `image:rotatedImage`, `scaledImage` |
| `Graphics.TileBitmap(b, x, y, w, h)` | repeat over a rectangle | `image:drawTiled` |
| `Width`, `Height`, `GetPixel(x, y)`, `GetData(...)` | size, one pixel, raw bits | `image:getSize`, `image:sample` |
| `Mask` | the transparency mask as a bitmap | `image:getMaskImage` / `setMaskImage` |
| `Copy()`, `Clear(color)` | duplicate / fill | `image:copy`, `image:clear` |
| `Graphics.PushContext(b)` ... `PopContext()` | draw into the image | `gfx.pushContext(image)` |
| `Graphics.CheckMaskCollision(...)` | pixel-perfect overlap test | `image:collidesWith`-style checks |
| `Dispose()` | free it | (garbage collected) |

```csharp
public sealed class BitmapDemo : Game
{
    private Bitmap _ship, _shield;
    private float _rotation;

    protected override void Initialize()
    {
        // Source/images/ship.png -> "images/ship". Load stops the game with an error if it's missing;
        // TryLoad lets you handle it.
        if (!Bitmap.TryLoad("images/ship", out _ship, out string error))
        {
            Sys.Log("no ship image (" + error + "), drawing one");
            _ship = Bitmap.Create(32, 32, SolidColor.Clear);
            Graphics.PushContext(_ship);                                        // draw into the bitmap
            Graphics.FillTriangle(16, 0, 0, 31, 31, 31, SolidColor.Black);
            Graphics.PopContext();
        }

        // A pre-rendered, scaled-up copy: cheaper to draw every frame than DrawScaled.
        _shield = _ship.Rotated(0, 2, 2);
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);

        _ship.Draw(20, 20);                                       // top-left corner at (20, 20)
        _ship.Draw(60, 20, BitmapFlip.FlippedX);                  // mirrored
        _ship.DrawScaled(100, 20, 1.5f, 1.5f);

        // Spin around the ship's middle, which lands on (300, 120).
        _rotation = (_rotation + 5) % 360;
        _ship.DrawRotated(300, 120, _rotation, 0.5f, 0.5f);

        // Draw only the black pixels of the big copy, as an outline effect.
        DrawMode old = Graphics.SetDrawMode(DrawMode.WhiteTransparent);
        _shield.Draw(20, 100);
        Graphics.SetDrawMode(old);
        return true;
    }

    protected override void OnEvent(SystemEvent ev, uint arg)
    {
        if (ev == SystemEvent.Terminate)
        {
            _ship.Dispose();                                      // free what you created
            _shield.Dispose();
        }
    }
}
```

Pixel-perfect collision between two images, checked inside the screen rectangle:

<!-- context: Bitmap a = default, b = default; int ax = 0, ay = 0, bx = 0, by = 0; -->
```csharp
bool hit = Graphics.CheckMaskCollision(a, ax, ay, BitmapFlip.Unflipped, b, bx, by, BitmapFlip.Unflipped,
                                       LCDRect.Make(0, 0, 400, 240));
```

## BitmapTable

A list of same-size images: animation frames or tiles (Lua `gfx.imagetable`). `pdc` builds one from
`name-table-W-H.png` (a grid of W x H cells) or a folder of `name-table-1.png`, `name-table-2.png` ...
Images from a table belong to the table: draw them, but don't dispose them.

```csharp
public sealed class AnimationDemo : Game
{
    private BitmapTable _walk;      // Source/images/walk-table-32-32.png
    private int _frame;

    protected override void Initialize() => _walk = BitmapTable.Load("images/walk");

    public override bool Update()
    {
        // Advance one animation frame every 3 game frames; Count is the number of images.
        _frame++;
        Bitmap image = _walk[(_frame / 3) % _walk.Count];

        Graphics.Clear(SolidColor.White);
        image.Draw(184, 104);
        return true;
    }
}
```

## Font

`Font.Load(path)` loads a `.pft` (pdc compiles `.fnt` + `.png`); system fonts are in `/System/Fonts`
(e.g. `Asheville-Sans-14-Bold.pft`, `Roobert-11-Medium.pft`). `Height` is the line height,
`GetTextWidth(text)` measures. `GetGlyph`, `GetPage` and `GetGlyphKerning` give low-level access to
glyph bitmaps. Dispose fonts you no longer use (not while they are set with `SetFont`).

## Graphics: frame buffer

For effects that touch many pixels (Game of Life, fire, scanlines), write the frame buffer directly.
It is 240 rows of 52 bytes (only the first 50 are visible); bit 7 of a byte is the leftmost pixel and
1 = white. Tell the display which rows changed with `MarkUpdatedRows`.

```csharp
public sealed class NoiseDemo : Game
{
    private readonly Random _random = new Random();

    public override bool Update()
    {
        Span<byte> frame = Graphics.GetFrame();             // 240 * Graphics.RowSize bytes

        // Fill the top half with random noise, one byte (8 pixels) at a time.
        for (int y = 0; y < 120; y++)
            for (int x = 0; x < 50; x++)
                frame[y * Graphics.RowSize + x] = (byte)_random.Next(256);

        // Set one black pixel at (200, 180): clear its bit.
        int px = 200, py = 180;
        frame[py * Graphics.RowSize + px / 8] &= (byte)~(0x80 >> (px % 8));

        Graphics.MarkUpdatedRows(0, 239);                   // both ends included
        return true;
    }
}
```

`GetDisplayFrame()` is the last frame sent to the display, `CopyFrameBufferBitmap()` a copy of the
working frame as a bitmap (dispose it), `GetDisplayBufferBitmap()` the last frame as a system-owned
bitmap, and `GetDebugBitmap()` (Simulator only) an overlay where white pixels show as red.

## Graphics: clipping, stencils and offsets

```csharp
public sealed class CameraDemo : Game
{
    private int _cameraX;
    private Bitmap _stencil;

    protected override void Initialize()
    {
        // A circular "spotlight" stencil: drawing only happens where the stencil is white.
        _stencil = Bitmap.Create(400, 240, SolidColor.Black);
        Graphics.PushContext(_stencil);
        Graphics.FillEllipse(120, 40, 160, 160, 0, 0, SolidColor.White);
        Graphics.PopContext();
    }

    public override bool Update()
    {
        _cameraX += Sys.IsPressed(Buttons.Right) ? 4 : Sys.IsPressed(Buttons.Left) ? -4 : 0;

        Graphics.Clear(SolidColor.Black);
        Graphics.SetStencilImage(_stencil);

        // World drawing: shifted by the camera.
        Graphics.SetDrawOffset(-_cameraX, 0);
        for (int i = 0; i < 40; i++)
            Graphics.FillRect(i * 50, 100 + (i % 3) * 20, 30, 30, SolidColor.White);

        // HUD drawing: back to screen coordinates, no stencil, clipped to the top strip.
        Graphics.SetDrawOffset(0, 0);
        Graphics.ClearStencil();
        Graphics.SetScreenClipRect(0, 0, 400, 20);
        Graphics.FillRect(0, 0, 400, 40, SolidColor.White);   // only the top 20 rows appear
        Graphics.ClearClipRect();
        return true;
    }
}
```

## TileMap

A grid of tiles drawn from a `BitmapTable` in one call (Lua `gfx.tilemap`). Indexes are 0-based
positions in the image table.

```csharp
public sealed class TileMapDemo : Game
{
    private BitmapTable _tiles;           // Source/tiles-table-16-16.png
    private TileMap _map;
    private float _scroll;

    protected override void Initialize()
    {
        _tiles = BitmapTable.Load("tiles");
        _map = TileMap.Create();
        _map.ImageTable = _tiles;

        // A 40 x 15 map: tile 1 on the bottom row, tile 0 elsewhere. SetTiles sets the size too.
        const int width = 40, height = 15;
        ushort[] indexes = new ushort[width * height];
        for (int x = 0; x < width; x++)
            indexes[(height - 1) * width + x] = 1;
        _map.SetTiles(indexes, width);

        _map.SetTile(5, 10, 2);           // a single tile at column 5, row 10
    }

    public override bool Update()
    {
        _scroll += Sys.CrankChange;
        _map.GetPixelSize(out uint mapWidth, out _);
        float x = -(_scroll % (mapWidth - 400));

        Graphics.Clear(SolidColor.White);
        _map.Draw(x, 0);
        return true;
    }
}
```

## VideoPlayer

Plays `.pdv` videos (made with Panic's video converter; there is no sound track, play the audio with
a `FilePlayer`). Render one frame per update:

```csharp
public sealed class VideoDemo : Game
{
    private VideoPlayer _video;
    private int _frame, _frameCount;
    private float _frameRate;

    protected override void Initialize()
    {
        _video = VideoPlayer.Load("intro");          // Source/intro.pdv
        _video.UseScreenContext();                   // render straight to the screen
        _video.GetInfo(out _, out _, out _frameRate, out _frameCount, out _);
        Display.RefreshRate = _frameRate;            // update once per video frame
    }

    public override bool Update()
    {
        if (!_video.RenderFrame(_frame))
            Sys.Log("video error: " + _video.Error);
        _frame = (_frame + 1) % _frameCount;          // loop
        return true;
    }
}
```

## LCDRect and the enums

`LCDRect` is an integer rectangle given by edges (`Left`, `Top`, `Right`, `Bottom`; right and bottom
excluded). Create one with `LCDRect.Make(x, y, width, height)`. Sprites use the float `PDRect`
(x, y, width, height) instead.

`DrawMode`: `Copy` (default), `WhiteTransparent`, `BlackTransparent`, `FillWhite`, `FillBlack`,
`XOR`, `NXOR`, `Inverted`. `BitmapFlip`: `Unflipped`, `FlippedX`, `FlippedY`, `FlippedXY`.
`LineCapStyle`: `Butt`, `Square`, `Round`. `TextWrappingMode`: `Clip`, `Character`, `Word`.
`TextAlignment`: `Left`, `Center`, `Right`. `PolygonFillRule`: `NonZero`, `EvenOdd`.
