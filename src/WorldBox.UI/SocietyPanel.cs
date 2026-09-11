using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Society;
using WorldBox.Core.Tribes;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Окно общества: во что верит двор, какой народ в государстве главный,
/// какая сейчас власть и насколько крепко она держится.
///
/// Без этого окна слой общества — просто цветные пятна: игрок видит, что город
/// сменил веру, но не знает, как она называется и почему держава вдруг зашаталась.
///
/// Строки собираются в общий буфер, народы отбираются вставкой в готовые массивы:
/// за кадр ноль аллокаций.
/// </summary>
public sealed class SocietyPanel
{
    /// <summary>Сколько народов показываем. Остальные видны в заголовке счётчиками.</summary>
    public const int MaxRows = 5;

    private const int PanelWidth = 620;
    private const int IconSpace = 22;

    /// <summary>Ниже этого держава уже трещит: строка меняет цвет и значок.</summary>
    private const float ShakyShare = 0.35f;

    private static readonly Color TotalsColor = new Color(232, 168, 96);
    private static readonly Color ShakyColor = new Color(206, 196, 164);

    private readonly TextBuilder _line = new TextBuilder(256);
    private readonly int[] _rows = new int[MaxRows];
    private readonly int[] _weight = new int[MaxRows];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

    /// <summary>Отступ сверху: игра опускает окно под остальные панели.</summary>
    public int TopMargin { get; set; } = 16;

