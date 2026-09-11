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
                BuildGrass(tile, salt);
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

    /// <summary>Вода: наклонные волны, которые смещаются от кадра к кадру.</summary>
    private static void BuildWater(Span<byte> tile, int salt, int frame)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int wave = (x + (y * 5) + (frame * 4)) & 15;
                byte shade = 0;
                if (wave < 3)
                {
                    shade = 2;
                }
                else if (wave > 11)
                {
                    shade = 1;
                }

                if (Hash(x, y, salt) % 19u == 0u)
                {
                    shade = shade == 2 ? (byte)2 : (byte)1;
                }

                tile[(y * TileSize) + x] = shade;
            }
        }
    }

    /// <summary>Лёд: ровная поверхность с бликами и парой трещин.</summary>
    private static void BuildIce(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            if (Hash(i & 15, i >> 4, salt) % 29u == 0u)
            {
                tile[i] = 2;
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

    /// <summary>Песок: редкие крупинки без рисунка.</summary>
    private static void BuildSand(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            uint h = Hash(i & 15, i >> 4, salt) % 16u;
            if (h == 0u)
            {
                tile[i] = 2;
            }
            else if (h == 1u)
            {
                tile[i] = 1;
            }
        }
    }

    /// <summary>Пустыня: дюнные полосы по диагонали.</summary>
    private static void BuildDune(Span<byte> tile, int salt)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int shift = (int)(Hash(0, y, salt) % 4u);
            for (int x = 0; x < TileSize; x++)
            {
                int band = (x + (y * 2) + shift) & 7;
                byte shade = 0;
                if (band == 0)
                {
                    shade = 2;
                }
                else if (band == 4)
                {
                    shade = 1;
                }

                tile[(y * TileSize) + x] = shade;
            }
        }
    }

    /// <summary>Луга: крап и кустики с бликом на кончике.</summary>
    private static void BuildGrass(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            if (Hash(i & 15, i >> 4, salt) % 8u == 0u)
            {
                tile[i] = 1;
            }
        }

        for (int tuft = 0; tuft < 7; tuft++)
        {
            int x = (int)(Hash(tuft, 1, salt) % TileSize);
            int y = 2 + (int)(Hash(tuft, 2, salt) % (TileSize - 3));
            Set(tile, x, y, 1);
            Set(tile, x, y - 1, 2);
            Set(tile, x + 1, y, 1);
        }
    }

    /// <summary>Степь и саванна: редкие сухие штрихи.</summary>
    private static void BuildSteppe(Span<byte> tile, int salt)
    {
        for (int dash = 0; dash < 12; dash++)
        {
            int x = (int)(Hash(dash, 3, salt) % TileSize);
            int y = (int)(Hash(dash, 4, salt) % TileSize);
            Set(tile, x, y, 1);
            Set(tile, x + 1, y, 1);
            if (Hash(dash, 5, salt) % 2u == 0u)
            {
                Set(tile, x, y - 1, 2);
            }
        }
    }

    /// <summary>Лес: трава плюс три дерева ромбиком и стволом.</summary>
    private static void BuildForest(Span<byte> tile, int salt)
    {
        BuildGrass(tile, salt + 13);

        for (int tree = 0; tree < 3; tree++)
        {
            int cx = 3 + (int)(Hash(tree, 11, salt) % 11u);
            int cy = 4 + (int)(Hash(tree, 12, salt) % 9u);

            for (int dy = -2; dy <= 1; dy++)
            {
                int half = 2 - Math.Abs(dy);
                for (int dx = -half; dx <= half; dx++)
                {
                    Set(tile, cx + dx, cy + dy, dy < 0 ? (byte)2 : (byte)3);
                }
            }

            Set(tile, cx, cy + 2, 3);
        }
    }

    /// <summary>Горы: угловатые пятна по 2x2 пикселя.</summary>
    private static void BuildRock(Span<byte> tile, int salt)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                uint h = Hash(x / 2, y / 2, salt) % 4u;
                byte shade = 0;
                if (h == 0u)
                {
                    shade = 2;
                }
                else if (h == 1u)
                {
                    shade = 1;
                }
                else if (h == 2u)
                {
                    shade = 3;
                }

                tile[(y * TileSize) + x] = shade;
            }
        }
    }

    /// <summary>Снег и пики: почти ровное поле с искрами.</summary>
    private static void BuildSnow(Span<byte> tile, int salt)
    {
        for (int i = 0; i < Pixels; i++)
        {
            uint h = Hash(i & 15, i >> 4, salt);
            if (h % 11u == 0u)
            {
                tile[i] = 2;
            }
            else if (h % 23u == 1u)
            {
                tile[i] = 1;
            }
        }
    }

    /// <summary>Болото: лужи с тёмной серединой и пятна тины.</summary>
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

        for (int i = 0; i < Pixels; i++)
        {
            if (Hash(i & 15, i >> 4, salt) % 17u == 0u)
            {
                tile[i] = 2;
            }
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
