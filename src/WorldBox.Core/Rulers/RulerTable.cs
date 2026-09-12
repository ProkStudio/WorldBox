using System.Text.Json;
using WorldBox.Core.Data;

namespace WorldBox.Core.Rulers;

/// <summary>
/// Таблица людей истории: data/rulers.json. Сколько живут правители, часто ли рождаются
/// наследники, насколько опасен переворот и что дают черты — всё меняется без пересборки.
///
/// Ошибка в файле не роняет игру: загрузка возвращает null и понятную русскую строку,
/// а партия идёт без правителей, как и без общества или войны.
/// </summary>
public sealed class RulerTable
{
    public const string FileName = "rulers.json";

    /// <summary>Какую версию файла понимает этот код.</summary>
    public const int SupportedVersion = 1;

    private RulerTable(
        int maxRulers,
        RulerLifeSettings life,
        RulerHeirSettings heirs,
        RulerTraitSettings traits,
        RulerHeroSettings heroes)
    {
        MaxRulers = maxRulers;
        Life = life;
        Heirs = heirs;
        TraitRules = traits;
        Heroes = heroes;
    }

    /// <summary>Сколько записей о людях держать в памяти: старые мёртвые вытесняются.</summary>
    public int MaxRulers { get; }

    public RulerLifeSettings Life { get; }

    public RulerHeirSettings Heirs { get; }

    /// <summary>Сила черт. Названо не Traits, чтобы не путать со списком черт <see cref="Rulers.Traits"/>.</summary>
    public RulerTraitSettings TraitRules { get; }

    public RulerHeroSettings Heroes { get; }

    /// <summary>Читает таблицу с диска. При ошибке возвращает null и пишет причину в error.</summary>
    public static RulerTable? Load(out string error)
    {
        string? path = DataPaths.Find(FileName);
        if (path == null)
        {
            error = "Файл data/" + FileName + " не найден.";
            return null;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
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

        return Parse(json, out error);
    }

    /// <summary>Разбирает таблицу из строки. Выделен отдельно ради тестов.</summary>
    public static RulerTable? Parse(string json, out string error)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "data/" + FileName + " должен быть объектом JSON.";
                return null;
            }

            if (!TryReadInt(root, "version", out int version) || version != SupportedVersion)
            {
                error = "В data/" + FileName + " ожидается version " + SupportedVersion + ".";
                return null;
            }

            if (!TryReadInt(root, "maxRulers", out int maxRulers) || maxRulers < 16 || maxRulers > 8192)
            {
                error = "В data/" + FileName + " поле maxRulers должно быть от 16 до 8192.";
                return null;
            }

            RulerLifeSettings? life = ReadLife(root, out error);
            if (life == null)
            {
                return null;
            }

            RulerHeirSettings? heirs = ReadHeirs(root, out error);
            if (heirs == null)
            {
                return null;
            }

            RulerTraitSettings? traits = ReadTraits(root, out error);
            if (traits == null)
            {
                return null;
            }

            RulerHeroSettings? heroes = ReadHeroes(root, out error);
            if (heroes == null)
            {
                return null;
            }

