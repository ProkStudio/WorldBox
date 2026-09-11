namespace WorldBox.Core.Economy;

/// <summary>
/// Торговые пути между поселениями разных народов.
/// Сеть собирается редко (раз в несколько сотен тиков), а грузы по ней едут каждый прогон экономики.
/// Концы пути хранятся вместе с координатами: рисовалке тогда не нужен склад поселений,
/// а если поселение исчезло, путь умрёт на ближайшей сборке сети.
/// </summary>
public sealed class TradeNetwork
{
    public const int DefaultCapacity = 512;

    public TradeNetwork(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Нужен хотя бы один слот пути.");
        }

        Capacity = capacity;
        FromSettlement = new int[capacity];
        ToSettlement = new int[capacity];
        FromTribe = new short[capacity];
        ToTribe = new short[capacity];
        FromX = new int[capacity];
        FromY = new int[capacity];
        ToX = new int[capacity];
        ToY = new int[capacity];
        Length = new int[capacity];
        Sea = new bool[capacity];
        Good = new int[capacity];
        Volume = new float[capacity];
        Total = new float[capacity];
    }

    /// <summary>Сколько путей помещается.</summary>
    public int Capacity { get; }

    /// <summary>Сколько путей живо сейчас.</summary>
    public int Count { get; private set; }

    /// <summary>Номер поселения-отправителя.</summary>
    public int[] FromSettlement { get; }

    /// <summary>Номер поселения-получателя.</summary>
    public int[] ToSettlement { get; }

    public short[] FromTribe { get; }

    public short[] ToTribe { get; }

    public int[] FromX { get; }

    public int[] FromY { get; }

    public int[] ToX { get; }

    public int[] ToY { get; }

    /// <summary>Длина пути в тайлах. Далёкие пути возят меньше.</summary>
    public int[] Length { get; }

    /// <summary>Путь морской: такие появляются только с нужной эпохи.</summary>
    public bool[] Sea { get; }

    /// <summary>Какой товар шёл по пути в последний раз.</summary>
    public int[] Good { get; }

    /// <summary>Сколько товара прошло в последний прогон. Рисовалка по нему выбирает яркость линии.</summary>
    public float[] Volume { get; }

    /// <summary>Сколько всего прошло по пути с его появления.</summary>
    public float[] Total { get; }

    /// <summary>Есть ли место под новый путь.</summary>
    public bool HasRoom => Count < Capacity;

    /// <summary>Заводит путь. Возвращает номер или -1, если сеть забита.</summary>
    public int Add(
        int fromSettlement,
        int toSettlement,
        short fromTribe,
        short toTribe,
        int fromX,
        int fromY,
        int toX,
        int toY,
        int length,
        bool sea)
    {
        if (Count >= Capacity)
        {
            return -1;
        }

        int index = Count++;
        FromSettlement[index] = fromSettlement;
        ToSettlement[index] = toSettlement;
        FromTribe[index] = fromTribe;
        ToTribe[index] = toTribe;
        FromX[index] = fromX;
        FromY[index] = fromY;
        ToX[index] = toX;
        ToY[index] = toY;
        Length[index] = length;
        Sea[index] = sea;
        Good[index] = 0;
        Volume[index] = 0f;
        Total[index] = 0f;
        return index;
    }

    /// <summary>Записывает итог перевозки по пути.</summary>
    public void Carry(int route, int good, float amount)
    {
        if ((uint)route >= (uint)Count)
        {
            return;
        }

        Good[route] = good;
        Volume[route] += amount;
        Total[route] += amount;
    }

    /// <summary>Сбрасывает груз последнего прогона, сами пути остаются.</summary>
    public void ClearFlow()
    {
        Array.Clear(Volume, 0, Count);
    }

    /// <summary>Стирает сеть целиком: перед сборкой заново и перед новым миром.</summary>
    public void Clear()
    {
        Count = 0;
    }

    /// <summary>Грубая контрольная сумма для теста на повторимость.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)Count);

            for (int i = 0; i < Count; i++)
            {
                hash = Mix(hash, (ulong)(uint)FromTribe[i]);
                hash = Mix(hash, (ulong)(uint)ToTribe[i]);
                hash = Mix(hash, (ulong)(uint)FromX[i]);
                hash = Mix(hash, (ulong)(uint)FromY[i]);
                hash = Mix(hash, (ulong)(uint)ToX[i]);
                hash = Mix(hash, (ulong)(uint)ToY[i]);
                hash = Mix(hash, (ulong)(uint)Length[i]);
                hash = Mix(hash, Sea[i] ? 1UL : 0UL);
                hash = Mix(hash, (ulong)(long)MathF.Round(Total[i] * 100f));
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
