using WorldBox.Core;
using WorldBox.Core.Economy;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Проверки хозяйства: склады, цены, торговые пути и повторимость прогона.
/// Самое важное здесь — повторимость: два одинаковых мира обязаны дать один и тот же
/// рынок, иначе сломается вся дальнейшая история мира — войны, летопись и катастрофы.
/// </summary>
public sealed class EconomyTests
{
    /// <summary>Маленький мир: тесты должны идти быстро.</summary>
    private const int WorldSize = 96;

    /// <summary>Хватает и на пересборку сети путей, и на первые переходы цен.</summary>
    private const int Ticks = 900;

    [Fact]
    public void TableCoversEveryResource()
    {
        EconomyTable? table = EconomyTable.Load(out string error);
        Assert.True(table != null, "Таблица товаров не прочиталась: " + error);

        Assert.Equal(ResourceKinds.Count, table!.Count);
        Assert.Equal("good.food", table.NameKeyOf(EconomyTable.Food));

        int iron = table.Find("iron");
        Assert.True(iron > 0, "В таблице нет железа.");
        Assert.Equal("resource.iron", table.NameKeyOf(iron));

        // Железо нельзя копать в каменном веке и можно в свою эпоху — иначе вся лестница эпох теряет смысл.
        Assert.False(table.CanExtract(iron, 0));
        Assert.True(table.CanExtract(iron, table.MinEra[iron]));
        Assert.True(table.CanExtract(EconomyTable.Food, 0));
    }

    [Fact]
    public void FlatCellsMatchAccessors()
    {
        var market = new TribeMarket(4, 3);

        Assert.Equal(4, market.Capacity);
        Assert.Equal(3, market.Goods);

        market.Stock[(1 * market.Goods) + 2] = 5f;
        market.Price[(2 * market.Goods) + 0] = 7f;

        Assert.Equal(5d, market.StockOf(1, 2), 3);
        Assert.Equal(7d, market.PriceOf(2, 0), 3);
    }

    [Fact]
    public void ResetTouchesOnlyOneTribe()
    {
        var market = new TribeMarket(4, 2);
        market.Stock[(1 * 2) + 0] = 9f;
        market.Stock[(2 * 2) + 0] = 4f;

        market.Reset(1);

        Assert.Equal(0d, market.StockOf(1, 0), 3);
        Assert.Equal(4d, market.StockOf(2, 0), 3);
    }

    [Fact]
    public void DearestGoodIsTheMostExpensiveOne()
    {
        var market = new TribeMarket(3, 4);

        market.Stock[(1 * 4) + 1] = 2f;
        market.Stock[(1 * 4) + 3] = 2f;
        market.Price[(1 * 4) + 1] = 3f;
        market.Price[(1 * 4) + 3] = 11f;

        Assert.Equal(3, market.DearestGoodOf(1));
    }

    [Fact]
    public void RoutesKeepFlowUntilCleared()
    {
        var routes = new TradeNetwork(4);

        routes.Add(1, 2, 1, 2, 10, 10, 14, 12, 6, false);
        routes.Add(2, 3, 2, 3, 14, 12, 30, 40, 24, true);

        Assert.Equal(2, routes.Count);
        Assert.True(routes.Sea[1]);

        routes.Carry(0, 0, 2.5f);
        routes.Carry(0, 1, 2.5f);
        Assert.Equal(5d, routes.Volume[0], 3);

        // Перевозки обнуляются каждый прогон, а сами пути живут до пересборки сети.
        routes.ClearFlow();
        Assert.Equal(2, routes.Count);
        Assert.Equal(0d, routes.Volume[0], 3);

        routes.Clear();
        Assert.Equal(0, routes.Count);
    }

    [Fact]
    public void EconomyRunIsRepeatable()
    {
        Run(4242, out TribeMarket firstMarket, out TradeNetwork firstRoutes, out TribeStore firstTribes);
        Run(4242, out TribeMarket secondMarket, out TradeNetwork secondRoutes, out TribeStore secondTribes);

        Assert.Equal(firstMarket.Checksum(firstTribes), secondMarket.Checksum(secondTribes));
        Assert.Equal(firstRoutes.Checksum(), secondRoutes.Checksum());
    }

    [Fact]
    public void WealthAndBonusStayInsideLimits()
    {
        Run(777, out TribeMarket market, out TradeNetwork routes, out TribeStore tribes);

        Assert.True(routes.Count <= routes.Capacity);

        for (short t = 1; t < tribes.Capacity; t++)
        {
            if (!tribes.Alive[t])
            {
                continue;
            }

            // Казна не уходит в минус, а надбавка за торговлю ограничена потолком из data/economy.json.
            Assert.True(market.Wealth[t] >= 0f, "Казна ушла в минус.");
            Assert.InRange(market.ResearchBonusOf(t), 0f, 0.75f);
            Assert.True(market.PartnerCount(t) >= 0);
        }
    }

    /// <summary>Собирает маленький мир с хозяйством и прогоняет его заданное число тиков.</summary>
    private static void Run(
        int seed,
        out TribeMarket market,
        out TradeNetwork routes,
        out TribeStore tribes)
    {
        EconomyTable? table = EconomyTable.Load(out string error);
        Assert.True(table != null, "Таблица товаров не прочиталась: " + error);

        WorldMap map = WorldGenerator.Generate(WorldSize, WorldSize, seed);
        var world = new WorldState(WorldSize, WorldSize, seed);
        world.SetMap(map);

        var people = new Population(Population.DefaultCapacity, WorldSize, WorldSize);
        tribes = new TribeStore();
        var settlements = new SettlementStore();
        var territory = new Territory(WorldSize, WorldSize, tribes.Capacity);
        var tech = new TribeTech(tribes.Capacity);

        TribeSeeder.Seed(world, people, tribes, settlements, territory, 5, 60);

        market = new TribeMarket(tribes.Capacity, table!.Count);
        routes = new TradeNetwork(table.Trade.MaxRoutes);

        var populationSystem = new PopulationSystem(people, null, tech);
        var settlementSystem = new SettlementSystem(people, tribes, settlements, territory);
        var economySystem = new EconomySystem(table, tribes, settlements, territory, market, routes);

        var loop = new SimulationLoop(world, populationSystem, settlementSystem, economySystem);
        loop.RunTicks(Ticks);
    }
}
