using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Eras;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Панель справа внизу: кто живёт в мире, в какой эпохе и что им мешает шагнуть дальше.
/// Это главное окно в симуляцию: без него игрок видит только цветные пятна и не понимает, почему
/// один народ вторую тысячу лет сидит в каменном веке.
/// Строки собираются в общий буфер, порядок — в заранее выделенный массив: за кадр ноль аллокаций.
/// </summary>
public sealed class TribePanel
{
    /// <summary>Сколько народов помещается в панель. Остальные сворачиваются в счётчик в заголовке.</summary>
    public const int MaxRows = 10;

    private const int PanelWidth = 560;
    private const int IconSpace = 22;

    private static readonly Color EraColor = new Color(206, 178, 240);

    private readonly TextBuilder _line = new TextBuilder(192);
    private readonly int[] _order = new int[MaxRows];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

    /// <summary>Отступ от низа окна. Игра поднимает панель над мини-картой.</summary>
    public int BottomMargin { get; set; } = 12;

    /// <summary>Сколько народов показано в последнем кадре.</summary>
    public int DrawnRows { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        TribeStore tribes,
        TribeTech? tech,
        EraTable? table,
        int viewportWidth,
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
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;

        int alive = 0;
        for (int t = 1; t < tribes.Capacity; t++)
        {
            if (tribes.Alive[t])
            {
                alive++;
            }
        }

        int rows = Collect(tribes);
        int textLines = rows > 0 ? 1 + (rows * 2) : 2;
        int height = (step * textLines) + (11 * scale);
        var panel = new Rectangle(
            viewportWidth - PanelWidth - 14,
            viewportHeight - height - BottomMargin,
            PanelWidth,
            height);

        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        if (skin != null)
        {
            skin.Icon(batch, IconKind.Tribes, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        _line.Clear().Append(Strings.Get("panel.tribes")).Append("   ").Append(alive);
        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Accent, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_tribes"), new Vector2(x, y), UiPalette.TextMuted, scale);
            return;
        }

        for (int i = 0; i < rows; i++)
        {
            int tribe = _order[i];
            Color color = TribePalette.Of(tribes.ColorIndex[tribe]);

            // Полоска на две строки: сразу видно, где заканчивается один народ и начинается другой.
            if ((i & 1) == 0)
            {
                var stripe = new Rectangle(panel.X + (3 * scale), y, PanelWidth - (6 * scale), step * 2);
                primitives.FillRect(batch, stripe, UiPalette.PanelLight * 0.45f);
            }

            // Цветной квадратик — тот же цвет, что у владений на карте и у таблички города.
            var swatch = new Rectangle(iconX, y + scale, step - (2 * scale), step - (3 * scale));
            primitives.FillRect(batch, swatch, color);
            primitives.FrameRect(batch, swatch, UiPalette.Border, 1);

            int textX = iconX + step + (2 * scale);
            _line.Clear().Append(tribes.Name[tribe] ?? string.Empty);
            if (table != null)
            {
                int era = Math.Clamp(tribes.Era[tribe], 0, table.Last);
                _line.Append("   ").Append(Strings.Get(table.NameKeyOf(era)));
            }

            font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
            y += step;

            DrawStatus(batch, font, skin, tribes, tech, table, tribe, textX, y, step, scale);
            y += step;
        }

        DrawnRows = rows;
    }

    /// <summary>Вторая строка народа: люди, поселения и причина остановки.</summary>
    private void DrawStatus(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        TribeStore tribes,
        TribeTech? tech,
        EraTable? table,
        int tribe,
        int x,
        int y,
        int step,
        int scale)
    {
        bool tracked = tech != null && (uint)tribe < (uint)tech.Capacity;
        long headcount = tracked ? tech!.Headcount[tribe] : tribes.People[tribe];

        _line.Clear()
            .Append(Strings.Get("panel.people")).Append(' ').AppendGrouped(headcount)
            .Append("   ").Append(Strings.Get("panel.settlements_short")).Append(' ').Append(tribes.Settlements[tribe])
            .Append("   ");

        Color color = UiPalette.TextMuted;
        IconKind icon = IconKind.People;

        if (table == null)
        {
            _line.Append(Strings.Get("panel.no_eras"));
            DrawStatusLine(batch, font, skin, icon, x, y, step, color, scale);
            return;
        }

        int era = Math.Clamp(tribes.Era[tribe], 0, table.Last);
        int next = era + 1;
        EraBlock block = tracked ? tech!.BlockOf(tribe) : EraBlock.Ready;

        if (next >= table.Count)
        {
            block = EraBlock.Top;
        }

        switch (block)
        {
            case EraBlock.Top:
                _line.Append(Strings.Get("panel.era_top"));
                color = UiPalette.Good;
                icon = IconKind.Star;
                break;

            case EraBlock.DarkAge:
                _line.Append(Strings.Get("panel.dark_age"));
                color = UiPalette.Bad;
                icon = IconKind.Warning;
                break;

            case EraBlock.People:
                _line.Append(Strings.Get("panel.need_people")).Append(' ').AppendGrouped(table.MinPop[next]);
                color = UiPalette.Accent;
                icon = IconKind.People;
                break;

            case EraBlock.Resource:
                ResourceKind missing = EraRules.FirstMissing(tracked ? tech!.MissingResources[tribe] : 0);
                _line.Append(Strings.Get("panel.need_resource")).Append(' ').Append(Strings.Get(ResourceKinds.NameKey(missing)));
                color = UiPalette.Accent;
                icon = IconKind.MapResources;
                break;

            case EraBlock.Geography:
                GeoFeature feature = EraRules.FirstMissingGeo(tracked ? tech!.MissingGeo[tribe] : (byte)0);
                _line.Append(Strings.Get("panel.need_geo")).Append(' ').Append(Strings.Get(EraRules.GeoNameKey(feature)));
                color = UiPalette.Accent;
                icon = IconKind.MapTerrain;
                break;

            default:
                float share = tracked ? tech!.ProgressShare(table, era, tribe) : 0f;
                _line.Append(Strings.Get("panel.progress")).Append(' ').Append((int)MathF.Round(share * 100f)).Append('%');
                color = EraColor;
                icon = IconKind.Era;
                break;
        }

        DrawStatusLine(batch, font, skin, icon, x, y, step, color, scale);
    }

    private void DrawStatusLine(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        IconKind icon,
        int x,
        int y,
        int step,
        Color color,
        int scale)
    {
        int textX = x;
        if (skin != null)
        {
            skin.Icon(batch, icon, x, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    /// <summary>Отбирает самые многолюдные народы вставкой в готовый массив, без сортировки списков.</summary>
    private int Collect(TribeStore tribes)
    {
        int count = 0;

        for (int t = 1; t < tribes.Capacity; t++)
        {
            if (!tribes.Alive[t])
            {
                continue;
            }

            int people = tribes.People[t];
            int pos = count;
            while (pos > 0 && tribes.People[_order[pos - 1]] < people)
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
                _order[i] = _order[i - 1];
            }

            _order[pos] = t;
            if (count < MaxRows)
            {
                count++;
            }
        }

        return count;
    }
}
