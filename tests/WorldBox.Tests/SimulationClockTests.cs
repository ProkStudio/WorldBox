using WorldBox.Core.Time;
using Xunit;

namespace WorldBox.Tests;

public class SimulationClockTests
{
    [Fact]
    public void На_паузе_тиков_нет()
    {
        var clock = new SimulationClock { Speed = GameSpeed.Paused };
        Assert.Equal(0, clock.Advance(1.0));
    }

    [Fact]
    public void Одна_секунда_на_x1_это_двадцать_тиков()
    {
        var clock = new SimulationClock { Speed = GameSpeed.X1 };
        int total = 0;
        for (int frame = 0; frame < 60; frame++)
        {
            total += clock.Advance(1.0 / 60.0);
        }

        Assert.InRange(total, 19, 20);
    }

    [Fact]
    public void Ускорение_умножает_число_тиков()
    {
        var clock = new SimulationClock { Speed = GameSpeed.X4 };
        int total = 0;
        for (int frame = 0; frame < 60; frame++)
        {
            total += clock.Advance(1.0 / 60.0);
        }

        Assert.InRange(total, 78, 80);
    }

    [Fact]
    public void Больше_потолка_за_кадр_не_выдаётся()
    {
        var clock = new SimulationClock { Speed = GameSpeed.X64, MaxTicksPerFrame = 8 };
        Assert.Equal(8, clock.Advance(1.0));
        Assert.True(clock.IsBehind);
    }

    [Fact]
    public void Огромный_кадр_не_вызывает_лавины()
    {
        var clock = new SimulationClock { Speed = GameSpeed.X1 };
        int ticks = clock.Advance(30.0);
        Assert.InRange(ticks, 1, clock.MaxTicksPerFrame);
    }

    [Fact]
    public void Скорости_переключаются_без_выхода_за_границы()
    {
        Assert.Equal(GameSpeed.Paused, GameSpeeds.Slower(GameSpeed.Paused));
        Assert.Equal(GameSpeed.X64, GameSpeeds.Faster(GameSpeed.X64));
        Assert.Equal(GameSpeed.X4, GameSpeeds.Faster(GameSpeed.X1));
        Assert.Equal(64, GameSpeeds.Multiplier(GameSpeed.X64));
    }
}
