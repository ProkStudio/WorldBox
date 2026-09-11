using WorldBox.Core.World;

namespace WorldBox.Core.Art;

/// <summary>Что стоит на тайле поверх земли.</summary>
public enum DecorKind : byte
{
    None = 0,
    PineTree,
    BroadTree,
    PalmTree,
    DeadTree,
    Bush,
    Cactus,
    Reeds,
    Rock,
    Boulder,
    Flowers,
    IceShard,
    Hut,
    House,
    StoneHouse,
    Tower,
    Castle,
    Farm,
}

/// <summary>
/// Процедурные спрайты декора 16x16. Как и у тайлов, результат — не цвета, а номера слотов
/// палитры: 0 пусто, 1 контур, 2 основной, 3 блик, 4 второй материал (ствол, крыша),
/// 5 акцент (цветок, окно, дым). Цвета слотов даёт <see cref="Color"/>.
///
/// Общий приём для вида как в WorldBox: сначала рисуется силуэт, затем по нему прогоняется
/// тёмный контур в один пиксель. Контур не даёт спрайту слиться с травой и делает картинку
/// мультяшной, а не шумной. Всё детерминировано: рисунок зависит только от вида и варианта.
///
/// Спрайт стоит на нижней строке: слой отрисовки ставит его основанием на низ тайла и может
/// рисовать выше одного тайла, чтобы крона нависала над соседом.
/// </summary>
public static class DecorArt
{
    public const int Size = 16;
    public const int Pixels = Size * Size;
    public const int Variants = 3;
    public const int Slots = 6;

    /// <summary>Сколько видов декора описано, включая пустой.</summary>
    public static int KindCount => 18;

    private static readonly ArtColor[] Trunk =
    {
        ArtColor.Rgb(96, 64, 40),
        ArtColor.Rgb(124, 84, 52),
    };

