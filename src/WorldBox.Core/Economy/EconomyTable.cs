using System.Globalization;
using System.Text.Json;
using WorldBox.Core.Data;
using WorldBox.Core.World;

namespace WorldBox.Core.Economy;

/// <summary>
/// Таблица экономики из data/economy.json: товары, добыча, потребление, склады, цены и торговля.
/// Файл читается один раз при старте, в тиках обращений к диску нет.
/// Значений баланса по умолчанию в коде нет: опечатка в файле видна сразу, а не через час партии.
///
/// Товаров ровно столько, сколько значений в <see cref="ResourceKind"/>. Нулевой слот на карте
/// означает «ресурса нет», поэтому в экономике он занят едой: так склад народа — это один
/// массив без особых случаев.
/// </summary>
public sealed class EconomyTable
{
    public const string FileName = "economy.json";

    /// <summary>Номер еды в складе. Совпадает с <see cref="ResourceKind.None"/> на карте.</summary>
    public const int Food = 0;

    private EconomyTable(
        string[] id,
        string[] nameKey,
        float[] value,
        int[] minEra,
        EconomyProductionSettings production,
        EconomyConsumptionSettings consumption,
        EconomyStorageSettings storage,
        EconomyPriceSettings price,
        EconomyTradeSettings trade)
    {
        Id = id;
        NameKey = nameKey;
        Value = value;
        MinEra = minEra;
        Production = production;
        Consumption = consumption;
        Storage = storage;
        Price = price;
        Trade = trade;
    }

    /// <summary>Сколько товаров в таблице.</summary>
    public int Count => Id.Length;

    /// <summary>Идентификаторы товаров: food, wood, stone и так далее.</summary>
    public string[] Id { get; }

    /// <summary>Ключи названий для data/strings.ru.json.</summary>
    public string[] NameKey { get; }

    /// <summary>Во сколько раз товар дороже еды.</summary>
    public float[] Value { get; }

    /// <summary>С какой эпохи народ умеет добывать товар.</summary>
    public int[] MinEra { get; }

    public EconomyProductionSettings Production { get; }

    public EconomyConsumptionSettings Consumption { get; }

    public EconomyStorageSettings Storage { get; }

    public EconomyPriceSettings Price { get; }

    public EconomyTradeSettings Trade { get; }

    /// <summary>Номер товара по ресурсу карты.</summary>
    public static int GoodOf(ResourceKind kind) => (int)kind;

    /// <summary>Умеет ли народ этой эпохи добывать товар.</summary>
    public bool CanExtract(int good, int era)
    {
        return (uint)good < (uint)Count && era >= MinEra[good];
    }

    /// <summary>Ключ названия товара. Номер за краем таблицы прижимается к границе.</summary>
    public string NameKeyOf(int good) => NameKey[Math.Clamp(good, 0, Count - 1)];

    /// <summary>Стоимость товара. Номер за краем таблицы прижимается к границе.</summary>
    public float ValueOf(int good) => Value[Math.Clamp(good, 0, Count - 1)];

    /// <summary>Читает таблицу из папки data. Возвращает null и текст ошибки, если не вышло.</summary>
    public static EconomyTable? Load(out string error)
    {
        string? path = DataPaths.Find(FileName);
        if (path == null)
        {
            error = "Файл data/" + FileName + " не найден рядом с программой и выше по дереву папок.";
            return null;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            error = "Не удалось прочитать " + path + ": " + exception.Message;
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = "Нет доступа к " + path + ": " + exception.Message;
            return null;
        }

        return Parse(json, out error);
    }

