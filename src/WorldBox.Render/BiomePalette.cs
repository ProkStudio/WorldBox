using Microsoft.Xna.Framework;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Палитра карты. Цветов нарочно мало и они приглушенные — так карта читается
/// и вблизи, и с высоты птичьего полёта, а яркие тона остаются для городов и событий.
/// </summary>
public static class BiomePalette
{
    private static readonly Color[] Table =
    {
        new Color(18, 36, 66),    // DeepOcean
        new Color(26, 58, 100),   // Ocean
        new Color(44, 96, 138),   // Coast
        new Color(214, 198, 146), // Beach
        new Color(52, 110, 160),  // Lake
        new Color(64, 130, 180),  // River
        new Color(74, 98, 72),    // Marsh
        new Color(216, 190, 122), // Desert
        new Color(176, 166, 92),  // Savanna
        new Color(150, 156, 104), // Steppe
        new Color(108, 150, 82),  // Grassland
        new Color(128, 142, 88),  // Shrubland
        new Color(60, 110, 64),   // TemperateForest
        new Color(34, 92, 58),    // Rainforest
        new Color(52, 88, 74),    // Taiga
        new Color(140, 148, 136), // Tundra
        new Color(226, 232, 238), // Glacier
        new Color(118, 114, 110), // Mountain
        new Color(192, 192, 198), // Peak
    };

    private static readonly Color[] ResourceColors =
    {
        new Color(0, 0, 0, 0),    // None
        new Color(96, 160, 84),   // Wood
        new Color(170, 170, 176), // Stone
        new Color(222, 146, 85),  // Copper
        new Color(196, 200, 214), // Tin
        new Color(146, 158, 176), // Iron
        new Color(40, 40, 46),    // Coal
        new Color(234, 194, 107), // Saltpeter
        new Color(122, 84, 168),  // Oil
        new Color(112, 224, 148), // Uranium
    };

    public static Color Of(Biome biome) => Table[(int)biome];

    public static Color Of(ResourceKind resource) => ResourceColors[(int)resource];

    /// <summary>Цвет тайла в выбранном режиме карты.</summary>
    public static Color For(MapMode mode, WorldMap map, int index) => mode switch
    {
        MapMode.Height => HeightColor(map, index),
        MapMode.Temperature => TemperatureColor(map, index),
        MapMode.Moisture => MoistureColor(map, index),
        MapMode.Fertility => FertilityColor(map, index),
        MapMode.Resources => ResourceColor(map, index),
        _ => TerrainColor(map, index),
    };

    public static Color TerrainColor(WorldMap map, int index)
    {
        var biome = (Biome)map.BiomeAt[index];
        Color color = Table[(int)biome];
        float elevation = map.Elevation[index];

        if (Biomes.IsWater(biome))
        {
            float depth = Math.Clamp((map.SeaLevel - elevation) * 4f, 0f, 1f);
            return Shade(color, 1f - (depth * 0.35f));
        }

        // Светлее на возвышенностях и темнее в низинах: рельеф читается без теней.
        float above = Math.Clamp((elevation - map.SeaLevel) * 2.2f, 0f, 1f);
        return Shade(color, 0.88f + (above * 0.30f));
    }

    private static Color HeightColor(WorldMap map, int index)
    {
        float elevation = map.Elevation[index];
        if (elevation < map.SeaLevel)
        {
            float depth = Math.Clamp((map.SeaLevel - elevation) / MathF.Max(map.SeaLevel, 0.0001f), 0f, 1f);
            return Color.Lerp(new Color(64, 132, 186), new Color(8, 18, 46), depth);
        }

        float above = Math.Clamp((elevation - map.SeaLevel) / MathF.Max(1f - map.SeaLevel, 0.0001f), 0f, 1f);
        if (above < 0.35f)
        {
            return Color.Lerp(new Color(58, 108, 62), new Color(148, 146, 88), above / 0.35f);
        }

        if (above < 0.7f)
        {
            return Color.Lerp(new Color(148, 146, 88), new Color(132, 104, 80), (above - 0.35f) / 0.35f);
        }

        return Color.Lerp(new Color(132, 104, 80), Color.White, (above - 0.7f) / 0.3f);
    }

    private static Color TemperatureColor(WorldMap map, int index)
    {
        float t = map.Temperature[index];
        if (t < 0.5f)
        {
            return Color.Lerp(new Color(40, 70, 170), new Color(96, 190, 170), t / 0.5f);
        }

        return Color.Lerp(new Color(96, 190, 170), new Color(214, 78, 60), (t - 0.5f) / 0.5f);
    }

    private static Color MoistureColor(WorldMap map, int index)
    {
        float m = map.Moisture[index];
        return Color.Lerp(new Color(198, 168, 104), new Color(36, 96, 176), m);
    }

    private static Color FertilityColor(WorldMap map, int index)
    {
        if (map.IsWater(index))
        {
            return new Color(24, 34, 48);
        }

        float f = map.Fertility[index];
        return Color.Lerp(new Color(76, 62, 48), new Color(126, 228, 108), f);
    }

    private static Color ResourceColor(WorldMap map, int index)
    {
        var resource = (ResourceKind)map.ResourceAt[index];
        if (resource == ResourceKind.None)
        {
            Color terrain = TerrainColor(map, index);
            byte gray = (byte)((terrain.R + terrain.G + terrain.B) / 5);
            return new Color(gray, gray, (byte)(gray + 6));
        }

        return ResourceColors[(int)resource];
    }

    private static Color Shade(Color color, float factor)
    {
        return new Color(
            (byte)Math.Clamp(color.R * factor, 0f, 255f),
            (byte)Math.Clamp(color.G * factor, 0f, 255f),
            (byte)Math.Clamp(color.B * factor, 0f, 255f));
    }
}
