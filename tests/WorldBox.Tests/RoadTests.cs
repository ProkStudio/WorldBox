using WorldBox.Core;
using WorldBox.Core.Roads;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Дороги. Главная жалоба была простая: города стояли сами по себе, между ними не было
/// ничего. Тесты держат новое поведение: дорога сама появляется между соседними городами,
/// обходит воду, новые маршруты липнут к старым и срастаются в тракты, полотно растёт с эпохой,
/// а после гибели города маршрут исчезает, а брошенная дорога остаётся лежать на земле.
/// </summary>
public sealed class RoadTests
{
    private const int Size = 48;

    [Fact]
    public void RoadAppearsBetweenNeighbourTowns()
    {
        Setup setup = Build(-1, -1);
        short tribe = setup.Tribes.Create("Первые", 1, 10, 24);
        int first = setup.Settlements.Found(10, 24, tribe, "Первый", 0);
        int second = setup.Settlements.Found(24, 24, tribe, "Второй", 0);

        Run(setup, 120);

        Assert.True(setup.Roads.Count > 0, "Между соседними городами так и не появилось дороги.");
        Assert.True(setup.Roads.Connects(first, second), "Дорога соединила не те города.");
        Assert.True(setup.Roads.Tiles > 10, "Полотно почти не легло на землю: тайлов " + setup.Roads.Tiles + ".");
        Assert.True(
            setup.Roads.LevelAt(17, 24) != RoadNetwork.NoRoad,
            "На полпути между городами дороги нет.");
    }

    [Fact]
    public void RoadGoesAroundWaterAndNeverGetsWet()
    {
        // Вода режет карту пополам, сухой осталась одна перемычка: дорога обязана кружить.
        Setup setup = Build(24, 20);
        short tribe = setup.Tribes.Create("Береговые", 1, 10, 10);
        setup.Settlements.Found(10, 10, tribe, "Запад", 0);
        setup.Settlements.Found(36, 10, tribe, "Восток", 0);

        Run(setup, 160);

        WorldMap map = setup.World.Map!;
        int wet = 0;
        for (int i = 0; i < map.TileCount; i++)
        {
            if (setup.Roads.Level[i] != RoadNetwork.NoRoad && !map.IsLand(i))
            {
                wet++;
            }
        }

        Assert.True(setup.Roads.Count > 0, "Дорога в обход воды не построилась.");
        Assert.True(wet == 0, "Дорога пошла по воде: мокрых тайлов " + wet + ".");
        Assert.True(
            setup.Roads.LevelAt(24, 20) != RoadNetwork.NoRoad,
            "Дорога не воспользовалась единственной сухой перемычкой.");
    }

    [Fact]
    public void NewRoutesStickToOldRoads()
    {
        Setup setup = Build(-1, -1);
        short tribe = setup.Tribes.Create("Трое", 1, 10, 24);
        int west = setup.Settlements.Found(10, 24, tribe, "Запад", 0);
        setup.Settlements.Found(24, 26, tribe, "Средний", 0);
        int east = setup.Settlements.Found(38, 24, tribe, "Восток", 0);

        Run(setup, 400);

        Assert.True(setup.Roads.Count >= 3, "Три города не собрали три маршрута: их " + setup.Roads.Count + ".");

        int far = RouteBetween(setup.Roads, west, east);
        Assert.True(far >= 0, "Между крайними городами нет маршрута.");

        // Сколько тайлов дальнего маршрута лежит на путях двух коротких.
        int shared = SharedTiles(setup.Roads, far);
        int length = setup.Roads.PathLength[far];

        Assert.True(
            shared * 2 > length,
            "Дальняя дорога пошла целиком своей линией: общих тайлов " + shared + " из " + length + ".");
    }

    [Fact]
    public void RoadGrowsWithEra()
    {
        Setup setup = Build(-1, -1);
        short tribe = setup.Tribes.Create("Развитые", 1, 10, 24);
        setup.Settlements.Found(10, 24, tribe, "Первый", 0);
        setup.Settlements.Found(24, 24, tribe, "Второй", 0);

        Run(setup, 40);

        Assert.True(setup.Roads.Count > 0, "Дорога каменного века не построилась.");
        Assert.Equal(RoadNetwork.Trail, setup.Roads.RouteLevel[0]);

        setup.Tribes.Era[tribe] = 9;
        Run(setup, 200);

        Assert.True(
            setup.Roads.RouteLevel[0] >= RoadNetwork.Rail,
            "В современности тропа не стала ни рельсами, ни шоссе: уровень " + setup.Roads.RouteLevel[0] + ".");
        Assert.True(
            setup.Roads.LevelAt(17, 24) >= RoadNetwork.Rail,
            "Полотно на земле осталось старым.");
    }

