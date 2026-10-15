// Port of mini3d/scene.{h,c} from the Playdate SDK's C 3D library example.
// Linked lists of the C version become List<T>/arrays; faces refer to points by index.

namespace Mini3D;

[Flags]
public enum RenderStyle
{
    InheritStyle = 0,
    Filled = 1 << 0,
    Wireframe = 1 << 1,
    WireframeBack = 1 << 2,
    WireframeWhite = 1 << 3, // 0 = black, 1 = white
}

public struct FaceInstance
{
    public int P1; // indexes into the shape instance's points array
    public int P2;
    public int P3;
    public int P4; // -1 for triangles
    public Vector3D Normal;
    public float ColorBias; // added to lighting computation
    public int Next; // ordering table chain, -1 = end
}

public sealed class ShapeInstance
{
    public Shape3D Prototype;
    public Matrix3D Transform;

    // cached values from node tree update
    public int NPoints;
    public Point3D[] Points;
    public int NFaces;
    public FaceInstance[] Faces;
    public Point3D Center;
    public float ColorBias;
    public RenderStyle RenderStyle;
    public bool Inverted; // transformation flipped it across a plane, so we need to reverse backface check
    public bool UseZBuffer;
    public int OrderTableSize;
    public int[] OrderTable = System.Array.Empty<int>();
}

public sealed class Scene3DNode
{
    public Matrix3D Transform = Matrix3D.Identity;
    public Scene3DNode ParentNode;
    public readonly List<Scene3DNode> ChildNodes = new List<Scene3DNode>();
    public readonly List<ShapeInstance> Shapes = new List<ShapeInstance>();
    public float ColorBias;
    public RenderStyle RenderStyle = RenderStyle.InheritStyle;
    public bool IsVisible = true;
    public bool NeedsUpdate = true;
    public bool UseZBuffer = true;

    public void SetTransform(in Matrix3D xform)
    {
        Transform = xform;

        // mark this branch of the tree for updating
        for (Scene3DNode node = this; node != null; node = node.ParentNode)
            node.NeedsUpdate = true;
    }

    public void AddTransform(in Matrix3D xform) => SetTransform(Matrix3D.Multiply(Transform, xform));

    public void SetColorBias(float bias)
    {
        ColorBias = bias;
        NeedsUpdate = true;
    }

    public void SetRenderStyle(RenderStyle style)
    {
        RenderStyle = style;
        NeedsUpdate = true;
    }

    public RenderStyle GetRenderStyle()
    {
        for (Scene3DNode node = this; node != null; node = node.ParentNode)
            if (node.RenderStyle != RenderStyle.InheritStyle)
                return node.RenderStyle;
        return RenderStyle.Filled;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        NeedsUpdate = true;
    }

    public void SetUsesZBuffer(bool flag)
    {
        UseZBuffer = flag;
        NeedsUpdate = true;
    }

    public void TranslateBy(float dx, float dy, float dz) => AddTransform(Matrix3D.MakeTranslate(dx, dy, dz));

    /// <summary>Scales the node's current transform (negative factors mirror it).</summary>
    public void ScaleBy(float sx, float sy, float sz)
    {
        Matrix3D xform = Transform;
        xform.IsIdentity = false;
        xform.M00 *= sx; xform.M10 *= sx; xform.M20 *= sx;
        xform.M01 *= sy; xform.M11 *= sy; xform.M21 *= sy;
        xform.M02 *= sz; xform.M12 *= sz; xform.M22 *= sz;
        xform.Dx *= sx;
        xform.Dy *= sy;
        xform.Dz *= sz;
        xform.Inverting = sx * sy * sz < 0;
        SetTransform(xform);
    }

    public void ScaleBy(float s) => ScaleBy(s, s, s);

    /// <summary>Draw filled faces (the default).</summary>
    public void SetFilled(bool filled)
    {
        RenderStyle style = GetRenderStyle();
        SetRenderStyle(filled ? style | RenderStyle.Filled : style & ~RenderStyle.Filled);
    }