    /// <summary>Цвет слота для вида декора.</summary>
    public static ArtColor Color(DecorKind kind, int slot)
    {
        int safe = slot < 0 ? 0 : slot % Slots;
        return kind switch
        {
            DecorKind.PineTree => Pick(safe, ArtColor.Rgb(18, 52, 40), ArtColor.Rgb(36, 112, 74), ArtColor.Rgb(66, 156, 100), Trunk[0], ArtColor.Rgb(220, 236, 226)),
            DecorKind.BroadTree => Pick(safe, ArtColor.Rgb(22, 64, 34), ArtColor.Rgb(56, 148, 66), ArtColor.Rgb(102, 198, 106), Trunk[1], ArtColor.Rgb(226, 96, 88)),
            DecorKind.PalmTree => Pick(safe, ArtColor.Rgb(24, 70, 44), ArtColor.Rgb(64, 160, 88), ArtColor.Rgb(116, 208, 124), Trunk[1], ArtColor.Rgb(216, 172, 78)),
            DecorKind.DeadTree => Pick(safe, ArtColor.Rgb(46, 34, 28), ArtColor.Rgb(104, 82, 62), ArtColor.Rgb(146, 122, 96), Trunk[0], ArtColor.Rgb(120, 100, 80)),
            DecorKind.Bush => Pick(safe, ArtColor.Rgb(26, 70, 38), ArtColor.Rgb(70, 156, 76), ArtColor.Rgb(118, 202, 116), Trunk[1], ArtColor.Rgb(232, 108, 100)),
            DecorKind.Cactus => Pick(safe, ArtColor.Rgb(24, 72, 46), ArtColor.Rgb(70, 152, 88), ArtColor.Rgb(116, 196, 124), Trunk[1], ArtColor.Rgb(240, 148, 168)),
            DecorKind.Reeds => Pick(safe, ArtColor.Rgb(32, 58, 34), ArtColor.Rgb(96, 148, 80), ArtColor.Rgb(148, 190, 112), ArtColor.Rgb(146, 120, 66), ArtColor.Rgb(186, 156, 84)),
            DecorKind.Rock => Pick(safe, ArtColor.Rgb(52, 50, 52), ArtColor.Rgb(126, 124, 126), ArtColor.Rgb(176, 174, 176), ArtColor.Rgb(96, 94, 96), ArtColor.Rgb(198, 198, 202)),
            DecorKind.Boulder => Pick(safe, ArtColor.Rgb(46, 44, 46), ArtColor.Rgb(116, 114, 116), ArtColor.Rgb(166, 164, 166), ArtColor.Rgb(86, 84, 86), ArtColor.Rgb(190, 190, 194)),
            DecorKind.Flowers => Pick(safe, ArtColor.Rgb(32, 82, 40), ArtColor.Rgb(86, 172, 84), ArtColor.Rgb(132, 212, 120), ArtColor.Rgb(246, 232, 118), ArtColor.Rgb(238, 116, 156)),
            DecorKind.IceShard => Pick(safe, ArtColor.Rgb(108, 152, 190), ArtColor.Rgb(198, 228, 244), ArtColor.Rgb(244, 252, 255), ArtColor.Rgb(152, 194, 224), ArtColor.Rgb(255, 255, 255)),
            DecorKind.Hut => Pick(safe, ArtColor.Rgb(48, 34, 24), ArtColor.Rgb(190, 164, 118), ArtColor.Rgb(224, 202, 158), ArtColor.Rgb(146, 96, 52), ArtColor.Rgb(248, 186, 88)),
            DecorKind.House => Pick(safe, ArtColor.Rgb(52, 36, 28), ArtColor.Rgb(226, 210, 176), ArtColor.Rgb(248, 238, 214), ArtColor.Rgb(176, 74, 56), ArtColor.Rgb(94, 150, 200)),
            DecorKind.StoneHouse => Pick(safe, ArtColor.Rgb(44, 42, 44), ArtColor.Rgb(198, 196, 190), ArtColor.Rgb(228, 226, 220), ArtColor.Rgb(120, 92, 76), ArtColor.Rgb(240, 190, 96)),
            DecorKind.Tower => Pick(safe, ArtColor.Rgb(40, 40, 44), ArtColor.Rgb(186, 184, 180), ArtColor.Rgb(222, 220, 216), ArtColor.Rgb(148, 60, 58), ArtColor.Rgb(244, 216, 128)),
            DecorKind.Castle => Pick(safe, ArtColor.Rgb(38, 38, 42), ArtColor.Rgb(192, 190, 186), ArtColor.Rgb(230, 228, 224), ArtColor.Rgb(126, 52, 54), ArtColor.Rgb(246, 220, 132)),
            DecorKind.Farm => Pick(safe, ArtColor.Rgb(62, 46, 30), ArtColor.Rgb(178, 142, 82), ArtColor.Rgb(216, 186, 118), ArtColor.Rgb(228, 200, 96), ArtColor.Rgb(140, 108, 66)),
            _ => Pick(safe, ArtColor.Rgb(40, 40, 40), ArtColor.Rgb(120, 120, 120), ArtColor.Rgb(180, 180, 180), ArtColor.Rgb(90, 90, 90), ArtColor.Rgb(200, 200, 200)),
        };
    }

    /// <summary>Заполняет буфер номерами слотов для одного спрайта.</summary>
    public static void Build(DecorKind kind, int variant, Span<byte> sprite)
    {
        if (sprite.Length < Pixels)
        {
            throw new ArgumentException("Буфер спрайта меньше 16x16.", nameof(sprite));
        }

        sprite[..Pixels].Clear();

        int safe = ((variant % Variants) + Variants) % Variants;
        int salt = (((int)kind + 1) * 733) + (safe * 97);

        switch (kind)
        {
            case DecorKind.PineTree:
                Pine(sprite, safe);
                break;
            case DecorKind.BroadTree:
                Broad(sprite, safe);
                break;
            case DecorKind.PalmTree:
                Palm(sprite, safe);
                break;
            case DecorKind.DeadTree:
                Dead(sprite, safe);
                break;
            case DecorKind.Bush:
                BushShape(sprite, safe);
                break;
            case DecorKind.Cactus:
                CactusShape(sprite, safe);
                break;
            case DecorKind.Reeds:
                ReedsShape(sprite, salt);
                break;
            case DecorKind.Rock:
                RockShape(sprite, safe, 3);
                break;
            case DecorKind.Boulder:
                RockShape(sprite, safe, 5);
                break;
            case DecorKind.Flowers:
                FlowersShape(sprite, salt);
                break;
            case DecorKind.IceShard:
                IceShape(sprite, safe);
                break;
            case DecorKind.Hut:
                HutShape(sprite, safe);
                break;
            case DecorKind.House:
                HouseShape(sprite, safe, false);
                break;
            case DecorKind.StoneHouse:
                HouseShape(sprite, safe, true);
                break;
            case DecorKind.Tower:
                TowerShape(sprite, safe);
                break;
            case DecorKind.Castle:
                CastleShape(sprite, safe);
                break;
            case DecorKind.Farm:
                FarmShape(sprite, safe);
                break;
            default:
                return;
        }

        AddOutline(sprite);
    }

