namespace WorldBox.Core.Rulers;

/// <summary>
/// Люди истории: правители, их дети и братья. Имена и родословные есть только здесь —
/// обычные жители мира так и остаются безымянными числами.
///
/// Хранилище помнит и мёртвых: без этого нельзя открыть список правителей за всю историю
/// и нельзя показать дерево дома. Память при этом ограничена: когда мест не остаётся,
/// вытесняется самая старая мёртвая запись, кроме тех, что помечены как линия нынешней власти.
/// </summary>
public sealed class RulerStore
{
    public const int DefaultCapacity = 768;

    /// <summary>«Никого»: пустой престол, неизвестный отец.</summary>
    public const int None = -1;

    private readonly int[] _free;

    private int _freeCount;
    private int _highWater;

    public RulerStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть положительной.");
        }

        Capacity = capacity;
        Name = new string[capacity];
        House = new string[capacity];
        Language = new byte[capacity];
        Tribe = new short[capacity];
        Traits = new byte[capacity];
        Born = new long[capacity];
        Died = new long[capacity];
        BornYear = new float[capacity];
        DiedYear = new float[capacity];
        Life = new float[capacity];
        Parent = new int[capacity];
        Generation = new int[capacity];
        Children = new int[capacity];
        Crowned = new long[capacity];
        Uncrowned = new long[capacity];
        Used = new bool[capacity];
        Dead = new bool[capacity];
        Keep = new bool[capacity];
        _free = new int[capacity];

        Array.Fill(Name, string.Empty);
        Array.Fill(House, string.Empty);
        Array.Fill(Parent, None);
    }

    public int Capacity { get; }

    /// <summary>Сколько записей занято: живые и памятные мёртвые вместе.</summary>
    public int Count { get; private set; }

    /// <summary>Сколько людей живо прямо сейчас.</summary>
    public int Living { get; private set; }

    public int HighWater => _highWater;

    /// <summary>Сколько записей вытеснено за партию: если растёт, история короче ёмкости.</summary>
    public int Forgotten { get; private set; }

    public string[] Name { get; }

    /// <summary>Имя дома. Меняется только с новой династией, дети наследуют дом отца.</summary>
    public string[] House { get; }

    public byte[] Language { get; }

    public short[] Tribe { get; }

    /// <summary>Набор черт из <see cref="Trait"/>.</summary>
    public byte[] Traits { get; }

    public long[] Born { get; }

    /// <summary>Тик смерти. Ноль, пока человек жив.</summary>
    public long[] Died { get; }

    /// <summary>Год рождения: лет в тике бывает разное число, поэтому возраст считается по годам.</summary>
    public float[] BornYear { get; }

    public float[] DiedYear { get; }

    /// <summary>Сколько лет отмерено этому человеку.</summary>
    public float[] Life { get; }

    /// <summary>Номер записи отца или <see cref="None"/>.</summary>
    public int[] Parent { get; }

    /// <summary>Поколение внутри дома: основатель — первое.</summary>
    public int[] Generation { get; }

    public int[] Children { get; }

    /// <summary>Тик коронации. Ноль — никогда не правил.</summary>
    public long[] Crowned { get; }

    /// <summary>Тик, когда человек оставил престол.</summary>
    public long[] Uncrowned { get; }

    /// <summary>Занят ли слот записью.</summary>
    public bool[] Used { get; }

    /// <summary>Умер ли человек. Запись при этом остаётся: это история.</summary>
    public bool[] Dead { get; }

    /// <summary>Линия нынешней власти: такие записи не вытесняются, иначе дерево дома обрывается.</summary>
    public bool[] Keep { get; }

    public bool Exists(int index) => (uint)index < (uint)Capacity && Used[index];

    public bool IsLiving(int index) => Exists(index) && !Dead[index];

    public string NameOf(int index) => Exists(index) ? Name[index] : string.Empty;

    public string HouseOf(int index) => Exists(index) ? House[index] : string.Empty;

    public Trait TraitsOf(int index) => Exists(index) ? (Trait)Traits[index] : Trait.None;

    public int ParentOf(int index) => Exists(index) ? Parent[index] : None;

    /// <summary>Возраст в годах на заданный год мира.</summary>
    public float AgeOf(int index, float year)
        => Exists(index) ? MathF.Max(0f, (Dead[index] ? DiedYear[index] : year) - BornYear[index]) : 0f;

    /// <summary>Заводит человека и возвращает номер записи или -1, если вытеснять некого.</summary>
    public int Add(
        string name,
        string house,
        byte language,
        short tribe,
        Trait traits,
        int parent,
        int generation,
        long tick,
        float year,
        float life)
    {
        int index = Take();
        if (index == None)
        {
            return None;
        }

        Name[index] = name ?? string.Empty;
        House[index] = house ?? string.Empty;
        Language[index] = language;
        Tribe[index] = tribe;
        Traits[index] = (byte)traits;
        Born[index] = tick;
        Died[index] = 0L;
        BornYear[index] = year;
        DiedYear[index] = 0f;
        Life[index] = life;
        Parent[index] = Exists(parent) ? parent : None;
        Generation[index] = generation;
        Children[index] = 0;
        Crowned[index] = 0L;
        Uncrowned[index] = 0L;
        Used[index] = true;
        Dead[index] = false;
        Keep[index] = false;
        Living++;

        if (Parent[index] != None)
        {
            Children[Parent[index]]++;
        }

        return index;
    }

    /// <summary>Человек умер. Запись остаётся: она и есть история.</summary>
    public bool Kill(int index, long tick, float year)
    {
        if (!IsLiving(index))
        {
            return false;
        }

        Dead[index] = true;
        Died[index] = tick;
        DiedYear[index] = year;
        Living--;
        return true;
    }

    /// <summary>Отмечает коронацию.</summary>
    public void Crown(int index, long tick)
    {
        if (Exists(index))
        {
            Crowned[index] = tick;
        }
    }

    /// <summary>Отмечает уход с престола: живой мог и отречься при перевороте.</summary>
    public void Uncrown(int index, long tick)
    {
        if (Exists(index))
        {
            Uncrowned[index] = tick;
        }
    }

    /// <summary>Сколько правителей было у народа за всю историю, считая нынешнего.</summary>
    public int ReignsOf(short tribe)
    {
        int count = 0;
        for (int i = 0; i < _highWater; i++)
        {
            if (Used[i] && Tribe[i] == tribe && Crowned[i] > 0L)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Забывает запись совсем: народ исчез вместе с домом.</summary>
    public void Forget(int index)
    {
        if (!Exists(index))
        {
            return;
        }

        Detach(index);

        if (!Dead[index])
        {
            Living--;
        }

        Used[index] = false;
        Dead[index] = false;
        Keep[index] = false;
        Name[index] = string.Empty;
        House[index] = string.Empty;
        Parent[index] = None;
        Crowned[index] = 0L;
        Count--;
        _free[_freeCount++] = index;
    }

    public void Clear()
    {
        Array.Clear(Used, 0, Used.Length);
        Array.Clear(Dead, 0, Dead.Length);
        Array.Clear(Keep, 0, Keep.Length);
        Array.Fill(Parent, None);
        _freeCount = 0;
        _highWater = 0;
        Count = 0;
        Living = 0;
        Forgotten = 0;
    }

    /// <summary>Грубая контрольная сумма для теста повторимости.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)Count);
            hash = Mix(hash, (ulong)Living);

            for (int i = 0; i < _highWater; i++)
            {
                if (!Used[i])
                {
                    continue;
                }

                hash = Mix(hash, (ulong)(uint)Tribe[i]);
                hash = Mix(hash, Traits[i]);
                hash = Mix(hash, (ulong)(uint)Generation[i]);
                hash = Mix(hash, (ulong)Born[i]);
                hash = Mix(hash, (ulong)Died[i]);
                hash = Mix(hash, (ulong)(long)MathF.Round(Life[i]));
                hash = Mix(hash, Dead[i] ? 1UL : 0UL);
            }

            return hash;
        }
    }

    /// <summary>Свободный слот: сначала освобождённые, потом новые, в конце вытеснение старого.</summary>
    private int Take()
    {
        if (_freeCount > 0)
        {
            Count++;
            return _free[--_freeCount];
        }

        if (_highWater < Capacity)
        {
            Count++;
            return _highWater++;
        }

        int oldest = OldestForgettable();
        if (oldest == None)
        {
            return None;
        }

        Detach(oldest);
        Forgotten++;
        return oldest;
    }

    /// <summary>Самая старая мёртвая запись вне линии нынешней власти.</summary>
    private int OldestForgettable()
    {
        int best = None;
        long bestDied = long.MaxValue;

        for (int i = 0; i < _highWater; i++)
        {
            if (!Used[i] || !Dead[i] || Keep[i])
            {
                continue;
            }

            if (Died[i] < bestDied)
            {
                bestDied = Died[i];
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Обрывает ссылки на запись перед её повторным использованием.
    /// Иначе ребёнок показал бы отцом совсем другого человека, занявшего слот.
    /// </summary>
    private void Detach(int index)
    {
        for (int i = 0; i < _highWater; i++)
        {
            if (Used[i] && Parent[i] == index)
            {
                Parent[i] = None;
            }
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
