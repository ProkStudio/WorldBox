using Microsoft.Xna.Framework;
using WorldBox.Core.Art;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Палитра карты. Базовые цвета биомов берутся из <see cref="ArtPalette"/>, поэтому вид
/// с высоты и вид вблизи — один и тот же мир, а не две разные игры. Режимы карты
/// (высота, температура, влажность, плодородие, ресурсы) остаются со своими шкалами.
/// </summary>
public static class BiomePalette
{
    private static readonly Color[] Table = BuildTable();

    private static readonly Color[] ResourceColors =
    {
        new Color(0, 0, 0, 0),    // None
        new Color(104, 180, 92),  // Wood
        new Color(184, 184, 190), // Stone
        new Color(236, 152, 84),  // Copper
        new Color(208, 214, 228), // Tin
        new Color(156, 170, 190), // Iron
        new Color(52, 52, 60),    // Coal
        new Color(246, 206, 112), // Saltpeter
        new Color(134, 92, 184),  // Oil
        new Color(120, 236, 156), // Uranium
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
            // Глубина уводит воду в синеву, отмель остаётся светлой и бирюзовой.
            float depth = Math.Clamp((map.SeaLevel - elevation) * 4f, 0f, 1f);
            return Shade(color, 1f - (depth * 0.30f));
        }

        // Светлее на возвышенностях и темнее в низинах: рельеф читается без теней.
        float above = Math.Clamp((elevation - map.SeaLevel) * 2.2f, 0f, 1f);
        return Shade(color, 0.92f + (above * 0.22f));
    }

    private static Color[] BuildTable()
    {
        var table = new Color[Biomes.Count];
        for (int index = 0; index < table.Length; index++)
        {
            table[index] = ArtPalette.Base((Biome)index).ToColor();
        }

        return table;
    }

    private static Color HeightColor(WorldMap map, int index)
    {
        float elevation = map.Elevation[index];
        if (elevation < map.SeaLevel)
        {
            float depth = Math.Clamp((map.SeaLevel - elevation) / MathF.Max(map.SeaLevel, 0.0001f), 0f, 1f);
            return Color.Lerp(new Color(86, 168, 226), new Color(10, 24, 58), depth);
        }

        float above = Math.Clamp((elevation - map.SeaLevel) / MathF.Max(1f - map.SeaLevel, 0.0001f), 0f, 1f);
        if (above < 0.35f)
        {
            return Color.Lerp(new Color(72, 150, 78), new Color(196, 190, 104), above / 0.35f);
        }

        if (above < 0.7f)
        {
            return Color.Lerp(new Color(196, 190, 104), new Color(158, 120, 88), (above - 0.35f) / 0.35f);
        }

        return Color.Lerp(new Color(158, 120, 88), Color.White, (above - 0.7f) / 0.3f);
    }

    private static Color TemperatureColor(WorldMap map, int index)
    {
        float t = map.Temperature[index];
        if (t < 0.5f)
        {
            return Color.Lerp(new Color(48, 84, 200), new Color(104, 208, 186), t / 0.5f);
        }

        return Color.Lerp(new Color(104, 208, 186), new Color(232, 84, 64), (t - 0.5f) / 0.5f);
    }

    private static Color MoistureColor(WorldMap map, int index)
    {
        float m = map.Moisture[index];
        return Color.Lerp(new Color(216, 184, 112), new Color(40, 108, 196), m);
    }

    private static Color FertilityColor(WorldMap map, int index)
    {
        if (map.IsWater(index))
        {
            return new Color(26, 38, 54);
        }

        float f = map.Fertility[index];
        return Color.Lerp(new Color(84, 68, 52), new Color(136, 240, 116), f);
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
