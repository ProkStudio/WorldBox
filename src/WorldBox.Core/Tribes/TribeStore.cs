namespace WorldBox.Core.Tribes;

/// <summary>
/// Народы мира. Слот 0 зарезервирован под «ничьё», поэтому номер племени у человека
/// и у владельца тайла — это прямой индекс в массивах ниже.
/// Имена придумываются один раз при рождении племени: в тиках строки не создаются.
/// </summary>
public sealed class TribeStore
{
    public const int DefaultCapacity = 64;

    /// <summary>«Нет племени»: дикари и ничейная земля.</summary>
    public const short None = 0;

    public TribeStore(int capacity = DefaultCapacity)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Нужно хотя бы два слота: нулевой занят.");
        }

        Capacity = capacity;
        Name = new string[capacity];
        ColorIndex = new byte[capacity];
        Alive = new bool[capacity];
        People = new int[capacity];
        Settlements = new int[capacity];
        Era = new byte[capacity];
        HomeX = new int[capacity];
        HomeY = new int[capacity];
        Name[0] = string.Empty;
    }

    public int Capacity { get; }

    /// <summary>Сколько народов сейчас живо.</summary>
    public int Count { get; private set; }

    public string[] Name { get; }

    /// <summary>Номер цвета в палитре народов. Сам цвет живёт в слое рисования.</summary>
    public byte[] ColorIndex { get; }

    public bool[] Alive { get; }

    /// <summary>Людей в народе. Пересчитывается системой племён раз в несколько тиков.</summary>
    public int[] People { get; }

    public int[] Settlements { get; }

    /// <summary>Номер эпохи из data/eras.json. Двигает его система эпох, читают рисование и интерфейс.</summary>
    public byte[] Era { get; }

    public int[] HomeX { get; }

    public int[] HomeY { get; }

    public bool IsAlive(short tribe) => (uint)tribe < (uint)Capacity && Alive[tribe];

    public string NameOf(short tribe) => IsAlive(tribe) ? Name[tribe] : string.Empty;

    /// <summary>Заводит народ и возвращает его номер. Ноль означает, что слотов не осталось.</summary>
    public short Create(string name, byte colorIndex, int homeX, int homeY)
    {
        for (short i = 1; i < Capacity; i++)
        {
            if (Alive[i])
            {
                continue;
            }

            Alive[i] = true;
            Name[i] = name;
            ColorIndex[i] = colorIndex;
            People[i] = 0;
            Settlements[i] = 0;
            Era[i] = 0;
            HomeX[i] = homeX;
            HomeY[i] = homeY;
            Count++;
            return i;
        }

        return None;
    }

    public bool Remove(short tribe)
    {
        if (!IsAlive(tribe))
        {
            return false;
        }

        Alive[tribe] = false;
        People[tribe] = 0;
        Settlements[tribe] = 0;
        Count--;
        return true;
    }

    /// <summary>Самый многолюдный народ. Ноль, если живых нет.</summary>
    public short Largest()
    {
        short best = None;
        int bestPeople = 0;

        for (short i = 1; i < Capacity; i++)
        {
            if (!Alive[i] || People[i] <= bestPeople)
            {
                continue;
            }

            best = i;
            bestPeople = People[i];
        }

        return best;
    }

    public void Clear()
    {
        Array.Clear(Alive, 0, Alive.Length);
        Array.Clear(People, 0, People.Length);
        Array.Clear(Settlements, 0, Settlements.Length);
        Array.Clear(Era, 0, Era.Length);
        Count = 0;
    }
}
