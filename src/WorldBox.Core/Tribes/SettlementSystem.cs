using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.World;

namespace WorldBox.Core.Tribes;

/// <summary>
/// Племена и их земли за один тик: где стало людно — появляется стоянка, стоянка растёт
/// в деревню и город, вокруг неё расширяются владения, опустевшее поселение исчезает и
/// отпускает землю. За тик обрабатывается только часть поселений, поэтому цена тика не
/// растёт вместе с картой. Аллокаций в тике нет: имена берутся из готовых списков.
/// </summary>
public sealed class SettlementSystem : ISimulationSystem
{
    private readonly TribeSettings _settings;

    /// <summary>Номер поселения плюс один по тайлам. Ноль — пусто.</summary>
    private readonly short[] _grid;

    private readonly int _width;
    private readonly int _height;

    private int _cursor;
    private int _sinceRecount;

    public SettlementSystem(
        Population people,
        TribeStore tribes,
        SettlementStore settlements,
        Territory territory,
        TribeSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(territory);

        People = people;
        Tribes = tribes;
        Settlements = settlements;
        Territory = territory;
        _settings = settings ?? TribeSettings.Default;
        _width = territory.Width;
        _height = territory.Height;
        _grid = new short[_width * _height];
        RebuildGrid();
    }

    public Population People { get; }

    public TribeStore Tribes { get; }

    public SettlementStore Settlements { get; }

    public Territory Territory { get; }

    public string Name => "Племена";

    public int LastFounded { get; private set; }

    public int LastAbandoned { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        LastFounded = 0;
        LastAbandoned = 0;

        UpdateSettlements(map);
        TryFound(world, map);

        _sinceRecount++;
        if (_sinceRecount >= _settings.RecountEvery)
        {
            _sinceRecount = 0;
            Recount();
        }
    }

    private void UpdateSettlements(WorldMap map)
    {
        int high = Settlements.HighWater;
        if (high == 0)
        {
            return;
        }

        int budget = Math.Min(_settings.ClaimPerTick, high);
        for (int n = 0; n < budget; n++)
        {
            if (_cursor >= high)
            {
                _cursor = 0;
            }

            int i = _cursor++;
            if (!Settlements.Alive[i])
            {
                continue;
            }

            int x = Settlements.X[i];
            int y = Settlements.Y[i];
            short tribe = Settlements.Tribe[i];

            int locals = CountLocals(x, y, _settings.LocalRadius);
            Settlements.People[i] = locals;

            if (locals < _settings.AbandonBelow)
            {
                Abandon(i);
                continue;
            }

            byte level = locals >= _settings.TownAt
                ? SettlementStore.Town
                : locals >= _settings.VillageAt ? SettlementStore.Village : SettlementStore.Camp;

            byte radius = level == SettlementStore.Town
                ? _settings.TownRadius
                : level == SettlementStore.Village ? _settings.VillageRadius : _settings.CampRadius;

            Settlements.Level[i] = level;
            Settlements.Radius[i] = radius;
            ClaimAround(map, x, y, radius, tribe);
        }
    }

    private void TryFound(WorldState world, WorldMap map)
    {
        int high = People.HighWater;
        if (high == 0 || !Settlements.HasRoom)
        {
            return;
        }

        Rng rng = world.Rng;
        for (int n = 0; n < _settings.FoundAttempts; n++)
        {
            if (!Settlements.HasRoom)
            {
                return;
            }

            int person = rng.NextInt(high);
            if (!People.Alive[person])
            {
                continue;
            }

            short tribe = People.Tribe[person];
            if (tribe == TribeStore.None || !Tribes.IsAlive(tribe))
            {
                continue;
            }

            int x = People.X[person];
            int y = People.Y[person];
            int index = (y * _width) + x;

            if (!map.IsLand(index)
                || map.Fertility[index] < _settings.FoundFertility
                || People.DensityAt(index) < _settings.FoundDensity
                || HasNeighborSettlement(x, y, _settings.MinDistance))
            {
                continue;
            }

            int slot = Settlements.Found(x, y, tribe, TribeNames.Place(rng), world.Tick);
            if (slot < 0)
            {
                return;
            }

            Settlements.Radius[slot] = _settings.CampRadius;
            Settlements.People[slot] = People.DensityAt(index);
            _grid[index] = (short)(slot + 1);
            Tribes.Settlements[tribe]++;
            ClaimAround(map, x, y, _settings.CampRadius, tribe);
            LastFounded++;
        }
    }

