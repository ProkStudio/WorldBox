namespace WorldBox.Core.Diagnostics;

/// <summary>
/// Кольцевой буфер замеров в миллисекундах: среднее, максимум, процентили.
/// После создания ничего не выделяет в памяти.
/// </summary>
public sealed class FrameStats
{
    private readonly double[] _samples;
    private readonly double[] _scratch;
    private int _next;
    private int _count;
    private double _sum;

    public FrameStats(int capacity)
    {
        if (capacity <= 0)
        {
            capacity = 1;
        }

        _samples = new double[capacity];
        _scratch = new double[capacity];
    }

    public int Count => _count;

    public double Last { get; private set; }

    public double Average => _count == 0 ? 0 : _sum / _count;

    public double PerSecond => Average > 0.0001 ? 1000.0 / Average : 0;

    public void Add(double milliseconds)
    {
        Last = milliseconds;
        if (_count == _samples.Length)
        {
            _sum -= _samples[_next];
        }
        else
        {
            _count++;
        }

        _samples[_next] = milliseconds;
        _sum += milliseconds;
        _next = (_next + 1) % _samples.Length;
    }

    public double Max()
    {
        double max = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_samples[i] > max)
            {
                max = _samples[i];
            }
        }

        return max;
    }

    /// <summary>Процентиль, например 0.99 — худший процент кадров.</summary>
    public double Percentile(double fraction)
    {
        if (_count == 0)
        {
            return 0;
        }

        Array.Copy(_samples, _scratch, _count);
        Array.Sort(_scratch, 0, _count);
        int index = (int)(fraction * (_count - 1));
        if (index < 0)
        {
            index = 0;
        }

        if (index >= _count)
        {
            index = _count - 1;
        }

        return _scratch[index];
    }

    public void Clear()
    {
        _next = 0;
        _count = 0;
        _sum = 0;
        Last = 0;
    }
}
