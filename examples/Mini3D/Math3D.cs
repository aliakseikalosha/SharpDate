// Port of mini3d/3dmath.{h,c} from the Playdate SDK's C 3D library example.

namespace Mini3D;

public struct Point3D
{
    public float X;
    public float Y;
    public float Z;

    public Point3D(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(Point3D b) => X == b.X && Y == b.Y && Z == b.Z;

    public Point3D AddVector(Vector3D v) => new Point3D(X + v.Dx, Y + v.Dy, Z + v.Dz);
}

public struct Vector3D
{
    public float Dx;
    public float Dy;
    public float Dz;

    public Vector3D(float dx, float dy, float dz)
    {
        Dx = dx;
        Dy = dy;
        Dz = dz;
    }

    public static Vector3D Cross(Vector3D a, Vector3D b)
        => new Vector3D(a.Dy * b.Dz - a.Dz * b.Dy, a.Dz * b.Dx - a.Dx * b.Dz, a.Dx * b.Dy - a.Dy * b.Dx);

    public static float Dot(Vector3D a, Vector3D b) => a.Dx * b.Dx + a.Dy * b.Dy + a.Dz * b.Dz;

    public float LengthSquared => Dx * Dx + Dy * Dy + Dz * Dz;

    public float Length => MathF.Sqrt(LengthSquared);

    public Vector3D Normalize()
    {
        float d = MathF.Sqrt(Dx * Dx + Dy * Dy + Dz * Dz);
        return new Vector3D(Dx / d, Dy / d, Dz / d);
    }

    public static Vector3D PNormal(in Point3D p1, in Point3D p2, in Point3D p3)
        => Cross(new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z),
                 new Vector3D(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z)).Normalize();
}

/// <summary>3x3 matrix plus translation. IsIdentity means the 3x3 part is the identity.</summary>
public struct Matrix3D
{
    public bool IsIdentity;
    public bool Inverting;
    // M[row][column]
    public float M00, M01, M02;
    public float M10, M11, M12;
    public float M20, M21, M22;
    public float Dx;
    public float Dy;
    public float Dz;

    public static readonly Matrix3D Identity = MakeTranslate(0, 0, 0);

    public static Matrix3D Make(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33, bool inverting)
        => new Matrix3D
        {
            IsIdentity = false,
            Inverting = inverting,
            M00 = m11, M01 = m12, M02 = m13,
            M10 = m21, M11 = m22, M12 = m23,
            M20 = m31, M21 = m32, M22 = m33,
        };

    public static Matrix3D MakeTranslate(float dx, float dy, float dz)
        => new Matrix3D
        {
            IsIdentity = true,
            M00 = 1, M11 = 1, M22 = 1,
            Dx = dx, Dy = dy, Dz = dz,
        };

    public static Matrix3D Multiply(in Matrix3D l, in Matrix3D r)
    {
        Matrix3D m = default;
        m.Inverting = l.Inverting ^ r.Inverting;

        if (l.IsIdentity)
        {
            if (r.IsIdentity)
            {
                m = Identity;
            }
            else
            {
                m.M00 = r.M00; m.M01 = r.M01; m.M02 = r.M02;
                m.M10 = r.M10; m.M11 = r.M11; m.M12 = r.M12;
                m.M20 = r.M20; m.M21 = r.M21; m.M22 = r.M22;
            }

            m.Dx = l.Dx + r.Dx;
            m.Dy = l.Dy + r.Dy;
            m.Dz = l.Dz + r.Dz;
        }
        else
        {
            if (!r.IsIdentity)
            {
                m.M00 = l.M00 * r.M00 + l.M10 * r.M01 + l.M20 * r.M02;
                m.M10 = l.M00 * r.M10 + l.M10 * r.M11 + l.M20 * r.M12;
                m.M20 = l.M00 * r.M20 + l.M10 * r.M21 + l.M20 * r.M22;

                m.M01 = l.M01 * r.M00 + l.M11 * r.M01 + l.M21 * r.M02;
                m.M11 = l.M01 * r.M10 + l.M11 * r.M11 + l.M21 * r.M12;
                m.M21 = l.M01 * r.M20 + l.M11 * r.M21 + l.M21 * r.M22;

                m.M02 = l.M02 * r.M00 + l.M12 * r.M01 + l.M22 * r.M02;
                m.M12 = l.M02 * r.M10 + l.M12 * r.M11 + l.M22 * r.M12;
                m.M22 = l.M02 * r.M20 + l.M12 * r.M21 + l.M22 * r.M22;
            }
            else
            {
                m.M00 = l.M00; m.M01 = l.M01; m.M02 = l.M02;
                m.M10 = l.M10; m.M11 = l.M11; m.M12 = l.M12;
                m.M20 = l.M20; m.M21 = l.M21; m.M22 = l.M22;
            }

            m.Dx = l.Dx * r.M00 + l.Dy * r.M01 + l.Dz * r.M02 + r.Dx;
            m.Dy = l.Dx * r.M10 + l.Dy * r.M11 + l.Dz * r.M12 + r.Dy;
            m.Dz = l.Dx * r.M20 + l.Dy * r.M21 + l.Dz * r.M22 + r.Dz;
        }

        return m;
    }

    /// <summary>Rotation of <paramref name="angleDegrees"/> around the axis (x, y, z).</summary>
    public static Matrix3D MakeRotation(float angleDegrees, float x, float y, float z)
    {
        const float Pi = 3.14159265358979323846f;
        float c = MathF.Cos(angleDegrees * Pi / 180);
        float s = MathF.Sin(angleDegrees * Pi / 180);
        float d = MathF.Sqrt(x * x + y * y + z * z);
        x /= d;
        y /= d;
        z /= d;

        Matrix3D p = default;
        p.M00 = c + x * x * (1 - c);
        p.M01 = x * y * (1 - c) - z * s;
        p.M02 = x * z * (1 - c) + y * s;
        p.M10 = y * x * (1 - c) + z * s;
        p.M11 = c + y * y * (1 - c);
        p.M12 = y * z * (1 - c) - x * s;
        p.M20 = z * x * (1 - c) - y * s;
        p.M21 = z * y * (1 - c) + x * s;
        p.M22 = c + z * z * (1 - c);
        return p;
    }

    public static Point3D Apply(in Matrix3D m, in Point3D p)
    {
        if (m.IsIdentity)
            return new Point3D(p.X + m.Dx, p.Y + m.Dy, p.Z + m.Dz);

        float x = p.X * m.M00 + p.Y * m.M01 + p.Z * m.M02 + m.Dx;
        float y = p.X * m.M10 + p.Y * m.M11 + p.Z * m.M12 + m.Dy;
        float z = p.X * m.M20 + p.Y * m.M21 + p.Z * m.M22 + m.Dz;
        return new Point3D(x, y, z);
    }

    public float Determinant
        => M00 * M11 * M22
         + M01 * M12 * M20
         + M02 * M10 * M21
         - M20 * M11 * M02
         - M10 * M01 * M22
         - M00 * M21 * M12;
}
