using WorldBox.Core;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

public class PopulationTests
{
    private const int Size = 96;
    private const int Capacity = 4000;
    private const int StartPeople = 300;

    [Fact]
    public void SpawnMoveAndKillKeepDensityConsistent()
    {
        var people = new Population(8, 16, 16);

        int first = people.Spawn(3, 4, 0, 10f);
        int second = people.Spawn(3, 4, 0, 20f);

        Assert.True(first >= 0);
        Assert.True(second >= 0);
        Assert.Equal(2, people.Count);
        Assert.Equal(2, people.DensityAt(3, 4));

        people.Move(second, 5, 4);
        Assert.Equal(1, people.DensityAt(3, 4));
        Assert.Equal(1, people.DensityAt(5, 4));

        Assert.True(people.Kill(first));
        Assert.False(people.Kill(first));
        Assert.Equal(1, people.Count);
        Assert.Equal(0, people.DensityAt(3, 4));
    }

    [Fact]
    public void CapacityIsRespectedAndSlotsAreReused()
    {
        var people = new Population(3, 8, 8);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(people.Spawn(1, 1, 0, 0f) >= 0);
        }

        Assert.False(people.HasRoom);
        Assert.Equal(-1, people.Spawn(1, 1, 0, 0f));

        people.Kill(1);
        Assert.True(people.HasRoom);
        Assert.Equal(1, people.Spawn(2, 2, 0, 0f));
        Assert.Equal(3, people.Count);
    }

    [Fact]
    public void SeedPutsPeopleOnLand()
    {
        (WorldState world, Population people, SimulationLoop _) = Build(4242);
        WorldMap map = world.Map!;

        Assert.True(people.Count > 0);
        AssertEveryoneOnLand(people, map);
    }

    [Fact]
    public void SimulationIsDeterministic()
    {
        (WorldState firstWorld, Population firstPeople, SimulationLoop firstLoop) = Build(20260911);
        (WorldState secondWorld, Population secondPeople, SimulationLoop secondLoop) = Build(20260911);

        firstLoop.RunTicks(120);
        secondLoop.RunTicks(120);

        Assert.Equal(firstPeople.Count, secondPeople.Count);
        Assert.Equal(firstPeople.Checksum(), secondPeople.Checksum());
        Assert.Equal(firstWorld.Checksum(), secondWorld.Checksum());
    }

    [Fact]
    public void PeopleSurviveTwoHundredTicks()
    {
        (WorldState world, Population people, SimulationLoop loop) = Build(77);

        loop.RunTicks(200);

        Assert.True(people.Count > 0);
        Assert.True(people.Count <= people.Capacity);
        AssertEveryoneOnLand(people, world.Map!);
    }

    [Fact]
    public void PeopleSurviveTwentyFiveYearTicksOfFirstEra()
    {
        // Первая эпоха сжимает время до двадцати пяти лет в тике (data/eras.json).
        // Раньше при таком тике рождаемость упиралась в потолок «один ребёнок за тик»,
        // а смертность — нет, и мир вымирал в палеолите за три десятка тиков.
        (WorldState world, Population people, SimulationLoop loop) = Build(77, 25f);

        loop.RunTicks(200);

        Assert.True(
            people.Count >= StartPeople,
            "При двадцати пяти годах в тике народ должен жить, а осталось жителей: " + people.Count);
        Assert.True(people.Count <= people.Capacity);
        AssertEveryoneOnLand(people, world.Map!);
    }

    private static (WorldState World, Population People, SimulationLoop Loop) Build(int seed, float yearsPerTick = 5f)
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, seed);
        var world = new WorldState(Size, Size, seed);
        world.SetMap(map);
        world.YearsPerTick = yearsPerTick;

        var people = new Population(Capacity, Size, Size);
        PopulationSeeder.Seed(world, people, StartPeople);

        var loop = new SimulationLoop(world, new PopulationSystem(people));
        return (world, people, loop);
    }

    private static void AssertEveryoneOnLand(Population people, WorldMap map)
    {
        for (int i = 0; i < people.HighWater; i++)
        {
            if (!people.Alive[i])
            {
                continue;
            }

            int x = people.X[i];
            int y = people.Y[i];

            Assert.True(map.InBounds(x, y));
            Assert.True(map.IsLand((y * map.Width) + x));
        }
    }
}
