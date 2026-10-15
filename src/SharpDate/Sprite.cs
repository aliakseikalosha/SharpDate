// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// A sprite: an image (or custom drawing) with a position, a Z order and an optional collision rectangle,
    /// managed by the sprite system (C: LCDSprite, Lua: playdate.graphics.sprite).
    /// </summary>
    /// <remarks>
    /// Subclass it and override <see cref="Update"/>, <see cref="Draw"/> (with <see cref="HasDrawFunction"/>)
    /// or <see cref="CollisionResponse"/> (with <see cref="HasCollisionResponse"/>). Call <see cref="Add"/>
    /// to put it on screen and <see cref="Sprites.UpdateAndDrawSprites"/> once per frame. A sprite stays
    /// alive (it is referenced by the sprite system) until you call <see cref="Dispose"/>, which removes and
    /// frees it; disposing inside its own <see cref="Update"/> is fine. Don't use a sprite after disposing
    /// it. Positions refer to the sprite's center unless <see cref="SetCenter"/> changes it.
    /// </remarks>
    public unsafe class Sprite : IDisposable
    {
        private LCDSprite* _handle;
        private GCHandle _self;

        private static playdate_sprite* Api => PD.Sprite;

        /// <summary>
        /// Creates a native sprite and attaches it to this object. It isn't visible until <see cref="Add"/>.
        /// </summary>
        public Sprite()
        {
            Attach(Api->newSprite());
        }

        /// <summary>Wraps an existing native sprite (e.g. from <see cref="CopyNative"/>), taking ownership.</summary>
        protected Sprite(LCDSprite* handle)
        {
            Attach(handle);
        }

        private void Attach(LCDSprite* handle)
        {
            _handle = handle;
            _self = GCHandle.Alloc(this);
            Api->setUserdata(handle, (void*)GCHandle.ToIntPtr(_self));
            Api->setUpdateFunction(handle, &UpdateTrampoline);
            if (HasDrawFunction)
                Api->setDrawFunction(handle, &DrawTrampoline);
            if (HasCollisionResponse)
                Api->setCollisionResponseFunction(handle, &CollisionTrampoline);
        }

        /// <summary>The native sprite (null after <see cref="Dispose"/>), for the raw API.</summary>
        public LCDSprite* Handle => _handle;

        /// <summary>Returns the managed sprite for a native handle, or null if it was not created by SharpDate.</summary>
        /// <param name="handle">A native sprite (null returns null).</param>
        public static Sprite FromHandle(LCDSprite* handle)
        {
            if (handle == null)
                return null;
            void* ud = Api->getUserdata(handle);
            return ud == null ? null : GCHandle.FromIntPtr((IntPtr)ud).Target as Sprite;
        }

        // ---- Overridable callbacks ----

        /// <summary>Called by <see cref="Sprites.UpdateAndDrawSprites"/> once per frame.</summary>
        protected virtual void Update() { }

        /// <summary>Override and return true to draw the sprite with <see cref="Draw"/> instead of its image.</summary>
        protected virtual bool HasDrawFunction => false;

        /// <summary>Custom drawing (only when <see cref="HasDrawFunction"/> is true).</summary>
        protected virtual void Draw(PDRect bounds, PDRect drawRect) { }

        /// <summary>Override and return true to choose collision responses with <see cref="CollisionResponse"/>.</summary>
        protected virtual bool HasCollisionResponse => false;

        /// <summary>How this sprite should react when moving into <paramref name="other"/> (default: freeze).</summary>
        protected virtual CollisionResponseType CollisionResponse(Sprite other) => CollisionResponseType.Freeze;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void UpdateTrampoline(LCDSprite* sprite) => FromHandle(sprite)?.Update();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DrawTrampoline(LCDSprite* sprite, PDRect bounds, PDRect drawRect) => FromHandle(sprite)?.Draw(bounds, drawRect);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static CollisionResponseType CollisionTrampoline(LCDSprite* sprite, LCDSprite* other)
        {
            Sprite self = FromHandle(sprite);
            return self != null ? self.CollisionResponse(FromHandle(other)) : CollisionResponseType.Freeze;
        }

        // ---- Display list ----

        /// <summary>Adds the sprite to the display list.</summary>
        public void Add() => Api->addSprite(_handle);

        /// <summary>
        /// Removes the sprite from the display list without freeing it; <see cref="Add"/> brings it back.
        /// </summary>
        public void Remove() => Api->removeSprite(_handle);

        /// <summary>
        /// Copies the native sprite. Wrap the result with the protected <see cref="Sprite(LCDSprite*)"/>
        /// constructor of a subclass, which takes ownership of it.
        /// </summary>
        protected LCDSprite* CopyNative() => Api->copy(_handle);

        // ---- Geometry ----

        /// <summary>
        /// The sprite's bounding rectangle in world coordinates (position and size). Setting it moves and
        /// resizes the sprite.
        /// </summary>
        public PDRect Bounds
        {
            get => Api->getBounds(_handle);
            set => Api->setBounds(_handle, value);
        }

        /// <summary>
        /// Moves the sprite so that its center point (see <see cref="SetCenter"/>) is at (x, y). Doesn't
        /// check collisions.
        /// </summary>
        /// <param name="x">World x for the center point.</param>
        /// <param name="y">World y for the center point.</param>
        public void MoveTo(float x, float y) => Api->moveTo(_handle, x, y);
        /// <summary>Moves the sprite by (dx, dy). Doesn't check collisions.</summary>
        /// <param name="dx">Horizontal distance (positive = right).</param>
        /// <param name="dy">Vertical distance (positive = down).</param>
        public void MoveBy(float dx, float dy) => Api->moveBy(_handle, dx, dy);

        /// <summary>The position of the sprite's center point.</summary>
        /// <param name="x">Receives the center point's x.</param>
        /// <param name="y">Receives the center point's y.</param>
        public void GetPosition(out float x, out float y)
        {
            float px, py;
            Api->getPosition(_handle, &px, &py);
            x = px;
            y = py;
        }

        /// <summary>X of the sprite's center point.</summary>
        public float X
        {
            get
            {
                GetPosition(out float x, out _);
                return x;
            }
        }

        /// <summary>Y of the sprite's center point.</summary>
        public float Y
        {
            get
            {
                GetPosition(out _, out float y);
                return y;
            }
        }

        /// <summary>
        /// Sets the size used for <see cref="Bounds"/> when the sprite has no image (e.g. custom-drawn
        /// sprites).
        /// </summary>
        /// <param name="width">Width in pixels, 0 or more.</param>
        /// <param name="height">Height in pixels, 0 or more.</param>
        public void SetSize(float width, float height) => Api->setSize(_handle, width, height);

        /// <summary>
        /// Sets which point of the sprite <see cref="MoveTo"/> positions, as fractions of its size: 0.5, 0.5
        /// (default) is the middle, 0, 0 the top-left corner.
        /// </summary>
        /// <param name="x">0 (left edge) to 1 (right edge); 0.5 = middle.</param>
        /// <param name="y">0 (top edge) to 1 (bottom edge); 0.5 = middle.</param>
        public void SetCenter(float x, float y) => Api->setCenter(_handle, x, y);

        /// <summary>Gets the center point set with <see cref="SetCenter"/>.</summary>
        /// <param name="x">Receives the horizontal center, 0-1.</param>
        /// <param name="y">Receives the vertical center, 0-1.</param>
        public void GetCenter(out float x, out float y)
        {
            float cx, cy;
            Api->getCenter(_handle, &cx, &cy);
            x = cx;
            y = cy;
        }

        // ---- Appearance ----

        private Bitmap _image;

        /// <summary>
        /// The sprite's image; the sprite's size follows the image. The sprite does not own the bitmap: keep
        /// it alive while the sprite uses it and dispose it yourself afterwards (several sprites can share
        /// one).
        /// </summary>
        public Bitmap Image
        {
            get => new Bitmap(Api->getImage(_handle));
            set => SetImage(value, BitmapFlip.Unflipped);
        }

        /// <summary>Sets the image and how it is flipped.</summary>
        /// <param name="image">The image (not owned by the sprite); a null bitmap removes it.</param>
        /// <param name="flip">Mirroring.</param>
        public void SetImage(Bitmap image, BitmapFlip flip)
        {
            _image = image;
            Api->setImage(_handle, image.Handle, flip);
        }

        /// <summary>
        /// Drawing order, -32768 to 32767: sprites with a higher Z index are drawn on top. Default 0.
        /// </summary>
        public short ZIndex
        {
            get => Api->getZIndex(_handle);
            set => Api->setZIndex(_handle, value);
        }

        /// <summary>How the image is drawn (e.g. <see cref="Playdate.DrawMode.WhiteTransparent"/>).</summary>
        public DrawMode DrawMode
        {
            set => Api->setDrawMode(_handle, value);
        }

        /// <summary>How the image is mirrored.</summary>
        public BitmapFlip ImageFlip
        {
            get => Api->getImageFlip(_handle);
            set => Api->setImageFlip(_handle, value);
        }

        /// <summary>Whether the sprite is drawn. Invisible sprites still update and collide.</summary>
        public bool Visible
        {
            get => Api->isVisible(_handle) != 0;
            set => Api->setVisible(_handle, value ? 1 : 0);
        }

        /// <summary>
        /// Declares that the sprite covers everything below it, so the sprite system can skip redrawing
        /// what's underneath. Set automatically for images without a mask.
        /// </summary>
        public bool Opaque
        {
            set => Api->setOpaque(_handle, value ? 1 : 0);
        }

        /// <summary>Whether <see cref="Update"/> is called. Disabled sprites are still drawn.</summary>
        public bool UpdatesEnabled
        {
            get => Api->updatesEnabled(_handle) != 0;
            set => Api->setUpdatesEnabled(_handle, value ? 1 : 0);
        }

        /// <summary>
        /// Draws the sprite in screen coordinates, ignoring <see cref="Graphics.SetDrawOffset"/> (for HUD
        /// elements). Collisions still use world coordinates.
        /// </summary>
        public bool IgnoresDrawOffset
        {
            set => Api->setIgnoresDrawOffset(_handle, value ? 1 : 0);
        }

        /// <summary>A free number (0-255) to identify kinds of sprites, e.g. in collision handling.</summary>
        public byte Tag
        {
            get => Api->getTag(_handle);
            set => Api->setTag(_handle, value);
        }

        /// <summary>
        /// Only draws the part of the sprite inside <paramref name="clipRect"/> (screen coordinates).
        /// </summary>
        /// <param name="clipRect">Area in screen coordinates.</param>
        public void SetClipRect(LCDRect clipRect) => Api->setClipRect(_handle, clipRect);
        /// <summary>Removes the sprite's clip rect.</summary>
        public void ClearClipRect() => Api->clearClipRect(_handle);
        /// <summary>
        /// Forces the sprite to be redrawn next frame (needed after changing what a custom <see cref="Draw"/>
        /// draws).
        /// </summary>
        public void MarkDirty() => Api->markDirty(_handle);
        /// <summary>Marks part of the sprite (relative to its top-left corner) for redrawing.</summary>
        /// <param name="rect">Area relative to the sprite's top-left corner.</param>
        public void MarkDirtyRect(PDRect rect) => Api->markDirtyRect(_handle, rect);

        /// <summary>
        /// Only draws the sprite where <paramref name="stencil"/> is white. The stencil is in screen
        /// coordinates and doesn't move with the sprite; tiled stencils must be a multiple of 32 pixels wide.
        /// </summary>
        /// <param name="stencil">White = draw, black = hide; in screen coordinates.</param>
        /// <param name="tile">Repeat the stencil (it must be a multiple of 32 pixels wide).</param>
        public void SetStencilImage(Bitmap stencil, bool tile) => Api->setStencilImage(_handle, stencil.Handle, tile ? 1 : 0);

        /// <summary>
        /// Sets an 8x8 repeating stencil: 8 bytes, one per row, bit 7 = leftmost pixel, 1 = draw.
        /// </summary>
        /// <param name="pattern">
        /// Up to 8 row bytes (missing rows are fully drawn); bit 7 = leftmost pixel, 1 = draw.
        /// </param>
        public void SetStencilPattern(ReadOnlySpan<byte> pattern)
        {
            byte* p = stackalloc byte[8];
            for (int i = 0; i < 8; i++)
                p[i] = i < pattern.Length ? pattern[i] : (byte)0xFF;
            Api->setStencilPattern(_handle, p);
        }

        /// <summary>Removes the sprite's stencil.</summary>
        public void ClearStencil() => Api->clearStencil(_handle);

        /// <summary>Uses a tile map as the sprite's image (not owned by the sprite).</summary>
        public TileMap Tilemap
        {
            get => new TileMap(Api->getTilemap(_handle));
            set => Api->setTilemap(_handle, value.Handle);
        }

        // ---- Collisions ----

        /// <summary>
        /// Whether the sprite takes part in collisions (default true; it also needs a
        /// <see cref="CollideRect"/>).
        /// </summary>
        public bool CollisionsEnabled
        {
            get => Api->collisionsEnabled(_handle) != 0;
            set => Api->setCollisionsEnabled(_handle, value ? 1 : 0);
        }

        /// <summary>
        /// The area that collides, relative to the sprite's top-left corner, e.g. <c>new PDRect(0, 0,
        /// image.Width, image.Height)</c>. A sprite without a collide rect never collides.
        /// </summary>
        public PDRect CollideRect
        {
            get => Api->getCollideRect(_handle);
            set => Api->setCollideRect(_handle, value);
        }

        /// <summary>Removes the collide rect (the sprite no longer collides).</summary>
        public void ClearCollideRect() => Api->clearCollideRect(_handle);

        /// <summary>
        /// Moves toward (goalX, goalY), stopping, sliding or bouncing at other sprites' collide rects
        /// according to <see cref="CollisionResponse"/>, and reports what it hit. Dispose the returned list
        /// (a <c>using</c> does it).
        /// </summary>
        /// <param name="actualX">Where the sprite ended up.</param>
        /// <returns>The collisions, in the order they happened.</returns>
        /// <param name="goalX">Where the center point should go (x).</param>
        /// <param name="goalY">Where the center point should go (y).</param>
        /// <param name="actualY">Where the sprite ended up (y).</param>
        public CollisionList MoveWithCollisions(float goalX, float goalY, out float actualX, out float actualY)
        {
            float ax, ay;
            int len;
            SpriteCollisionInfo* info = Api->moveWithCollisions(_handle, goalX, goalY, &ax, &ay, &len);
            actualX = ax;
            actualY = ay;
            return new CollisionList(info, len);
        }

        /// <summary>
        /// Computes the result of <see cref="MoveWithCollisions"/> without moving the sprite. Dispose the
        /// returned list.
        /// </summary>
        /// <param name="goalX">Target x of the center point.</param>
        /// <param name="goalY">Target y of the center point.</param>
        /// <param name="actualX">Where the sprite would end up (x).</param>
        /// <param name="actualY">Where the sprite would end up (y).</param>
        public CollisionList CheckCollisions(float goalX, float goalY, out float actualX, out float actualY)
        {
            float ax, ay;
            int len;
            SpriteCollisionInfo* info = Api->checkCollisions(_handle, goalX, goalY, &ax, &ay, &len);
            actualX = ax;
            actualY = ay;
            return new CollisionList(info, len);
        }

        /// <summary>
        /// The sprites whose collide rects currently overlap this one's. Dispose the returned list.
        /// </summary>
        public SpriteList OverlappingSprites()
        {
            int len;
            LCDSprite** list = Api->overlappingSprites(_handle, &len);
            return new SpriteList(list, len);
        }

        // ---- Lifetime ----

        /// <summary>
        /// Removes the sprite from the display list and frees it. Safe to call more than once and from the
        /// sprite's own <see cref="Update"/>. Doesn't free the sprite's image.
        /// </summary>
        public void Dispose()
        {
            if (_handle == null)
                return;
            Api->removeSprite(_handle);
            Api->setUserdata(_handle, null);
            Api->freeSprite(_handle);
            _handle = null;
            if (_self.IsAllocated)
                _self.Free();
        }
    }

    /// <summary>
    /// The sprite system as a whole: drawing all sprites, queries and global settings (C: <c>pd->sprite</c>,
    /// Lua: <c>playdate.graphics.sprite</c> static functions).
    /// </summary>
    public static unsafe class Sprites
    {
        private static playdate_sprite* Api => PD.Sprite;

        /// <summary>
        /// Calls <see cref="Sprite.Update"/> on every sprite in the display list, then redraws the areas that
        /// changed. Call once per frame from <see cref="Game.Update"/>.
        /// </summary>
        public static void UpdateAndDrawSprites() => Api->updateAndDrawSprites();

        /// <summary>Draws the sprites without updating them.</summary>
        public static void DrawSprites() => Api->drawSprites();

        /// <summary>
        /// Redraws every sprite every frame instead of tracking dirty areas. Faster when many sprites move at
        /// once.
        /// </summary>
        /// <param name="flag">
        /// True = redraw everything every frame, false = only dirty areas (default).
        /// </param>
        public static void SetAlwaysRedraw(bool flag) => Api->setAlwaysRedraw(flag ? 1 : 0);

        /// <summary>Marks a screen area for redrawing (drawing functions do this automatically).</summary>
        /// <param name="rect">Area in screen coordinates.</param>
        public static void AddDirtyRect(LCDRect rect) => Api->addDirtyRect(rect);

        /// <summary>
        /// Removes all sprites from the display list (they aren't freed; dispose them separately).
        /// </summary>
        public static void RemoveAllSprites() => Api->removeAllSprites();

        /// <summary>Number of sprites in the display list.</summary>
        public static int Count => Api->getSpriteCount();

        /// <summary>
        /// Sets a clip rect on every sprite whose Z index is between startZ and endZ (inclusive).
        /// </summary>
        /// <param name="clipRect">Area in screen coordinates.</param>
        /// <param name="startZ">Lowest Z index affected, -32768 to 32767.</param>
        /// <param name="endZ">Highest Z index affected (included).</param>
        public static void SetClipRectsInRange(LCDRect clipRect, int startZ, int endZ) => Api->setClipRectsInRange(clipRect, startZ, endZ);
        /// <summary>
        /// Clears the clip rects of sprites with Z index between startZ and endZ (inclusive).
        /// </summary>
        /// <param name="startZ">Lowest Z index affected.</param>
        /// <param name="endZ">Highest Z index affected (included).</param>
        public static void ClearClipRectsInRange(int startZ, int endZ) => Api->clearClipRectsInRange(startZ, endZ);

        /// <summary>Rebuilds the collision data from scratch (resetting it to its default state).</summary>
        public static void ResetCollisionWorld() => Api->resetCollisionWorld();

        /// <summary>Sprites whose collide rects contain the point. Dispose the returned list.</summary>
        /// <param name="x">World x.</param>
        /// <param name="y">World y.</param>
        public static SpriteList QuerySpritesAtPoint(float x, float y)
        {
            int len;
            return new SpriteList(Api->querySpritesAtPoint(x, y, &len), len);
        }

        /// <summary>Sprites whose collide rects intersect the rectangle. Dispose the returned list.</summary>
        /// <param name="x">Left edge in world coordinates.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public static SpriteList QuerySpritesInRect(float x, float y, float width, float height)
        {
            int len;
            return new SpriteList(Api->querySpritesInRect(x, y, width, height, &len), len);
        }

        /// <summary>
        /// Sprites whose collide rects the line from (x1, y1) to (x2, y2) crosses. Dispose the returned list.
        /// </summary>
        /// <param name="x1">Start x.</param>
        /// <param name="y1">Start y.</param>
        /// <param name="x2">End x.</param>
        /// <param name="y2">End y.</param>
        public static SpriteList QuerySpritesAlongLine(float x1, float y1, float x2, float y2)
        {
            int len;
            return new SpriteList(Api->querySpritesAlongLine(x1, y1, x2, y2, &len), len);
        }

        /// <summary>
        /// Like <see cref="QuerySpritesAlongLine"/>, with the entry and exit points. Slower; dispose the
        /// result.
        /// </summary>
        /// <param name="x1">Start x.</param>
        /// <param name="y1">Start y.</param>
        /// <param name="x2">End x.</param>
        /// <param name="y2">End y.</param>
        public static SpriteQueryList QuerySpriteInfoAlongLine(float x1, float y1, float x2, float y2)
        {
            int len;
            return new SpriteQueryList(Api->querySpriteInfoAlongLine(x1, y1, x2, y2, &len), len);
        }

        /// <summary>
        /// All overlapping pairs: entries 0 and 1 overlap, 2 and 3 overlap, and so on. Dispose the returned
        /// list.
        /// </summary>
        public static SpriteList AllOverlappingSprites()
        {
            int len;
            return new SpriteList(Api->allOverlappingSprites(&len), len);
        }
    }

    /// <summary>
    /// The collisions returned by <see cref="Sprite.MoveWithCollisions"/> and
    /// <see cref="Sprite.CheckCollisions"/>. A native array: dispose it (preferably with <c>using</c>) after
    /// reading it.
    /// </summary>
    public readonly unsafe struct CollisionList : IDisposable
    {
        private readonly SpriteCollisionInfo* _items;
        /// <summary>Number of collisions.</summary>
        public readonly int Count;

        internal CollisionList(SpriteCollisionInfo* items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

        /// <summary>
        /// The collision at <paramref name="index"/> (by reference; don't keep it after disposing the list).
        /// </summary>
        public ref SpriteCollisionInfo this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new IndexOutOfRangeException();
                return ref _items[index];
            }
        }

        /// <summary>The collisions as a span, valid until the list is disposed.</summary>
        public Span<SpriteCollisionInfo> AsSpan() => new Span<SpriteCollisionInfo>(_items, Count);

        /// <summary>Frees the native array.</summary>
        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }

    /// <summary>A list of sprites returned by a query. A native array: dispose it after use.</summary>
    public readonly unsafe struct SpriteList : IDisposable
    {
        private readonly LCDSprite** _items;
        /// <summary>Number of sprites.</summary>
        public readonly int Count;

        internal SpriteList(LCDSprite** items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

        /// <summary>The native sprite at <paramref name="index"/>.</summary>
        /// <param name="index">0 to Count - 1.</param>
        public LCDSprite* GetHandle(int index) => _items[index];

        /// <summary>The managed sprite at <paramref name="index"/> (null for sprites not created through SharpDate).</summary>
        public Sprite this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new IndexOutOfRangeException();
                return Sprite.FromHandle(_items[index]);
            }
        }

        /// <summary>Frees the native array (not the sprites).</summary>
        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }

    /// <summary>
    /// The results of <see cref="Sprites.QuerySpriteInfoAlongLine"/>. A native array: dispose it after use.
    /// </summary>
    public readonly unsafe struct SpriteQueryList : IDisposable
    {
        private readonly SpriteQueryInfo* _items;
        /// <summary>Number of results.</summary>
        public readonly int Count;

        internal SpriteQueryList(SpriteQueryInfo* items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

        /// <summary>The result at <paramref name="index"/>.</summary>
        public ref SpriteQueryInfo this[int index] => ref _items[index];

        /// <summary>Frees the native array.</summary>
        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }
}