    /// <summary>0 = no wireframe, 1 = front faces only, 2 = front and back faces.</summary>
    public void SetWireframeMode(int mode)
    {
        RenderStyle style = GetRenderStyle();
        if (mode > 0)
        {
            style |= RenderStyle.Wireframe;
            style = mode == 1 ? style & ~RenderStyle.WireframeBack : style | RenderStyle.WireframeBack;
        }
        else
        {
            style &= ~RenderStyle.Wireframe;
        }
        SetRenderStyle(style);
    }

    public void SetWireframeColor(bool white)
    {
        RenderStyle style = GetRenderStyle();
        SetRenderStyle(white ? style | RenderStyle.WireframeWhite : style & ~RenderStyle.WireframeWhite);
    }

    public void AddShape(Shape3D shape, Matrix3D transform)
    {
        var instance = new ShapeInstance
        {
            RenderStyle = RenderStyle.InheritStyle,
            Prototype = shape,
            NPoints = shape.NPoints,
            Points = new Point3D[shape.NPoints],
            NFaces = shape.NFaces,
            Faces = new FaceInstance[shape.NFaces],
            Transform = transform,
            Center = Matrix3D.Apply(transform, shape.Center),
        };

        for (int i = 0; i < shape.NFaces; ++i)
        {
            ref FaceInstance face = ref instance.Faces[i];
            ref Face3D proto = ref shape.Faces[i];
            face.P1 = proto.P1;
            face.P2 = proto.P2;
            face.P3 = proto.P3;
            face.P4 = proto.P4 != 0xffff ? proto.P4 : -1;
            face.ColorBias = proto.ColorBias;
            face.Next = -1;
        }

        Shapes.Add(instance);
    }

    public void AddShape(Shape3D shape, Vector3D offset) => AddShape(shape, Matrix3D.MakeTranslate(offset.Dx, offset.Dy, offset.Dz));

    public void AddShape(Shape3D shape) => AddShape(shape, Matrix3D.Identity);

    public Scene3DNode NewChild()
    {
        var child = new Scene3DNode { ParentNode = this };
        ChildNodes.Add(child);
        return child;
    }
}

public sealed unsafe class Scene3D
{
    private const int Width = 400;
    private const int Height = 240;

    public bool HasPerspective = true;
    public Matrix3D Camera;
    public Vector3D Light;

    // location of the Z vanishing point on the screen. (0,0) is top left corner, (1,1) is bottom right.
    public float CenterX;
    public float CenterY;

    // display scaling factor. Default is 120, so that the screen extents are (-1.66,1.66)x(-1,1)
    public float Scale;

    public readonly Scene3DNode Root = new Scene3DNode();

    // all shapes from the render tree are added here and z-sorted
    private readonly List<ShapeInstance> _shapeList = new List<ShapeInstance>();

    private float _zmin;

