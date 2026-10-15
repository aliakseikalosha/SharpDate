// The Playdate SDK's "3D library" example scenes (icosahedra.lua, knot.lua, knot-ordered.lua,
// ztest.lua) rewritten in C# on top of the Mini3D library - no Lua involved.
//
// Controls: A = next scene (or pick one from the system menu).
//   Icosahedra: d-pad moves the camera, crank rolls it.
//   Z test: up/down roll the scene.

using System.Runtime.CompilerServices;
using Mini3D;

namespace Lib3DCSharp;

public abstract class Demo3D
{
    public abstract string Name { get; }
    public abstract void Update();
    public virtual void OnCrank(float angle) { }
}

public sealed class Lib3DCSharpGame : Game
{
    private static readonly string[] SceneNames = { "Icosahedra", "Knot", "Ordered", "Z test" };

    private Demo3D _demo;
    private int _index;
    private MenuItem _menu;

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new Lib3DCSharpGame());

    protected override void Initialize()
    {
        Display.RefreshRate = 0; // as fast as possible
        _menu = Sys.AddOptionsMenuItem("Scene", SceneNames, item => Select(item.Value));
        Select(0);
    }

    private void Select(int index)
    {
        _index = index;
        _demo = index switch
        {
            0 => new IcosahedraDemo(),
            1 => new KnotDemo(),
            2 => new OrderedKnotDemo(),
            _ => new ZTestDemo(),
        };
        if (_menu.Value != index)
            _menu.Value = index;
        GC.Collect(); // drop the previous scene now rather than mid-frame
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            Select((_index + 1) % SceneNames.Length);

        if (Sys.CrankChange != 0)
            _demo.OnCrank(Sys.CrankAngle);

        Graphics.Clear(SolidColor.Black);
        _demo.Update();
        Sys.DrawFPS(0, 0);
        Graphics.SetDrawMode(DrawMode.FillWhite);
        Graphics.DrawText(_demo.Name, 4, 222);
        Graphics.SetDrawMode(DrawMode.Copy);
        return true;
    }
}

/// <summary>icosahedra.lua: six copies of an icosahedron with different render styles.</summary>
public sealed class IcosahedraDemo : Demo3D
{
    private readonly Scene3D _scene = new Scene3D();
    private readonly Matrix3D _rot1 = Matrix3D.MakeRotation(5, 0, 0, 1);
    private readonly Matrix3D _rot2 = Matrix3D.MakeRotation(3, 0, 1, 0);
    private float _x;
    private float _z = -4;

    public override string Name => "Icosahedra";

    public IcosahedraDemo()
    {
        _scene.CameraOrigin = new Point3D(0, 0, -4);
        _scene.SetGlobalLight(new Vector3D(0.2f, 0.8f, 0.4f));

        Shape3D shape = ShapeBuilder.Icosahedron();
        Scene3DNode root = _scene.Root;

        Scene3DNode n1 = root.NewChild();
        n1.AddShape(shape, new Vector3D(2, 0, 0));

        Scene3DNode n2 = root.NewChild();
        n2.AddShape(shape, new Vector3D(-2, 0, 0));
        n2.SetColorBias(0.8f);

        Scene3DNode n3 = root.NewChild();
        n3.AddShape(shape, new Vector3D(0, 2, 0));
        n3.SetWireframeMode(2);
        n3.SetWireframeColor(true);
        n3.SetFilled(false);

        Scene3DNode n4 = root.NewChild();
        n4.AddShape(shape, new Vector3D(0, -2, 0));
        n4.SetWireframeMode(1);
        n4.SetWireframeColor(true);
        n4.SetFilled(false);

        Scene3DNode n5 = root.NewChild();
        n5.AddShape(shape, new Vector3D(0, 0, 2));
        n5.SetColorBias(0.8f);
        n5.SetWireframeMode(2);
        n5.SetWireframeColor(false);

        Scene3DNode n6 = root.NewChild();
        n6.AddShape(shape, new Vector3D(0, 0, -2));
        n6.SetColorBias(-0.8f);
        n6.SetWireframeMode(1);
        n6.SetWireframeColor(true);
    }

    public override void OnCrank(float angle)
    {
        float a = angle * MathF.PI / 180;
        _scene.CameraUp = new Vector3D(MathF.Sin(a), MathF.Cos(a), 0);
    }