    /// <summary>Насколько часто на тайле этого биома вообще что-то растёт, в процентах.</summary>
    public static int DensityPercent(Biome biome)
    {
        return biome switch
        {
            Biome.TemperateForest => 82,
            Biome.Rainforest => 88,
            Biome.Taiga => 76,
            Biome.Shrubland => 34,
            Biome.Grassland => 14,
            Biome.Savanna => 16,
            Biome.Steppe => 10,
            Biome.Marsh => 44,
            Biome.Desert => 8,
            Biome.Beach => 5,
            Biome.Tundra => 12,
            Biome.Mountain => 26,
            Biome.Peak => 10,
            Biome.Glacier => 6,
            _ => 0,
        };
    }

    /// <summary>Что именно вырастет на тайле. Решение зависит только от биома и хеша клетки.</summary>
    public static DecorKind NatureFor(Biome biome, uint hash)
    {
        uint roll = hash % 100u;
        return biome switch
        {
            Biome.TemperateForest => roll < 62u ? DecorKind.BroadTree : (roll < 92u ? DecorKind.PineTree : DecorKind.Bush),
            Biome.Rainforest => roll < 55u ? DecorKind.BroadTree : (roll < 90u ? DecorKind.PalmTree : DecorKind.Bush),
            Biome.Taiga => roll < 84u ? DecorKind.PineTree : (roll < 94u ? DecorKind.DeadTree : DecorKind.Rock),
            Biome.Shrubland => roll < 68u ? DecorKind.Bush : (roll < 88u ? DecorKind.BroadTree : DecorKind.Rock),
            Biome.Grassland => roll < 52u ? DecorKind.Flowers : (roll < 82u ? DecorKind.Bush : DecorKind.BroadTree),
            Biome.Savanna => roll < 46u ? DecorKind.Bush : (roll < 78u ? DecorKind.BroadTree : DecorKind.Rock),
            Biome.Steppe => roll < 64u ? DecorKind.Bush : DecorKind.Rock,
            Biome.Marsh => roll < 74u ? DecorKind.Reeds : DecorKind.DeadTree,
            Biome.Desert => roll < 56u ? DecorKind.Cactus : DecorKind.Rock,
            Biome.Beach => roll < 70u ? DecorKind.PalmTree : DecorKind.Rock,
            Biome.Tundra => roll < 50u ? DecorKind.Rock : (roll < 80u ? DecorKind.DeadTree : DecorKind.Bush),
            Biome.Mountain => roll < 58u ? DecorKind.Boulder : (roll < 88u ? DecorKind.Rock : DecorKind.PineTree),
            Biome.Peak => roll < 60u ? DecorKind.IceShard : DecorKind.Boulder,
            Biome.Glacier => DecorKind.IceShard,
            _ => DecorKind.None,
        };
    }

    /// <summary>Как выглядит постройка: уровень поселения 0 лагерь, 1 деревня, 2 город.</summary>
    public static DecorKind BuildingFor(int era, int level)
    {
        if (level <= 0)
        {
            return era >= 4 ? DecorKind.House : DecorKind.Hut;
        }

        if (level == 1)
        {
            if (era >= 6)
            {
                return DecorKind.Tower;
            }

            return era >= 3 ? DecorKind.StoneHouse : DecorKind.House;
        }

        if (era >= 4)
        {
            return DecorKind.Castle;
        }

        return era >= 2 ? DecorKind.Tower : DecorKind.StoneHouse;
    }

    // ---------------------------------------------------------------- рисование

