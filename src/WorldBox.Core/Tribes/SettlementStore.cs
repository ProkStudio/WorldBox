namespace WorldBox.Core.Tribes;

/// <summary>
/// Поселения: стоянка, деревня, город. Хранилище устроено как у людей —
/// массивы полей, фиксированная ёмкость, освободившиеся слоты переиспользуются.
/// </summary>
public sealed class SettlementStore
{
    public const int DefaultCapacity = 512;

    public const byte Camp = 0;
    public const byte Village = 1;
    public const byte Town = 2;

    private readonly int[] _free;

    private int _freeCount;
    private int _highWater;

    public SettlementStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        Capacity = capacity;
        X = new int[capacity];
        Y = new int[capacity];
        Tribe = new short[capacity];
        People = new int[capacity];
        Level = new byte[capacity];
        Radius = new byte[capacity];
        Founded = new long[capacity];
        Name = new string[capacity];
        Food = new float[capacity];
        Siege = new float[capacity];
        Unrest = new float[capacity];
        Origin = new short[capacity];
        Captured = new long[capacity];
        Alive = new bool[capacity];
        _free = new int[capacity];
    }

    public int Capacity { get; }

    public int Count { get; private set; }

    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    public int[] X { get; }

    public int[] Y { get; }

    public short[] Tribe { get; }

    /// <summary>Сколько людей живёт вокруг. Считает система племён, не каждый тик.</summary>
    public int[] People { get; }

    public byte[] Level { get; }

    /// <summary>Радиус владений в тайлах. Растёт вместе с уровнем.</summary>
    public byte[] Radius { get; }

    public long[] Founded { get; }

    public string[] Name { get; }

    /// <summary>Запас еды в городе. Город без запаса сдаётся осаждающему от голода.</summary>
    public float[] Food { get; }

    /// <summary>Ход осады от 0 до 1. Дошёл до единицы — город взят.</summary>
    public float[] Siege { get; }

    /// <summary>Недовольство от 0 до 1: взятые города бунтуют.</summary>
    public float[] Unrest { get; }

    /// <summary>Кто основал город. Бунт возвращает его прежнему владельцу.</summary>
    public short[] Origin { get; }

    /// <summary>На каком тике город взяли силой. Ноль — никогда.</summary>
    public long[] Captured { get; }

    public bool[] Alive { get; }

    /// <summary>Возвращает номер слота или -1, если мест нет.</summary>
    public int Found(int x, int y, short tribe, string name, long tick)
    {
        int index;
        if (_freeCount > 0)
        {
            index = _free[--_freeCount];
        }
        else if (_highWater < Capacity)
        {
            index = _highWater++;
        }
        else
        {
            return -1;
        }

        X[index] = x;
        Y[index] = y;
        Tribe[index] = tribe;
        People[index] = 0;
        Level[index] = Camp;
        Radius[index] = 1;
        Founded[index] = tick;
        Name[index] = name;
        Food[index] = 0f;
        Siege[index] = 0f;
        Unrest[index] = 0f;
        Origin[index] = tribe;
        Captured[index] = 0L;
        Alive[index] = true;
        Count++;
        return index;
    }

    public bool Remove(int index)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return false;
        }

        Alive[index] = false;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    public void Clear()
    {
        Array.Clear(Alive, 0, Alive.Length);
        _freeCount = 0;
        _highWater = 0;
        Count = 0;
    }

    /// <summary>Грубая контрольная сумма для теста детерминизма.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)Count);

            for (int i = 0; i < _highWater; i++)
            {
                if (!Alive[i])
                {
                    continue;
                }

                hash = Mix(hash, (ulong)(uint)X[i]);
                hash = Mix(hash, (ulong)(uint)Y[i]);
                hash = Mix(hash, (ulong)(uint)Tribe[i]);
                hash = Mix(hash, Level[i]);
                hash = Mix(hash, (ulong)(uint)People[i]);
                hash = Mix(hash, (ulong)(long)MathF.Round(Food[i]));
                hash = Mix(hash, (ulong)(long)MathF.Round(Unrest[i] * 100f));
            }

            return hash;
        }
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        unchecked
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }
}
