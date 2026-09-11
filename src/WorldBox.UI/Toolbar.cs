using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>Что просит сделать нажатая кнопка панели.</summary>
public enum ToolbarAction : byte
{
    None = 0,
    TogglePause,
    Speed1,
    Speed2,
    Speed3,
    Speed4,
    MapTerrain,
    MapHeight,
    MapTemperature,
    MapMoisture,
    MapFertility,
    MapResources,
    ToggleTribes,
    ToggleMarket,
    ToggleWar,
    ToggleLegend,
    ToggleBorders,
    ToggleInspector,
    NewWorld,
    FitWorld,
}

/// <summary>Состояние игры для подсветки активных кнопок. Структура, чтобы кадр не сорил в память.</summary>
public readonly struct ToolbarState
{
    public ToolbarState(
        bool paused,
        int speed,
        MapMode mode,
        bool tribes,
        bool market,
        bool war,
        bool legend,
        bool borders,
        bool inspector)
    {
        Paused = paused;
        Speed = speed;
        Mode = mode;
        Tribes = tribes;
        Market = market;
        War = war;
        Legend = legend;
        Borders = borders;
        Inspector = inspector;
    }

    public bool Paused { get; }

    /// <summary>Номер скорости от 1 до 4.</summary>
    public int Speed { get; }

    public MapMode Mode { get; }

    public bool Tribes { get; }

    public bool Market { get; }

    public bool War { get; }

    public bool Legend { get; }

    public bool Borders { get; }

    public bool Inspector { get; }
}

/// <summary>
/// Нижняя панель инструментов, как в играх-песочницах: группа времени, группа режимов
/// карты, группа панелей и группа действий с миром. Кнопки — значки из <see cref="IconArt"/>,
/// подсказка с горячей клавишей всплывает над кнопкой под мышкой.
/// Клавиатура продолжает работать: панель — второй способ, а не единственный.
/// </summary>
public sealed class Toolbar
{
    public const int ButtonSize = 42;
    public const int Padding = 8;
    public const int Gap = 5;
    public const int GroupGap = 16;
    public const int BottomMargin = 12;

    private static readonly ButtonDef[] Definitions =
    {
        new ButtonDef(IconKind.Pause, ToolbarAction.TogglePause, "tool.pause", "Space", 0),
        new ButtonDef(IconKind.Speed1, ToolbarAction.Speed1, "tool.speed1", "1", 0),
        new ButtonDef(IconKind.Speed2, ToolbarAction.Speed2, "tool.speed2", "2", 0),
        new ButtonDef(IconKind.Speed3, ToolbarAction.Speed3, "tool.speed3", "3", 0),
        new ButtonDef(IconKind.Speed4, ToolbarAction.Speed4, "tool.speed4", "4", 0),

        new ButtonDef(IconKind.MapTerrain, ToolbarAction.MapTerrain, "map.terrain", "M", 1),
        new ButtonDef(IconKind.MapHeight, ToolbarAction.MapHeight, "map.height", "M", 1),
        new ButtonDef(IconKind.MapTemperature, ToolbarAction.MapTemperature, "map.temperature", "M", 1),
        new ButtonDef(IconKind.MapMoisture, ToolbarAction.MapMoisture, "map.moisture", "M", 1),
        new ButtonDef(IconKind.MapFertility, ToolbarAction.MapFertility, "map.fertility", "M", 1),
        new ButtonDef(IconKind.MapResources, ToolbarAction.MapResources, "map.resources", "M", 1),

        new ButtonDef(IconKind.Tribes, ToolbarAction.ToggleTribes, "tool.tribes", "E", 2),
        new ButtonDef(IconKind.Market, ToolbarAction.ToggleMarket, "tool.market", "G", 2),
        new ButtonDef(IconKind.War, ToolbarAction.ToggleWar, "tool.war", "V", 2),
        new ButtonDef(IconKind.Legend, ToolbarAction.ToggleLegend, "tool.legend", "L", 2),
        new ButtonDef(IconKind.Borders, ToolbarAction.ToggleBorders, "tool.borders", "T", 2),
        new ButtonDef(IconKind.Inspect, ToolbarAction.ToggleInspector, "tool.inspector", "F2", 2),

        new ButtonDef(IconKind.NewWorld, ToolbarAction.NewWorld, "tool.newworld", "N", 3),
        new ButtonDef(IconKind.FitWorld, ToolbarAction.FitWorld, "tool.fitworld", "Home", 3),
    };

