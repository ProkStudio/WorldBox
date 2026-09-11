using System.Text.Json;
using WorldBox.Core.Data;

namespace WorldBox.Core.War;

/// <summary>
/// Таблица войны из data/war.json: армии, бой, осада, причины войн и восстания.
/// Числа лежат в данных, а не в коде: баланс войны правят файлом, без пересборки.
/// Ошибки разбора возвращаются строкой на русском — их видно в игре.
/// </summary>
public sealed class WarTable
{
    /// <summary>Имя файла с настройками войны.</summary>
    public const string FileName = "war.json";

    private WarTable(
        ArmySettings armies,
        BattleSettings battle,
        SiegeSettings siege,
        WarCauseSettings war,
        RevoltSettings revolt)
    {
        Armies = armies;
        Battle = battle;
        Siege = siege;
        War = war;
        Revolt = revolt;
    }

    /// <summary>Сколько отрядов народ выставляет и чем за них платит.</summary>
    public ArmySettings Armies { get; }

    /// <summary>Из чего складывается сила отряда и чем кончается встреча.</summary>
    public BattleSettings Battle { get; }

    /// <summary>Осада города и что бывает после взятия.</summary>
    public SiegeSettings Siege { get; }

    /// <summary>Причины войн и условия мира.</summary>
    public WarCauseSettings War { get; }

    /// <summary>Восстания на захваченных землях.</summary>
    public RevoltSettings Revolt { get; }

