using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Time;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Угловой оверлей с fps, временем кадра и состоянием мира.
/// Строки собираются в переиспользуемый буфер: за кадр ноль аллокаций.
/// </summary>
public sealed class DebugOverlay
{
    private static readonly Color PanelColor = new Color(10, 12, 16, 190);
    private static readonly Color BorderColor = new Color(255, 255, 255, 40);
    private static readonly Color TextColor = new Color(232, 232, 232);
    private static readonly Color AccentColor = new Color(94, 159, 232);
    private static readonly Color WarningColor = new Color(233, 115, 102);
    private static readonly Color PeopleColor = new Color(246, 214, 160);
    private static readonly Color TribesColor = new Color(158, 204, 172);

    private readonly TextBuilder _line = new TextBuilder(256);

    public bool Visible { get; set; } = true;

    public bool HintVisible { get; set; } = true;

    public int Scale { get; set; } = 2;

    public void Draw(SpriteBatch batch, PixelFont font, Primitives primitives, in OverlayInfo info, int viewportWidth, int viewportHeight)
    {
        if (Visible)
        {
            DrawPanel(batch, font, primitives, in info);
        }

        if (HintVisible)
        {
            DrawHint(batch, font, primitives, viewportWidth, viewportHeight);
        }
    }

    private void DrawPanel(SpriteBatch batch, PixelFont font, Primitives primitives, in OverlayInfo info)
    {
        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;
        int lines = info.IsBehind ? 8 : 7;
        var panel = new Rectangle(12, 12, 620, (step * lines) + (10 * scale));
        primitives.FillRect(batch, panel, PanelColor);
        primitives.FrameRect(batch, panel, BorderColor);

        int x = panel.X + (6 * scale);
        int y = panel.Y + (5 * scale);

        _line.Clear()
            .Append(Strings.Get("hud.fps")).Append(' ').Append((int)Math.Round(info.Fps))
            .Append("   ").Append(Strings.Get("hud.frame")).Append(' ').Append(info.FrameMs, 1).Append(' ').Append(Strings.Get("hud.ms"))
            .Append("   ").Append(Strings.Get("hud.worst")).Append(' ').Append(info.FrameMsWorst, 1);
        font.Draw(batch, _line.Span, new Vector2(x, y), AccentColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.sim")).Append(' ').Append(info.SimMs, 2).Append(' ').Append(Strings.Get("hud.ms"))
            .Append("   ").Append(Strings.Get("hud.ticks")).Append(' ').AppendGrouped(info.Ticks)
            .Append("   ").Append(Strings.Get("hud.speed")).Append(' ').Append(SpeedLabel(info.Speed));
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.year")).Append(' ').AppendYear(info.Year);
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.people")).Append(' ').AppendGrouped(info.People)
            .Append("   ").Append(Strings.Get("hud.births")).Append(" +").Append(info.Births)
            .Append("   ").Append(Strings.Get("hud.deaths")).Append(" -").Append(info.Deaths);
        font.Draw(batch, _line.Span, new Vector2(x, y), PeopleColor, scale);
        y += step;

        string largest = info.LargestTribe ?? string.Empty;
        _line.Clear()
            .Append(Strings.Get("hud.tribes")).Append(' ').Append(info.Tribes)
            .Append("   ").Append(Strings.Get("hud.settlements")).Append(' ').Append(info.Settlements);
        if (largest.Length > 0)
        {
            _line.Append("   ").Append(Strings.Get("hud.largest")).Append(' ').Append(largest);
        }

        font.Draw(batch, _line.Span, new Vector2(x, y), TribesColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.zoom")).Append(' ').Append(info.Zoom, 2).Append(' ').Append(Strings.Get("hud.px"))
            .Append("   ").Append(Strings.Get("hud.visible")).Append(' ').Append((int)info.VisibleTiles);
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear().Append(Strings.Get("hud.cursor")).Append(' ');
        if (info.CursorInside)
        {
            _line.Append(info.CursorX).Append(", ").Append(info.CursorY);
        }
        else
        {
            _line.Append('-');
        }

        _line.Append("   ").Append(Strings.Get("hud.world")).Append(' ')
            .Append(info.WorldWidth).Append('x').Append(info.WorldHeight);
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        if (info.IsBehind)
        {
            font.Draw(batch, Strings.Get("hud.behind").AsSpan(), new Vector2(x, y), WarningColor, scale);
        }
    }

    private static void DrawHint(SpriteBatch batch, PixelFont font, Primitives primitives, int viewportWidth, int viewportHeight)
    {
        const int scale = 2;
        string line1 = Strings.Get("hint.line1");
        string line2 = Strings.Get("hint.line2");
        int width = Math.Max(font.Measure(line1, scale), font.Measure(line2, scale)) + (16 * scale);
        int height = (font.LineHeight * scale * 2) + (10 * scale);
        var panel = new Rectangle((viewportWidth - width) / 2, viewportHeight - height - 16, width, height);
        primitives.FillRect(batch, panel, PanelColor);
        primitives.FrameRect(batch, panel, BorderColor);
        font.Draw(batch, line1, new Vector2(panel.X + (8 * scale), panel.Y + (5 * scale)), TextColor, scale);
        font.Draw(batch, line2, new Vector2(panel.X + (8 * scale), panel.Y + (5 * scale) + (font.LineHeight * scale)), TextColor, scale);
    }

    private static string SpeedLabel(GameSpeed speed) => speed switch
    {
        GameSpeed.Paused => Strings.Get("speed.paused"),
        GameSpeed.X1 => Strings.Get("speed.x1"),
        GameSpeed.X4 => Strings.Get("speed.x4"),
        GameSpeed.X16 => Strings.Get("speed.x16"),
        _ => Strings.Get("speed.x64"),
    };
}
