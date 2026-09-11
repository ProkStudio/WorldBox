using WorldBox.Core.Diagnostics;
using WorldBox.Core.World;

namespace WorldBox.Core;

/// <summary>
/// Состояние мира: размер, сид, счётчик тиков, год и слои карты.
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

    /// <summary>Слои карты: высота, влага, температура, биомы, ресурсы. Заполняет генератор на старте.</summary>
    public WorldMap? Map { get; private set; }

    /// <summary>Сколько тиков прошло с начала партии. Двигается только через SimulationLoop.</summary>
    public long Tick { get; internal set; }

    /// <summary>Сколько игровых лет в одном тике. Срез S4 будет менять это по эпохам из data/eras.json.</summary>
    public float YearsPerTick { get; set; } = 5f;

    public double Year => StartYear + (Tick * YearsPerTick);

    /// <summary>Замеры последних тиков. На саму симуляцию не влияют.</summary>
    public FrameStats TickStats { get; } = new FrameStats(120);

    public int Index(int x, int y) => (y * Width) + x;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Привязать сгенерированную карту. Размер карты должен совпадать с размером мира.</summary>
    public void SetMap(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Width != Width || map.Height != Height)
        {
            throw new ArgumentException("Размер карты не совпадает с размером мира.", nameof(map));
        }

        Map = map;
    }

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

            if (Map != null)
            {
                hash = Mix(hash, Map.Checksum());
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
