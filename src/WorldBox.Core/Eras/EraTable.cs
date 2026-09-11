using System.Text.Json;
using WorldBox.Core.Data;
using WorldBox.Core.World;

namespace WorldBox.Core.Eras;

/// <summary>Какая земля нужна эпохе. Считается по своим тайлам народа.</summary>
[Flags]
public enum GeoFeature : byte
{
    None = 0,
    Fertile = 1,
    River = 2,
    Coast = 4,
    Mountain = 8,
}

/// <summary>Шансы перенять эпоху у соседа. Числа приходят из data/eras.json.</summary>
public sealed class EraDiffusionSettings
{
    public EraDiffusionSettings(float tradePartner, float borderNeighbor, float conquered, float isolated)
    {
        TradePartner = tradePartner;
        BorderNeighbor = borderNeighbor;
        Conquered = conquered;
        Isolated = isolated;
    }

    /// <summary>Сосед, который делится нужным ресурсом, учит быстрее всех.</summary>
    public float TradePartner { get; }

    /// <summary>Обычный сосед по границе.</summary>
    public float BorderNeighbor { get; }

    /// <summary>Захваченный народ. Пригодится на срезе войн, сейчас только читается из файла.</summary>
    public float Conquered { get; }

    /// <summary>Народ без соседей: перенимать не у кого.</summary>
    public float Isolated { get; }
}

/// <summary>Сколько знания нужно на шаг вперёд и что его ускоряет.</summary>
public sealed class EraResearchSettings
{
    public EraResearchSettings(
        float baseCost,
        float costPerEra,
        float settlementBonus,
        float neighborBonus,
        float isolatedPenalty)
    {
        BaseCost = baseCost;
        CostPerEra = costPerEra;
        SettlementBonus = settlementBonus;
        NeighborBonus = neighborBonus;
        IsolatedPenalty = isolatedPenalty;
    }

    /// <summary>Цена первого шага в очках знания.</summary>
    public float BaseCost { get; }

    /// <summary>Насколько дороже каждая следующая эпоха.</summary>
    public float CostPerEra { get; }

    /// <summary>Прибавка за каждое поселение народа.</summary>
    public float SettlementBonus { get; }

    /// <summary>Прибавка за каждого соседа, который уже в этой эпохе.</summary>
    public float NeighborBonus { get; }

    /// <summary>Множитель для народа без соседей: в одиночку думается медленнее.</summary>
    public float IsolatedPenalty { get; }
}

/// <summary>Перевод человечков на карте в население в цифрах.</summary>
public sealed class EraPopulationSettings
{
    public EraPopulationSettings(float perAgent, float perAgentPerEra)
    {
        PerAgent = perAgent;
        PerAgentPerEra = perAgentPerEra;
    }

    /// <summary>Сколько людей изображает один человечек в нулевой эпохе.</summary>
    public float PerAgent { get; }

    /// <summary>Во сколько раз это число растёт с каждой эпохой.</summary>
    public float PerAgentPerEra { get; }
}

/// <summary>Когда земля считается плодородной, речной, морской или горной.</summary>
public sealed class EraGeoSettings
{
    public EraGeoSettings(float fertileValue, int fertileTiles, int riverTiles, int coastTiles, int mountainTiles)
    {
        FertileValue = fertileValue;
        FertileTiles = fertileTiles;
        RiverTiles = riverTiles;
        CoastTiles = coastTiles;
        MountainTiles = mountainTiles;
    }

    /// <summary>С какого плодородия тайл считается пашней.</summary>
    public float FertileValue { get; }

    public int FertileTiles { get; }

    public int RiverTiles { get; }

    public int CoastTiles { get; }

    public int MountainTiles { get; }
}

/// <summary>Откат назад, когда народ не удержал свою эпоху.</summary>
public sealed class EraCollapseSettings
{
    public EraCollapseSettings(float stabilityThreshold, int erasLost, float darkAgeYears, float popShareToHold)
    {
        StabilityThreshold = stabilityThreshold;
        ErasLost = erasLost;
        DarkAgeYears = darkAgeYears;
        PopShareToHold = popShareToHold;
    }

    /// <summary>Порог стабильности. Появится на срезе политики, пока только читается.</summary>
    public float StabilityThreshold { get; }

    /// <summary>Сколько эпох теряется при откате.</summary>
    public int ErasLost { get; }

    /// <summary>Сколько игровых лет народ после откола не развивается.</summary>
    public float DarkAgeYears { get; }

    /// <summary>Какую долю от порога своей эпохи надо удерживать по населению.</summary>
    public float PopShareToHold { get; }
}

/// <summary>
/// Таблица эпох из data/eras.json: пороги перехода, сжатие времени, еда и сила армии.
/// Файл читается один раз при старте, в тиках обращений к диску нет.
/// Числа баланса живут только в файле: в коде нет ни одного значения по умолчанию,
/// поэтому опечатка в данных видна сразу, а не растворяется в коде.
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

    /// <summary>Название из файла. В интерфейсе показывается строка по ключу, это резерв для отладки.</summary>
    public string[] Name { get; }

    /// <summary>Ключи названий для data/strings.ru.json: era.paleolithic и так далее.</summary>
    public string[] NameKey { get; }

    /// <summary>Сколько игровых лет в тике, когда мир дошёл до этой эпохи.</summary>
    public float[] YearsPerTick { get; }

    /// <summary>Сколько людей нужно, чтобы войти в эпоху.</summary>
    public long[] MinPop { get; }

    /// <summary>Нужные ресурсы битовой маской по ResourceKind.</summary>
    public int[] RequiredResources { get; }

    /// <summary>Нужная география битовой маской по GeoFeature.</summary>
    public byte[] RequiredGeo { get; }

    public float[] FoodPerWorker { get; }

    public int[] MilitaryPower { get; }

    public int[] Pollution { get; }

    public EraDiffusionSettings Diffusion { get; }

    public EraResearchSettings Research { get; }

    public EraPopulationSettings Population { get; }

    public EraGeoSettings Geo { get; }

    public EraCollapseSettings Collapse { get; }

    public static int ResourceBit(ResourceKind kind) => 1 << (int)kind;

    /// <summary>Ключ названия эпохи. Номер за границами таблицы прижимается к краю.</summary>
    public string NameKeyOf(int era) => NameKey[Math.Clamp(era, 0, Last)];

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

    /// <summary>Читает таблицу из папки data. Возвращает null и текст ошибки, если не получилось.</summary>
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
        error =