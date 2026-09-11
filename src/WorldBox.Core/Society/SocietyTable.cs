using System.Text.Json;
using WorldBox.Core.Data;

namespace WorldBox.Core.Society;

/// <summary>
/// Таблица общества: data/society.json. Всё, что можно крутить без пересборки игры:
/// с какой вероятностью рождаются веры, как быстро ассимилируется чужой народ,
/// какие формы власти доступны в какую эпоху и из чего складывается стабильность.
///
/// Ошибки таблицы не роняют игру: загрузка возвращает null и понятную русскую строку,
/// а симуляция живёт без общества, как и без хозяйства или войны.
/// </summary>
public sealed class SocietyTable
{
    public const string FileName = "society.json";

    /// <summary>Какую версию файла понимает этот код.</summary>
    public const int SupportedVersion = 1;

    private SocietyTable(
        SocietyReligionSettings religion,
        SocietyCultureSettings culture,
        SocietyIdeologySettings ideology,
        SocietyStabilitySettings stability,
        Forms forms)
    {
        Religion = religion;
        Culture = culture;
        Ideology = ideology;
        Stability = stability;

        FormId = forms.Id;
        FormMinEra = forms.MinEra;
        FormMaxEra = forms.MaxEra;
        FormStability = forms.Stability;
        FormResearch = forms.Research;
        FormAggression = forms.Aggression;
        FormTaxes = forms.Taxes;

        FormNameKey = new string[forms.Id.Length];
        for (int i = 0; i < forms.Id.Length; i++)
        {
            FormNameKey[i] = "ideology." + forms.Id[i];
        }
    }

    public SocietyReligionSettings Religion { get; }

    public SocietyCultureSettings Culture { get; }

    public SocietyIdeologySettings Ideology { get; }

    public SocietyStabilitySettings Stability { get; }

    /// <summary>Кодовые имена форм власти: tribe, monarchy, republic и так далее.</summary>
    public string[] FormId { get; }

    /// <summary>Готовые ключи перевода: собраны при загрузке, чтобы не склеивать строки каждый кадр.</summary>
    public string[] FormNameKey { get; }

    /// <summary>С какой эпохи форма власти вообще возможна.</summary>
    public int[] FormMinEra { get; }

    /// <summary>До какой эпохи форма власти считается своевременной.</summary>
    public int[] FormMaxEra { get; }

    /// <summary>Надбавка к стабильности от формы власти.</summary>
    public float[] FormStability { get; }

    /// <summary>Надбавка к скорости науки.</summary>
    public float[] FormResearch { get; }

    /// <summary>Готовность воевать от 0 до 1.</summary>
    public float[] FormAggression { get; }

    /// <summary>Тяжесть налогов от 0 до 1.</summary>
    public float[] FormTaxes { get; }

    public int FormCount => FormId.Length;

    /// <summary>Ключ перевода формы власти или ideology.none, если власти нет.</summary>
    public string NameKeyOf(int form)
        => (uint)form < (uint)FormCount ? FormNameKey[form] : "ideology.none";

    /// <summary>Доступна ли форма власти в этой эпохе.</summary>
    public bool Allows(int form, int era)
        => (uint)form < (uint)FormCount && era >= FormMinEra[form];

    /// <summary>
    /// Насколько форма власти соответствует эпохе: единица в своё время,
    /// меньше — если племенной совет дожил до паровых машин или республика появилась слишком рано.
    /// </summary>
    public float EraFit(int form, int era)
    {
        if ((uint)form >= (uint)FormCount)
        {
            return 0f;
        }

        if (era < FormMinEra[form])
        {
            return MathF.Max(0f, 1f - ((FormMinEra[form] - era) * 0.35f));
        }

        if (era > FormMaxEra[form])
        {
            return MathF.Max(0f, 1f - ((era - FormMaxEra[form]) * 0.25f));
        }

        return 1f;
    }