    /// <summary>Читает таблицу с диска. Возвращает null и текст ошибки, если файл плохой.</summary>
    public static WarTable? Load(out string error)
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
    public static WarTable? Parse(string json, out string error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            ArmySettings? armies = ReadArmies(root, out error);
            if (armies == null)
            {
                return null;
            }

            BattleSettings? battle = ReadBattle(root, out error);
            if (battle == null)
            {
                return null;
            }

            SiegeSettings? siege = ReadSiege(root, out error);
            if (siege == null)
            {
                return null;
            }

            WarCauseSettings? war = ReadWar(root, out error);
            if (war == null)
            {
                return null;
            }

            RevoltSettings? revolt = ReadRevolt(root, out error);
            if (revolt == null)
            {
                return null;
            }

            error = string.Empty;
            return new WarTable(armies, battle, siege, war, revolt);
        }
        catch (JsonException exception)
        {
            error = "data/" + FileName + " не читается как JSON: " + exception.Message;
            return null;
        }
    }

    private static ArmySettings? ReadArmies(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "armies", out JsonElement block)
            || !TryReadInt(block, "maxArmies", out int maxArmies)
            || !TryReadInt(block, "menPerArmy", out int menPerArmy)
            || !TryReadFloat(block, "menPerThousandPeople", out float menPerThousandPeople)
            || !TryReadFloat(block, "goldPerMan", out float goldPerMan)
            || !TryReadFloat(block, "upkeepPerMan", out float upkeepPerMan)
            || !TryReadInt(block, "marchTilesPerRun", out int marchTilesPerRun)
            || !TryReadInt(block, "retreatTiles", out int retreatTiles)
            || !TryReadFloat(block, "moraleStart", out float moraleStart)
            || !TryReadFloat(block, "moraleRecovery", out float moraleRecovery)
            || !TryReadFloat(block, "supplyLossPerRun", out float supplyLossPerRun)
            || !TryReadFloat(block, "starveLossShare", out float starveLossShare)
            || !TryReadFloat(block, "starveMoraleDrop", out float starveMoraleDrop)
            || !TryReadInt(block, "disbandMen", out int disbandMen))
        {
            error = "Раздел armies в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (maxArmies <= 0 || menPerArmy <= 0)
        {
            error = "В разделе armies нужны положительные maxArmies и menPerArmy.";
            return null;
        }

        if (menPerThousandPeople < 0f || goldPerMan < 0f || upkeepPerMan < 0f)
        {
            error = "В разделе armies набор и содержание войска не могут быть отрицательными.";
            return null;
        }

        if (marchTilesPerRun <= 0)
        {
            error = "В разделе armies нужен положительный marchTilesPerRun, иначе войско не дойдёт до врага.";
            return null;
        }

        if (retreatTiles < 0 || disbandMen < 0)
        {
            error = "В разделе armies retreatTiles и disbandMen не могут быть отрицательными.";
            return null;
        }

        if (moraleStart <= 0f || moraleStart > 1f)
        {
            error = "В разделе armies moraleStart должен быть больше 0 и не больше 1.";
            return null;
        }

        if (moraleRecovery < 0f || supplyLossPerRun < 0f || starveMoraleDrop < 0f)
        {
            error = "В разделе armies мораль и снабжение меняются на неотрицательную величину.";
            return null;
        }

        if (starveLossShare < 0f || starveLossShare > 1f)
        {
            error = "В разделе armies starveLossShare должен быть от 0 до 1.";
            return null;
        }

        error = string.Empty;
        return new ArmySettings(
            maxArmies,
            menPerArmy,
            menPerThousandPeople,
            goldPerMan,
            upkeepPerMan,
            marchTilesPerRun,
            retreatTiles,
            moraleStart,
            moraleRecovery,
            supplyLossPerRun,
            starveLossShare,
            starveMoraleDrop,
            disbandMen);
    }

    private static BattleSettings? ReadBattle(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "battle", out JsonElement block)
            || !TryReadFloat(block, "eraPowerWeight", out float eraPowerWeight)
            || !TryReadFloat(block, "moraleWeight", out float moraleWeight)
            || !TryReadFloat(block, "terrainDefense", out float terrainDefense)
            || !TryReadFloat(block, "supplyFloor", out float supplyFloor)
            || !TryReadFloat(block, "lossShareWinner", out float lossShareWinner)
            || !TryReadFloat(block, "lossShareLoser", out float lossShareLoser)
            || !TryReadFloat(block, "moraleHitLoser", out float moraleHitLoser)
            || !TryReadFloat(block, "moraleGainWinner", out float moraleGainWinner)
            || !TryReadFloat(block, "retreatMorale", out float retreatMorale)
            || !TryReadFloat(block, "civiliansPerSoldier", out float civiliansPerSoldier)
            || !TryReadInt(block, "lossRadius", out int lossRadius))
        {
            error = "Раздел battle в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (eraPowerWeight < 0f || moraleWeight < 0f || terrainDefense < 0f || civiliansPerSoldier < 0f)
        {
            error = "В разделе battle прибавки к силе не могут быть отрицательными.";
            return null;
        }

        if (supplyFloor <= 0f || supplyFloor > 1f)
        {
            error = "В разделе battle supplyFloor должен быть больше 0 и не больше 1.";
            return null;
        }

        if (lossShareWinner < 0f || lossShareWinner > 1f || lossShareLoser < 0f || lossShareLoser > 1f)
        {
            error = "В разделе battle доли потерь должны быть от 0 до 1.";
            return null;
        }

        if (lossShareLoser < lossShareWinner)
        {
            error = "В разделе battle проигравший должен терять не меньше победителя.";
            return null;
        }

        if (moraleHitLoser < 0f || moraleGainWinner < 0f)
        {
            error = "В разделе battle мораль меняется на неотрицательную величину.";
            return null;
        }

        if (retreatMorale < 0f || retreatMorale > 1f)
        {
            error = "В разделе battle retreatMorale должен быть от 0 до 1.";
            return null;
        }

        if (lossRadius < 0)
        {
            error = "В разделе battle lossRadius не может быть отрицательным.";
            return null;
        }

        error = string.Empty;
        return new BattleSettings(
            eraPowerWeight,
            moraleWeight,
            terrainDefense,
            supplyFloor,
            lossShareWinner,
            lossShareLoser,
            moraleHitLoser,
            moraleGainWinner,
            retreatMorale,
            civiliansPerSoldier,
            lossRadius);
    }

    private static SiegeSettings? ReadSiege(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "siege", out JsonElement block)
            || !TryReadFloat(block, "progressPerRun", out float progressPerRun)
            || !TryReadFloat(block, "reliefPerRun", out float reliefPerRun)
            || !TryReadFloat(block, "wallsPerLevel", out float wallsPerLevel)
            || !TryReadInt(block, "menForFullProgress", out int menForFullProgress)
            || !TryReadFloat(block, "starveMinProgress", out float starveMinProgress)
            || !TryReadFloat(block, "starvePerPerson", out float starvePerPerson)
            || !TryReadFloat(block, "sackShare", out float sackShare)
            || !TryReadInt(block, "annexRadius", out int annexRadius))
        {
            error = "Раздел siege в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (progressPerRun <= 0f)
        {
            error = "В разделе siege нужен положительный progressPerRun, иначе осада никогда не кончится.";
            return null;
        }

        if (reliefPerRun < 0f || wallsPerLevel < 0f || starvePerPerson < 0f)
        {
            error = "В разделе siege reliefPerRun, wallsPerLevel и starvePerPerson не могут быть отрицательными.";
            return null;
        }

        if (menForFullProgress <= 0)
        {
            error = "В разделе siege нужен положительный menForFullProgress.";
            return null;
        }

        if (starveMinProgress < 0f || starveMinProgress > 1f)
        {
            error = "В разделе siege starveMinProgress должен быть от 0 до 1.";
            return null;
        }

        if (sackShare < 0f || sackShare > 1f)
        {
            error = "В разделе siege sackShare должен быть от 0 до 1.";
            return null;
        }

        if (annexRadius < 0)
        {
            error = "В разделе siege annexRadius не может быть отрицательным.";
            return null;
        }

        error = string.Empty;
        return new SiegeSettings(
            progressPerRun,
            reliefPerRun,
            wallsPerLevel,
            menForFullProgress,
            starveMinProgress,
            starvePerPerson,
            sackShare,
            annexRadius);
    }

    private static WarCauseSettings? ReadWar(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "war", out JsonElement block)
            || !TryReadInt(block, "checkRuns", out int checkRuns)
            || !TryReadInt(block, "minEra", out int minEra)
            || !TryReadFloat(block, "landHunger", out float landHunger)
            || !TryReadFloat(block, "resourceGreed", out float resourceGreed)
            || !TryReadFloat(block, "ambition", out float ambition)
            || !TryReadFloat(block, "allyJoin", out float allyJoin)
            || !TryReadInt(block, "declarationsPerCheck", out int declarationsPerCheck)
            || !TryReadFloat(block, "maxChancePerCheck", out float maxChancePerCheck)
            || !TryReadFloat(block, "exhaustionPerSoldier", out float exhaustionPerSoldier)
            || !TryReadFloat(block, "exhaustionDecay", out float exhaustionDecay)
            || !TryReadFloat(block, "peaceExhaustion", out float peaceExhaustion)
            || !TryReadInt(block, "maxWarRuns", out int maxWarRuns)
            || !TryReadInt(block, "truceRuns", out int truceRuns))
        {
            error = "Раздел war в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (checkRuns <= 0)
        {
            error = "В разделе war нужен положительный checkRuns.";
            return null;
        }

        if (minEra < 0)
        {
            error = "В разделе war minEra не может быть отрицательным.";
            return null;
        }

        if (landHunger < 0f || resourceGreed < 0f || ambition < 0f)
        {
            error = "В разделе war причины войны не могут быть отрицательными.";
            return null;
        }

        if (allyJoin < 0f || allyJoin > 1f)
        {
            error = "В разделе war allyJoin должен быть от 0 до 1.";
            return null;
        }

        if (declarationsPerCheck <= 0)
        {
            error = "В разделе war нужен положительный declarationsPerCheck.";
            return null;
        }

        if (maxChancePerCheck <= 0f || maxChancePerCheck > 1f)
        {
            error = "В разделе war maxChancePerCheck должен быть больше 0 и не больше 1.";
            return null;
        }

        if (exhaustionPerSoldier < 0f || exhaustionDecay < 0f)
        {
            error = "В разделе war усталость от войны меняется на неотрицательную величину.";
            return null;
        }

        if (peaceExhaustion <= 0f || peaceExhaustion > 1f)
        {
            error = "В разделе war peaceExhaustion должен быть больше 0 и не больше 1, иначе мира не будет.";
            return null;
        }

        if (maxWarRuns <= 0)
        {
            error = "В разделе war нужен положительный maxWarRuns: без предела войны шли бы вечно.";
            return null;
        }

        if (truceRuns < 0)
        {
            error = "В разделе war truceRuns не может быть отрицательным.";
            return null;
        }

        error = string.Empty;
        return new WarCauseSettings(
            checkRuns,
            minEra,
            landHunger,
            resourceGreed,
            ambition,
            allyJoin,
            declarationsPerCheck,
            maxChancePerCheck,
            exhaustionPerSoldier,
            exhaustionDecay,
            peaceExhaustion,
            maxWarRuns,
            truceRuns);
    }

    private static RevoltSettings? ReadRevolt(JsonElement root, out string error)
    {
        if (!TryReadBlock(root, "revolt", out JsonElement block)
            || !TryReadFloat(block, "unrestOnCapture", out float unrestOnCapture)
            || !TryReadFloat(block, "unrestDecay", out float unrestDecay)
            || !TryReadFloat(block, "chancePerRun", out float chancePerRun)
            || !TryReadFloat(block, "minUnrest", out float minUnrest)
            || !TryReadFloat(block, "freeTribeChance", out float freeTribeChance))
        {
            error = "Раздел revolt в data/" + FileName + " отсутствует или заполнен не полностью.";
            return null;
        }

        if (unrestOnCapture < 0f || unrestOnCapture > 1f)
        {
            error = "В разделе revolt unrestOnCapture должен быть от 0 до 1.";
            return null;
        }

        if (unrestDecay < 0f)
        {
            error = "В разделе revolt unrestDecay не может быть отрицательным.";
            return null;
        }

        if (chancePerRun < 0f || chancePerRun > 1f)
        {
            error = "В разделе revolt chancePerRun должен быть от 0 до 1.";
            return null;
        }

        if (minUnrest < 0f || minUnrest > 1f)
        {
            error = "В разделе revolt minUnrest должен быть от 0 до 1.";
            return null;
        }

        if (freeTribeChance < 0f || freeTribeChance > 1f)
        {
            error = "В разделе revolt freeTribeChance должен быть от 0 до 1.";
            return null;
        }

        error = string.Empty;
        return new RevoltSettings(unrestOnCapture, unrestDecay, chancePerRun, minUnrest, freeTribeChance);
    }

    private static bool TryReadBlock(JsonElement root, string name, out JsonElement block)
    {
        return root.TryGetProperty(name, out block) && block.ValueKind == JsonValueKind.Object;
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
