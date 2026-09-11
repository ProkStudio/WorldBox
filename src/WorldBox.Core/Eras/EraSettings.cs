namespace WorldBox.Core.Eras;

/// <summary>Какая земля нужна эпохе. Считается по своим тайлам народа.</summary>
[Flags]
public enum GeoFeature : byte
{
    None = 0,
    Fertile = 1,
    River = 2,
    Coast = 4,
    Mountain = 8,
}

/// <summary>Шансы перенять эпоху у соседа. Раздел diffusion в data/eras.json.</summary>
public sealed class EraDiffusionSettings
{
    public EraDiffusionSettings(float tradePartner, float borderNeighbor, float conquered, float isolated)
    {
        TradePartner = tradePartner;
        BorderNeighbor = borderNeighbor;
        Conquered = conquered;
        Isolated = isolated;
    }

    /// <summary>Сосед делится нужным ресурсом — учит быстрее всех.</summary>
    public float TradePartner { get; }

    /// <summary>Обычный сосед по границе.</summary>
    public float BorderNeighbor { get; }

    /// <summary>Захваченный народ. Понадобится на срезе войн, пока только читается.</summary>
    public float Conquered { get; }

    /// <summary>Народ без соседей: перенимать не у кого.</summary>
    public float Isolated { get; }
}

/// <summary>Цена шага вперёд и всё, что его ускоряет. Раздел research.</summary>
public sealed class EraResearchSettings
{
    public EraResearchSettings(
        float baseCost,
        float costPerEra,
        float settlementBonus,
        float neighborBonus,
        float isolatedPenalty)
    {
        BaseCost = baseCost;
        CostPerEra = costPerEra;
        SettlementBonus = settlementBonus;
        NeighborBonus = neighborBonus;
        IsolatedPenalty = isolatedPenalty;
    }

    /// <summary>Цена первого шага в очках знания.</summary>
    public float BaseCost { get; }

    /// <summary>Насколько дороже каждая следующая эпоха.</summary>
    public float CostPerEra { get; }

    /// <summary>Прибавка к темпу за каждое поселение.</summary>
    public float SettlementBonus { get; }

    /// <summary>Прибавка за каждого соседа, который уже в следующей эпохе.</summary>
    public float NeighborBonus { get; }

    /// <summary>Множитель темпа для народа без соседей.</summary>
    public float IsolatedPenalty { get; }
}

/// <summary>Перевод человечков на карте в население в цифрах. Раздел population.</summary>
public sealed class EraPopulationSettings
{
    public EraPopulationSettings(float perAgent, float perAgentPerEra)
    {
        PerAgent = perAgent;
        PerAgentPerEra = perAgentPerEra;
    }

    /// <summary>Сколько людей изображает один человечек в нулевой эпохе.</summary>
    public float PerAgent { get; }

    /// <summary>Во сколько раз это число растёт с каждой эпохой.</summary>
    public float PerAgentPerEra { get; }
}

/// <summary>Пороги географии: когда земля считается пашней, рекой, берегом или горами. Раздел geo.</summary>
public sealed class EraGeoSettings
{
    public EraGeoSettings(float fertileValue, int fertileTiles, int riverTiles, int coastTiles, int mountainTiles)
    {
        FertileValue = fertileValue;
        FertileTiles = fertileTiles;
        RiverTiles = riverTiles;
        CoastTiles = coastTiles;
        MountainTiles = mountainTiles;
    }

    /// <summary>С какого плодородия тайл считается пашней.</summary>
    public float FertileValue { get; }

    public int FertileTiles { get; }

    public int RiverTiles { get; }

    public int CoastTiles { get; }

    public int MountainTiles { get; }
}

/// <summary>Откат назад, когда народ не удержал свою эпоху. Раздел collapse.</summary>
public sealed class EraCollapseSettings
{
    public EraCollapseSettings(float stabilityThreshold, int erasLost, float darkAgeYears, float popShareToHold)
    {
        StabilityThreshold = stabilityThreshold;
        ErasLost = erasLost;
        DarkAgeYears = darkAgeYears;
        PopShareToHold = popShareToHold;
    }

    /// <summary>Порог стабильности. Появится на срезе политики, пока только читается.</summary>
    public float StabilityThreshold { get; }

    /// <summary>Сколько эпох теряется за один откат.</summary>
    public int ErasLost { get; }

    /// <summary>Сколько игровых лет после отката народ не развивается.</summary>
    public float DarkAgeYears { get; }

    /// <summary>Какую долю от порога своей эпохи надо удерживать по населению.</summary>
    public float PopShareToHold { get; }
}
