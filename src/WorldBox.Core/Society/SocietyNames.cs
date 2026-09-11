namespace WorldBox.Core.Society;

/// <summary>
/// Названия культур и религий. Каждая культура говорит на одном из шести условных
/// языков, и слоги у языков разные: соседние народы звучат по-разному, а города,
/// культура и вера одного народа — похоже.
///
/// Случайность приходит только из <see cref="Rng"/>, поэтому один сид даёт
/// одинаковые названия в любом запуске.
/// </summary>
public static class SocietyNames
{
    /// <summary>Сколько условных языков знает генератор.</summary>
    public const int Languages = 6;

    private static readonly string[][] Roots =
    {
        new[] { "Вен", "Сол", "Мар", "Лид", "Тир", "Ней" },
        new[] { "Кар", "Дур", "Хаз", "Гор", "Раг", "Тум" },
        new[] { "Ани", "Сиа", "Мие", "Лоа", "Эри", "Ува" },
        new[] { "Ясн", "Свет", "Зорь", "Град", "Брег", "Долн" },
        new[] { "Тао", "Шинь", "Кун", "Ляо", "Хэн", "Цзин" },
        new[] { "Умм", "Сафи", "Нахи", "Бадр", "Зайд", "Каир" },
    };

    private static readonly string[][] CultureEnds =
    {
        new[] { "ары", "иты", "оны", "еи" },
        new[] { "уны", "дры", "оги", "аки" },
        new[] { "нцы", "лы", "иды", "ны" },
        new[] { "ичи", "овцы", "ане", "ичане" },
        new[] { "цы", "сцы", "ны", "ши" },
        new[] { "иды", "иты", "ани", "уры" },
    };

    private static readonly string[] FaithPrefixes =
    {
        "Путь", "Свет", "Завет", "Круг", "Дети", "Слово", "Поток", "Огонь",
    };

    private static readonly string[] FaithEnds =
    {
        "а", "и", "ы", "на", "ара", "има",
    };

    private static readonly string[] SchismMarks =
    {
        "Новый", "Строгий", "Тихий", "Старый", "Истинный", "Второй",
    };

    /// <summary>Название культуры: «Венары», «Дурдры», «Ясничи».</summary>
    public static string Culture(Rng rng, int language)
    {
        ArgumentNullException.ThrowIfNull(rng);

        int lang = Clamp(language);
        string[] roots = Roots[lang];
        string[] ends = CultureEnds[lang];
        return string.Concat(roots[rng.NextInt(roots.Length)], ends[rng.NextInt(ends.Length)]);
    }

    /// <summary>Название религии: «Путь Солна», «Дети Хазара», «Тумизм».</summary>
    public static string Religion(Rng rng, int language)
    {
        ArgumentNullException.ThrowIfNull(rng);

        int lang = Clamp(language);
        string[] roots = Roots[lang];
        string root = roots[rng.NextInt(roots.Length)];

        // Половина вер зовётся по корню («Тумизм»), половина — двумя словами («Путь Солна»).
        if (rng.Chance(0.5f))
        {
            return string.Concat(root, "изм");
        }

        return string.Concat(
            FaithPrefixes[rng.NextInt(FaithPrefixes.Length)],
            " ",
            root,
            FaithEnds[rng.NextInt(FaithEnds.Length)]);
    }

    /// <summary>Название раскола: к имени матери-веры добавляется пометка толка.</summary>
    public static string Schism(Rng rng, string parent)
    {
        ArgumentNullException.ThrowIfNull(rng);

        string mark = SchismMarks[rng.NextInt(SchismMarks.Length)];
        if (string.IsNullOrEmpty(parent))
        {
            return mark + " толк";
        }

        return string.Concat(mark, " ", parent);
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
