using System.Globalization;
using WorldBox.Core;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки эпох: что таблица из data/eras.json действительно читается,
/// что без олова бронзовый век не наступает, что народ доходит до следующей эпохи
/// и укорачивает тик, что отставший сосед учится через границу, и что всё это повторимо.
/// Учебная таблица собирается в памяти: тесты не должны падать от правки баланса в файле.
/// </summary>
public sealed class EraTests
{
    private const int Size = 64;

    private sealed class Setup
    {
        public WorldState World = null!;
        public Population People = null!;
        public TribeStore Tribes = null!;
        public SettlementStore Settlements = null!;
        public Territory Territory = null!;
        public TribeTech Tech = null!;
        public SimulationLoop Loop = null!;
    }

    [Fact]
    public void RealTableLoadsAndGrowsMonotonically()
    {
        EraTable? table = EraTable.Load(out string error);

        Assert.True(table != null, "Таблица эпох не прочиталась: " + error);
        Assert.Equal(11, table!.Count);
        Assert.Equal("paleolithic", table.Id[0]);
        Assert.Equal("space", table.Id[table.Last]);

        int bronze = table.Find("bronze");
        Assert.True(bronze > 0, "В таблице нет бронзового века.");
        Assert.Equal(
            EraTable.ResourceBit(ResourceKind.Copper) | EraTable.ResourceBit(ResourceKind.Tin),
            table.RequiredResources[bronze]);

        int neolithic = table.Find("neolithic");
        Assert.True(neolithic > 0, "В таблице нет неолита.");
        Assert.Equal((byte)GeoFeature.Fertile, table.RequiredGeo[neolithic]);

        for (int era = 0; era < table.Count; era++)
        {
            Assert.False(string.IsNullOrWhiteSpace(table.Name[era]), "Эпоха без названия.");
            Assert.Equal("era." + table.Id[era], table.NameKey[era]);
            Assert.True(table.YearsPerTick[era] > 0f, "Сжатие времени должно быть положительным.");

            if (era == 0)
            {
                continue;
            }

            Assert.True(
                table.YearsPerTick[era] <= table.YearsPerTick[era - 1],
                "С ростом эпохи тик не должен удлиняться.");
            Assert.True(
                table.MinPop[era] > table.MinPop[era - 1],
                "Порог населения должен расти.");
        }

        Assert.True(
            EraRules.Headcount(table, 100, 1) > EraRules.Headcount(table, 100, 0),
            "Один агент в старшей эпохе должен считаться за большее число людей.");
    }

    [Fact]
    public void BronzeNeedsTinAndPeople()
    {
        EraTable? table = EraTable.Load(out string error);
        Assert.True(table != null, "Таблица эпох не прочиталась: " + error);

        int neolithic = table!.Find("neolithic");
        var allLand = (byte)(GeoFeature.Fertile | GeoFeature.River | GeoFeature.Coast | GeoFeature.Mountain);
        int onlyCopper = EraTable.ResourceBit(ResourceKind.Copper);

        EraBlock withoutTin = EraRules.Evaluate(
            table,
            neolithic,
            1_000_000L,
            onlyCopper,
            allLand,
            out int missingResources,
            out byte missingGeo);

        Assert.Equal(EraBlock.Resource, withoutTin);
        Assert.Equal(ResourceKind.Tin, EraRules.FirstMissing(missingResources));
        Assert.Equal((byte)0, missingGeo);

        int withTin = onlyCopper | EraTable.ResourceBit(ResourceKind.Tin);

        EraBlock tooFew = EraRules.Evaluate(table, neolithic, 10L, withTin, allLand, out _, out _);
        Assert.Equal(EraBlock.People, tooFew);

        EraBlock ready = EraRules.Evaluate(table, neolithic, 1_000_000L, withTin, allLand, out _, out _);
        Assert.Equal(EraBlock.Ready, ready);

        EraBlock top = EraRules.Evaluate(table, table.Last, 1_000_000_000L, int.MaxValue, allLand, out _, out _);
        Assert.Equal(EraBlock.Top, top);
    }

    [Fact]
    public void BrokenTableIsRejectedWithMessage()
    {
        EraTable? empty = EraTable.Parse("{ \"eras\": [] }", out string error);

        Assert.Null(empty);
        Assert.False(string.IsNullOrWhiteSpace(error), "Ошибка должна быть объяснена текстом.");

        EraTable? noBlocks = EraTable.Parse(
            "{ \"eras\": [ { \"id\": \"a\", \"yearsPerTick\": 1, \"foodPerWorker\": 1, \"militaryPower\": 1,"
                + " \"requires\": { \"minPop\": 0, \"resources\": [], \"geo\": [] } } ] }",
            out string blocksError);

        Assert.Null(noBlocks);
        Assert.False(string.IsNullOrWhiteSpace(blocksError), "О пропущенных разделах надо сообщать.");
    }