    /// <summary>Ель: три яруса треугольником, ствол снизу, блик на левой стороне.</summary>
    private static void Pine(Span<byte> sprite, int variant)
    {
        int top = 1 + variant;
        Rect(sprite, 7, 12, 2, 4, 4);

        for (int tier = 0; tier < 3; tier++)
        {
            int baseY = top + 3 + (tier * 3);
            int half = 2 + tier;
            for (int y = baseY - 3; y <= baseY; y++)
            {
                int spread = half - (baseY - y);
                if (spread < 0)
                {
                    continue;
                }

                for (int x = 8 - spread; x <= 8 + spread; x++)
                {
                    Set(sprite, x, y, (byte)(x <= 8 - spread + 1 ? 3 : 2));
                }
            }
        }
    }

    /// <summary>Лиственное дерево: круглая крона, блик слева сверху, иногда плоды.</summary>
    private static void Broad(Span<byte> sprite, int variant)
    {
        int radius = 4 + (variant == 2 ? 1 : 0);
        int cy = 7 - (variant == 1 ? 1 : 0);

        Rect(sprite, 7, cy + radius - 1, 2, Size - (cy + radius) + 1, 4);
        Disc(sprite, 8, cy, radius, 2);
        Disc(sprite, 6, cy - 2, radius - 2, 3);

        if (variant == 0)
        {
            Set(sprite, 10, cy + 1, 5);
            Set(sprite, 6, cy + 2, 5);
        }
    }

    /// <summary>Палма: изогнутый ствол и пять листьев веером, снизу два кокоса.</summary>
    private static void Palm(Span<byte> sprite, int variant)
    {
        int lean = variant == 1 ? 1 : (variant == 2 ? -1 : 0);
        int topX = 8 + (lean * 3);
        int topY = 3;

        for (int y = Size - 1; y >= topY; y--)
        {
            int t = (Size - 1 - y) * (topX - 8) / Math.Max(1, Size - 1 - topY);
            Set(sprite, 8 + t, y, 4);
            Set(sprite, 9 + t, y, 4);
        }

        int[] dx = { -5, -4, 0, 4, 5 };
        int[] dy = { 1, -2, -3, -2, 1 };
        for (int leaf = 0; leaf < dx.Length; leaf++)
        {
            int ex = topX + dx[leaf];
            int ey = topY + dy[leaf];
            Line(sprite, topX, topY, ex, ey, (byte)(leaf % 2 == 0 ? 2 : 3));
            Set(sprite, ex, ey + 1, 2);
        }

        Set(sprite, topX - 1, topY + 2, 5);
        Set(sprite, topX + 1, topY + 2, 5);
    }

    /// <summary>Сухое дерево: ствол и две ветки вверх.</summary>
    private static void Dead(Span<byte> sprite, int variant)
    {
        int top = 4 + variant;
        Rect(sprite, 8, top, 1, Size - top, 4);
        Line(sprite, 8, top + 3, 5, top, 2);
        Line(sprite, 8, top + 5, 12, top + 2, 2);
        Set(sprite, 5, top - 1, 3);
        Set(sprite, 12, top + 1, 3);
    }

    /// <summary>Куст: два кома листвы с бликом и редкой ягодой.</summary>
    private static void BushShape(Span<byte> sprite, int variant)
    {
        int cy = 11;
        Disc(sprite, 6, cy, 3, 2);
        Disc(sprite, 10, cy + 1, 3 - (variant == 0 ? 1 : 0), 2);
        Disc(sprite, 5, cy - 1, 1, 3);
        if (variant != 2)
        {
            Set(sprite, 9, cy, 5);
        }
    }

    /// <summary>Кактус: столб с одной или двумя руками и цветком на макушке.</summary>
    private static void CactusShape(Span<byte> sprite, int variant)
    {
        Rect(sprite, 7, 5, 3, 11, 2);
        Rect(sprite, 7, 5, 1, 11, 3);

        if (variant != 1)
        {
            Rect(sprite, 4, 9, 3, 2, 2);
            Rect(sprite, 4, 7, 2, 3, 2);
        }

        if (variant != 0)
        {
            Rect(sprite, 10, 11, 3, 2, 2);
            Rect(sprite, 11, 9, 2, 3, 2);
        }

        Set(sprite, 8, 4, 5);
    }

