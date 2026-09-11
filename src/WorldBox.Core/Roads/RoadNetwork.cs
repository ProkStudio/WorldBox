namespace WorldBox.Core.Roads;

/// <summary>
/// Дороги мира. Уровень полотна лежит по тайлам — один байт на тайл, а маршруты между
/// поселениями — отдельным списком. По тайлам рисуется сама дорога, по маршрутам ездит
/// транспорт. Полотно не исчезает вместе с маршрутом: брошенная дорога остаётся на земле,
/// как и в жизни, и потом её подбирает следующий город.
///
/// Путь каждого маршрута живёт в общем буфере с постоянным шагом MaxPathLength: так удаление
/// маршрута не требует сжатия всего буфера, а аллокаций во время игры нет вообще.
/// Счётчик изменений нужен рисованию: пока он не вырос, текстуру дорог пересобирать незачем.
/// </summary>
public sealed class RoadNetwork
{
    /// <summary>Дороги нет.</summary>
    public const byte NoRoad = 0;

    /// <summary>Тропа: каменный век и неолит, люди ходят пешком.</summary>
    public const byte Trail = 1;

    /// <summary>Грунтовая дорога с колеями: бронза и железо, пошли телеги.</summary>
    public const byte Paved = 2;

    /// <summary>Тракт, мощёный камнем: средние века и порох, по нему идут фургоны.</summary>
    public const byte Highway = 3;

    /// <summary>Железная дорога: индустрия и дальше, по ней ходят поезда.</summary>
    public const byte Rail = 4;

    /// <summary>Шоссе с разметкой: современность и космос, по нему едут машины.</summary>
    public const byte Motorway = 5;

    /// <summary>Сколько всего уровней полотна вместе с «дороги нет».</summary>
    public const int LevelCount = 6;

    public const int DefaultCapacity = 384;

    /// <summary>Самый длинный путь в тайлах. Длиннее — города считаются несоседями.</summary>
    public const int MaxPathLength = 160;

    private readonly int _width;
    private readonly int _height;

