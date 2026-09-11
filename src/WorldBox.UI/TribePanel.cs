using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    private static readonly Color PanelColor = new Color(10, 12, 16, 200);
    private static readonly Color BorderColor = new Color(255, 255, 255, 45);
    private static readonly Color TitleColor = new Color(206, 178, 240);
    private static readonly Color TextColor = new Color(226, 226, 226);
    private static readonly Color DimColor = new Color(158, 158, 166);
    private static readonly Color BlockedColor = new Color(226, 168, 120);
    private static readonly Color DarkAgeColor = new Color(233, 115, 102);
    private static readonly Color TopColor = new Color(158, 204, 172);

    private readonly TextBuilder _line = new TextBuilder(192);
    private readonly int[] _order = new int[MaxRows];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

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
        int viewportHeight)
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
        int height = (step * textLines) + (10 * scale);
        var panel = new Rectangle(viewportWidth - PanelWidth - 12, viewportHeight - height - 12, PanelWidth, height);
        primitives.FillRect(batch, panel, PanelColor);
        primitives.FrameRect(batch, panel, BorderColor);

        int x = panel.X + (6 * scale);
        int y = panel.Y + (5 * scale);

        _line.Clear().Append(Strings.Get("panel.tribes")).Append("   ").Append(alive);
        font.Draw(batch, _line.Span, new Vector2(x, y), TitleColor, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_tribes"), new Vector2(x, y), DimColor, scale);
            return;
        }

        for (int i = 0; i < rows; i++)
        {
            int tribe = _order[i];
            Color color = TribePalette.Of(tribes.ColorIndex[tribe]);

            // Цветной квадратик — тот же цвет, что у владений на карте.
            var swatch = new Rectangle(x, y + scale, step - (2 * scale), step - (3 * scale));
            primitives.FillRect(batch, swatch, color);
            primitives.FrameRect(batch, swatch, BorderColor);

            int textX = x + step;
            _line.Clear().Append(tribes.Name[tribe] ?? string.Empty);
            if (table != null)
            {
                int era = Math.Clamp(tribes.Era[tribe], 0, table.Last);
                _line.Append("   ").Append(Strings.Get(table.NameKeyOf(era)));
            }

            font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
            y += step;

            DrawStatus(batch, font, tribes, tech, table, tribe, textX, y, scale);
            y += step;
        }

        DrawnRows = rows;
    }

    /// <summary>Вторая строка народа: люди, поселения и причина остановки.</summary>
    private void DrawStatus(
        SpriteBatch batch,
        PixelFont font,
        TribeStore tribes,
        TribeTech? tech,
        EraTable? table,
        int tribe,
        int x,
        int y,
        int scale)
    {
        bool tracked = tech != null && (uint)tribe < (uint)tech.Capacity;
        long headcount = tracked ? tech!.Headcount[tribe] : tribes.People[tribe];

        _line.Clear()
            .Append(Strings.Get("panel.people")).Append(' ').AppendGrouped(headcount)
            .Append("   ").Append(Strings.Get("panel.settlements_short")).Append(' ').Append(tribes.Settlements[tribe])
            .Append("   ");

        Color color = DimColor;

        if (table == null)
        {
            _line.Append(Strings.Get("panel.no_eras"));
            font.Draw(batch, _line.Span, new Vector2(x, y), color, scale);
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
                color = TopColor;
                break;

            case EraBlock.DarkAge:
                _line.Append(Strings.Get("panel.dark_age"));
                color = DarkAgeColor;
                break;

            case EraBlock.People:
                _line.Append(Strings.Get("panel.need_people")).Append(' ').AppendGrouped(table.MinPop[next]);
                color = BlockedColor;
                break;

            case EraBlock.Resource:
                ResourceKind missing = EraRules.FirstMissing(tracked ? tech!.MissingResources[tribe] : 0);
                _line.Append(Strings.Get("panel.need_resource")).Append(' ').Append(Strings.Get(ResourceKinds.NameKey(missing)));
                color = BlockedColor;
                break;

            case EraBlock.Geography:
                GeoFeature feature = EraRules.FirstMissingGeo(tracked ? tech!.MissingGeo[tribe] : (byte)0);
                _line.Append(Strings.Get("panel.need_geo")).Append(' ').Append(Strings.Get(EraRules.GeoNameKey(feature)));
                color = BlockedColor;
                break;

            default:
                float share = tracked ? tech!.ProgressShare(table, era, tribe) : 0f;
                _line.Append(Strings.Get("panel.progress")).Append(' ').Append((int)MathF.Round(share * 100f)).Append('%');
                color = TextColor;
                break;
        }

        font.Draw(batch, _line.Span, new Vector2(x, y), color, scale);
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
