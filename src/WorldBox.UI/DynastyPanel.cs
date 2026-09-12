using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Rulers;
using WorldBox.Core.Tribes;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Окно династии: кто сейчас на престоле, какого он дома, сколько ему лет,
/// чем он известен и кого двор считает наследником.
///
/// Без этого окна правители остаются числами в памяти: игрок видит бунт в городах,
/// но не знает, что его устроил Жестокий из дома Хазаров, а до него три поколения было тихо.
///
/// Отдельные строки показывают род самого людного народа и его историю правлений.
/// Строки собираются в общий буфер, народы и история отбираются вставкой в готовые массивы:
/// за кадр ноль аллокаций.
/// </summary>
public sealed class DynastyPanel
{
    /// <summary>Сколько народов показываем.</summary>
    public const int MaxRows = 4;

    /// <summary>Сколько поколений рода умещается в строке.</summary>
    public const int LineDepth = 6;

    /// <summary>Сколько последних правителей показываем в строке истории.</summary>
    public const int HistoryRows = 5;

    private const int PanelWidth = 660;
    private const int IconSpace = 22;

    private static readonly Color TotalsColor = new Color(232, 168, 96);
    private static readonly Color LineColor = new Color(180, 196, 220);

    private readonly TextBuilder _line = new TextBuilder(320);
    private readonly int[] _rows = new int[MaxRows];
    private readonly int[] _weight = new int[MaxRows];
    private readonly int[] _history = new int[HistoryRows];
    private readonly long[] _historyTick = new long[HistoryRows];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

    /// <summary>Отступ сверху: игра опускает окно под остальные панели.</summary>
    public int TopMargin { get; set; } = 16;

    /// <summary>Куда легло окно в последнем кадре. Пусто, если скрыто.</summary>
    public Rectangle Bounds { get; private set; }

    public int DrawnRows { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        TribeStore tribes,
        RulerSystem? rulers,
        RulerStore? people,
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

        if (rulers == null || people == null)
        {
            DrawEmpty(batch, font, primitives, skin, step, scale);
            return;
        }

        int rows = Collect(tribes);
        int textLines = rows > 0 ? 4 + (rows * 2) : 2;
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
            skin.Icon(batch, IconKind.Star, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        _line.Clear()
            .Append(Strings.Get("panel.dynasty"))
            .Append("   ").Append(Strings.Get("panel.reigns")).Append(' ').AppendGrouped(rulers.TotalReigns)
            .Append("   ").Append(Strings.Get("panel.crises")).Append(' ').AppendGrouped(rulers.TotalCrises)
            .Append("   ").Append(Strings.Get("panel.coups")).Append(' ').AppendGrouped(rulers.TotalCoups)
            .Append("   ").Append(Strings.Get("panel.heroes")).Append(' ').Append(rulers.HeroCount);
        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Accent, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_dynasty"), new Vector2(x, y), UiPalette.TextMuted, scale);
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

            // Полоска на две строки: сразу видно, где кончается один двор и начинается другой.
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

            DrawRuler(batch, font, rulers, tribes, tribe, textX, y, scale);
            y += step;
            DrawCourt(batch, font, skin, rulers, people, tribe, textX, y, step, scale);
            y += step;
            DrawnRows++;
        }

        int top = _rows[0];

        if (y + step <= bottom)
        {
            DrawLineage(batch, font, skin, rulers, people, tribes, top, iconX, y, step, scale);
            y += step;
        }

        if (y + step <= bottom)
        {
            DrawHistory(batch, font, skin, people, tribes, top, iconX, y, step, scale);
            y += step;
        }

        if (y + step <= bottom)
        {
            DrawTotals(batch, font, skin, rulers, iconX, y, step, scale);
        }
    }

    /// <summary>Первая строка народа: имя с прозвищем, дом, поколение и возраст.</summary>
    private void DrawRuler(
        SpriteBatch batch,
        PixelFont font,
        RulerSystem rulers,
        TribeStore tribes,
        int tribe,
        int x,
        int y,
        int scale)
    {
        string name = rulers.RulerNameOf(tribe);
        string epithet = rulers.EpithetOf(tribe);
        string house = rulers.HouseOf(tribe);

        _line.Clear().Append(tribes.Name[tribe] ?? string.Empty).Append("   ");

        if (name.Length == 0)
        {
            _line.Append(Strings.Get("panel.no_ruler"));
            font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.TextMuted, scale);
            return;
        }

        _line.Append(name);
        if (epithet.Length > 0)
        {
            _line.Append(' ').Append(epithet);
        }

