using WorldBox.Core.World;

namespace WorldBox.Core.Art;

/// <summary>Класс поверхности: определяет, каким узором рисуется тайл.</summary>
public enum TileTexture : byte
{
    Water,
    Ice,
    Sand,
    Dune,
    Grass,
    Steppe,
    Forest,
    Rock,
    Snow,
    Marsh,
}

/// <summary>
/// Рисует тайлы 16x16 процедурно. Результат — не цвета, а номера оттенков 0..3:
/// 0 базовый, 1 тень, 2 блик, 3 деталь. Цвета подставляет слой отрисовки, поэтому
/// палитра остаётся в одном месте, а ядро не знает про MonoGame.
/// Всё детерминировано: рисунок зависит только от биома, номера варианта и кадра.
///
/// Ориентир — оригинальный WorldBox: поверхность читается крупными пятнами, а не шумом.
/// Все узоры собраны из кусков по 2–4 пикселя: одиночные точки на дальнем зуме превращаются в грязь.
/// </summary>
public static class TileArt
{
    public const int TileSize = 16;
    public const int Pixels = TileSize * TileSize;
    public const int Variants = 4;
    public const int Frames = 4;
    public const int Shades = 4;

    // Порядок строго совпадает с перечислением Biome: 0 глубокий океан ... 18 пик.
    private static readonly TileTexture[] Classes =
    {
        TileTexture.Water,  // глубокий океан
        TileTexture.Water,  // океан
        TileTexture.Water,  // прибрежная вода
        TileTexture.Sand,   // пляж
        TileTexture.Water,  // озеро
        TileTexture.Water,  // река
        TileTexture.Marsh,  // болото
        TileTexture.Dune,   // пустыня
        TileTexture.Steppe, // саванна
        TileTexture.Steppe, // степь
        TileTexture.Grass,  // луга
        TileTexture.Steppe, // кустарник
        TileTexture.Forest, // широколиственный лес
        TileTexture.Forest, // дождевой лес
        TileTexture.Forest, // тайга
        TileTexture.Snow,   // тундра
        TileTexture.Ice,    // ледник
        TileTexture.Rock,   // горы
        TileTexture.Snow,   // пик
    };

    /// <summary>Сколько биомов описано в таблице поверхностей. Должно совпадать с Biomes.Count.</summary>
    public static int ClassCount => Classes.Length;

    public static TileTexture ClassOf(Biome biome)
    {
        int index = (int)biome;
        return index >= 0 && index < Classes.Length ? Classes[index] : TileTexture.Grass;
    }

    /// <summary>Анимируется только вода: остальное рисуется одним кадром и не тратит кадровое время.</summary>
    public static bool IsAnimated(Biome biome)
    {
        return ClassOf(biome) == TileTexture.Water;
    }

    public static int FrameCount(Biome biome)
    {
        return IsAnimated(biome) ? Frames : 1;
    }

    /// <summary>Номер варианта рисунка для клетки карты: одинаковые биомы не выглядят обоями.</summary>
    public static int VariantFor(int x, int y)
    {
        return (int)(Hash(x, y, 8191) % Variants);
    }

    /// <summary>Заполняет буфер номерами оттенков для одного тайла.</summary>
    public static void Build(Biome biome, int variant, int frame, Span<byte> tile)
    {
        if (tile.Length < Pixels)
        {
            throw new ArgumentException("Буфер тайла меньше 16x16.", nameof(tile));
        }

        tile[..Pixels].Clear();

        TileTexture texture = ClassOf(biome);
        int salt = (((int)biome + 1) * 977) + (((variant % Variants) + Variants) % Variants * 31);
        int step = ((frame % Frames) + Frames) % Frames;

        switch (texture)
        {
            case TileTexture.Water:
                BuildWater(tile, salt, step);
                break;
            case TileTexture.Ice:
                BuildIce(tile, salt);
                break;
            case TileTexture.Sand:
                BuildSand(tile, salt);
                break;
            case TileTexture.Dune:
                BuildDune(tile, salt);
                break;
            case TileTexture.Grass:
                BuildGrass(tile, salt, true);
                break;
            case TileTexture.Steppe:
                BuildSteppe(tile, salt);
                break;
            case TileTexture.Forest:
                BuildForest(tile, salt);
                break;
            case TileTexture.Rock:
                BuildRock(tile, salt);
                break;
            case TileTexture.Snow:
                BuildSnow(tile, salt);
                break;
            default:
                BuildMarsh(tile, salt);
                break;
        }
    }

