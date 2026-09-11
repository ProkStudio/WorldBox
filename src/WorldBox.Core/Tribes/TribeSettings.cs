namespace WorldBox.Core.Tribes;

/// <summary>Весь баланс племён и поселений в одном месте: правится здесь, а не по коду.</summary>
public sealed class TribeSettings
{
    /// <summary>Сколько людей должно стоять на тайле, чтобы там возникла стоянка.</summary>
    public int FoundDensity { get; init; } = 3;

    public float FoundFertility { get; init; } = 0.28f;

    /// <summary>Минимум тайлов между соседними поселениями.</summary>
    public int MinDistance { get; init; } = 6;

    /// <summary>Сколько случайных людей за тик проверяется на постройку стоянки.</summary>
    public int FoundAttempts { get; init; } = 48;

    /// <summary>Сколько поселений за тик обновляют население и границы.</summary>
    public int ClaimPerTick { get; init; } = 24;

    /// <summary>Через столько тиков пересчитывается население народов.</summary>
    public int RecountEvery { get; init; } = 20;

    /// <summary>С какого расстояния поселение считает людей своими.</summary>
    public int LocalRadius { get; init; } = 3;

    /// <summary>Меньше этого числа жителей — поселение брошено.</summary>
    public int AbandonBelow { get; init; } = 1;

    public int VillageAt { get; init; } = 12;

    public int TownAt { get; init; } = 40;

    public byte CampRadius { get; init; } = 2;

    public byte VillageRadius { get; init; } = 4;

    public byte TownRadius { get; init; } = 7;

    public static readonly TribeSettings Default = new TribeSettings();
}
