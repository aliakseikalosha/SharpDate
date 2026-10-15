// Port of mini3d/render.{h,c} from the Playdate SDK's C 3D library example: scanline rasterizer
// for the 1-bit frame buffer, with an optional 16-bit z-buffer. Float-to-int conversions follow
// the C code (truncation) so the output matches the original.

using System.Runtime.InteropServices;

namespace Mini3D;

public struct RowRange
{
    public short Start;
    public short End;

    public RowRange(float start, float end)
    {
        Start = (short)start;
        End = (short)end;
    }
}

public static unsafe class Render
{
    private const int Rows = 240;
    private const int Columns = 400;
    private const float ZBias = 0;

    private static ushort* s_zbuf;
    private static float s_zscale;

    public static void ResetZBuffer(float zmin)
    {
        if (s_zbuf == null)
            s_zbuf = (ushort*)NativeMemory.Alloc((nuint)(Columns * Rows * sizeof(ushort)));
        NativeMemory.Clear(s_zbuf, (nuint)(Columns * Rows * sizeof(ushort)));
        s_zscale = 0xffff * (zmin + ZBias);
    }

    private static uint Swap(uint n)
        => ((n & 0xff000000) >> 24) | ((n & 0xff0000) >> 8) | ((n & 0xff00) << 8) | (n << 24);

    private static void DrawMaskPattern(uint* p, uint mask, uint color)
    {
        if (mask == 0xffffffff)
            *p = color;
        else
            *p = (*p & ~mask) | (color & mask);
    }

    private static uint PatternColor(byte* pattern, int y)
    {
        uint p = pattern[y % 8];
        return (p << 24) | (p << 16) | (p << 8) | p;
    }

    private static void DrawFragment(uint* row, int x1, int x2, uint color)
    {
        if (x2 < 0 || x1 >= Columns)
            return;
        if (x1 < 0)
            x1 = 0;
        if (x2 > Columns)
            x2 = Columns;
        if (x1 > x2)
            return;

        // Operate on 32 bits at a time
        int startbit = x1 % 32;
        uint startmask = Swap((uint)((1L << (32 - startbit)) - 1));
        int endbit = x2 % 32;
        uint endmask = Swap((uint)(((1L << endbit) - 1) << (32 - endbit)));

        int col = x1 / 32;
        uint* p = row + col;

        if (col == x2 / 32)
        {
            uint mask = 0;
            if (startbit > 0 && endbit > 0)
                mask = startmask & endmask;
            else if (startbit > 0)
                mask = startmask;
            else if (endbit > 0)
                mask = endmask;
            DrawMaskPattern(p, mask, color);
        }
        else
        {
            int x = x1;
            if (startbit > 0)
            {
                DrawMaskPattern(p++, startmask, color);
                x += 32 - startbit;
            }
            while (x + 32 <= x2)
            {
                DrawMaskPattern(p++, 0xffffffff, color);
                x += 32;
            }
            if (endbit > 0)
                DrawMaskPattern(p, endmask, color);
        }
    }

    private static void DrawFragmentZBuf(uint* row, ushort* zbrow, int x, int endx, uint z, int dzdx, uint color)
    {
        if (endx < 0 || x >= Columns)
            return;
        if (x < 0)
        {
            z += (uint)(-x * dzdx);
            x = 0;
        }
        if (endx > Columns)
            endx = Columns;

        uint mask = 0;
        uint* p = row + x / 32;

        while (x < endx)
        {
            ushort zi = (ushort)(z >> 16);
            if (zi > zbrow[x])
            {
                mask |= 0x80000000u >> (x % 32);
                zbrow[x] = zi;
            }
            ++x;
            z += (uint)dzdx;
            if (x % 32 == 0)
            {
                DrawMaskPattern(p++, Swap(mask), color);
                mask = 0;
            }
        }

        if (x % 32 != 0)
            DrawMaskPattern(p, Swap(mask), color);
    }

    private static int Slope(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        if (dy < 1)
            return (int)(dx * (1 << 16));
        return (int)(dx / dy * (1 << 16));
    }

    private static float Min(float a, float b) => a < b ? a : b;
    private static float Max(float a, float b) => a > b ? a : b;