    /// <summary>Камыш: пучок стеблей с бурыми метёлками.</summary>
    private static void ReedsShape(Span<byte> sprite, int salt)
    {
        for (int stem = 0; stem < 5; stem++)
        {
            int x = 4 + stem * 2;
            int height = 6 + (int)(Hash(stem, 1, salt) % 5u);
            for (int y = Size - 1; y >= Size - height; y--)
            {
                Set(sprite, x, y, (byte)(stem % 2 == 0 ? 2 : 3));
            }

            Set(sprite, x, Size - height - 1, 4);
            Set(sprite, x, Size - height - 2, 5);
        }
    }

    /// <summary>Камень: скруглённая глыба с бликом на левой грани.</summary>
    private static void RockShape(Span<byte> sprite, int variant, int radius)
    {
        int cy = Size - radius - 1;
        int cx = 8 + (variant == 1 ? -1 : (variant == 2 ? 1 : 0));
        Disc(sprite, cx, cy, radius, 2);
        Rect(sprite, cx - radius, cy, (radius * 2) + 1, radius + 1, 2);
        Disc(sprite, cx - (radius / 2), cy - (radius / 2), Math.Max(1, radius - 2), 3);
        Rect(sprite, cx - radius, Size - 2, (radius * 2) + 1, 2, 4);
    }

    /// <summary>Цветы: три травинки с разноцветными шапками.</summary>
    private static void FlowersShape(Span<byte> sprite, int salt)
    {
        for (int flower = 0; flower < 4; flower++)
        {
            int x = 3 + (int)(Hash(flower, 2, salt) % 11u);
            int height = 4 + (int)(Hash(flower, 3, salt) % 4u);
            for (int y = Size - 1; y >= Size - height; y--)
            {
                Set(sprite, x, y, 2);
            }

            byte cap = (byte)(Hash(flower, 4, salt) % 2u == 0u ? 4 : 5);
            Set(sprite, x, Size - height - 1, cap);
            Set(sprite, x - 1, Size - height, cap);
            Set(sprite, x + 1, Size - height, cap);
        }
    }

    /// <summary>Льдина: острый кристалл с белым ребром.</summary>
    private static void IceShape(Span<byte> sprite, int variant)
    {
        int top = 4 + variant;
        for (int y = Size - 1; y >= top; y--)
        {
            int half = (Size - 1 - y) / 2;
            for (int x = 8 - half; x <= 8 + half; x++)
            {
                Set(sprite, x, y, (byte)(x == 8 - half ? 3 : 2));
            }
        }

        Set(sprite, 8, top - 1, 5);
    }

    /// <summary>Шалаш: соломенная двускатная крыша до земли и тёмный вход.</summary>
    private static void HutShape(Span<byte> sprite, int variant)
    {
        int top = 4;
        for (int y = top; y < Size - 1; y++)
        {
            int half = y - top + 1;
            for (int x = 8 - half; x <= 8 + half; x++)
            {
                Set(sprite, x, y, (byte)(x < 8 ? 4 : 2));
            }
        }

        Rect(sprite, 7, Size - 5, 3, 4, 1);
        if (variant == 2)
        {
            Set(sprite, 8, top - 2, 5);
        }
    }

    /// <summary>Дом: стена, двускатная крыша, дверь и окно. Каменный вариант серее и выше.</summary>
    private static void HouseShape(Span<byte> sprite, int variant, bool stone)
    {
        int wallTop = stone ? 7 : 8;
        Rect(sprite, 3, wallTop, 11, Size - wallTop - 1, 2);
        Rect(sprite, 3, wallTop, 11, 1, 3);

        for (int y = 0; y < wallTop; y++)
        {
            int half = y + 2;
            if (half > 7)
            {
                half = 7;
            }

            for (int x = 8 - half; x <= 8 + half; x++)
            {
                Set(sprite, x, wallTop - 1 - y, 4);
            }
        }

        Rect(sprite, 7, Size - 5, 3, 4, 1);
        Set(sprite, 5, wallTop + 2, 5);
        Set(sprite, 11, wallTop + 2, 5);

        if (variant != 0)
        {
            Rect(sprite, 11, 1, 2, 4, stone ? (byte)2 : (byte)4);
        }
    }

    /// <summary>Башня: узкий столб с зубцами и флагом.</summary>
    private static void TowerShape(Span<byte> sprite, int variant)
    {
        Rect(sprite, 5, 4, 7, 12, 2);
        Rect(sprite, 5, 4, 1, 12, 3);

        for (int x = 4; x <= 12; x += 2)
        {
            Rect(sprite, x, 2, 2, 2, 2);
        }

        Rect(sprite, 7, 10, 3, 6, 1);
        Set(sprite, 8, 1, 4);
        if (variant != 1)
        {
            Rect(sprite, 9, 0, 3, 2, 4);
        }

        Set(sprite, 6, 7, 5);
        Set(sprite, 10, 7, 5);
    }

