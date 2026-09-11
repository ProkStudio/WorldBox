using WorldBox.Core.Diagnostics;

namespace WorldBox.Core;

/// <summary>
/// Состояние мира. На срезе S0 здесь только размер, сид, счётчик тиков и год.
/// Срез S1 добавит сюда слои карты массивами примитивов (float[] elevation и так далее).
/// Ссылок на графику здесь нет и не будет.
/// </summary>
public sealed class WorldState
{
    /// <summary>С какого года начинается партия (отрицательные — до нашей эры).</summary>
    public const int StartYear = -8000;

    public WorldState(int width, int height, int seed)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер мира должен быть положительным.");
        }

        Width = width;
        Height = height;
        Seed = seed;
        Rng = new Rng(seed);
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCount => Width * Height;

    public int Seed { get; }

    public Rng Rng { get; }

    /// <summary>Сколько тиков прошло с начала партии. Двигается только через SimulationLoop.</summary>
    public long Tick { get; internal set; }

    /// <summary>Сколько игровых лет в одном тике. Срез S4 будет менять это по эпохам из data/eras.json.</summary>
    public float YearsPerTick { get; set; } = 5f;

    public double Year => StartYear + (Tick * YearsPerTick);

    /// <summary>Замеры последних тиков. На саму симуляцию не влияют.</summary>
    public FrameStats TickStats { get; } = new FrameStats(120);

    public int Index(int x, int y) => (y * Width) + x;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Грубая контрольная сумма состояния. Используется тестом детерминизма.</summary>
    public ulong Checksum()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)Width);
            hash = Mix(hash, (ulong)Height);
            hash = Mix(hash, (ulong)(uint)Seed);
            hash = Mix(hash, (ulong)Tick);
            Span<uint> state = stackalloc uint[4];
            Rng.GetState(state);
            for (int i = 0; i < state.Length; i++)
            {
                hash = Mix(hash, state[i]);
            }

            return hash;
        }
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        unchecked
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }
}