            error = string.Empty;
            return new RulerTable(maxRulers, life, heirs, traits, heroes);
        }
        catch (JsonException exception)
        {
            error = "data/" + FileName + " не разбирается: " + exception.Message;
            return null;
        }
    }

    private static RulerLifeSettings? ReadLife(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "life", out JsonElement block, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "minLifeYears", out int minLife) || minLife < 5)
        {
            error = "В разделе life поле minLifeYears должно быть не меньше пяти.";
            return null;
        }

        if (!TryReadInt(block, "maxLifeYears", out int maxLife) || maxLife < minLife)
        {
            error = "В разделе life поле maxLifeYears не может быть меньше minLifeYears.";
            return null;
        }

        if (!TryReadInt(block, "adultYears", out int adult) || adult < 1 || adult >= minLife)
        {
            error = "В разделе life поле adultYears должно быть от единицы до minLifeYears.";
            return null;
        }

        if (!TryReadShare(block, "violentDeathChance", out float violent, out error)
            || !TryReadShare(block, "unrestDeathWeight", out float unrestWeight, out error))
        {
            return null;
        }

        error = string.Empty;
        return new RulerLifeSettings(minLife, maxLife, adult, violent, unrestWeight);
    }

    private static RulerHeirSettings? ReadHeirs(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "heirs", out JsonElement block, out error))
        {
            return null;
        }

        if (!TryReadShare(block, "heirChancePerRun", out float heirChance, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "maxChildren", out int maxChildren) || maxChildren < 1 || maxChildren > 16)
        {
            error = "В разделе heirs поле maxChildren должно быть от 1 до 16.";
            return null;
        }

        if (!TryReadShare(block, "coupChance", out float coup, out error)
            || !TryReadShare(block, "crisisUnrest", out float crisisUnrest, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "crisisRuns", out int crisisRuns) || crisisRuns < 0 || crisisRuns > 100)
        {
            error = "В разделе heirs поле crisisRuns должно быть от 0 до 100.";
            return null;
        }

        error = string.Empty;
        return new RulerHeirSettings(heirChance, maxChildren, coup, crisisUnrest, crisisRuns);
    }

    private static RulerTraitSettings? ReadTraits(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "traits", out JsonElement block, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "minTraits", out int minTraits) || minTraits < 0 || minTraits > Rulers.Traits.Count)
        {
            error = "В разделе traits поле minTraits должно быть от 0 до " + Rulers.Traits.Count + ".";
            return null;
        }

        if (!TryReadInt(block, "maxTraits", out int maxTraits) || maxTraits < minTraits || maxTraits > Rulers.Traits.Count)
        {
            error = "В разделе traits поле maxTraits должно быть от minTraits до " + Rulers.Traits.Count + ".";
            return null;
        }

        if (!TryReadShare(block, "cruelUnrest", out float cruelUnrest, out error)
            || !TryReadShare(block, "justUnrest", out float justUnrest, out error))
        {
            return null;
        }

        if (!TryReadAmount(block, "builderFood", out float builderFood, out error)
            || !TryReadAmount(block, "scholarProgress", out float scholarProgress, out error))
        {
            return null;
        }

        if (!TryReadShare(block, "conquerorAggression", out float aggression, out error)
            || !TryReadShare(block, "piousStability", out float piousStability, out error)
            || !TryReadShare(block, "cruelStability", out float cruelStability, out error))
        {
            return null;
        }

        error = string.Empty;
        return new RulerTraitSettings(
            minTraits,
            maxTraits,
            cruelUnrest,
            justUnrest,
            builderFood,
            scholarProgress,
            aggression,
            piousStability,
            cruelStability);
    }

    private static RulerHeroSettings? ReadHeroes(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "heroes", out JsonElement block, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "maxHeroes", out int maxHeroes) || maxHeroes < 0 || maxHeroes > 256)
        {
            error = "В разделе heroes поле maxHeroes должно быть от 0 до 256.";
            return null;
        }

        if (!TryReadShare(block, "chancePerRun", out float chance, out error))
        {
            return null;
        }

        if (!TryReadInt(block, "lifeRuns", out int lifeRuns) || lifeRuns < 1 || lifeRuns > 1000)
        {
            error = "В разделе heroes поле lifeRuns должно быть от 1 до 1000.";
            return null;
        }

        if (!TryReadShare(block, "commanderAggression", out float commander, out error)
            || !TryReadShare(block, "prophetUnrest", out float prophet, out error))
        {
            return null;
        }

        if (!TryReadAmount(block, "inventorProgress", out float inventor, out error)
            || !TryReadAmount(block, "explorerFood", out float explorer, out error))
        {
            return null;
        }

        error = string.Empty;
        return new RulerHeroSettings(maxHeroes, chance, lifeRuns, commander, inventor, prophet, explorer);
    }

    private static bool TryReadBlock(JsonElement root, string name, out JsonElement block, out string error)
    {
        if (!root.TryGetProperty(name, out block) || block.ValueKind != JsonValueKind.Object)
        {
            error = "В data/" + FileName + " нет раздела " + name + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement field)
            && field.ValueKind == JsonValueKind.Number
            && field.TryGetInt32(out value);
    }

    /// <summary>Доля от нуля до единицы: вероятности и надбавки больше ста процентов бессмысленны.</summary>
    private static bool TryReadShare(JsonElement element, string name, out float value, out string error)
    {
        if (!TryReadFloat(element, name, out value) || value < 0f || value > 1f)
        {
            error = "Поле " + name + " в data/" + FileName + " должно быть от 0 до 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>Прибавка в единицах еды или знания: тут единица не потолок.</summary>
    private static bool TryReadAmount(JsonElement element, string name, out float value, out string error)
    {
        if (!TryReadFloat(element, name, out value) || value < 0f || value > 1000f)
        {
            error = "Поле " + name + " в data/" + FileName + " должно быть от 0 до 1000.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadFloat(JsonElement element, string name, out float value)
    {
        value = 0f;
        if (!element.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!field.TryGetDouble(out double raw))
        {
            return false;
        }

        value = (float)raw;
        return true;
    }
}