    public static RowRange DrawLineZBuf(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, int thick, byte* pattern)
    {
        if (p1->Y > p2->Y)
        {
            Point3D* tmp = p1;
            p1 = p2;
            p2 = tmp;
        }

        int y = (int)p1->Y;
        int endy = (int)p2->Y;

        if (y >= Rows || endy < 0 || Min(p1->X, p2->X) >= Columns || Max(p1->X, p2->X) < 0)
            return default;

        int x = (int)(p1->X * (1 << 16));
        int dx = Slope(p1->X, p1->Y, p2->X, p2->Y + 1);
        float py = p1->Y;

        // move lines a bit forward so they don't get buried in solid geometry
        float z1 = s_zscale / (p1->Z + ZBias) + 256;
        float z2 = s_zscale / (p2->Z + ZBias) + 256;
        if (z1 > 65535) z1 = 65535;
        if (z2 > 65535) z2 = 65535;

        int dzdy = Slope(z1, p1->Y, z2, p2->Y + 1);
        int dzdx = Slope(z1, p1->X, z2, p2->X);

        uint z = (uint)(z1 * (1 << 16));

        if (y < 0)
        {
            x += -y * dx;
            z += (uint)(-y * dzdy);
            y = 0;
            py = 0;
        }

        int x1 = (int)(x + dx * (y + 1 - py));

        while (y <= endy)
        {
            uint color = PatternColor(pattern, y);

            if (y == endy)
                x1 = (int)(p2->X * (1 << 16));

            if (dx < 0)
            {
                z += (uint)dzdy;
                DrawFragmentZBuf((uint*)&bitmap[y * rowstride], &s_zbuf[y * Columns], x1 >> 16, (x >> 16) + thick, z, dzdx, color);
            }
            else
            {
                DrawFragmentZBuf((uint*)&bitmap[y * rowstride], &s_zbuf[y * Columns], x >> 16, (x1 >> 16) + thick, z, dzdx, color);
                z += (uint)dzdy;
            }

            if (++y == Rows)
                break;

            x = x1;
            x1 += dx;
        }

        return new RowRange(Max(0, p1->Y), Min(Rows, p2->Y));
    }

    public static RowRange DrawLine(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, int thick, byte* pattern)
    {
        if (p1->Y > p2->Y)
        {
            Point3D* tmp = p1;
            p1 = p2;
            p2 = tmp;
        }

        int y = (int)p1->Y;
        int endy = (int)p2->Y;

        if (y >= Rows || endy < 0 || Min(p1->X, p2->X) >= Columns || Max(p1->X, p2->X) < 0)
            return default;

        int x = (int)(p1->X * (1 << 16));
        int dx = Slope(p1->X, p1->Y, p2->X, p2->Y);
        float py = p1->Y;

        if (y < 0)
        {
            x = (int)(x + -p1->Y * dx);
            y = 0;
            py = 0;
        }

        int x1 = (int)(x + dx * (y + 1 - py));

        while (y <= endy)
        {
            uint color = PatternColor(pattern, y);

            if (y == endy)
                x1 = (int)(p2->X * (1 << 16));

            if (dx < 0)
                DrawFragment((uint*)&bitmap[y * rowstride], x1 >> 16, (x >> 16) + thick, color);
            else
                DrawFragment((uint*)&bitmap[y * rowstride], x >> 16, (x1 >> 16) + thick, color);

            if (++y == Rows)
                break;

            x = x1;
            x1 += dx;
        }

        return new RowRange(Max(0, p1->Y), Min(Rows, p2->Y));
    }

    private static void FillRange(byte* bitmap, int rowstride, int y, int endy, ref int x1p, int dx1, ref int x2p, int dx2, byte* pattern)
    {
        int x1 = x1p, x2 = x2p;

        if (endy < 0)
        {
            int dy = endy - y;
            x1p = x1 + dy * dx1;
            x2p = x2 + dy * dx2;
            return;
        }

        if (y < 0)
        {
            x1 += -y * dx1;
            x2 += -y * dx2;
            y = 0;
        }

        while (y < endy)
        {
            DrawFragment((uint*)&bitmap[y * rowstride], x1 >> 16, (x2 >> 16) + 1, PatternColor(pattern, y));
            x1 += dx1;
            x2 += dx2;
            ++y;
        }

        x1p = x1;
        x2p = x2;
    }

    private static void FillRangeZBuf(byte* bitmap, int rowstride, int y, int endy, ref int x1p, int dx1, ref int x2p, int dx2, ref uint zp, int dzdy, int dzdx, byte* pattern)
    {
        int x1 = x1p, x2 = x2p;
        uint z = zp;

        if (endy < 0)
        {
            int dy = endy - y;
            x1p = x1 + dy * dx1;
            x2p = x2 + dy * dx2;
            zp = z + (uint)(dy * dzdy);
            return;
        }

        if (y < 0)
        {
            x1 += -y * dx1;
            x2 += -y * dx2;
            z += (uint)(-y * dzdy);
            y = 0;
        }

        while (y < endy)
        {
            DrawFragmentZBuf((uint*)&bitmap[y * rowstride], &s_zbuf[y * Columns], x1 >> 16, (x2 >> 16) + 1, z, dzdx, PatternColor(pattern, y));
            x1 += dx1;
            x2 += dx2;
            z += (uint)dzdy;
            ++y;
        }

        x1p = x1;
        x2p = x2;
        zp = z;
    }

    private static void SortTri(ref Point3D* p1, ref Point3D* p2, ref Point3D* p3)
    {
        float y1 = p1->Y, y2 = p2->Y, y3 = p3->Y;

        if (y1 <= y2 && y1 < y3)
        {
            if (y3 < y2) // 1,3,2
            {
                Point3D* tmp = p2;
                p2 = p3;
                p3 = tmp;
            }
        }
        else if (y2 < y1 && y2 < y3)
        {
            Point3D* tmp = p1;
            p1 = p2;
            if (y3 < y1) // 2,3,1
            {
                p2 = p3;
                p3 = tmp;
            }
            else // 2,1,3
            {
                p2 = tmp;
            }
        }
        else
        {
            Point3D* tmp = p1;
            p1 = p3;
            if (y1 < y2) // 3,1,2
            {
                p3 = p2;
                p2 = tmp;
            }
            else // 3,2,1
            {
                p3 = tmp;
            }
        }
    }

