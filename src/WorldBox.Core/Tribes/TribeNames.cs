namespace WorldBox.Core.Tribes;

/// <summary>
/// Имена. Народы собираются из слогов, поселения берутся из готового списка.
/// Всё через генератор мира, поэтому один сид даёт одинаковые названия.
/// </summary>
public static class TribeNames
{
    private static readonly string[] Starts =
    {
        "Ар", "Бел", "Вен", "Гор", "Даг", "Зар", "Кем", "Лан", "Мор", "Нур",
        "Ост", "Рав", "Сар", "Тал", "Уль", "Фар", "Хад", "Чер", "Эль", "Ярн",
        "Кан", "Син", "Тур", "Вар",
    };

    private static readonly string[] Middles = { "", "", "а", "о", "и", "ен", "ар", "ол", "ур", "ай" };

    private static readonly string[] Ends =
    {
        "ия", "ты", "ны", "дар", "гард", "кан", "мир", "тея", "слав", "град",
        "хейм", "рия", "тан", "сар",
    };

    private static readonly string[] Places =
    {
        "Каменка", "Белый Брод", "Три Холма", "Соляной Мыс", "Дубрава", "Заречье",
        "Красный Яр", "Тихий Дол", "Волчий Лог", "Медвежий Угол", "Рыбный Затон",
        "Ясный Ключ", "Сухая Балка", "Утиное Озеро", "Синий Камень", "Долгий Плёс",
        "Гончарный Ряд", "Соколиный Гребень", "Липовый Куст", "Старая Гать",
        "Оленья Тропа", "Медный Ручей", "Зимовье", "Ветреный Мыс", "Пепельное Поле",
        "Лисья Нора", "Журавли", "Тёплый Исток", "Каменный Лоб", "Берёзовый Стан",
        "Сосновый Брод", "Гусиный Луг", "Барсучий Холм", "Чёрный Камень",
        "Светлый Затон", "Косой Овраг", "Тростники", "Ореховый Скат",
    };

    public static string Next(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return string.Concat(
            Starts[rng.NextInt(Starts.Length)],
            Middles[rng.NextInt(Middles.Length)],
            Ends[rng.NextInt(Ends.Length)]);
    }

    /// <summary>Имя, которого ещё нет у живых народов. После нескольких проб берёт что вышло.</summary>
    public static string Unique(Rng rng, TribeStore tribes)
    {
        ArgumentNullException.ThrowIfNull(tribes);

        string name = Next(rng);
        for (int attempt = 0; attempt < 16 && Taken(tribes, name); attempt++)
        {
            name = Next(rng);
        }

        return name;
    }

    public static string Place(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return Places[rng.NextInt(Places.Length)];
    }

    private static bool Taken(TribeStore tribes, string name)
    {
        for (int i = 1; i < tribes.Capacity; i++)
        {
            if (tribes.Alive[i] && string.Equals(tribes.Name[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
