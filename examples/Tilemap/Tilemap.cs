// Port of the Playdate SDK's C_API/Examples/Tilemap.

using System.Runtime.CompilerServices;

namespace Tilemap;

public sealed class TilemapGame : Game
{
    private readonly Random _random = new Random();
    private TileMap _tilemap;

#if !SHARPDATE_DEMOS // the Demos launcher starts this game itself
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new TilemapGame());
#endif

    protected override void Initialize()
    {
        _tilemap = TileMap.Create();
        _tilemap.SetSize(20, 12);
        _tilemap.ImageTable = BitmapTable.Load("font");
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);

        _tilemap.SetTile(_random.Next(20), _random.Next(12), (ushort)_random.Next(200));
        _tilemap.Draw(0, 0);

        return true;
    }
}
