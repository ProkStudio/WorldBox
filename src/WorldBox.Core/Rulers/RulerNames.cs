namespace WorldBox.Core.Rulers;

/// <summary>
/// Имена правителей, домов и героев. Язык берётся у культуры народа, поэтому
/// правитель звучит как его народ: у одних Светозар из Светичей, у других Хазар из Хазаров.
///
/// Случайность только из <see cref="Rng"/>: один сид — одна и та же родословная.
/// </summary>
public static class RulerNames
{
    /// <summary>Столько же условных языков, сколько знает генератор культур.</summary>
    public const int Languages = 6;

    private static readonly string[][] Given =
    {
        new[] { "Венар", "Солин", "Марций", "Лидор", "Тирен", "Нейран", "Велий", "Кассин", "Аврен", "Ситар" },
        new[] { "Карг", "Дурн", "Хазар", "Горм", "Рагн", "Тумар", "Брок", "Грид", "Ульв", "Скарн" },
        new[] { "Аниэ", "Сиан", "Миел", "Лоар", "Эрин", "Уваи", "Наиль", "Сэйра", "Тиан", "Ойле" },
        new[] { "Ясномир", "Светозар", "Зорян", "Градимир", "Брегослав", "Долномир", "Ратибор", "Всемил", "Любодар", "Стоян" },
        new[] { "Тао", "Шинь", "Кунь", "Ляо", "Хэнь", "Цзин", "Юэ", "Мэйли", "Лань", "Баоши" },
        new[] { "Умар", "Сафи", "Нахид", "Бадр", "Зайд", "Каир", "Мансур", "Рашид", "Тахир", "Ясин" },
    };

    private static readonly string[][] Houses =
    {
        new[] { "Венарии", "Солиды", "Марцелы", "Лидары", "Тирены", "Нейраны" },
        new[] { "Каргуны", "Дурниды", "Хазары", "Гормы", "Рагниды", "Тумары" },
        new[] { "Аниды", "Сианы", "Миелы", "Лоары", "Эриды", "Уваи" },
        new[] { "Ясничи", "Светичи", "Зоряничи", "Градичи", "Брежичи", "Долничи" },
        new[] { "Тао", "Шинь", "Кунь", "Ляо", "Хэн", "Цзин" },
        new[] { "Умайи", "Сафиды", "Нахиды", "Бадриды", "Зайдиды", "Каириды" },
    };

    /// <summary>Прозвища идут в том же порядке, что и черты в <see cref="Traits"/>.</summary>
    private static readonly string[] Epithets =
    {
        "Жестокий", "Справедливый", "Мудрый", "Строитель", "Завоеватель", "Благочестивый",
    };

    /// <summary>Личное имя правителя или героя.</summary>
    public static string Person(Rng rng, int language)
    {
        ArgumentNullException.ThrowIfNull(rng);

        string[] names = Given[Clamp(language)];
        return names[rng.NextInt(names.Length)];
    }

    /// <summary>Имя дома: им зовётся вся династия, пока её не сменит переворот или выборы.</summary>
    public static string House(Rng rng, int language)
    {
        ArgumentNullException.ThrowIfNull(rng);

        string[] houses = Houses[Clamp(language)];
        return houses[rng.NextInt(houses.Length)];
    }

    /// <summary>Прозвище по первой черте: «Жестокий», «Мудрый». Пусто, если черт нет.</summary>
    public static string Epithet(Trait traits)
    {
        for (int i = 0; i < Traits.Count; i++)
        {
            if ((traits & Traits.Of(i)) != Trait.None)
            {
                return Epithets[i];
            }
        }

        return string.Empty;
    }

    private static int Clamp(int language)
    {
        if (language < 0)
        {
            return 0;
        }

        return language >= Languages ? Languages - 1 : language;
    }
}
