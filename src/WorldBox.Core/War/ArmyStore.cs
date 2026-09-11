namespace WorldBox.Core.War;

/// <summary>
/// Армии — отряды, а не отдельные человечки: численность, мораль, снабжение и цель.
/// Эпоха вооружения не дублируется: она берётся у народа, иначе при смене эпохи
/// старые отряды навсегда оставались бы с каменными топорами.
///
/// Хранилище устроено как у людей и поселений: массивы полей, фиксированная ёмкость,
/// освободившиеся слоты переиспользуются. Аллокаций в тике нет.
/// </summary>
public sealed class ArmyStore
{
    public const int DefaultCapacity = 256;

    /// <summary>Цели нет: отряд стоит на месте.</summary>
    public const int NoTarget = -1;

    private readonly int[] _free;

    private int _freeCount;
    private int _highWater;

    public ArmyStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        Capacity = capacity;
        X = new int[capacity];
        Y = new int[capacity];
        Tribe = new short[capacity];
        Men = new int[capacity];
        Morale = new float[capacity];
        Supply = new float[capacity];
        Target = new int[capacity];
        Alive = new bool[capacity];
        _free = new int[capacity];
    }

    public int Capacity { get; }

    public int Count { get; private set; }

    /// <summary>Верхняя граница занятых слотов: цикл идёт до неё, а не до ёмкости.</summary>
    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    public int[] X { get; }

    public int[] Y { get; }

    public short[] Tribe { get; }

    /// <summary>Сколько воинов в отряде.</summary>
    public int[] Men { get; }

    /// <summary>Мораль от 0 до 1.</summary>
    public float[] Morale { get; }

    /// <summary>Снабжение от 0 до 1: на своей земле растёт, на чужой падает.</summary>
    public float[] Supply { get; }

    /// <summary>Номер чужого поселения, к которому идёт отряд, или <see cref="NoTarget"/>.</summary>
    public int[] Target { get; }

    public bool[] Alive { get; }

    /// <summary>Поднимает отряд и возвращает номер слота или -1, если мест нет.</summary>
    public int Raise(int x, int y, short tribe, int men, float morale)
    {
        if (men <= 0)
        {
            return -1;
        }

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
        Men[index] = men;
        Morale[index] = morale;
        Supply[index] = 1f;
        Target[index] = NoTarget;
        Alive[index] = true;
        Count++;
        return index;
    }

    /// <summary>Отряд расходится: разбит, обескровлен или война кончилась.</summary>
    public bool Disband(int index)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return false;
        }

        Alive[index] = false;
        Men[index] = 0;
        Target[index] = NoTarget;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    /// <summary>Сколько отрядов у народа. Нужно интерфейсу и тестам, в горячем цикле не зовётся.</summary>
    public int CountOf(short tribe)
    {
        int total = 0;
        for (int i = 0; i < _highWater; i++)
        {
            if (Alive[i] && Tribe[i] == tribe)
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>Сколько воинов всего под ружьём у народа.</summary>
    public int MenOf(short tribe)
    {
        int total = 0;
        for (int i = 0; i < _highWater; i++)
        {
            if (Alive[i] && Tribe[i] == tribe)
            {
                total += Men[i];
            }
        }

        return total;
    }

    public void Clear()
    {
        Array.Clear(Alive, 0, Alive.Length);
        _freeCount = 0;
        _highWater = 0;
        Count = 0;
    }

    /// <summary>Грубая контрольная сумма для теста повторимости.</summary>
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
                hash = Mix(hash, (ulong)(uint)Men[i]);
                hash = Mix(hash, (ulong)(long)MathF.Round(Morale[i] * 100f));
                hash = Mix(hash, (ulong)(long)MathF.Round(Supply[i] * 100f));
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
