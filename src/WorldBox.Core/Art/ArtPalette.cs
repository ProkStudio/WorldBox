using WorldBox.Core.World;

namespace WorldBox.Core.Art;

/// <summary>
/// Цвет в ядре: три байта и никакой зависимости от MonoGame. Слой отрисовки превращает
/// его в Color одной строкой, поэтому вся палитра живёт рядом с рисовалкой тайлов.
/// </summary>
public readonly struct ArtColor : IEquatable<ArtColor>
{
    public ArtColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public byte R { get; }

    public byte G { get; }

    public byte B { get; }

    public static ArtColor Rgb(int r, int g, int b)
    {
        return new ArtColor(Clamp(r), Clamp(g), Clamp(b));
    }

    /// <summary>Умножение яркости: меньше единицы — тень, больше — блик.</summary>
    public ArtColor Scaled(float factor)
    {
        return Rgb(
            (int)MathF.Round(R * factor),
            (int)MathF.Round(G * factor),
            (int)MathF.Round(B * factor));
    }

    /// <summary>Плавный переход между двумя цветами, доля второго от 0 до 1.</summary>
    public static ArtColor Lerp(ArtColor from, ArtColor to, float t)
    {
        float k = t < 0f ? 0f : (t > 1f ? 1f : t);
        return Rgb(
            (int)MathF.Round(from.R + ((to.R - from.R) * k)),
            (int)MathF.Round(from.G + ((to.G - from.G) * k)),
            (int)MathF.Round(from.B + ((to.B - from.B) * k)));
    }

    public uint Packed => ((uint)R << 16) | ((uint)G << 8) | B;

    public bool Equals(ArtColor other)
    {
        return R == other.R && G == other.G && B == other.B;
    }

    public override bool Equals(object? obj)
    {
        return obj is ArtColor other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Packed;
    }

    public override string ToString()
    {
        return $"#{R:X2}{G:X2}{B:X2}";
    }

    public static bool operator ==(ArtColor left, ArtColor right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ArtColor left, ArtColor right)
    {
        return !left.Equals(right);
    }

    private static byte Clamp(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 255 ? (byte)255 : (byte)value;
    }
}

/// <summary>
/// Палитра ближнего плана. На каждый биом четыре подобранных руками цвета:
/// 0 базовый, 1 тень, 2 блик, 3 деталь. Раньше все четыре получались умножением
/// одного цвета на коэффициент — картинка выходила блёклой и серой. Теперь тон
/// каждого оттенка задан отдельно, поэтому трава сочная, вода глубокая, а песок тёплый.
/// Порядок строк строго совпадает с перечислением <see cref="Biome"/>.
/// </summary>
public static class ArtPalette
{
    /// <summary>Сколько оттенков на биом. Совпадает с <see cref="TileArt.Shades"/>.</summary>
    public const int Shades = 4;

    /// <summary>Цвет пены и барашков на волнах.</summary>
    private static readonly ArtColor FoamColor = ArtColor.Rgb(226, 246, 255);