    /// <summary>Номер формы власти по кодовому имени или -1.</summary>
    public int Find(string id)
    {
        for (int i = 0; i < FormId.Length; i++)
        {
            if (string.Equals(FormId[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Читает таблицу с диска. При ошибке возвращает null и пишет причину в error.</summary>
    public static SocietyTable? Load(out string error)
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
    public static SocietyTable? Parse(string json, out string error)
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

            SocietyReligionSettings? religion = ReadReligion(root, out error);
            if (religion == null)
            {
                return null;
            }

            SocietyCultureSettings? culture = ReadCulture(root, out error);
            if (culture == null)
            {
                return null;
            }

            SocietyIdeologySettings? ideology = ReadIdeology(root, out error);
            if (ideology == null)
            {
                return null;
            }

            SocietyStabilitySettings? stability = ReadStability(root, out error);
            if (stability == null)
            {
                return null;
            }

            Forms? forms = ReadForms(root, out error);
            if (forms == null)
            {
                return null;
            }

            error = string.Empty;
            return new SocietyTable(religion, culture, ideology, stability, forms);
        }
        catch (JsonException exception)
        {
            error = "data/" + FileName + " не читается как JSON: " + exception.Message;
            return null;
        }
    }

    private static SocietyReligionSettings? ReadReligion(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "religion", out JsonElement block))
        {
            error = "Раздел religion в data/" + FileName + " отсутствует.";
            return null;
        }

        if (!TryReadInt(block, "maxReligions", out int maxReligions) || maxReligions <= 0)
        {
            error = "В разделе religion нужен положительный maxReligions.";
            return null;
        }

        if (!TryReadInt(block, "minPeople", out int minPeople) || minPeople <= 0)
        {
            error = "В разделе religion нужен положительный minPeople.";
            return null;
        }

        if (!TryReadInt(block, "minEra", out int minEra) || minEra < 0)
        {
            error = "В разделе religion поле minEra не может быть отрицательным.";
            return null;
        }

        if (!TryReadInt(block, "youngRuns", out int youngRuns) || youngRuns < 0)
        {
            error = "В разделе religion поле youngRuns не может быть отрицательным.";
            return null;
        }

        if (!TryReadInt(block, "schismMinFollowers", out int schismMinFollowers) || schismMinFollowers <= 0)
        {
            error = "В разделе religion нужен положительный schismMinFollowers.";
            return null;
        }

        if (!TryReadShare(block, "birthChancePerRun", out float birth, out error)
            || !TryReadShare(block, "crisisBonus", out float crisis, out error)
            || !TryReadShare(block, "neighbourSpreadChance", out float neighbour, out error)
            || !TryReadShare(block, "tradeSpreadChance", out float trade, out error)
            || !TryReadShare(block, "conquestSpreadChance", out float conquest, out error)
            || !TryReadShare(block, "schismChancePerRun", out float schism, out error)
            || !TryReadShare(block, "schismUnrest", out float schismUnrest, out error)
            || !TryReadShare(block, "foreignFaithUnrest", out float foreignFaith, out error))
        {
            return null;
        }

        error = string.Empty;
        return new SocietyReligionSettings(
            maxReligions,
            minPeople,
            minEra,
            youngRuns,
            birth,
            crisis,
            neighbour,
            trade,
            conquest,
            schismMinFollowers,
            schism,
            schismUnrest,
            foreignFaith);
    }

    private static SocietyCultureSettings? ReadCulture(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "culture", out JsonElement block))
        {
            error = "Раздел culture в data/" + FileName + " отсутствует.";
            return null;
        }

        if (!TryReadInt(block, "maxCultures", out int maxCultures) || maxCultures <= 0)
        {
            error = "В разделе culture нужен положительный maxCultures.";
            return null;
        }

        if (!TryReadInt(block, "languages", out int languages) || languages <= 0 || languages > SocietyNames.Languages)
        {
            error = "В разделе culture поле languages должно быть от 1 до " + SocietyNames.Languages + ".";
            return null;
        }

        if (!TryReadShare(block, "assimilationChance", out float assimilation, out error)
            || !TryReadShare(block, "assimilationPerEra", out float perEra, out error)
            || !TryReadShare(block, "foreignUnrest", out float foreignUnrest, out error)
            || !TryReadShare(block, "absorbShare", out float absorb, out error))
        {
            return null;
        }

