using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

namespace WorldBox.Core.Economy;

/// <summary>
/// Хозяйство народов. За один запуск система делает шесть вещей:
/// смотрит полосу карты и считает, с чего народ кормится и что добывает;
/// кладёт добычу на склад; кормит людей и стройки; портит часть запаса;
/// пересчитывает цены по дефициту; везёт товар по торговым путям туда, где он дороже.
///
/// Карта обходится по частям, как в системе эпох: за запуск — полоса строк, итог
/// публикуется после полного круга. Аллокаций в тике нет, случайности в экономике нет
/// вовсе: одинаковые партии дают одинаковые склады.
///
/// Торговые пути пока идут по прямой: важна только дальность, горы и реки каравану
/// не мешают. За море груз идёт только с эпохи кораблей и только между прибрежными поселениями.
/// </summary>
public sealed class EconomySystem : ISimulationSystem
{
    /// <summary>Раз во сколько тиков система просыпается.</summary>
    public const int TicksPerRun = 5;

    /// <summary>За сколько запусков осматривается вся карта.</summary>
    public const int SweepParts = 8;

    private readonly EconomyTable _table;
    private readonly TribeStore _tribes;
    private readonly SettlementStore _settlements;
    private readonly Territory _territory;
    private readonly TribeMarket _market;
    private readonly TradeNetwork _routes;
    private readonly int _goods;
    private readonly int[] _pendingTiles;
    private readonly float[] _pendingFertility;
    private readonly int[] _pendingResource;
    private readonly int[] _tiles;
    private readonly float[] _fertility;
    private readonly int[] _resource;
    private readonly float[] _need;
    private readonly int[] _available;
    private readonly int _width;
    private readonly int _height;
    private readonly int _rowsPerRun;

    private int _scanRow;
    private long _nextRebuild;
    private bool _seeded;

    public EconomySystem(
        EconomyTable table,
        TribeStore tribes,
        SettlementStore settlements,
        Territory territory,
        TribeMarket market,
        TradeNetwork routes)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(routes);

        if (market.Goods != table.Count)
        {
            throw new ArgumentException("Склад собран на другое число товаров, чем в таблице экономики.", nameof(market));
        }

        _table = table;
        _tribes = tribes;
        _settlements = settlements;
        _territory = territory;
        _market = market;
        _routes = routes;
        _goods = table.Count;
        _width = territory.Width;
        _height = territory.Height;
        _rowsPerRun = Math.Max(1, _height / SweepParts);

        // Партнёры хранятся битовой маской, поэтому торгуют первые 64 народа.
        Capacity = Math.Min(Math.Min(tribes.Capacity, market.Capacity), TribeMarket.MaxTracked);

