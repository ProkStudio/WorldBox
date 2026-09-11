using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Панель войны: кто с кем воюет, сколько у сторон войск, насколько народы устали
/// и что война уже сделала с миром — сколько было битв, взятых городов и бунтов.
///
/// Без этого окна война — это молчаливые точки на карте: игрок видит, что город сменил
/// цвет, но не понимает, кто его взял и почему сосед вдруг перестал воевать.
///
/// Войны отбираются вставкой в заранее выделенные массивы, строки собираются в общий
/// буфер: за кадр ноль аллокаций.
/// </summary>
public sealed class WarPanel
{
    /// <summary>Сколько войн помещается в панель. Остальные сворачиваются в счётчик заголовка.</summary>
    public const int MaxRows = 5;

    private const int PanelWidth = 620;
    private const int IconSpace = 22;

    /// <summary>С какой усталости сторона уже ищет мира: строка меняет цвет и значок.</summary>
    private const float TiredShare = 0.5f;

    private static readonly Color TotalsColor = new Color(232, 168, 96);
    private static readonly Color TiredColor = new Color(206, 196, 164);

    private readonly TextBuilder _line = new TextBuilder(256);
    private readonly int[] _left = new int[MaxRows];
    private readonly int[] _right = new int[MaxRows];
    private readonly int[] _weight = new int[MaxRows];

    /// <summary>Отряды по народам: считаются одним проходом за кадр, а не заново на каждую строку.</summary>
    private int[] _armyCount = new int[TribeStore.DefaultCapacity];

    /// <summary>Бойцы по народам. Массив общий с числом отрядов и растёт вместе с ним.</summary>
    private int[] _armyMen = new int[TribeStore.DefaultCapacity];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

    /// <summary>Отступ сверху: игра опускает панель под статистику и окно торговли.</summary>
    public int TopMargin { get; set; } = 16;

    /// <summary>Куда легла панель в последнем кадре. Пустой прямоугольник, если она скрыта.</summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>Сколько войн показано в последнем кадре.</summary>
    public int DrawnRows { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        TribeStore tribes,
        ArmyStore? armies,
        Diplomacy? diplomacy,
        SettlementStore settlements,
        WarSystem? war,
        float yearsPerTick,
        int viewportHeight,
        UiSkin? skin = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);

        DrawnRows = 0;
        if (!Visible)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;

        if (armies == null || diplomacy == null || war == null)
        {
            DrawEmpty(batch, font, primitives, skin, step, scale);
            return;
        }

        CountArmies(tribes, armies);
        int sieges = CountSieges(settlements);
        int rows = Collect(tribes, diplomacy);

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
            skin.Icon(batch, IconKind.War, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        _line.Clear()
            .Append(Strings.Get("panel.war"))
            .Append("   ").Append(Strings.Get("panel.wars")).Append(' ').Append(war.ActiveWars)
            .Append("   ").Append(Strings.Get("panel.armies")).Append(' ').Append(armies.Count)
            .Append("   ").Append(Strings.Get("panel.sieges")).Append(' ').Append(sieges);
        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Accent, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_wars"), new Vector2(x, y), UiPalette.Good, scale);
            return;
        }

        int bottom = panel.Bottom - (4 * scale);

        for (int i = 0; i < rows; i++)
        {
            if (y + (step * 2) > bottom)
            {
                break;
            }

            int a = _left[i];
            int b = _right[i];

            // Полоска на две строки: сразу видно, где кончается одна война и начинается другая.
            if ((i & 1) == 0)
            {
                var stripe = new Rectangle(panel.X + (3 * scale), y, PanelWidth - (6 * scale), step * 2);
                primitives.FillRect(batch, stripe, UiPalette.PanelLight * 0.45f);
            }

            // Два квадратика: те же цвета, что у владений на карте и у знамён отрядов.
            int side = step - (3 * scale);
            var swatchA = new Rectangle(iconX, y + scale, side, side);
            var swatchB = new Rectangle(iconX + side + (2 * scale), y + scale, side, side);
            primitives.FillRect(batch, swatchA, TribePalette.Of(tribes.ColorIndex[a]));
            primitives.FrameRect(batch, swatchA, UiPalette.Border, 1);
            primitives.FillRect(batch, swatchB, TribePalette.Of(tribes.ColorIndex[b]));
            primitives.FrameRect(batch, swatchB, UiPalette.Border, 1);

            int textX = iconX + (side * 2) + (6 * scale);

            // Длину войны показываем в игровых годах: прогоны и тики владельцу ни о чём не говорят.
            long runs = war.Run - diplomacy.SinceRun(a, b);
            if (runs < 0)
            {
                runs = 0;
            }

            _line.Clear()
                .Append(tribes.Name[a] ?? string.Empty)
                .Append("  ").Append(Strings.Get("panel.versus")).Append("  ")
                .Append(tribes.Name[b] ?? string.Empty)
                .Append("   ").Append(Strings.Get("panel.war_years")).Append(' ')
                .AppendGrouped((long)MathF.Round(runs * WarSystem.TicksPerRun * yearsPerTick));
            font.Draw(batch, _line.Span, new Vector2(textX, y), UiPalette.Text, scale);
            y += step;

            DrawSides(batch, font, skin, diplomacy, a, b, textX, y, step, scale);
            y += step;
            DrawnRows++;
        }

