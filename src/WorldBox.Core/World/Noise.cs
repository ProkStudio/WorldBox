namespace WorldBox.Core.World;

/// <summary>
/// Простой детерминированный шум по узлам решётки. Всё считается из сида и координат,
/// память не выделяется, результат одинаков на любой машине.
/// </summary>
public sealed class Noise
{
    private readonly int _seed;

    public Noise(int seed)
    {
        _seed = seed;
    }

    /// <summary>Значение шума в точке, 0..1.</summary>
    public float Value(float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = Smooth(x - x0);
        float fy = Smooth(y - y0);

        float v00 = Hash01(_seed, x0, y0);
        float v10 = Hash01(_seed, x0 + 1, y0);
        float v01 = Hash01(_seed, x0, y0 + 1);
        float v11 = Hash01(_seed, x0 + 1, y0 + 1);

        float top = v00 + ((v10 - v00) * fx);
        float bottom = v01 + ((v11 - v01) * fx);
        return top + ((bottom - top) * fy);
    }

    /// <summary>Сумма октав: крупные формы плюс мелкие детали. Результат 0..1.</summary>
    public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Value(x * frequency, y * frequency) * amplitude;
            total += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return total > 0f ? sum / total : 0f;
    }

    /// <summary>Гребневой шум: даёт горные хребты вместо круглых холмов. Результат 0..1.</summary>
    public float Ridged(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float value = Value(x * frequency, y * frequency);
            float ridge = 1f - MathF.Abs((value * 2f) - 1f);
            sum += ridge * ridge * amplitude;
            total += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return total > 0f ? sum / total : 0f;
    }

    private static float Smooth(float t) => t * t * (3f - (2f * t));

    private static float Hash01(int seed, int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }
}