    public RoadNetwork(int width, int height, int capacity = DefaultCapacity)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер карты должен быть положительным.");
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Нужен хотя бы один слот маршрута.");
        }

        _width = width;
        _height = height;
        Capacity = capacity;
        Level = new byte[width * height];
        FromSettlement = new int[capacity];
        ToSettlement = new int[capacity];
        FromTribe = new short[capacity];
        ToTribe = new short[capacity];
        FromX = new int[capacity];
        FromY = new int[capacity];
        ToX = new int[capacity];
        ToY = new int[capacity];
        RouteLevel = new byte[capacity];
        PathLength = new int[capacity];
        Path = new int[capacity * MaxPathLength];
    }

    public int Width => _width;

    public int Height => _height;

    public int Capacity { get; }

    /// <summary>Сколько маршрутов живо сейчас.</summary>
    public int Count { get; private set; }

    public bool HasRoom => Count < Capacity;

    /// <summary>Уровень дороги по тайлам.</summary>
    public byte[] Level { get; }

    /// <summary>Сколько тайлов занято дорогой.</summary>
    public int Tiles { get; private set; }

    /// <summary>Растёт при любом изменении полотна.</summary>
    public int Version { get; private set; }

    public int[] FromSettlement { get; }

    public int[] ToSettlement { get; }

    public short[] FromTribe { get; }

    public short[] ToTribe { get; }

    public int[] FromX { get; }

    public int[] FromY { get; }

    public int[] ToX { get; }

    public int[] ToY { get; }

    /// <summary>Уровень, до которого маршрут уже доведён.</summary>
    public byte[] RouteLevel { get; }

    /// <summary>Сколько тайлов в пути маршрута.</summary>
    public int[] PathLength { get; }

    /// <summary>Общий буфер путей: маршрут n лежит с n * MaxPathLength.</summary>
    public int[] Path { get; }

    /// <summary>Где в общем буфере начинается путь маршрута.</summary>
    public static int PathStart(int route) => route * MaxPathLength;

    /// <summary>Какое полотно положено народу этой эпохи.</summary>
    /// <remarks>
    /// С индустрии половина направлений становится железной дорогой, остальные остаются
    /// шоссе: иначе либо везде рельсы, либо нигде. Выбор по номерам городов — без случайности,
    /// чтобы один и тот же мир выглядел одинаково.
    /// </remarks>
    public static byte LevelFor(int era, int fromSettlement, int toSettlement)
    {
        if (era <= 1)
        {
            return Trail;
        }

        if (era <= 4)
        {
            return Paved;
        }

        if (era <= 6)
        {
            return Highway;
        }

        bool rail = ((fromSettlement + toSettlement) & 1) == 0;
        if (era <= 8)
        {
            return rail ? Rail : Highway;
        }

        return rail ? Rail : Motorway;
    }

    public byte LevelAt(int index) => (uint)index < (uint)Level.Length ? Level[index] : NoRoad;

    public byte LevelAt(int x, int y)
    {
        return (uint)x < (uint)_width && (uint)y < (uint)_height ? Level[(y * _width) + x] : NoRoad;
    }

    /// <summary>Поднимает уровень тайла. Вниз не понижает: шоссе не становится тропой.</summary>
    public bool Upgrade(int index, byte level)
    {
        if ((uint)index >= (uint)Level.Length || level == NoRoad)
        {
            return false;
        }

        byte current = Level[index];
        if (current >= level)
        {
            return false;
        }

        if (current == NoRoad)
        {
            Tiles++;
        }

        Level[index] = level;
        Version++;
        return true;
    }

    /// <summary>Заводит маршрут с готовым путём. Возвращает номер или -1.</summary>
    public int Add(
        int fromSettlement,
        int toSettlement,
        short fromTribe,
        short toTribe,
        byte level,
        int[] path,
        int pathLength)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Count >= Capacity || pathLength < 2 || pathLength > MaxPathLength || pathLength > path.Length)
        {
            return -1;
        }

        int route = Count++;
        Array.Copy(path, 0, Path, PathStart(route), pathLength);
        PathLength[route] = pathLength;
        FromSettlement[route] = fromSettlement;
        ToSettlement[route] = toSettlement;
        FromTribe[route] = fromTribe;
        ToTribe[route] = toTribe;

        int first = path[0];
        int last = path[pathLength - 1];
        FromX[route] = first % _width;
        FromY[route] = first / _width;
        ToX[route] = last % _width;
        ToY[route] = last / _width;
        RouteLevel[route] = level;
        Version++;
        return route;
    }

    /// <summary>Кладёт полотно вдоль пути. Возвращает число изменённых тайлов.</summary>
    public int Stamp(int route, byte level)
    {
        if ((uint)route >= (uint)Count)
        {
            return 0;
        }

        int start = PathStart(route);
        int length = PathLength[route];
        int changed = 0;

        for (int i = 0; i < length; i++)
        {
            if (Upgrade(Path[start + i], level))
            {
                changed++;
            }
        }

        if (RouteLevel[route] < level)
        {
            RouteLevel[route] = level;
        }

        return changed;
    }

    /// <summary>Связаны ли два поселения живым маршрутом.</summary>
    public bool Connects(int firstSettlement, int secondSettlement)
    {
        for (int i = 0; i < Count; i++)
        {
            int from = FromSettlement[i];
            int to = ToSettlement[i];
            if ((from == firstSettlement && to == secondSettlement)
                || (from == secondSettlement && to == firstSettlement))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Сколько маршрутов выходит из поселения.</summary>
    public int LinksOf(int settlement)
    {
        int links = 0;
        for (int i = 0; i < Count; i++)
        {
            if (FromSettlement[i] == settlement || ToSettlement[i] == settlement)
            {
                links++;
            }
        }

        return links;
    }

    /// <summary>Снимает маршрут. Полотно на земле остаётся.</summary>
    public bool Remove(int route)
    {
        if ((uint)route >= (uint)Count)
        {
            return false;
        }

        int last = Count - 1;
        if (route != last)
        {
            FromSettlement[route] = FromSettlement[last];
            ToSettlement[route] = ToSettlement[last];
            FromTribe[route] = FromTribe[last];
            ToTribe[route] = ToTribe[last];
            FromX[route] = FromX[last];
            FromY[route] = FromY[last];
            ToX[route] = ToX[last];
            ToY[route] = ToY[last];
            RouteLevel[route] = RouteLevel[last];
            PathLength[route] = PathLength[last];
            Array.Copy(Path, PathStart(last), Path, PathStart(route), PathLength[last]);
        }

        PathLength[last] = 0;
        Count--;
        Version++;
        return true;
    }

    /// <summary>Стирает и маршруты, и полотно. Зовётся только перед новым миром.</summary>
    public void Clear()
    {
        Array.Clear(Level, 0, Level.Length);
        Array.Clear(PathLength, 0, PathLength.Length);
        Count = 0;
        Tiles = 0;
        Version++;
    }

    /// <summary>Грубая контрольная сумма для теста на повторимость.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)Count);
            hash = Mix(hash, (ulong)Tiles);

            for (int i = 0; i < Count; i++)
            {
                hash = Mix(hash, (ulong)(uint)FromX[i]);
                hash = Mix(hash, (ulong)(uint)FromY[i]);
                hash = Mix(hash, (ulong)(uint)ToX[i]);
                hash = Mix(hash, (ulong)(uint)ToY[i]);
                hash = Mix(hash, RouteLevel[i]);
                hash = Mix(hash, (ulong)(uint)PathLength[i]);
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