    private readonly Rectangle[] _rects = new Rectangle[Definitions.Length];

    private Rectangle _bar;
    private int _layoutWidth = -1;
    private int _layoutHeight = -1;
    private int _hover = -1;

    public bool Visible { get; set; } = true;

    /// <summary>Высота панели с отступом: нужна другим панелям, чтобы не лезть под неё.</summary>
    public static int ReservedHeight => ButtonSize + (Padding * 2) + BottomMargin;

    /// <summary>Пересчитывает геометрию кнопок. Дешёво, но делается только при смене размера окна.</summary>
    public void Layout(int viewportWidth, int viewportHeight)
    {
        if (_layoutWidth == viewportWidth && _layoutHeight == viewportHeight)
        {
            return;
        }

        _layoutWidth = viewportWidth;
        _layoutHeight = viewportHeight;

        int width = 0;
        for (int i = 0; i < Definitions.Length; i++)
        {
            width += ButtonSize;
            if (i + 1 < Definitions.Length)
            {
                width += Definitions[i + 1].Group != Definitions[i].Group ? GroupGap : Gap;
            }
        }

        int barWidth = width + (Padding * 2);
        int barHeight = ButtonSize + (Padding * 2);
        int barX = (viewportWidth - barWidth) / 2;
        int barY = viewportHeight - barHeight - BottomMargin;
        if (barX < 4)
        {
            barX = 4;
        }

        _bar = new Rectangle(barX, barY, barWidth, barHeight);

        int x = barX + Padding;
        int y = barY + Padding;
        for (int i = 0; i < Definitions.Length; i++)
        {
            _rects[i] = new Rectangle(x, y, ButtonSize, ButtonSize);
            x += ButtonSize;
            if (i + 1 < Definitions.Length)
            {
                x += Definitions[i + 1].Group != Definitions[i].Group ? GroupGap : Gap;
            }
        }
    }

    /// <summary>Запоминает, над какой кнопкой мышка. Вызывать каждый кадр до отрисовки.</summary>
    public void Update(int mouseX, int mouseY)
    {
        _hover = -1;
        if (!Visible)
        {
            return;
        }

        for (int i = 0; i < _rects.Length; i++)
        {
            if (_rects[i].Contains(mouseX, mouseY))
            {
                _hover = i;
                return;
            }
        }
    }

    /// <summary>Попадает ли точка в панель. Если да, клик не должен уходить в карту.</summary>
    public bool Contains(int x, int y) => Visible && _bar.Contains(x, y);

    /// <summary>Какое действие заказали кликом.</summary>
    public ToolbarAction HitTest(int x, int y)
    {
        if (!Visible)
        {
            return ToolbarAction.None;
        }

        for (int i = 0; i < _rects.Length; i++)
        {
            if (_rects[i].Contains(x, y))
            {
                return Definitions[i].Action;
            }
        }

        return ToolbarAction.None;
    }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        UiSkin skin,
        ToolbarState state,
        int viewportWidth,
        int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(skin);

        if (!Visible)
        {
            return;
        }

        Layout(viewportWidth, viewportHeight);
        skin.Panel(batch, _bar);

        int iconSize = IconAtlas.IconSize * 2;
        int inset = (ButtonSize - iconSize) / 2;