    // 33 dither patterns (8 rows each) from black to white.
    private static readonly byte[] s_patterns =
    {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x80, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
        0x88, 0x00, 0x00, 0x00, 0x88, 0x00, 0x00, 0x00,
        0x88, 0x00, 0x20, 0x00, 0x88, 0x00, 0x02, 0x00,
        0x88, 0x00, 0x22, 0x00, 0x88, 0x00, 0x22, 0x00,
        0xa8, 0x00, 0x22, 0x00, 0x8a, 0x00, 0x22, 0x00,
        0xaa, 0x00, 0x22, 0x00, 0xaa, 0x00, 0x22, 0x00,
        0xaa, 0x00, 0xa2, 0x00, 0xaa, 0x00, 0x2a, 0x00,
        0xaa, 0x00, 0xaa, 0x00, 0xaa, 0x00, 0xaa, 0x00,
        0xaa, 0x40, 0xaa, 0x00, 0xaa, 0x04, 0xaa, 0x00,
        0xaa, 0x44, 0xaa, 0x00, 0xaa, 0x44, 0xaa, 0x00,
        0xaa, 0x44, 0xaa, 0x10, 0xaa, 0x44, 0xaa, 0x01,
        0xaa, 0x44, 0xaa, 0x11, 0xaa, 0x44, 0xaa, 0x11,
        0xaa, 0x54, 0xaa, 0x11, 0xaa, 0x45, 0xaa, 0x11,
        0xaa, 0x55, 0xaa, 0x11, 0xaa, 0x55, 0xaa, 0x11,
        0xaa, 0x55, 0xaa, 0x51, 0xaa, 0x55, 0xaa, 0x15,
        0xaa, 0x55, 0xaa, 0x55, 0xaa, 0x55, 0xaa, 0x55,
        0xba, 0x55, 0xaa, 0x55, 0xab, 0x55, 0xaa, 0x55,
        0xbb, 0x55, 0xaa, 0x55, 0xbb, 0x55, 0xaa, 0x55,
        0xbb, 0x55, 0xea, 0x55, 0xbb, 0x55, 0xae, 0x55,
        0xbb, 0x55, 0xee, 0x55, 0xbb, 0x55, 0xee, 0x55,
        0xfb, 0x55, 0xee, 0x55, 0xbf, 0x55, 0xee, 0x55,
        0xff, 0x55, 0xee, 0x55, 0xff, 0x55, 0xee, 0x55,
        0xff, 0x55, 0xfe, 0x55, 0xff, 0x55, 0xef, 0x55,
        0xff, 0x55, 0xff, 0x55, 0xff, 0x55, 0xff, 0x55,
        0xff, 0x55, 0xff, 0xd5, 0xff, 0x55, 0xff, 0x5d,
        0xff, 0x55, 0xff, 0xdd, 0xff, 0x55, 0xff, 0xdd,
        0xff, 0x75, 0xff, 0xdd, 0xff, 0x57, 0xff, 0xdd,
        0xff, 0x77, 0xff, 0xdd, 0xff, 0x77, 0xff, 0xdd,
        0xff, 0x77, 0xff, 0xfd, 0xff, 0x77, 0xff, 0xdf,
        0xff, 0x77, 0xff, 0xff, 0xff, 0x77, 0xff, 0xff,
        0xff, 0xf7, 0xff, 0xff, 0xff, 0x7f, 0xff, 0xff,
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
    };

    public Scene3D()
    {
        SetCamera(new Point3D(0, 0, 0), new Point3D(0, 0, 1), 1.0f, new Vector3D(0, 1, 0));
        SetGlobalLight(new Vector3D(0, -1, 0));
        SetCenter(0.5f, 0.5f);
    }

    public void SetGlobalLight(Vector3D light) => Light = light;

    public void SetCenter(float x, float y)
    {
        CenterX = x;
        CenterY = y;
    }

    public void SetCamera(Point3D origin, Point3D lookAt, float scale, Vector3D up)
    {
        Matrix3D camera = Matrix3D.Identity;
        camera.IsIdentity = false;
        camera.Dx = -origin.X;
        camera.Dy = -origin.Y;
        camera.Dz = -origin.Z;

        var dir = new Vector3D(lookAt.X - origin.X, lookAt.Y - origin.Y, lookAt.Z - origin.Z);
        float l = MathF.Sqrt(dir.LengthSquared);
        dir.Dx /= l;
        dir.Dy /= l;
        dir.Dz /= l;

        Scale = 240 * scale;

        // first yaw around the y axis
        float h = 0;
        if (dir.Dx != 0 || dir.Dz != 0)
        {
            h = MathF.Sqrt(dir.Dx * dir.Dx + dir.Dz * dir.Dz);
            Matrix3D yaw = Matrix3D.Make(dir.Dz / h, 0, -dir.Dx / h, 0, 1, 0, dir.Dx / h, 0, dir.Dz / h, false);
            camera = Matrix3D.Multiply(camera, yaw);
        }

        // then pitch up/down to y elevation
        Matrix3D pitch = Matrix3D.Make(1, 0, 0, 0, h, -dir.Dy, 0, dir.Dy, h, false);
        camera = Matrix3D.Multiply(camera, pitch);

        // and roll to position the up vector
        if (up.Dx != 0 || up.Dy != 0)
        {
            l = MathF.Sqrt(up.Dx * up.Dx + up.Dy * up.Dy);
            Matrix3D roll = Matrix3D.Make(up.Dy / l, up.Dx / l, 0, -up.Dx / l, up.Dy / l, 0, 0, 0, 1, false);
            Camera = Matrix3D.Multiply(camera, roll);
        }
        else
        {
            Camera = camera;
        }

        Root.NeedsUpdate = true;
    }

