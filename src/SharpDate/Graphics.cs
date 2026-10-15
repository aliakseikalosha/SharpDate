using System;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// A drawing color: one of the <see cref="SolidColor"/> values or a pointer to an 8x8
    /// <see cref="Pattern"/> (C type <c>LCDColor</c>).
    /// </summary>
    public readonly unsafe struct LCDColor : IEquatable<LCDColor>
    {
        public readonly nuint Value;

        public LCDColor(nuint value) { Value = value; }

        public static LCDColor Black => new LCDColor((nuint)SolidColor.Black);
        public static LCDColor White => new LCDColor((nuint)SolidColor.White);
        public static LCDColor Clear => new LCDColor((nuint)SolidColor.Clear);
        public static LCDColor XOR => new LCDColor((nuint)SolidColor.XOR);

        public bool IsPattern => Value > (nuint)SolidColor.XOR;

        public static implicit operator LCDColor(SolidColor color) => new LCDColor((nuint)color);

        public bool Equals(LCDColor other) => Value == other.Value;
        public override bool Equals(object obj) => obj is LCDColor c && c.Value == Value;
        public override int GetHashCode() => (int)Value;
    }

    /// <summary>
    /// An 8x8 pattern (8 rows of pixel data followed by 8 rows of mask) in native memory, usable
    /// as an <see cref="LCDColor"/>. The memory stays valid until <see cref="Dispose"/>.
    /// </summary>
    public sealed unsafe class Pattern : IDisposable
    {
        private byte* _data;

        /// <summary>Creates an opaque pattern from 8 rows of pixel bits (1 = white).</summary>
        public Pattern(byte r0, byte r1, byte r2, byte r3, byte r4, byte r5, byte r6, byte r7)
            : this(r0, r1, r2, r3, r4, r5, r6, r7, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff)
        {
        }

        /// <summary>Creates a pattern with explicit mask rows (mask bit 1 = opaque).</summary>
        public Pattern(byte r0, byte r1, byte r2, byte r3, byte r4, byte r5, byte r6, byte r7,
                       byte m0, byte m1, byte m2, byte m3, byte m4, byte m5, byte m6, byte m7)
        {
            _data = (byte*)NativeMemory.Alloc(16);
            _data[0] = r0; _data[1] = r1; _data[2] = r2; _data[3] = r3;
            _data[4] = r4; _data[5] = r5; _data[6] = r6; _data[7] = r7;
            _data[8] = m0; _data[9] = m1; _data[10] = m2; _data[11] = m3;
            _data[12] = m4; _data[13] = m5; _data[14] = m6; _data[15] = m7;
        }

        /// <summary>Creates a pattern from a region of a bitmap (pd->graphics->setColorToPattern).</summary>
        public static Pattern FromBitmap(Bitmap bitmap, int x, int y)
        {
            var p = new Pattern(0, 0, 0, 0, 0, 0, 0, 0);
            nuint color = 0;
            PD.Graphics->setColorToPattern(&color, bitmap.Handle, x, y);
            if (color > 3)
                Buffer.MemoryCopy((void*)color, p._data, 16, 16);
            return p;
        }

        private static Pattern s_gray50;

        /// <summary>A 50% checkerboard dither.</summary>
        public static Pattern Gray50 => s_gray50 ??= new Pattern(0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55);

        public byte* Data => _data;

        public LCDColor Color => new LCDColor((nuint)_data);

        public static implicit operator LCDColor(Pattern pattern) => pattern.Color;

        public void Dispose()
        {
            NativeMemory.Free(_data);
            _data = null;
        }
    }

    /// <summary>Wraps <c>pd->graphics</c>: drawing primitives, text, bitmaps and the frame buffer.</summary>
    public static unsafe class Graphics
    {
        private static playdate_graphics* Api => PD.Graphics;

        public const int Width = PD.LCD_COLUMNS;
        public const int Height = PD.LCD_ROWS;
        public const int RowSize = PD.LCD_ROWSIZE;

        // ---- Drawing state ----

        public static void Clear(LCDColor color) => Api->clear(color.Value);
        public static void SetBackgroundColor(SolidColor color) => Api->setBackgroundColor(color);
        public static DrawMode SetDrawMode(DrawMode mode) => Api->setDrawMode(mode);
        public static void SetDrawOffset(int dx, int dy) => Api->setDrawOffset(dx, dy);
        public static void SetClipRect(int x, int y, int width, int height) => Api->setClipRect(x, y, width, height);
        public static void SetScreenClipRect(int x, int y, int width, int height) => Api->setScreenClipRect(x, y, width, height);
        public static void ClearClipRect() => Api->clearClipRect();
        public static void SetLineCapStyle(LineCapStyle style) => Api->setLineCapStyle(style);
        public static void SetFont(Font font) => Api->setFont(font.Handle);
        public static void SetTextTracking(int tracking) => Api->setTextTracking(tracking);
        public static int GetTextTracking() => Api->getTextTracking();
        public static void SetTextLeading(int lineHeightAdjustment) => Api->setTextLeading(lineHeightAdjustment);
        public static void SetStencilImage(Bitmap stencil, bool tile = false) => Api->setStencilImage(stencil.Handle, tile ? 1 : 0);
        public static void ClearStencil() => Api->setStencilImage(null, 0);

        /// <summary>Redirects drawing into <paramref name="target"/> (or the screen when target is null) until <see cref="PopContext"/>.</summary>
        public static void PushContext(Bitmap target) => Api->pushContext(target.Handle);
        public static void PopContext() => Api->popContext();

        // ---- Primitives ----

        public static void DrawLine(int x1, int y1, int x2, int y2, int width, LCDColor color) => Api->drawLine(x1, y1, x2, y2, width, color.Value);
        public static void FillTriangle(int x1, int y1, int x2, int y2, int x3, int y3, LCDColor color) => Api->fillTriangle(x1, y1, x2, y2, x3, y3, color.Value);
        public static void DrawRect(int x, int y, int width, int height, LCDColor color) => Api->drawRect(x, y, width, height, color.Value);
        public static void FillRect(int x, int y, int width, int height, LCDColor color) => Api->fillRect(x, y, width, height, color.Value);
        public static void DrawRoundRect(int x, int y, int width, int height, int radius, int lineWidth, LCDColor color) => Api->drawRoundRect(x, y, width, height, radius, lineWidth, color.Value);
        public static void FillRoundRect(int x, int y, int width, int height, int radius, LCDColor color) => Api->fillRoundRect(x, y, width, height, radius, color.Value);

        /// <summary>Draws an ellipse stroked inside the rect; angles in degrees (0,0 = full ellipse).</summary>
        public static void DrawEllipse(int x, int y, int width, int height, int lineWidth, float startAngle, float endAngle, LCDColor color)
            => Api->drawEllipse(x, y, width, height, lineWidth, startAngle, endAngle, color.Value);

        public static void FillEllipse(int x, int y, int width, int height, float startAngle, float endAngle, LCDColor color)
            => Api->fillEllipse(x, y, width, height, startAngle, endAngle, color.Value);

        public static void SetPixel(int x, int y, LCDColor color) => Api->setPixel(x, y, color.Value);

        /// <summary>Fills a polygon; <paramref name="coords"/> holds x0, y0, x1, y1, ...</summary>
        public static void FillPolygon(ReadOnlySpan<int> coords, LCDColor color, PolygonFillRule fillRule = PolygonFillRule.NonZero)
        {
            fixed (int* p = coords)
                Api->fillPolygon(coords.Length / 2, p, color.Value, fillRule);
        }

        // ---- Bitmaps ----

        public static void DrawBitmap(Bitmap bitmap, int x, int y, BitmapFlip flip = BitmapFlip.Unflipped) => Api->drawBitmap(bitmap.Handle, x, y, flip);
        public static void TileBitmap(Bitmap bitmap, int x, int y, int width, int height, BitmapFlip flip = BitmapFlip.Unflipped) => Api->tileBitmap(bitmap.Handle, x, y, width, height, flip);
        public static void DrawScaledBitmap(Bitmap bitmap, int x, int y, float xScale, float yScale) => Api->drawScaledBitmap(bitmap.Handle, x, y, xScale, yScale);

        /// <summary>Draws a bitmap rotated by <paramref name="rotation"/> degrees around (centerX, centerY) in [0,1] bitmap coordinates.</summary>
        public static void DrawRotatedBitmap(Bitmap bitmap, int x, int y, float rotation, float centerX, float centerY, float xScale, float yScale)
            => Api->drawRotatedBitmap(bitmap.Handle, x, y, rotation, centerX, centerY, xScale, yScale);

        public static bool CheckMaskCollision(Bitmap bitmap1, int x1, int y1, BitmapFlip flip1, Bitmap bitmap2, int x2, int y2, BitmapFlip flip2, LCDRect rect)
            => Api->checkMaskCollision(bitmap1.Handle, x1, y1, flip1, bitmap2.Handle, x2, y2, flip2, rect) != 0;

        // ---- Text ----

        /// <summary>Draws text with the current font; returns the width drawn.</summary>
        public static int DrawText(string text, int x, int y)
        {
            fixed (char* p = text)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y);
        }

        /// <summary>Draws UTF-8 text, e.g. <c>Graphics.DrawText("Hello"u8, x, y)</c>.</summary>
        public static int DrawText(ReadOnlySpan<byte> utf8, int x, int y)
        {
            fixed (byte* p = utf8)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(utf8), StringEncoding.UTF8, x, y);
        }

        /// <summary>Draws a span of characters (no string allocation needed for formatted numbers).</summary>
        public static int DrawText(ReadOnlySpan<char> text, int x, int y)
        {
            fixed (char* p = text)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y);
        }

        public static void DrawTextInRect(string text, int x, int y, int width, int height, TextWrappingMode wrap = TextWrappingMode.Word, TextAlignment align = TextAlignment.Left)
        {
            fixed (char* p = text)
                Api->drawTextInRect(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y, width, height, wrap, align);
        }

        public static int GetTextWidth(Font font, string text, int tracking = 0)
        {
            fixed (char* p = text)
                return Api->getTextWidth(font.Handle, p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, tracking);
        }

        public static int GetTextHeightForMaxWidth(Font font, string text, int maxWidth, TextWrappingMode wrap = TextWrappingMode.Word, int tracking = 0, int extraLeading = 0)
        {
            fixed (char* p = text)
                return Api->getTextHeightForMaxWidth(font.Handle, p, (nuint)Utf8.CodePointCount(text), maxWidth, StringEncoding.UTF16LE, wrap, tracking, extraLeading);
        }

        // ---- Frame buffer ----

        /// <summary>The working frame buffer: 240 rows of <see cref="RowSize"/> bytes, 1 bit per pixel (1 = white).</summary>
        public static Span<byte> GetFrame() => new Span<byte>(Api->getFrame(), RowSize * Height);

        /// <summary>The frame buffer currently shown on the display.</summary>
        public static Span<byte> GetDisplayFrame() => new Span<byte>(Api->getDisplayFrame(), RowSize * Height);

        public static byte* GetFramePointer() => Api->getFrame();

        /// <summary>After writing to the frame buffer directly, marks rows start..end (inclusive) as changed.</summary>
        public static void MarkUpdatedRows(int start, int end) => Api->markUpdatedRows(start, end);

        /// <summary>Copies the frame buffer to the display immediately (normally done after Update returns).</summary>
        public static void Display() => Api->display();

        /// <summary>A copy of the frame buffer as a bitmap (caller owns it).</summary>
        public static Bitmap CopyFrameBufferBitmap() => new Bitmap(Api->copyFrameBufferBitmap());

        /// <summary>The bitmap holding the last completed frame (do not free).</summary>
        public static Bitmap GetDisplayBufferBitmap() => new Bitmap(Api->getDisplayBufferBitmap());

        /// <summary>Simulator-only debug overlay bitmap (null on device).</summary>
        public static Bitmap GetDebugBitmap() => Api->getDebugBitmap == null ? default : new Bitmap(Api->getDebugBitmap());
    }

    /// <summary>A handle to a Playdate <c>LCDBitmap</c>. Bitmaps you create or load must be freed with <see cref="Dispose"/>.</summary>
    public readonly unsafe struct Bitmap : IEquatable<Bitmap>, IDisposable
    {
        public readonly LCDBitmap* Handle;

        public Bitmap(LCDBitmap* handle) { Handle = handle; }

        public bool IsNull => Handle == null;

        /// <summary>Creates a new bitmap filled with <paramref name="background"/>.</summary>
        public static Bitmap Create(int width, int height, LCDColor background) => new Bitmap(PD.Graphics->newBitmap(width, height, background.Value));

        /// <summary>Loads an image from the game's pdx (e.g. "images/player"). Reports an error and returns a null bitmap on failure.</summary>
        public static Bitmap Load(string path)
        {
            if (!TryLoad(path, out Bitmap bitmap, out string error))
                Sys.Error(string.Concat("Couldn't load bitmap ", path, ": ", error));
            return bitmap;
        }

        public static bool TryLoad(string path, out Bitmap bitmap, out string error)
        {
            byte* err = null;
            bitmap = new Bitmap(PD.Graphics->loadBitmap(Utf8.Arg(path), &err));
            error = bitmap.IsNull ? Utf8.ToString(err) : null;
            return !bitmap.IsNull;
        }

        public void LoadInto(string path)
        {
            byte* err = null;
            PD.Graphics->loadIntoBitmap(Utf8.Arg(path), Handle, &err);
            if (err != null)
                Sys.Error(string.Concat("Couldn't load bitmap ", path, ": ", Utf8.ToString(err)));
        }

        public Bitmap Copy() => new Bitmap(PD.Graphics->copyBitmap(Handle));

        public void Clear(LCDColor background) => PD.Graphics->clearBitmap(Handle, background.Value);

        /// <summary>Returns a new rotated/scaled copy of the bitmap.</summary>
        public Bitmap Rotated(float rotation, float xScale = 1, float yScale = 1)
        {
            int size;
            return new Bitmap(PD.Graphics->rotatedBitmap(Handle, rotation, xScale, yScale, &size));
        }

        public void GetData(out int width, out int height, out int rowBytes, out byte* mask, out byte* data)
        {
            int w, h, rb;
            byte* m, d;
            PD.Graphics->getBitmapData(Handle, &w, &h, &rb, &m, &d);
            width = w;
            height = h;
            rowBytes = rb;
            mask = m;
            data = d;
        }

        public int Width
        {
            get
            {
                int w, h, rb;
                byte* m, d;
                PD.Graphics->getBitmapData(Handle, &w, &h, &rb, &m, &d);
                return w;
            }
        }

        public int Height
        {
            get
            {
                int w, h, rb;
                byte* m, d;
                PD.Graphics->getBitmapData(Handle, &w, &h, &rb, &m, &d);
                return h;
            }
        }

        public SolidColor GetPixel(int x, int y) => PD.Graphics->getBitmapPixel(Handle, x, y);

        public Bitmap Mask
        {
            get => new Bitmap(PD.Graphics->getBitmapMask(Handle));
            set => PD.Graphics->setBitmapMask(Handle, value.Handle);
        }

        public void Draw(int x, int y, BitmapFlip flip = BitmapFlip.Unflipped) => PD.Graphics->drawBitmap(Handle, x, y, flip);

        public void DrawScaled(int x, int y, float xScale, float yScale) => PD.Graphics->drawScaledBitmap(Handle, x, y, xScale, yScale);

        public void DrawRotated(int x, int y, float rotation, float centerX = 0.5f, float centerY = 0.5f, float xScale = 1, float yScale = 1)
            => PD.Graphics->drawRotatedBitmap(Handle, x, y, rotation, centerX, centerY, xScale, yScale);

        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->freeBitmap(Handle);
        }

        public bool Equals(Bitmap other) => Handle == other.Handle;
        public override bool Equals(object obj) => obj is Bitmap b && b.Handle == Handle;
        public override int GetHashCode() => (int)(nint)Handle;
        public static bool operator ==(Bitmap a, Bitmap b) => a.Handle == b.Handle;
        public static bool operator !=(Bitmap a, Bitmap b) => a.Handle != b.Handle;
    }

    /// <summary>A handle to an image table (<c>LCDBitmapTable</c>), e.g. an animation strip.</summary>
    public readonly unsafe struct BitmapTable : IDisposable
    {
        public readonly LCDBitmapTable* Handle;

        public BitmapTable(LCDBitmapTable* handle) { Handle = handle; }

        public bool IsNull => Handle == null;

        public static BitmapTable Create(int count, int width, int height) => new BitmapTable(PD.Graphics->newBitmapTable(count, width, height));

        /// <summary>Loads an image table (e.g. "images/explosion" for explosion-table-32-32.png or a folder of frames).</summary>
        public static BitmapTable Load(string path)
        {
            byte* err = null;
            var table = new BitmapTable(PD.Graphics->loadBitmapTable(Utf8.Arg(path), &err));
            if (table.IsNull)
                Sys.Error(string.Concat("Couldn't load bitmap table ", path, ": ", Utf8.ToString(err)));
            return table;
        }

        public void LoadInto(string path)
        {
            byte* err = null;
            PD.Graphics->loadIntoBitmapTable(Utf8.Arg(path), Handle, &err);
            if (err != null)
                Sys.Error(string.Concat("Couldn't load bitmap table ", path, ": ", Utf8.ToString(err)));
        }

        /// <summary>Returns the bitmap at <paramref name="index"/> (owned by the table; do not free).</summary>
        public Bitmap this[int index] => new Bitmap(PD.Graphics->getTableBitmap(Handle, index));

        public int Count
        {
            get
            {
                int count, width;
                PD.Graphics->getBitmapTableInfo(Handle, &count, &width);
                return count;
            }
        }

        /// <summary>Number of cells across (for grid-based tables).</summary>
        public int CellsWide
        {
            get
            {
                int count, width;
                PD.Graphics->getBitmapTableInfo(Handle, &count, &width);
                return width;
            }
        }

        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->freeBitmapTable(Handle);
        }
    }

    /// <summary>A handle to a font (<c>LCDFont</c>).</summary>
    public readonly unsafe struct Font
    {
        public readonly LCDFont* Handle;

        public Font(LCDFont* handle) { Handle = handle; }

        public bool IsNull => Handle == null;

        /// <summary>Loads a font, e.g. "/System/Fonts/Asheville-Sans-14-Bold.pft". Reports an error on failure.</summary>
        public static Font Load(string path)
        {
            byte* err = null;
            var font = new Font(PD.Graphics->loadFont(Utf8.Arg(path), &err));
            if (font.IsNull)
                Sys.Error(string.Concat("Couldn't load font ", path, ": ", Utf8.ToString(err)));
            return font;
        }

        public static Font FromData(LCDFontData* data, bool wide, int dataLength) => new Font(PD.Graphics->makeFontFromData(data, wide ? 1 : 0, dataLength));

        public int Height => PD.Graphics->getFontHeight(Handle);

        public int GetTextWidth(string text, int tracking = 0) => Graphics.GetTextWidth(this, text, tracking);

        public LCDFontPage* GetPage(uint character) => PD.Graphics->getFontPage(Handle, character);

        /// <summary>Returns the glyph for <paramref name="character"/>, its bitmap and advance.</summary>
        public LCDFontGlyph* GetGlyph(uint character, out Bitmap bitmap, out int advance)
        {
            LCDBitmap* bmp;
            int adv;
            LCDFontGlyph* glyph = PD.Graphics->getFontGlyph(Handle, character, &bmp, &adv);
            bitmap = new Bitmap(bmp);
            advance = adv;
            return glyph;
        }

        public static int GetGlyphKerning(LCDFontGlyph* glyph, uint glyphCode, uint nextCode) => PD.Graphics->getGlyphKerning(glyph, glyphCode, nextCode);
    }
}
