namespace WorldBox.Core.World;

/// <summary>Тип местности тайла. Порядок фиксирован: числа попадают в сейвы.</summary>
public enum Biome : byte
{
    DeepOcean = 0,
    Ocean = 1,
    Coast = 2,
    Beach = 3,
    Lake = 4,
    River = 5,
    Marsh = 6,
    Desert = 7,
    Savanna = 8,
    Steppe = 9,
    Grassland = 10,
    Shrubland = 11,
    TemperateForest = 12,
    Rainforest = 13,
    Taiga = 14,
    Tundra = 15,
    Glacier = 16,
    Mountain = 17,
    Peak = 18,
}

/// <summary>Свойства биома. Цвета живут в слое рендера, здесь только игровые числа.</summary>
public readonly struct BiomeInfo
{
    public BiomeInfo(string nameKey, bool isWater, float fertility, float moveCost)
    {
        NameKey = nameKey;
        IsWater = isWater;
        Fertility = fertility;
        MoveCost = moveCost;
    }

    /// <summary>Ключ названия в data/strings.ru.json.</summary>
    public string NameKey { get; }

    public bool IsWater { get; }

    /// <summary>Базовая плодородность 0..1 до поправок на влагу и реки.</summary>
    public float Fertility { get; }

    /// <summary>Стоимость прохода для будущих перемещений и торговых путей.</summary>
    public float MoveCost { get; }
}

public static class Biomes
{
    public const int Count = 19;

    private static readonly BiomeInfo[] Table =
    {
        new BiomeInfo("biome.deep_ocean", true, 0f, 6f),
        new BiomeInfo("biome.ocean", true, 0f, 4f),
        new BiomeInfo("biome.coast", true, 0.10f, 3f),
        new BiomeInfo("biome.beach", false, 0.15f, 1.1f),
        new BiomeInfo("biome.lake", true, 0.05f, 3f),
        new BiomeInfo("biome.river", false, 0.90f, 1.6f),
        new BiomeInfo("biome.marsh", false, 0.55f, 2.2f),
        new BiomeInfo("biome.desert", false, 0.04f, 1.4f),
        new BiomeInfo("biome.savanna", false, 0.48f, 1.0f),
        new BiomeInfo("biome.steppe", false, 0.38f, 1.0f),
        new BiomeInfo("biome.grassland", false, 0.75f, 1.0f),
        new BiomeInfo("biome.shrubland", false, 0.30f, 1.2f),
        new BiomeInfo("biome.temperate_forest", false, 0.60f, 1.5f),
        new BiomeInfo("biome.rainforest", false, 0.45f, 2.0f),
        new BiomeInfo("biome.taiga", false, 0.25f, 1.6f),
        new BiomeInfo("biome.tundra", false, 0.10f, 1.3f),
        new BiomeInfo("biome.glacier", false, 0f, 2.5f),
        new BiomeInfo("biome.mountain", false, 0.08f, 3f),
        new BiomeInfo("biome.peak", false, 0f, 5f),
    };

    public static BiomeInfo Info(Biome biome) => Table[(int)biome];

    public static string NameKey(Biome biome) => Table[(int)biome].NameKey;

    public static bool IsWater(Biome biome) => Table[(int)biome].IsWater;

    public static bool IsLand(Biome biome) => !Table[(int)biome].IsWater;
}