    /// <summary>Разбирает таблицу из текста. Отдельно от файла, чтобы тесты не зависели от диска.</summary>
    public static EconomyTable? Parse(string json, out string error)
    {
        ArgumentNullException.ThrowIfNull(json);
        error = string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("goods", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array
                || list.GetArrayLength() != ResourceKinds.Count)
            {
                error = "В data/economy.json нужен массив goods ровно из "
                    + ResourceKinds.Count.ToString(CultureInfo.InvariantCulture)
                    + " товаров, первый из них — еда.";
                return null;
            }

            int count = list.GetArrayLength();
            var id = new string[count];
            var nameKey = new string[count];
            var value = new float[count];
            var minEra = new int[count];

            int index = 0;
            foreach (JsonElement good in list.EnumerateArray())
            {
                string place = "Товар номер " + (index + 1).ToString(CultureInfo.InvariantCulture) + ": ";

                string? goodId = ReadString(good, "id");
                if (string.IsNullOrEmpty(goodId))
                {
                    error = place + "нет поля id.";
                    return null;
                }

                if (index == Food)
                {
                    if (!string.Equals(goodId, "food", StringComparison.OrdinalIgnoreCase))
                    {
                        error = place + "первым товаром должна быть еда с id food.";
                        return null;
                    }

                    nameKey[index] = "good.food";
                }
                else
                {
                    // Порядок товаров совпадает с ResourceKind, иначе склад разъедется с картой.
                    ResourceKind kind = ResourceKinds.FromId(goodId);
                    if (kind != (ResourceKind)index)
                    {
                        error = place + "ожидался ресурс карты номер "
                            + index.ToString(CultureInfo.InvariantCulture)
                            + ", а в файле " + goodId + ".";
                        return null;
                    }

                    nameKey[index] = "resource." + goodId;
                }

                id[index] = goodId;

                if (!TryReadFloat(good, "value", out value[index]) || value[index] <= 0f)
                {
                    error = place + "нужно положительное value.";
                    return null;
                }

                if (!TryReadInt(good, "minEra", out minEra[index]) || minEra[index] < 0)
                {
                    error = place + "нужно поле minEra не меньше нуля.";
                    return null;
                }

                index++;
            }

            if (!TryReadBlock(root, "production", out JsonElement productionBlock)
                || !TryReadFloat(productionBlock, "foodPerFertileTile", out float foodPerFertileTile)
                || !TryReadFloat(productionBlock, "foodPerTile", out float foodPerTile)
                || !TryReadFloat(productionBlock, "resourcePerTile", out float resourcePerTile)
                || !TryReadFloat(productionBlock, "settlementBonus", out float productionSettlementBonus)
                || !TryReadFloat(productionBlock, "eraBonus", out float eraBonus))
            {
                error = "Раздел production в data/economy.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (foodPerFertileTile < 0f || foodPerTile < 0f || resourcePerTile <= 0f)
            {
                error = "Раздел production: добыча не может быть отрицательной, resourcePerTile должен быть больше нуля.";
                return null;
            }

            if (!TryReadBlock(root, "consumption", out JsonElement consumptionBlock)
                || !TryReadFloat(consumptionBlock, "foodPerPerson", out float foodPerPerson)
                || !TryReadFloat(consumptionBlock, "goodsPerPerson", out float goodsPerPerson)
                || !TryReadFloat(consumptionBlock, "goodsPerSettlement", out float goodsPerSettlement))
            {
                error = "Раздел consumption в data/economy.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (foodPerPerson <= 0f || goodsPerPerson < 0f || goodsPerSettlement < 0f)
            {
                error = "Раздел consumption: foodPerPerson должен быть больше нуля, остальное не меньше нуля.";
                return null;
            }

            if (!TryReadBlock(root, "storage", out JsonElement storageBlock)
                || !TryReadFloat(storageBlock, "perTile", out float perTile)
                || !TryReadFloat(storageBlock, "perSettlement", out float perSettlement)
                || !TryReadFloat(storageBlock, "spoilShare", out float spoilShare)
                || !TryReadFloat(storageBlock, "startFoodShare", out float startFoodShare))
            {
                error = "Раздел storage в data/economy.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (perTile < 0f || perSettlement < 0f || perTile + perSettlement <= 0f
                || spoilShare < 0f || spoilShare >= 1f
                || startFoodShare < 0f || startFoodShare > 1f)
            {
                error = "Раздел storage: склад должен быть больше нуля, spoilShare меньше единицы, startFoodShare от 0 до 1.";
                return null;
            }

            if (!TryReadBlock(root, "price", out JsonElement priceBlock)
                || !TryReadFloat(priceBlock, "base", out float priceBase)
                || !TryReadFloat(priceBlock, "min", out float priceMin)
                || !TryReadFloat(priceBlock, "max", out float priceMax)
                || !TryReadFloat(priceBlock, "coverTarget", out float coverTarget)
                || !TryReadFloat(priceBlock, "scarcityWeight", out float scarcityWeight)
                || !TryReadFloat(priceBlock, "smoothing", out float smoothing))
            {
                error = "Раздел price в data/economy.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (priceBase <= 0f || priceMin <= 0f || priceMax <= priceMin
                || coverTarget <= 0f || scarcityWeight <= 0f
                || smoothing <= 0f || smoothing > 1f)
            {
                error = "Раздел price: цены должны быть больше нуля, max больше min, smoothing от 0 до 1.";
                return null;
            }

            if (!TryReadBlock(root, "trade", out JsonElement tradeBlock)
                || !TryReadInt(tradeBlock, "maxRoutes", out int maxRoutes)
                || !TryReadInt(tradeBlock, "rangeTiles", out int rangeTiles)
                || !TryReadInt(tradeBlock, "seaRangeTiles", out int seaRangeTiles)
                || !TryReadInt(tradeBlock, "seaEra", out int seaEra)
                || !TryReadInt(tradeBlock, "rebuildTicks", out int rebuildTicks)
                || !TryReadFloat(tradeBlock, "flowShare", out float flowShare)
                || !TryReadFloat(tradeBlock, "flowCap", out float flowCap)
                || !TryReadFloat(tradeBlock, "researchBonusPerRoute", out float researchBonusPerRoute)
                || !TryReadFloat(tradeBlock, "researchBonusMax", out float researchBonusMax)
                || !TryReadFloat(tradeBlock, "profitShare", out float profitShare))
            {
                error = "Раздел trade в data/economy.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (maxRoutes <= 0 || rangeTiles <= 0 || seaRangeTiles < rangeTiles || seaEra < 0
                || rebuildTicks <= 0 || flowShare <= 0f || flowShare > 1f || flowCap <= 0f
                || researchBonusPerRoute < 0f || researchBonusMax < 0f || profitShare < 0f)
            {
                error = "Раздел trade: нужны положительные maxRoutes, rangeTiles, rebuildTicks и flowCap, "
                    + "flowShare от 0 до 1, морская дальность не меньше сухопутной.";
                return null;
            }

            return new EconomyTable(
                id,
                nameKey,
                value,
                minEra,
                new EconomyProductionSettings(
                    foodPerFertileTile,
                    foodPerTile,
                    resourcePerTile,
                    productionSettlementBonus,
                    eraBonus),
                new EconomyConsumptionSettings(foodPerPerson, goodsPerPerson, goodsPerSettlement),
                new EconomyStorageSettings(perTile, perSettlement, spoilShare, startFoodShare),
                new EconomyPriceSettings(priceBase, priceMin, priceMax, coverTarget, scarcityWeight, smoothing),
                new EconomyTradeSettings(
                    maxRoutes,
                    rangeTiles,
                    seaRangeTiles,
                    seaEra,
                    rebuildTicks,
                    flowShare,
                    flowCap,
                    researchBonusPerRoute,
                    researchBonusMax,
                    profitShare));
        }
        catch (JsonException exception)
        {
            error = "data/economy.json не читается как JSON: " + exception.Message;
            return null;
        }
    }

    private static bool TryReadBlock(JsonElement root, string name, out JsonElement block)
    {
        return root.TryGetProperty(name, out block) && block.ValueKind == JsonValueKind.Object;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryReadFloat(JsonElement element, string name, out float result)
    {
        if (element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetSingle(out result))
        {
            return true;
        }

        result = 0f;
        return false;
    }

    private static bool TryReadInt(JsonElement element, string name, out int result)
    {
        if (element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out result))
        {
            return true;
        }

        result = 0;
        return false;
    }
}