    [Fact]
    public void RouteDiesWithTownButRoadStays()
    {
        Setup setup = Build(-1, -1);
        short tribe = setup.Tribes.Create("Исчезающие", 1, 10, 24);
        setup.Settlements.Found(10, 24, tribe, "Первый", 0);
        int second = setup.Settlements.Found(24, 24, tribe, "Второй", 0);

        Run(setup, 40);
        Assert.True(setup.Roads.Count > 0, "Дорога не построилась, проверять нечего.");

        int tiles = setup.Roads.Tiles;
        setup.Settlements.Remove(second);
        Run(setup, 120);

        Assert.Equal(0, setup.Roads.Count);
        Assert.Equal(tiles, setup.Roads.Tiles);
    }

    [Fact]
    public void SameWorldBuildsSameRoads()
    {
        ulong first = RunAndHash();
        ulong second = RunAndHash();

        Assert.Equal(first, second);
    }

    [Fact]
    public void RoadKindFollowsEras()
    {
        Assert.Equal(RoadNetwork.Trail, RoadNetwork.LevelFor(0, 1, 2));
        Assert.Equal(RoadNetwork.Trail, RoadNetwork.LevelFor(1, 1, 2));
        Assert.Equal(RoadNetwork.Paved, RoadNetwork.LevelFor(3, 1, 2));
        Assert.Equal(RoadNetwork.Highway, RoadNetwork.LevelFor(5, 1, 2));

        // С индустрии направления делятся: часть уходит под рельсы, часть остаётся дорогой.
        Assert.Equal(RoadNetwork.Rail, RoadNetwork.LevelFor(7, 2, 4));
        Assert.Equal(RoadNetwork.Highway, RoadNetwork.LevelFor(7, 2, 5));
        Assert.Equal(RoadNetwork.Rail, RoadNetwork.LevelFor(10, 2, 4));
        Assert.Equal(RoadNetwork.Motorway, RoadNetwork.LevelFor(10, 2, 5));
    }

    private sealed class Setup
    {
        public WorldState World = null!;
        public TribeStore Tribes = null!;
        public SettlementStore Settlements = null!;
        public RoadNetwork Roads = null!;
        public RoadSystem System = null!;
    }

    /// <summary>
    /// Ровная травяная карта. waterColumn меньше нуля — суша целиком, иначе это пролив
    /// по всей высоте карты с одной сухой перемычкой в ряду bridgeRow.
    /// </summary>
    private static Setup Build(int waterColumn, int bridgeRow)
    {
        var map = new WorldMap(Size, Size);
        Array.Fill(map.BiomeAt, (byte)Biome.Grassland);

        if (waterColumn >= 0)
        {
            for (int y = 0; y < Size; y++)
            {
                if (y == bridgeRow)
                {
                    continue;
                }

                map.BiomeAt[(y * Size) + waterColumn] = (byte)Biome.Ocean;
            }
        }

        map.Recount();

        var world = new WorldState(Size, Size, 11);
        world.SetMap(map);

        var tribes = new TribeStore();
        var settlements = new SettlementStore();
        var roads = new RoadNetwork(Size, Size);

        return new Setup
        {
            World = world,
            Tribes = tribes,
            Settlements = settlements,
            Roads = roads,
            System = new RoadSystem(tribes, settlements, roads),
        };
    }

    private static void Run(Setup setup, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            setup.System.Tick(setup.World);
        }
    }

    private static ulong RunAndHash()
    {
        Setup setup = Build(-1, -1);
        short tribe = setup.Tribes.Create("Повторимые", 1, 10, 24);
        setup.Settlements.Found(10, 24, tribe, "Первый", 0);
        setup.Settlements.Found(24, 26, tribe, "Второй", 0);
        setup.Settlements.Found(38, 24, tribe, "Третий", 0);

        Run(setup, 300);
        return setup.Roads.Checksum();
    }

    /// <summary>Номер маршрута между двумя городами или -1.</summary>
    private static int RouteBetween(RoadNetwork roads, int first, int second)
    {
        for (int i = 0; i < roads.Count; i++)
        {
            int from = roads.FromSettlement[i];
            int to = roads.ToSettlement[i];
            if ((from == first && to == second) || (from == second && to == first))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Сколько тайлов маршрута лежит ещё и на других маршрутах.</summary>
    private static int SharedTiles(RoadNetwork roads, int route)
    {
        int start = RoadNetwork.PathStart(route);
        int length = roads.PathLength[route];
        int shared = 0;

        for (int i = 0; i < length; i++)
        {
            int tile = roads.Path[start + i];

            for (int other = 0; other < roads.Count; other++)
            {
                if (other == route)
                {
                    continue;
                }

                int otherStart = RoadNetwork.PathStart(other);
                int otherLength = roads.PathLength[other];
                bool hit = false;

                for (int j = 0; j < otherLength; j++)
                {
                    if (roads.Path[otherStart + j] == tile)
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                {
                    shared++;
                    break;
                }
            }
        }

        return shared;
    }
}
