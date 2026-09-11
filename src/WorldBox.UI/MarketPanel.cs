using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Economy;
using WorldBox.Core.Tribes;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Панель торговли: у кого сколько еды и денег, кто голодает и кто с кем возит товар.
/// Экономика без окна — это невидимые числа: игрок видит, что город зачах, но не понимает,
/// что у него кончилась еда, а сосед разбогател на железе. Панель отвечает ровно на это.
///
/// Народы сортируются по богатству, но голодные всегда всплывают наверх: беда важнее казны.
/// Строки собираются в один буфер, порядок — в заранее выделенный массив: за кадр ноль аллокаций.
/// </summary>
public sealed class MarketPanel
{
    /// <summary>Сколько народов помещается в панель. Больше шести окно упирается в осмотр тайла.</summary>
    public const int MaxRows = 6;

    private const int PanelWidth = 620;
    private const int IconSpace = 22;

    /// <summary>Надбавка голодным: такая беда важнее любой казны и всегда всплывает первой строкой.</summary>
    private const float HungryBoost = 1000000f;

    private static readonly Color GoodsColor = new Color(214, 198, 160);

    private readonly TextBuilder _line = new TextBuilder(224);
    private readonly int[] _order = new int[MaxRows];

    public bool Visible { get; set; }

    public int Scale { get; set; } = 2;

    /// <summary>Отступ сверху. Игра опускает панель под статистику, чтобы окна не налезали.</summary>
    public int TopMargin { get; set; } = 16;

