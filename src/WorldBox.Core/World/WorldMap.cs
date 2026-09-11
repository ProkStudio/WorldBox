namespace WorldBox.Core.World;

/// <summary>
/// Карта мира в виде отдельных массивов на каждое свойство (структура массивов).
/// Так процессор читает подряд только нужное поле и не тащит лишние байты в кеш.
/// </summary>
public sealed class WorldMap
{
    public WorldMap(int width, int height)
    {
        Width = width;
        Height = height;
        TileCount = width * height;
        Elevation = new float[TileCount];
        Moisture = new float[TileCount];
        Temperature = new float[TileCount];
        Fertility = new float[TileCount];
        Flow = new float[TileCount];
        FlowDirection = new byte[TileCount];
        BiomeAt = new byte[TileCount];
        ResourceAt = new byte[TileCount];
        BiomeCounts = new int[Biomes.Count];
        ResourceCounts = new int[ResourceKinds.Count];
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCount { get; }

    /// <summary>Высота 0..1. Всё, что ниже SeaLevel, — вода.</summary>
    public float[] Elevation { get; }

    /// <summary>Влажность 0..1.</summary>
    public float[] Moisture { get; }

    /// <summary>Температура 0..1, где 0 — полюс, 1 — экватор на уровне моря.</summary>
    public float[] Temperature { get; }

    /// <summary>Плодородность 0..1: сколько еды даст тайл.</summary>
    public float[] Fertility { get; }

    /// <summary>Накопленный сток воды. Большие значения — русла рек.</summary>
    public float[] Flow { get; }

    /// <summary>Куда течёт вода: 0..7 по восьми соседям, 255 — стока нет.</summary>
    public byte[] FlowDirection { get; }

    public byte[] BiomeAt { get; }

    public byte[] ResourceAt { get; }

    public int[] BiomeCounts { get; }

    public int[] ResourceCounts { get; }

    public float SeaLevel { get; set; }

    /// <summary>Порог стока, с которого тайл считается рекой.</summary>
    public float RiverThreshold { get; set; }

    public int LandTiles { get; private set; }

    public int WaterTiles => TileCount - LandTiles;

    public int Seed { get; set; }

    public int Index(int x, int y) => (y * Width) + x;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public Biome BiomeOf(int index) => (Biome)BiomeAt[index];

    public Biome BiomeOf(int x, int y) => (Biome)BiomeAt[Index(x, y)];

    public ResourceKind ResourceOf(int index) => (ResourceKind)ResourceAt[index];

    public bool IsLand(int index) => Biomes.IsLand((Biome)BiomeAt[index]);

    public bool IsWater(int index) => Biomes.IsWater((Biome)BiomeAt[index]);

    /// <summary>Пересчитать сводку после генерации.</summary>
    public void Recount()
    {
        Array.Clear(BiomeCounts);
        Array.Clear(ResourceCounts);
        int land = 0;
        for (int i = 0; i < TileCount; i++)
        {
            byte biome = BiomeAt[i];
            BiomeCounts[biome]++;
            ResourceCounts[ResourceAt[i]]++;
            if (Biomes.IsLand((Biome)biome))
            {
                land++;
            }
        }

        LandTiles = land;
    }

    /// <summary>Сколько разных биомов реально встретилось на карте.</summary>
    public int DistinctBiomes(int minimumTiles = 1)
    {
        int count = 0;
        for (int i = 0; i < BiomeCounts.Length; i++)
        {
            if (BiomeCounts[i] >= minimumTiles)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Самый частый биом суши в долях от всей суши.</summary>
    public float LargestLandBiomeShare()
    {
        if (LandTiles == 0)
        {
            return 0f;
        }

        int largest = 0;
        for (int i = 0; i < BiomeCounts.Length; i++)
        {
            if (Biomes.IsLand((Biome)i) && BiomeCounts[i] > largest)
            {
                largest = BiomeCounts[i];
            }
        }

        return largest / (float)LandTiles;
    }

    /// <summary>Короткая подпись карты для проверки детерминизма.</summary>
    public ulong Checksum()
    {
        ulong hash = 1469598103934665603ul;
        for (int i = 0; i < TileCount; i++)
        {
            hash = Mix(hash, BiomeAt[i]);
            hash = Mix(hash, ResourceAt[i]);
            hash = Mix(hash, (ulong)(int)(Elevation[i] * 4096f));
            hash = Mix(hash, (ulong)(int)(Moisture[i] * 1024f));
        }

        return hash;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211ul;
        return hash;
    }
}
