using System.Text.Json;
using WorldBox.Core.Data;
using WorldBox.Core.World;

namespace WorldBox.Core.Economy;

/// <summary>
/// Таблица товаров и правил хозяйства из data/economy.json.
/// Числа живут в данных, а не в коде: балансом крутят файлом, не сборкой.
///
/// Товаров ровно столько, сколько видов ресурсов на карте. Нулевой слот карты —
/// «ничего», и в экономике он занят едой: еда не лежит жилой в земле, но считается
/// так же, как остальные товары, и это избавляет от второго массива складов.
/// Поэтому номер товара для слотов с первого совпадает с <see cref="ResourceKind"/>,
/// а бит товара совпадает с битом ресурса в таблице эпох.
/// </summary>
public sealed class EconomyTable
{
    /// <summary>Имя файла с настройками экономики.</summary>
    public const string FileName = "economy.json";

    /// <summary>Номер еды. Совпадает с «пустым» слотом ресурсов карты.</summary>
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

    /// <summary>Сколько товаров в игре.</summary>
    public int Count => Id.Length;

    /// <summary>Строковый ключ товара: food, wood, iron и так далее.</summary>
    public string[] Id { get; }

    /// <summary>Ключ русского названия для data/strings.ru.json.</summary>
    public string[] NameKey { get; }

    /// <summary>Базовая ценность товара: железо дороже дерева.</summary>
    public float[] Value { get; }

    /// <summary>С какой эпохи народ умеет добывать этот товар.</summary>
    public int[] MinEra { get; }

    public EconomyProductionSettings Production { get; }

    public EconomyConsumptionSettings Consumption { get; }

    public EconomyStorageSettings Storage { get; }

    public EconomyPriceSettings Price { get; }

    public EconomyTradeSettings Trade { get; }

    /// <summary>Бит товара. Совпадает с битом ресурса в таблице эпох.</summary>
    public static int GoodBit(int good) => 1 << good;

    /// <summary>Умеет ли народ этой эпохи добывать товар сам.</summary>
    public bool CanExtract(int good, int era)
    {
        return (uint)good < (uint)Count && era >= MinEra[good];
    }

    /// <summary>Ключ названия товара или пустая строка, если номера нет.</summary>
    public string NameKeyOf(int good)
    {
        return (uint)good < (uint)Count ? NameKey[good] : string.Empty;
    }