        _line.Append("   ").Append(Strings.Get("panel.house")).Append(' ')
            .Append(house.Length > 0 ? house : "-")
            .Append("   ").Append(Strings.Get("panel.generation")).Append(' ').Append(rulers.GenerationOf(tribe))
            .Append("   ").Append(Strings.Get("panel.age")).Append(' ')
            .Append((int)MathF.Round(rulers.AgeOf(tribe)));

        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Text, scale);
    }

    /// <summary>Вторая строка народа: черты, наследник, кризисы и герои.</summary>
    private void DrawCourt(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        RulerSystem rulers,
        RulerStore people,
        int tribe,
        int x,
        int y,
        int step,
        int scale)
    {
        int heir = rulers.HeirOf(tribe);
        int crises = rulers.CrisesOf(tribe);

        _line.Clear().Append(Strings.Get("panel.traits")).Append(' ');
        AppendTraits(rulers.TraitsOf(tribe));

        _line.Append("   ").Append(Strings.Get("panel.heir")).Append(' ')
            .Append(heir >= 0 ? people.NameOf(heir) : "-")
            .Append("   ").Append(Strings.Get("panel.crises")).Append(' ').Append(crises)
            .Append("   ").Append(Strings.Get("panel.heroes")).Append(' ').Append(rulers.HeroesOf(tribe));

        bool shaky = heir < 0 || crises > 0;
        int textX = x;
        if (skin != null)
        {
            skin.Icon(batch, shaky ? IconKind.Warning : IconKind.People, x, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), UiPalette.TextMuted, scale);
    }

    /// <summary>Строка рода: нынешний правитель и его предки вглубь на несколько поколений.</summary>
    private void DrawLineage(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        RulerSystem rulers,
        RulerStore people,
        TribeStore tribes,
        int tribe,
        int iconX,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.lineage")).Append(' ')
            .Append(tribes.Name[tribe] ?? string.Empty).Append("   ");

        int walk = rulers.RulerOf(tribe);
        int shown = 0;

        while (walk >= 0 && shown < LineDepth)
        {
            if (shown > 0)
            {
                _line.Append(" < ");
            }

            _line.Append(people.NameOf(walk));
            walk = people.ParentOf(walk);
            shown++;
        }

        if (shown == 0)
        {
            _line.Append('-');
        }

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Tribes, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), LineColor, scale);
    }

    /// <summary>Строка истории: последние правители народа с годами смерти.</summary>
    private void DrawHistory(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        RulerStore people,
        TribeStore tribes,
        int tribe,
        int iconX,
        int y,
        int step,
        int scale)
    {
        int found = CollectHistory(people, tribe);

        _line.Clear()
            .Append(Strings.Get("panel.ruler_history")).Append(' ')
            .Append(tribes.Name[tribe] ?? string.Empty).Append("   ");

        if (found == 0)
        {
            _line.Append('-');
        }

        for (int i = 0; i < found; i++)
        {
            if (i > 0)
            {
                _line.Append(", ");
            }

            int index = _history[i];
            _line.Append(people.Name[index] ?? string.Empty);
            if (people.Dead[index])
            {
                _line.Append(" (").AppendYear(people.DiedYear[index]).Append(')');
            }
        }

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Chronicle, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), UiPalette.TextMuted, scale);
    }

    /// <summary>Итоги: что дворы успели нажить за всю партию.</summary>
    private void DrawTotals(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        RulerSystem rulers,
        int iconX,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.deaths")).Append(' ').AppendGrouped(rulers.TotalDeaths)
            .Append("   ").Append(Strings.Get("panel.heirs")).Append(' ').AppendGrouped(rulers.TotalHeirs)
            .Append("   ").Append(Strings.Get("panel.elections")).Append(' ').AppendGrouped(rulers.TotalElections)
            .Append("   ").Append(Strings.Get("panel.depth")).Append(' ').Append(rulers.DeepestGeneration)
            .Append("   ").Append(Strings.Get("panel.age")).Append(' ')
            .Append((int)MathF.Round(rulers.AverageAge));

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Era, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), TotalsColor, scale);
    }

    /// <summary>Окно-заглушка, когда data/rulers.json не прочитался и правителей в партии нет.</summary>
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
            skin.Icon(batch, IconKind.Star, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, Strings.Get("panel.dynasty"), new Vector2(x, y), UiPalette.Accent, scale);
        y += step;
        font.Draw(batch, Strings.Get("panel.no_rulers_table"), new Vector2(x, y), UiPalette.Bad, scale);
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

    /// <summary>
    /// Последние правители народа. Ищем по тику коронации вставкой в готовый массив:
    /// список правителей за всю историю нужен игроку, а сортировать в кадре нечем.
    /// </summary>
    private int CollectHistory(RulerStore people, int tribe)
    {
        int count = 0;

        for (int i = 0; i < people.HighWater; i++)
        {
            if (!people.Used[i] || people.Tribe[i] != tribe || people.Crowned[i] <= 0L)
            {
                continue;
            }

            long crowned = people.Crowned[i];
            int pos = count;
            while (pos > 0 && _historyTick[pos - 1] < crowned)
            {
                pos--;
            }

            if (pos >= HistoryRows)
            {
                continue;
            }

            int last = Math.Min(count, HistoryRows - 1);
            for (int k = last; k > pos; k--)
            {
                _history[k] = _history[k - 1];
                _historyTick[k] = _historyTick[k - 1];
            }

            _history[pos] = i;
            _historyTick[pos] = crowned;
            if (count < HistoryRows)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Черты через запятую. Если черт нет — прочерк, а не пустое место.</summary>
    private void AppendTraits(Trait traits)
    {
        bool any = false;

        for (int i = 0; i < Traits.Count; i++)
        {
            if ((traits & Traits.Of(i)) == Trait.None)
            {
                continue;
            }

            if (any)
            {
                _line.Append(", ");
            }

            _line.Append(Strings.Get(Traits.NameKey(i)));
            any = true;
        }

        if (!any)
        {
            _line.Append('-');
        }
    }
}
