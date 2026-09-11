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
    private const int TribeCount = 5;
    private const int PerTribe = 40;

    private sealed class Setup
    {
        public WorldState World = null!;
        public Population People = null!;
        public TribeStore Tribes = null!;
        public SettlementStore Settlements = null!;
        public Territory Territory = null!;
        public SimulationLoop Loop = null!;
        public int Created;
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

        int created = TribeSeeder.Seed(world, people, tribes, settlements, territory, TribeCount, PerTribe);

        var loop = new SimulationLoop(
            world,
            new PopulationSystem(people),
            new SettlementSystem(people, tribes, settlements, territory));

        return new Setup
        {
            World = world,
            People = people,
            Tribes = tribes,
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
        Assert.Equal(setup.Created, setup.Tribes.Count);
        Assert.True(setup.People.Count > 0, "Ни одного человека не поселилось.");
        Assert.True(setup.Settlements.Count >= 3, "Не хватает стартовых стоянок.");
        Assert.True(setup.Territory.Claimed > 0, "Стоянки не заняли землю.");

        for (short tribe = 1; tribe < setup.Tribes.Capacity; tribe++)
        {
            if (setup.Tribes.Alive[tribe])
            {
                Assert.False(string.IsNullOrWhiteSpace(setup.Tribes.Name[tribe]), "Народ без имени.");
            }
        }

        WorldMap map = setup.World.Map!;
        int high = setup.People.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!setup.People.Alive[i])
            {
                continue;
            }

            Assert.NotEqual(TribeStore.None, setup.People.Tribe[i]);
            Assert.True(map.IsLand((setup.People.Y[i] * Size) + setup.People.X[i]), "Человек оказался в воде.");
        }
    }

    [Fact]
    public void TribesHoldLandAfterTwoHundredTicks()
    {
        Setup setup = Build(90210);
        setup.Loop.RunTicks(200);

        Assert.True(setup.Settlements.Count > 0, "Все поселения исчезли.");
        Assert.True(setup.Territory.Claimed > 0, "Земля ни за кем не числится.");

        int counted = 0;
        short[] owners = setup.Territory.Owner;
        for (int i = 0; i < owners.Length; i++)
        {
            short owner = owners[i];
            if (owner == TribeStore.None)
            {
                continue;
            }

            counted++;
            Assert.True(setup.Tribes.IsAlive(owner), "Тайл числится за исчезнувшим народом.");
        }

        Assert.Equal(counted, setup.Territory.Claimed);
    }

    [Fact]
    public void SameSeedGivesSameHistory()
    {
        Setup first = Build(777);
        Setup second = Build(777);

        first.Loop.RunTicks(150);
        second.Loop.RunTicks(150);

        Assert.Equal(first.People.Checksum(), second.People.Checksum());
        Assert.Equal(first.Settlements.Checksum(), second.Settlements.Checksum());
        Assert.Equal(first.Territory.Checksum(), second.Territory.Checksum());
        Assert.Equal(first.Tribes.Count, second.Tribes.Count);
    }

    [Fact]
    public void ReleaseTouchesOnlyOwnTiles()
    {
        var territory = new Territory(16, 16, 8);

        Assert.True(territory.Claim(territory.Width * 5 + 5, 1));
        Assert.True(territory.Claim(territory.Width * 5 + 6, 2));
        Assert.Equal(2, territory.Claimed);

        territory.Release(1, 5, 5, 2);

        Assert.Equal(TribeStore.None, territory.OwnerAt(5, 5));
        Assert.Equal((short)2, territory.OwnerAt(6, 5));
        Assert.Equal(1, territory.Claimed);
        Assert.Equal(0, territory.Tiles[1]);
        Assert.Equal(1, territory.Tiles[2]);
    }

    [Fact]
    public void TribeSlotsAreReusedAfterRemoval()
    {
        var tribes = new TribeStore(4);

        short first = tribes.Create("Аргард", 0, 1, 1);
        short second = tribes.Create("Белмир", 1, 2, 2);

        Assert.NotEqual(TribeStore.None, first);
        Assert.NotEqual(first, second);
        Assert.Equal(2, tribes.Count);

        Assert.True(tribes.Remove(first));
        Assert.Equal(1, tribes.Count);

        short third = tribes.Create("Венград", 2, 3, 3);
        Assert.Equal(first, third);
        Assert.Equal("Венград", tribes.NameOf(third));
    }
}
