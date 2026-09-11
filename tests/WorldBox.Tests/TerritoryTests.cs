using WorldBox.Core;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Границы держав. Раньше поселение занимало только круг вокруг себя, поэтому карта была
/// россыпью цветных пятен с ничейной землёй между городами. Тесты держат новое поведение:
/// страны дорастают до общей границы, одинокий лоскут внутри чужой земли переходит хозяину
/// окружения, земля под самим городом не уходит никогда, а через воду страна не перелезает.
/// </summary>
public sealed class TerritoryTests
{
    private const int Size = 48;

    [Fact]
    public void CountriesGrowUntilTheyShareBorder()
    {
        Setup setup = Build(-1);
        short first = setup.Tribes.Create("Первые", 1, 12, 24);
        short second = setup.Tribes.Create("Вторые", 2, 36, 24);
        setup.Tribes.People[first] = 400;
        setup.Tribes.People[second] = 400;
        ClaimCore(setup, 12, 24, 2, first);
        ClaimCore(setup, 36, 24, 2, second);

        Run(setup, 400);

        int land = setup.World.Map!.LandTiles;
        Assert.True(
            setup.Territory.Claimed > land * 0.9,
            "Страны не поделили сушу: занято " + setup.Territory.Claimed + " из " + land + ".");
        Assert.True(setup.Territory.Tiles[first] > 300, "Первая страна почти не выросла.");
        Assert.True(setup.Territory.Tiles[second] > 300, "Вторая страна почти не выросла.");
        Assert.True(
            SharedBorder(setup) > 10,
            "У стран нет общей границы: между ними осталась полоса ничейной земли.");
        Assert.True(LonelyPatches(setup) == 0, "На карте остались одинокие лоскуты внутри чужой земли.");
    }

    [Fact]
    public void LonelyPatchJoinsSurroundingCountry()
    {
        Setup setup = Build(-1);
        short host = setup.Tribes.Create("Хозяева", 1, 24, 24);
        short guest = setup.Tribes.Create("Гости", 2, 24, 24);
        setup.Tribes.People[host] = 200;
        setup.Tribes.People[guest] = 200;

        for (int y = 22; y <= 26; y++)
        {
            for (int x = 22; x <= 26; x++)
            {
                setup.Territory.Claim((y * Size) + x, host);
            }
        }

        int center = (24 * Size) + 24;
        setup.Territory.Claim(center, guest);

        Run(setup, 60);

        Assert.Equal(host, setup.Territory.OwnerAt(center));
    }

    [Fact]
    public void TownGroundNeverChangesHands()
    {
        Setup setup = Build(-1);
        short host = setup.Tribes.Create("Хозяева", 1, 24, 24);
        short guest = setup.Tribes.Create("Гости", 2, 24, 24);
        setup.Tribes.People[host] = 200;
        setup.Tribes.People[guest] = 200;

        for (int y = 22; y <= 26; y++)
        {
            for (int x = 22; x <= 26; x++)
            {
                setup.Territory.Claim((y * Size) + x, host);
            }
        }

        int center = (24 * Size) + 24;
        setup.Territory.Claim(center, guest, true);

        Run(setup, 200);

        Assert.Equal(guest, setup.Territory.OwnerAt(center));
    }

    [Fact]
    public void CountryDoesNotCrossWater()
    {
        Setup setup = Build(24);
        short tribe = setup.Tribes.Create("Островитяне", 1, 12, 24);
        setup.Tribes.People[tribe] = 400;
        ClaimCore(setup, 12, 24, 2, tribe);

        Run(setup, 400);

        WorldMap map = setup.World.Map!;
        int beyond = 0;
        int wet = 0;

        for (int i = 0; i < map.TileCount; i++)
        {
            if (setup.Territory.Owner[i] == TribeStore.None)
            {
                continue;
            }

            if (!map.IsLand(i))
            {
                wet++;
            }

            if (i % Size > 24)
            {
                beyond++;
            }
        }

        Assert.True(setup.Territory.Tiles[tribe] > 300, "Страна не выросла на своём берегу.");
        Assert.True(wet == 0, "Страна заняла воду: занято " + wet + " мокрых тайлов.");
        Assert.True(beyond == 0, "Страна перелезла через море: " + beyond + " тайлов на чужом берегу.");
    }

    private sealed class Setup
    {
        public WorldState World = null!;
        public TribeStore Tribes = null!;
        public Territory Territory = null!;
        public TerritorySystem System = null!;
    }

    /// <summary>Ровная травяная карта. waterColumn меньше нуля — суша целиком.</summary>
    private static Setup Build(int waterColumn)
    {
        var map = new WorldMap(Size, Size);
        Array.Fill(map.BiomeAt, (byte)Biome.Grassland);

        if (waterColumn >= 0)
        {
            for (int y = 0; y < Size; y++)
            {
                map.BiomeAt[(y * Size) + waterColumn] = (byte)Biome.Ocean;
            }
        }

        map.Recount();

        var world = new WorldState(Size, Size, 7);
        world.SetMap(map);

        var tribes = new TribeStore();
        var territory = new Territory(Size, Size, tribes.Capacity);

        return new Setup
        {
            World = world,
            Tribes = tribes,
            Territory = territory,
            System = new TerritorySystem(tribes, territory),
        };
    }

    private static void Run(Setup setup, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            setup.System.Tick(setup.World);
        }
    }

    private static void ClaimCore(Setup setup, int centerX, int centerY, int radius, short tribe)
    {
        WorldMap map = setup.World.Map!;

        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int index = (y * Size) + x;
                if (map.IsLand(index))
                {
                    setup.Territory.Claim(index, tribe, true);
                }
            }
        }
    }

    /// <summary>Сколько раз соседние тайлы принадлежат разным державам: это и есть общая граница.</summary>
    private static int SharedBorder(Setup setup)
    {
        short[] owner = setup.Territory.Owner;
        int count = 0;

        for (int y = 0; y < Size; y++)
        {
            int row = y * Size;
            for (int x = 0; x < Size - 1; x++)
            {
                short left = owner[row + x];
                short right = owner[row + x + 1];
                if (left != TribeStore.None && right != TribeStore.None && left != right)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Тайлы, у которых все четыре соседа принадлежат одному чужому народу.</summary>
    private static int LonelyPatches(Setup setup)
    {
        short[] owner = setup.Territory.Owner;
        byte[] core = setup.Territory.Core;
        int count = 0;

        for (int y = 1; y < Size - 1; y++)
        {
            int row = y * Size;
            for (int x = 1; x < Size - 1; x++)
            {
                int index = row + x;
                short mine = owner[index];
                if (mine == TribeStore.None || core[index] != 0)
                {
                    continue;
                }

                short west = owner[index - 1];
                if (west == TribeStore.None || west == mine)
                {
                    continue;
                }

                if (owner[index + 1] == west && owner[index - Size] == west && owner[index + Size] == west)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
