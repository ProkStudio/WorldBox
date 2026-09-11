namespace WorldBox.Core.Society;

/// <summary>
/// Кто во что верит и под какой властью живёт. Государство и город — разные слои:
/// у народа есть вера двора, культура и форма власти, а у каждого поселения — своя вера
/// и свой народ. Именно поэтому карта религий не совпадает с картой государств.
///
/// Только данные: считает и меняет их <see cref="SocietySystem"/>, рисует — слой общества.
/// </summary>
public sealed class SocietyState
{
    /// <summary>Формы власти нет: народ ещё не обустроился.</summary>
    public const int NoIdeology = -1;

    public SocietyState(int tribeCapacity, int settlementCapacity)
    {
        if (tribeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tribeCapacity), "Ёмкость народов должна быть положительной.");
        }

        if (settlementCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settlementCapacity), "Ёмкость поселений должна быть положительной.");
        }

        TribeCapacity = tribeCapacity;
        SettlementCapacity = settlementCapacity;

        Religion = new short[tribeCapacity];
        Culture = new short[tribeCapacity];
        Ideology = new int[tribeCapacity];
        Stability = new float[tribeCapacity];
        Unity = new float[tribeCapacity];
        Faith = new float[tribeCapacity];
        LowRuns = new int[tribeCapacity];
        IdeologyRun = new long[tribeCapacity];

        SettlementReligion = new short[settlementCapacity];
        SettlementCulture = new short[settlementCapacity];

        Clear();
    }

    public int TribeCapacity { get; }

    public int SettlementCapacity { get; }

    /// <summary>Вера двора: религия, которую государство признало своей.</summary>
    public short[] Religion { get; }

    /// <summary>Культура господствующего народа государства.</summary>
    public short[] Culture { get; }

    /// <summary>Номер формы власти в таблице или <see cref="NoIdeology"/>.</summary>
    public int[] Ideology { get; }

    /// <summary>Стабильность от 0 до 1. Ниже порога государство начинает трещать.</summary>
    public float[] Stability { get; }

    /// <summary>Единство культуры: доля людей господствующей культуры.</summary>
    public float[] Unity { get; }

    /// <summary>Согласие религии: доля людей веры двора.</summary>
    public float[] Faith { get; }

    /// <summary>Сколько прогонов подряд стабильность ниже порога.</summary>
    public int[] LowRuns { get; }

    /// <summary>На каком прогоне власть менялась в последний раз.</summary>
    public long[] IdeologyRun { get; }

    /// <summary>Вера каждого поселения.</summary>
    public short[] SettlementReligion { get; }

    /// <summary>Культура каждого поселения.</summary>
    public short[] SettlementCulture { get; }

    /// <summary>Номер версии картинки общества: по нему слой на карте понимает, что пора перерисоваться.</summary>
    public int Version { get; private set; }

    /// <summary>Отмечает, что картинка устарела.</summary>
    public void Touch()
    {
        Version++;
    }

    /// <summary>Народ исчез: его строка освобождается, иначе слот достанется новому с чужой верой.</summary>
    public void ForgetTribe(int tribe)
    {
        if ((uint)tribe >= (uint)TribeCapacity)
        {
            return;
        }

        Religion[tribe] = ReligionStore.None;
        Culture[tribe] = CultureStore.None;
        Ideology[tribe] = NoIdeology;
        Stability[tribe] = 0f;
        Unity[tribe] = 0f;
        Faith[tribe] = 0f;
        LowRuns[tribe] = 0;
        IdeologyRun[tribe] = 0L;
    }

    /// <summary>Поселение исчезло: слот готов к повторному использованию.</summary>
    public void ForgetSettlement(int index)
    {
        if ((uint)index >= (uint)SettlementCapacity)
        {
            return;
        }

        SettlementReligion[index] = ReligionStore.None;
        SettlementCulture[index] = CultureStore.None;
    }

    public void Clear()
    {
        Array.Fill(Religion, ReligionStore.None);
        Array.Fill(Culture, CultureStore.None);
        Array.Fill(Ideology, NoIdeology);
        Array.Clear(Stability, 0, Stability.Length);
        Array.Clear(Unity, 0, Unity.Length);
        Array.Clear(Faith, 0, Faith.Length);
        Array.Clear(LowRuns, 0, LowRuns.Length);
        Array.Clear(IdeologyRun, 0, IdeologyRun.Length);
        Array.Fill(SettlementReligion, ReligionStore.None);
        Array.Fill(SettlementCulture, CultureStore.None);
        Version++;
    }

    /// <summary>Грубая контрольная сумма для теста повторимости.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;

            for (int t = 0; t < TribeCapacity; t++)
            {
                hash = Mix(hash, (ulong)(uint)Religion[t]);
                hash = Mix(hash, (ulong)(uint)Culture[t]);
                hash = Mix(hash, (ulong)(uint)Ideology[t]);
                hash = Mix(hash, (ulong)(long)MathF.Round(Stability[t] * 100f));
                hash = Mix(hash, (ulong)(long)MathF.Round(Unity[t] * 100f));
                hash = Mix(hash, (ulong)(long)MathF.Round(Faith[t] * 100f));
            }

            for (int i = 0; i < SettlementCapacity; i++)
            {
                hash = Mix(hash, (ulong)(uint)SettlementReligion[i]);
                hash = Mix(hash, (ulong)(uint)SettlementCulture[i]);
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
