using System.Diagnostics;
using System.Globalization;
using WorldBox.Core;
using WorldBox.Core.Economy;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Roads;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

// Консольный замер симуляции без окна: карта, люди, племена, поселения, хозяйство и эпохи.
// Пример: dotnet run -c Release --project src/WorldBox.Bench -- --ticks 5000 --size 512 --seed 1
// Длинный прогон для проверки эпох и торговых путей: --ticks 100000
int ticks = 5000;
int size = 512;
int seed = 20260911;
int tribeCount = TribeSeeder.DefaultTribes;
int startPeople = 420;

for (int i = 0; i < args.Length - 1; i++)
{
    if (!int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
    {
        continue;
    }

    switch (args[i])
    {
        case "--ticks":
            ticks = Math.Max(1, value);
            break;
        case "--size":
            size = Math.Clamp(value, 16, 4096);
            break;
        case "--seed":
            seed = value;
            break;
        case "--tribes":
            // Ноль — это пустой мир без народов и людей: базовая строка S0 в docs/PERF.md.
            tribeCount = Math.Clamp(value, 0, 60);
            break;
        case "--people":
            startPeople = Math.Clamp(value, tribeCount, Population.DefaultCapacity);
            break;
    }
}

Console.OutputEncoding = System.Text.Encoding.UTF8;

var generation = Stopwatch.StartNew();
WorldMap map = WorldGenerator.Generate(size, size, seed);
generation.Stop();

var world = new WorldState(size, size, seed);
world.SetMap(map);

var people = new Population(Population.DefaultCapacity, size, size);
var tribes = new TribeStore();
var settlements = new SettlementStore();
var territory = new Territory(size, size, tribes.Capacity);
var tech = new TribeTech(tribes.Capacity);

EraTable? table = EraTable.Load(out string eraError);
EconomyTable? economy = EconomyTable.Load(out string economyError);

// С нулём народов никого не селим: мир остаётся пустым, и видна цена самого цикла.
if (tribeCount > 0)
{
    TribeSeeder.Seed(world, people, tribes, settlements, territory, tribeCount, Math.Max(1, startPeople / tribeCount));
}

var populationSystem = new PopulationSystem(people, null, tech);
var settlementSystem = new SettlementSystem(people, tribes, settlements, territory);
var territorySystem = new TerritorySystem(tribes, territory);

// Дороги входят в замер: поиск пути — самая дорогая часть тика после жителей.
var roads = new RoadNetwork(size, size);
var roadSystem = new RoadSystem(tribes, settlements, roads);

// Хозяйство собирается раньше эпох: эпохам нужны торговые ресурсы и надбавка к развитию.
TribeMarket? market = null;
TradeNetwork? routes = null;
EconomySystem? economySystem = null;
if (economy != null)
{
    market = new TribeMarket(tribes.Capacity, economy.Count);
    routes = new TradeNetwork(economy.Trade.MaxRoutes);
    economySystem = new EconomySystem(economy, tribes, settlements, territory, market, routes);
}

EraSystem? eraSystem = null;
if (table != null)
{
    world.YearsPerTick = table.YearsPerTickOf(0);
    eraSystem = new EraSystem(table, tribes, territory, tech, market);
}

var systems = new List<ISimulationSystem> { populationSystem, settlementSystem, territorySystem, roadSystem };
if (economySystem != null)
{
    systems.Add(economySystem);
}

if (eraSystem != null)
{
    systems.Add(eraSystem);
}

var loop = new SimulationLoop(world, systems.ToArray());

// Прогрев: первые тики всегда медленнее из-за JIT.
loop.RunTicks(Math.Min(200, ticks));
world.TickStats.Clear();

var watch = Stopwatch.StartNew();
loop.RunTicks(ticks);
watch.Stop();

double totalMs = watch.Elapsed.TotalMilliseconds;
double msPerTick = totalMs / ticks;

Console.WriteLine("Мир: {0}x{1}, сид {2}", size, size, seed);
Console.WriteLine("Карта считана за: {0:F1} мс", generation.Elapsed.TotalMilliseconds);
Console.WriteLine("Тиков: {0}", ticks);
Console.WriteLine("Всего: {0:F1} мс", totalMs);
Console.WriteLine("На тик: {0:F4} мс (бюджет 6 мс)", msPerTick);
Console.WriteLine("Худший 1% тиков: {0:F4} мс", world.TickStats.Percentile(0.99));
Console.WriteLine("Тиков в секунду: {0:F0}", 1000.0 / Math.Max(msPerTick, 0.000001));
Console.WriteLine();
Console.WriteLine(
    "Люди: {0}, рождений за тик {1}, смертей за тик {2}",
    people.Count,
    populationSystem.LastBirths,
    populationSystem.LastDeaths);
Console.WriteLine("Народы: {0}, поселения: {1}", tribes.Count, settlements.Count);

var roadTiles = new int[RoadNetwork.LevelCount];
for (int i = 0; i < roads.Level.Length; i++)
{
    roadTiles[roads.Level[i]]++;
}

Console.WriteLine(
    "Дороги: маршрутов {0} из {1}, тайлов полотна {2}, снято за последний прогон {3}",
    roads.Count,
    roads.Capacity,
    roads.Tiles,
    roadSystem.LastRemoved);
Console.WriteLine(
    "  тропы {0}, грунтовки {1}, тракты {2}, рельсы {3}, шоссе {4}",
    roadTiles[RoadNetwork.Trail],
    roadTiles[RoadNetwork.Paved],
    roadTiles[RoadNetwork.Highway],
    roadTiles[RoadNetwork.Rail],
    roadTiles[RoadNetwork.Motorway]);
Console.WriteLine("  контрольная сумма дорог: {0}", roads.Checksum());
Console.WriteLine("Год: {0:F0}, лет в тике: {1:F2}", world.Year, world.YearsPerTick);
Console.WriteLine();

if (table != null)
{
    var perEra = new int[table.Count];
    for (short t = 1; t < tribes.Capacity; t++)
    {
        if (!tribes.Alive[t])
        {
            continue;
        }

        perEra[Math.Clamp(tribes.Era[t], 0, table.Last)]++;
    }

    Console.WriteLine("Эпохи живых народов:");
    for (int era = 0; era < table.Count; era++)
    {
        if (perEra[era] == 0)
        {
            continue;
        }

        Console.WriteLine(
            "  {0}: {1} народов, {2:F2} лет в тике",
            table.Name[era],
            perEra[era],
            table.YearsPerTick[era]);
    }

    if (eraSystem != null)
    {
        Console.WriteLine("Самая развитая эпоха: {0}", table.Name[Math.Clamp(eraSystem.TopEra, 0, table.Last)]);
    }

    Console.WriteLine("Контрольная сумма развития: {0}", tech.Checksum(tribes));
}
else
{
    Console.WriteLine("Таблица эпох не прочитана, эпохи в замер не вошли: {0}", eraError);
}

Console.WriteLine();

if (economy != null && market != null && routes != null && economySystem != null)
{
    int seaRoutes = 0;
    for (int i = 0; i < routes.Count; i++)
    {
        if (routes.Sea[i])
        {
            seaRoutes++;
        }
    }

    Console.WriteLine("Хозяйство:");
    Console.WriteLine("  торговых путей: {0} из {1}, из них морских {2}", routes.Count, routes.Capacity, seaRoutes);
    Console.WriteLine("  перевезено за последний прогон: {0:F1}", economySystem.LastTradeVolume);
    Console.WriteLine("  голодают народов: {0}", economySystem.HungryTribes);

    short richest = (short)economySystem.Richest;
    if (tribes.IsAlive(richest))
    {
        Console.WriteLine(
            "  самый богатый: {0}, казна {1:F0}, путей {2}",
            tribes.NameOf(richest),
            market.Wealth[richest],
            market.Routes[richest]);
    }

    Console.WriteLine();
    Console.WriteLine("Товары (среднее по живым народам):");
    for (int good = 0; good < economy.Count; good++)
    {
        float priceSum = 0f;
        float stockSum = 0f;
        int alive = 0;
        for (short t = 1; t < tribes.Capacity; t++)
        {
            if (!tribes.Alive[t])
            {
                continue;
            }

            priceSum += market.PriceOf(t, good);
            stockSum += market.StockOf(t, good);
            alive++;
        }

        if (alive == 0)
        {
            continue;
        }

        Console.WriteLine(
            "  {0}: цена {1:F2}, склад {2:F1}, с эпохи {3}",
            economy.Id[good],
            priceSum / alive,
            stockSum / alive,
            economy.MinEra[good]);
    }

    Console.WriteLine();
    Console.WriteLine("Контрольная сумма рынка: {0}", market.Checksum(tribes));
    Console.WriteLine("Контрольная сумма путей: {0}", routes.Checksum());
}
else
{
    Console.WriteLine("Таблица товаров не прочитана, хозяйство в замер не вошло: {0}", economyError);
}

Console.WriteLine();
Console.WriteLine("Строка для docs/PERF.md:");
Console.WriteLine(
    "| {0:yyyy-MM-dd} | S5 | {1}x{1}, {2} народов, {3} человек, {4} путей | {5:F4} | {6:F4} | — |",
    DateTime.Now,
    size,
    tribeCount,
    startPeople,
    routes != null ? routes.Count : 0,
    msPerTick,
    world.TickStats.Percentile(0.99));

Console.WriteLine();
Console.WriteLine("Системы (вместе с прогревом):");
for (int i = 0; i < loop.SystemCount; i++)
{
    Console.WriteLine("  {0}: {1:F2} мс всего, последний запуск {2:F3} мс", loop.SystemName(i), loop.SystemTotalMs(i), loop.SystemLastMs(i));
}

// Отдельная строка про бюджет хозяйства: его проверка записана в плане среза S5.
if (economySystem != null)
{
    for (int i = 0; i < loop.SystemCount; i++)
    {
        if (!string.Equals(loop.SystemName(i), economySystem.Name, StringComparison.Ordinal))
        {
            continue;
        }

        Console.WriteLine();
        Console.WriteLine(
            "Экономический прогон: {0:F3} мс (бюджет 2 мс на 500 поселений, сейчас {1})",
            loop.SystemLastMs(i),
            settlements.Count);
    }
}
