// Port of luaglue.c from the Playdate SDK's C 3D library example: exposes the mini3d engine to Lua
// as lib3d.scene, lib3d.scenenode, lib3d.shape, lib3d.point and lib3d.matrix.
//
// Lua objects hold GC handles to managed objects; each class's __gc releases its handle.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mini3D;

namespace Lib3D;

/// <summary>A heap-allocated point for Lua (lib3d.point).</summary>
public sealed class PointObject
{
    public Point3D Value;
    public PointObject(Point3D value) { Value = value; }
}

/// <summary>A heap-allocated matrix for Lua (lib3d.matrix).</summary>
public sealed class MatrixObject
{
    public Matrix3D Value;
    public MatrixObject(Matrix3D value) { Value = value; }
}

public static unsafe class LuaGlue
{
    private const string SceneClass = "lib3d.scene";
    private const string NodeClass = "lib3d.scenenode";
    private const string ShapeClass = "lib3d.shape";
    private const string PointClass = "lib3d.point";
    private const string MatrixClass = "lib3d.matrix";

    private static T Get3DObj<T>(int n, string type) where T : class
    {
        T obj = Lua.GetArgManagedObject<T>(n, type);
        if (obj == null)
            Sys.Error($"object of type {type} not found at stack position {n}");
        return obj;
    }

    private static Scene3D GetScene(int n) => Get3DObj<Scene3D>(n, SceneClass);
    private static Scene3DNode GetSceneNode(int n) => Get3DObj<Scene3DNode>(n, NodeClass);
    private static Shape3D GetShape(int n) => Get3DObj<Shape3D>(n, ShapeClass);
    private static PointObject GetPoint(int n) => Get3DObj<PointObject>(n, PointClass);
    private static MatrixObject GetMatrix(int n) => Get3DObj<MatrixObject>(n, MatrixClass);

    // Every lib3d class releases the GC handle of the Lua object being collected.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneGc(void* L) { Lua.ReleaseManagedObject(1, SceneClass); return 0; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeGc(void* L) { Lua.ReleaseManagedObject(1, NodeClass); return 0; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ShapeGc(void* L) { Lua.ReleaseManagedObject(1, ShapeClass); return 0; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int PointGc(void* L) { Lua.ReleaseManagedObject(1, PointClass); return 0; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int MatrixGc(void* L) { Lua.ReleaseManagedObject(1, MatrixClass); return 0; }