    private static readonly ArtColor[] Ramps =
    {
        // глубокий океан
        ArtColor.Rgb(26, 72, 150), ArtColor.Rgb(18, 52, 118), ArtColor.Rgb(48, 106, 192), ArtColor.Rgb(14, 42, 98),
        // океан
        ArtColor.Rgb(34, 98, 188), ArtColor.Rgb(22, 74, 152), ArtColor.Rgb(64, 138, 224), ArtColor.Rgb(18, 62, 132),
        // прибрежная вода
        ArtColor.Rgb(56, 148, 222), ArtColor.Rgb(36, 116, 190), ArtColor.Rgb(104, 194, 246), ArtColor.Rgb(30, 100, 170),
        // пляж
        ArtColor.Rgb(240, 224, 160), ArtColor.Rgb(214, 192, 124), ArtColor.Rgb(252, 242, 198), ArtColor.Rgb(198, 172, 108),
        // озеро
        ArtColor.Rgb(48, 134, 216), ArtColor.Rgb(32, 104, 180), ArtColor.Rgb(92, 180, 240), ArtColor.Rgb(26, 88, 156),
        // река
        ArtColor.Rgb(62, 152, 228), ArtColor.Rgb(40, 120, 196), ArtColor.Rgb(110, 200, 248), ArtColor.Rgb(34, 104, 176),
        // болото
        ArtColor.Rgb(88, 120, 76), ArtColor.Rgb(62, 92, 58), ArtColor.Rgb(124, 158, 98), ArtColor.Rgb(48, 74, 52),
        // пустыня
        ArtColor.Rgb(238, 206, 132), ArtColor.Rgb(212, 176, 102), ArtColor.Rgb(250, 230, 172), ArtColor.Rgb(196, 158, 88),
        // саванна
        ArtColor.Rgb(206, 190, 104), ArtColor.Rgb(176, 158, 80), ArtColor.Rgb(230, 216, 142), ArtColor.Rgb(156, 138, 68),
        // степь
        ArtColor.Rgb(170, 188, 104), ArtColor.Rgb(140, 156, 82), ArtColor.Rgb(200, 214, 138), ArtColor.Rgb(126, 140, 72),
        // луга
        ArtColor.Rgb(104, 188, 88), ArtColor.Rgb(72, 152, 64), ArtColor.Rgb(144, 216, 112), ArtColor.Rgb(58, 130, 56),
        // кустарник
        ArtColor.Rgb(130, 180, 92), ArtColor.Rgb(98, 146, 70), ArtColor.Rgb(166, 204, 122), ArtColor.Rgb(84, 126, 62),
        // широколиственный лес
        ArtColor.Rgb(58, 148, 72), ArtColor.Rgb(36, 112, 54), ArtColor.Rgb(88, 180, 98), ArtColor.Rgb(26, 90, 46),
        // дождевой лес
        ArtColor.Rgb(34, 130, 64), ArtColor.Rgb(20, 96, 48), ArtColor.Rgb(62, 164, 88), ArtColor.Rgb(14, 76, 42),
        // тайга
        ArtColor.Rgb(48, 120, 86), ArtColor.Rgb(30, 92, 66), ArtColor.Rgb(76, 150, 110), ArtColor.Rgb(24, 74, 56),
        // тундра
        ArtColor.Rgb(158, 172, 150), ArtColor.Rgb(128, 142, 122), ArtColor.Rgb(192, 202, 182), ArtColor.Rgb(114, 128, 112),
        // ледник
        ArtColor.Rgb(226, 242, 250), ArtColor.Rgb(192, 216, 234), ArtColor.Rgb(248, 253, 255), ArtColor.Rgb(172, 202, 228),
        // горы
        ArtColor.Rgb(136, 132, 128), ArtColor.Rgb(104, 100, 98), ArtColor.Rgb(174, 170, 166), ArtColor.Rgb(88, 84, 82),
        // пик
        ArtColor.Rgb(238, 244, 250), ArtColor.Rgb(204, 214, 228), ArtColor.Rgb(253, 254, 255), ArtColor.Rgb(186, 198, 214),
    };

    /// <summary>Сколько биомов описано. Должно совпадать с <see cref="Biomes.Count"/>.</summary>
    public static int BiomeCount => Ramps.Length / Shades;

    /// <summary>Цвет оттенка: 0 базовый, 1 тень, 2 блик, 3 деталь.</summary>
    public static ArtColor Ramp(Biome biome, int shade)
    {
        int index = (int)biome;
        if (index < 0 || index >= BiomeCount)
        {
            index = 0;
        }

        int safe = shade < 0 ? 0 : shade % Shades;
        return Ramps[(index * Shades) + safe];
    }

    public static ArtColor Base(Biome biome)
    {
        return Ramp(biome, 0);
    }

    public static ArtColor Shadow(Biome biome)
    {
        return Ramp(biome, 1);
    }

    public static ArtColor Highlight(Biome biome)
    {
        return Ramp(biome, 2);
    }

    public static ArtColor Detail(Biome biome)
    {
        return Ramp(biome, 3);
    }

    /// <summary>Светлая кромка воды у берега.</summary>
    public static ArtColor Foam(Biome biome)
    {
        return ArtColor.Lerp(Highlight(biome), FoamColor, 0.55f);
    }

    /// <summary>Тёмный контур: рисуется под спрайтами, чтобы они не сливались с землёй.</summary>
    public static ArtColor Outline(Biome biome)
    {
        return Shadow(biome).Scaled(0.62f);
    }
}