    public override void Update()
    {
        Buttons held = Sys.ButtonsCurrent;
        int dx = ((held & Buttons.Right) != 0 ? 1 : 0) - ((held & Buttons.Left) != 0 ? 1 : 0);
        int dz = ((held & Buttons.Up) != 0 ? 1 : 0) - ((held & Buttons.Down) != 0 ? 1 : 0);
        if (dx != 0 || dz != 0)
        {
            _x += dx / 10f;
            _z += dz / 10f;
            _scene.CameraOrigin = new Point3D(-_x, 0, _z);
        }

        _scene.Root.AddTransform(_rot2);
        _scene.Root.AddTransform(_rot1);
        _scene.Draw();
    }
}

/// <summary>knot.lua: a wireframed trefoil knot inside a checkered torus.</summary>
public sealed class KnotDemo : Demo3D
{
    private readonly Scene3D _scene = new Scene3D();
    private readonly Scene3DNode _knot;
    private readonly Scene3DNode _torus;
    private readonly Matrix3D _rot1 = Matrix3D.MakeRotation(2, 0, 1, 1);
    private readonly Matrix3D _rot2 = Matrix3D.MakeRotation(1, 1, 1, 0);

    public override string Name => "Knot";

    public KnotDemo()
    {
        _scene.CameraOrigin = new Point3D(0, 0, -4);
        _scene.SetGlobalLight(new Vector3D(0.2f, 0.8f, 0.4f));

        _knot = _scene.Root.NewChild();
        _knot.AddShape(ShapeBuilder.Tube(ShapeBuilder.Knot, 48, 12, 0.45f));
        _knot.SetWireframeMode(1);
        _knot.SetWireframeColor(true);
        _knot.SetColorBias(-1);

        _torus = _scene.Root.NewChild();
        _torus.AddShape(ShapeBuilder.Tube(ShapeBuilder.Torus, 16, 16, 0.5f, (i, j) => (i + j) % 2 - 0.5f, quads: true));
    }

    public override void Update()
    {
        _knot.AddTransform(_rot1);
        _torus.AddTransform(_rot2);
        _scene.Draw();
    }
}

/// <summary>knot-ordered.lua: depth sorting with an ordering table instead of the z-buffer.</summary>
public sealed class OrderedKnotDemo : Demo3D
{
    private readonly Scene3D _scene = new Scene3D();
    private readonly Scene3DNode _knot;
    private readonly Matrix3D _rot = Matrix3D.MakeRotation(2, 0, 1, 1);

    public override string Name => "Knot (ordering table)";

    public OrderedKnotDemo()
    {
        _scene.CameraOrigin = new Point3D(0, 0, -4);
        _scene.SetGlobalLight(new Vector3D(0.2f, 0.8f, 0.4f));
        _scene.Root.ScaleBy(-1, 1, 1);

        Shape3D shape = ShapeBuilder.Tube(ShapeBuilder.Knot, 48, 16, 0.3f, (i, j) => 0.5f - (i % 2));
        shape.OrderTableSize = 50;

        _knot = _scene.Root.NewChild();
        _knot.AddShape(shape);
        _knot.SetUsesZBuffer(false);
    }

    public override void Update()
    {
        _knot.AddTransform(_rot);
        _scene.Draw();
    }
}

/// <summary>ztest.lua: a square rotating through another square to exercise the z-buffer.</summary>
public sealed class ZTestDemo : Demo3D
{
    private readonly Scene3D _scene = new Scene3D();
    private readonly Scene3DNode _spinning;
    private readonly Matrix3D _rot = Matrix3D.MakeRotation(1, 0, 1, 0);
    private float _roll;

    public override string Name => "Z test";

    public ZTestDemo()
    {
        _scene.CameraOrigin = new Point3D(0, 0, 4);
        _scene.SetGlobalLight(new Vector3D(0.2f, 0.8f, 0.4f));

        _scene.Root.NewChild().AddShape(ShapeBuilder.Square(2));
        _spinning = _scene.Root.NewChild();
        _spinning.AddShape(ShapeBuilder.Square(1, -0.5f));
    }

    public override void Update()
    {
        // up/down roll the scene forward and backwards
        if (Sys.JustPressed(Buttons.Up))
            _roll -= 5;
        if (Sys.JustPressed(Buttons.Down))
            _roll += 5;

        _scene.Root.SetTransform(Matrix3D.MakeRotation(_roll, 1, 0, 0));
        _spinning.AddTransform(_rot);
        _scene.Draw();
    }
}