    /// <summary>Куда легло окно в последнем кадре. Пусто, если скрыто.</summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>Сколько народов показано в последнем кадре.</summary>
    public int DrawnRows { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        TribeStore tribes,
        SocietySystem? society,
        SocietyState? state,
        ReligionStore? religions,
        CultureStore? cultures,
        int viewportHeight,
        UiSkin? skin = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(tribes);

        DrawnRows = 0;
        if (!Visible)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;

        if (society == null || state == null || religions == null || cultures == null)
        {
            DrawEmpty(batch, font, primitives, skin, step, scale);
            return;
        }

        int rows = Collect(tribes);
        int textLines = rows > 0 ? 2 + (rows * 2) : 2;
        int height = (step * textLines) + (11 * scale);

        // Окно не должно залезть на панель инструментов: лишние строки просто не рисуются.
        int room = viewportHeight - TopMargin - Toolbar.ReservedHeight - 12;
        if (room > step * 2 && height > room)
        {
            height = room;
        }

        var panel = new Rectangle(14, TopMargin, PanelWidth, height);
        Bounds = panel;
        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        if (skin != null)
        {
            skin.Icon(batch, IconKind.Tribes, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        _line.Clear()
            .Append(Strings.Get("panel.society"))
            .Append("   ").Append(Strings.Get("panel.faiths")).Append(' ').Append(society.ReligionCount)
            .Append("   ").Append(Strings.Get("panel.cultures")).Append(' ').Append(society.CultureCount)
            .Append("   ").Append(Strings.Get("panel.schisms")).Append(' ').Append(society.SchismCount)
            .Append("   ").Append(Strings.Get("hud.stability")).Append(' ')
            .Append(Percent(society.AverageStability)).Append('%');
        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Accent, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_society"), new Vector2(x, y), UiPalette.TextMuted, scale);
            return;
        }

        int bottom = panel.Bottom - (4 * scale);

        for (int i = 0; i < rows; i++)
        {
            if (y + (step * 2) > bottom)
            {
                break;
            }

            int tribe = _rows[i];

            // Полоска на две строки: сразу видно, где кончается один народ и начинается другой.
            if ((i & 1) == 0)
            {
                var stripe = new Rectangle(panel.X + (3 * scale), y, PanelWidth - (6 * scale), step * 2);
                primitives.FillRect(batch, stripe, UiPalette.PanelLight * 0.45f);
            }

            int side = step - (3 * scale);
            var swatch = new Rectangle(iconX, y + scale, side, side);
            primitives.FillRect(batch, swatch, TribePalette.Of(tribes.ColorIndex[tribe]));
            primitives.FrameRect(batch, swatch, UiPalette.Border, 1);

            int textX = iconX + side + (6 * scale);

            string faith = religions.NameOf(state.Religion[tribe]);
            string culture = cultures.NameOf(state.Culture[tribe]);

            _line.Clear()
                .Append(tribes.Name[tribe] ?? string.Empty)
                .Append("   ").Append(Strings.Get("panel.religion")).Append(' ')
                .Append(faith.Length > 0 ? faith : "-")
                .Append("   ").Append(Strings.Get("panel.culture")).Append(' ')
                .Append(culture.Length > 0 ? culture : "-");
            font.Draw(batch, _line.Span, new Vector2(textX, y), UiPalette.Text, scale);
            y += step;

            DrawPower(batch, font, skin, society, state, tribe, textX, y, step, scale);
            y += step;
            DrawnRows++;
        }

        if (y + step <= bottom)
        {
            DrawTotals(batch, font, skin, society, iconX, y, step, scale);
        }
    }

    /// <summary>Вторая строка народа: власть, согласие веры, единство культуры и стабильность.</summary>
    private void DrawPower(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        SocietySystem society,
        SocietyState state,
        int tribe,
        int x,
        int y,
        int step,
        int scale)
    {
        float stability = state.Stability[tribe];

        _line.Clear()
            .Append(Strings.Get("panel.ideology")).Append(' ')
            .Append(Strings.Get(society.IdeologyNameKeyOf(tribe)))
            .Append("   ").Append(Strings.Get("panel.faith")).Append(' ')
            .Append(Percent(state.Faith[tribe])).Append('%')
            .Append("   ").Append(Strings.Get("panel.unity")).Append(' ')
            .Append(Percent(state.Unity[tribe])).Append('%')
            .Append("   ").Append(Strings.Get("panel.stability")).Append(' ')
            .Append(Percent(stability)).Append('%');

        bool shaky = stability < ShakyShare;
        Color color = shaky ? ShakyColor : UiPalette.TextMuted;

        int textX = x;
        if (skin != null)
        {
            skin.Icon(batch, shaky ? IconKind.Warning : IconKind.Star, x, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    /// <summary>Итоговая строка: что общество успело сделать с миром за всю партию.</summary>
    private void DrawTotals(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        SocietySystem society,
        int iconX,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.conversions")).Append(' ').AppendGrouped(society.TotalConversions)
            .Append("   ").Append(Strings.Get("panel.schisms")).Append(' ').AppendGrouped(society.TotalSchisms)
            .Append("   ").Append(Strings.Get("panel.collapses")).Append(' ').AppendGrouped(society.TotalCollapses);

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Chronicle, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), TotalsColor, scale);
    }

    /// <summary>Окно-заглушка, когда data/society.json не прочитался и общества в партии нет.</summary>
    private void DrawEmpty(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        UiSkin? skin,
        int step,
        int scale)
    {
        int height = (step * 2) + (11 * scale);
        var panel = new Rectangle(14, TopMargin, PanelWidth, height);
        Bounds = panel;
        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        if (skin != null)
        {
            skin.Icon(batch, IconKind.Tribes, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, Strings.Get("panel.society"), new Vector2(x, y), UiPalette.Accent, scale);
        y += step;
        font.Draw(batch, Strings.Get("panel.no_society_table"), new Vector2(x, y), UiPalette.Bad, scale);
    }

    /// <summary>Отбирает самые людные народы вставкой в готовые массивы.</summary>
    private int Collect(TribeStore tribes)
    {
        int count = 0;

        for (int tribe = 1; tribe < tribes.Capacity; tribe++)
        {
            if (!tribes.Alive[tribe])
            {
                continue;
            }

            int weight = tribes.People[tribe];
            int pos = count;
            while (pos > 0 && _weight[pos - 1] < weight)
            {
                pos--;
            }

            if (pos >= MaxRows)
            {
                continue;
            }

            int last = Math.Min(count, MaxRows - 1);
            for (int i = last; i > pos; i--)
            {
                _rows[i] = _rows[i - 1];
                _weight[i] = _weight[i - 1];
            }

            _rows[pos] = tribe;
            _weight[pos] = weight;
            if (count < MaxRows)
            {
                count++;
            }
        }

        return count;
    }

    private static int Percent(float share) => (int)MathF.Round(share * 100f);
}