    /// <summary>Замок: две башни, стена с воротами, флаги на зубцах.</summary>
    private static void CastleShape(Span<byte> sprite, int variant)
    {
        Rect(sprite, 1, 7, 14, 9, 2);
        Rect(sprite, 1, 7, 14, 1, 3);
        Rect(sprite, 0, 3, 4, 13, 2);
        Rect(sprite, 12, 3, 4, 13, 2);
        Rect(sprite, 0, 3, 1, 13, 3);
        Rect(sprite, 12, 3, 1, 13, 3);

        for (int x = 0; x <= 14; x += 3)
        {
            Rect(sprite, x, 5, 2, 2, 2);
        }

        Rect(sprite, 6, 10, 4, 6, 1);
        Rect(sprite, 1, 1, 2, 3, 4);
        Rect(sprite, 13, 1, 2, 3, 4);
        Set(sprite, 5, 9, 5);
        Set(sprite, 10, 9, 5);

        if (variant == 2)
        {
            Rect(sprite, 7, 8, 2, 2, 5);
        }
    }

    /// <summary>Поле: борозды с колосьями и жердь по краю.</summary>
    private static void FarmShape(Span<byte> sprite, int variant)
    {
        Rect(sprite, 1, 8, 14, 8, 2);

        for (int row = 0; row < 4; row++)
        {
            int y = 9 + (row * 2);
            for (int x = 2; x < 14; x++)
            {
                Set(sprite, x, y, (byte)(x % 2 == 0 ? 4 : 3));
            }
        }

        Rect(sprite, 1, 6, 1, 10, 1);
        Rect(sprite, 14, 6, 1, 10, 1);
        if (variant != 0)
        {
            Rect(sprite, 1, 6, 14, 1, 1);
        }
    }

    // ---------------------------------------------------------------- утилиты

    /// <summary>Тёмный контур в один пиксель вокруг силуэта. Даёт мультяшный вид.</summary>
    private static void AddOutline(Span<byte> sprite)
    {
        Span<byte> copy = stackalloc byte[Pixels];
        sprite[..Pixels].CopyTo(copy);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (copy[(y * Size) + x] != 0)
                {
                    continue;
                }

                if (Filled(copy, x - 1, y) || Filled(copy, x + 1, y) || Filled(copy, x, y - 1) || Filled(copy, x, y + 1))
                {
                    sprite[(y * Size) + x] = 1;
                }
            }
        }
    }

    private static bool Filled(Span<byte> sprite, int x, int y)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
        {
            return false;
        }

        return sprite[(y * Size) + x] != 0;
    }

    private static void Set(Span<byte> sprite, int x, int y, byte slot)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
        {
            return;
        }

        sprite[(y * Size) + x] = slot;
    }

    private static void Rect(Span<byte> sprite, int x, int y, int width, int height, byte slot)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                Set(sprite, x + dx, y + dy, slot);
            }
        }
    }

    private static void Disc(Span<byte> sprite, int cx, int cy, int radius, byte slot)
    {
        if (radius < 1)
        {
            radius = 1;
        }

        int limit = (radius * radius) + radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) <= limit)
                {
                    Set(sprite, cx + dx, cy + dy, slot);
                }
            }
        }
    }

    private static void Line(Span<byte> sprite, int x0, int y0, int x1, int y1, byte slot)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;
        int x = x0;
        int y = y0;

        for (int guard = 0; guard < Size * 4; guard++)
        {
            Set(sprite, x, y, slot);
            if (x == x1 && y == y1)
            {
                return;
            }

            int doubled = error * 2;
            if (doubled > -dy)
            {
                error -= dy;
                x += sx;
            }

            if (doubled < dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static ArtColor Pick(int slot, ArtColor outline, ArtColor main, ArtColor light, ArtColor second, ArtColor accent)
    {
        return slot switch
        {
            1 => outline,
            2 => main,
            3 => light,
            4 => second,
            5 => accent,
            _ => main,
        };
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
