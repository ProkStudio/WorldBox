using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Time;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Угловой оверлей с fps, временем кадра и состоянием мира.
/// Строки собираются в переиспользуемый буфер: за кадр ноль аллокаций.
/// Каждая строка помечена значком, чтобы глаз находил нужную без чтения.
/// После отрисовки панель запоминает своё место в <see cref="Bounds"/>: по нему игра ставит
/// следующие окна так, чтобы они не налезали друг на друга.
/// </summary>
public sealed class DebugOverlay
{
    private const int PanelWidth = 620;
    private const int IconSpace = 22;

    private static readonly Color PeopleColor = new Color(246, 214, 160);
    private static readonly Color EraColor = new Color(206, 178, 240);

    private readonly TextBuilder _line = new TextBuilder(256);

    public bool Visible { get; set; } = true;

    public bool HintVisible { get; set; } = true;

    public int Scale { get; set; } = 2;

    /// <summary>Куда легла панель в последнем кадре. Пустой прямоугольник, если она скрыта.</summary>
    public Rectangle Bounds { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        in OverlayInfo info,
        int viewportWidth,
        int viewportHeight,
        UiSkin? skin = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(primitives);

        if (Visible)
        {
            DrawPanel(batch, font, primitives, skin, in info);
        }
        else
        {
            Bounds = Rectangle.Empty;
        }

        if (HintVisible)
        {
            DrawHint(batch, font, primitives, skin, viewportWidth, viewportHeight);
        }
    }

    private void DrawPanel(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        UiSkin? skin,
        in OverlayInfo info)
    {
        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;

        // Строка эпохи появляется только когда таблица эпох загружена, поэтому высота считается.
        int lines = 7;
        if (info.EraName != null)
        {
            lines++;
        }

        if (info.IsBehind)
        {
            lines++;
        }

        var panel = new Rectangle(14, 14, PanelWidth, (step * lines) + (11 * scale));
        Bounds = panel;
        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int textX = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        _line.Clear()
            .Append(Strings.Get("hud.fps")).Append(' ').Append((int)Math.Round(info.Fps))
            .Append("   ").Append(Strings.Get("hud.frame")).Append(' ').Append(info.FrameMs, 1).Append(' ').Append(Strings.Get("hud.ms"))
            .Append("   ").Append(Strings.Get("hud.worst")).Append(' ').Append(info.FrameMsWorst, 1);
        DrawLine(batch, font, skin, IconKind.Gear, iconX, textX, y, step, UiPalette.Accent, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.sim")).Append(' ').Append(info.SimMs, 2).Append(' ').Append(Strings.Get("hud.ms"))
            .Append("   ").Append(Strings.Get("hud.ticks")).Append(' ').AppendGrouped(info.Ticks)
            .Append("   ").Append(Strings.Get("hud.speed")).Append(' ').Append(SpeedLabel(info.Speed));
        IconKind clock = info.Speed == GameSpeed.Paused ? IconKind.Pause : IconKind.Play;
        DrawLine(batch, font, skin, clock, iconX, textX, y, step, UiPalette.Text, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.year")).Append(' ').AppendYear(info.Year);
        DrawLine(batch, font, skin, IconKind.Chronicle, iconX, textX, y, step, UiPalette.Text, scale);
        y += step;

        if (info.EraName != null)
        {
            _line.Clear()
                .Append(Strings.Get("hud.era")).Append(' ').Append(info.EraName)
                .Append("   ").Append(Strings.Get("hud.years_per_tick")).Append(' ').Append(info.YearsPerTick, 2);
            DrawLine(batch, font, skin, IconKind.Era, iconX, textX, y, step, EraColor, scale);
            y += step;
        }

        _line.Clear()
            .Append(Strings.Get("hud.people")).Append(' ').AppendGrouped(info.People)
            .Append("   ").Append(Strings.Get("hud.births")).Append(" +").Append(info.Births)
            .Append("   ").Append(Strings.Get("hud.deaths")).Append(" -").Append(info.Deaths);
        DrawLine(batch, font, skin, IconKind.People, iconX, textX, y, step, PeopleColor, scale);
        y += step;

        string largest = info.LargestTribe ?? string.Empty;
        _line.Clear()
            .Append(Strings.Get("hud.tribes")).Append(' ').Append(info.Tribes)
            .Append("   ").Append(Strings.Get("hud.settlements")).Append(' ').Append(info.Settlements);
        if (largest.Length > 0)
        {
            _line.Append("   ").Append(Strings.Get("hud.largest")).Append(' ').Append(largest);
        }

        DrawLine(batch, font, skin, IconKind.Settlement, iconX, textX, y, step, UiPalette.Good, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("hud.zoom")).Append(' ').Append(info.Zoom, 2).Append(' ').Append(Strings.Get("hud.px"))
            .Append("   ").Append(Strings.Get("hud.visible")).Append(' ').Append((int)info.VisibleTiles);
        DrawLine(batch, font, skin, IconKind.MapTerrain, iconX, textX, y, step, UiPalette.Text, scale);
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
        DrawLine(batch, font, skin, IconKind.Inspect, iconX, textX, y, step, UiPalette.TextMuted, scale);
        y += step;

        if (info.IsBehind)
        {
            _line.Clear().Append(Strings.Get("hud.behind"));
            DrawLine(batch, font, skin, IconKind.Warning, iconX, textX, y, step, UiPalette.Bad, scale);
        }
    }

    /// <summary>Рисует собранную строку и её значок. Значок рисуется только если есть скин.</summary>
    private void DrawLine(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        IconKind icon,
        int iconX,
        int textX,
        int y,
        int lineHeight,
        Color color,
        int scale)
    {
        if (skin != null)
        {
            skin.Icon(batch, icon, iconX, y + ((lineHeight - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    /// <summary>Подсказка по клавишам стоит над панелью инструментов, а не под ней.</summary>
    private static void DrawHint(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        UiSkin? skin,
        int viewportWidth,
        int viewportHeight)
    {
        const int scale = 2;
        string line1 = Strings.Get("hint.line1");
        string line2 = Strings.Get("hint.line2");
        int width = Math.Max(font.Measure(line1, scale), font.Measure(line2, scale)) + (16 * scale);
        int height = (font.LineHeight * scale * 2) + (10 * scale);
        int bottom = viewportHeight - Toolbar.ReservedHeight - 8;
        var panel = new Rectangle((viewportWidth - width) / 2, bottom - height, width, height);

        UiChrome.Panel(batch, primitives, skin, panel);
        font.Draw(batch, line1, new Vector2(panel.X + (8 * scale), panel.Y + (5 * scale)), UiPalette.Text, scale);
        font.Draw(
            batch,
            line2,
            new Vector2(panel.X + (8 * scale), panel.Y + (5 * scale) + (font.LineHeight * scale)),
            UiPalette.TextMuted,
            scale);
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
