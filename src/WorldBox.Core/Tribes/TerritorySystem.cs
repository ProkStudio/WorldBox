using WorldBox.Core.Simulation;
using WorldBox.Core.World;

namespace WorldBox.Core.Tribes;

/// <summary>
/// Границы держав. Поселение занимает только круг вокруг себя, поэтому карта выглядела
/// набором цветных пятен: между городами оставалась ничейная земля, а края владений были
/// рваными. Эта система двигает границу дальше: свободная суша достаётся тому соседу, чьей
/// земли рядом больше, а одинокий лоскут внутри чужой страны переходит хозяину окружения.
/// Так владения смыкаются в цельные страны с общей границей.
///
/// Карта обходится полосами по GrowRows строк за тик, поэтому цена тика не зависит от размера
/// мира. Решения складываются в буфер и применяются в конце тика: иначе только что занятый
/// тайл тянул бы за собой соседа в той же строке и страны росли бы полосами вправо.
/// Случайность здесь не используется вообще: один и тот же мир каждый раз делится одинаково.
/// </summary>
public sealed class TerritorySystem : ISimulationSystem
{
    private readonly TribeSettings _settings;
    private readonly int[] _pendingIndex;
    private readonly short[] _pendingTribe;
    private readonly int _width;
    private readonly int _height;
    private readonly int _rows;

    private int _cursorRow;
    private int _pending;

    public TerritorySystem(TribeStore tribes, Territory territory, TribeSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(territory);

        Tribes = tribes;
        Territory = territory;
        _settings = settings ?? TribeSettings.Default;
        _width = territory.Width;
        _height = territory.Height;
        _rows = Math.Clamp(_settings.GrowRows, 1, _height);
        _pendingIndex = new int[_rows * _width];
        _pendingTribe = new short[_rows * _width];
    }

    public TribeStore Tribes { get; }

    public Territory Territory { get; }

    public string Name => "Земли";

    public int Interval => 1;

    /// <summary>Сколько ничейных тайлов заняли державы в последнем тике.</summary>
    public int LastGrown { get; private set; }

    /// <summary>Сколько лоскутов сменило хозяина при сглаживании границы.</summary>
    public int LastSmoothed { get; private set; }

    public void Tick(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        WorldMap? map = world.Map;
        if (map == null)
        {
            return;
        }

        LastGrown = 0;
        LastSmoothed = 0;
        _pending = 0;

        for (int n = 0; n < _rows; n++)
        {
            ScanRow(map, _cursorRow);
            _cursorRow++;
            if (_cursorRow >= _height)
            {
                _cursorRow = 0;
            }
        }

        for (int i = 0; i < _pending; i++)
        {
            Territory.Claim(_pendingIndex[i], _pendingTribe[i]);
        }
    }

    private void ScanRow(WorldMap map, int y)
    {
        short[] owner = Territory.Owner;
        byte[] core = Territory.Core;
        int row = y * _width;

        for (int x = 0; x < _width; x++)
        {
            int index = row + x;
            if (!map.IsLand(index))
            {
                continue;
            }

            short mine = owner[index];
            short best = Vote(owner, x, y, index, out int votes);
            if (best == TribeStore.None || best == mine)
            {
                continue;
            }

            if (mine == TribeStore.None)
            {
                if (CanHold(best) && Queue(index, best))
                {
                    LastGrown++;
                }

                continue;
            }

            // Своя земля уходит соседу только если он обступил тайл с трёх сторон: это срезает
            // зубцы на границе, но целую область без войны не отбирает. Землю под самим
            // поселением (ядро) сглаживание не трогает никогда.
            if (core[index] == 0 && votes >= _settings.SmoothNeed && CanHold(best) && Queue(index, best))
            {
                LastSmoothed++;
            }
        }
    }

    /// <summary>Кто из соседей по четырём сторонам сильнее всего окружает тайл.</summary>
    private short Vote(short[] owner, int x, int y, int index, out int votes)
    {
        short n0 = x > 0 ? owner[index - 1] : TribeStore.None;
        short n1 = x < _width - 1 ? owner[index + 1] : TribeStore.None;
        short n2 = y > 0 ? owner[index - _width] : TribeStore.None;
        short n3 = y < _height - 1 ? owner[index + _width] : TribeStore.None;

        short best = TribeStore.None;
        int bestVotes = 0;

        Consider(n0, n0, n1, n2, n3, ref best, ref bestVotes);
        Consider(n1, n0, n1, n2, n3, ref best, ref bestVotes);
        Consider(n2, n0, n1, n2, n3, ref best, ref bestVotes);
        Consider(n3, n0, n1, n2, n3, ref best, ref bestVotes);

        votes = bestVotes;
        return best;
    }

    private void Consider(
        short candidate,
        short n0,
        short n1,
        short n2,
        short n3,
        ref short best,
        ref int bestVotes)
    {
        if (candidate == TribeStore.None || !Tribes.IsAlive(candidate))
        {
            return;
        }

        int votes = (n0 == candidate ? 1 : 0)
            + (n1 == candidate ? 1 : 0)
            + (n2 == candidate ? 1 : 0)
            + (n3 == candidate ? 1 : 0);

        // При равенстве голосов побеждает меньший номер народа: итог не зависит от порядка обхода.
        if (votes > bestVotes || (votes == bestVotes && best != TribeStore.None && candidate < best))
        {
            best = candidate;
            bestVotes = votes;
        }
    }

    /// <summary>Можно ли народу взять ещё землю: пустая империя из трёх человек не растёт.</summary>
    private bool CanHold(short tribe)
    {
        long limit = ((long)Tribes.People[tribe] * _settings.TilesPerPerson) + _settings.BaseTiles;
        return Territory.Tiles[tribe] < limit;
    }

    private bool Queue(int index, short tribe)
    {
        if (_pending >= _pendingIndex.Length)
        {
            return false;
        }

        _pendingIndex[_pending] = index;
        _pendingTribe[_pending] = tribe;
        _pending++;
        return true;
    }
}
