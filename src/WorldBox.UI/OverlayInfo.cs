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
}