        error = string.Empty;
        return new SocietyCultureSettings(maxCultures, assimilation, perEra, foreignUnrest, absorb, languages);
    }

    private static SocietyIdeologySettings? ReadIdeology(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "ideology", out JsonElement block))
        {
            error = "Раздел ideology в data/" + FileName + " отсутствует.";
            return null;
        }

        if (!TryReadInt(block, "changeRuns", out int changeRuns) || changeRuns <= 0)
        {
            error = "В разделе ideology нужен положительный changeRuns.";
            return null;
        }

        if (!TryReadShare(block, "switchChance", out float switchChance, out error))
        {
            return null;
        }

        error = string.Empty;
        return new SocietyIdeologySettings(changeRuns, switchChance);
    }

    private static SocietyStabilitySettings? ReadStability(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "stability", out JsonElement block))
        {
            error = "Раздел stability в data/" + FileName + " отсутствует.";
            return null;
        }

        if (!TryReadInt(block, "collapseRuns", out int collapseRuns) || collapseRuns <= 0)
        {
            error = "В разделе stability нужен положительный collapseRuns.";
            return null;
        }

        if (!TryReadShare(block, "base", out float baseValue, out error)
            || !TryReadShare(block, "hungerWeight", out float hunger, out error)
            || !TryReadShare(block, "unityWeight", out float unity, out error)
            || !TryReadShare(block, "faithWeight", out float faith, out error)
            || !TryReadShare(block, "eraFitWeight", out float eraFit, out error)
            || !TryReadShare(block, "warWeight", out float war, out error)
            || !TryReadShare(block, "unrestWeight", out float unrestWeight, out error)
            || !TryReadShare(block, "unrestPerPoint", out float unrestPerPoint, out error)
            || !TryReadShare(block, "collapseThreshold", out float collapseThreshold, out error)
            || !TryReadShare(block, "collapseUnrest", out float collapseUnrest, out error))
        {
            return null;
        }

        error = string.Empty;
        return new SocietyStabilitySettings(
            baseValue,
            hunger,
            unity,
            faith,
            eraFit,
            war,
            unrestWeight,
            unrestPerPoint,
            collapseThreshold,
            collapseRuns,
            collapseUnrest);
    }

    private static Forms? ReadForms(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "ideology", out JsonElement block)
            || !block.TryGetProperty("forms", out JsonElement list)
            || list.ValueKind != JsonValueKind.Array)
        {
            error = "Раздел ideology.forms в data/" + FileName + " отсутствует.";
            return null;
        }

        int count = list.GetArrayLength();
        if (count <= 0)
        {
            error = "В разделе ideology.forms нужна хотя бы одна форма власти.";
            return null;
        }

        string[] id = new string[count];
        int[] minEra = new int[count];
        int[] maxEra = new int[count];
        float[] stability = new float[count];
        float[] research = new float[count];
        float[] aggression = new float[count];
        float[] taxes = new float[count];

        int index = 0;
        bool hasStartForm = false;
        foreach (JsonElement item in list.EnumerateArray())
        {
            string prefix = "Форма власти номер " + (index + 1) + ": ";
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = prefix + "ожидается объект JSON.";
                return null;
            }

            string? formId = ReadString(item, "id");
            if (formId == null)
            {
                error = prefix + "нужен непустой id.";
                return null;
            }

            for (int i = 0; i < index; i++)
            {
                if (string.Equals(id[i], formId, StringComparison.Ordinal))
                {
                    error = prefix + "id \"" + formId + "\" уже занят.";
                    return null;
                }
            }

            if (!TryReadInt(item, "minEra", out int formMinEra) || formMinEra < 0)
            {
                error = prefix + "minEra не может быть отрицательным.";
                return null;
            }

            if (!TryReadInt(item, "maxEra", out int formMaxEra) || formMaxEra < formMinEra)
            {
                error = prefix + "maxEra должен быть не меньше minEra.";
                return null;
            }

            if (!TryReadShareWithPrefix(item, "stability", prefix, out stability[index], out error)
                || !TryReadShareWithPrefix(item, "research", prefix, out research[index], out error)
                || !TryReadShareWithPrefix(item, "aggression", prefix, out aggression[index], out error)
                || !TryReadShareWithPrefix(item, "taxes", prefix, out taxes[index], out error))
            {
                return null;
            }

            id[index] = formId;
            minEra[index] = formMinEra;
            maxEra[index] = formMaxEra;
            hasStartForm |= formMinEra == 0;
            index++;
        }

        if (!hasStartForm)
        {
            error = "В разделе ideology.forms нужна форма власти с minEra 0: молодым народам тоже нужна власть.";
            return null;
        }

        error = string.Empty;
        return new Forms(id, minEra, maxEra, stability, research, aggression, taxes);
    }

    private static bool TryReadBlock(JsonElement root, string name, out JsonElement block)
    {
        if (root.TryGetProperty(name, out block) && block.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        block = default;
        return false;
    }

    private static bool TryReadInt(JsonElement block, string name, out int value)
    {
        value = 0;
        return block.TryGetProperty(name, out JsonElement field)
            && field.ValueKind == JsonValueKind.Number
            && field.TryGetInt32(out value);
    }

    private static bool TryReadFloat(JsonElement block, string name, out float value)
    {
        value = 0f;
        if (!block.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.Number)
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

    /// <summary>Доля от 0 до 1: таких полей в таблице больше всего, поэтому проверка общая.</summary>
    private static bool TryReadShare(JsonElement block, string name, out float value, out string error)
    {
        if (!TryReadFloat(block, name, out value) || value < 0f || value > 1f)
        {
            value = 0f;
            error = "Поле " + name + " в data/" + FileName + " должно быть от 0 до 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadShareWithPrefix(JsonElement block, string name, string prefix, out float value, out string error)
    {
        if (!TryReadFloat(block, name, out value) || value < 0f || value > 1f)
        {
            value = 0f;
            error = prefix + "поле " + name + " должно быть от 0 до 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string? ReadString(JsonElement block, string name)
    {
        if (!block.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = field.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private sealed record Forms(
        string[] Id,
        int[] MinEra,
        int[] MaxEra,
        float[] Stability,
        float[] Research,
        float[] Aggression,
        float[] Taxes);
}
