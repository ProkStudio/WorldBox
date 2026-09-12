namespace WorldBox.Core.Rulers;

/// <summary>
/// Кто сидит на престоле каждого народа и что с этим престолом уже случалось:
/// сколько домов сменилось, сколько раз наследника не нашлось, сколько было переворотов.
///
/// Только данные. Считает и меняет их <see cref="RulerSystem"/>, показывает — окно династии.
/// </summary>
public sealed class DynastyState
{
    /// <summary>Престол пуст: правителя нет.</summary>
    public const int NoRuler = -1;

    public DynastyState(int tribeCapacity)
    {
        if (tribeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tribeCapacity), "Ёмкость народов должна быть положительной.");
        }

        TribeCapacity = tribeCapacity;

        Ruler = new int[tribeCapacity];
        Previous = new int[tribeCapacity];
        Heir = new int[tribeCapacity];
        House = new string[tribeCapacity];
        Generation = new int[tribeCapacity];
        Houses = new int[tribeCapacity];
        Crises = new int[tribeCapacity];
        Coups = new int[tribeCapacity];
        Reigns = new int[tribeCapacity];
        CrownedRun = new long[tribeCapacity];
        CrisisRun = new long[tribeCapacity];

        Clear();
    }

    public int TribeCapacity { get; }

    /// <summary>Номер записи правящего человека в <see cref="RulerStore"/> или <see cref="NoRuler"/>.</summary>
    public int[] Ruler { get; }

    /// <summary>Предыдущий правитель: от него ищутся дети и братья.</summary>
    public int[] Previous { get; }

    /// <summary>Кого двор считает наследником сейчас. Нужен для окна и для проверки кризиса.</summary>
    public int[] Heir { get; }

    /// <summary>Имя правящего дома.</summary>
    public string[] House { get; }

    /// <summary>Какое поколение дома сидит на престоле.</summary>
    public int[] Generation { get; }

    /// <summary>Сколько домов успело сменить народ.</summary>
    public int[] Houses { get; }

    /// <summary>Сколько раз престол остался без наследника.</summary>
    public int[] Crises { get; }

    /// <summary>Сколько раз власть взяли силой.</summary>
    public int[] Coups { get; }

    /// <summary>Сколько правителей всего сменилось у народа.</summary>
    public int[] Reigns { get; }

    /// <summary>На каком прогоне короновали нынешнего.</summary>
    public long[] CrownedRun { get; }

    /// <summary>На каком прогоне был последний кризис наследования.</summary>
    public long[] CrisisRun { get; }

    /// <summary>Номер версии: по нему окно понимает, что пора перечитать данные.</summary>
    public int Version { get; private set; }

    public void Touch()
    {
        Version++;
    }

    public bool HasRuler(int tribe)
        => (uint)tribe < (uint)TribeCapacity && Ruler[tribe] != NoRuler;

    /// <summary>Народ погиб: строка освобождается, иначе новый народ унаследует чужой дом.</summary>
    public void ForgetTribe(int tribe)
    {
        if ((uint)tribe >= (uint)TribeCapacity)
        {
            return;
        }

        Ruler[tribe] = NoRuler;
        Previous[tribe] = NoRuler;
        Heir[tribe] = NoRuler;
        House[tribe] = string.Empty;
        Generation[tribe] = 0;
        Houses[tribe] = 0;
        Crises[tribe] = 0;
        Coups[tribe] = 0;
        Reigns[tribe] = 0;
        CrownedRun[tribe] = 0L;
        CrisisRun[tribe] = 0L;
    }

    public void Clear()
    {
        Array.Fill(Ruler, NoRuler);
        Array.Fill(Previous, NoRuler);
        Array.Fill(Heir, NoRuler);
        Array.Fill(House, string.Empty);
        Array.Clear(Generation, 0, Generation.Length);
        Array.Clear(Houses, 0, Houses.Length);
        Array.Clear(Crises, 0, Crises.Length);
        Array.Clear(Coups, 0, Coups.Length);
        Array.Clear(Reigns, 0, Reigns.Length);
        Array.Clear(CrownedRun, 0, CrownedRun.Length);
        Array.Clear(CrisisRun, 0, CrisisRun.Length);
        Version++;
    }

    /// <summary>Грубая контрольная сумма для теста повторимости.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;

            for (int tribe = 0; tribe < TribeCapacity; tribe++)
            {
                hash = Mix(hash, (ulong)(uint)Ruler[tribe]);
                hash = Mix(hash, (ulong)(uint)Generation[tribe]);
                hash = Mix(hash, (ulong)(uint)Houses[tribe]);
                hash = Mix(hash, (ulong)(uint)Crises[tribe]);
                hash = Mix(hash, (ulong)(uint)Coups[tribe]);
                hash = Mix(hash, (ulong)(uint)Reigns[tribe]);
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
