using WorldBox.Core.Simulation;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

namespace WorldBox.Core.Roads;

/// <summary>
/// Строители дорог. Каждые BuildEvery тиков берётся один город, ищутся ближайшие
/// соседи — свои и чужие — и между ними кладётся дорога по самому дешёвому пути:
/// по ровному и сухому, в обход гор, с переправой через реку, но не по морю. Шаг по готовой
/// дороге стоит дешевле, поэтому новые маршруты липнут к старым и из отдельных линий
/// сама собой вырастает сеть с трактами и развязками.
///
/// Цена тика не зависит от размера мира: за прогон строится не больше RoutesPerRun дорог,
/// поиск идёт только в окне между двумя городами и сдаётся после MaxExpansions тайлов.
/// Память под поиск выделена заранее, чистится только тронутое, случайности нет вообще.
/// </summary>
public sealed class RoadSystem : ISimulationSystem
{
    private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private readonly RoadSettings _settings;
    private readonly int _width;
    private readonly int _height;

    /// <summary>Цена пути до тайла.</summary>
    private readonly int[] _cost;

    /// <summary>Откуда пришли в тайл.</summary>
    private readonly int[] _from;

    /// <summary>0 — не видели, 1 — в очереди, 2 — разобран.</summary>
    private readonly byte[] _state;

    private readonly int[] _touched;
    private readonly int[] _heapTile;
    private readonly int[] _heapScore;
    private readonly int[] _path;
    private readonly int[] _candidate;
    private readonly int[] _candidateScore;

    private int _touchedCount;
    private int _heapCount;
    private int _cursor;
    private int _upkeepCursor;
    private int _sinceRun;

    public RoadSystem(
        TribeStore tribes,
        SettlementStore settlements,
        RoadNetwork roads,
        RoadSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(roads);

        Tribes = tribes;
        Settlements = settlements;
        Roads = roads;
        _settings = settings ?? RoadSettings.Default;
        _width = roads.Width;
        _height = roads.Height;

        int tiles = _width * _height;
        _cost = new int[tiles];
        _from = new int[tiles];
        _state = new byte[tiles];
        _touched = new int[tiles];
        _heapTile = new int[Math.Min(tiles * 4, 1 << 16)];
        _heapScore = new int[_heapTile.Length];
        _path = new int[RoadNetwork.MaxPathLength];

        int links = Math.Max(1, _settings.LinksPerSettlement);
        _candidate = new int[links];
        _candidateScore = new int[links];
    }

    public TribeStore Tribes { get; }

    public SettlementStore Settlements { get; }

    public RoadNetwork Roads { get; }

    public string Name => "Дороги";

    public int Interval => 1;

    /// <summary>Сколько дорог проложено в последнем прогоне.</summary>
    public int LastBuilt { get; private set; }

    /// <summary>Сколько маршрутов подняло уровень полотна.</summary>
    public int LastUpgraded { get; private set; }

    /// <summary>Сколько маршрутов снято: город на конце исчез.</summary>
    public int LastRemoved { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        LastBuilt = 0;
        LastUpgraded = 0;
        LastRemoved = 0;

        _sinceRun++;
        if (_sinceRun < _settings.BuildEvery)
        {
            return;
        }

        _sinceRun = 0;
        Upkeep();
        Build(map);
    }

    /// <summary>Проверка готовых маршрутов: снять бесхозные, поднять уровень по эпохе.</summary>
    private void Upkeep()
    {
        int checks = Math.Min(_settings.UpkeepPerRun, Roads.Count);

        for (int n = 0; n < checks; n++)
        {
            if (Roads.Count == 0)
            {
                return;
            }

            if (_upkeepCursor >= Roads.Count)
            {
                _upkeepCursor = 0;
            }

            int route = _upkeepCursor;
            int from = Roads.FromSettlement[route];
            int to = Roads.ToSettlement[route];

            bool standing = (uint)from < (uint)Settlements.Capacity
                && (uint)to < (uint)Settlements.Capacity
                && Settlements.Alive[from]
                && Settlements.Alive[to]
                && Settlements.X[from] == Roads.FromX[route]
                && Settlements.Y[from] == Roads.FromY[route]
                && Settlements.X[to] == Roads.ToX[route]
                && Settlements.Y[to] == Roads.ToY[route];

            if (!standing)
            {
                // Маршрут ушёл, но полотно осталось: заброшенная дорога видна на земле.
                // Курсор не двигаем: на этом месте теперь лежит другой маршрут.
                Roads.Remove(route);
                LastRemoved++;
                continue;
            }

            byte level = LevelFor(from, to);
            if (level > Roads.RouteLevel[route])
            {
                Roads.Stamp(route, level);
                LastUpgraded++;
            }

            _upkeepCursor++;
        }
    }

