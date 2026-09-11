using System.Globalization;
using System.Text.Json;
using WorldBox.Core.Data;
using WorldBox.Core.World;

namespace WorldBox.Core.Eras;

/// <summary>
/// Таблица эпох из data/eras.json: пороги перехода, сжатие времени, еда и сила армии.
/// Файл читается один раз при старте, в тиках обращений к диску нет.
/// Значений баланса по умолчанию в коде нет: если в файле опечатка, таблица не грузится
/// и об этом сразу видно в игре, а не через час странного баланса.
/// </summary>
public sealed class EraTable
{
    public const string FileName = "eras.json";

    private EraTable(
        string[] id,
        string[] name,
        string[] nameKey,
        float[] yearsPerTick,
        long[] minPop,
        int[] requiredResources,
        byte[] requiredGeo,
        float[] foodPerWorker,
        int[] militaryPower,
        int[] pollution,
        EraDiffusionSettings diffusion,
        EraResearchSettings research,
        EraPopulationSettings population,
        EraGeoSettings geo,
        EraCollapseSettings collapse)
    {
        Id = id;
        Name = name;
        NameKey = nameKey;
        YearsPerTick = yearsPerTick;
        MinPop = minPop;
        RequiredResources = requiredResources;
        RequiredGeo = requiredGeo;
        FoodPerWorker = foodPerWorker;
        MilitaryPower = militaryPower;
        Pollution = pollution;
        Diffusion = diffusion;
        Research = research;
        Population = population;
        Geo = geo;
        Collapse = collapse;
    }

    /// <summary>Сколько эпох в таблице.</summary>
    public int Count => Id.Length;

    /// <summary>Номер последней эпохи.</summary>
    public int Last => Id.Length - 1;

    /// <summary>Идентификаторы эпох в порядке развития.</summary>
    public string[] Id { get; }

    /// <summary>Название из файла. Интерфейс берёт строку по ключу, это резерв для отладки и замеров.</summary>
    public string[] Name { get; }

    /// <summary>Ключи названий для data/strings.ru.json: era.paleolithic и так далее.</summary>
    public string[] NameKey { get; }

    /// <summary>Сколько игровых лет в тике, когда мир дошёл до этой эпохи.</summary>
    public float[] YearsPerTick { get; }

    /// <summary>Сколько людей нужно, чтобы войти в эпоху.</summary>
    public long[] MinPop { get; }

    /// <summary>Нужные ресурсы битовой маской по ResourceKind.</summary>
    public int[] RequiredResources { get; }

    /// <summary>Нужная земля битовой маской по GeoFeature.</summary>
    public byte[] RequiredGeo { get; }

    /// <summary>Сколько еды даёт один работник.</summary>
    public float[] FoodPerWorker { get; }

    /// <summary>Сила армии эпохи. Понадобится на срезе войн.</summary>
    public int[] MilitaryPower { get; }

    /// <summary>Грязь эпохи. Понадобится на срезе экологии.</summary>
    public int[] Pollution { get; }

    public EraDiffusionSettings Diffusion { get; }

    public EraResearchSettings Research { get; }

    public EraPopulationSettings Population { get; }

    public EraGeoSettings Geo { get; }

    public EraCollapseSettings Collapse { get; }

    /// <summary>Бит ресурса в маске доступа.</summary>
    public static int ResourceBit(ResourceKind kind) => 1 << (int)kind;

    /// <summary>Ключ названия эпохи. Номер за краем таблицы прижимается к границе.</summary>
    public string NameKeyOf(int era) => NameKey[Math.Clamp(era, 0, Last)];

    /// <summary>Сжатие времени для эпохи.</summary>
    public float YearsPerTickOf(int era) => YearsPerTick[Math.Clamp(era, 0, Last)];

