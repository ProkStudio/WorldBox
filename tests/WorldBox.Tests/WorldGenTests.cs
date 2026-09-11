using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

public class WorldGenTests
{
    private const int Size = 256;

    [Fact]
    public void Одинаковый_сид_даёт_одинаковый_мир()
    {
        WorldMap first = WorldGenerator.Generate(Size, Size, 12345);
        WorldMap second = WorldGenerator.Generate(Size, Size, 12345);

        Assert.Equal(first.Checksum(), second.Checksum());
        Assert.Equal(first.SeaLevel, second.SeaLevel);
        Assert.Equal(first.LandTiles, second.LandTiles);
    }

    [Fact]
    public void Разные_сиды_дают_разные_миры()
    {
        WorldMap first = WorldGenerator.Generate(Size, Size, 1);
        WorldMap second = WorldGenerator.Generate(Size, Size, 2);

        Assert.NotEqual(first.Checksum(), second.Checksum());
    }

    [Fact]
    public void Суша_занимает_разумную_долю()
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, 777);
        float share = map.LandTiles / (float)map.TileCount;

        Assert.InRange(share, 0.20f, 0.50f);
    }

    [Fact]
    public void На_карте_не_меньше_восьми_биомов()
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            WorldMap map = WorldGenerator.Generate(Size, Size, seed);
            int distinct = map.DistinctBiomes(map.TileCount / 500);

            Assert.True(distinct >= 8, $"сид {seed}: биомов только {distinct}");
        }
    }

    [Fact]
    public void Ни_один_биом_не_занимает_больше_трети_суши()
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            WorldMap map = WorldGenerator.Generate(Size, Size, seed);
            float share = map.LargestLandBiomeShare();

            Assert.True(share <= 0.34f, $"сид {seed}: самый частый биом занял {share:P0} суши");
        }
    }

    [Fact]
    public void Вода_течёт_только_вниз()
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, 4242);
        int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                byte direction = map.FlowDirection[i];
                if (direction == 255)
                {
                    continue;
                }

                int nx = x + dx[direction];
                int ny = y + dy[direction];
                Assert.True(map.InBounds(nx, ny));
                Assert.True(map.Elevation[map.Index(nx, ny)] < map.Elevation[i]);
            }
        }
    }

    [Fact]
    public void На_карте_есть_реки_и_ресурсы()
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, 31337);

        Assert.True(map.BiomeCounts[(int)Biome.River] > 0, "рек нет вообще");
        Assert.True(map.ResourceCounts[(int)ResourceKind.Copper] > 0, "меди нет");
        Assert.True(map.ResourceCounts[(int)ResourceKind.Iron] > 0, "железа нет");
        Assert.True(map.ResourceCounts[(int)ResourceKind.None] > map.TileCount / 2, "ресурсы раскиданы слишком густо");
    }
}
