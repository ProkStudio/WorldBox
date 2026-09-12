namespace WorldBox.Core.Rulers;

/// <summary>Кем герой был. Порядок важен: по нему идут ключи перевода.</summary>
public enum HeroKind : byte
{
    /// <summary>Полководец: войско под ним идёт смелее.</summary>
    Commander = 0,

    /// <summary>Изобретатель: знание копится быстрее.</summary>
    Inventor = 1,

    /// <summary>Пророк: успокаивает города словом, а не стражей.</summary>
    Prophet = 2,

    /// <summary>Исследователь: приводит хлеб и новые земли.</summary>
    Explorer = 3,
}

/// <summary>Список видов героев: по нему их катает случай и переводит интерфейс.</summary>
public static class HeroKinds
{
    public const int Count = 4;

    private static readonly string[] Keys =
    {
        "hero.commander", "hero.inventor", "hero.prophet", "hero.explorer",
    };

    public static string NameKey(HeroKind kind) => Keys[(byte)kind % Count];

    public static HeroKind Of(int index) => (HeroKind)(byte)(((index % Count) + Count) % Count);
}

/// <summary>
/// Герои: полководцы, изобретатели, пророки и исследователи. Их мало и они смертны,
/// поэтому хранилище маленькое, а освободившиеся слоты переиспользуются.
///
/// Герой — не человечек на карте, а запись с бонусом народу, пока он жив.
/// </summary>
public sealed class HeroStore
{
    public const int DefaultCapacity = 32;

    public const int None = -1;

    private readonly int[] _free;

    private int _freeCount;
    private int _highWater;

    public HeroStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        Capacity = capacity;
        Name = new string[capacity];
        Kind = new byte[capacity];
        Tribe = new short[capacity];
        X = new int[capacity];
        Y = new int[capacity];
        Born = new long[capacity];
        BornYear = new float[capacity];
        Alive = new bool[capacity];
        _free = new int[capacity];

        Array.Fill(Name, string.Empty);
    }

    public int Capacity { get; }

    /// <summary>Сколько героев живо сейчас.</summary>
    public int Count { get; private set; }

    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    public string[] Name { get; }

    public byte[] Kind { get; }

    public short[] Tribe { get; }

    public int[] X { get; }

    public int[] Y { get; }

    public long[] Born { get; }

    /// <summary>Год рождения: по нему считается возраст, ведь лет в тике бывает разное число.</summary>
    public float[] BornYear { get; }

    public bool[] Alive { get; }

    public bool IsAlive(int index) => (uint)index < (uint)Capacity && Alive[index];

    public string NameOf(int index) => IsAlive(index) ? Name[index] : string.Empty;

    public HeroKind KindOf(int index) => IsAlive(index) ? (HeroKind)Kind[index] : HeroKind.Commander;

    /// <summary>Заводит героя и возвращает номер записи или -1, если мест нет.</summary>
    public int Add(string name, HeroKind kind, short tribe, int x, int y, long tick, float year)
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
            return None;
        }

        Name[index] = name ?? string.Empty;
        Kind[index] = (byte)kind;
        Tribe[index] = tribe;
        X[index] = x;
        Y[index] = y;
        Born[index] = tick;
        BornYear[index] = year;
        Alive[index] = true;
        Count++;
        return index;
    }

    public bool Remove(int index)
    {
        if (!IsAlive(index))
        {
            return false;
        }

        Alive[index] = false;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    /// <summary>Сколько живых героев у народа.</summary>
    public int CountOf(short tribe)
    {
        int count = 0;
        for (int i = 0; i < _highWater; i++)
        {
            if (Alive[i] && Tribe[i] == tribe)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Первый живой герой народа заданного вида или -1.</summary>
    public int FindOf(short tribe, HeroKind kind)
    {
        for (int i = 0; i < _highWater; i++)
        {
            if (Alive[i] && Tribe[i] == tribe && Kind[i] == (byte)kind)
            {
                return i;
            }
        }

        return None;
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

                hash = Mix(hash, Kind[i]);
                hash = Mix(hash, (ulong)(uint)Tribe[i]);
                hash = Mix(hash, (ulong)(uint)X[i]);
                hash = Mix(hash, (ulong)(uint)Y[i]);
                hash = Mix(hash, (ulong)Born[i]);
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