    /// <summary>Номер эпохи по идентификатору или -1.</summary>
    public int Find(string id)
    {
        for (int i = 0; i < Id.Length; i++)
        {
            if (string.Equals(Id[i], id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Читает таблицу из папки data. Возвращает null и текст ошибки, если не вышло.</summary>
    public static EraTable? Load(out string error)
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
    public static EraTable? Parse(string json, out string error)
    {
        ArgumentNullException.ThrowIfNull(json);
        error = string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("eras", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array
                || list.GetArrayLength() == 0)
            {
                error = "В data/eras.json нет непустого массива eras.";
                return null;
            }

            int count = list.GetArrayLength();
            var id = new string[count];
            var name = new string[count];
            var nameKey = new string[count];
            var yearsPerTick = new float[count];
            var minPop = new long[count];
            var resources = new int[count];
            var geoMask = new byte[count];
            var food = new float[count];
            var military = new int[count];
            var pollution = new int[count];

            int index = 0;
            foreach (JsonElement era in list.EnumerateArray())
            {
                string place = "Эпоха номер " + (index + 1).ToString(CultureInfo.InvariantCulture) + ": ";

                string? eraId = ReadString(era, "id");
                if (string.IsNullOrEmpty(eraId))
                {
                    error = place + "нет поля id.";
                    return null;
                }

                id[index] = eraId;
                name[index] = ReadString(era, "name") ?? eraId;
                nameKey[index] = "era." + eraId;

                if (!TryReadFloat(era, "yearsPerTick", out yearsPerTick[index]) || yearsPerTick[index] <= 0f)
                {
                    error = place + "нужно положительное yearsPerTick.";
                    return null;
                }

                if (!TryReadFloat(era, "foodPerWorker", out food[index]) || food[index] <= 0f)
                {
                    error = place + "нужно положительное foodPerWorker.";
                    return null;
                }

                if (!TryReadInt(era, "militaryPower", out military[index]))
                {
                    error = place + "нет поля militaryPower.";
                    return null;
                }

                // Грязь есть не у всех эпох: без поля считаем, что её нет.
                pollution[index] = TryReadInt(era, "pollution", out int dirt) ? dirt : 0;

                if (!era.TryGetProperty("requires", out JsonElement requires) || requires.ValueKind != JsonValueKind.Object)
                {
                    error = place + "нет раздела requires.";
                    return null;
                }

                if (!TryReadLong(requires, "minPop", out minPop[index]))
                {
                    error = place + "нет поля requires.minPop.";
                    return null;
                }

                if (!TryReadResources(requires, out resources[index], out string problem))
                {
                    error = place + problem;
                    return null;
                }

                if (!TryReadGeo(requires, out geoMask[index], out problem))
                {
                    error = place + problem;
                    return null;
                }

                string? prev = ReadString(requires, "prev");
                bool chained = index == 0
                    ? prev == null
                    : string.Equals(prev, id[index - 1], StringComparison.OrdinalIgnoreCase);
                if (!chained)
                {
                    error = place + "поле requires.prev должно указывать на предыдущую эпоху.";
                    return null;
                }

                index++;
            }

            if (!TryReadBlock(root, "diffusion", out JsonElement diffusionBlock)
                || !TryReadFloat(diffusionBlock, "tradePartner", out float tradePartner)
                || !TryReadFloat(diffusionBlock, "borderNeighbor", out float borderNeighbor)
                || !TryReadFloat(diffusionBlock, "conquered", out float conquered)
                || !TryReadFloat(diffusionBlock, "isolated", out float isolated))
            {
                error = "Раздел diffusion в data/eras.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (!TryReadBlock(root, "research", out JsonElement researchBlock)
                || !TryReadFloat(researchBlock, "baseCost", out float baseCost)
                || !TryReadFloat(researchBlock, "costPerEra", out float costPerEra)
                || !TryReadFloat(researchBlock, "settlementBonus", out float settlementBonus)
                || !TryReadFloat(researchBlock, "neighborBonus", out float neighborBonus)
                || !TryReadFloat(researchBlock, "isolatedPenalty", out float isolatedPenalty))
            {
                error = "Раздел research в data/eras.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (!TryReadBlock(root, "population", out JsonElement populationBlock)
                || !TryReadFloat(populationBlock, "perAgent", out float perAgent)
                || !TryReadFloat(populationBlock, "perAgentPerEra", out float perAgentPerEra)
                || perAgent <= 0f
                || perAgentPerEra <= 0f)
            {
                error = "Раздел population в data/eras.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (!TryReadBlock(root, "geo", out JsonElement geoBlock)
                || !TryReadFloat(geoBlock, "fertileValue", out float fertileValue)
                || !TryReadInt(geoBlock, "fertileTiles", out int fertileTiles)
                || !TryReadInt(geoBlock, "riverTiles", out int riverTiles)
                || !TryReadInt(geoBlock, "coastTiles", out int coastTiles)
                || !TryReadInt(geoBlock, "mountainTiles", out int mountainTiles))
            {
                error = "Раздел geo в data/eras.json отсутствует или заполнен не полностью.";
                return null;
            }

            if (!TryReadBlock(root, "collapse", out JsonElement collapseBlock)
                || !TryReadFloat(collapseBlock, "stabilityThreshold", out float stability)
                || !TryReadInt(collapseBlock, "erasLost", out int erasLost)
                || !TryReadFloat(collapseBlock, "darkAgeYears", out float darkAgeYears)
                || !TryReadFloat(collapseBlock, "popShareToHold", out float popShareToHold))
            {
                error = "Раздел collapse в data/eras.json отсутствует или заполнен не полностью.";
                return null;
            }

            return new EraTable(
                id,
                name,
                nameKey,
                yearsPerTick,
                minPop,
                resources,
                geoMask,
                food,
                military,
                pollution,
                new EraDiffusionSettings(tradePartner, borderNeighbor, conquered, isolated),
                new EraResearchSettings(baseCost, costPerEra, settlementBonus, neighborBonus, isolatedPenalty),
                new EraPopulationSettings(perAgent, perAgentPerEra),
                new EraGeoSettings(fertileValue, fertileTiles, riverTiles, coastTiles, mountainTiles),
                new EraCollapseSettings(stability, erasLost, darkAgeYears, popShareToHold));
        }
        catch (JsonException exception)
        {
            error = "data/eras.json не читается как JSON: " + exception.Message;
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

    private static bool TryReadLong(JsonElement element, string name, out long result)
    {
        if (element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out result))
        {
            return true;
        }

        result = 0L;
        return false;
    }

    private static bool TryReadResources(JsonElement requires, out int mask, out string problem)
    {
        mask = 0;
        problem = string.Empty;

        if (!requires.TryGetProperty("resources", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            problem = "нет массива requires.resources.";
            return false;
        }

        foreach (JsonElement item in list.EnumerateArray())
        {
            string? raw = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (raw == null)
            {
                problem = "в requires.resources должны быть строки.";
                return false;
            }

            ResourceKind kind = ResourceKinds.FromId(raw);
            if (kind == ResourceKind.None)
            {
                problem = "неизвестный ресурс " + raw + ".";
                return false;
            }

            mask |= ResourceBit(kind);
        }

        return true;
    }

    private static bool TryReadGeo(JsonElement requires, out byte mask, out string problem)
    {
        mask = 0;
        problem = string.Empty;

        if (!requires.TryGetProperty("geo", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            problem = "нет массива requires.geo.";
            return false;
        }

        foreach (JsonElement item in list.EnumerateArray())
        {
            string? raw = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            GeoFeature feature = raw switch
            {
                "fertile" => GeoFeature.Fertile,
                "river" => GeoFeature.River,
                "coast" => GeoFeature.Coast,
                "mountain" => GeoFeature.Mountain,
                _ => GeoFeature.None,
            };

            if (feature == GeoFeature.None)
            {
                problem = "неизвестное требование к земле в requires.geo.";
                return false;
            }

            mask |= (byte)feature;
        }

        return true;
    }
}
