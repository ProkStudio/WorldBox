namespace WorldBox.Core.Society;

/// <summary>
/// Культуры — идентичность народа: название, цвет, язык. Язык нужен не для красоты:
/// из него берутся слоги названий религий и новых культур, так что родня звучит похоже.
///
/// Устроено как остальные хранилища: массивы полей, фиксированная ёмкость, слоты переиспользуются.
/// </summary>
public sealed class CultureStore
{
    public const int DefaultCapacity = 32;

    /// <summary>Культуры нет: поселение или народ ещё не описан.</summary>
    public const short None = -1;

    private readonly short[] _free;

    private int _freeCount;
    private int _highWater;

    public CultureStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        if (capacity > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Культур не может быть больше 32767.");
        }

        Capacity = capacity;
        Name = new string[capacity];
        ColorIndex = new byte[capacity];
        Language = new byte[capacity];
        Founded = new long[capacity];
        People = new int[capacity];
        Settlements = new int[capacity];
        Alive = new bool[capacity];
        _free = new short[capacity];
    }

    public int Capacity { get; }

    public int Count { get; private set; }

    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    public string[] Name { get; }

    /// <summary>Цвет культуры в палитре народов: им красится слой культур.</summary>
    public byte[] ColorIndex { get; }

    /// <summary>Номер условного языка из <see cref="SocietyNames"/>.</summary>
    public byte[] Language { get; }

    public long[] Founded { get; }

    /// <summary>Сколько людей относит себя к этой культуре.</summary>
    public int[] People { get; }

    /// <summary>В скольких поселениях культура главная.</summary>
    public int[] Settlements { get; }

    public bool[] Alive { get; }

    /// <summary>Заводит культуру и возвращает номер слота или <see cref="None"/>, если мест нет.</summary>
    public short Found(string name, byte colorIndex, byte language, long tick)
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
        Language[index] = language;
        Founded[index] = tick;
        People[index] = 0;
        Settlements[index] = 0;
        Alive[index] = true;
        Count++;
        return index;
    }

    /// <summary>Культура исчезла: последние её города ассимилированы или вырезаны.</summary>
    public bool Remove(short index)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return false;
        }

        Alive[index] = false;
        People[index] = 0;
        Settlements[index] = 0;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    public bool IsAlive(short index) => (uint)index < (uint)Capacity && Alive[index];

    public string NameOf(short index) => IsAlive(index) ? Name[index] ?? string.Empty : string.Empty;

    public byte LanguageOf(short index) => IsAlive(index) ? Language[index] : (byte)0;

    /// <summary>Самая многочисленная культура мира.</summary>
    public short Largest()
    {
        short best = None;
        int bestPeople = -1;
        for (int i = 0; i < _highWater; i++)
        {
            if (!Alive[i] || People[i] <= bestPeople)
            {
                continue;
            }

            best = (short)i;
            bestPeople = People[i];
        }

        return best;
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

                hash = Mix(hash, ColorIndex[i]);
                hash = Mix(hash, Language[i]);
                hash = Mix(hash, (ulong)(uint)People[i]);
                hash = Mix(hash, (ulong)(uint)Settlements[i]);
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
