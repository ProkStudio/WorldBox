namespace WorldBox.Core.World;

/// <summary>
/// Ресурс на тайле. Порядок фиксирован и совпадает с именами в data/eras.json.
/// На тайле может быть только один ресурс: так проще читать карту и дешевле хранить.
/// </summary>
public enum ResourceKind : byte
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Copper = 3,
    Tin = 4,
    Iron = 5,
    Coal = 6,
    Saltpeter = 7,
    Oil = 8,
    Uranium = 9,
}

public static class ResourceKinds
{
    public const int Count = 10;

    private static readonly string[] Keys =
    {
        "resource.none",
        "resource.wood",
        "resource.stone",
        "resource.copper",
        "resource.tin",
        "resource.iron",
        "resource.coal",
        "resource.saltpeter",
        "resource.oil",
        "resource.uranium",
    };

    /// <summary>Идентификаторы из data/eras.json для связки с требованиями эпох.</summary>
    private static readonly string[] Ids =
    {
        string.Empty,
        "wood",
        "stone",
        "copper",
        "tin",
        "iron",
        "coal",
        "saltpeter",
        "oil",
        "uranium",
    };

    public static string NameKey(ResourceKind kind) => Keys[(int)kind];

    public static string Id(ResourceKind kind) => Ids[(int)kind];

    public static ResourceKind FromId(string id)
    {
        for (int i = 1; i < Ids.Length; i++)
        {
            if (string.Equals(Ids[i], id, StringComparison.OrdinalIgnoreCase))
            {
                return (ResourceKind)i;
            }
        }

        return ResourceKind.None;
    }
}
