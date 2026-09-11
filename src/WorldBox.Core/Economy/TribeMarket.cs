using WorldBox.Core.Eras;
using WorldBox.Core.Tribes;

namespace WorldBox.Core.Economy;

/// <summary>
/// Склады и цены каждого народа. Один глупый склад данных без правил:
/// считает цифры <see cref="EconomySystem"/>, а пользуются ими интерфейс, рисовалка и эпохи.
///
/// Данные лежат плоскими массивами длиной «народы на товары»: так проход по складу
/// одного народа идёт по памяти подряд и в тике нет ни одной аллокации.
/// </summary>
public sealed class TribeMarket : ITradeAccess
{
    /// <summary>Сколько народов влезает в битовую маску партнёров.</summary>
    public const int MaxTracked = 64;

    private readonly int _goods;

    public TribeMarket(int capacity, int goods)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Нужен хотя бы один слот народа.");
        }

        if (goods <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(goods), "Нужен хотя бы один товар.");
        }

        Capacity = capacity;
        _goods = goods;

        int cells = capacity * goods;
        Stock = new float[cells];
        Price = new float[cells];
        Produced = new float[cells];
        Consumed = new float[cells];
        Imported = new float[cells];
        Exported = new float[cells];

        Limit = new float[capacity];
        Wealth = new float[capacity];
        Income = new float[capacity];
        Deficit = new int[capacity];
        Surplus = new int[capacity];
        TradeResources = new int[capacity];
        ResearchBonus = new float[capacity];
        Routes = new int[capacity];
        Partners = new ulong[capacity];
        Famine = new float[capacity];
    }

    /// <summary>Сколько народов помещается.</summary>
    public int Capacity { get; }

    /// <summary>Сколько товаров у каждого народа.</summary>
    public int Goods => _goods;

    /// <summary>Запас товара на складе.</summary>
    public float[] Stock { get; }

    /// <summary>Цена товара у народа.</summary>
    public float[] Price { get; }

    /// <summary>Сколько добыто в последний прогон.</summary>
    public float[] Produced { get; }

    /// <summary>Сколько съедено и потрачено в последний прогон.</summary>
    public float[] Consumed { get; }

    /// <summary>Сколько привезли торговцы.</summary>
    public float[] Imported { get; }

    /// <summary>Сколько увезли торговцы.</summary>
    public float[] Exported { get; }

    /// <summary>Вместимость склада на один товар.</summary>
    public float[] Limit { get; }

    /// <summary>Накопленное богатство от торговли.</summary>
    public float[] Wealth { get; }

    /// <summary>Сколько богатства пришло в последний прогон.</summary>
    public float[] Income { get; }

    /// <summary>Маска товаров, которых не хватает.</summary>
    public int[] Deficit { get; }

    /// <summary>Маска товаров, которых с избытком.</summary>
    public int[] Surplus { get; }

    /// <summary>Маска ресурсов, которые приходят извне по торговым путям.</summary>
    public int[] TradeResources { get; }

    /// <summary>Насколько торговля ускоряет исследования.</summary>
    public float[] ResearchBonus { get; }

    /// <summary>Сколько торговых путей у народа.</summary>
    public int[] Routes { get; }

    /// <summary>С кем народ торгует, битовой маской по номерам народов.</summary>
    public ulong[] Partners { get; }

    /// <summary>Доля нехватки еды в последний прогон: 0 — сыты, 1 — есть нечего.</summary>
    public float[] Famine { get; }

    /// <summary>Место товара народа в плоском массиве.</summary>
    public int Cell(int tribe, int good) => (tribe * _goods) + good;

    /// <summary>Запас товара. За краем таблицы возвращает 0.</summary>
    public float StockOf(int tribe, int good)
    {
        return Inside(tribe, good) ? Stock[Cell(tribe, good)] : 0f;
    }

    /// <summary>Цена товара. За краем таблицы возвращает 0.</summary>
    public float PriceOf(int tribe, int good)
    {
        return Inside(tribe, good) ? Price[Cell(tribe, good)] : 0f;
    }

    /// <summary>Сколько еды на складе.</summary>
    public float FoodOf(int tribe) => StockOf(tribe, EconomyTable.Food);

    /// <summary>Голодает ли народ сейчас.</summary>
    public bool IsHungry(int tribe)
    {
        return (uint)tribe < (uint)Capacity && Famine[tribe] > 0f;
    }

    /// <summary>Сколько стоит всё, что лежит на складах народа.</summary>
    public float StockValueOf(int tribe)
    {
        if ((uint)tribe >= (uint)Capacity)
        {
            return 0f;
        }

        int first = tribe * _goods;
        float sum = 0f;
        for (int good = 0; good < _goods; good++)
        {
            sum += Stock[first + good] * Price[first + good];
        }

        return sum;
    }

    /// <summary>Самый дорогой товар народа или -1, если цен ещё нет.</summary>
    public int DearestGoodOf(int tribe)
    {
        if ((uint)tribe >= (uint)Capacity)
        {
            return -1;
        }

        int first = tribe * _goods;
        int best = -1;
        float top = 0f;
        for (int good = 0; good < _goods; good++)
        {
            float price = Price[first + good];
            if (price > top)
            {
                top = price;
                best = good;
            }
        }

        return best;
    }

    /// <inheritdoc/>
    public int TradeResourcesOf(int tribe)
    {
        return (uint)tribe < (uint)Capacity ? TradeResources[tribe] : 0;
    }

    /// <inheritdoc/>
    public float ResearchBonusOf(int tribe)
    {
        return (uint)tribe < (uint)Capacity ? ResearchBonus[tribe] : 0f;
    }

    /// <summary>Сколько торговых партнёров у народа.</summary>
    public int PartnerCount(int tribe)
    {
        return (uint)tribe < (uint)Capacity
            ? System.Numerics.BitOperations.PopCount(Partners[tribe])
            : 0;
    }

    /// <summary>Стирает всё про один народ: после гибели слот уходит новому владельцу чистым.</summary>
    public void Reset(int tribe)
    {
        if ((uint)tribe >= (uint)Capacity)
        {
            return;
        }

        int first = tribe * _goods;
        Array.Clear(Stock, first, _goods);
        Array.Clear(Price, first, _goods);
        Array.Clear(Produced, first, _goods);
        Array.Clear(Consumed, first, _goods);
        Array.Clear(Imported, first, _goods);
        Array.Clear(Exported, first, _goods);

        Limit[tribe] = 0f;
        Wealth[tribe] = 0f;
        Income[tribe] = 0f;
        Deficit[tribe] = 0;
        Surplus[tribe] = 0;
        TradeResources[tribe] = 0;
        ResearchBonus[tribe] = 0f;
        Routes[tribe] = 0;
        Partners[tribe] = 0UL;
        Famine[tribe] = 0f;
    }

    /// <summary>Полная очистка перед новым миром.</summary>
    public void Clear()
    {
        Array.Clear(Stock);
        Array.Clear(Price);
        Array.Clear(Produced);
        Array.Clear(Consumed);
        Array.Clear(Imported);
        Array.Clear(Exported);
        Array.Clear(Limit);
        Array.Clear(Wealth);
        Array.Clear(Income);
        Array.Clear(Deficit);
        Array.Clear(Surplus);
        Array.Clear(TradeResources);
        Array.Clear(ResearchBonus);
        Array.Clear(Routes);
        Array.Clear(Partners);
        Array.Clear(Famine);
    }

    /// <summary>
    /// Цифра для тестов на повторимость. Цены и запасы округляются до сотых:
    /// два одинаковых прогона должны давать одно и то же число.
    /// </summary>
    public ulong Checksum(TribeStore tribes)
    {
        ArgumentNullException.ThrowIfNull(tribes);

        ulong hash = 1469598103934665603UL;
        int limit = Math.Min(Capacity, tribes.Capacity);

        for (int tribe = 1; tribe < limit; tribe++)
        {
            if (!tribes.Alive[tribe])
            {
                continue;
            }

            hash = Mix(hash, (ulong)tribe);
            hash = Mix(hash, (ulong)(long)MathF.Round(Wealth[tribe] * 100f));
            hash = Mix(hash, (ulong)(long)MathF.Round(Famine[tribe] * 1000f));
            hash = Mix(hash, (ulong)(uint)Deficit[tribe]);
            hash = Mix(hash, (ulong)(uint)Surplus[tribe]);
            hash = Mix(hash, (ulong)(uint)TradeResources[tribe]);
            hash = Mix(hash, (ulong)(uint)Routes[tribe]);
            hash = Mix(hash, Partners[tribe]);

            int first = tribe * _goods;
            for (int good = 0; good < _goods; good++)
            {
                hash = Mix(hash, (ulong)(long)MathF.Round(Stock[first + good] * 100f));
                hash = Mix(hash, (ulong)(long)MathF.Round(Price[first + good] * 100f));
            }
        }

        return hash;
    }

    private bool Inside(int tribe, int good)
    {
        return (uint)tribe < (uint)Capacity && (uint)good < (uint)_goods;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
        return hash;
    }
}
