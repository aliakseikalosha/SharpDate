// Port of mini3d/shape.{h,c} from the Playdate SDK's C 3D library example.
// Reference counting is replaced by the garbage collector.

namespace Mini3D;

public struct Face3D
{
    public ushort P1;
    public ushort P2;
    public ushort P3;
    public ushort P4; // 0xffff if it's a tri
    public float ColorBias;
}

public sealed class Shape3D
{
    public int NPoints;
    public Point3D[] Points = System.Array.Empty<Point3D>();
    public int NFaces;
    public Face3D[] Faces = System.Array.Empty<Face3D>();
    public Point3D Center; // used for z-sorting entire shapes at a time
    public float ColorBias;
    public bool IsClosed;
    public int OrderTableSize;

    private int AddPoint(in Point3D p)
    {
        for (int i = 0; i < NPoints; ++i)
            if (p.Equals(Points[i]))
                return i;

        if (NPoints == Points.Length)
            System.Array.Resize(ref Points, Points.Length == 0 ? 8 : Points.Length * 2);
        Points[NPoints] = p;
        return NPoints++;
    }

    /// <summary>Adds a triangle (d == null) or quad.</summary>
    public void AddFace(in Point3D a, in Point3D b, in Point3D c, Point3D? d, float colorBias)
    {
        if (NFaces == Faces.Length)
            System.Array.Resize(ref Faces, Faces.Length == 0 ? 8 : Faces.Length * 2);

        ref Face3D face = ref Faces[NFaces];
        face.P1 = (ushort)AddPoint(a);
        face.P2 = (ushort)AddPoint(b);
        face.P3 = (ushort)AddPoint(c);
        face.P4 = d.HasValue ? (ushort)AddPoint(d.Value) : (ushort)0xffff;
        face.ColorBias = colorBias;

        ++NFaces;

        if (d.HasValue)
        {
            Point3D dv = d.Value;
            Center.X += (a.X + b.X + c.X + dv.X) / 4 / NFaces;
            Center.Y += (a.Y + b.Y + c.Y + dv.Y) / 4 / NFaces;
            Center.Z += (a.Z + b.Z + c.Z + dv.Z) / 4 / NFaces;
        }
        else
        {
            Center.X += (a.X + b.X + c.X) / 3 / NFaces;
            Center.Y += (a.Y + b.Y + c.Y) / 3 / NFaces;
            Center.Z += (a.Z + b.Z + c.Z) / 3 / NFaces;
        }
    }
}