    [Fact]
    public void TribeReachesNextEraAndTickGetsShorter()
    {
        EraTable? table = EraTable.Parse(CheapTable(30f, 0f), out string error);
        Assert.True(table != null, "Учебная таблица не собралась: " + error);

        var world = new WorldState(Size, Size, 2026);
        world.SetMap(WorldGenerator.Generate(Size, Size, 2026));

        var tribes = new TribeStore();
        var territory = new Territory(Size, Size, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        short tribe = tribes.Create("Испытатели", 0, 8, 8);
        tribes.People[tribe] = 500;

        world.YearsPerTick = table!.YearsPerTickOf(0);
        var loop = new SimulationLoop(world, new EraSystem(table, tribes, territory, tech));
        loop.RunTicks(100);

        Assert.Equal((byte)1, tribes.Era[tribe]);
        Assert.True(
            MathF.Abs(world.YearsPerTick - table.YearsPerTick[1]) < 0.001f,
            "Длина тика должна браться из достигнутой эпохи.");
        Assert.True(world.YearsPerTick < 20f, "Время должно сжаться после перехода.");
        Assert.True(world.Year > WorldState.StartYear, "Год должен идти вперёд.");
    }

    [Fact]
    public void BackwardNeighborLearnsThroughBorder()
    {
        // Своими силами дойти невозможно: цена знания огромная, зато сосед учит наверняка.
        EraTable? table = EraTable.Parse(CheapTable(1_000_000_000f, 1f), out string error);
        Assert.True(table != null, "Учебная таблица не собралась: " + error);

        var world = new WorldState(Size, Size, 555);
        world.SetMap(WorldGenerator.Generate(Size, Size, 555));

        var tribes = new TribeStore();
        var territory = new Territory(Size, Size, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        short ahead = tribes.Create("Передовые", 0, 8, 8);
        short behind = tribes.Create("Отставшие", 1, 9, 8);
        tribes.Era[ahead] = 1;
        tribes.People[ahead] = 500;
        tribes.People[behind] = 500;

        for (int y = 6; y <= 10; y++)
        {
            int row = y * Size;
            for (int x = 4; x <= 8; x++)
            {
                territory.Claim(row + x, ahead);
            }

            for (int x = 9; x <= 13; x++)
            {
                territory.Claim(row + x, behind);
            }
        }

        world.YearsPerTick = table!.YearsPerTickOf(0);
        var loop = new SimulationLoop(world, new EraSystem(table, tribes, territory, tech));

        // Соседи видны только после полного обхода карты, это десятки тиков.
        loop.RunTicks(300);

        Assert.Equal((byte)1, tribes.Era[behind]);
        Assert.Equal((byte)1, tribes.Era[ahead]);
        Assert.True(tech.NeighborCount(behind) > 0, "Граница с соседом не замечена.");
    }

    [Fact]
    public void SameSeedGivesSameEraHistory()
    {
        EraTable? table = EraTable.Parse(CheapTable(400f, 0.05f), out string error);
        Assert.True(table != null, "Учебная таблица не собралась: " + error);

        Setup first = Build(3131, table!);
        Setup second = Build(3131, table!);

        first.Loop.RunTicks(300);
        second.Loop.RunTicks(300);

        Assert.Equal(first.Tech.Checksum(first.Tribes), second.Tech.Checksum(second.Tribes));
        Assert.Equal(first.People.Checksum(), second.People.Checksum());
        Assert.Equal(first.Territory.Checksum(), second.Territory.Checksum());
        Assert.Equal(first.World.Year, second.World.Year);
        Assert.Equal(first.World.YearsPerTick, second.World.YearsPerTick);
    }

    private static Setup Build(int seed, EraTable table)
    {
        WorldMap map = WorldGenerator.Generate(Size, Size, seed);
        var world = new WorldState(Size, Size, seed);
        world.SetMap(map);

        var people = new Population(4000, Size, Size);
        var tribes = new TribeStore();
        var settlements = new SettlementStore(64);
        var territory = new Territory(Size, Size, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        TribeSeeder.Seed(world, people, tribes, settlements, territory, 4, 30);

        world.YearsPerTick = table.YearsPerTickOf(0);
        var loop = new SimulationLoop(
            world,
            new PopulationSystem(people, null, tech),
            new SettlementSystem(people, tribes, settlements, territory),
            new EraSystem(table, tribes, territory, tech));

        return new Setup
        {
            World = world,
            People = people,
            Tribes = tribes,
            Settlements = settlements,
            Territory = territory,
            Tech = tech,
            Loop = loop,
        };
    }

    /// <summary>Две эпохи без требований: остаётся только цена знания и шанс подсмотреть у соседа.</summary>
    private static string CheapTable(float baseCost, float borderNeighbor)
    {
        string cost = baseCost.ToString("0.####", CultureInfo.InvariantCulture);
        string teach = borderNeighbor.ToString("0.####", CultureInfo.InvariantCulture);

        return "{"
            + "\"version\": 2,"
            + "\"diffusion\": { \"tradePartner\": 0, \"borderNeighbor\": " + teach + ", \"conquered\": 0, \"isolated\": 0 },"
            + "\"research\": { \"baseCost\": " + cost + ", \"costPerEra\": 0, \"settlementBonus\": 0,"
            + " \"neighborBonus\": 0, \"isolatedPenalty\": 1 },"
            + "\"population\": { \"perAgent\": 1, \"perAgentPerEra\": 1 },"
            + "\"geo\": { \"fertileValue\": 0.55, \"fertileTiles\": 1, \"riverTiles\": 1, \"coastTiles\": 1, \"mountainTiles\": 1 },"
            + "\"collapse\": { \"stabilityThreshold\": 0, \"erasLost\": 1, \"darkAgeYears\": 0, \"popShareToHold\": 0 },"
            + "\"eras\": ["
            + "{ \"id\": \"start\", \"name\": \"Начало\", \"yearsPerTick\": 20, \"foodPerWorker\": 1, \"militaryPower\": 1,"
            + " \"requires\": { \"minPop\": 0, \"resources\": [], \"geo\": [] } },"
            + "{ \"id\": \"next\", \"name\": \"Следующая\", \"yearsPerTick\": 2, \"foodPerWorker\": 1.5, \"militaryPower\": 2,"
            + " \"requires\": { \"prev\": \"start\", \"minPop\": 0, \"resources\": [], \"geo\": [] } }"
            + "]"
            + "}";
    }
}
