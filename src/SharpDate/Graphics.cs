// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

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
        /// <summary>
        /// The raw C value: 0-3 for a <see cref="SolidColor"/>, otherwise a pointer to 16 pattern bytes.
        /// </summary>
        public readonly nuint Value;

        /// <summary>Wraps a raw C LCDColor value.</summary>
        /// <param name="value">0-3 for <see cref="SolidColor"/>, or the address of 16 pattern bytes.</param>
        public LCDColor(nuint value) { Value = value; }

        /// <summary>Solid black.</summary>
        public static LCDColor Black => new LCDColor((nuint)SolidColor.Black);
        /// <summary>Solid white.</summary>
        public static LCDColor White => new LCDColor((nuint)SolidColor.White);
        /// <summary>Transparent: nothing is drawn.</summary>
        public static LCDColor Clear => new LCDColor((nuint)SolidColor.Clear);
        /// <summary>Inverts the pixels underneath.</summary>
        public static LCDColor XOR => new LCDColor((nuint)SolidColor.XOR);

        /// <summary>True if this color is a pattern rather than a solid color.</summary>
        public bool IsPattern => Value > (nuint)SolidColor.XOR;

        /// <summary>
        /// Lets any <see cref="SolidColor"/> be passed where an <see cref="LCDColor"/> is expected.
        /// </summary>
        public static implicit operator LCDColor(SolidColor color) => new LCDColor((nuint)color);

        /// <summary>True if both refer to the same solid color or the same pattern memory.</summary>
        /// <param name="other">The color to compare with.</param>
        public bool Equals(LCDColor other) => Value == other.Value;
        /// <summary>True if <paramref name="obj"/> is an equal <see cref="LCDColor"/>.</summary>
        /// <param name="obj">The object to compare with.</param>
        public override bool Equals(object obj) => obj is LCDColor c && c.Value == Value;
        /// <summary>Hash of the raw value.</summary>
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
        /// <param name="r0">
        /// Row 0 (top) of the 8x8 tile: bit 7 = leftmost pixel, 1 = white, 0 = black.
        /// </param>
        /// <param name="r1">Row 1.</param>
        /// <param name="r2">Row 2.</param>
        /// <param name="r3">Row 3.</param>
        /// <param name="r4">Row 4.</param>
        /// <param name="r5">Row 5.</param>
        /// <param name="r6">Row 6.</param>
        /// <param name="r7">Row 7 (bottom).</param>
        public Pattern(byte r0, byte r1, byte r2, byte r3, byte r4, byte r5, byte r6, byte r7)
            : this(r0, r1, r2, r3, r4, r5, r6, r7, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff)
        {
        }

        /// <summary>Creates a pattern with explicit mask rows (mask bit 1 = opaque).</summary>
        /// <param name="r0">Pixel row 0 (top): bit 7 = leftmost pixel, 1 = white, 0 = black.</param>
        /// <param name="r1">Pixel row 1.</param>
        /// <param name="r2">Pixel row 2.</param>
        /// <param name="r3">Pixel row 3.</param>
        /// <param name="r4">Pixel row 4.</param>
        /// <param name="r5">Pixel row 5.</param>
        /// <param name="r6">Pixel row 6.</param>
        /// <param name="r7">Pixel row 7 (bottom).</param>
        /// <param name="m0">Mask row 0: bit 7 = leftmost pixel, 1 = drawn, 0 = transparent.</param>
        /// <param name="m1">Mask row 1.</param>
        /// <param name="m2">Mask row 2.</param>
        /// <param name="m3">Mask row 3.</param>
        /// <param name="m4">Mask row 4.</param>
        /// <param name="m5">Mask row 5.</param>
        /// <param name="m6">Mask row 6.</param>
        /// <param name="m7">Mask row 7.</param>
        public Pattern(byte r0, byte r1, byte r2, byte r3, byte r4, byte r5, byte r6, byte r7,
                       byte m0, byte m1, byte m2, byte m3, byte m4, byte m5, byte m6, byte m7)
        {
            _data = (byte*)NativeMemory.Alloc(16);
            _data[0] = r0; _data[1] = r1; _data[2] = r2; _data[3] = r3;
            _data[4] = r4; _data[5] = r5; _data[6] = r6; _data[7] = r7;
            _data[8] = m0; _data[9] = m1; _data[10] = m2; _data[11] = m3;
            _data[12] = m4; _data[13] = m5; _data[14] = m6; _data[15] = m7;
        }

        /// <summary>
        /// Creates a pattern from the 8x8 block of <paramref name="bitmap"/> whose top-left corner is (x, y),
        /// including its mask (C: setColorToPattern).
        /// </summary>
        /// <param name="bitmap">The source bitmap.</param>
        /// <param name="x">Left of the 8x8 block, 0 to width - 8.</param>
        /// <param name="y">Top of the 8x8 block, 0 to height - 8.</param>
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

        /// <summary>
        /// The 16 pattern bytes: 8 rows of pixels (bit 7 = leftmost, 1 = white), then 8 rows of mask (1 =
        /// opaque).
        /// </summary>
        public byte* Data => _data;

        /// <summary>
        /// The pattern as a color for the drawing functions. Valid until <see cref="Dispose"/>.
        /// </summary>
        public LCDColor Color => new LCDColor((nuint)_data);

        /// <summary>Lets a pattern be passed wherever a color is expected.</summary>
        public static implicit operator LCDColor(Pattern pattern) => pattern.Color;

        /// <summary>Frees the pattern memory. Don't draw with the pattern afterwards.</summary>
        public void Dispose()
        {
            NativeMemory.Free(_data);
            _data = null;
        }
    }

    /// <summary>
    /// Drawing on the screen or into a bitmap: shapes, text, bitmaps, clipping, stencils and direct frame
    /// buffer access (C: <c>pd->graphics</c>, Lua: <c>playdate.graphics</c>).
    /// </summary>
    /// <remarks>
    /// Coordinates are in pixels, (0, 0) is the top-left corner and the screen is <see cref="Width"/> x
    /// <see cref="Height"/>. Drawing goes to the screen unless a bitmap was pushed with
    /// <see cref="PushContext"/>. Drawing state (offset, clip rect, draw mode, stencil, font) is reset at the
    /// start of every frame. Colors are <see cref="LCDColor"/> values: a <see cref="SolidColor"/> or a
    /// <see cref="Pattern"/>. See docs/reference/graphics.md.
    /// </remarks>
    public static unsafe class Graphics
    {
        private static playdate_graphics* Api => PD.Graphics;

        /// <summary>Screen width in pixels (400).</summary>
        public const int Width = PD.LCD_COLUMNS;
        /// <summary>Screen height in pixels (240).</summary>
        public const int Height = PD.LCD_ROWS;
        /// <summary>Bytes per frame-buffer row (52: 50 bytes of pixels plus 2 bytes of padding).</summary>
        public const int RowSize = PD.LCD_ROWSIZE;

        // ---- Drawing state ----

        /// <summary>
        /// Fills the whole screen (or current bitmap context) with <paramref name="color"/> (Lua:
        /// playdate.graphics.clear).
        /// </summary>
        /// <param name="color">Fill color or pattern.</param>
        public static void Clear(LCDColor color) => Api->clear(color.Value);
        /// <summary>
        /// Sets the color used where the display is offset (<see cref="Display.SetOffset"/>) and to clear
        /// dirty areas behind sprites.
        /// </summary>
        /// <param name="color">
        /// <see cref="SolidColor.Black"/>, <see cref="SolidColor.White"/> or <see cref="SolidColor.Clear"/>.
        /// </param>
        public static void SetBackgroundColor(SolidColor color) => Api->setBackgroundColor(color);
        /// <summary>Sets how bitmaps and text are drawn (Lua: setImageDrawMode).</summary>
        /// <returns>The previous mode, so it can be restored.</returns>
        /// <param name="mode">The new mode.</param>
        public static DrawMode SetDrawMode(DrawMode mode) => Api->setDrawMode(mode);
        /// <summary>
        /// Offsets every following drawing call by (dx, dy), e.g. to scroll a camera over a world larger than
        /// the screen. Sprites are drawn with the offset too unless <see cref="Sprite.IgnoresDrawOffset"/> is
        /// set.
        /// </summary>
        /// <param name="dx">Added to every x coordinate (negative scrolls the view right).</param>
        /// <param name="dy">Added to every y coordinate.</param>
        public static void SetDrawOffset(int dx, int dy) => Api->setDrawOffset(dx, dy);
        /// <summary>
        /// Restricts drawing to a rectangle given in world coordinates (it is moved by the draw offset).
        /// Cleared at the start of every frame.
        /// </summary>
        /// <param name="x">Left edge in world coordinates.</param>
        /// <param name="y">Top edge in world coordinates.</param>
        /// <param name="width">Width in pixels, 1 or more.</param>
        /// <param name="height">Height in pixels, 1 or more.</param>
        public static void SetClipRect(int x, int y, int width, int height) => Api->setClipRect(x, y, width, height);
        /// <summary>
        /// Restricts drawing to a rectangle given in screen coordinates (ignores the draw offset).
        /// </summary>
        /// <param name="x">Left edge on screen, 0-399.</param>
        /// <param name="y">Top edge on screen, 0-239.</param>
        /// <param name="width">Width in pixels, 1 or more.</param>
        /// <param name="height">Height in pixels, 1 or more.</param>
        public static void SetScreenClipRect(int x, int y, int width, int height) => Api->setScreenClipRect(x, y, width, height);
        /// <summary>Removes the clip rect.</summary>
        public static void ClearClipRect() => Api->clearClipRect();
        /// <summary>Sets how the ends of lines drawn with <see cref="DrawLine"/> look.</summary>
        /// <param name="style">The cap style for following lines.</param>
        public static void SetLineCapStyle(LineCapStyle style) => Api->setLineCapStyle(style);
        /// <summary>
        /// Sets the font used by the text functions. Without a font the system font (Asheville Sans 14 Light)
        /// is used.
        /// </summary>
        /// <param name="font">A loaded font; keep it alive while it is set.</param>
        public static void SetFont(Font font) => Api->setFont(font.Handle);
        /// <summary>Sets extra spacing in pixels between characters (can be negative).</summary>
        /// <param name="tracking">Extra pixels between characters (default 0, can be negative).</param>
        public static void SetTextTracking(int tracking) => Api->setTextTracking(tracking);
        /// <summary>Returns the current text tracking.</summary>
        public static int GetTextTracking() => Api->getTextTracking();
        /// <summary>
        /// Adds <paramref name="lineHeightAdjustment"/> pixels to the font's line spacing for multi-line
        /// text.
        /// </summary>
        /// <param name="lineHeightAdjustment">
        /// Extra pixels between lines (default 0, can be negative).
        /// </param>
        public static void SetTextLeading(int lineHeightAdjustment) => Api->setTextLeading(lineHeightAdjustment);
        /// <summary>
        /// Only draws where <paramref name="stencil"/> is white. With <paramref name="tile"/> the stencil
        /// repeats; tiled stencils must be a multiple of 32 pixels wide.
        /// </summary>
        /// <param name="stencil">The stencil, in screen coordinates; white = draw, black = don't.</param>
        /// <param name="tile">
        /// Repeat the stencil over the screen; it must then be a multiple of 32 pixels wide.
        /// </param>
        public static void SetStencilImage(Bitmap stencil, bool tile = false) => Api->setStencilImage(stencil.Handle, tile ? 1 : 0);
        /// <summary>Removes the stencil.</summary>
        public static void ClearStencil() => Api->setStencilImage(null, 0);

        /// <summary>Redirects drawing into <paramref name="target"/> (or the screen when target is null) until <see cref="PopContext"/>.</summary>
        /// <param name="target">
        /// The bitmap to draw into; a null bitmap (<c>default</c>) draws to the screen with a fresh drawing
        /// state.
        /// </param>
        public static void PushContext(Bitmap target) => Api->pushContext(target.Handle);
        /// <summary>
        /// Ends drawing into the bitmap pushed with <see cref="PushContext"/> and restores the previous
        /// drawing settings.
        /// </summary>
        public static void PopContext() => Api->popContext();

        // ---- Primitives ----

        /// <summary>Draws a line from (x1, y1) to (x2, y2), <paramref name="width"/> pixels thick.</summary>
        /// <param name="x1">Start x.</param>
        /// <param name="y1">Start y.</param>
        /// <param name="x2">End x.</param>
        /// <param name="y2">End y.</param>
        /// <param name="width">Line thickness in pixels, 1 or more.</param>
        /// <param name="color">Color or pattern.</param>
        public static void DrawLine(int x1, int y1, int x2, int y2, int width, LCDColor color) => Api->drawLine(x1, y1, x2, y2, width, color.Value);
        /// <summary>Fills the triangle with corners (x1, y1), (x2, y2), (x3, y3).</summary>
        /// <param name="x1">First corner x.</param>
        /// <param name="y1">First corner y.</param>
        /// <param name="x2">Second corner x.</param>
        /// <param name="y2">Second corner y.</param>
        /// <param name="x3">Third corner x.</param>
        /// <param name="y3">Third corner y.</param>
        /// <param name="color">Color or pattern.</param>
        public static void FillTriangle(int x1, int y1, int x2, int y2, int x3, int y3, LCDColor color) => Api->fillTriangle(x1, y1, x2, y2, x3, y3, color.Value);
        /// <summary>Draws a 1-pixel rectangle outline with its top-left corner at (x, y).</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="color">Color or pattern of the 1-pixel outline.</param>
        public static void DrawRect(int x, int y, int width, int height, LCDColor color) => Api->drawRect(x, y, width, height, color.Value);
        /// <summary>Fills a rectangle with its top-left corner at (x, y).</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="color">Color or pattern.</param>
        public static void FillRect(int x, int y, int width, int height, LCDColor color) => Api->fillRect(x, y, width, height, color.Value);
        /// <summary>Draws a rounded rectangle outline inside the given rectangle.</summary>
        /// <param name="radius">Corner radius in pixels.</param>
        /// <param name="lineWidth">Outline thickness.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="color">Color or pattern.</param>
        public static void DrawRoundRect(int x, int y, int width, int height, int radius, int lineWidth, LCDColor color) => Api->drawRoundRect(x, y, width, height, radius, lineWidth, color.Value);
        /// <summary>Fills a rounded rectangle.</summary>
        /// <param name="radius">Corner radius in pixels.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="color">Color or pattern.</param>
        public static void FillRoundRect(int x, int y, int width, int height, int radius, LCDColor color) => Api->fillRoundRect(x, y, width, height, radius, color.Value);

        /// <summary>
        /// Draws an ellipse (or an arc) inside the rectangle {x, y, width, height}; the line is inset from
        /// the edges.
        /// </summary>
        /// <param name="lineWidth">Stroke width.</param>
        /// <param name="startAngle">Start of the arc in degrees, clockwise from 12 o'clock.</param>
        /// <param name="endAngle">
        /// End of the arc. Use the same value as <paramref name="startAngle"/> (e.g. 0, 0) for a full
        /// ellipse.
        /// </param>
        /// <param name="x">Left of the bounding rectangle.</param>
        /// <param name="y">Top of the bounding rectangle.</param>
        /// <param name="width">Width of the bounding rectangle (diameter for a circle).</param>
        /// <param name="height">Height of the bounding rectangle.</param>
        /// <param name="color">Color or pattern.</param>
        public static void DrawEllipse(int x, int y, int width, int height, int lineWidth, float startAngle, float endAngle, LCDColor color)
            => Api->drawEllipse(x, y, width, height, lineWidth, startAngle, endAngle, color.Value);

        /// <summary>Fills an ellipse inside the rectangle, or a pie wedge when the angles differ.</summary>
        /// <param name="startAngle">Start angle in degrees, clockwise from 12 o'clock.</param>
        /// <param name="endAngle">End angle. Equal angles fill the whole ellipse.</param>
        /// <param name="x">Left of the bounding rectangle.</param>
        /// <param name="y">Top of the bounding rectangle.</param>
        /// <param name="width">Width of the bounding rectangle.</param>
        /// <param name="height">Height of the bounding rectangle.</param>
        /// <param name="color">Color or pattern.</param>
        public static void FillEllipse(int x, int y, int width, int height, float startAngle, float endAngle, LCDColor color)
            => Api->fillEllipse(x, y, width, height, startAngle, endAngle, color.Value);

        /// <summary>
        /// Sets one pixel. Slow for bulk work (about 20,000 calls drop a frame below 30 fps); use
        /// <see cref="GetFrame"/> to write many pixels.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        /// <param name="color">Color (a pattern uses its pixel at this position).</param>
        public static void SetPixel(int x, int y, LCDColor color) => Api->setPixel(x, y, color.Value);

        /// <summary>
        /// Fills a polygon. <paramref name="coords"/> holds alternating x and y values; the last point is
        /// joined to the first.
        /// </summary>
        /// <param name="coords">x0, y0, x1, y1, ... at least 3 points (6 values), an even count.</param>
        /// <param name="color">Color or pattern.</param>
        /// <param name="fillRule">How overlapping areas are filled.</param>
        public static void FillPolygon(ReadOnlySpan<int> coords, LCDColor color, PolygonFillRule fillRule = PolygonFillRule.NonZero)
        {
            fixed (int* p = coords)
                Api->fillPolygon(coords.Length / 2, p, color.Value, fillRule);
        }

        // ---- Bitmaps ----

        /// <summary>Draws a bitmap with its top-left corner at (x, y).</summary>
        /// <param name="bitmap">The image.</param>
        /// <param name="x">Screen x of its left edge.</param>
        /// <param name="y">Screen y of its top edge.</param>
        /// <param name="flip">Mirroring.</param>
        public static void DrawBitmap(Bitmap bitmap, int x, int y, BitmapFlip flip = BitmapFlip.Unflipped) => Api->drawBitmap(bitmap.Handle, x, y, flip);
        /// <summary>Repeats the bitmap to fill the rectangle {x, y, width, height}.</summary>
        /// <param name="bitmap">The image to repeat.</param>
        /// <param name="x">Left of the area (and of the first copy).</param>
        /// <param name="y">Top of the area.</param>
        /// <param name="width">Width of the area in pixels.</param>
        /// <param name="height">Height of the area in pixels.</param>
        /// <param name="flip">Mirroring of each copy.</param>
        public static void TileBitmap(Bitmap bitmap, int x, int y, int width, int height, BitmapFlip flip = BitmapFlip.Unflipped) => Api->tileBitmap(bitmap.Handle, x, y, width, height, flip);
        /// <summary>
        /// Draws a bitmap scaled by (xScale, yScale) with its top-left corner at (x, y). Negative scales
        /// mirror it.
        /// </summary>
        /// <param name="bitmap">The image.</param>
        /// <param name="x">Left edge of the result.</param>
        /// <param name="y">Top edge of the result.</param>
        /// <param name="xScale">Horizontal factor (1 = original, 2 = double, negative = mirrored).</param>
        /// <param name="yScale">Vertical factor.</param>
        public static void DrawScaledBitmap(Bitmap bitmap, int x, int y, float xScale, float yScale) => Api->drawScaledBitmap(bitmap.Handle, x, y, xScale, yScale);

        /// <summary>
        /// Draws a bitmap scaled, then rotated by <paramref name="rotation"/> degrees (clockwise), so that
        /// the point (centerX, centerY) of the bitmap lands on (x, y). The center is a fraction of the
        /// bitmap's size: 0.5, 0.5 is its middle, 0, 0 its top-left corner.
        /// </summary>
        /// <param name="bitmap">The image.</param>
        /// <param name="x">Screen x where the center point lands.</param>
        /// <param name="y">Screen y where the center point lands.</param>
        /// <param name="rotation">Clockwise rotation in degrees (any value; 360 = full turn).</param>
        /// <param name="centerX">
        /// Horizontal position of the pivot in the image, 0 (left) to 1 (right).
        /// </param>
        /// <param name="centerY">Vertical position of the pivot, 0 (top) to 1 (bottom).</param>
        /// <param name="xScale">Horizontal scale, applied before rotating (1 = original).</param>
        /// <param name="yScale">Vertical scale.</param>
        public static void DrawRotatedBitmap(Bitmap bitmap, int x, int y, float rotation, float centerX, float centerY, float xScale, float yScale)
            => Api->drawRotatedBitmap(bitmap.Handle, x, y, rotation, centerX, centerY, xScale, yScale);

        /// <summary>
        /// Pixel-perfect collision test: true if any opaque pixel of <paramref name="bitmap1"/> at (x1, y1)
        /// overlaps an opaque pixel of <paramref name="bitmap2"/> at (x2, y2), considering only the area
        /// inside <paramref name="rect"/> (which must not be empty).
        /// </summary>
        /// <param name="bitmap1">First image.</param>
        /// <param name="x1">Left of the first image.</param>
        /// <param name="y1">Top of the first image.</param>
        /// <param name="flip1">Mirroring of the first image.</param>
        /// <param name="bitmap2">Second image.</param>
        /// <param name="x2">Left of the second image.</param>
        /// <param name="y2">Top of the second image.</param>
        /// <param name="flip2">Mirroring of the second image.</param>
        /// <param name="rect">Only pixels inside this rectangle count; must not be empty.</param>
        public static bool CheckMaskCollision(Bitmap bitmap1, int x1, int y1, BitmapFlip flip1, Bitmap bitmap2, int x2, int y2, BitmapFlip flip2, LCDRect rect)
            => Api->checkMaskCollision(bitmap1.Handle, x1, y1, flip1, bitmap2.Handle, x2, y2, flip2, rect) != 0;

        // ---- Text ----

        /// <summary>
        /// Draws text with the current font, top-left corner at (x, y). Newlines start a new line.
        /// </summary>
        /// <returns>The width of the drawn text in pixels.</returns>
        /// <param name="text">The text; \n starts a new line. null draws nothing.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge of the first line.</param>
        public static int DrawText(string text, int x, int y)
        {
            fixed (char* p = text)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y);
        }

        /// <summary>
        /// Draws UTF-8 text, e.g. <c>Graphics.DrawText("Hello"u8, x, y)</c> (no string conversion at all).
        /// </summary>
        /// <returns>The width of the drawn text in pixels.</returns>
        /// <param name="utf8">UTF-8 text.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge of the first line.</param>
        public static int DrawText(ReadOnlySpan<byte> utf8, int x, int y)
        {
            fixed (byte* p = utf8)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(utf8), StringEncoding.UTF8, x, y);
        }

        /// <summary>
        /// Draws a span of characters, e.g. a number formatted into a stack buffer, without allocating a
        /// string.
        /// </summary>
        /// <returns>The width of the drawn text in pixels.</returns>
        /// <param name="text">The characters.</param>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge of the first line.</param>
        public static int DrawText(ReadOnlySpan<char> text, int x, int y)
        {
            fixed (char* p = text)
                return Api->drawText(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y);
        }

        /// <summary>
        /// Draws text wrapped and aligned inside the rectangle {x, y, width, height}; text that doesn't fit
        /// is cut off.
        /// </summary>
        /// <param name="text">The text; \n forces a line break.</param>
        /// <param name="x">Left of the box.</param>
        /// <param name="y">Top of the box.</param>
        /// <param name="width">Box width in pixels; lines wrap at it.</param>
        /// <param name="height">Box height in pixels; lines below it are not drawn.</param>
        /// <param name="wrap">Where lines may break.</param>
        /// <param name="align">Horizontal alignment of each line.</param>
        public static void DrawTextInRect(string text, int x, int y, int width, int height, TextWrappingMode wrap = TextWrappingMode.Word, TextAlignment align = TextAlignment.Left)
        {
            fixed (char* p = text)
                Api->drawTextInRect(p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, x, y, width, height, wrap, align);
        }

        /// <summary>
        /// Returns the width in pixels of a single line of text in <paramref name="font"/>.
        /// </summary>
        /// <param name="tracking">
        /// Extra spacing between characters, as set with <see cref="SetTextTracking"/>.
        /// </param>
        /// <param name="font">The font to measure with.</param>
        /// <param name="text">One line of text.</param>
        public static int GetTextWidth(Font font, string text, int tracking = 0)
        {
            fixed (char* p = text)
                return Api->getTextWidth(font.Handle, p, (nuint)Utf8.CodePointCount(text), StringEncoding.UTF16LE, tracking);
        }

        /// <summary>
        /// Returns the height of <paramref name="text"/> when wrapped to <paramref name="maxWidth"/> pixels,
        /// to size a box before calling <see cref="DrawTextInRect"/>.
        /// </summary>
        /// <param name="font">The font to measure with.</param>
        /// <param name="text">The text.</param>
        /// <param name="maxWidth">Width in pixels the text wraps at.</param>
        /// <param name="wrap">Where lines may break.</param>
        /// <param name="tracking">Extra pixels between characters.</param>
        /// <param name="extraLeading">Extra pixels between lines.</param>
        public static int GetTextHeightForMaxWidth(Font font, string text, int maxWidth, TextWrappingMode wrap = TextWrappingMode.Word, int tracking = 0, int extraLeading = 0)
        {
            fixed (char* p = text)
                return Api->getTextHeightForMaxWidth(font.Handle, p, (nuint)Utf8.CodePointCount(text), maxWidth, StringEncoding.UTF16LE, wrap, tracking, extraLeading);
        }

        // ---- Frame buffer ----

        /// <summary>
        /// The frame buffer being drawn: 240 rows of <see cref="RowSize"/> bytes, 1 bit per pixel, bit 7 of a
        /// byte is the leftmost pixel, 1 = white. After writing to it call <see cref="MarkUpdatedRows"/>.
        /// </summary>
        public static Span<byte> GetFrame() => new Span<byte>(Api->getFrame(), RowSize * Height);

        /// <summary>The frame buffer currently shown on the display.</summary>
        public static Span<byte> GetDisplayFrame() => new Span<byte>(Api->getDisplayFrame(), RowSize * Height);

        /// <summary>The working frame buffer as a raw pointer (see <see cref="GetFrame"/>).</summary>
        public static byte* GetFramePointer() => Api->getFrame();

        /// <summary>
        /// Tells the display which rows changed after writing to <see cref="GetFrame"/> directly; both ends
        /// are included (MarkUpdatedRows(0, 239) refreshes everything).
        /// </summary>
        /// <param name="start">First changed row, 0-239.</param>
        /// <param name="end">Last changed row, start-239 (included).</param>
        public static void MarkUpdatedRows(int start, int end) => Api->markUpdatedRows(start, end);

        /// <summary>Copies the frame buffer to the display immediately (normally done after Update returns).</summary>
        public static void Display() => Api->display();

        /// <summary>
        /// Returns a copy of the working frame buffer as a new bitmap. Dispose it when done.
        /// </summary>
        public static Bitmap CopyFrameBufferBitmap() => new Bitmap(Api->copyFrameBufferBitmap());

        /// <summary>
        /// Returns the last frame sent to the display as a bitmap owned by the system. Do not dispose it.
        /// </summary>
        public static Bitmap GetDisplayBufferBitmap() => new Bitmap(Api->getDisplayBufferBitmap());

        /// <summary>
        /// Simulator only: a bitmap overlaid on the screen; white pixels drawn into it show as translucent
        /// red. Returns a null bitmap on the device.
        /// </summary>
        public static Bitmap GetDebugBitmap() => Api->getDebugBitmap == null ? default : new Bitmap(Api->getDebugBitmap());
    }

    /// <summary>
    /// An image (C: LCDBitmap, Lua: playdate.graphics.image). A small struct around a native pointer: copying
    /// it copies the handle, not the image.
    /// </summary>
    /// <remarks>
    /// Bitmaps you <see cref="Create"/>, <see cref="Load"/>, <see cref="Copy"/> or get from
    /// <see cref="Rotated"/> are yours: <see cref="Dispose"/> them when done. Bitmaps returned by an image
    /// table, a sprite or the system are owned elsewhere and must not be disposed. Images are 1-bit with an
    /// optional mask (transparency).
    /// </remarks>
    public readonly unsafe struct Bitmap : IEquatable<Bitmap>, IDisposable
    {
        /// <summary>The native pointer, for the raw API (<c>PD.Graphics</c>).</summary>
        public readonly LCDBitmap* Handle;

        /// <summary>Wraps a native bitmap pointer.</summary>
        /// <param name="handle">
        /// A native bitmap pointer; this struct doesn't take ownership (dispose only if you own it).
        /// </param>
        public Bitmap(LCDBitmap* handle) { Handle = handle; }

        /// <summary>True if this handle doesn't refer to a bitmap (e.g. a failed load).</summary>
        public bool IsNull => Handle == null;

        /// <summary>
        /// Creates a new <paramref name="width"/> x <paramref name="height"/> bitmap filled with
        /// <paramref name="background"/> (use <see cref="SolidColor.Clear"/> for a transparent one).
        /// </summary>
        /// <param name="width">Width in pixels, 1 or more.</param>
        /// <param name="height">Height in pixels, 1 or more.</param>
        /// <param name="background">
        /// Initial fill; <see cref="SolidColor.Clear"/> gives a fully transparent image with a mask.
        /// </param>
        public static Bitmap Create(int width, int height, LCDColor background) => new Bitmap(PD.Graphics->newBitmap(width, height, background.Value));

        /// <summary>
        /// Loads an image compiled into the pdx, e.g. "images/player" for Source/images/player.png (no
        /// extension). If the file can't be loaded the game stops with an error; use <see cref="TryLoad"/> to
        /// handle that yourself.
        /// </summary>
        /// <param name="path">Path in the pdx without extension, e.g. "images/player".</param>
        public static Bitmap Load(string path)
        {
            if (!TryLoad(path, out Bitmap bitmap, out string error))
                Sys.Error(string.Concat("Couldn't load bitmap ", path, ": ", error));
            return bitmap;
        }

        /// <summary>
        /// Loads an image; returns false and an error message instead of stopping the game.
        /// </summary>
        /// <param name="path">Path in the pdx without extension.</param>
        /// <param name="bitmap">Receives the image, or a null bitmap on failure.</param>
        /// <param name="error">Receives the reason on failure, otherwise null.</param>
        public static bool TryLoad(string path, out Bitmap bitmap, out string error)
        {
            byte* err = null;
            bitmap = new Bitmap(PD.Graphics->loadBitmap(Utf8.Arg(path), &err));
            error = bitmap.IsNull ? Utf8.ToString(err) : null;
            return !bitmap.IsNull;
        }

        /// <summary>Loads an image file into this existing bitmap (same size).</summary>
        /// <param name="path">
        /// Path in the pdx without extension; the image must have this bitmap's size.
        /// </param>
        public void LoadInto(string path)
        {
            byte* err = null;
            PD.Graphics->loadIntoBitmap(Utf8.Arg(path), Handle, &err);
            if (err != null)
                Sys.Error(string.Concat("Couldn't load bitmap ", path, ": ", Utf8.ToString(err)));
        }

        /// <summary>Returns a new bitmap with the same pixels and mask.</summary>
        public Bitmap Copy() => new Bitmap(PD.Graphics->copyBitmap(Handle));

        /// <summary>Fills the bitmap with <paramref name="background"/>.</summary>
        /// <param name="background">Fill color or pattern.</param>
        public void Clear(LCDColor background) => PD.Graphics->clearBitmap(Handle, background.Value);

        /// <summary>
        /// Returns a new bitmap containing this one rotated by <paramref name="rotation"/> degrees and
        /// scaled. Dispose it when done. Faster to draw repeatedly than <see cref="DrawRotated"/>.
        /// </summary>
        /// <param name="rotation">Clockwise rotation in degrees.</param>
        /// <param name="xScale">Horizontal scale (1 = original).</param>
        /// <param name="yScale">Vertical scale (1 = original).</param>
        public Bitmap Rotated(float rotation, float xScale = 1, float yScale = 1)
        {
            int size;
            return new Bitmap(PD.Graphics->rotatedBitmap(Handle, rotation, xScale, yScale, &size));
        }

        /// <summary>
        /// Gives raw access to the pixels: size, bytes per row, mask (null if none) and pixel data (1 bit per
        /// pixel, bit 7 = leftmost, 1 = white).
        /// </summary>
        /// <param name="width">Receives the width in pixels.</param>
        /// <param name="height">Receives the height in pixels.</param>
        /// <param name="rowBytes">Receives the bytes per row (rows are padded).</param>
        /// <param name="mask">Receives the mask bits (1 = opaque), or null without a mask.</param>
        /// <param name="data">Receives the pixel bits (1 = white); valid while the bitmap exists.</param>
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

        /// <summary>Width in pixels.</summary>
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

        /// <summary>Height in pixels.</summary>
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

        /// <summary>
        /// Returns <see cref="SolidColor.Black"/> or <see cref="SolidColor.White"/>, or
        /// <see cref="SolidColor.Clear"/> outside the bitmap or where the mask is transparent.
        /// </summary>
        /// <param name="x">Column, 0 to Width - 1.</param>
        /// <param name="y">Row, 0 to Height - 1.</param>
        public SolidColor GetPixel(int x, int y) => PD.Graphics->getBitmapPixel(Handle, x, y);

        /// <summary>
        /// The bitmap's mask (white = opaque). Getting it returns a new bitmap that shares this bitmap's mask
        /// memory: draw into it to change the mask, and dispose it when done. A null bitmap means no mask.
        /// Setting it copies a same-size bitmap into the mask.
        /// </summary>
        public Bitmap Mask
        {
            get => new Bitmap(PD.Graphics->getBitmapMask(Handle));
            set => PD.Graphics->setBitmapMask(Handle, value.Handle);
        }

        /// <summary>Draws the bitmap with its top-left corner at (x, y).</summary>
        /// <param name="x">Screen x of the left edge.</param>
        /// <param name="y">Screen y of the top edge.</param>
        /// <param name="flip">Mirroring.</param>
        public void Draw(int x, int y, BitmapFlip flip = BitmapFlip.Unflipped) => PD.Graphics->drawBitmap(Handle, x, y, flip);

        /// <summary>Draws the bitmap scaled; negative scales mirror it.</summary>
        /// <param name="x">Left edge of the result.</param>
        /// <param name="y">Top edge of the result.</param>
        /// <param name="xScale">Horizontal factor (negative = mirrored).</param>
        /// <param name="yScale">Vertical factor.</param>
        public void DrawScaled(int x, int y, float xScale, float yScale) => PD.Graphics->drawScaledBitmap(Handle, x, y, xScale, yScale);

        /// <summary>
        /// Draws the bitmap rotated by <paramref name="rotation"/> degrees so that its point (centerX,
        /// centerY) (fractions of its size; 0.5, 0.5 = middle) lands on (x, y).
        /// </summary>
        /// <param name="x">Screen x where the pivot lands.</param>
        /// <param name="y">Screen y where the pivot lands.</param>
        /// <param name="rotation">Clockwise rotation in degrees.</param>
        /// <param name="centerX">Pivot position in the image, 0 (left) to 1 (right); default 0.5.</param>
        /// <param name="centerY">Pivot position, 0 (top) to 1 (bottom); default 0.5.</param>
        /// <param name="xScale">Horizontal scale (1 = original).</param>
        /// <param name="yScale">Vertical scale (1 = original).</param>
        public void DrawRotated(int x, int y, float rotation, float centerX = 0.5f, float centerY = 0.5f, float xScale = 1, float yScale = 1)
            => PD.Graphics->drawRotatedBitmap(Handle, x, y, rotation, centerX, centerY, xScale, yScale);

        /// <summary>Frees the bitmap. Only for bitmaps you created, loaded or copied.</summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->freeBitmap(Handle);
        }

        /// <summary>True if both handles refer to the same native bitmap.</summary>
        /// <param name="other">The bitmap to compare with.</param>
        public bool Equals(Bitmap other) => Handle == other.Handle;
        /// <summary>True if <paramref name="obj"/> is a <see cref="Bitmap"/> with the same handle.</summary>
        /// <param name="obj">The object to compare with.</param>
        public override bool Equals(object obj) => obj is Bitmap b && b.Handle == Handle;
        /// <summary>Hash of the handle.</summary>
        public override int GetHashCode() => (int)(nint)Handle;
        /// <summary>True if both handles refer to the same native bitmap.</summary>
        public static bool operator ==(Bitmap a, Bitmap b) => a.Handle == b.Handle;
        /// <summary>True if the handles differ.</summary>
        public static bool operator !=(Bitmap a, Bitmap b) => a.Handle != b.Handle;
    }

    /// <summary>
    /// A list of same-size images, e.g. animation frames or tiles (C: LCDBitmapTable, Lua:
    /// playdate.graphics.imagetable). Dispose tables you create or load.
    /// </summary>
    public readonly unsafe struct BitmapTable : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly LCDBitmapTable* Handle;

        /// <summary>Wraps a native table pointer.</summary>
        /// <param name="handle">A native table pointer; this struct doesn't take ownership.</param>
        public BitmapTable(LCDBitmapTable* handle) { Handle = handle; }

        /// <summary>True if this handle doesn't refer to a table.</summary>
        public bool IsNull => Handle == null;

        /// <summary>
        /// Creates an empty table for <paramref name="count"/> images of <paramref name="width"/> x
        /// <paramref name="height"/>.
        /// </summary>
        /// <param name="count">Number of images, 1 or more.</param>
        /// <param name="width">Width of each image in pixels.</param>
        /// <param name="height">Height of each image in pixels.</param>
        public static BitmapTable Create(int count, int width, int height) => new BitmapTable(PD.Graphics->newBitmapTable(count, width, height));

        /// <summary>
        /// Loads an image table from the pdx: "images/explosion" finds explosion-table-32-32.png (a grid of
        /// 32x32 cells) or a folder of numbered images explosion-table-1.png, explosion-table-2.png ... Stops
        /// the game with an error if it can't be loaded.
        /// </summary>
        /// <param name="path">
        /// Path in the pdx without the "-table-W-H" suffix and extension, e.g. "images/explosion".
        /// </param>
        public static BitmapTable Load(string path)
        {
            byte* err = null;
            var table = new BitmapTable(PD.Graphics->loadBitmapTable(Utf8.Arg(path), &err));
            if (table.IsNull)
                Sys.Error(string.Concat("Couldn't load bitmap table ", path, ": ", Utf8.ToString(err)));
            return table;
        }

        /// <summary>Loads an image table file into this existing table.</summary>
        /// <param name="path">Path of an image table with the same image count and size.</param>
        public void LoadInto(string path)
        {
            byte* err = null;
            PD.Graphics->loadIntoBitmapTable(Utf8.Arg(path), Handle, &err);
            if (err != null)
                Sys.Error(string.Concat("Couldn't load bitmap table ", path, ": ", Utf8.ToString(err)));
        }

        /// <summary>
        /// The image at 0-based <paramref name="index"/> (a null bitmap if out of range). Owned by the table:
        /// don't dispose it, and don't use it after the table is disposed.
        /// </summary>
        public Bitmap this[int index] => new Bitmap(PD.Graphics->getTableBitmap(Handle, index));

        /// <summary>Number of images.</summary>
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

        /// <summary>Frees the table and all its images.</summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->freeBitmapTable(Handle);
        }
    }

    /// <summary>
    /// A font (C: LCDFont, Lua: playdate.graphics.font). Fonts are .fnt/.png files compiled to .pft by pdc;
    /// the system fonts live in /System/Fonts.
    /// </summary>
    public readonly unsafe struct Font : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly LCDFont* Handle;

        /// <summary>Wraps a native font pointer.</summary>
        /// <param name="handle">A native font pointer; this struct doesn't take ownership.</param>
        public Font(LCDFont* handle) { Handle = handle; }

        /// <summary>True if this handle doesn't refer to a font.</summary>
        public bool IsNull => Handle == null;

        /// <summary>
        /// Loads a font, e.g. "/System/Fonts/Asheville-Sans-14-Bold.pft" or "fonts/myfont" from the pdx.
        /// Stops the game with an error if the font can't be loaded.
        /// </summary>
        /// <param name="path">
        /// A .pft path, e.g. "/System/Fonts/Asheville-Sans-14-Bold.pft", or a font in the pdx without
        /// extension.
        /// </param>
        public static Font Load(string path)
        {
            byte* err = null;
            var font = new Font(PD.Graphics->loadFont(Utf8.Arg(path), &err));
            if (font.IsNull)
                Sys.Error(string.Concat("Couldn't load font ", path, ": ", Utf8.ToString(err)));
            return font;
        }

        /// <summary>
        /// Wraps the contents of an uncompressed .pft file (without its 16-byte header) already in memory.
        /// </summary>
        /// <param name="wide">The header flag saying whether the font has glyphs above U+1FFFF.</param>
        /// <param name="data">The font data; must stay valid while the font is used.</param>
        /// <param name="dataLength">Size of <paramref name="data"/> in bytes.</param>
        public static Font FromData(LCDFontData* data, bool wide, int dataLength) => new Font(PD.Graphics->makeFontFromData(data, wide ? 1 : 0, dataLength));

        /// <summary>Line height in pixels.</summary>
        public int Height => PD.Graphics->getFontHeight(Handle);

        /// <summary>Width in pixels of a single line of <paramref name="text"/>.</summary>
        /// <param name="text">One line of text.</param>
        /// <param name="tracking">Extra pixels between characters.</param>
        public int GetTextWidth(string text, int tracking = 0) => Graphics.GetTextWidth(this, text, tracking);

        /// <summary>
        /// Returns the page holding the glyphs for 256 characters around <paramref name="character"/>
        /// (low-level glyph access).
        /// </summary>
        /// <param name="character">A Unicode code point.</param>
        public LCDFontPage* GetPage(uint character) => PD.Graphics->getFontPage(Handle, character);

        /// <summary>
        /// Returns the glyph for a character, its bitmap (owned by the font) and its advance width in pixels.
        /// </summary>
        /// <param name="character">A Unicode code point.</param>
        /// <param name="bitmap">Receives the glyph image (owned by the font).</param>
        /// <param name="advance">Receives how far to move right after the glyph, in pixels.</param>
        public LCDFontGlyph* GetGlyph(uint character, out Bitmap bitmap, out int advance)
        {
            LCDBitmap* bmp;
            int adv;
            LCDFontGlyph* glyph = PD.Graphics->getFontGlyph(Handle, character, &bmp, &adv);
            bitmap = new Bitmap(bmp);
            advance = adv;
            return glyph;
        }

        /// <summary>
        /// Kerning adjustment in pixels between <paramref name="glyphCode"/> and the following
        /// <paramref name="nextCode"/>.
        /// </summary>
        /// <param name="glyph">The glyph of <paramref name="glyphCode"/>.</param>
        /// <param name="glyphCode">The current character's code point.</param>
        /// <param name="nextCode">The following character's code point.</param>
        public static int GetGlyphKerning(LCDFontGlyph* glyph, uint glyphCode, uint nextCode) => PD.Graphics->getGlyphKerning(glyph, glyphCode, nextCode);

        /// <summary>
        /// Frees a font loaded with <see cref="Load"/>. Don't dispose fonts that are still set with
        /// <see cref="Graphics.SetFont"/>.
        /// </summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.System->realloc(Handle, 0);
        }
    }
}
