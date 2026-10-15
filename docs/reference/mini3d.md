# Mini3D

A small 3D engine in pure C#, in [`examples/Mini3D`](../../examples/Mini3D): vector and matrix math, a
scene graph, and a 1-bit rasterizer with lighting, dithering, a z-buffer and ordering tables. It is a
port of the C "3D library" example from the Playdate SDK (`mini3d`). `examples/Lib3DCSharp` uses it
from C#; `examples/Lib3D` exposes it to Lua like the SDK example does.

It is a library project, not part of SharpDate itself. To use it in a game, reference it:

<!-- no-compile -->
```xml
<ItemGroup>
  <ProjectReference Include="path/to/SharpDate/examples/Mini3D/Mini3D.csproj" />
</ItemGroup>
```

and add `using Mini3D;`. Lib3DCSharp renders its scenes at about 16 fps on the device.

## Coordinates

World coordinates are in arbitrary units; the default camera sits at (0, 0, -1) looking along +z. With the default scale,
the visible area at distance 1 from the camera is about x -1.66..1.66, y -1..1. Angles are degrees.
Shading comes from one directional light; faces facing it are white, faces facing away are black, with
33 dither levels in between.

## Types

| Type | What it is |
|---|---|
| `Point3D` (`X`, `Y`, `Z`) | a position; `AddVector(v)` |
| `Vector3D` (`Dx`, `Dy`, `Dz`) | a direction; `Cross`, `Dot`, `Length`, `LengthSquared`, `Normalize()`, `PNormal(p1, p2, p3)` (normal of a triangle) |
| `Matrix3D` | rotation/scale (3x3) plus translation; `Identity`, `MakeTranslate(dx, dy, dz)`, `MakeRotation(degrees, axisX, axisY, axisZ)`, `Make(...)`, `Multiply(left, right)`, `Apply(m, point)`, `Determinant` |
| `Shape3D` | a mesh: points and triangle/quad faces (`AddFace`), `ColorBias`, `IsClosed`, `OrderTableSize` |
| `ShapeBuilder` | ready-made shapes: `Icosahedron()`, `Square(halfSize)`, `Tube(curve, steps, spokes, thickness, colorFunction, quads)` with the curves `Knot` and `Torus` |
| `Scene3DNode` | a node of the scene tree: a transform, child nodes and shape instances, plus render settings inherited by children |
| `Scene3D` | the scene: `Root` node, camera, light, projection; `Draw()` renders into the frame buffer |
| `RenderStyle` | flags: `Filled`, `Wireframe`, `WireframeBack`, `WireframeWhite` (`InheritStyle` = use the parent's) |
| `Render` | the low-level rasterizer (`FillTriangle`, `FillQuad`, `DrawLine` and z-buffered variants) for drawing into any 1-bit buffer |

## Scene3D

| Member | Meaning |
|---|---|
| `Root` | the top node; add children with `Root.NewChild()` |
| `CameraOrigin`, `CameraTarget`, `CameraUp`, `CameraScale` | where the camera is, what it looks at, which way is up (default (0, -1, 0)), zoom (default 1) |
| `SetCamera(origin, lookAt, scale, up)` | all camera settings at once |
| `SetGlobalLight(direction)` | direction the light comes from, e.g. (0.2, 0.8, 0.4) |
| `SetCenter(x, y)` | where the vanishing point is on screen, 0-1 (default 0.5, 0.5 = middle) |
| `Scale` | pixels per world unit at distance 1 (default 120) |
| `HasPerspective` | false for an orthographic projection |
| `Draw()` | renders the whole scene into the frame buffer (clear the screen first) |
| `Draw(buffer, rowStride)` | renders into any 400x240 1-bit buffer, e.g. a bitmap's data |

## Scene3DNode

| Member | Meaning |
|---|---|
| `NewChild()` | a child node; its transform is relative to this node |
| `AddShape(shape)`, `AddShape(shape, offset)`, `AddShape(shape, transform)` | place an instance of a shape in this node; one shape can be added many times |
| `SetTransform(m)`, `AddTransform(m)` | replace / combine the node's transform. A rotation turns the node's contents around the node's origin, so shapes added with an offset orbit; to spin in place, rotate a child node and position its parent |
| `TranslateBy(dx, dy, dz)`, `ScaleBy(s)`, `ScaleBy(sx, sy, sz)` | shortcuts (negative scale mirrors) |
| `SetColorBias(b)` | brighten (positive) or darken (negative) everything in the node, about -1 to 1 |
| `SetFilled(bool)` | draw faces filled (default) |
| `SetWireframeMode(n)` | 0 = no wireframe, 1 = edges of front faces, 2 = edges of all faces |
| `SetWireframeColor(white)` | wireframe in white (true) or black |
| `SetVisible(bool)` | hide the node and its children |
| `SetUsesZBuffer(bool)` | per-pixel depth test (default) or sorting faces with the shape's ordering table (faster, less exact) |

## Example: a spinning, crank-controlled scene

```csharp
using Mini3D;

public sealed class SpinningShapes : Game
{
    private readonly Scene3D _scene = new Scene3D();
    private Scene3DNode _spinner, _cube;
    private readonly Matrix3D _spin = Matrix3D.MakeRotation(3, 0, 1, 0);   // 3 degrees around y per frame

    protected override void Initialize()
    {
        Display.RefreshRate = 0;                                   // render as fast as possible

        // Camera 4 units in front of the origin, looking at it.
        _scene.CameraOrigin = new Point3D(0, 0, -4);
        _scene.SetGlobalLight(new Vector3D(0.2f, 0.8f, 0.4f));

        // An icosahedron, filled, with white front edges. To spin something in place, put its
        // position in a parent node and rotate a child whose shape sits at the child's origin:
        // rotating a node turns everything in it around the node's origin.
        Scene3DNode left = _scene.Root.NewChild();
        left.TranslateBy(-1.2f, 0, 0);
        _spinner = left.NewChild();
        _spinner.AddShape(ShapeBuilder.Icosahedron());
        _spinner.SetWireframeMode(1);
        _spinner.SetWireframeColor(true);

        // A hand-made cube: six quads, corners listed in order around each face.
        Scene3DNode right = _scene.Root.NewChild();
        right.TranslateBy(1.2f, 0, 0);
        _cube = right.NewChild();
        _cube.AddShape(MakeCube(0.6f));
        _cube.SetColorBias(0.3f);                                  // a bit lighter
    }

    private static Shape3D MakeCube(float h)
    {
        var p = new Point3D[8];
        for (int i = 0; i < 8; i++)
            p[i] = new Point3D((i & 1) != 0 ? h : -h, (i & 2) != 0 ? h : -h, (i & 4) != 0 ? h : -h);

        var cube = new Shape3D();
        cube.AddFace(p[0], p[1], p[3], p[2], 0);       // back   (z = -h)
        cube.AddFace(p[4], p[5], p[7], p[6], 0);       // front  (z = +h)
        cube.AddFace(p[0], p[1], p[5], p[4], 0);       // bottom
        cube.AddFace(p[2], p[3], p[7], p[6], 0);       // top
        cube.AddFace(p[0], p[2], p[6], p[4], 0);       // left
        cube.AddFace(p[1], p[3], p[7], p[5], -0.2f);   // right, a bit darker
        // IsClosed stays false: both sides of every face are drawn, so the corner order doesn't
        // matter. Set it to true (with consistent outward winding) to skip hidden faces.
        return cube;
    }

    public override bool Update()
    {
        _spinner.AddTransform(_spin);                              // keep spinning
        _cube.AddTransform(Matrix3D.MakeRotation(Sys.CrankChange, 1, 0, 0));   // the crank tilts the cube

        // Zoom with up/down.
        if (Sys.IsPressed(Buttons.Up)) _scene.CameraScale *= 1.02f;
        if (Sys.IsPressed(Buttons.Down)) _scene.CameraScale /= 1.02f;

        Graphics.Clear(SolidColor.Black);
        _scene.Draw();
        Sys.DrawFPS(0, 0);
        return true;
    }
}
```

## Tubes and other generated shapes

`ShapeBuilder.Tube(curve, steps, spokes, thickness, color, quads)` sweeps a ring of `spokes` points along
a curve `t → Point3D` (t from 0 to 1) in `steps` segments. `color(segment, spoke)` returns a color bias
per face, e.g. a checkerboard:

<!-- context: Scene3D scene = new Scene3D(); -->
```csharp
using Mini3D;

// A checkered torus: 16 segments, 16 spokes, tube radius 0.5, quads instead of triangles.
Shape3D torus = ShapeBuilder.Tube(ShapeBuilder.Torus, 16, 16, 0.5f, (i, j) => (i + j) % 2 - 0.5f, quads: true);
scene.Root.NewChild().AddShape(torus);

// Your own curve: a helix.
Shape3D spring = ShapeBuilder.Tube(t => new Point3D(MathF.Cos(t * 20), MathF.Sin(t * 20), t * 4 - 2), 120, 6, 0.1f);
scene.Root.NewChild().AddShape(spring);
```

For shapes with many faces, `shape.OrderTableSize = 50` plus `node.SetUsesZBuffer(false)` sorts faces
into 50 depth buckets instead of testing every pixel — faster, with occasional sorting errors on
intersecting faces.