    /// <summary>Сколько народов показано в последнем кадре.</summary>
    public int DrawnRows { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        TribeStore tribes,
        TribeMarket? market,
        EconomyTable? table,
        TradeNetwork? network,
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

        if (market == null || table == null)
        {
            DrawEmpty(batch, font, primitives, skin, step, scale);
            return;
        }

        int rows = Collect(tribes, market);
        int textLines = rows > 0 ? 2 + (rows * 2) : 2;
        int height = (step * textLines) + (11 * scale);

        // Окно не должно залезть на тулбар: если места мало, нижние строки просто не рисуются.
        int room = viewportHeight - TopMargin - Toolbar.ReservedHeight - 12;
        if (room > step * 2 && height > room)
        {
            height = room;
        }

        var panel = new Rectangle(14, TopMargin, PanelWidth, height);
        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        if (skin != null)
        {
            skin.Icon(batch, IconKind.Market, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        _line.Clear().Append(Strings.Get("panel.market"));
        if (network != null)
        {
            _line.Append("   ").Append(Strings.Get("panel.routes")).Append(' ').Append(network.Count);
        }

        font.Draw(batch, _line.Span, new Vector2(x, y), UiPalette.Accent, scale);
        y += step;

        if (rows == 0)
        {
            font.Draw(batch, Strings.Get("panel.no_trade"), new Vector2(x, y), UiPalette.TextMuted, scale);
            return;
        }

        int bottom = panel.Bottom - (4 * scale);

        for (int i = 0; i < rows; i++)
        {
            if (y + (step * 2) > bottom)
            {
                break;
            }

            int tribe = _order[i];
            Color color = TribePalette.Of(tribes.ColorIndex[tribe]);

            // Полоска на две строки: сразу видно, где заканчивается один народ.
            if ((i & 1) == 0)
            {
                var stripe = new Rectangle(panel.X + (3 * scale), y, PanelWidth - (6 * scale), step * 2);
                primitives.FillRect(batch, stripe, UiPalette.PanelLight * 0.45f);
            }

            var swatch = new Rectangle(iconX, y + scale, step - (2 * scale), step - (3 * scale));
            primitives.FillRect(batch, swatch, color);
            primitives.FrameRect(batch, swatch, UiPalette.Border, 1);

            int textX = iconX + step + (2 * scale);

            _line.Clear()
                .Append(tribes.Name[tribe] ?? string.Empty)
                .Append("   ").Append(Strings.Get("panel.wealth")).Append(' ')
                .AppendGrouped((long)MathF.Round(market.Wealth[tribe]))
                .Append("   ").Append(Strings.Get("panel.food")).Append(' ')
                .AppendGrouped((long)MathF.Round(market.FoodOf(tribe)));

            font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
            y += step;

            DrawStatus(batch, font, skin, market, table, tribe, textX, y, step, scale);
            y += step;
            DrawnRows++;
        }

        if (network != null && y + step <= bottom)
        {
            DrawFlow(batch, font, skin, network, iconX, y, step, scale);
        }
    }

    /// <summary>Вторая строка народа: голод, пути и самый дорогой товар.</summary>
    private void DrawStatus(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        TribeMarket market,
        EconomyTable table,
        int tribe,
        int x,
        int y,
        int step,
        int scale)
    {
        Color color = UiPalette.TextMuted;
        IconKind icon = IconKind.Food;
        _line.Clear();

        float famine = market.Famine[tribe];
        if (famine > 0f)
        {
            // Голод важнее всего: строка краснеет, остальные цифры уходят на второй план.
            _line.Append(Strings.Get("panel.hunger")).Append(' ')
                .Append((int)MathF.Round(famine * 100f)).Append('%')
                .Append("   ").Append(Strings.Get("panel.routes")).Append(' ').Append(market.Routes[tribe]);
            color = UiPalette.Bad;
            icon = IconKind.Warning;
        }
        else
        {
            _line.Append(Strings.Get("panel.routes")).Append(' ').Append(market.Routes[tribe])
                .Append("   ").Append(Strings.Get("panel.partners")).Append(' ').Append(market.PartnerCount(tribe));

            int dearest = market.DearestGoodOf(tribe);
            if (dearest >= 0)
            {
                _line.Append("   ").Append(Strings.Get("panel.dearest")).Append(' ')
                    .Append(Strings.Get(table.NameKeyOf(dearest))).Append(' ')
                    .Append(market.PriceOf(tribe, dearest), 1);
                color = GoodsColor;
                icon = IconKind.MapResources;
            }

            if (market.Routes[tribe] > 0)
            {
                color = UiPalette.Good;
                icon = IconKind.Market;
            }
        }

        int textX = x;
        if (skin != null)
        {
            skin.Icon(batch, icon, x, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    /// <summary>Итоговая строка: сколько товара прошло по всем путям в последний прогон.</summary>
    private void DrawFlow(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        TradeNetwork network,
        int iconX,
        int y,
        int step,
        int scale)
    {
        float flow = 0f;
        for (int i = 0; i < network.Count; i++)
        {
            flow += network.Volume[i];
        }

        _line.Clear()
            .Append(Strings.Get("panel.trade_flow")).Append(' ')
            .AppendGrouped((long)MathF.Round(flow));

        int textX = iconX;
        if (skin != null)
        {
            skin.Icon(batch, IconKind.Market, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
            textX += IconSpace;
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), UiPalette.TextMuted, scale);
    }

    /// <summary>Окно-заглушка, когда data/economy.json не прочитался.</summary>
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
        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        if (skin != null)
        {
            skin.Icon(batch, IconKind.Market, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, Strings.Get("panel.market"), new Vector2(x, y), UiPalette.Accent, scale);
        y += step;
        font.Draw(batch, Strings.Get("panel.no_market"), new Vector2(x, y), UiPalette.TextMuted, scale);
    }

    /// <summary>Отбирает народов вставкой в готовый массив: голодные первыми, дальше по богатству.</summary>
    private int Collect(TribeStore tribes, TribeMarket market)
    {
        int count = 0;
        int limit = Math.Min(tribes.Capacity, market.Capacity);

        for (int t = 1; t < limit; t++)
        {
            if (!tribes.Alive[t])
            {
                continue;
            }

            float score = ScoreOf(market, t);
            int pos = count;
            while (pos > 0 && ScoreOf(market, _order[pos - 1]) < score)
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

    private static float ScoreOf(TribeMarket market, int tribe)
    {
        float score = market.Wealth[tribe] + (market.StockValueOf(tribe) * 0.25f);
        return market.IsHungry(tribe) ? score + HungryBoost : score;
    }
}