        for (int i = 0; i < Definitions.Length; i++)
        {
            ButtonDef def = Definitions[i];
            Rectangle rect = _rects[i];
            bool active = IsActive(def.Action, state);
            bool hovered = _hover == i;

            skin.Button(batch, rect, active, hovered);

            IconKind icon = def.Icon;
            if (def.Action == ToolbarAction.TogglePause)
            {
                icon = state.Paused ? IconKind.Play : IconKind.Pause;
            }

            var iconRect = new Rectangle(rect.X + inset, rect.Y + inset, iconSize, iconSize);
            skin.Icon(batch, icon, iconRect, hovered || active ? Color.White : new Color(226, 230, 238));

            // Разделитель между группами рисуем после последней кнопки группы.
            if (i + 1 < Definitions.Length && Definitions[i + 1].Group != def.Group)
            {
                skin.Separator(batch, rect.Right + (GroupGap / 2) - 1, rect.Y + 4, ButtonSize - 8);
            }
        }

        DrawTooltip(batch, font, skin);
    }

    private void DrawTooltip(SpriteBatch batch, PixelFont font, UiSkin skin)
    {
        if (_hover < 0)
        {
            return;
        }

        ButtonDef def = Definitions[_hover];
        string title = Strings.Get(def.TooltipKey);
        string hotkey = def.Hotkey;

        const int scale = 2;
        int titleWidth = font.Measure(title, scale);
        int hotkeyWidth = font.Measure(hotkey, 1);
        int textWidth = Math.Max(titleWidth, hotkeyWidth);
        int height = (font.GlyphHeight * scale) + font.GlyphHeight + 16;
        int width = textWidth + 20;

        Rectangle button = _rects[_hover];
        int x = button.Center.X - (width / 2);
        int y = button.Y - height - 8;
        if (x < 6)
        {
            x = 6;
        }

        if (x + width > _layoutWidth - 6)
        {
            x = _layoutWidth - 6 - width;
        }

        var rect = new Rectangle(x, y, width, height);
        skin.Plate(batch, rect, UiPalette.Accent);

        font.Draw(batch, title, new Vector2(x + 10, y + 7), UiPalette.Text, scale);
        font.Draw(
            batch,
            hotkey,
            new Vector2(x + 10, y + 9 + (font.GlyphHeight * scale)),
            UiPalette.TextMuted,
            1);
    }

    private static bool IsActive(ToolbarAction action, ToolbarState state)
    {
        return action switch
        {
            ToolbarAction.TogglePause => state.Paused,
            ToolbarAction.Speed1 => !state.Paused && state.Speed == 1,
            ToolbarAction.Speed2 => !state.Paused && state.Speed == 2,
            ToolbarAction.Speed3 => !state.Paused && state.Speed == 3,
            ToolbarAction.Speed4 => !state.Paused && state.Speed == 4,
            ToolbarAction.MapTerrain => state.Mode == MapMode.Terrain,
            ToolbarAction.MapHeight => state.Mode == MapMode.Height,
            ToolbarAction.MapTemperature => state.Mode == MapMode.Temperature,
            ToolbarAction.MapMoisture => state.Mode == MapMode.Moisture,
            ToolbarAction.MapFertility => state.Mode == MapMode.Fertility,
            ToolbarAction.MapResources => state.Mode == MapMode.Resources,
            ToolbarAction.ToggleTribes => state.Tribes,
            ToolbarAction.ToggleMarket => state.Market,
            ToolbarAction.ToggleWar => state.War,
            ToolbarAction.ToggleLegend => state.Legend,
            ToolbarAction.ToggleBorders => state.Borders,
            ToolbarAction.ToggleInspector => state.Inspector,
            _ => false,
        };
    }

    private readonly struct ButtonDef
    {
        public ButtonDef(IconKind icon, ToolbarAction action, string tooltipKey, string hotkey, int group)
        {
            Icon = icon;
            Action = action;
            TooltipKey = tooltipKey;
            Hotkey = hotkey;
            Group = group;
        }

        public IconKind Icon { get; }

        public ToolbarAction Action { get; }

        public string TooltipKey { get; }

        public string Hotkey { get; }

        public int Group { get; }
    }
}