    private int CountLocals(int centerX, int centerY, int radius)
    {
        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(_width - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(_height - 1, centerY + radius);
        int total = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;
            for (int x = minX; x <= maxX; x++)
            {
                total += People.DensityAt(row + x);
            }
        }

        return total;
    }

    /// <summary>Занимает свободную сушу в круге. Чужие тайлы не отбирает: войны будут позже.</summary>
    private void ClaimAround(WorldMap map, int centerX, int centerY, int radius, short tribe)
    {
        int limit = radius * radius;
        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(_width - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(_height - 1, centerY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + (dy * dy) > limit)
                {
                    continue;
                }

                int index = row + x;
                if (!map.IsLand(index))
                {
                    continue;
                }

                short owner = Territory.Owner[index];
                if (owner != TribeStore.None && owner != tribe)
                {
                    continue;
                }

                Territory.Claim(index, tribe);
            }
        }
    }

    private bool HasNeighborSettlement(int centerX, int centerY, int distance)
    {
        int minX = Math.Max(0, centerX - distance);
        int maxX = Math.Min(_width - 1, centerX + distance);
        int minY = Math.Max(0, centerY - distance);
        int maxY = Math.Min(_height - 1, centerY + distance);

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;
            for (int x = minX; x <= maxX; x++)
            {
                if (_grid[row + x] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Abandon(int index)
    {
        short tribe = Settlements.Tribe[index];
        int x = Settlements.X[index];
        int y = Settlements.Y[index];
        int radius = Settlements.Radius[index];

        Territory.Release(tribe, x, y, radius);

        if ((uint)x < (uint)_width && (uint)y < (uint)_height)
        {
            _grid[(y * _width) + x] = 0;
        }

        if (Tribes.IsAlive(tribe) && Tribes.Settlements[tribe] > 0)
        {
            Tribes.Settlements[tribe]--;
        }

        Settlements.Remove(index);
        LastAbandoned++;
    }

    /// <summary>Полный пересчёт народов. Идёт раз в RecountEvery тиков, не каждый тик.</summary>
    private void Recount()
    {
        Array.Clear(Tribes.People, 0, Tribes.People.Length);
        Array.Clear(Tribes.Settlements, 0, Tribes.Settlements.Length);

        int high = People.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!People.Alive[i])
            {
                continue;
            }

            short tribe = People.Tribe[i];
            if ((uint)tribe < (uint)Tribes.Capacity && Tribes.Alive[tribe])
            {
                Tribes.People[tribe]++;
            }
        }

        int settlements = Settlements.HighWater;
        for (int i = 0; i < settlements; i++)
        {
            if (!Settlements.Alive[i])
            {
                continue;
            }

            short tribe = Settlements.Tribe[i];
            if ((uint)tribe < (uint)Tribes.Capacity && Tribes.Alive[tribe])
            {
                Tribes.Settlements[tribe]++;
            }
        }

        // Народ без единого живого человека вымер. Его города сами не опустеют: CountLocals
        // считает всех людей в радиусе, не разбирая народа, и мёртвое поселение вечно живёт
        // за счёт чужого населения и держит землю. Бросаем такие поселения сами.
        for (int i = 0; i < settlements; i++)
        {
            if (!Settlements.Alive[i])
            {
                continue;
            }

            short owner = Settlements.Tribe[i];
            if ((uint)owner < (uint)Tribes.Capacity && Tribes.Alive[owner] && Tribes.People[owner] == 0)
            {
                Abandon(i);
            }
        }

        for (short tribe = 1; tribe < Tribes.Capacity; tribe++)
        {
            if (Tribes.Alive[tribe] && Tribes.People[tribe] == 0)
            {
                Territory.ReleaseAll(tribe);
                Tribes.Remove(tribe);
            }
        }
    }

    private void RebuildGrid()
    {
        Array.Clear(_grid, 0, _grid.Length);

        int high = Settlements.HighWater;
        for (int i = 0; i < high; i++)
        {
            if (!Settlements.Alive[i])
            {
                continue;
            }

            int x = Settlements.X[i];
            int y = Settlements.Y[i];
            if ((uint)x < (uint)_width && (uint)y < (uint)_height)
            {
                _grid[(y * _width) + x] = (short)(i + 1);
            }
        }
    }
}
