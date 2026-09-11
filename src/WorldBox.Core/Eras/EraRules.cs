using WorldBox.Core.World;

namespace WorldBox.Core.Eras;

/// <summary>Что мешает народу шагнуть в следующую эпоху. Показывается в панелях.</summary>
public enum EraBlock : byte
{
    /// <summary>Всё готово, знание копится.</summary>
    Ready = 0,

    /// <summary>Выше эпох в таблице нет.</summary>
    Top = 1,

    /// <summary>Мало людей.</summary>
    People = 2,

    /// <summary>Нет доступа к нужному ресурсу ни у себя, ни у соседей.</summary>
    Resource = 3,

    /// <summary>Нет нужной земли: пашни, реки, берега или гор.</summary>
    Geography = 4,

    /// <summary>Тёмные века после отката назад.</summary>
    DarkAge = 5,
}

/// <summary>
/// Правила перехода между эпохами. Здесь только чистые функции без состояния:
/// их удобно проверять тестами, и они одинаково считаются в игре, в тестах и в замерах.
/// </summary>
public static class EraRules
{
    /// <summary>Сколько людей изображает один человечек в этой эпохе.</summary>
    public static float PerAgent(EraTable table, int era)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Population.PerAgent * MathF.Pow(table.Population.PerAgentPerEra, Math.Max(0, era));
    }

    /// <summary>Население народа в цифрах исторического масштаба.</summary>
    public static long Headcount(float perAgent, int people)
    {
        if (people <= 0 || perAgent <= 0f)
        {
            return 0L;
        }

        return (long)(people * (double)perAgent);
    }

    /// <summary>То же самое, но с расчётом множителя по таблице.</summary>
    public static long Headcount(EraTable table, int people, int era) => Headcount(PerAgent(table, era), people);

    /// <summary>Сколько очков знания нужно, чтобы войти в эпоху с номером nextEra.</summary>
    public static float Cost(EraTable table, int nextEra)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Research.BaseCost + (table.Research.CostPerEra * Math.Max(0, nextEra - 1));
    }

    /// <summary>
    /// Главная проверка среза: может ли народ шагнуть дальше.
    /// resourceAccess — это свои месторождения плюс то, что дают соседи.
    /// Маски нехватки заполняются всегда, даже когда мешает другая причина: так панель сразу показывает весь список бед.
    /// </summary>
    public static EraBlock Evaluate(
        EraTable table,
        int era,
        long headcount,
        int resourceAccess,
        byte geo,
        out int missingResources,
        out byte missingGeo)
    {
        ArgumentNullException.ThrowIfNull(table);

        missingResources = 0;
        missingGeo = 0;

        int next = era + 1;
        if (next >= table.Count)
        {
            return EraBlock.Top;
        }

        missingResources = table.RequiredResources[next] & ~resourceAccess;
        missingGeo = (byte)(table.RequiredGeo[next] & ~geo);

        if (headcount < table.MinPop[next])
        {
            return EraBlock.People;
        }

        if (missingResources != 0)
        {
            return EraBlock.Resource;
        }

        if (missingGeo != 0)
        {
            return EraBlock.Geography;
        }

        return EraBlock.Ready;
    }

    /// <summary>Первый недостающий ресурс из маски — его и показываем игроку.</summary>
    public static ResourceKind FirstMissing(int missingResources)
    {
        for (int i = 1; i < ResourceKinds.Count; i++)
        {
            if ((missingResources & (1 << i)) != 0)
            {
                return (ResourceKind)i;
            }
        }

        return ResourceKind.None;
    }

    /// <summary>Первое недостающее требование к земле.</summary>
    public static GeoFeature FirstMissingGeo(byte missingGeo)
    {
        if ((missingGeo & (byte)GeoFeature.Fertile) != 0)
        {
            return GeoFeature.Fertile;
        }

        if ((missingGeo & (byte)GeoFeature.River) != 0)
        {
            return GeoFeature.River;
        }

        if ((missingGeo & (byte)GeoFeature.Coast) != 0)
        {
            return GeoFeature.Coast;
        }

        if ((missingGeo & (byte)GeoFeature.Mountain) != 0)
        {
            return GeoFeature.Mountain;
        }

        return GeoFeature.None;
    }

    /// <summary>Ключ строки для требования к земле.</summary>
    public static string GeoNameKey(GeoFeature feature) => feature switch
    {
        GeoFeature.Fertile => "geo.fertile",
        GeoFeature.River => "geo.river",
        GeoFeature.Coast => "geo.coast",
        GeoFeature.Mountain => "geo.mountain",
        _ => "geo.none",
    };
}
