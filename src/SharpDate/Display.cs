// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Display settings: frame rate, scale, inversion, mosaic, flipping and offset (C: <c>pd->display</c>,
    /// Lua: <c>playdate.display</c>).
    /// </summary>
    public static unsafe class Display
    {
        private static playdate_display* Api => PD.Display;

        /// <summary>
        /// Display width in pixels at the current scale: 400 at scale 1, 200 at scale 2, ...
        /// </summary>
        public static int Width => Api->getWidth();
        /// <summary>
        /// Display height in pixels at the current scale: 240 at scale 1, 120 at scale 2, ...
        /// </summary>
        public static int Height => Api->getHeight();

        /// <summary>
        /// The target frame rate (default 30, maximum 50). 0 calls <see cref="Game.Update"/> as often as
        /// possible.
        /// </summary>
        public static float RefreshRate
        {
            get => Api->getRefreshRate();
            set => Api->setRefreshRate(value);
        }

        /// <summary>
        /// The frame rate actually achieved, which can be lower than <see cref="RefreshRate"/> when frames
        /// take too long.
        /// </summary>
        public static float FPS => Api->getFPS();

        /// <summary>Sets the target frame rate; same as setting <see cref="RefreshRate"/>.</summary>
        /// <param name="rate">Frames per second, up to 50; 0 = as fast as possible.</param>
        public static void SetRefreshRate(float rate) => Api->setRefreshRate(rate);
        /// <summary>Shows the display inverted (white on black) when true.</summary>
        /// <param name="inverted">True for white-on-black, false for normal.</param>
        public static void SetInverted(bool inverted) => Api->setInverted(inverted ? 1 : 0);

        /// <summary>
        /// Scales up the top-left part of the frame buffer to fill the screen: 1, 2, 4 or 8. At scale 2 the
        /// game draws into a 200x120 area and each pixel shows as 2x2.
        /// </summary>
        /// <param name="scale">1, 2, 4 or 8. Other values are ignored.</param>
        public static void SetScale(uint scale) => Api->setScale(scale);
        /// <summary>Adds a mosaic effect; x and y are 0 (off) to 3.</summary>
        /// <param name="x">Horizontal mosaic amount, 0 (off) to 3.</param>
        /// <param name="y">Vertical mosaic amount, 0 (off) to 3.</param>
        public static void SetMosaic(uint x, uint y) => Api->setMosaic(x, y);
        /// <summary>Mirrors the display horizontally and/or vertically.</summary>
        /// <param name="x">Mirror left-right.</param>
        /// <param name="y">Mirror top-bottom.</param>
        public static void SetFlipped(bool x, bool y) => Api->setFlipped(x ? 1 : 0, y ? 1 : 0);
        /// <summary>
        /// Moves the whole image by (x, y) pixels (e.g. for a screen shake); uncovered areas show the
        /// background color (<see cref="Graphics.SetBackgroundColor"/>).
        /// </summary>
        /// <param name="x">Horizontal shift in pixels (positive = right).</param>
        /// <param name="y">Vertical shift in pixels (positive = down).</param>
        public static void SetOffset(int x, int y) => Api->setOffset(x, y);
    }

    /// <summary>
    /// A grid of tiles drawn from a <see cref="BitmapTable"/> (C: LCDTileMap, Lua:
    /// playdate.graphics.tilemap). Create with <see cref="Create"/>, dispose when done; a tile map can also
    /// be a sprite's image (<see cref="Sprite.Tilemap"/>).
    /// </summary>
    public readonly unsafe struct TileMap : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly LCDTileMap* Handle;

        /// <summary>Wraps a native tile map pointer.</summary>
        /// <param name="handle">A native tile map pointer; this struct doesn't take ownership.</param>
        public TileMap(LCDTileMap* handle) { Handle = handle; }

        /// <summary>Creates an empty tile map. Set <see cref="ImageTable"/> and the size next.</summary>
        public static TileMap Create() => new TileMap(PD.Graphics->tilemap->newTilemap());

        /// <summary>True if this handle doesn't refer to a tile map.</summary>
        public bool IsNull => Handle == null;

        /// <summary>The image table supplying the tile images (not owned by the tile map).</summary>
        public BitmapTable ImageTable
        {
            get => new BitmapTable(PD.Graphics->tilemap->getImageTable(Handle));
            set => PD.Graphics->tilemap->setImageTable(Handle, value.Handle);
        }

        /// <summary>Sets the size in tiles.</summary>
        /// <param name="tilesWide">Number of columns, 1 or more.</param>
        /// <param name="tilesHigh">Number of rows, 1 or more.</param>
        public void SetSize(int tilesWide, int tilesHigh) => PD.Graphics->tilemap->setSize(Handle, tilesWide, tilesHigh);

        /// <summary>Gets the size in tiles.</summary>
        /// <param name="tilesWide">Receives the number of columns.</param>
        /// <param name="tilesHigh">Receives the number of rows.</param>
        public void GetSize(out int tilesWide, out int tilesHigh)
        {
            int w, h;
            PD.Graphics->tilemap->getSize(Handle, &w, &h);
            tilesWide = w;
            tilesHigh = h;
        }

        /// <summary>Gets the size in pixels (tile size times the number of tiles).</summary>
        /// <param name="width">Receives the width in pixels (columns times tile width).</param>
        /// <param name="height">Receives the height in pixels.</param>
        public void GetPixelSize(out uint width, out uint height)
        {
            uint w, h;
            PD.Graphics->tilemap->getPixelSize(Handle, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>
        /// Sets all tiles at once, row by row; the map becomes <paramref name="rowWidth"/> tiles wide and
        /// indexes.Length / rowWidth tiles high (the length must be a multiple of rowWidth).
        /// </summary>
        /// <param name="indexes">
        /// Image table indexes (0-based), row by row; its length must be a multiple of
        /// <paramref name="rowWidth"/>.
        /// </param>
        /// <param name="rowWidth">Tiles per row, 1 or more.</param>
        public void SetTiles(ReadOnlySpan<ushort> indexes, int rowWidth)
        {
            fixed (ushort* p = indexes)
                PD.Graphics->tilemap->setTiles(Handle, p, indexes.Length, rowWidth);
        }

        /// <summary>
        /// Sets the tile at grid position (x, y) to image <paramref name="index"/> (0-based) of the image
        /// table.
        /// </summary>
        /// <param name="x">Column, 0 to width - 1.</param>
        /// <param name="y">Row, 0 to height - 1.</param>
        /// <param name="index">Image table index, 0 to the table's Count - 1.</param>
        public void SetTile(int x, int y, ushort index) => PD.Graphics->tilemap->setTileAtPosition(Handle, x, y, index);
        /// <summary>Returns the image index of the tile at (x, y), or -1 outside the map.</summary>
        /// <param name="x">Column, 0 to width - 1.</param>
        /// <param name="y">Row, 0 to height - 1.</param>
        public int GetTile(int x, int y) => PD.Graphics->tilemap->getTileAtPosition(Handle, x, y);
        /// <summary>Draws the map with its top-left corner at (x, y).</summary>
        /// <param name="x">Screen x of the map's left edge (can be negative to scroll).</param>
        /// <param name="y">Screen y of the map's top edge.</param>
        public void Draw(float x, float y) => PD.Graphics->tilemap->drawAtPoint(Handle, x, y);

        /// <summary>Frees the tile map (not its image table).</summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->tilemap->freeTilemap(Handle);
        }
    }

    /// <summary>
    /// Renders frames of a .pdv video (C: LCDVideoPlayer, Lua: playdate.graphics.video). Videos have no
    /// sound; play the audio separately with a <see cref="FilePlayer"/>.
    /// </summary>
    public readonly unsafe struct VideoPlayer : IDisposable
    {
        /// <summary>The native pointer.</summary>
        public readonly LCDVideoPlayer* Handle;

        /// <summary>Wraps a native video player pointer.</summary>
        /// <param name="handle">A native video player pointer; this struct doesn't take ownership.</param>
        public VideoPlayer(LCDVideoPlayer* handle) { Handle = handle; }

        /// <summary>
        /// Opens a .pdv file from the pdx. Stops the game with an error if it can't be opened.
        /// </summary>
        /// <param name="path">Path of the .pdv file in the pdx, without extension.</param>
        public static VideoPlayer Load(string path)
        {
            var player = new VideoPlayer(PD.Graphics->video->loadVideo(Utf8.Arg(path)));
            if (player.Handle == null)
                Sys.Error(string.Concat("Couldn't load video ", path));
            return player;
        }

        /// <summary>True if this handle doesn't refer to a player.</summary>
        public bool IsNull => Handle == null;

        /// <summary>
        /// Renders frames into <paramref name="context"/> instead of the screen (use an opaque bitmap).
        /// </summary>
        /// <returns>False on error; see <see cref="Error"/>.</returns>
        /// <param name="context">The bitmap to render into; it should be at least the video's size.</param>
        public bool SetContext(Bitmap context) => PD.Graphics->video->setContext(Handle, context.Handle) != 0;
        /// <summary>Renders frames directly to the screen.</summary>
        public void UseScreenContext() => PD.Graphics->video->useScreenContext(Handle);
        /// <summary>
        /// The bitmap frames are rendered into; if none was set, one with the video's size is created.
        /// </summary>
        public Bitmap Context => new Bitmap(PD.Graphics->video->getContext(Handle));
        /// <summary>Renders frame number <paramref name="frame"/> (0-based) into the context.</summary>
        /// <returns>False on error; see <see cref="Error"/>.</returns>
        /// <param name="frame">Frame number, 0 to frameCount - 1 (see <see cref="GetInfo"/>).</param>
        public bool RenderFrame(int frame) => PD.Graphics->video->renderFrame(Handle, frame) != 0;
        /// <summary>Text describing the last error.</summary>
        public string Error => Utf8.ToString(PD.Graphics->video->getError(Handle));

        /// <summary>
        /// Gets the video's size, frame rate, number of frames and the frame last rendered.
        /// </summary>
        /// <param name="width">Receives the video width in pixels.</param>
        /// <param name="height">Receives the video height in pixels.</param>
        /// <param name="frameRate">Receives the frames per second the video was made for.</param>
        /// <param name="frameCount">Receives the number of frames.</param>
        /// <param name="currentFrame">Receives the last frame rendered.</param>
        public void GetInfo(out int width, out int height, out float frameRate, out int frameCount, out int currentFrame)
        {
            int w, h, fc, cf;
            float fr;
            PD.Graphics->video->getInfo(Handle, &w, &h, &fr, &fc, &cf);
            width = w;
            height = h;
            frameRate = fr;
            frameCount = fc;
            currentFrame = cf;
        }

        /// <summary>Frees the player.</summary>
        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->video->freePlayer(Handle);
        }
    }
}
