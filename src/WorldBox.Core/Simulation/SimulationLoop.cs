using System.Diagnostics;

namespace WorldBox.Core.Simulation;

/// <summary>
/// Цикл симуляции. Здесь и только здесь задан порядок систем внутри тика.
/// Аллокаций за тик нет: все буферы выделяются в конструкторе.
/// </summary>
public sealed class SimulationLoop
{
    private readonly ISimulationSystem[] _systems;
    private readonly double[] _lastMs;
    private readonly double[] _totalMs;

    public SimulationLoop(WorldState world, params ISimulationSystem[] systems)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        _systems = systems ?? Array.Empty<ISimulationSystem>();
        _lastMs = new double[_systems.Length];
        _totalMs = new double[_systems.Length];
    }

    public WorldState World { get; }

    public int SystemCount => _systems.Length;

    /// <summary>Собирать ли время каждой системы. В Bench всегда да, в игре можно выключить.</summary>
    public bool Profiling { get; set; } = true;

    public string SystemName(int index) => _systems[index].Name;

    /// <summary>Миллисекунды, потраченные системой в последнем тике.</summary>
    public double SystemLastMs(int index) => _lastMs[index];

    /// <summary>Суммарные миллисекунды системы за всё время прогона.</summary>
    public double SystemTotalMs(int index) => _totalMs[index];

    public void RunTick()
    {
        long tickStart = Stopwatch.GetTimestamp();
        World.Tick++;

        for (int i = 0; i < _systems.Length; i++)
        {
            ISimulationSystem system = _systems[i];
            int interval = system.Interval;
            if (interval > 1 && World.Tick % interval != 0)
            {
                continue;
            }

            if (Profiling)
            {
                long start = Stopwatch.GetTimestamp();
                system.Tick(World);
                double ms = ToMs(Stopwatch.GetTimestamp() - start);
                _lastMs[i] = ms;
                _totalMs[i] += ms;
            }
            else
            {
                system.Tick(World);
            }
        }

        World.TickStats.Add(ToMs(Stopwatch.GetTimestamp() - tickStart));
    }

    public void RunTicks(int count)
    {
        for (int i = 0; i < count; i++)
        {
            RunTick();
        }
    }

    private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
