namespace WorldBox.Core.Roads;

/// <summary>Весь баланс дорог в одном месте: правится здесь, а не по коду.</summary>
public sealed class RoadSettings
{
    /// <summary>Раз в сколько тиков строители берутся за работу.</summary>
    public int BuildEvery { get; init; } = 8;

    /// <summary>Сколько новых дорог прокладывается за один прогон: поиск пути — самое дорогое здесь.</summary>
    public int RoutesPerRun { get; init; } = 1;

    /// <summary>Сколько готовых маршрутов за прогон проверяется на снос и повышение уровня.</summary>
    public int UpkeepPerRun { get; init; } = 8;

    /// <summary>Дальше этого расстояния дорогу между городами не тянут.</summary>
    public int MaxDistance { get; init; } = 40;

    /// <summary>Сколько дорог выходит из одного города.</summary>
    public int LinksPerSettlement { get; init; } = 3;

    /// <summary>Насколько дороже тянуть дорогу к чужому народу, в тайлах расстояния.</summary>
    public int ForeignPenalty { get; init; } = 10;

    /// <summary>Цена шага по готовой дороге от обычной, в десятых: поэтому дороги сливаются в тракты.</summary>
    public int RoadDiscount { get; init; } = 4;

    /// <summary>Цена подъёма: разница высот 0..1 умножается на это число.</summary>
    public int SlopePenalty { get; init; } = 900;

    /// <summary>На сколько тайлов окно поиска шире прямоугольника между городами.</summary>
    public int SearchPad { get; init; } = 12;

    /// <summary>Сколько тайлов разбирает поиск пути, пока не сдаётся.</summary>
    public int MaxExpansions { get; init; } = 6000;

    public static readonly RoadSettings Default = new RoadSettings();
}
