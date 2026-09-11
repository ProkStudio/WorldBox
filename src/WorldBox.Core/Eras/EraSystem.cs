using System.Numerics;
using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

namespace WorldBox.Core.Eras;

/// <summary>
/// Развитие народов по эпохам. За один запуск система делает четыре вещи:
/// смотрит полосу карты и собирает, что у народа есть на земле и с кем он граничит;
/// считает доступ к ресурсам через соседей и торговлю; двигает эпохи вперёд или назад;
/// выставляет сжатие времени по самому развитому народу.
///
/// Карта обходится по частям: за один запуск смотрим полосу строк, а результат публикуем
/// только после полного круга. Иначе на карте 1024 на 1024 один тик врезался бы в кадр.
/// Аллокаций в тике нет, случайность только из world.Rng.
///
/// Если передан <see cref="ITradeAccess"/>, к доступу по границе добавляются товары,
/// которые народ получает караванами, и торговый бонус к исследованиям. Без него
/// система работает как раньше: чужая медь доступна только через общую границу.
/// </summary>
public sealed class EraSystem : ISimulationSystem
{
    /// <summary>Раз во сколько тиков система просыпается.</summary>
    public const int TicksPerRun = 10;

    /// <summary>За сколько запусков осматривается вся карта.</summary>
    public const int SweepParts = 8;

    private readonly EraTable _table;
    private readonly TribeStore _tribes;
    private readonly Territory _territory;
    private readonly TribeTech _tech;
    private readonly ITradeAccess? _trade;
    private readonly float[] _perAgent;
    private readonly int[] _pendingResources;
    private readonly int[] _pendingFertile;
    private readonly int[] _pendingRiver;
    private readonly int[] _pendingCoast;
    private readonly int[] _pendingMountain;
    private readonly int[] _pendingTiles;
    private readonly ulong[] _pendingContacts;
    private readonly int _width;
    private readonly int _height;
    private readonly int _rowsPerRun;

    private int _scanRow;

    /// <param name="table">Таблица эпох из data/eras.json.</param>
    /// <param name="tribes">Народы мира.</param>
    /// <param name="territory">Владения: по ним считается земля народа.</param>
    /// <param name="tech">Технологическое состояние народов.</param>
    /// <param name="trade">Откуда брать торговый доступ к чужим ресурсам. Необязателен.</param>
    public EraSystem(
        EraTable table,
        TribeStore tribes,
        Territory territory,
        TribeTech tech,
        ITradeAccess? trade = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(tech);

        if (table.Count == 0)
        {
            throw new ArgumentException("Таблица эпох пустая.", nameof(table));
        }

        _table = table;
        _tribes = tribes;
        _territory = territory;
        _tech = tech;
        _trade = trade;
        _width = territory.Width;
        _height = territory.Height;
        _rowsPerRun = Math.Max(1, _height / SweepParts);

        // Соседи хранятся битовой маской, поэтому развиваются первые 64 народа.
        Capacity = Math.Min(Math.Min(tribes.Capacity, tech.Capacity), TribeTech.MaxTracked);

        _pendingResources = new int[Capacity];
        _pendingFertile = new int[Capacity];
        _pendingRiver = new int[Capacity];
        _pendingCoast = new int[Capacity];
        _pendingMountain = new int[Capacity];
        _pendingTiles = new int[Capacity];
        _pendingContacts = new ulong[Capacity];

        _perAgent = new float[table.Count];
        for (int era = 0; era < table.Count; era++)
        {
            _perAgent[era] = EraRules.PerAgent(table, era);
        }
    }

    public string Name => "Эпохи";

    public int Interval => TicksPerRun;

    /// <summary>Сколько народов система ведёт.</summary>
    public int Capacity { get; }

    /// <summary>Эпоха самого развитого живого народа. По ней считается сжатие времени.</summary>
    public byte TopEra { get; private set; }

    /// <summary>Сколько переходов случилось в последний запуск.</summary>
    public int LastAdvances { get; private set; }

    /// <summary>Сколько из них подсмотрено у соседей.</summary>
    public int LastLearned { get; private set; }

    /// <summary>Сколько народов откатилось назад.</summary>
    public int LastCollapses { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        LastAdvances = 0;
        LastLearned = 0;
        LastCollapses = 0;

        Scan(map);
        UpdateTrade();
        Advance(world);

        world.YearsPerTick = _table.YearsPerTickOf(TopEra);
    }

