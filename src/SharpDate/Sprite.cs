using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// A Playdate sprite (<c>LCDSprite</c>) owned by a managed object. Subclass it and override
    /// <see cref="Update"/>, <see cref="Draw"/> or <see cref="CollisionResponse"/> to receive the
    /// corresponding callbacks from the sprite system. Call <see cref="Dispose"/> to free it.
    /// </summary>
    public unsafe class Sprite : IDisposable
    {
        private LCDSprite* _handle;
        private GCHandle _self;

        private static playdate_sprite* Api => PD.Sprite;

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

        public LCDSprite* Handle => _handle;

        /// <summary>Returns the managed sprite for a native handle, or null if it was not created by SharpDate.</summary>
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

        /// <summary>Removes the sprite from the display list (it can be added again).</summary>
        public void Remove() => Api->removeSprite(_handle);

        /// <summary>
        /// Copies the native sprite. Wrap the result with the protected <see cref="Sprite(LCDSprite*)"/>
        /// constructor of a subclass, which takes ownership of it.
        /// </summary>
        protected LCDSprite* CopyNative() => Api->copy(_handle);

        // ---- Geometry ----

        public PDRect Bounds
        {
            get => Api->getBounds(_handle);
            set => Api->setBounds(_handle, value);
        }

        public void MoveTo(float x, float y) => Api->moveTo(_handle, x, y);
        public void MoveBy(float dx, float dy) => Api->moveBy(_handle, dx, dy);

        public void GetPosition(out float x, out float y)
        {
            float px, py;
            Api->getPosition(_handle, &px, &py);
            x = px;
            y = py;
        }

        public float X
        {
            get
            {
                GetPosition(out float x, out _);
                return x;
            }
        }

        public float Y
        {
            get
            {
                GetPosition(out _, out float y);
                return y;
            }
        }

        public void SetSize(float width, float height) => Api->setSize(_handle, width, height);

        /// <summary>Sets the sprite's center point relative to its bounds (0..1, default 0.5, 0.5).</summary>
        public void SetCenter(float x, float y) => Api->setCenter(_handle, x, y);

        public void GetCenter(out float x, out float y)
        {
            float cx, cy;
            Api->getCenter(_handle, &cx, &cy);
            x = cx;
            y = cy;
        }

        // ---- Appearance ----

        private Bitmap _image;

        /// <summary>The sprite's image. The sprite does not own it; free it yourself when no longer used.</summary>
        public Bitmap Image
        {
            get => new Bitmap(Api->getImage(_handle));
            set => SetImage(value, BitmapFlip.Unflipped);
        }

        public void SetImage(Bitmap image, BitmapFlip flip)
        {
            _image = image;
            Api->setImage(_handle, image.Handle, flip);
        }

        public short ZIndex
        {
            get => Api->getZIndex(_handle);
            set => Api->setZIndex(_handle, value);
        }

        public DrawMode DrawMode
        {
            set => Api->setDrawMode(_handle, value);
        }

        public BitmapFlip ImageFlip
        {
            get => Api->getImageFlip(_handle);
            set => Api->setImageFlip(_handle, value);
        }

        public bool Visible
        {
            get => Api->isVisible(_handle) != 0;
            set => Api->setVisible(_handle, value ? 1 : 0);
        }

        /// <summary>Marks the sprite as fully opaque so sprites below it are not redrawn.</summary>
        public bool Opaque
        {
            set => Api->setOpaque(_handle, value ? 1 : 0);
        }

        public bool UpdatesEnabled
        {
            get => Api->updatesEnabled(_handle) != 0;
            set => Api->setUpdatesEnabled(_handle, value ? 1 : 0);
        }

        public bool IgnoresDrawOffset
        {
            set => Api->setIgnoresDrawOffset(_handle, value ? 1 : 0);
        }

        public byte Tag
        {
            get => Api->getTag(_handle);
            set => Api->setTag(_handle, value);
        }

        public void SetClipRect(LCDRect clipRect) => Api->setClipRect(_handle, clipRect);
        public void ClearClipRect() => Api->clearClipRect(_handle);
        public void MarkDirty() => Api->markDirty(_handle);
        public void MarkDirtyRect(PDRect rect) => Api->markDirtyRect(_handle, rect);

        public void SetStencilImage(Bitmap stencil, bool tile) => Api->setStencilImage(_handle, stencil.Handle, tile ? 1 : 0);

        /// <summary>Sets an 8x8 stencil pattern (8 bytes).</summary>
        public void SetStencilPattern(ReadOnlySpan<byte> pattern)
        {
            byte* p = stackalloc byte[8];
            for (int i = 0; i < 8; i++)
                p[i] = i < pattern.Length ? pattern[i] : (byte)0xFF;
            Api->setStencilPattern(_handle, p);
        }

        public void ClearStencil() => Api->clearStencil(_handle);

        public TileMap Tilemap
        {
            get => new TileMap(Api->getTilemap(_handle));
            set => Api->setTilemap(_handle, value.Handle);
        }

        // ---- Collisions ----

        public bool CollisionsEnabled
        {
            get => Api->collisionsEnabled(_handle) != 0;
            set => Api->setCollisionsEnabled(_handle, value ? 1 : 0);
        }

        /// <summary>The collision rectangle, relative to the sprite's bounds.</summary>
        public PDRect CollideRect
        {
            get => Api->getCollideRect(_handle);
            set => Api->setCollideRect(_handle, value);
        }

        public void ClearCollideRect() => Api->clearCollideRect(_handle);

        /// <summary>
        /// Moves toward (goalX, goalY) resolving collisions. Dispose the returned list to free it.
        /// </summary>
        public CollisionList MoveWithCollisions(float goalX, float goalY, out float actualX, out float actualY)
        {
            float ax, ay;
            int len;
            SpriteCollisionInfo* info = Api->moveWithCollisions(_handle, goalX, goalY, &ax, &ay, &len);
            actualX = ax;
            actualY = ay;
            return new CollisionList(info, len);
        }

        /// <summary>Like <see cref="MoveWithCollisions"/> without actually moving the sprite.</summary>
        public CollisionList CheckCollisions(float goalX, float goalY, out float actualX, out float actualY)
        {
            float ax, ay;
            int len;
            SpriteCollisionInfo* info = Api->checkCollisions(_handle, goalX, goalY, &ax, &ay, &len);
            actualX = ax;
            actualY = ay;
            return new CollisionList(info, len);
        }

        /// <summary>Sprites whose collide rects overlap this one. Dispose the returned list.</summary>
        public SpriteList OverlappingSprites()
        {
            int len;
            LCDSprite** list = Api->overlappingSprites(_handle, &len);
            return new SpriteList(list, len);
        }

        // ---- Lifetime ----

        /// <summary>Removes the sprite, frees the native sprite and releases the managed handle.</summary>
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

    /// <summary>Global sprite-system functions (<c>pd->sprite</c>).</summary>
    public static unsafe class Sprites
    {
        private static playdate_sprite* Api => PD.Sprite;

        /// <summary>Calls each sprite's update function, then draws all dirty sprites.</summary>
        public static void UpdateAndDrawSprites() => Api->updateAndDrawSprites();

        public static void DrawSprites() => Api->drawSprites();

        /// <summary>When true, all sprites are redrawn every frame (useful when the background changes a lot).</summary>
        public static void SetAlwaysRedraw(bool flag) => Api->setAlwaysRedraw(flag ? 1 : 0);

        public static void AddDirtyRect(LCDRect rect) => Api->addDirtyRect(rect);

        public static void RemoveAllSprites() => Api->removeAllSprites();

        public static int Count => Api->getSpriteCount();

        public static void SetClipRectsInRange(LCDRect clipRect, int startZ, int endZ) => Api->setClipRectsInRange(clipRect, startZ, endZ);
        public static void ClearClipRectsInRange(int startZ, int endZ) => Api->clearClipRectsInRange(startZ, endZ);

        /// <summary>Clears the collision world, e.g. after changing many collide rects.</summary>
        public static void ResetCollisionWorld() => Api->resetCollisionWorld();

        public static SpriteList QuerySpritesAtPoint(float x, float y)
        {
            int len;
            return new SpriteList(Api->querySpritesAtPoint(x, y, &len), len);
        }

        public static SpriteList QuerySpritesInRect(float x, float y, float width, float height)
        {
            int len;
            return new SpriteList(Api->querySpritesInRect(x, y, width, height, &len), len);
        }

        public static SpriteList QuerySpritesAlongLine(float x1, float y1, float x2, float y2)
        {
            int len;
            return new SpriteList(Api->querySpritesAlongLine(x1, y1, x2, y2, &len), len);
        }

        public static SpriteQueryList QuerySpriteInfoAlongLine(float x1, float y1, float x2, float y2)
        {
            int len;
            return new SpriteQueryList(Api->querySpriteInfoAlongLine(x1, y1, x2, y2, &len), len);
        }

        public static SpriteList AllOverlappingSprites()
        {
            int len;
            return new SpriteList(Api->allOverlappingSprites(&len), len);
        }
    }

    /// <summary>Collision results from <see cref="Sprite.MoveWithCollisions"/> (native array; free with Dispose).</summary>
    public readonly unsafe struct CollisionList : IDisposable
    {
        private readonly SpriteCollisionInfo* _items;
        public readonly int Count;

        internal CollisionList(SpriteCollisionInfo* items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

        public ref SpriteCollisionInfo this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new IndexOutOfRangeException();
                return ref _items[index];
            }
        }

        public Span<SpriteCollisionInfo> AsSpan() => new Span<SpriteCollisionInfo>(_items, Count);

        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }

    /// <summary>A native list of sprites returned by a query (free with Dispose).</summary>
    public readonly unsafe struct SpriteList : IDisposable
    {
        private readonly LCDSprite** _items;
        public readonly int Count;

        internal SpriteList(LCDSprite** items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

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

        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }

    /// <summary>Results of <see cref="Sprites.QuerySpriteInfoAlongLine"/> (free with Dispose).</summary>
    public readonly unsafe struct SpriteQueryList : IDisposable
    {
        private readonly SpriteQueryInfo* _items;
        public readonly int Count;

        internal SpriteQueryList(SpriteQueryInfo* items, int count)
        {
            _items = items;
            Count = items == null ? 0 : count;
        }

        public ref SpriteQueryInfo this[int index] => ref _items[index];

        public void Dispose()
        {
            if (_items != null)
                PD.System->realloc(_items, 0);
        }
    }
}