        if (y + step <= bottom)
        {
            DrawTotals(batch, font, skin, war, iconX, y, step, scale);
        }
    }

    /// <summary>Вторая строка войны: у кого сколько войск и насколько стороны устали воевать.</summary>
    private void DrawSides(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        Diplomacy diplomacy,
        int a,
        int b,
        int x,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.armies")).Append(' ')
            .Append(_armyCount[a]).Append(" : ").Append(_armyCount[b])
            .Append("   ").Append(Strings.Get("panel.soldiers")).Append(' ')
            .AppendGrouped(_armyMen[a]).Append(" : ").AppendGrouped(_armyMen[b])
            .Append("   ").Append(Strings.Get("panel.exhaustion")).Append(' ')
            .Append((int)MathF.Round(diplomacy.Exhaustion[a] * 100f)).Append("% : ")
            .Append((int)MathF.Round(diplomacy.Exhaustion[b] * 100f)).Append('%');

        // Уставшая сторона скоро попросит мира — это главное в строке, поэтому меняется цвет.
        float tired = MathF.Max(diplomacy.Exhaustion[a], diplomacy.Exhaustion[b]);
        Color color = tired >= TiredShare ? TiredColor : UiPalette.TextMuted;
        IconKind icon = tired >= TiredShare ? IconKind.Warning : IconKind.War;

        int textX = x;
        if (skin != null)
        {
            skin.Icon(batch, icon, x, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    /// <summary>Итоговая строка: что война сделала с миром за всю партию.</summary>
    private void DrawTotals(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        WarSystem war,
        int iconX,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.battles")).Append(' ').AppendGrouped(war.TotalBattles)
            .Append("   ").Append(Strings.Get("panel.captures")).Append(' ').AppendGrouped(war.TotalCaptures)
            .Append("   ").Append(Strings.Get("panel.revolts")).Append(' ').AppendGrouped(war.TotalRevolts)
            .Append("   ").Append(Strings.Get("panel.war_deaths")).Append(' ').AppendGrouped(war.TotalDeaths);

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Chronicle, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), TotalsColor, scale);
    }

    /// <summary>Окно-заглушка, когда data/war.json не прочитался и войны в партии нет вовсе.</summary>
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
            skin.Icon(batch, IconKind.War, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, Strings.Get("panel.war"), new Vector2(x, y), UiPalette.Accent, scale);
        y += step;
        font.Draw(batch, Strings.Get("panel.no_war_table"), new Vector2(x, y), UiPalette.Bad, scale);
    }

    /// <summary>Считает отряды и бойцов по народам. Массивы пересоздаются только при смене ёмкости.</summary>
    private void CountArmies(TribeStore tribes, ArmyStore armies)
    {
        if (_armyCount.Length < tribes.Capacity)
        {
            _armyCount = new int[tribes.Capacity];
            _armyMen = new int[tribes.Capacity];
        }

        Array.Clear(_armyCount, 0, _armyCount.Length);
        Array.Clear(_armyMen, 0, _armyMen.Length);

        for (int i = 0; i < armies.HighWater; i++)
        {
            if (!armies.Alive[i])
            {
                continue;
            }

            int tribe = armies.Tribe[i];
            if ((uint)tribe >= (uint)_armyCount.Length)
            {
                continue;
            }

            _armyCount[tribe]++;
            _armyMen[tribe] += armies.Men[i];
        }
    }

    /// <summary>Сколько городов сейчас в осаде. Один проход по списку поселений за кадр.</summary>
    private static int CountSieges(SettlementStore settlements)
    {
        int count = 0;
        for (int i = 0; i < settlements.HighWater; i++)
        {
            if (settlements.Alive[i] && settlements.Siege[i] > 0f)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Отбирает самые крупные войны вставкой в готовые массивы: пары перебираются один раз.</summary>
    private int Collect(TribeStore tribes, Diplomacy diplomacy)
    {
        int count = 0;
        int limit = Math.Min(tribes.Capacity, diplomacy.Capacity);

        for (int a = 1; a < limit; a++)
        {
            // У народа без войн внутренний цикл даже не начинается: это самый частый случай.
            if (!tribes.Alive[a] || diplomacy.Wars[a] == 0)
            {
                continue;
            }

            for (int b = a + 1; b < limit; b++)
            {
                if (!tribes.Alive[b] || !diplomacy.IsAtWar(a, b))
                {
                    continue;
                }

                int weight = _armyMen[a] + _armyMen[b];
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
                    _left[i] = _left[i - 1];
                    _right[i] = _right[i - 1];
                    _weight[i] = _weight[i - 1];
                }

                _left[pos] = a;
                _right[pos] = b;
                _weight[pos] = weight;
                if (count < MaxRows)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