    private void Build(WorldMap map)
    {
        int high = Settlements.HighWater;
        if (high == 0 || !Roads.HasRoom)
        {
            return;
        }

        int built = 0;
        int tries = 0;

        while (built < _settings.RoutesPerRun && tries < high)
        {
            tries++;

            if (_cursor >= high)
            {
                _cursor = 0;
            }

            int from = _cursor++;
            if (!Settlements.Alive[from])
            {
                continue;
            }

            short tribe = Settlements.Tribe[from];
            if (!Tribes.IsAlive(tribe) || Roads.LinksOf(from) >= _settings.LinksPerSettlement)
            {
                continue;
            }

            int found = Neighbors(from, high);
            for (int n = 0; n < found; n++)
            {
                int to = _candidate[n];
                if (Roads.Connects(from, to))
                {
                    continue;
                }

                if (!Roads.HasRoom)
                {
                    return;
                }

                int length = FindPath(map, from, to);
                if (length < 2)
                {
                    continue;
                }

                byte level = LevelFor(from, to);
                int route = Roads.Add(from, to, tribe, Settlements.Tribe[to], level, _path, length);
                if (route < 0)
                {
                    return;
                }

                Roads.Stamp(route, level);
                LastBuilt++;
                built++;

                if (built >= _settings.RoutesPerRun)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Ближайшие города. Свои идут вперёд чужих: дорога внутри страны нужнее.</summary>
    private int Neighbors(int from, int high)
    {
        int links = _candidate.Length;
        int fx = Settlements.X[from];
        int fy = Settlements.Y[from];
        short tribe = Settlements.Tribe[from];
        int max = _settings.MaxDistance;
        int found = 0;

        for (int other = 0; other < high; other++)
        {
            if (other == from || !Settlements.Alive[other])
            {
                continue;
            }

            short otherTribe = Settlements.Tribe[other];
            if (!Tribes.IsAlive(otherTribe))
            {
                continue;
            }

            int dx = Math.Abs(Settlements.X[other] - fx);
            int dy = Math.Abs(Settlements.Y[other] - fy);
            int distance = Math.Max(dx, dy);
            if (distance > max)
            {
                continue;
            }

            int score = distance + (otherTribe == tribe ? 0 : _settings.ForeignPenalty);
            if (found == links && score >= _candidateScore[links - 1])
            {
                continue;
            }

            int at = found < links ? found : links - 1;
            while (at > 0 && _candidateScore[at - 1] > score)
            {
                _candidateScore[at] = _candidateScore[at - 1];
                _candidate[at] = _candidate[at - 1];
                at--;
            }

            _candidateScore[at] = score;
            _candidate[at] = other;
            if (found < links)
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Какое полотно положено этой паре городов: берётся старшая эпоха из двух.</summary>
    private byte LevelFor(int from, int to)
    {
        short first = Settlements.Tribe[from];
        short second = Settlements.Tribe[to];
        int era = 0;

        if (Tribes.IsAlive(first))
        {
            era = Tribes.Era[first];
        }

        if (Tribes.IsAlive(second))
        {
            era = Math.Max(era, Tribes.Era[second]);
        }

        return RoadNetwork.LevelFor(era, from, to);
    }

    /// <summary>Ищет самый дешый путь по суше. Возвращает длину пути в _path или 0.</summary>
    private int FindPath(WorldMap map, int fromSettlement, int toSettlement)
    {
        int sx = Settlements.X[fromSettlement];
        int sy = Settlements.Y[fromSettlement];
        int gx = Settlements.X[toSettlement];
        int gy = Settlements.Y[toSettlement];
        int start = (sy * _width) + sx;
        int goal = (gy * _width) + gx;

        if (start == goal || !map.IsLand(start) || !map.IsLand(goal))
        {
            return 0;
        }

        int pad = _settings.SearchPad;
        int minX = Math.Max(0, Math.Min(sx, gx) - pad);
        int maxX = Math.Min(_width - 1, Math.Max(sx, gx) + pad);
        int minY = Math.Max(0, Math.Min(sy, gy) - pad);
        int maxY = Math.Min(_height - 1, Math.Max(sy, gy) + pad);

        ResetSearch();
        _cost[start] = 0;
        Touch(start);
        _state[start] = 1;
        Push(start, Heuristic(sx, sy, gx, gy));

        int expansions = 0;

        while (_heapCount > 0)
        {
            int current = Pop();
            if (_state[current] == 2)
            {
                continue;
            }

            _state[current] = 2;

            if (current == goal)
            {
                return Trace(start, goal);
            }

            expansions++;
            if (expansions > _settings.MaxExpansions)
            {
                break;
            }

            int cx = current % _width;
            int cy = current / _width;
            int currentCost = _cost[current];

            for (int d = 0; d < 8; d++)
            {
                int nx = cx + StepX[d];
                int ny = cy + StepY[d];
                if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                {
                    continue;
                }

                int next = (ny * _width) + nx;
                if (_state[next] == 2)
                {
                    continue;
                }

                int step = StepCost(map, current, next, d >= 4);
                if (step < 0)
                {
                    continue;
                }

                int candidate = currentCost + step;
                if (_state[next] == 1 && candidate >= _cost[next])
                {
                    continue;
                }

                _cost[next] = candidate;
                _from[next] = current;

                if (_state[next] == 0)
                {
                    if (!Touch(next))
                    {
                        return 0;
                    }

                    _state[next] = 1;
                }

                Push(next, candidate + Heuristic(nx, ny, gx, gy));
            }
        }

        return 0;
    }

    /// <summary>Цена шага в десятых единицы. Отрицательное — туда дорогу не ведут.</summary>
    private int StepCost(WorldMap map, int from, int next, bool diagonal)
    {
        var biome = (Biome)map.BiomeAt[next];
        if (Biomes.IsWater(biome) || biome == Biome.Peak || biome == Biome.Glacier)
        {
            return -1;
        }

        int cost = (int)(Biomes.Info(biome).MoveCost * 10f);
        if (cost < 1)
        {
            cost = 1;
        }

        if (diagonal)
        {
            cost = (cost * 14) / 10;
        }

        // Подъём дорого: дорога сама обходит крутые склоны и тянется долинами.
        float rise = MathF.Abs(map.Elevation[next] - map.Elevation[from]);
        cost += (int)(rise * _settings.SlopePenalty);

        // Шаг по готовой дороге дешевле: маршруты липнут в общие тракты.
        if (Roads.Level[next] != RoadNetwork.NoRoad)
        {
            cost = (cost * _settings.RoadDiscount) / 10;
            if (cost < 1)
            {
                cost = 1;
            }
        }

        return cost;
    }

    /// <summary>Оценка остатка пути. Занижена намеренно: иначе поиск нашёл бы не самый дешёвый путь.</summary>
    private static int Heuristic(int x, int y, int goalX, int goalY)
    {
        int dx = Math.Abs(goalX - x);
        int dy = Math.Abs(goalY - y);
        return 4 * Math.Max(dx, dy);
    }

    /// <summary>Собирает путь от конца к началу и переворачивает его.</summary>
    private int Trace(int start, int goal)
    {
        int length = 0;
        int node = goal;

        while (true)
        {
            if (length >= _path.Length)
            {
                return 0;
            }

            _path[length++] = node;
            if (node == start)
            {
                break;
            }

            node = _from[node];
        }

        for (int i = 0, j = length - 1; i < j; i++, j--)
        {
            int swap = _path[i];
            _path[i] = _path[j];
            _path[j] = swap;
        }

        return length;
    }

    private void ResetSearch()
    {
        for (int i = 0; i < _touchedCount; i++)
        {
            _state[_touched[i]] = 0;
        }

        _touchedCount = 0;
        _heapCount = 0;
    }

    private bool Touch(int tile)
    {
        if (_touchedCount >= _touched.Length)
        {
            return false;
        }

        _touched[_touchedCount++] = tile;
        return true;
    }

    private void Push(int tile, int score)
    {
        if (_heapCount >= _heapTile.Length)
        {
            return;
        }

        int i = _heapCount++;
        _heapTile[i] = tile;
        _heapScore[i] = score;

        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (!Less(i, parent))
            {
                break;
            }

            Swap(i, parent);
            i = parent;
        }
    }

    private int Pop()
    {
        int top = _heapTile[0];
        int last = --_heapCount;
        _heapTile[0] = _heapTile[last];
        _heapScore[0] = _heapScore[last];
        int i = 0;

        while (true)
        {
            int left = (i * 2) + 1;
            if (left >= _heapCount)
            {
                break;
            }

            int right = left + 1;
            int best = right < _heapCount && Less(right, left) ? right : left;
            if (!Less(best, i))
            {
                break;
            }

            Swap(i, best);
            i = best;
        }

        return top;
    }

    /// <summary>При равной цене первым идёт тайл с меньшим номером: путь не зависит от порядка обхода.</summary>
    private bool Less(int first, int second)
    {
        int a = _heapScore[first];
        int b = _heapScore[second];
        return a < b || (a == b && _heapTile[first] < _heapTile[second]);
    }

    private void Swap(int first, int second)
    {
        int tile = _heapTile[first];
        int score = _heapScore[first];
        _heapTile[first] = _heapTile[second];
        _heapScore[first] = _heapScore[second];
        _heapTile[second] = tile;
        _heapScore[second] = score;
    }
}
