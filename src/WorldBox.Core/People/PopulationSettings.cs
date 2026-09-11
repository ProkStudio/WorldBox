namespace WorldBox.Core.People;

/// <summary>
/// Числа, по которым живут жители. Один тик — пять игровых лет, поэтому все шансы
/// считаются на тик, а не на год. Баланс черновой: он будет уточняться на срезах
/// S4 (эпохи) и S5 (экономика), когда еда начнёт зависеть не только от плодородия.
/// </summary>
public sealed class PopulationSettings
{
    public static readonly PopulationSettings Default = new PopulationSettings();

    /// <summary>С какого возраста житель может завести ребёнка.</summary>
    public float AdultAge { get; init; } = 15f;

    /// <summary>До какого возраста житель может завести ребёнка.</summary>
    public float LastFertileAge { get; init; } = 45f;

    /// <summary>После этого возраста начинает работать шанс смерти от старости.</summary>
    public float MaxAge { get; init; } = 65f;

    public float OldAgeChance { get; init; } = 0.35f;

    /// <summary>
    /// Ожидаемое число детей у сытого взрослого за опорный пятилетний тик. Голод его снижает.
    /// При длинном тике число растёт пропорционально годам, поэтому за тик их может быть больше одного.
    /// За тридцать фертильных лет это даёт около 1,7 ребёнка на человека — чуть выше замещения.
    /// </summary>
    public float BirthChance { get; init; } = 0.28f;

    /// <summary>Предохранитель: сколько детей один взрослый может завести за один тик.</summary>
    public int MaxBirthsPerTick { get; init; } = 4;

    /// <summary>Выше этого голода детей не заводят.</summary>
    public float BirthHungerLimit { get; init; } = 0.55f;

    /// <summary>Сколько голода набегает за тик без еды.</summary>
    public float HungerGain { get; init; } = 0.02f;

    /// <summary>Во сколько раз плодородие тайла превращается в еду.</summary>
    public float FoodFactor { get; init; } = 0.08f;

    /// <summary>Насколько сильно соседи на том же тайле отнимают еду.</summary>
    public float Crowding { get; init; } = 0.6f;

    /// <summary>Шанс за тик попробовать сменить тайл.</summary>
    public float MoveChance { get; init; } = 0.35f;

    /// <summary>Голодный житель уходит куда угодно, лишь бы с места.</summary>
    public float DesperateHunger { get; init; } = 0.5f;

    /// <summary>Шанс умереть за тик, когда голод дошёл до предела.</summary>
    public float StarveChance { get; init; } = 0.08f;
}
