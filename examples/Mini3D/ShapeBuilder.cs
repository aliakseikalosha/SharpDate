// Shape generators. Tube() is a port of makeShape() from knot.lua in the Playdate SDK's C 3D
// library example; Icosahedron() follows icosahedra.lua.

namespace Mini3D;

public static class ShapeBuilder
{
    /// <summary>A parametric curve: t in [0, 1) -> point.</summary>
    public delegate Point3D Curve(float t);

    /// <summary>Face color bias for (segment, spoke); 0 = plain lighting.</summary>
    public delegate float ColorFunction(int segment, int spoke);

    private const float TwoPi = 2 * MathF.PI;

    /// <summary>Trefoil knot curve.</summary>
    public static Point3D Knot(float t)
    {
        t *= TwoPi;
        return new Point3D(
            (MathF.Sin(t) + 2 * MathF.Sin(2 * t)) / 3,
            (MathF.Cos(t) - 2 * MathF.Cos(2 * t)) / 3,
            MathF.Sin(3 * t) / 2);
    }

    /// <summary>Unit circle in the XY plane.</summary>
    public static Point3D Torus(float t)
    {
        t *= TwoPi;
        return new Point3D(MathF.Sin(t), MathF.Cos(t), 0);
    }

    /// <summary>
    /// Sweeps a ring of <paramref name="spokes"/> points along <paramref name="curve"/> to make a closed
    /// tube, using quads or (staggered) triangles.
    /// </summary>
    public static Shape3D Tube(Curve curve, int steps, int spokes, float thickness, ColorFunction color = null, bool quads = false)
    {
        var shape = new Shape3D();
        const float d = 0.01f;

        Point3D[] MakeRing(int step)
        {
            float t = (float)step / steps;
            Point3D c = curve(t);

            // direction of the curve at this point
            Point3D m = curve(t - d);
            Point3D p = curve(t + d);
            float vx = p.X - m.X, vy = p.Y - m.Y, vz = p.Z - m.Z;
            float s = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
            vx /= s;
            vy /= s;
            vz /= s;

            float r = MathF.Sqrt(vx * vx + vy * vy);
            var ring = new Point3D[spokes];

            // ring of points around the curve point, perpendicular to v
            for (int i = 0; i < spokes; i++)
            {
                float k = i;
                if (!quads)
                    k += (step % 2) / 2f;
                float st = TwoPi * k / spokes;
                ring[i] = new Point3D(
                    c.X - thickness * (MathF.Sin(st) * vy + MathF.Cos(st) * vx * vz) / r,
                    c.Y + thickness * (MathF.Sin(st) * vx - MathF.Cos(st) * vy * vz) / r,
                    c.Z + thickness * MathF.Cos(st) * r);
            }
            return ring;
        }

        void AddRing(Point3D[] p1, Point3D[] p2, int s)
        {
            int n = p1.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (quads)
                {
                    float c = color?.Invoke(s, i + 1) ?? 0;
                    shape.AddFace(p1[i], p2[i], p2[j], p1[j], c);
                }
                else
                {
                    float c1 = color?.Invoke(2 * s, i + 1) ?? 0;
                    float c2 = color?.Invoke(2 * s + 1, i + 1) ?? 0;
                    if (s % 2 == 0)
                    {
                        shape.AddFace(p1[i], p2[i], p1[j], null, c1);
                        shape.AddFace(p2[i], p2[j], p1[j], null, c2);
                    }
                    else
                    {
                        shape.AddFace(p1[i], p2[i], p2[j], null, c2);
                        shape.AddFace(p1[i], p2[j], p1[j], null, c1);
                    }
                }
            }
        }

        Point3D[] ring0 = MakeRing(0);
        Point3D[] ring = ring0;
        for (int i = 0; i < steps - 1; i++)
        {
            Point3D[] next = MakeRing(i + 1);
            AddRing(ring, next, i);
            ring = next;
        }
        AddRing(ring, ring0, steps - 1);

        shape.IsClosed = true;
        return shape;
    }

    /// <summary>A unit icosahedron with outward-facing triangles.</summary>
    public static Shape3D Icosahedron()
    {
        float p = (MathF.Sqrt(5) - 1) / 2;

        var x1 = new Point3D(0, -p, 1);
        var x2 = new Point3D(0, p, 1);
        var x3 = new Point3D(0, p, -1);
        var x4 = new Point3D(0, -p, -1);
        var y1 = new Point3D(1, 0, p);
        var y2 = new Point3D(1, 0, -p);
        var y3 = new Point3D(-1, 0, -p);
        var y4 = new Point3D(-1, 0, p);
        var z1 = new Point3D(-p, 1, 0);
        var z2 = new Point3D(p, 1, 0);
        var z3 = new Point3D(p, -1, 0);
        var z4 = new Point3D(-p, -1, 0);

        var shape = new Shape3D();
        void Tri(Point3D a, Point3D b, Point3D c) => shape.AddFace(a, b, c, null, 0);

        Tri(z1, y3, y4); Tri(z1, x3, y3); Tri(z1, z2, x3); Tri(z1, x2, z2); Tri(z1, y4, x2);
        Tri(y4, y3, z4); Tri(z4, y3, x4); Tri(y3, x3, x4); Tri(x4, x3, y2); Tri(x3, z2, y2);
        Tri(y2, z2, y1); Tri(z2, x2, y1); Tri(y1, x2, x1); Tri(x2, y4, x1); Tri(x1, y4, z4);
        Tri(z3, y2, y1); Tri(z3, y1, x1); Tri(z3, x1, z4); Tri(z3, z4, x4); Tri(z3, x4, y2);

        shape.IsClosed = true;
        return shape;
    }

    /// <summary>A flat square of side 2*halfSize in the XY plane (single quad).</summary>
    public static Shape3D Square(float halfSize, float colorBias = 0)
    {
        var shape = new Shape3D();
        shape.AddFace(new Point3D(-halfSize, -halfSize, 0), new Point3D(halfSize, -halfSize, 0),
                      new Point3D(halfSize, halfSize, 0), new Point3D(-halfSize, halfSize, 0), colorBias);
        return shape;
    }
}