    private void UpdateShapeInstance(ShapeInstance shape, in Matrix3D xform, float colorBias, RenderStyle style)
    {
        Shape3D proto = shape.Prototype;
        Point3D[] points = shape.Points;

        // transform points
        for (int i = 0; i < shape.NPoints; ++i)
            points[i] = Matrix3D.Apply(xform, Matrix3D.Apply(shape.Transform, proto.Points[i]));

        shape.Center = Matrix3D.Apply(xform, Matrix3D.Apply(shape.Transform, proto.Center));
        shape.ColorBias = proto.ColorBias + colorBias;
        shape.RenderStyle = style;
        shape.Inverted = xform.Inverting;

        float zmin = 1e23f;
        float zmax = 0;
        int orderSize = proto.OrderTableSize;

        if (orderSize != shape.OrderTableSize)
        {
            shape.OrderTableSize = orderSize;
            shape.OrderTable = new int[orderSize];
        }
        for (int i = 0; i < orderSize; i++)
            shape.OrderTable[i] = -1;

        // recompute face normals
        for (int i = 0; i < shape.NFaces; ++i)
        {
            ref FaceInstance face = ref shape.Faces[i];
            face.Normal = Vector3D.PNormal(points[face.P1], points[face.P2], points[face.P3]);

            if (orderSize > 0)
            {
                float z = points[face.P1].Z + points[face.P2].Z + points[face.P3].Z;
                if (z < zmin) zmin = z;
                if (z > zmax) zmax = z;
            }
        }

        // apply perspective, scale to display
        for (int i = 0; i < shape.NPoints; ++i)
        {
            ref Point3D p = ref points[i];

            if (p.Z > 0)
            {
                if (HasPerspective)
                {
                    p.X = Scale * (p.X / p.Z + 1.6666666f * CenterX);
                    p.Y = Scale * (p.Y / p.Z + CenterY);
                }
                else
                {
                    p.X = Scale * (p.X + 1.6666666f * CenterX);
                    p.Y = Scale * (p.Y + CenterY);
                }
            }

            if (p.Z < _zmin)
                _zmin = p.Z;
        }

        // Put faces in bins separated by average z value
        if (orderSize > 0)
        {
            float d = zmax - zmin + 0.0001f;
            for (int i = 0; i < shape.NFaces; ++i)
            {
                ref FaceInstance face = ref shape.Faces[i];
                int idx = (int)(orderSize * (points[face.P1].Z + points[face.P2].Z + points[face.P3].Z - zmin) / d);
                face.Next = shape.OrderTable[idx];
                shape.OrderTable[idx] = i;
            }
        }
    }

    private void UpdateNode(Scene3DNode node, Matrix3D xform, float colorBias, RenderStyle style, bool update)
    {
        if (!node.IsVisible)
            return;

        if (node.NeedsUpdate)
        {
            update = true;
            node.NeedsUpdate = false;
        }

        if (!update)
            return;

        xform = Matrix3D.Multiply(node.Transform, xform);
        colorBias += node.ColorBias;

        if (node.RenderStyle != RenderStyle.InheritStyle)
            style = node.RenderStyle;

        // the C version keeps shapes in a list with the most recently added first
        for (int i = node.Shapes.Count - 1; i >= 0; i--)
        {
            ShapeInstance shape = node.Shapes[i];
            UpdateShapeInstance(shape, xform, colorBias, style);
            shape.UseZBuffer = node.UseZBuffer;
        }

        foreach (Scene3DNode child in node.ChildNodes)
            UpdateNode(child, xform, colorBias, style, update);
    }

    private void GetShapesAtNode(Scene3DNode node)
    {
        if (!node.IsVisible)
            return;
        for (int i = node.Shapes.Count - 1; i >= 0; i--)
            _shapeList.Add(node.Shapes[i]);
        foreach (Scene3DNode child in node.ChildNodes)
            GetShapesAtNode(child);
    }

