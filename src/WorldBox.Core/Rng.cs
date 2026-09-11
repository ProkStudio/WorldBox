namespace WorldBox.Core;

/// <summary>
/// Единственный источник случайности во всём проекте (xorshift128).
/// System.Random в симуляции запрещён: он не даёт гарантии одинаковой
/// последовательности между версиями .NET, а нам нужен повторяемый мир по сиду.
/// Все методы без аллокаций.
/// </summary>
public sealed class Rng
{
    private uint _x;
    private uint _y;
    private uint _z;
    private uint _w;

    public Rng(int seed)
    {
        Seed = seed;
        Reset(seed);
    }

    /// <summary>Сид, с которого генератор был инициализирован в последний раз.</summary>
    public int Seed { get; private set; }

    public void Reset(int seed)
    {
        Seed = seed;
        ulong state = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x0123456789ABCDEFUL);
        _x = (uint)(SplitMix64(ref state) >> 16);
        _y = (uint)(SplitMix64(ref state) >> 16);
        _z = (uint)(SplitMix64(ref state) >> 16);
        _w = (uint)(SplitMix64(ref state) >> 16);
        if ((_x | _y | _z | _w) == 0u)
        {
            _x = 0x9E3779B9u;
        }
    }

    /// <summary>Поток для отдельной подсистемы: тот же сид мира, но независимая последовательность.</summary>
    public Rng Fork(int streamId) => new Rng(unchecked(Seed * 73856093 + streamId * 19349663));

    private static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public uint NextUInt()
    {
        unchecked
        {
            uint t = _x ^ (_x << 11);
            _x = _y;
            _y = _z;
            _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }
    }

    /// <summary>Целое в [0, maxExclusive). Без остатка от деления, чтобы не было перекоса.</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1)
        {
            return 0;
        }

        return (int)(((ulong)NextUInt() * (ulong)(uint)maxExclusive) >> 32);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return minInclusive + NextInt(maxExclusive - minInclusive);
    }

    /// <summary>Дробное в [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    public bool Chance(float probability) => NextFloat() < probability;

    /// <summary>Состояние для сохранения игры.</summary>
    public void GetState(Span<uint> destination)
    {
        destination[0] = _x;
        destination[1] = _y;
        destination[2] = _z;
        destination[3] = _w;
    }

    public void SetState(ReadOnlySpan<uint> source)
    {
        _x = source[0];
        _y = source[1];
        _z = source[2];
        _w = source[3];
    }
}
