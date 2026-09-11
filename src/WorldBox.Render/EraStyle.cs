using Microsoft.Xna.Framework;

namespace WorldBox.Render;

/// <summary>Чем эпоха украшает значок поселения на карте.</summary>
public enum SettlementDecor : byte
{
    /// <summary>Каменный век и неолит: голая отметка без украшений.</summary>
    Hut = 0,

    /// <summary>Бронза, железо, античность: вокруг появляется стена.</summary>
    Walls = 1,

    /// <summary>Средние века и порох: стена и башня.</summary>
    Castle = 2,

    /// <summary>Промышленность и нефть: над городом дым.</summary>
    Smoke = 3,

    /// <summary>Современность и космос: город светится огнями.</summary>
    Lights = 4,
}

/// <summary>
/// Цвета и правила украшений поселений. Слой рисования не знает про таблицу эпох
/// и не читает json: ему хватает номера эпохи из TribeStore.Era.
/// Границы заданы по смыслу эпох базовой таблицы, но любая другая таблица тоже будет рисоваться:
/// чем выше номер, тем богаче значок.
/// </summary>
public static class EraStyle
{
    /// <summary>Цвет крепостной стены.</summary>
    public static readonly Color Wall = new Color(232, 229, 218) * 0.8f;

    /// <summary>Цвет башни.</summary>
    public static readonly Color Tower = new Color(248, 240, 206);

    /// <summary>Цвет заводского дыма.</summary>
    public static readonly Color Smoke = new Color(136, 136, 146) * 0.75f;

    /// <summary>Цвет ночных огней.</summary>
    public static readonly Color Light = new Color(255, 231, 148);

    /// <summary>Как выглядит поселение народа в этой эпохе.</summary>
    public static SettlementDecor DecorOf(int era)
    {
        if (era <= 1)
        {
            return SettlementDecor.Hut;
        }

        if (era <= 4)
        {
            return SettlementDecor.Walls;
        }

        if (era <= 6)
        {
            return SettlementDecor.Castle;
        }

        if (era <= 8)
        {
            return SettlementDecor.Smoke;
        }

        return SettlementDecor.Lights;
    }
}