    private void DrawShapeFace(ShapeInstance shape, byte* bitmap, int rowstride, Point3D* points, ref FaceInstance face)
    {
        Point3D* p1 = &points[face.P1];
        Point3D* p2 = &points[face.P2];
        Point3D* p3 = &points[face.P3];
        Point3D* p4 = face.P4 >= 0 ? &points[face.P4] : null;

        // If any vertex is behind the camera, skip it
        if (p1->Z <= 0 || p2->Z <= 0 || p3->Z <= 0)
            return;

        float x1 = p1->X, y1 = p1->Y;
        float x2 = p2->X, y2 = p2->Y;
        float x3 = p3->X, y3 = p3->Y;

        // quick bounds check
        if ((x1 < 0 && x2 < 0 && x3 < 0 && (p4 == null || p4->X < 0)) ||
            (x1 >= Width && x2 >= Width && x3 >= Width && (p4 == null || p4->X >= Width)) ||
            (y1 < 0 && y2 < 0 && y3 < 0 && (p4 == null || p4->Y < 0)) ||
            (y1 >= Height && y2 >= Height && y3 >= Height && (p4 == null || p4->Y >= Height)))
            return;

        if (shape.Prototype.IsClosed)
        {
            // only render front side of faces
            float d = HasPerspective
                ? (x2 - x1) * (y3 - y1) - (y2 - y1) * (x3 - x1) // use winding order
                : face.Normal.Dz;                                // use direction of normal

            if ((d >= 0) ^ shape.Inverted)
                return;
        }

        // lighting
        float c = face.ColorBias + shape.ColorBias;
        float v;

        if (c <= -1)
        {
            v = 0;
        }
        else if (c >= 1)
        {
            v = 1;
        }
        else
        {
            if (shape.Inverted)
                v = (1.0f + Vector3D.Dot(face.Normal, Light)) / 2;
            else
                v = (1.0f - Vector3D.Dot(face.Normal, Light)) / 2;

            if (c > 0)
                v = c + (1 - c) * v; // map [0,1] to [c,1]
            else if (c < 0)
                v *= 1 + c; // map [0,1] to [0, 1+c]
        }

        int vi = (int)(32.99f * v);
        if (vi > 32)
            vi = 32;
        else if (vi < 0)
            vi = 0;

        fixed (byte* pattern = &s_patterns[vi * 8])
        {
            if (p4 != null)
            {
                if (shape.UseZBuffer)
                    Render.FillQuadZBuf(bitmap, rowstride, p1, p2, p3, p4, pattern);
                else
                    Render.FillQuad(bitmap, rowstride, p1, p2, p3, p4, pattern);
            }
            else
            {
                if (shape.UseZBuffer)
                    Render.FillTriangleZBuf(bitmap, rowstride, p1, p2, p3, pattern);
                else
                    Render.FillTriangle(bitmap, rowstride, p1, p2, p3, pattern);
            }
        }
    }

    private void DrawFilledShape(ShapeInstance shape, byte* bitmap, int rowstride)
    {
        fixed (Point3D* points = shape.Points)
        {
            if (shape.OrderTableSize > 0)
            {
                for (int i = shape.OrderTableSize - 1; i >= 0; --i)
                {
                    for (int f = shape.OrderTable[i]; f >= 0; f = shape.Faces[f].Next)
                        DrawShapeFace(shape, bitmap, rowstride, points, ref shape.Faces[f]);
                }
            }
            else
            {
                for (int f = 0; f < shape.NFaces; ++f)
                    DrawShapeFace(shape, bitmap, rowstride, points, ref shape.Faces[f]);
            }
        }
    }

