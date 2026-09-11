namespace WorldBox.Core.Society;

/// <summary>
/// Догматы религии. Каждая вера несёт набор отношений к миру, и по ним видно,
/// почему один народ с этой верой воюет охотнее, а другой торгует и учится.
/// </summary>
[Flags]
public enum Dogma : byte
{
    None = 0,

    /// <summary>Война угодна вере: народ легче поднимает войско.</summary>
    War = 1,

    /// <summary>Торговля благословлена: вера идёт по путям купцов охотнее.</summary>
    Trade = 2,

    /// <summary>Знание свято: наука идёт быстрее.</summary>
    Knowledge = 4,

    /// <summary>Порядок выше человека: меньше бунтов, но и меньше свободы.</summary>
    Order = 8,
}

/// <summary>Правила религий: где рождаются, как расходятся и когда раскалываются.</summary>
public sealed record SocietyReligionSettings(
    int MaxReligions,
    int MinPeople,
    int MinEra,
    int YoungRuns,
    float BirthChancePerRun,
    float CrisisBonus,
    float NeighbourSpreadChance,
    float TradeSpreadChance,
    float ConquestSpreadChance,
    int SchismMinFollowers,
    float SchismChancePerRun,
    float SchismUnrest,
    float ForeignFaithUnrest);

/// <summary>Правила культур: ассимиляция, поглощение слабых и недовольство чужой землёй.</summary>
public sealed record SocietyCultureSettings(
    int MaxCultures,
    float AssimilationChance,
    float AssimilationPerEra,
    float ForeignUnrest,
    float AbsorbShare,
    int Languages);

/// <summary>Как часто народ пересматривает форму власти.</summary>
public sealed record SocietyIdeologySettings(int ChangeRuns, float SwitchChance);

/// <summary>
/// Стабильность государства: из чего складывается и когда государство трещит.
/// Вес — это доля, которую слагаемое добавляет или отнимает от базовой стабильности.
/// </summary>
public sealed record SocietyStabilitySettings(
    float Base,
    float HungerWeight,
    float UnityWeight,
    float FaithWeight,
    float EraFitWeight,
    float WarWeight,
    float UnrestWeight,
    float UnrestPerPoint,
    float CollapseThreshold,
    int CollapseRuns,
    float CollapseUnrest);
