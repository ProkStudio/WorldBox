namespace WorldBox.Render;

/// <summary>
/// Что показывает слой общества поверх обычной карты.
/// Режимы карты (<see cref="MapMode"/>) остались про землю и погоду:
/// общество — это отдельный слой, его можно включить над любым режимом.
/// </summary>
public enum SocietyLayer : byte
{
    Off = 0,
    Religion = 1,
    Culture = 2,
    Ideology = 3,
}

/// <summary>Перебор слоёв общества и их названия.</summary>
public static class SocietyLayers
{
    public const int Count = 4;

    private static readonly string[] Keys =
    {
        "layer.society_off",
        "layer.religion",
        "layer.culture",
        "layer.ideology",
    };

    public static string NameKey(SocietyLayer layer) => Keys[(byte)layer % Count];

    public static SocietyLayer Next(SocietyLayer layer) => (SocietyLayer)(((byte)layer + 1) % Count);

    public static SocietyLayer Previous(SocietyLayer layer) => (SocietyLayer)(((byte)layer + Count - 1) % Count);
}