    private void DrawWireframe(ShapeInstance shape, byte* bitmap, int rowstride)
    {
        RenderStyle style = shape.RenderStyle;

        fixed (Point3D* points = shape.Points)
        fixed (byte* color = &s_patterns[32 * 8])
        {
            for (int f = 0; f < shape.NFaces; ++f)
            {
                ref FaceInstance face = ref shape.Faces[f];
                Point3D* p1 = &points[face.P1];
                Point3D* p2 = &points[face.P2];
                Point3D* p3 = &points[face.P3];
                Point3D* p4 = face.P4 >= 0 ? &points[face.P4] : null;

                // If any vertex is behind the camera, skip it
                if (p1->Z <= 0 || p2->Z <= 0 || p3->Z <= 0 || (p4 != null && p4->X <= 0))
                    continue;

                float x1 = p1->X, y1 = p1->Y;
                float x2 = p2->X, y2 = p2->Y;
                float x3 = p3->X, y3 = p3->Y;
                float x4 = p4 != null ? p4->X : 0;
                float y4 = p4 != null ? p4->Y : 0;

                // quick bounds check (the C version compares x4 in the third test too)
                if ((x1 < 0 && x2 < 0 && x3 < 0 && x4 < 0) || (x1 >= Width && x2 >= Width && x3 >= Width && x4 >= Width) ||
                    (y1 < 0 && y2 < 0 && y3 < 0 && x4 < 0) || (y1 >= Height && y2 >= Height && y3 >= Height && y4 >= Height))
                    continue;

                if ((style & RenderStyle.WireframeBack) == 0)
                {
                    // only render front side of faces
                    float d = HasPerspective
                        ? (x2 - x1) * (y3 - y1) - (y2 - y1) * (x3 - x1)
                        : face.Normal.Dz;

                    if ((d > 0) ^ shape.Inverted)
                        continue;
                }

                if (shape.UseZBuffer)
                {
                    Render.DrawLineZBuf(bitmap, rowstride, p1, p2, 1, color);
                    Render.DrawLineZBuf(bitmap, rowstride, p2, p3, 1, color);
                    if (p4 != null)
                    {
                        Render.DrawLineZBuf(bitmap, rowstride, p3, p4, 1, color);
                        Render.DrawLineZBuf(bitmap, rowstride, p4, p1, 1, color);
                    }
                    else
                    {
                        Render.DrawLineZBuf(bitmap, rowstride, p3, p1, 1, color);
                    }
                }
                else
                {
                    Render.DrawLine(bitmap, rowstride, p1, p2, 1, color);
                    Render.DrawLine(bitmap, rowstride, p2, p3, 1, color);
                    if (p4 != null)
                    {
                        Render.DrawLine(bitmap, rowstride, p3, p4, 1, color);
                        Render.DrawLine(bitmap, rowstride, p4, p1, 1, color);
                    }
                    else
                    {
                        Render.DrawLine(bitmap, rowstride, p3, p1, 1, color);
                    }
                }
            }
        }
    }

    public void DrawNode(Scene3DNode node, byte* bitmap, int rowstride)
    {
        // order shapes by z, and draw back to front
        _shapeList.Clear();
        GetShapesAtNode(node);
        if (_shapeList.Count > 1)
            _shapeList.Sort((a, b) => b.Center.Z.CompareTo(a.Center.Z));

        foreach (ShapeInstance shape in _shapeList)
        {
            RenderStyle style = shape.RenderStyle;

            if ((style & RenderStyle.Filled) != 0)
                DrawFilledShape(shape, bitmap, rowstride);

            if ((style & RenderStyle.Wireframe) != 0)
                DrawWireframe(shape, bitmap, rowstride);
        }
    }

    // Camera parameters kept separately so each can be changed on its own (as the Lua API does).
    private Point3D _cameraOrigin = new Point3D(0, 0, -1);
    private Point3D _cameraTarget = new Point3D(0, 0, 0);
    private float _cameraScale = 1.0f;
    private Vector3D _cameraUp = new Vector3D(0, -1, 0);

    public Point3D CameraOrigin
    {
        get => _cameraOrigin;
        set { _cameraOrigin = value; ApplyCamera(); }
    }

    public Point3D CameraTarget
    {
        get => _cameraTarget;
        set { _cameraTarget = value; ApplyCamera(); }
    }

    public float CameraScale
    {
        get => _cameraScale;
        set { _cameraScale = value; ApplyCamera(); }
    }

    public Vector3D CameraUp
    {
        get => _cameraUp;
        set { _cameraUp = value; ApplyCamera(); }
    }

    private void ApplyCamera() => SetCamera(_cameraOrigin, _cameraTarget, _cameraScale, _cameraUp);

    /// <summary>Draws the scene into the Playdate frame buffer.</summary>
    public void Draw()
    {
        Draw(Playdate.Graphics.GetFramePointer(), Playdate.PD.LCD_ROWSIZE);
        Playdate.Graphics.MarkUpdatedRows(0, Playdate.PD.LCD_ROWS - 1);
    }

    public void Draw(byte* bitmap, int rowstride)
    {
        _zmin = 1e23f;
        UpdateNode(Root, Camera, 0, RenderStyle.Filled, false);
        Render.ResetZBuffer(_zmin);
        DrawNode(Root, bitmap, rowstride);
    }
}