    /// <summary>Осматривает полосу карты и копит данные о земле и соседях.</summary>
    private void Scan(WorldMap map)
    {
        short[] owner = _territory.Owner;
        byte[] resourceAt = map.ResourceAt;
        byte[] biomeAt = map.BiomeAt;
        float[] fertility = map.Fertility;
        float fertileValue = _table.Geo.FertileValue;
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

                int resource = resourceAt[index];
                if (resource != (int)ResourceKind.None)
                {
                    _pendingResources[tribe] |= 1 << resource;
                }

                if (fertility[index] >= fertileValue)
                {
                    _pendingFertile[tribe]++;
                }

                var biome = (Biome)biomeAt[index];
                if (biome == Biome.River || biome == Biome.Lake || biome == Biome.Marsh)
                {
                    _pendingRiver[tribe]++;
                }
                else if (biome == Biome.Beach || biome == Biome.Coast)
                {
                    _pendingCoast[tribe]++;
                }
                else if (biome == Biome.Mountain || biome == Biome.Peak)
                {
                    _pendingMountain[tribe]++;
                }

                // Достаточно смотреть вправо и вниз: обратная пара запишется сама.
                if (x + 1 < width)
                {
                    Link(tribe, owner[index + 1]);
                }

                if (y + 1 < _height)
                {
                    Link(tribe, owner[index + width]);
                }
            }
        }

        _scanRow = endRow >= _height ? 0 : endRow;
        if (_scanRow == 0)
        {
            Publish();
        }
    }

    private void Link(short a, short b)
    {
        if (b == TribeStore.None || b == a || (uint)b >= (uint)Capacity)
        {
            return;
        }

        _pendingContacts[a] |= 1UL << b;
        _pendingContacts[b] |= 1UL << a;
    }

    /// <summary>Перекладывает итоги полного круга в живые данные и обнуляет счётчики.</summary>
    private void Publish()
    {
        EraGeoSettings geo = _table.Geo;

        for (int t = 1; t < Capacity; t++)
        {
            byte features = 0;
            if (_pendingFertile[t] >= geo.FertileTiles)
            {
                features |= (byte)GeoFeature.Fertile;
            }

            if (_pendingRiver[t] >= geo.RiverTiles)
            {
                features |= (byte)GeoFeature.River;
            }

            if (_pendingCoast[t] >= geo.CoastTiles)
            {
                features |= (byte)GeoFeature.Coast;
            }

            if (_pendingMountain[t] >= geo.MountainTiles)
            {
                features |= (byte)GeoFeature.Mountain;
            }

            _tech.OwnResources[t] = _pendingResources[t];
            _tech.Geo[t] = features;
            _tech.Contacts[t] = _pendingContacts[t];
            _tech.Tiles[t] = _pendingTiles[t];

            _pendingResources[t] = 0;
            _pendingFertile[t] = 0;
            _pendingRiver[t] = 0;
            _pendingCoast[t] = 0;
            _pendingMountain[t] = 0;
            _pendingTiles[t] = 0;
            _pendingContacts[t] = 0UL;
        }
    }

    /// <summary>
    /// Что народ может выменять. Граница даёт доступ к ресурсам соседа, а торговые пути —
    /// к ресурсам партнёров, до которых идут караваны и корабли. Маска собирается каждый
    /// запуск заново, поэтому потеря пути честно закрывает доступ к чужому олову.
    /// </summary>
    private void UpdateTrade()
    {
        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                _tech.TradeResources[t] = 0;
                continue;
            }

            ulong contacts = _tech.Contacts[t];
            int mask = 0;
            while (contacts != 0UL)
            {
                int neighbor = BitOperations.TrailingZeroCount(contacts);
                contacts &= contacts - 1UL;
                if ((uint)neighbor < (uint)Capacity && _tribes.Alive[neighbor])
                {
                    mask |= _tech.OwnResources[neighbor];
                }
            }

            if (_trade != null)
            {
                mask |= _trade.TradeResourcesOf(t);
            }

            _tech.TradeResources[t] = mask;
        }
    }

    private void Advance(WorldState world)
    {
        Rng rng = world.Rng;
        byte top = 0;

        for (int t = 1; t < Capacity; t++)
        {
            if (!_tribes.Alive[t])
            {
                if (_tech.Tiles[t] != 0 || _tech.Progress[t] != 0f || _tech.Contacts[t] != 0UL)
                {
                    _tech.Reset(t);
                }

                continue;
            }

            byte era = _tribes.Era[t];
            if (era > _table.Last)
            {
                era = (byte)_table.Last;
                _tribes.Era[t] = era;
            }

            long headcount = EraRules.Headcount(_perAgent[era], _tribes.People[t]);
            _tech.Headcount[t] = headcount;

            if (TryCollapse(world, t, era, headcount))
            {
                era = _tribes.Era[t];
            }
            else if (world.Tick < _tech.DarkAgeUntil[t])
            {
                _tech.Block[t] = (byte)EraBlock.DarkAge;
            }
            else
            {
                Step(world, rng, t, era, headcount);
                era = _tribes.Era[t];
            }

            if (era > top)
            {
                top = era;
            }
        }

        TopEra = top;
    }

    /// <summary>Один шаг развития одного народа.</summary>
    private void Step(WorldState world, Rng rng, int tribe, byte era, long headcount)
    {
        int access = _tech.OwnResources[tribe] | _tech.TradeResources[tribe];
        EraBlock block = EraRules.Evaluate(
            _table,
            era,
            headcount,
            access,
            _tech.Geo[tribe],
            out int missingResources,
            out byte missingGeo);

        _tech.MissingResources[tribe] = missingResources;
        _tech.MissingGeo[tribe] = missingGeo;
        _tech.Block[tribe] = (byte)block;

        if (block != EraBlock.Ready)
        {
            return;
        }

        int next = era + 1;

        // Сосед, который уже живёт в этой эпохе, может научить сразу — так отставшие догоняют.
        float teach = TeachChance(tribe, next);
        if (teach > 0f && rng.Chance(MathF.Min(1f, teach * TicksPerRun)))
        {
            Promote(world, tribe, next);
            LastLearned++;
            return;
        }

        _tech.Progress[tribe] += TicksPerRun * ResearchRate(tribe, next);
        if (_tech.Progress[tribe] >= EraRules.Cost(_table, next))
        {
            Promote(world, tribe, next);
        }
    }

    /// <summary>Шанс за тик перенять эпоху у соседа.</summary>
    private float TeachChance(int tribe, int nextEra)
    {
        ulong contacts = _tech.Contacts[tribe];
        if (contacts == 0UL)
        {
            return _table.Diffusion.Isolated;
        }

        int need = _table.RequiredResources[nextEra];
        int own = _tech.OwnResources[tribe];
        float best = 0f;

        while (contacts != 0UL)
        {
            int neighbor = BitOperations.TrailingZeroCount(contacts);
            contacts &= contacts - 1UL;

            if ((uint)neighbor >= (uint)Capacity || !_tribes.Alive[neighbor] || _tribes.Era[neighbor] < nextEra)
            {
                continue;
            }

            // Сосед, без которого нечего плавить, учит быстрее простого соседа по границе.
            bool trades = (need & ~own & _tech.OwnResources[neighbor]) != 0;
            float chance = trades ? _table.Diffusion.TradePartner : _table.Diffusion.BorderNeighbor;
            if (chance > best)
            {
                best = chance;
            }
        }

        return best;
    }

    /// <summary>
    /// Сколько очков знания народ даёт за тик. Торговля прибавляется отдельным слагаемым:
    /// караваны везут не только медь, но и чужие приёмы, поэтому торговые державы
    /// обгоняют изолированные даже без общей границы.
    /// </summary>
    private float ResearchRate(int tribe, int nextEra)
    {
        EraResearchSettings research = _table.Research;
        float rate = 1f + (_tribes.Settlements[tribe] * research.SettlementBonus);
        float trade = _trade?.ResearchBonusOf(tribe) ?? 0f;

        ulong contacts = _tech.Contacts[tribe];
        if (contacts == 0UL)
        {
            return (rate * research.IsolatedPenalty) + trade;
        }

        int ahead = 0;
        while (contacts != 0UL)
        {
            int neighbor = BitOperations.TrailingZeroCount(contacts);
            contacts &= contacts - 1UL;
            if ((uint)neighbor < (uint)Capacity && _tribes.Alive[neighbor] && _tribes.Era[neighbor] >= nextEra)
            {
                ahead++;
            }
        }

        return rate + (ahead * research.NeighborBonus) + trade;
    }

    private void Promote(WorldState world, int tribe, int nextEra)
    {
        if (nextEra > _table.Last)
        {
            return;
        }

        _tribes.Era[tribe] = (byte)nextEra;
        _tech.Progress[tribe] = 0f;
        _tech.EraChangedTick[tribe] = world.Tick;
        _tech.FoodMultiplier[tribe] = FoodMultiplier(nextEra);
        _tech.Block[tribe] = (byte)EraBlock.Ready;
        LastAdvances++;
    }

    /// <summary>Народ, который не удержал население, теряет эпоху и уходит в тёмные века.</summary>
    private bool TryCollapse(WorldState world, int tribe, byte era, long headcount)
    {
        if (era == 0)
        {
            return false;
        }

        var hold = (long)(_table.MinPop[era] * _table.Collapse.PopShareToHold);
        if (headcount >= hold)
        {
            return false;
        }

        int fallen = Math.Max(0, era - Math.Max(1, _table.Collapse.ErasLost));
        _tribes.Era[tribe] = (byte)fallen;
        _tech.Progress[tribe] = 0f;
        _tech.FoodMultiplier[tribe] = FoodMultiplier(fallen);
        _tech.EraChangedTick[tribe] = world.Tick;
        _tech.Block[tribe] = (byte)EraBlock.DarkAge;

        float yearsPerTick = world.YearsPerTick > 0f ? world.YearsPerTick : 1f;
        _tech.DarkAgeUntil[tribe] = world.Tick + (long)(_table.Collapse.DarkAgeYears / yearsPerTick);
        LastCollapses++;
        return true;
    }

    /// <summary>Во сколько раз эпоха кормит лучше нулевой.</summary>
    private float FoodMultiplier(int era)
    {
        float start = _table.FoodPerWorker[0];
        return start <= 0f ? 1f : _table.FoodPerWorker[Math.Clamp(era, 0, _table.Last)] / start;
    }
}