        _pendingTiles = new int[Capacity];
        _pendingFertility = new float[Capacity];
        _pendingResource = new int[Capacity * _goods];
        _tiles = new int[Capacity];
        _fertility = new float[Capacity];
        _resource = new int[Capacity * _goods];
        _need = new float[Capacity * _goods];
        _available = new int[Capacity];
    }

    public string Name => "Экономика";

    public int Interval => TicksPerRun;

    /// <summary>Сколько народов система ведёт.</summary>
    public int Capacity { get; }

    /// <summary>Сколько товара проехало по всем путям в последний запуск.</summary>
    public float LastTradeVolume { get; private set; }

    /// <summary>Сколько народов голодало в последний запуск.</summary>
    public int HungryTribes { get; private set; }

    /// <summary>Сколько торговых путей в сети.</summary>
    public int RouteCount => _routes.Count;

    /// <summary>Самый богатый народ или 0, если богатства ни у кого нет.</summary>
    public short Richest { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        Scan(map);

        if (world.Tick >= _nextRebuild)
        {
            Rebuild(map);
            _nextRebuild = world.Tick + _table.Trade.RebuildTicks;
        }

        Produce();
        Consume();
        Spoil();
        UpdatePrices();
        RunTrade();
    }

    /// <summary>Осматривает полосу карты и копит землю, плодородие и жилы каждого народа.</summary>
    private void Scan(WorldMap map)
    {
        short[] owner = _territory.Owner;
        byte[] resourceAt = map.ResourceAt;
        float[] fertility = map.Fertility;
        int width = _width;
        int endRow = Math.Min(_height, _scanRow + _rowsPerRun);

        for (int y = _scanRow; y < endRow; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = row + x;
                short tribe = owner[index];
                if (tribe == TribeStore.None || (uint)tribe >= (uint)Capacity)
                {
                    continue;
                }

                _pendingTiles[tribe]++;
                _pendingFertility[tribe] += fertility[index];

                int resource = resourceAt[index];
                if (resource != (int)ResourceKind.None && resource < _goods)
                {
                    _pendingResource[(tribe * _goods) + resource]++;
                }
            }
        }

        _scanRow = endRow >= _height ? 0 : endRow;
        if (_scanRow == 0)
        {
            Publish();
        }
    }

    /// <summary>Перекладывает итоги полного круга в живые данные и заодно считает вместимость складов.</summary>
    private void Publish()
    {
        EconomyStorageSettings storage = _table.Storage;

        for (int t = 1; t < Capacity; t++)
        {
            _tiles[t] = _pendingTiles[t];
            _fertility[t] = _pendingFertility[t];
            _pendingTiles[t] = 0;
            _pendingFertility[t] = 0f;

            int first = t * _goods;
            for (int good = 0; good < _goods; good++)
            {
                _resource[first + good] = _pendingResource[first + good];
                _pendingResource[first + good] = 0;
            }

            float limit = (_tiles[t] * storage.PerTile) + (_tribes.Settlements[t] * storage.PerSettlement);
            _market.Limit[t] = MathF.Max(1f, limit);
        }

        if (!_seeded)
        {
            Seed();
            _seeded = true;
        }
    }

    /// <summary>На старте партии у народов есть запас еды, иначе первые же тики — голод.</summary>
    private void Seed()
    {
        float share = _table.Storage.StartFoodShare;
        if (share <= 0f)
        {
            return;
        }

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                continue;
            }

            _market.Stock[(t * _goods) + EconomyTable.Food] = _market.Limit[t] * share;
        }
    }

    /// <summary>Добыча со своей земли.</summary>
    private void Produce()
    {
        EconomyProductionSettings production = _table.Production;

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                if (_market.Limit[t] != 0f || _market.Wealth[t] != 0f || _market.Routes[t] != 0)
                {
                    _market.Reset(t);
                }

                continue;
            }

            int era = _tribes.Era[t];
            float bonus = 1f
                + (_tribes.Settlements[t] * production.SettlementBonus)
                + (era * production.EraBonus);
            int first = t * _goods;
            float limit = _market.Limit[t];

            // Плодородие суммируется как есть: тайл с плодородием 0.5 даёт половину еды.
            float food = ((_fertility[t] * production.FoodPerFertileTile)
                + (_tiles[t] * production.FoodPerTile)) * bonus;
            _market.Produced[first + EconomyTable.Food] = food;
            Store(first + EconomyTable.Food, food, limit);

            for (int good = 1; good < _goods; good++)
            {
                float mined = _table.CanExtract(good, era)
                    ? _resource[first + good] * production.ResourcePerTile * bonus
                    : 0f;
                _market.Produced[first + good] = mined;
                if (mined > 0f)
                {
                    Store(first + good, mined, limit);
                }
            }
        }
    }

    /// <summary>Еда людям и товары стройкам.</summary>
    private void Consume()
    {
        EconomyConsumptionSettings consumption = _table.Consumption;
        HungryTribes = 0;

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                continue;
            }

            int era = _tribes.Era[t];
            int first = t * _goods;
            int deficit = 0;
            int surplus = 0;

            float needFood = _tribes.People[t] * consumption.FoodPerPerson;
            _need[first + EconomyTable.Food] = needFood;

            int foodCell = first + EconomyTable.Food;
            float eaten = MathF.Min(_market.Stock[foodCell], needFood);
            _market.Stock[foodCell] -= eaten;
            _market.Consumed[foodCell] = eaten;
            _market.Famine[t] = needFood > 0f ? MathF.Max(0f, (needFood - eaten) / needFood) : 0f;
            if (_market.Famine[t] > 0f)
            {
                HungryTribes++;
                deficit |= 1 << EconomyTable.Food;
            }

            // Спрос на сырьё взвешен по своей земле. Раньше он делился между товарами эпохи
            // поровну, и урановая жила на четверти процента земли получала столько же спроса,
            // сколько повсеместный лес: склад редкого сырья съедался в том же прогоне, в котором
            // наполнялся, цена упиралась в потолок, а запас стоял в нуле. Добавка
            // DemandFloorTiles оставляет небольшой спрос и на то, чего в земле нет.
            float floor = consumption.DemandFloorTiles;
            float weight = 0f;
            for (int good = 1; good < _goods; good++)
            {
                if (_table.CanExtract(good, era))
                {
                    weight += _resource[first + good] + floor;
                }
            }

            float goodsNeed = (_tribes.People[t] * consumption.GoodsPerPerson)
                + (_tribes.Settlements[t] * consumption.GoodsPerSettlement);

            for (int good = 1; good < _goods; good++)
            {
                int cell = first + good;
                float need = 0f;
                if (weight > 0f && _table.CanExtract(good, era))
                {
                    need = goodsNeed * ((_resource[first + good] + floor) / weight);
                }

                _need[cell] = need;

                float used = MathF.Min(_market.Stock[cell], need);
                _market.Stock[cell] -= used;
                _market.Consumed[cell] = used;

                if (need > 0f && _market.Stock[cell] < need)
                {
                    deficit |= 1 << good;
                }
                else if (_market.Stock[cell] > need * _table.Price.CoverTarget)
                {
                    surplus |= 1 << good;
                }
            }

            if (_market.Stock[foodCell] > needFood * _table.Price.CoverTarget)
            {
                surplus |= 1 << EconomyTable.Food;
            }

            _market.Deficit[t] = deficit;
            _market.Surplus[t] = surplus;
        }
    }

    /// <summary>Часть запаса пропадает: еда портится, инструмент изнашивается.</summary>
    private void Spoil()
    {
        float keep = 1f - _table.Storage.SpoilShare;
        if (keep >= 1f)
        {
            return;
        }

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                continue;
            }

            int first = t * _goods;
            for (int good = 0; good < _goods; good++)
            {
                _market.Stock[first + good] *= keep;
            }
        }
    }

    /// <summary>Цена идёт вверх, когда запаса меньше нормы, и вниз при избытке.</summary>
    private void UpdatePrices()
    {
        EconomyPriceSettings price = _table.Price;

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                continue;
            }

            int first = t * _goods;
            for (int good = 0; good < _goods; good++)
            {
                int cell = first + good;
                float target = _need[cell] * price.CoverTarget;
                float cover = target > 0.0001f
                    ? _market.Stock[cell] / target
                    : (_market.Stock[cell] > 0f ? 4f : 1f);

                float scarcity = MathF.Pow(1f / MathF.Max(0.05f, cover), price.ScarcityWeight);
                scarcity = Math.Clamp(scarcity, price.Min, price.Max);
                float want = price.Base * _table.Value[good] * scarcity;

                float now = _market.Price[cell];
                _market.Price[cell] = now <= 0f ? want : now + ((want - now) * price.Smoothing);
            }
        }
    }

    /// <summary>Груз идёт по путям туда, где товар дороже.</summary>
    private void RunTrade()
    {
        EconomyTradeSettings trade = _table.Trade;
        LastTradeVolume = 0f;
        _routes.ClearFlow();

        for (int t = 1; t < Capacity; t++)
        {
            _market.Income[t] = 0f;
            _market.TradeResources[t] = 0;
            _available[t] = 0;

            if (!_tribes.Alive[t])
            {
                continue;
            }

            int first = t * _goods;
            for (int good = 1; good < _goods; good++)
            {
                if (_market.Stock[first + good] > 0f)
                {
                    _available[t] |= 1 << good;
                }
            }

            Array.Clear(_market.Imported, first, _goods);
            Array.Clear(_market.Exported, first, _goods);
        }

        for (int route = 0; route < _routes.Count; route++)
        {
            int a = _routes.FromTribe[route];
            int b = _routes.ToTribe[route];
            if ((uint)a >= (uint)Capacity || (uint)b >= (uint)Capacity
                || !_tribes.Alive[a] || !_tribes.Alive[b])
            {
                continue;
            }

            // Сам факт пути уже открывает доступ к чужим ресурсам для эпох.
            _market.TradeResources[a] |= _available[b];
            _market.TradeResources[b] |= _available[a];

            int firstA = a * _goods;
            int firstB = b * _goods;
            int bestGood = -1;
            float bestGap = 0f;
            int source = a;
            int destination = b;

            for (int good = 0; good < _goods; good++)
            {
                float priceA = _market.Price[firstA + good];
                float priceB = _market.Price[firstB + good];
                float gap = MathF.Abs(priceA - priceB);
                if (gap <= bestGap)
                {
                    continue;
                }

                int from = priceA < priceB ? a : b;
                int to = priceA < priceB ? b : a;
                if (_market.Stock[(from * _goods) + good] <= 0f)
                {
                    continue;
                }

                bestGood = good;
                bestGap = gap;
                source = from;
                destination = to;
            }

            if (bestGood < 0)
            {
                continue;
            }

            int sourceCell = (source * _goods) + bestGood;
            int destinationCell = (destination * _goods) + bestGood;

            // Свою половину нормы народ никогда не продаёт: иначе торговля убивала бы города.
            float reserve = _need[sourceCell] * _table.Price.CoverTarget * 0.5f;
            float spare = _market.Stock[sourceCell] - reserve;
            if (spare <= 0f)
            {
                continue;
            }

            float reach = 1f / (1f + (_routes.Length[route] / (float)Math.Max(1, trade.RangeTiles)));
            float free = MathF.Max(0f, _market.Limit[destination] - _market.Stock[destinationCell]);
            float amount = MathF.Min(trade.FlowCap * reach, spare * trade.FlowShare);
            amount = MathF.Min(amount, free);
            if (amount <= 0.0001f)
            {
                continue;
            }

            _market.Stock[sourceCell] -= amount;
            _market.Stock[destinationCell] += amount;
            _market.Exported[sourceCell] += amount;
            _market.Imported[destinationCell] += amount;

            float profit = amount * bestGap * trade.ProfitShare;
            _market.Wealth[source] += profit;
            _market.Income[source] += profit;
            _market.Wealth[destination] += profit * 0.5f;
            _market.Income[destination] += profit * 0.5f;

            _routes.Carry(route, bestGood, amount);
            LastTradeVolume += amount;
        }

        short richest = TribeStore.None;
        float top = 0f;
        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                continue;
            }

            _market.ResearchBonus[t] = MathF.Min(
                trade.ResearchBonusMax,
                _market.Routes[t] * trade.ResearchBonusPerRoute);

            if (_market.Wealth[t] > top)
            {
                top = _market.Wealth[t];
                richest = (short)t;
            }
        }

        Richest = richest;
    }

    /// <summary>Собирает сеть путей заново. Делается редко, потому что стоит квадрат от числа поселений.</summary>
    private void Rebuild(WorldMap map)
    {
        EconomyTradeSettings trade = _table.Trade;
        _routes.Clear();

        for (int t = 1; t < Capacity; t++)
        {
            _market.Routes[t] = 0;
            _market.Partners[t] = 0UL;
        }

        int landRange = trade.RangeTiles * trade.RangeTiles;
        int seaRange = trade.SeaRangeTiles * trade.SeaRangeTiles;
        int high = _settlements.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!_settlements.Alive[i])
            {
                continue;
            }

            int a = _settlements.Tribe[i];
            if ((uint)a >= (uint)Capacity || !_tribes.Alive[a])
            {
                continue;
            }

            for (int j = i + 1; j < high; j++)
            {
                if (!_settlements.Alive[j])
                {
                    continue;
                }

                int b = _settlements.Tribe[j];
                if ((uint)b >= (uint)Capacity || !_tribes.Alive[b] || a == b)
                {
                    continue;
                }

                int dx = _settlements.X[i] - _settlements.X[j];
                int dy = _settlements.Y[i] - _settlements.Y[j];
                int distance = (dx * dx) + (dy * dy);

                bool land = distance <= landRange;
                bool sea = !land
                    && distance <= seaRange
                    && _tribes.Era[a] >= trade.SeaEra
                    && _tribes.Era[b] >= trade.SeaEra
                    && IsCoastal(map, _settlements.X[i], _settlements.Y[i])
                    && IsCoastal(map, _settlements.X[j], _settlements.Y[j]);

                if (!land && !sea)
                {
                    continue;
                }

                if (!_routes.HasRoom)
                {
                    return;
                }

                _routes.Add(
                    i,
                    j,
                    (short)a,
                    (short)b,
                    _settlements.X[i],
                    _settlements.Y[i],
                    _settlements.X[j],
                    _settlements.Y[j],
                    (int)MathF.Round(MathF.Sqrt(distance)),
                    sea);

                _market.Routes[a]++;
                _market.Routes[b]++;
                _market.Partners[a] |= 1UL << b;
                _market.Partners[b] |= 1UL << a;
            }
        }
    }

    /// <summary>Стоит ли поселение у воды. Смотрим квадрат два на два вокруг.</summary>
    private static bool IsCoastal(WorldMap map, int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= map.Height)
            {
                continue;
            }

            for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= map.Width)
                {
                    continue;
                }

                var biome = (Biome)map.BiomeAt[(ny * map.Width) + nx];
                if (biome == Biome.Coast || biome == Biome.Beach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Store(int cell, float amount, float limit)
    {
        float total = _market.Stock[cell] + amount;
        _market.Stock[cell] = total > limit ? limit : total;
    }
}