    /// <summary>
    /// Вода: ровная гладь и светлые гребни-чёрточки, которые едут вбок от кадра к кадру.
    /// Именно так вода выглядит в оригинале: не рябь по всему тайлу, а редкие штрихи на тёмной глади.
    /// </summary>
    private static void BuildWater(Span<byte> tile, int salt, int frame)
    {
        // Лёгкая подвижная тень полосами — глубина под поверхностью.
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int band = (x + (y * 3) + (frame * 2)) & 15;
                if (band < 2)
                {
                    tile[(y * TileSize) + x] = 1;
                }
            }
        }

        // Гребни: короткие горизонтальные штрихи с тенью под ними.
        for (int y = 1; y < TileSize; y += 4)
        {
            int start = (int)((Hash(0, y, salt) + (uint)(frame * 5)) % TileSize);
            int length = 3 + (int)(Hash(1, y, salt) % 3u);

            for (int d = 0; d < length; d++)
            {
                int x = (start + d) % TileSize;
                Set(tile, x, y, 2);
                Set(tile, x, y + 1, 1);
            }
        }

        // Редкие блёстки по два пикселя: одиночные точки издали превращаются в шум.
        for (int spark = 0; spark < 3; spark++)
        {
            int x = (int)(Hash(spark, 31 + frame, salt) % TileSize);
            int y = (int)(Hash(spark, 37 + frame, salt) % TileSize);
            Set(tile, x, y, 2);
            Set(tile, x + 1, y, 2);
        }
    }

    /// <summary>Лёд: крупные плиты со светлыми краями и парой трещин.</summary>
    private static void BuildIce(Span<byte> tile, int salt)
    {
        // Плиты 8x8 с разным тоном — лёд перестаёт быть пустым белым пятном.
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool light = ((((x + 3) >> 3) + (y >> 3)) & 1) == 0;
                tile[(y * TileSize) + x] = light ? (byte)0 : (byte)2;
            }
        }

        for (int crack = 0; crack < 2; crack++)
        {
            int x = (int)(Hash(crack, 7, salt) % TileSize);
            for (int y = 0; y < TileSize; y++)
            {
                Set(tile, x, y, 1);
                if (Hash(x, y, salt + crack) % 3u == 0u)
                {
                    x += Hash(y, crack, salt) % 2u == 0u ? 1 : -1;
                }
            }
        }
    }

    /// <summary>Песок: мелкая крупа, полосы прибоя и редкие ракушки.</summary>
    private static void BuildSand(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            uint h = Hash(i & 15, i >> 4, salt) % 18u;
            if (h == 0u)
            {
                tile[i] = 2;
            }
            else if (h == 1u)
            {
                tile[i] = 1;
            }
        }

        // Две мягкие волнистые полосы — след прибоя на пляже.
        for (int line = 0; line < 2; line++)
        {
            int y = 3 + (int)(Hash(line, 41, salt) % 10u);
            for (int x = 0; x < TileSize; x++)
            {
                int wobble = (int)(Hash(x >> 2, line, salt) % 2u);
                Set(tile, x, y + wobble, 1);
            }
        }

        int shells = (int)(Hash(9, 9, salt) % 2u);
        for (int shell = 0; shell < shells; shell++)
        {
            int x = (int)(Hash(shell, 51, salt) % TileSize);
            int y = (int)(Hash(shell, 53, salt) % TileSize);
            Set(tile, x, y, 3);
        }
    }

    /// <summary>Пустыня: крупные дюнные волны с освещённым гребнем и тенью в ложбине.</summary>
    private static void BuildDune(Span<byte> tile, int salt)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int shift = (int)(Hash(0, y >> 1, salt) % 3u);
            for (int x = 0; x < TileSize; x++)
            {
                int band = (x + (y * 2) + shift) % 12;
                byte shade = 0;
                if (band == 0 || band == 1)
                {
                    shade = 2;
                }
                else if (band == 6 || band == 7)
                {
                    shade = 1;
                }

                tile[(y * TileSize) + x] = shade;
            }
        }
    }

    /// <summary>
    /// Трава: та самая шахматка из оригинала — клетки 4x4 чередуют основной цвет и тень.
    /// Сверху ложатся кустики, а на лугах — ещё и цветы детальным цветом.
    /// </summary>
    private static void BuildGrass(Span<byte> tile, int salt, bool flowers)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool shaded = (((x >> 2) + (y >> 2)) & 1) == 1;
                tile[(y * TileSize) + x] = shaded ? (byte)1 : (byte)0;
            }
        }

        // Пятна света по 2x1: шахматка перестаёт читаться сеткой.
        for (int patch = 0; patch < 5; patch++)
        {
            int x = (int)(Hash(patch, 61, salt) % TileSize);
            int y = (int)(Hash(patch, 67, salt) % TileSize);
            Set(tile, x, y, 2);
            Set(tile, x + 1, y, 2);
        }

        for (int tuft = 0; tuft < 5; tuft++)
        {
            int x = (int)(Hash(tuft, 1, salt) % TileSize);
            int y = 2 + (int)(Hash(tuft, 2, salt) % (TileSize - 3));
            Set(tile, x, y, 1);
            Set(tile, x, y - 1, 2);
            Set(tile, x + 1, y, 1);
        }

        if (!flowers)
        {
            return;
        }

        int count = (int)(Hash(3, 71, salt) % 3u);
        for (int flower = 0; flower < count; flower++)
        {
            int x = 1 + (int)(Hash(flower, 73, salt) % (TileSize - 2));
            int y = 1 + (int)(Hash(flower, 79, salt) % (TileSize - 2));
            Set(tile, x, y, 3);
            Set(tile, x, y + 1, 1);
        }
    }

    /// <summary>Степь, саванна и кустарник: выгоревшие прогалины и сухие пучки.</summary>
    private static void BuildSteppe(Span<byte> tile, int salt)
    {
        // Крупные выгоревшие пятна 4x2 вместо ровного фона.
        for (int spot = 0; spot < 6; spot++)
        {
            int x = (int)(Hash(spot, 83, salt) % TileSize);
            int y = (int)(Hash(spot, 89, salt) % TileSize);
            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 4; dx++)
                {
                    Set(tile, x + dx, y + dy, 1);
                }
            }
        }

        for (int dash = 0; dash < 10; dash++)
        {
            int x = (int)(Hash(dash, 3, salt) % TileSize);
            int y = (int)(Hash(dash, 4, salt) % TileSize);
            Set(tile, x, y, 2);
            Set(tile, x + 1, y, 2);
            if (Hash(dash, 5, salt) % 3u == 0u)
            {
                Set(tile, x, y - 1, 3);
            }
        }
    }

    /// <summary>Лес: тёмный подлесок и четыре кроны с обводкой и бликом сверху.</summary>
    private static void BuildForest(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            if (Hash(i & 15, i >> 4, salt) % 5u == 0u)
            {
                tile[i] = 1;
            }
        }

        for (int tree = 0; tree < 4; tree++)
        {
            int cx = 3 + (int)(Hash(tree, 11, salt) % 11u);
            int cy = 4 + (int)(Hash(tree, 12, salt) % 9u);
            BuildCrown(tile, cx, cy);
        }
    }

    /// <summary>Одна крона: шарик из основного цвета, свет сверху слева, тёмная обводка и ствол.</summary>
    private static void BuildCrown(Span<byte> tile, int cx, int cy)
    {
        ReadOnlySpan<int> halves = stackalloc int[] { 1, 2, 2, 2, 1 };

        for (int row = 0; row < halves.Length; row++)
        {
            int dy = row - 3;
            int half = halves[row];

            for (int dx = -half; dx <= half; dx++)
            {
                Set(tile, cx + dx, cy + dy, 0);
            }

            // Обводка по бокам: без неё кроны сливаются в одно зелёное пятно.
            Set(tile, cx - half - 1, cy + dy, 3);
            Set(tile, cx + half + 1, cy + dy, 3);
        }

        Set(tile, cx - 1, cy - 3, 2);
        Set(tile, cx, cy - 3, 2);
        Set(tile, cx - 1, cy - 2, 2);

        Set(tile, cx, cy + 2, 3);
        Set(tile, cx, cy + 3, 3);
    }

    /// <summary>Горы: гранёные блоки 4x4 со светлой вершиной и тёмной подошвой.</summary>
    private static void BuildRock(Span<byte> tile, int salt)
    {
        for (int by = 0; by < TileSize; by += 4)
        {
            for (int bx = 0; bx < TileSize; bx += 4)
            {
                uint h = Hash(bx, by, salt) % 3u;
                byte body = h switch
                {
                    0u => 0,
                    1u => 1,
                    _ => 3,
                };

                for (int dy = 0; dy < 4; dy++)
                {
                    for (int dx = 0; dx < 4; dx++)
                    {
                        Set(tile, bx + dx, by + dy, body);
                    }
                }

                // Грань сверху и тень снизу — камень становится объёмным.
                for (int dx = 0; dx < 4; dx++)
                {
                    Set(tile, bx + dx, by, 2);
                    Set(tile, bx + dx, by + 3, 1);
                }
            }
        }
    }

    /// <summary>Снег и пики: почти ровное поле, мягкие сугробы и редкие искры.</summary>
    private static void BuildSnow(Span<byte> tile, int salt)
    {
        for (int drift = 0; drift < 4; drift++)
        {
            int x = (int)(Hash(drift, 97, salt) % TileSize);
            int y = (int)(Hash(drift, 101, salt) % TileSize);
            for (int dx = -2; dx <= 2; dx++)
            {
                Set(tile, x + dx, y, 2);
                if (Math.Abs(dx) < 2)
                {
                    Set(tile, x + dx, y + 1, 1);
                }
            }
        }

        for (int i = 0; i < Pixels; i++)
        {
            if (Hash(i & 15, i >> 4, salt) % 31u == 0u)
            {
                tile[i] = 2;
            }
        }
    }

    /// <summary>Болото: лужи с тёмной серединой, кочки и камыш.</summary>
    private static void BuildMarsh(Span<byte> tile, int salt)
    {
        for (int puddle = 0; puddle < 4; puddle++)
        {
            int cx = (int)(Hash(puddle, 21, salt) % TileSize);
            int cy = (int)(Hash(puddle, 22, salt) % TileSize);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (Math.Abs(dx) == 2 && dy != 0)
                    {
                        continue;
                    }

                    Set(tile, cx + dx, cy + dy, 1);
                }
            }

            Set(tile, cx, cy, 3);
        }

        // Кочки и камыш: два пикселя вверх, чтобы болото не выглядело лужайкой.
        for (int reed = 0; reed < 5; reed++)
        {
            int x = (int)(Hash(reed, 103, salt) % TileSize);
            int y = 2 + (int)(Hash(reed, 107, salt) % (TileSize - 3));
            Set(tile, x, y, 2);
            Set(tile, x, y - 1, 2);
        }
    }

    private static void Set(Span<byte> tile, int x, int y, byte shade)
    {
        if (x < 0 || y < 0 || x >= TileSize || y >= TileSize)
        {
            return;
        }

        tile[(y * TileSize) + x] = shade;
    }

    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
