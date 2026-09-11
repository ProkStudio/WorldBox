namespace WorldBox.Render;

/// <summary>Что именно показывает карта. Переключается клавишей M.</summary>
public enum MapMode : byte
{
    Terrain = 0,
    Height = 1,
    Temperature = 2,
    Moisture = 3,
    Fertility = 4,
    Resources = 5,
}

public static class MapModes
{
    public const int Count = 6;

    private static readonly string[] Keys =
    {
        "map.terrain",
        "map.height",
        "map.temperature",
        "map.moisture",
        "map.fertility",
        "map.resources",
    };

    public static string NameKey(MapMode mode) => Keys[(int)mode];

    public static MapMode Next(MapMode mode) => (MapMode)(((int)mode + 1) % Count);

    public static MapMode Previous(MapMode mode) => (MapMode)(((int)mode + Count - 1) % Count);
}