    /// <summary>Номер товара по строковому ключу или -1.</summary>
    public int Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return -1;
        }

        for (int good = 0; good < Id.Length; good++)
        {
            if (string.Equals(Id[good], id, StringComparison.OrdinalIgnoreCase))
            {
                return good;
            }
        }

        return -1;
    }

    /// <summary>Читает таблицу с диска. Возвращает null и текст ошибки, если файл плохой.</summary>
    public static EconomyTable? Load(out string error)
    {
        string? path = DataPaths.Find(FileName);
        if (path == null)
        {
            error = "Файл data/" + FileName + " не найден.";
            return null;
        }

        try
        {
            return Parse(File.ReadAllText(path), out error);
        }
        catch (IOException exception)
        {
            error = "data/" + FileName + " не читается: " + exception.Message;
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = "data/" + FileName + " не читается: " + exception.Message;
            return null;
        }
    }

    /// <summary>Разбирает таблицу из текста. Ошибка приходит строкой на русском.</summary>
    public static EconomyTable? Parse(string json, out string error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            EconomyProductionSettings? production = ReadProduction(root, out error);
            if (production == null)
            {
                return null;
            }

            EconomyConsumptionSettings? consumption = ReadConsumption(root, out error);
            if (consumption == null)
            {
                return null;
            }

            EconomyStorageSettings? storage = ReadStorage(root, out error);
            if (storage == null)
            {
                return null;
            }

            EconomyPriceSettings? price = ReadPrice(root, out error);
            if (price == null)
            {
                return null;
            }

            EconomyTradeSettings? trade = ReadTrade(root, out error);
            if (trade == null)
            {
                return null;
            }

            if (!root.TryGetProperty("goods", out JsonElement goods) || goods.ValueKind != JsonValueKind.Array)
            {
                error = "Раздел goods в data/" + FileName + " отсутствует.";
                return null;
            }

            int count = goods.GetArrayLength();
            if (count != ResourceKinds.Count)
            {
                error = "В разделе goods должно быть " + ResourceKinds.Count
                    + " товаров по числу ресурсов карты, а их " + count + ".";
                return null;
            }

            var id = new string[count];
            var nameKey = new string[count];
            var value = new float[count];
            var minEra = new int[count];

            int index = 0;
            foreach (JsonElement item in goods.EnumerateArray())
            {
                string prefix = "Товар номер " + (index + 1) + ": ";

                if (item.ValueKind != JsonValueKind.Object)
                {
                    error = prefix + "ожидается объект с полями id, value и minEra.";
                    return null;
                }

                string raw = ReadString(item, "id");
                if (raw.Length == 0)
                {
                    error = prefix + "нужен непустой id.";
                    return null;
                }

                if (index == Food)
                {
                    if (!string.Equals(raw, "food", StringComparison.OrdinalIgnoreCase))
                    {
                        error = prefix + "первым товаром идёт еда с id food, а стоит " + raw + ".";
                        return null;
                    }

                    nameKey[index] = "good.food";
                }
                else
                {
                    if (ResourceKinds.FromId(raw) != (ResourceKind)index)
                    {
                        error = prefix + "порядок товаров должен совпадать с ресурсами карты, "
                            + "на этом месте ждём ресурс номер " + index + ", а стоит " + raw + ".";
                        return null;
                    }

                    nameKey[index] = "resource." + raw;
                }

                if (!TryReadFloat(item, "value", out float goodValue) || goodValue <= 0f)
                {
                    error = prefix + "нужна положительная ценность value.";
                    return null;
                }

                if (!TryReadInt(item, "minEra", out int goodMinEra) || goodMinEra < 0)
                {
                    error = prefix + "нужен неотрицательный minEra.";
                    return null;
                }

                id[index] = raw;
                value[index] = goodValue;
                minEra[index] = goodMinEra;
                index++;
            }

            error = string.Empty;
            return new EconomyTable(id, nameKey, value, minEra, production, consumption, storage, price, trade);
        }
        catch (JsonException exception)
        {
            error = "data/" + FileName + " не читается как JSON: " + exception.Message;
            return null;
        }
    }

    private static EconomyProductionSettings? ReadProduction(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "production", out JsonElement block)
            || !TryReadFloat(block, "foodPerFertileTile", out float foodPerFertileTile)
            || !TryReadFloat(block, "foodPerTile", out float foodPerTile)
            || !TryReadFloat(block, "resourcePerTile", out float resourcePerTile)
            || !TryReadFloat(block, "settlementBonus", out float settlementBonus)
            || !TryReadFloat(block, "eraBonus", out float eraBonus))
        {
            error = "Раздел production в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (foodPerFertileTile < 0f || foodPerTile < 0f || resourcePerTile < 0f)
        {
            error = "В разделе production добыча не может быть отрицательной.";
            return null;
        }

        error = string.Empty;
        return new EconomyProductionSettings(
            foodPerFertileTile,
            foodPerTile,
            resourcePerTile,
            settlementBonus,
            eraBonus);
    }

    private static EconomyConsumptionSettings? ReadConsumption(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "consumption", out JsonElement block)
            || !TryReadFloat(block, "foodPerPerson", out float foodPerPerson)
            || !TryReadFloat(block, "goodsPerPerson", out float goodsPerPerson)
            || !TryReadFloat(block, "goodsPerSettlement", out float goodsPerSettlement)
            || !TryReadFloat(block, "demandFloorTiles", out float demandFloorTiles))
        {
            error = "Раздел consumption в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (foodPerPerson <= 0f)
        {
            error = "В разделе consumption нужна положительная еда на человека.";
            return null;
        }

        if (demandFloorTiles < 0f)
        {
            error = "В разделе consumption demandFloorTiles не может быть отрицательным.";
            return null;
        }

        error = string.Empty;
        return new EconomyConsumptionSettings(
            foodPerPerson,
            goodsPerPerson,
            goodsPerSettlement,
            demandFloorTiles);
    }

    private static EconomyStorageSettings? ReadStorage(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "storage", out JsonElement block)
            || !TryReadFloat(block, "perTile", out float perTile)
            || !TryReadFloat(block, "perSettlement", out float perSettlement)
            || !TryReadFloat(block, "spoilShare", out float spoilShare)
            || !TryReadFloat(block, "startFoodShare", out float startFoodShare)
            || !TryReadFloat(block, "granaryPerPerson", out float granaryPerPerson)
            || !TryReadFloat(block, "granaryFillShare", out float granaryFillShare)
            || !TryReadFloat(block, "siegeDrainShare", out float siegeDrainShare))
        {
            error = "Раздел storage в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (perTile < 0f || perSettlement < 0f)
        {
            error = "В разделе storage вместимость не может быть отрицательной.";
            return null;
        }

        if (spoilShare < 0f || spoilShare >= 1f)
        {
            error = "В разделе storage spoilShare должен быть от 0 до 1.";
            return null;
        }

        if (startFoodShare < 0f || startFoodShare > 1f)
        {
            error = "В разделе storage startFoodShare должен быть от 0 до 1.";
            return null;
        }

        if (granaryPerPerson < 0f)
        {
            error = "В разделе storage granaryPerPerson не может быть отрицательным.";
            return null;
        }

        if (granaryFillShare <= 0f || granaryFillShare > 1f)
        {
            error = "В разделе storage granaryFillShare должен быть больше 0 и не больше 1.";
            return null;
        }

        if (siegeDrainShare <= 0f || siegeDrainShare > 1f)
        {
            error = "В разделе storage siegeDrainShare должен быть больше 0 и не больше 1.";
            return null;
        }

        error = string.Empty;
        return new EconomyStorageSettings(
            perTile,
            perSettlement,
            spoilShare,
            startFoodShare,
            granaryPerPerson,
            granaryFillShare,
            siegeDrainShare);
    }

    private static EconomyPriceSettings? ReadPrice(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "price", out JsonElement block)
            || !TryReadFloat(block, "base", out float basePrice)
            || !TryReadFloat(block, "min", out float min)
            || !TryReadFloat(block, "max", out float max)
            || !TryReadFloat(block, "coverTarget", out float coverTarget)
            || !TryReadFloat(block, "scarcityWeight", out float scarcityWeight)
            || !TryReadFloat(block, "smoothing", out float smoothing))
        {
            error = "Раздел price в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (basePrice <= 0f)
        {
            error = "В разделе price нужна положительная базовая цена.";
            return null;
        }

        if (min <= 0f || max <= min)
        {
            error = "В разделе price нужно 0 < min < max.";
            return null;
        }

        if (coverTarget <= 0f)
        {
            error = "В разделе price нужен положительный coverTarget.";
            return null;
        }

        if (scarcityWeight <= 0f)
        {
            error = "В разделе price нужен положительный scarcityWeight.";
            return null;
        }

        if (smoothing <= 0f || smoothing > 1f)
        {
            error = "В разделе price smoothing должен быть больше 0 и не больше 1.";
            return null;
        }

        error = string.Empty;
        return new EconomyPriceSettings(basePrice, min, max, coverTarget, scarcityWeight, smoothing);
    }

    private static EconomyTradeSettings? ReadTrade(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "trade", out JsonElement block)
            || !TryReadInt(block, "maxRoutes", out int maxRoutes)
            || !TryReadInt(block, "rangeTiles", out int rangeTiles)
            || !TryReadInt(block, "seaRangeTiles", out int seaRangeTiles)
            || !TryReadInt(block, "seaEra", out int seaEra)
            || !TryReadInt(block, "rebuildTicks", out int rebuildTicks)
            || !TryReadFloat(block, "flowShare", out float flowShare)
            || !TryReadFloat(block, "flowCap", out float flowCap)
            || !TryReadFloat(block, "researchBonusPerRoute", out float researchBonusPerRoute)
            || !TryReadFloat(block, "researchBonusMax", out float researchBonusMax)
            || !TryReadFloat(block, "profitShare", out float profitShare))
        {
            error = "Раздел trade в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (maxRoutes <= 0)
        {
            error = "В разделе trade нужен положительный maxRoutes.";
            return null;
        }

        if (rangeTiles <= 0 || seaRangeTiles <= 0)
        {
            error = "В разделе trade дальность путей должна быть положительной.";
            return null;
        }

        if (seaEra < 0)
        {
            error = "В разделе trade seaEra не может быть отрицательным.";
            return null;
        }

        if (rebuildTicks <= 0)
        {
            error = "В разделе trade нужен положительный rebuildTicks.";
            return null;
        }

        if (flowShare <= 0f || flowShare > 1f)
        {
            error = "В разделе trade flowShare должен быть больше 0 и не больше 1.";
            return null;
        }

        if (flowCap <= 0f)
        {
            error = "В разделе trade нужен положительный flowCap.";
            return null;
        }

        if (researchBonusPerRoute < 0f || researchBonusMax < 0f)
        {
            error = "В разделе trade торговый бонус к исследованиям не может быть отрицательным.";
            return null;
        }

        if (profitShare < 0f)
        {
            error = "В разделе trade profitShare не может быть отрицательным.";
            return null;
        }

        error = string.Empty;
        return new EconomyTradeSettings(
            maxRoutes,
            rangeTiles,
            seaRangeTiles,
            seaEra,
            rebuildTicks,
            flowShare,
            flowCap,
            researchBonusPerRoute,
            researchBonusMax,
            profitShare);
    }

    private static bool TryReadBlock(JsonElement root, string name, out JsonElement block)
    {
        return root.TryGetProperty(name, out block) && block.ValueKind == JsonValueKind.Object;
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryReadFloat(JsonElement element, string name, out float result)
    {
        if (element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number))
        {
            result = (float)number;
            return true;
        }

        result = 0f;
        return false;
    }

    private static bool TryReadInt(JsonElement element, string name, out int result)
    {
        if (element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int number))
        {
            result = number;
            return true;
        }

        result = 0;
        return false;
    }
}
