namespace WorldBox.Core.Time;

/// <summary>
/// Переводит реальное время кадра в целое число тиков симуляции.
/// Шаг фиксированный, поэтому при любом fps мир развивается одинаково.
/// Если компьютер не успевает, лишние тики отбрасываются — кадр всё равно рисуется,
/// игра замедляется, но не встаёт насмерть.
/// </summary>
public sealed class SimulationClock
{
    public const double TickSeconds = 1.0 / GameSpeeds.BaseTicksPerSecond;

    private double _accumulator;

    /// <summary>Текущая скорость.</summary>
    public GameSpeed Speed { get; set; } = GameSpeed.X1;

    /// <summary>Жёсткий потолок тиков за один кадр. При x64 и 60 fps нужно около 22.</summary>
    public int MaxTicksPerFrame { get; set; } = 32;

    /// <summary>Сколько реального времени накоплено и ещё не потрачено на тики.</summary>
    public double Accumulator => _accumulator;

    /// <summary>Правда, если в прошлый раз пришлось выбросить тики из-за нехватки времени.</summary>
    public bool IsBehind { get; private set; }

    public void Reset()
    {
        _accumulator = 0;
        IsBehind = false;
    }

    /// <summary>Сколько тиков надо прогнать за этот кадр.</summary>
    public int Advance(double deltaSeconds)
    {
        IsBehind = false;
        int multiplier = GameSpeeds.Multiplier(Speed);
        if (multiplier == 0)
        {
            _accumulator = 0;
            return 0;
        }

        // Защита от спирали смерти: после паузы или перетаскивания окна кадр бывает огромным.
        if (deltaSeconds > 0.25)
        {
            deltaSeconds = 0.25;
        }

        _accumulator += deltaSeconds * multiplier;
        int ticks = (int)(_accumulator / TickSeconds);
        if (ticks <= 0)
        {
            return 0;
        }

        if (ticks > MaxTicksPerFrame)
        {
            ticks = MaxTicksPerFrame;
            _accumulator = 0;
            IsBehind = true;
        }
        else
        {
            _accumulator -= ticks * TickSeconds;
        }

        return ticks;
    }
}
