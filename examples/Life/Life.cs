// Port of the Playdate SDK's C_API/Examples/Life.
//
// Game of Life:
//  Any live cell with fewer than two live neighbours dies, as if caused by under-population.
//  Any live cell with two or three live neighbours lives on to the next generation.
//  Any live cell with more than three live neighbours dies, as if by overcrowding.
//  Any dead cell with exactly three live neighbours becomes a live cell, as if by reproduction.
//
// Cells are pixels of the 1-bit frame buffer (0 = black = alive).

using System.Runtime.CompilerServices;

namespace Life;

public sealed unsafe class LifeGame : Game
{
    private const int Columns = PD.LCD_COLUMNS;
    private const int Rows = PD.LCD_ROWS;
    private const int RowSize = PD.LCD_ROWSIZE;

    private readonly Random _random = new Random();

#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new LifeGame());
#endif

    protected override void Initialize()
    {
        Display.SetRefreshRate(0); // run as fast as possible
        Randomize();
    }

    private void Randomize()
    {
        byte* frame = PD.Graphics->getDisplayFrame();
        for (int y = 0; y < Rows; ++y)
        {
            byte* row = &frame[y * RowSize];
            for (int x = 0; x < Columns / 8; ++x)
                row[x] = (byte)_random.Next(256);
        }
    }

    private static bool IsOn(byte* row, int x) => (row[x / 8] & (0x80 >> (x % 8))) == 0;

    private static int Val(byte* row, int x) => 1 - ((row[x / 8] >> (7 - (x % 8))) & 1);

    private static int RowSum(byte* row, int x)
    {
        if (x == 0)
            return Val(row, Columns - 1) + Val(row, x) + Val(row, x + 1);
        else if (x < Columns - 1)
            return Val(row, x - 1) + Val(row, x) + Val(row, x + 1);
        else
            return Val(row, x - 1) + Val(row, x) + Val(row, 0);
    }

    private static int MiddleRowSum(byte* row, int x)
    {
        if (x == 0)
            return Val(row, Columns - 1) + Val(row, x + 1);
        else if (x < Columns - 1)
            return Val(row, x - 1) + Val(row, x + 1);
        else
            return Val(row, x - 1) + Val(row, 0);
    }

    private static void DoRow(byte* rowAbove, byte* row, byte* rowBelow, byte* outRow)
    {
        int b = 0;
        int bitPos = 0x80;

        for (int x = 0; x < Columns; ++x)
        {
            // If total is 3 cell is alive; if total is 4, no change; else, cell is dead.
            int sum = RowSum(rowAbove, x) + MiddleRowSum(row, x) + RowSum(rowBelow, x);

            if (sum == 3 || (IsOn(row, x) && sum == 2))
                b |= bitPos;

            bitPos >>= 1;

            if (bitPos == 0)
            {
                outRow[x / 8] = (byte)~b;
                b = 0;
                bitPos = 0x80;
            }
        }
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            Randomize();

        byte* nextFrame = PD.Graphics->getFrame();     // working buffer
        byte* frame = PD.Graphics->getDisplayFrame();  // buffer currently on screen (or headed there, anyway)

        if (frame != null)
        {
            byte* rowAbove = &frame[RowSize * (Rows - 1)];
            byte* row = frame;
            byte* rowBelow = &frame[RowSize];

            for (int y = 0; y < Rows; ++y)
            {
                DoRow(rowAbove, row, rowBelow, &nextFrame[y * RowSize]);

                rowAbove = row;
                row = rowBelow;
                rowBelow = &frame[((y + 2) % Rows) * RowSize];
            }
        }

        // we twiddled the framebuffer bits directly, so we have to tell the system about it
        Graphics.MarkUpdatedRows(0, Rows);
        return true;
    }
}