    /// Scene

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneNew(void* L)
    {
        Lua.PushManagedObject(new Scene3D(), SceneClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneGetRoot(void* L)
    {
        Lua.PushManagedObject(GetScene(1).Root, NodeClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneDraw(void* L)
    {
        GetScene(1).Draw();
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneDrawNode(void* L)
    {
        GetScene(1).DrawNode(GetSceneNode(2), Graphics.GetFramePointer(), PD.LCD_ROWSIZE);
        Graphics.MarkUpdatedRows(0, PD.LCD_ROWS - 1);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetLight(void* L)
    {
        GetScene(1).SetGlobalLight(new Vector3D(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4)));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetCenter(void* L)
    {
        GetScene(1).SetCenter(Lua.GetArgFloat(2), Lua.GetArgFloat(3));
        return 0;
    }

    private static Point3D s_cameraOrigin = new Point3D(0, 0, -1);
    private static Point3D s_cameraLookAt = new Point3D(0, 0, 0);
    private static float s_cameraScale = 1.0f;
    private static Vector3D s_cameraUp = new Vector3D(0, -1, 0);

    private static void UpdateCamera(Scene3D scene) => scene.SetCamera(s_cameraOrigin, s_cameraLookAt, s_cameraScale, s_cameraUp);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetCameraOrigin(void* L)
    {
        Scene3D scene = GetScene(1);
        s_cameraOrigin = new Point3D(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        UpdateCamera(scene);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetCameraTarget(void* L)
    {
        Scene3D scene = GetScene(1);
        s_cameraLookAt = new Point3D(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        UpdateCamera(scene);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetCameraScale(void* L)
    {
        Scene3D scene = GetScene(1);
        s_cameraScale = Lua.GetArgFloat(2);
        UpdateCamera(scene);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int SceneSetCameraUp(void* L)
    {
        Scene3D scene = GetScene(1);
        s_cameraUp = new Vector3D(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        UpdateCamera(scene);
        return 0;
    }

    /// Node

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeAddShape(void* L)
    {
        Scene3DNode node = GetSceneNode(1);
        Shape3D shape = GetShape(2);
        MatrixObject m = Lua.GetArgManagedObject<MatrixObject>(3, MatrixClass);

        if (m != null)
            node.AddShape(shape, m.Value);
        else if (Lua.ArgCount > 2)
            node.AddShape(shape, new Vector3D(Lua.GetArgFloat(3), Lua.GetArgFloat(4), Lua.GetArgFloat(5)));
        else
            node.AddShape(shape);

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeMakeChildNode(void* L)
    {
        Lua.PushManagedObject(GetSceneNode(1).NewChild(), NodeClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetTransform(void* L)
    {
        GetSceneNode(1).SetTransform(GetMatrix(2).Value);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeAddTransform(void* L)
    {
        GetSceneNode(1).AddTransform(GetMatrix(2).Value);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeTranslateBy(void* L)
    {
        Scene3DNode node = GetSceneNode(1);
        Matrix3D xform = Matrix3D.MakeTranslate(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        node.AddTransform(xform);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeScaleBy(void* L)
    {
        Scene3DNode node = GetSceneNode(1);
        if (Lua.ArgCount > 2)
            node.ScaleBy(Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        else
            node.ScaleBy(Lua.GetArgFloat(2));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetColorBias(void* L)
    {
        GetSceneNode(1).SetColorBias(Lua.GetArgFloat(2));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetFilled(void* L)
    {
        GetSceneNode(1).SetFilled(Lua.GetArgBool(2));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetWireframeMode(void* L)
    {
        GetSceneNode(1).SetWireframeMode(Lua.GetArgInt(2));
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetWireframeColor(void* L)
    {
        GetSceneNode(1).SetWireframeColor(Lua.GetArgInt(2) != 0);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NodeSetVisible(void* L)
    {
        GetSceneNode(1).SetVisible(Lua.GetArgBool(2));
        return 0;
    }

    /// Shape

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ShapeNew(void* L)
    {
        Lua.PushManagedObject(new Shape3D(), ShapeClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ShapeAddFace(void* L)
    {
        Shape3D shape = GetShape(1);
        Point3D a = GetPoint(2).Value;
        Point3D b = GetPoint(3).Value;
        Point3D c = GetPoint(4).Value;
        Point3D? d = null;
        float color;

        if (Lua.GetArgType(5, out string type) == LuaType.Object && type == PointClass)
        {
            d = GetPoint(5).Value;
            color = Lua.GetArgFloat(6);
        }
        else
        {
            color = Lua.GetArgFloat(5);
        }

        shape.AddFace(a, b, c, d, color);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ShapeSetClosed(void* L)
    {
        GetShape(1).IsClosed = Lua.GetArgBool(2);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ShapeSetOrderTableSize(void* L)
    {
        GetShape(1).OrderTableSize = Lua.GetArgInt(2);
        return 0;
    }

    /// Point

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int PointNew(void* L)
    {
        var p = new Point3D(Lua.GetArgFloat(1), Lua.GetArgFloat(2), Lua.GetArgFloat(3));
        Lua.PushManagedObject(new PointObject(p), PointClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int PointIndex(void* L)
    {
        PointObject p = GetPoint(1);
        string arg = Lua.GetArgString(2);

        if (arg == "x")
            Lua.PushFloat(p.Value.X);
        else if (arg == "y")
            Lua.PushFloat(p.Value.Y);
        else if (arg == "z")
            Lua.PushFloat(p.Value.Z);
        else
            Lua.PushNil();
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int PointNewIndex(void* L)
    {
        PointObject p = GetPoint(1);
        string arg = Lua.GetArgString(2);

        if (arg == "x")
            p.Value.X = Lua.GetArgFloat(3);
        else if (arg == "y")
            p.Value.Y = Lua.GetArgFloat(3);
        else if (arg == "z")
            p.Value.Z = Lua.GetArgFloat(3);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int PointMul(void* L)
    {
        PointObject p = GetPoint(1);
        MatrixObject m = GetMatrix(2);
        Lua.PushManagedObject(new PointObject(Matrix3D.Apply(m.Value, p.Value)), PointClass);
        return 1;
    }

    /// Matrix

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int MatrixMul(void* L)
    {
        MatrixObject l = GetMatrix(1);
        MatrixObject r = GetMatrix(2);
        Lua.PushManagedObject(new MatrixObject(Matrix3D.Multiply(l.Value, r.Value)), MatrixClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int MatrixNew(void* L)
    {
        Matrix3D p = default;
        p.IsIdentity = false;
        p.M00 = Lua.GetArgFloat(1);
        p.M01 = Lua.GetArgFloat(2);
        p.M02 = Lua.GetArgFloat(3);
        p.M10 = Lua.GetArgFloat(4);
        p.M11 = Lua.GetArgFloat(5);
        p.M12 = Lua.GetArgFloat(6);
        p.M20 = Lua.GetArgFloat(7);
        p.M21 = Lua.GetArgFloat(8);
        p.M22 = Lua.GetArgFloat(9);
        p.Dx = Lua.GetArgFloat(10);
        p.Dy = Lua.GetArgFloat(11);
        p.Dz = Lua.GetArgFloat(12);
        p.Inverting = p.Determinant < 0;

        Lua.PushManagedObject(new MatrixObject(p), MatrixClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int MatrixNewRotation(void* L)
    {
        Matrix3D p = Matrix3D.MakeRotation(Lua.GetArgFloat(1), Lua.GetArgFloat(2), Lua.GetArgFloat(3), Lua.GetArgFloat(4));
        Lua.PushManagedObject(new MatrixObject(p), MatrixClass);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int MatrixAddTranslation(void* L)
    {
        MatrixObject l = GetMatrix(1);
        l.Value.Dx += Lua.GetArgFloat(2);
        l.Value.Dy += Lua.GetArgFloat(3);
        l.Value.Dz += Lua.GetArgFloat(4);
        return 0;
    }

    private static void Register(string name, LuaFunction[] methods)
    {
        if (!Lua.RegisterClass(name, methods, out string error))
            Sys.Log($"registerClass failed, {error}");
    }

    public static void Register3D()
    {
        Register(SceneClass, new[]
        {
            new LuaFunction("__gc", &SceneGc),
            new LuaFunction("new", &SceneNew),
            new LuaFunction("draw", &SceneDraw),
            new LuaFunction("drawNode", &SceneDrawNode),
            new LuaFunction("getRootNode", &SceneGetRoot),
            new LuaFunction("setLight", &SceneSetLight),
            new LuaFunction("setCenter", &SceneSetCenter),
            new LuaFunction("setCameraOrigin", &SceneSetCameraOrigin),
            new LuaFunction("setCameraScale", &SceneSetCameraScale),
            new LuaFunction("setCameraTarget", &SceneSetCameraTarget),
            new LuaFunction("setCameraUp", &SceneSetCameraUp),
        });

        Register(NodeClass, new[]
        {
            new LuaFunction("__gc", &NodeGc),
            new LuaFunction("addChildNode", &NodeMakeChildNode),
            new LuaFunction("addShape", &NodeAddShape),
            new LuaFunction("addTransform", &NodeAddTransform),
            new LuaFunction("setTransform", &NodeSetTransform),
            new LuaFunction("translateBy", &NodeTranslateBy),
            new LuaFunction("scaleBy", &NodeScaleBy),
            new LuaFunction("setColorBias", &NodeSetColorBias),
            new LuaFunction("setFilled", &NodeSetFilled),
            new LuaFunction("setWireframeMode", &NodeSetWireframeMode),
            new LuaFunction("setWireframeColor", &NodeSetWireframeColor),
            new LuaFunction("setVisible", &NodeSetVisible),
        });

        Register(ShapeClass, new[]
        {
            new LuaFunction("new", &ShapeNew),
            new LuaFunction("__gc", &ShapeGc),
            new LuaFunction("addFace", &ShapeAddFace),
            new LuaFunction("setClosed", &ShapeSetClosed),
            new LuaFunction("setOrderTableSize", &ShapeSetOrderTableSize),
        });

        Register(PointClass, new[]
        {
            new LuaFunction("new", &PointNew),
            new LuaFunction("__gc", &PointGc),
            new LuaFunction("__index", &PointIndex),
            new LuaFunction("__newindex", &PointNewIndex),
            new LuaFunction("__mul", &PointMul),
        });

        Register(MatrixClass, new[]
        {
            new LuaFunction("__gc", &MatrixGc),
            new LuaFunction("__mul", &MatrixMul),
            new LuaFunction("new", &MatrixNew),
            new LuaFunction("newRotation", &MatrixNewRotation),
            new LuaFunction("addTranslation", &MatrixAddTranslation),
        });
    }
}

public sealed class Lib3DGame : Game
{
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new Lib3DGame());

    // main.lua runs the demo; C# provides the lib3d classes.
    protected override bool UsesUpdateCallback => false;

    protected override void InitializeLua() => LuaGlue.Register3D();
}