    public static RowRange FillTriangle(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, Point3D* p3, byte* pattern)
    {
        // sort by y coord
        SortTri(ref p1, ref p2, ref p3);

        int endy = (int)Min(Rows, p3->Y);

        if (p1->Y > Rows || endy < 0)
            return default;

        int x1 = (int)(p1->X * (1 << 16));
        int x2 = x1;

        int sb = Slope(p1->X, p1->Y, p2->X, p2->Y);
        int sc = Slope(p1->X, p1->Y, p3->X, p3->Y);

        int dx1 = Math.Min(sb, sc);
        int dx2 = Math.Max(sb, sc);

        FillRange(bitmap, rowstride, (int)p1->Y, (int)Min(Rows, p2->Y), ref x1, dx1, ref x2, dx2, pattern);

        int dx = Slope(p2->X, p2->Y, p3->X, p3->Y);

        if (sb < sc)
        {
            x1 = (int)(p2->X * (1 << 16));
            FillRange(bitmap, rowstride, (int)p2->Y, endy, ref x1, dx, ref x2, dx2, pattern);
        }
        else
        {
            x2 = (int)(p2->X * (1 << 16));
            FillRange(bitmap, rowstride, (int)p2->Y, endy, ref x1, dx1, ref x2, dx, pattern);
        }

        return new RowRange(Max(0, p1->Y), endy);
    }

    public static RowRange FillTriangleZBuf(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, Point3D* p3, byte* pattern)
    {
        SortTri(ref p1, ref p2, ref p3);

        int endy = (int)Min(Rows, p3->Y);
        int det = (int)((p3->X - p1->X) * (p2->Y - p1->Y) - (p2->X - p1->X) * (p3->Y - p1->Y));

        if (p1->Y > Rows || endy < 0 || det == 0)
            return default;

        int x1 = (int)(p1->X * (1 << 16));
        int x2 = x1;

        int sb = Slope(p1->X, p1->Y, p2->X, p2->Y);
        int sc = Slope(p1->X, p1->Y, p3->X, p3->Y);

        int dx1 = Math.Min(sb, sc);
        int dx2 = Math.Max(sb, sc);

        float z1 = s_zscale / (p1->Z + ZBias);
        float z2 = s_zscale / (p2->Z + ZBias);
        float z3 = s_zscale / (p3->Z + ZBias);

        float mx = p1->X + (p2->Y - p1->Y) * (p3->X - p1->X) / (p3->Y - p1->Y);
        float mz = z1 + (p2->Y - p1->Y) * (z3 - z1) / (p3->Y - p1->Y);

        int dzdx, dzdy;

        if (sc < sb)
        {
            dzdx = Slope(mz, mx, z2, p2->X);
            dzdy = Slope(z1, p1->Y, z3, p3->Y);
        }
        else
        {
            dzdx = Slope(z2, p2->X, mz, mx);
            dzdy = Slope(z1, p1->Y, z2, p2->Y);
        }

        uint z = (uint)(z1 * (1 << 16));

        FillRangeZBuf(bitmap, rowstride, (int)p1->Y, (int)Min(Rows, p2->Y), ref x1, dx1, ref x2, dx2, ref z, dzdy, dzdx, pattern);

        int dx = Slope(p2->X, p2->Y, p3->X, p3->Y);

        if (sb < sc)
        {
            dzdy = Slope(z2, p2->Y, z3, p3->Y);
            x1 = (int)(p2->X * (1 << 16));
            z = (uint)(z2 * (1 << 16));
            FillRangeZBuf(bitmap, rowstride, (int)p2->Y, endy, ref x1, dx, ref x2, dx2, ref z, dzdy, dzdx, pattern);
        }
        else
        {
            x2 = (int)(p2->X * (1 << 16));
            FillRangeZBuf(bitmap, rowstride, (int)p2->Y, endy, ref x1, dx1, ref x2, dx, ref z, dzdy, dzdx, pattern);
        }

        return new RowRange(Max(0, p1->Y), endy);
    }

    public static RowRange FillQuad(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, Point3D* p3, Point3D* p4, byte* pattern)
    {
        FillTriangle(bitmap, rowstride, p1, p2, p3, pattern);
        return FillTriangle(bitmap, rowstride, p1, p3, p4, pattern);
    }

    public static RowRange FillQuadZBuf(byte* bitmap, int rowstride, Point3D* p1, Point3D* p2, Point3D* p3, Point3D* p4, byte* pattern)
    {
        FillTriangleZBuf(bitmap, rowstride, p1, p2, p3, pattern);
        return FillTriangleZBuf(bitmap, rowstride, p1, p3, p4, pattern);
    }
}
