using System.Globalization;

namespace WorldBox.Render.Text;

/// <summary>
/// Сборщик строк без аллокаций. В цикле рендера конкатенация string запрещена:
/// каждый кадр — это мусор для сборщика и рывки на быстрой скорости.
/// </summary>
public sealed class TextBuilder
{
    private readonly char[] _buffer;
    private int _length;

    public TextBuilder(int capacity = 256)
    {
        _buffer = new char[Math.Max(16, capacity)];
    }

    public int Length => _length;

    public ReadOnlySpan<char> Span => _buffer.AsSpan(0, _length);

    public TextBuilder Clear()
    {
        _length = 0;
        return this;
    }

    public TextBuilder Append(char value)
    {
        if (_length < _buffer.Length)
        {
            _buffer[_length++] = value;
        }

        return this;
    }

    public TextBuilder Append(ReadOnlySpan<char> value)
    {
        int count = Math.Min(value.Length, _buffer.Length - _length);
        for (int i = 0; i < count; i++)
        {
            _buffer[_length + i] = value[i];
        }

        _length += count;
        return this;
    }

    public TextBuilder Append(int value)
    {
        if (value.TryFormat(_buffer.AsSpan(_length), out int written, default, CultureInfo.InvariantCulture))
        {
            _length += written;
        }

        return this;
    }

    public TextBuilder Append(long value)
    {
        if (value.TryFormat(_buffer.AsSpan(_length), out int written, default, CultureInfo.InvariantCulture))
        {
            _length += written;
        }

        return this;
    }

    /// <summary>Дробное число с заданным числом знаков после запятой.</summary>
    public TextBuilder Append(double value, int decimals)
    {
        Span<char> format = stackalloc char[2];
        format[0] = 'F';
        format[1] = (char)('0' + Math.Clamp(decimals, 0, 9));
        if (value.TryFormat(_buffer.AsSpan(_length), out int written, format, CultureInfo.InvariantCulture))
        {
            _length += written;
        }

        return this;
    }

    /// <summary>Большое число с разделителем тысяч пробелом: 12 480.</summary>
    public TextBuilder AppendGrouped(long value)
    {
        if (value < 0)
        {
            Append('-');
            value = -value;
        }

        Span<char> digits = stackalloc char[20];
        int count = 0;
        do
        {
            digits[count++] = (char)('0' + (int)(value % 10));
            value /= 10;
        }
        while (value > 0);

        for (int i = count - 1; i >= 0; i--)
        {
            Append(digits[i]);
            if (i > 0 && i % 3 == 0)
            {
                Append(' ');
            }
        }

        return this;
    }

    /// <summary>Игровой год: отрицательные превращаются в «до н.э.».</summary>
    public TextBuilder AppendYear(double year)
    {
        long rounded = (long)Math.Round(year);
        if (rounded < 0)
        {
            AppendGrouped(-rounded);
            Append(" до н.э.");
        }
        else
        {
            AppendGrouped(rounded);
            Append(" н.э.");
        }

        return this;
    }

    public override string ToString() => new string(_buffer, 0, _length);
}
