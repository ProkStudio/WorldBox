using WorldBox.Core;
using Xunit;

namespace WorldBox.Tests;

public class RngTests
{
    [Fact]
    public void Одинаковый_сид_даёт_одинаковую_последовательность()
    {
        var a = new Rng(12345);
        var b = new Rng(12345);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextUInt(), b.NextUInt());
        }
    }

    [Fact]
    public void Разные_сиды_дают_разные_последовательности()
    {
        var a = new Rng(1);
        var b = new Rng(2);
        int same = 0;
        for (int i = 0; i < 100; i++)
        {
            if (a.NextUInt() == b.NextUInt())
            {
                same++;
            }
        }

        Assert.True(same < 5);
    }

    [Fact]
    public void Целые_в_заданном_диапазоне()
    {
        var rng = new Rng(7);
        for (int i = 0; i < 10000; i++)
        {
            int value = rng.NextInt(3, 9);
            Assert.InRange(value, 3, 8);
        }
    }

    [Fact]
    public void Дробные_в_полуинтервале_от_нуля_до_единицы()
    {
        var rng = new Rng(99);
        for (int i = 0; i < 10000; i++)
        {
            float value = rng.NextFloat();
            Assert.True(value >= 0f && value < 1f);
        }
    }

    [Fact]
    public void Состояние_восстанавливается_для_сохранений()
    {
        var rng = new Rng(555);
        for (int i = 0; i < 50; i++)
        {
            rng.NextUInt();
        }

        uint[] state = new uint[4];
        rng.GetState(state);
        uint expected = rng.NextUInt();

        var restored = new Rng(0);
        restored.SetState(state);
        Assert.Equal(expected, restored.NextUInt());
    }
}
