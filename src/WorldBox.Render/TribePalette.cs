using Microsoft.Xna.Framework;

namespace WorldBox.Render;

/// <summary>
/// Цвета народов. Двенадцать заметно разных оттенков, чтобы соседние
/// державы не сливались на карте. Ядро хранит только номер цвета: сам цвет живёт здесь.
/// </summary>
public static class TribePalette
{
    private static readonly Color[] Colors =
    {
        new Color(226, 92, 84),
        new Color(86, 158, 232),
        new Color(116, 196, 118),
        new Color(240, 190, 88),
        new Color(176, 126, 220),
        new Color(238, 140, 84),
        new Color(96, 206, 200),
        new Color(230, 130, 180),
        new Color(154, 176, 96),
        new Color(140, 150, 230),
        new Color(204, 120, 96),
        new Color(120, 200, 150),
    };

    public static int Count => Colors.Length;

    public static Color Of(byte colorIndex) => Colors[colorIndex % Colors.Length];
}
