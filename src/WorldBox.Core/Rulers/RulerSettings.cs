using System.Numerics;

namespace WorldBox.Core.Rulers;

/// <summary>
/// Черты правителя. Черта — не украшение в окне: каждая что-то делает с державой,
/// и в замере видно, какая именно. Жестокий копит недовольство, справедливый его гасит,
/// учёный ускоряет знание, строитель наполняет амбары.
/// </summary>
[Flags]
public enum Trait : byte
{
    None = 0,

    /// <summary>Жестокий: держит страхом. Воюет охотнее, но улица злее и держава слабее.</summary>
    Cruel = 1,

    /// <summary>Справедливый: суд по правде гасит недовольство в городах.</summary>
    Just = 2,

    /// <summary>Учёный: двор кормит книжников, знание копится быстрее.</summary>
    Scholar = 4,

    /// <summary>Строитель: амбары и стены. В городах больше запаса еды.</summary>
    Builder = 8,

    /// <summary>Завоеватель: войско поднимается охотнее.</summary>
    Conqueror = 16,

    /// <summary>Набожный: согласие с верой двора держит державу крепче.</summary>
    Pious = 32,
}

/// <summary>Черты по порядку: по нему их катает случай и показывает интерфейс.</summary>
public static class Traits
{
    /// <summary>Сколько черт знает игра.</summary>
    public const int Count = 6;

    private static readonly Trait[] Order =
    {
        Trait.Cruel, Trait.Just, Trait.Scholar, Trait.Builder, Trait.Conqueror, Trait.Pious,
    };

    private static readonly string[] Keys =
    {
        "trait.cruel", "trait.just", "trait.scholar", "trait.builder", "trait.conqueror", "trait.pious",
    };

    public static Trait Of(int index) => Order[Wrap(index)];

    public static string NameKey(int index) => Keys[Wrap(index)];

    /// <summary>Ключ перевода первой черты набора или пустая строка, если черт нет.</summary>
    public static string FirstNameKey(Trait traits)
    {
        for (int i = 0; i < Count; i++)
        {
            if ((traits & Order[i]) != Trait.None)
            {
                return Keys[i];
            }
        }

        return string.Empty;
    }

    public static bool Has(Trait traits, Trait one) => (traits & one) != Trait.None;

    public static int CountOf(Trait traits) => BitOperations.PopCount((byte)traits);

    /// <summary>
    /// Катает набор черт. Пустой набор разрешён только если таблица просит ноль:
    /// правитель без черт — это правитель, который ни на что не влияет.
    /// </summary>
    public static Trait Roll(Rng rng, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(rng);

        int low = Math.Clamp(min, 0, Count);
        int high = Math.Clamp(max, low, Count);
        int want = low == high ? low : rng.NextInt(low, high + 1);

        Trait traits = Trait.None;

        // Повторный бросок в ту же черту не считается: крутим с запасом, пока не наберём нужное число.
        for (int guard = 0; guard < Count * 6 && CountOf(traits) < want; guard++)
        {
            traits |= Of(rng.NextInt(Count));
        }

        return traits;
    }

    private static int Wrap(int index) => ((index % Count) + Count) % Count;
}

/// <summary>Сколько живут правители и от чего умирают раньше срока.</summary>
public sealed record RulerLifeSettings(
    int MinLifeYears,
    int MaxLifeYears,
    int AdultYears,
    float ViolentDeathChance,
    float UnrestDeathWeight);

/// <summary>Дети, наследование, перевороты и кризис без наследника.</summary>
public sealed record RulerHeirSettings(
    float HeirChancePerRun,
    int MaxChildren,
    float CoupChance,
    float CrisisUnrest,
    int CrisisRuns);

/// <summary>Насколько сильно черты правителя двигают державу.</summary>
public sealed record RulerTraitSettings(
    int MinTraits,
    int MaxTraits,
    float CruelUnrest,
    float JustUnrest,
    float BuilderFood,
    float ScholarProgress,
    float ConquerorAggression,
    float PiousStability,
    float CruelStability);

/// <summary>Герои: сколько их, как часто приходят и что дают, пока живы.</summary>
public sealed record RulerHeroSettings(
    int MaxHeroes,
    float ChancePerRun,
    int LifeRuns,
    float CommanderAggression,
    float InventorProgress,
    float ProphetUnrest,
    float ExplorerFood);
