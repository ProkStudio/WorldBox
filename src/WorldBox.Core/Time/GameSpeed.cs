namespace WorldBox.Core.Time;

/// <summary>Скорости игры. Клавиши пробел и 1–4.</summary>
public enum GameSpeed
{
    Paused = 0,
    X1 = 1,
    X4 = 2,
    X16 = 3,
    X64 = 4,
}

public static class GameSpeeds
{
    /// <summary>Базовая частота симуляции. От fps не зависит.</summary>
    public const int BaseTicksPerSecond = 20;

    private static readonly int[] MultipliersByIndex = { 0, 1, 4, 16, 64 };

    private static readonly string[] LabelsByIndex = { "Пауза", "x1", "x4", "x16", "x64" };

    public static int Multiplier(GameSpeed speed) => MultipliersByIndex[(int)speed];

    public static string Label(GameSpeed speed) => LabelsByIndex[(int)speed];

    public static GameSpeed Faster(GameSpeed speed) => speed >= GameSpeed.X64 ? GameSpeed.X64 : speed + 1;

    public static GameSpeed Slower(GameSpeed speed) => speed <= GameSpeed.Paused ? GameSpeed.Paused : speed - 1;
}
