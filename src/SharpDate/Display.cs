using System;
using Playdate.Native;

namespace Playdate
{
    /// <summary>Wraps <c>pd->display</c>.</summary>
    public static unsafe class Display
    {
        private static playdate_display* Api => PD.Display;

        /// <summary>Display width, accounting for <see cref="SetScale"/>.</summary>
        public static int Width => Api->getWidth();
        public static int Height => Api->getHeight();

        /// <summary>Target frames per second (default 30; 0 = as fast as possible).</summary>
        public static float RefreshRate
        {
            get => Api->getRefreshRate();
            set => Api->setRefreshRate(value);
        }

        /// <summary>Measured frames per second.</summary>
        public static float FPS => Api->getFPS();

        public static void SetRefreshRate(float rate) => Api->setRefreshRate(rate);
        public static void SetInverted(bool inverted) => Api->setInverted(inverted ? 1 : 0);

        /// <summary>Display scale factor: 1, 2, 4 or 8.</summary>
        public static void SetScale(uint scale) => Api->setScale(scale);
        public static void SetMosaic(uint x, uint y) => Api->setMosaic(x, y);
        public static void SetFlipped(bool x, bool y) => Api->setFlipped(x ? 1 : 0, y ? 1 : 0);
        public static void SetOffset(int x, int y) => Api->setOffset(x, y);
    }

    /// <summary>A grid of tiles drawn from an image table (<c>LCDTileMap</c>, SDK 3.0+).</summary>
    public readonly unsafe struct TileMap : IDisposable
    {
        public readonly LCDTileMap* Handle;

        public TileMap(LCDTileMap* handle) { Handle = handle; }

        public static TileMap Create() => new TileMap(PD.Graphics->tilemap->newTilemap());

        public bool IsNull => Handle == null;

        public BitmapTable ImageTable
        {
            get => new BitmapTable(PD.Graphics->tilemap->getImageTable(Handle));
            set => PD.Graphics->tilemap->setImageTable(Handle, value.Handle);
        }

        public void SetSize(int tilesWide, int tilesHigh) => PD.Graphics->tilemap->setSize(Handle, tilesWide, tilesHigh);

        public void GetSize(out int tilesWide, out int tilesHigh)
        {
            int w, h;
            PD.Graphics->tilemap->getSize(Handle, &w, &h);
            tilesWide = w;
            tilesHigh = h;
        }

        public void GetPixelSize(out uint width, out uint height)
        {
            uint w, h;
            PD.Graphics->tilemap->getPixelSize(Handle, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>Sets tile indexes row by row; <paramref name="rowWidth"/> is the number of tiles per row in <paramref name="indexes"/>.</summary>
        public void SetTiles(ReadOnlySpan<ushort> indexes, int rowWidth)
        {
            fixed (ushort* p = indexes)
                PD.Graphics->tilemap->setTiles(Handle, p, indexes.Length, rowWidth);
        }

        public void SetTile(int x, int y, ushort index) => PD.Graphics->tilemap->setTileAtPosition(Handle, x, y, index);
        public int GetTile(int x, int y) => PD.Graphics->tilemap->getTileAtPosition(Handle, x, y);
        public void Draw(float x, float y) => PD.Graphics->tilemap->drawAtPoint(Handle, x, y);

        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->tilemap->freeTilemap(Handle);
        }
    }

    /// <summary>Plays a .pdv video file (<c>LCDVideoPlayer</c>).</summary>
    public readonly unsafe struct VideoPlayer : IDisposable
    {
        public readonly LCDVideoPlayer* Handle;

        public VideoPlayer(LCDVideoPlayer* handle) { Handle = handle; }

        public static VideoPlayer Load(string path)
        {
            var player = new VideoPlayer(PD.Graphics->video->loadVideo(Utf8.Arg(path)));
            if (player.Handle == null)
                Sys.Error(string.Concat("Couldn't load video ", path));
            return player;
        }

        public bool IsNull => Handle == null;

        /// <summary>Renders into <paramref name="context"/> instead of the screen.</summary>
        public bool SetContext(Bitmap context) => PD.Graphics->video->setContext(Handle, context.Handle) != 0;
        public void UseScreenContext() => PD.Graphics->video->useScreenContext(Handle);
        public Bitmap Context => new Bitmap(PD.Graphics->video->getContext(Handle));
        public bool RenderFrame(int frame) => PD.Graphics->video->renderFrame(Handle, frame) != 0;
        public string Error => Utf8.ToString(PD.Graphics->video->getError(Handle));

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

        public void Dispose()
        {
            if (Handle != null)
                PD.Graphics->video->freePlayer(Handle);
        }
    }
}
