using WorldBox.Core.Time;

namespace WorldBox.UI;

/// <summary>Всё, что отладочный оверлей показывает за кадр. Обычная структура, без аллокаций.</summary>
public struct OverlayInfo
{
    public double Fps;
    public double FrameMs;
    public double FrameMsWorst;
    public double SimMs;
    public long Ticks;
    public double Year;
    public GameSpeed Speed;
    public bool IsBehind;
    public float Zoom;
    public float VisibleTiles;
    public int CursorX;
    public int CursorY;
    public bool CursorInside;
    public int WorldWidth;
    public int WorldHeight;
    public int People;
    public int Births;
    public int Deaths;
    public int Tribes;
    public int Settlements;

    /// <summary>Сколько игровых лет сейчас в одном тике. Меняется вместе с эпохой.</summary>
    public float YearsPerTick;

    /// <summary>Имя самого многолюдного народа. Ссылка на готовую строку, новых строк за кадр нет.</summary>
    public string? LargestTribe;

    /// <summary>Имя самой передовой эпохи мира. null — таблица эпох не загружена, строка не рисуется.</summary>
    public string? EraName;
}
