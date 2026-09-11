namespace WorldBox.Core.People;

/// <summary>
/// Хранилище жителей. Массивы отдельных полей (SoA), фиксированная ёмкость, свободные
/// слоты лежат в стеке и переиспользуются. Плотность по тайлам обновляется сразу при
/// рождении, переезде и смерти, поэтому её не надо пересчитывать каждый тик.
/// Аллокаций во время симуляции нет вообще.
/// </summary>
public sealed class Population
{
    public const int DefaultCapacity = 20000;

    private readonly int[] _free;
    private readonly int[] _density;
    private readonly int _width;
    private readonly int _height;

    private int _freeCount;
    private int _highWater;

    public Population(int capacity, int mapWidth, int mapHeight)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        if (mapWidth <= 0 || mapHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapWidth), "Размер карты должен быть положительным.");
        }

        Capacity = capacity;
        _width = mapWidth;
        _height = mapHeight;

        X = new int[capacity];
        Y = new int[capacity];
        Age = new float[capacity];
        Hunger = new float[capacity];
        Tribe = new short[capacity];
        Alive = new bool[capacity];

        _free = new int[capacity];
        _density = new int[mapWidth * mapHeight];
    }

    public int Capacity { get; }

    public int Count { get; private set; }

    /// <summary>Верхняя граница занятых слотов. Цикл по жителям идёт до неё, а не до ёмкости.</summary>
    public int HighWater => _highWater;

    public bool HasRoom => Count < Capacity;

    public int[] X { get; }

    public int[] Y { get; }

    /// <summary>Возраст в игровых годах.</summary>
    public float[] Age { get; }

    /// <summary>Голод от 0 (сыт) до 1 (умирает).</summary>
    public float[] Hunger { get; }

    /// <summary>Номер народа. На срезе S3 у всех 0, племена появятся на S4.</summary>
    public short[] Tribe { get; }

    public bool[] Alive { get; }

    public int DensityAt(int tileIndex)
    {
        return (uint)tileIndex < (uint)_density.Length ? _density[tileIndex] : 0;
    }

    public int DensityAt(int x, int y)
    {
        return (uint)x < (uint)_width && (uint)y < (uint)_height ? _density[(y * _width) + x] : 0;
    }

    /// <summary>Возвращает номер слота или -1, если места нет либо координаты вне карты.</summary>
    public int Spawn(int x, int y, short tribe, float age)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
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
        Age[index] = age;
        Hunger[index] = 0f;
        Tribe[index] = tribe;
        Alive[index] = true;

        _density[(y * _width) + x]++;
        Count++;
        return index;
    }

    public bool Kill(int index)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return false;
        }

        Alive[index] = false;
        _density[(Y[index] * _width) + X[index]]--;
        _free[_freeCount++] = index;
        Count--;
        return true;
    }

    public void Move(int index, int x, int y)
    {
        if ((uint)index >= (uint)Capacity || !Alive[index])
        {
            return;
        }

        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
        {
            return;
        }

        int from = (Y[index] * _width) + X[index];
        int to = (y * _width) + x;
        if (from == to)
        {
            return;
        }

        _density[from]--;
        _density[to]++;
        X[index] = x;
        Y[index] = y;
    }

    public void Clear()
    {
        Array.Clear(Alive, 0, Alive.Length);
        Array.Clear(_density, 0, _density.Length);
        _freeCount = 0;
        _highWater = 0;
        Count = 0;
    }

    /// <summary>Грубая контрольная сумма. Нужна тесту детерминизма.</summary>
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

                hash = Mix(hash, (ulong)(uint)i);
                hash = Mix(hash, (ulong)(uint)X[i]);
                hash = Mix(hash, (ulong)(uint)Y[i]);
                hash = Mix(hash, (ulong)(uint)(int)(Age[i] * 16f));
                hash = Mix(hash, (ulong)(uint)(int)(Hunger[i] * 256f));
                hash = Mix(hash, (ulong)(uint)Tribe[i]);
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
