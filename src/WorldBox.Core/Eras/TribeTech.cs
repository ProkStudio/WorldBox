using System.Numerics;
using WorldBox.Core.Tribes;

namespace WorldBox.Core.Eras;

/// <summary>
/// Технологическое состояние народов: что у них под ногами, с кем они граничат,
/// сколько знания накоплено и что мешает шагнуть дальше.
/// Массивы отдельных полей, размер фиксирован ёмкостью TribeStore, аллокаций в тике нет.
/// Сама эпоха лежит в TribeStore.Era, чтобы её без лишних связей видели рисование и интерфейс.
/// </summary>
public sealed class TribeTech
{
    /// <summary>Соседи хранятся битовой маской, поэтому учитываются первые 64 народа.</summary>
    public const int MaxTracked = 64;

    public TribeTech(int capacity)
    {
        if (capacity <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость должна быть больше одного.");
        }

        Capacity = capacity;
        Progress = new float[capacity];
        OwnResources = new int[capacity];
        TradeResources = new int[capacity];
        Geo = new byte[capacity];
        Tiles = new int[capacity];
        Headcount = new long[capacity];
        MissingResources = new int[capacity];
        MissingGeo = new byte[capacity];
        Block = new byte[capacity];
        Contacts = new ulong[capacity];
        FoodMultiplier = new float[capacity];
        EraChangedTick = new long[capacity];
        DarkAgeUntil = new long[capacity];

        Array.Fill(FoodMultiplier, 1f);
    }

    public int Capacity { get; }

    /// <summary>Накопленное знание для следующей эпохи.</summary>
    public float[] Progress { get; }

    /// <summary>Ресурсы на своей земле, битовая маска по ResourceKind.</summary>
    public int[] OwnResources { get; }

    /// <summary>Ресурсы, доступные через соседей.</summary>
    public int[] TradeResources { get; }

    /// <summary>Какая земля есть у народа, битовая маска по GeoFeature.</summary>
    public byte[] Geo { get; }

    /// <summary>Сколько тайлов занято на последний полный обход карты.</summary>
    public int[] Tiles { get; }

    /// <summary>Население в цифрах исторического масштаба.</summary>
    public long[] Headcount { get; }

    /// <summary>Каких ресурсов не хватает для следующей эпохи.</summary>
    public int[] MissingResources { get; }

    /// <summary>Какой земли не хватает для следующей эпохи.</summary>
    public byte[] MissingGeo { get; }

    /// <summary>Причина остановки из EraBlock.</summary>
    public byte[] Block { get; }

    /// <summary>С кем народ граничит, битовая маска по номерам народов.</summary>
    public ulong[] Contacts { get; }

    /// <summary>Во сколько раз эпоха улучшила еду по сравнению с нулевой.</summary>
    public float[] FoodMultiplier { get; }

    /// <summary>На каком тике народ сменил эпоху.</summary>
    public long[] EraChangedTick { get; }

    /// <summary>До какого тика длятся тёмные века.</summary>
    public long[] DarkAgeUntil { get; }

    /// <summary>Все ресурсы, до которых народ может дотянуться.</summary>
    public int ResourcesOf(int tribe)
    {
        return (uint)tribe < (uint)Capacity ? OwnResources[tribe] | TradeResources[tribe] : 0;
    }

    public EraBlock BlockOf(int tribe)
    {
        return (uint)tribe < (uint)Capacity ? (EraBlock)Block[tribe] : EraBlock.Ready;
    }

    /// <summary>Сколько у народа соседей по границе.</summary>
    public int NeighborCount(int tribe)
    {
        return (uint)tribe < (uint)Capacity ? BitOperations.PopCount(Contacts[tribe]) : 0;
    }

    /// <summary>Доля накопленного знания от нужного, от нуля до единицы.</summary>
    public float ProgressShare(EraTable table, int era, int tribe)
    {
        if (table == null || (uint)tribe >= (uint)Capacity)
        {
            return 0f;
        }

        int next = era + 1;
        if (next >= table.Count)
        {
            return 1f;
        }

        float cost = EraRules.Cost(table, next);
        return cost <= 0f ? 1f : Math.Clamp(Progress[tribe] / cost, 0f, 1f);
    }

    /// <summary>Сбрасывает один народ: вызывается, когда слот освободился.</summary>
    public void Reset(int tribe)
    {
        if ((uint)tribe >= (uint)Capacity)
        {
            return;
        }

        Progress[tribe] = 0f;
        OwnResources[tribe] = 0;
        TradeResources[tribe] = 0;
        Geo[tribe] = 0;
        Tiles[tribe] = 0;
        Headcount[tribe] = 0L;
        MissingResources[tribe] = 0;
        MissingGeo[tribe] = 0;
        Block[tribe] = 0;
        Contacts[tribe] = 0UL;
        FoodMultiplier[tribe] = 1f;
        EraChangedTick[tribe] = 0L;
        DarkAgeUntil[tribe] = 0L;
    }

    public void Clear()
    {
        Array.Clear(Progress);
        Array.Clear(OwnResources);
        Array.Clear(TradeResources);
        Array.Clear(Geo);
        Array.Clear(Tiles);
        Array.Clear(Headcount);
        Array.Clear(MissingResources);
        Array.Clear(MissingGeo);
        Array.Clear(Block);
        Array.Clear(Contacts);
        Array.Clear(EraChangedTick);
        Array.Clear(DarkAgeUntil);
        Array.Fill(FoodMultiplier, 1f);
    }

    /// <summary>Грубая контрольная сумма для теста детерминизма.</summary>
    public ulong Checksum(TribeStore tribes)
    {
        ArgumentNullException.ThrowIfNull(tribes);

        unchecked
        {
            ulong hash = 1469598103934665603UL;
            int limit = Math.Min(Capacity, tribes.Capacity);

            for (int i = 1; i < limit; i++)
            {
                if (!tribes.Alive[i])
                {
                    continue;
                }

                hash = Mix(hash, (ulong)(uint)i);
                hash = Mix(hash, tribes.Era[i]);
                hash = Mix(hash, (ulong)(uint)(int)Progress[i]);
                hash = Mix(hash, (ulong)(uint)OwnResources[i]);
                hash = Mix(hash, (ulong)(uint)TradeResources[i]);
                hash = Mix(hash, Geo[i]);
                hash = Mix(hash, (ulong)Headcount[i]);
                hash = Mix(hash, Contacts[i]);
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
