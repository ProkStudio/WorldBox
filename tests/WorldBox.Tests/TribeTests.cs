using WorldBox.Core;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

public sealed class TribeTests
{
    private const int Size = 128;
    private const int Tribes = 5;
    private const int PerTribe = 40;

    private sealed class Setup
    {
        public required WorldState World { get; init; }

        public required Population People { get; init; }

        public required TribeStore TribeStore { get; init; }

        public required SettlementStore Settlements { get; init; }

        public required Territory Territory { get; init; }

        public required SimulationLoop Loop { get; init; }

        public required int Created { get; init; }
    }

    private static Setup Build(int seed)
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, seed);
        var world = new WorldState(Size, Size, seed);
        world.SetMap(map);

        var people = new Population(6000, Size, Size);
        var tribes = new TribeStore();
        var settlements = new SettlementStore(128);
        var territory = new Territory(Size, Size, tribes.Capacity);

        int created = TribeSeeder.Seed(world, people, tribes, settlements, territory, Tribes, PerTribe);

        var population = new PopulationSystem(people);
        var settlementSystem = new SettlementSystem(people, tribes, settlements, territory);
        var loop = new SimulationLoop(world, population, settlementSystem);

        return new Setup
        {
            World = world,
            People = people,
            TribeStore = tribes,
            Settlements = settlements,
            Territory = territory,
            Loop = loop,
            Created = created,
        };
    }

    [Fact]
    public void SeederPlacesTribesOnLandWithNamesAndCamps()
    {
        Setup setup = Build(4242);

        Assert.True(setup.Created >= 3, "Ожидали хотя бы три очага на карте 128x128.");
        Assert.Equal(setup.Created, setup.TribeStore.Count);
        Assert.True(setup.People.Count > 0);
        Assert.True(setup.Settlements.Count >= 3);

        for (short tribe = 1; tribe < setup.TribeStore.Capacity; tribe++)
        {
            if (!setup.TribeStore.Alive[tribe])
            {
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(setup.TribeStore.Name[tribe]));
        }

        WorldMap map = setup.World.Map!;
        int high = setup.People.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!setup.People.Alive[i])
            {
                continue;
            }

            Assert.True(setup.People.Tribe[i] != TribeStore.None, "Человек без народа на старте.");
            Assert.True(map.IsLand((setup.People.Y[i] * Size) + setup.People.X[i]), "Человек оказался в воде.");
        }
    }

    [Fact]
    public void TribesHoldLandAfterTwoHundredTicks()
    {
        Setup setup = Build(90210);
        setup.Loop.RunTicks(200);

        Assert.True(setup.Territory.Claimed > 0, "Ни один тайл не занят.");
        Assert.True(setup.Settlements.Count > 0, "Все поселения исчезли.");

        for (int i = 0; i < setup.Territory.Owner.Length; i++)
        {
            short owner = setup.Territory.