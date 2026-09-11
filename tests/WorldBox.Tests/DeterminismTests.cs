using WorldBox.Core;
using WorldBox.Core.Simulation;
using Xunit;

namespace WorldBox.Tests;

/// <summary>
/// Главный тест проекта: один сид и одинаковое число тиков дают одинаковый мир.
/// Если он красный — сломаны сохранения, повтор багов и воспроизводимость замеров.
/// </summary>
public class DeterminismTests
{
    private sealed class NoiseSystem : ISimulationSystem
    {
        public string Name => "шум";

        public void Tick(WorldState world)
        {
            world.Rng.NextUInt();
            world.Rng.NextInt(100);
        }
    }

    private sealed class EveryFifthSystem : ISimulationSystem
    {
        public string Name => "каждый пятый";

        public int Interval => 5;

        public int Runs { get; private set; }

        public void Tick(WorldState world) => Runs++;
    }

    private static ulong Run(int seed, int ticks)
    {
        var world = new WorldState(64, 64, seed);
        var loop = new SimulationLoop(world, new NoiseSystem());
        loop.RunTicks(ticks);
        return world.Checksum();
    }

    [Fact]
    public void Одинаковый_сид_даёт_одинаковый_мир()
    {
        Assert.Equal(Run(2026, 1000), Run(2026, 1000));
    }

    [Fact]
    public void Разные_сиды_дают_разные_миры()
    {
        Assert.NotEqual(Run(1, 500), Run(2, 500));
    }

    [Fact]
    public void Система_с_интервалом_запускается_реже()
    {
        var world = new WorldState(8, 8, 5);
        var every5 = new EveryFifthSystem();
        var loop = new SimulationLoop(world, every5);
        loop.RunTicks(100);
        Assert.Equal(20, every5.Runs);
        Assert.Equal(100, world.Tick);
    }

    [Fact]
    public void Год_считается_от_восьми_тысяч_до_нашей_эры()
    {
        var world = new WorldState(8, 8, 1);
        Assert.Equal(-8000, world.Year);
        var loop = new SimulationLoop(world);
        loop.RunTicks(100);
        Assert.Equal(-7500, world.Year);
    }
}
