namespace WorldBox.Core.Society;

/// <summary>
/// Религии: название, цвет знака, откуда пошла, догматы и сколько у неё людей.
/// У раскола есть родитель: так видно, что вера не придумана заново, а отошла от старой.
///
/// Хранилище устроено как у отрядов и поселений: массивы полей, фиксированная ёмкость,
/// освободившиеся слоты переиспользуются. Аллокаций в тике нет, кроме самого названия.
/// </summary>
public sealed class ReligionStore
{
    public const int DefaultCapacity = 48;

    /// <summary>Веры нет: у поселения или народа религия ещё не сложилась.</summary>
    public const short None = -1;

    private readonly short[] _free;

    private int _freeCount;
    private int _highWater;

    public ReligionStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        if (capacity > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Религий не может быть больше 32767.");
        }

        Capacity = capacity;
        Name = new string[capacity];
        ColorIndex = new byte[capacity];
        Origin = new short[capacity];
        Parent = new short[capacity];
        Founded = new long[capacity];
        Followers = new int[capacity];
        Settlements = new int[capacity];
        Dogmas = new byte[capacity];
        Alive = new bool[capacity];
        _free = new short[capacity];
    }

    public int Capacity { get; }

    public int Count { get; private set; }

    /// <summary>Верхняя граница занятых слотов: цикл идёт до неё, а не до ёмкости.</summary>
    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    /// <summary>Сколько вер родилось за партию, считая исчезшие.</summary>
    public int Born { get; private set; }

    public string[] Name { get; }

    /// <summary>Цвет знака веры в палитре народов: им красится слой религий на карте.</summary>
    public byte[] ColorIndex { get; }

    /// <summary>У какого народа вера родилась.</summary>
    public short[] Origin { get; }

    /// <summary>От какой веры отошла эта, или <see cref="None"/> у первородной.</summary>
    public short[] Parent { get; }

    public long[] Founded { get; }

    /// <summary>Сколько людей исповедует веру. Считается раз в прогон, не каждый тик.</summary>
    public int[] Followers { get; }

    /// <summary>В скольких поселениях вера главная.</summary>
    public int[] Settlements { get; }

    /// <summary>Догматы веры, уложенные в байт флагов <see cref="Dogma"/>.</summary>
    public byte[] Dogmas { get; }

    public bool[] Alive { get; }

    /// <summary>Основывает веру и возвращает номер слота или <see cref="None"/>, если мест нет.</summary>
    public short Found(string name, byte colorIndex, short origin, short parent, Dogma dogmas, long tick)
    {
        short index;
        if (_freeCount > 0)
        {
            index = _free[--_freeCount];
        }
        else if (_highWater < Capacity)
        {
            index = (short)_highWater;
            _highWater++;
        }
        else
        {
            return None;
        }

        Name[index] = name;
        ColorIndex[index] = colorIndex;
        Origin[index] = origin;
        Parent[index] = parent;
        Founded[index] = tick;
        Followers[index] = 0;
        Settlements[index] = 0;
        Dogmas[index] = (byte)dogmas;
        Alive[index] = true;
        Count++;
        Born++;
        return index;
    }

    /// <summary>Вера угасла: последний город с ней пал или перешёл к чужому богу.</summary>
    public bool Remove(short index)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return false;
        }

        Alive[index] = false;
        Followers[index] = 0;
        Settlements[index] = 0;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    public bool IsAlive(short index) => (uint)index < (uint)Capacity && Alive[index];

    public string NameOf(short index) => IsAlive(index) ? Name[index] ?? string.Empty : string.Empty;

    public Dogma DogmasOf(short index) => IsAlive(index) ? (Dogma)Dogmas[index] : Dogma.None;

    /// <summary>Самая многочисленная вера мира. Нужна интерфейсу, в горячем цикле не зовётся.</summary>
    public short Largest()
    {
        short best = None;
        int bestFollowers = -1;
        for (int i = 0; i < _highWater; i++)
        {
            if (!Alive[i] || Followers[i] <= bestFollowers)
            {
                continue;
            }

            best = (short)i;
            bestFollowers = Followers[i];
        }

        return best;
    }

    /// <summary>Сколько вер родилось расколом, а не сами по себе.</summary>
    public int SchismCount()
    {
        int total = 0;
        for (int i = 0; i < _highWater; i++)
        {
            if (Alive[i] && Parent[i] != None)
            {
                total++;
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
        Born = 0;
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

                hash = Mix(hash, ColorIndex[i]);
                hash = Mix(hash, (ulong)(uint)Origin[i]);
                hash = Mix(hash, (ulong)(uint)Parent[i]);
                hash = Mix(hash, (ulong)(uint)Followers[i]);
                hash = Mix(hash, (ulong)(uint)Settlements[i]);
                hash = Mix(hash, Dogmas[i]);
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
