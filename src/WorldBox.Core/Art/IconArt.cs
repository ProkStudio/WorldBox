namespace WorldBox.Core.Art;

/// <summary>Значки интерфейса. Порядок важен: по нему строится атлас.</summary>
public enum IconKind : byte
{
    None = 0,
    Play,
    Pause,
    Speed1,
    Speed2,
    Speed3,
    Speed4,
    MapTerrain,
    MapHeight,
    MapTemperature,
    MapMoisture,
    MapFertility,
    MapResources,
    Tribes,
    Market,
    Era,
    Chronicle,
    Legend,
    Borders,
    NewWorld,
    Inspect,
    FitWorld,
    People,
    Settlement,
    Food,
    Warning,
    Star,
    Gear,
    War,
}

/// <summary>
/// Процедурные значки 16x16 без единого файла картинки в репозитории. Результат — номера
/// слотов: 0 пусто, 1 контур, 2 основной, 3 блик, 4 акцент. Контур обводится автоматически,
/// поэтому значки читаются и на тёмной панели, и поверх карты.
/// </summary>
public static class IconArt
{
    public const int Size = 16;
    public const int Pixels = Size * Size;
    public const int Slots = 5;

    /// <summary>Сколько значков описано, включая пустой.</summary>
    public static int KindCount => 29;

    private static readonly ArtColor Ink = ArtColor.Rgb(14, 18, 26);
    private static readonly ArtColor Paper = ArtColor.Rgb(230, 236, 245);
    private static readonly ArtColor PaperLight = ArtColor.Rgb(255, 255, 255);

    public static ArtColor Color(IconKind kind, int slot)
    {
        int safe = slot < 0 ? 0 : slot % Slots;
        return kind switch
        {
            IconKind.Play => Pick(safe, ArtColor.Rgb(126, 214, 106), ArtColor.Rgb(180, 240, 150)),
            IconKind.Pause => Pick(safe, ArtColor.Rgb(244, 200, 96), ArtColor.Rgb(252, 228, 158)),
            IconKind.Speed1 => Pick(safe, Paper, ArtColor.Rgb(126, 214, 106)),
            IconKind.Speed2 => Pick(safe, Paper, ArtColor.Rgb(160, 220, 110)),
            IconKind.Speed3 => Pick(safe, Paper, ArtColor.Rgb(244, 200, 96)),
            IconKind.Speed4 => Pick(safe, Paper, ArtColor.Rgb(238, 128, 96)),
            IconKind.MapTerrain => Pick(safe, ArtColor.Rgb(108, 196, 96), ArtColor.Rgb(150, 128, 104)),
            IconKind.MapHeight => Pick(safe, ArtColor.Rgb(200, 192, 122), ArtColor.Rgb(158, 120, 88)),
            IconKind.MapTemperature => Pick(safe, Paper, ArtColor.Rgb(232, 84, 64)),
            IconKind.MapMoisture => Pick(safe, ArtColor.Rgb(86, 168, 236), ArtColor.Rgb(168, 220, 252)),
            IconKind.MapFertility => Pick(safe, ArtColor.Rgb(110, 206, 104), ArtColor.Rgb(186, 142, 84)),
            IconKind.MapResources => Pick(safe, ArtColor.Rgb(236, 152, 84), ArtColor.Rgb(120, 236, 156)),
            IconKind.Tribes => Pick(safe, ArtColor.Rgb(242, 226, 198), ArtColor.Rgb(226, 92, 84)),
            IconKind.Market => Pick(safe, ArtColor.Rgb(246, 206, 112), ArtColor.Rgb(176, 124, 44)),
            IconKind.Era => Pick(safe, Paper, ArtColor.Rgb(240, 190, 96)),
            IconKind.Chronicle => Pick(safe, ArtColor.Rgb(240, 232, 214), ArtColor.Rgb(176, 74, 56)),
            IconKind.Legend => Pick(safe, Paper, ArtColor.Rgb(120, 180, 240)),
            IconKind.Borders => Pick(safe, ArtColor.Rgb(240, 196, 96), ArtColor.Rgb(226, 92, 84)),
            IconKind.NewWorld => Pick(safe, ArtColor.Rgb(96, 190, 236), ArtColor.Rgb(108, 196, 96)),
            IconKind.Inspect => Pick(safe, Paper, ArtColor.Rgb(120, 180, 240)),
            IconKind.FitWorld => Pick(safe, Paper, ArtColor.Rgb(150, 166, 190)),
            IconKind.People => Pick(safe, ArtColor.Rgb(242, 226, 198), ArtColor.Rgb(86, 158, 232)),
            IconKind.Settlement => Pick(safe, ArtColor.Rgb(230, 214, 180), ArtColor.Rgb(176, 74, 56)),
            IconKind.Food => Pick(safe, ArtColor.Rgb(242, 208, 112), ArtColor.Rgb(150, 110, 50)),
            IconKind.Warning => Pick(safe, ArtColor.Rgb(244, 196, 72), ArtColor.Rgb(60, 44, 16)),
            IconKind.Star => Pick(safe, ArtColor.Rgb(250, 214, 96), ArtColor.Rgb(255, 244, 190)),
            IconKind.Gear => Pick(safe, ArtColor.Rgb(202, 208, 218), ArtColor.Rgb(128, 138, 154)),
            IconKind.War => Pick(safe, ArtColor.Rgb(206, 214, 226), ArtColor.Rgb(176, 92, 52)),
            _ => Pick(safe, Paper, PaperLight),
        };
    }

    /// <summary>Заполняет буфер номерами слотов для одного значка.</summary>
    public static void Build(IconKind kind, Span<byte> icon)
    {
        if (icon.Length < Pixels)
        {
            throw new ArgumentException("Буфер значка меньше 16x16.", nameof(icon));
        }

        icon[..Pixels].Clear();

        switch (kind)
        {
            case IconKind.Play:
                for (int y = 2; y <= 13; y++)
                {
                    int reach = 6 - (int)(MathF.Abs(y - 7.5f) * 0.9f);
                    Rect(icon, 5, y, Math.Max(1, reach), 1, 2);
                }

                break;
            case IconKind.Pause:
                Rect(icon, 4, 3, 3, 10, 2);
                Rect(icon, 9, 3, 3, 10, 2);
                break;
            case IconKind.Speed1:
                Chevron(icon, 6, 4);
                break;
            case IconKind.Speed2:
                Chevron(icon, 3, 4);
                Chevron(icon, 8, 4);
                break;
            case IconKind.Speed3:
                Chevron(icon, 1, 4);
                Chevron(icon, 6, 4);
                Chevron(icon, 11, 4);
                break;
            case IconKind.Speed4:
                Chevron(icon, 1, 4);
                Chevron(icon, 5, 4);
                Chevron(icon, 9, 4);
                Rect(icon, 13, 4, 2, 8, 4);
                break;
            case IconKind.MapTerrain:
                Rect(icon, 1, 10, 14, 4, 2);
                Triangle(icon, 5, 3, 5, 4);
                Rect(icon, 11, 7, 2, 4, 4);
                Disc(icon, 12, 6, 2, 2);
                break;
            case IconKind.MapHeight:
                Rect(icon, 2, 11, 12, 2, 2);
                Rect(icon, 4, 8, 8, 2, 2);
                Rect(icon, 6, 5, 4, 2, 4);
                Rect(icon, 7, 2, 2, 2, 4);
                break;
            case IconKind.MapTemperature:
                Rect(icon, 7, 2, 2, 8, 2);
                Disc(icon, 8, 12, 3, 4);
                Rect(icon, 7, 6, 2, 5, 4);
                break;
            case IconKind.MapMoisture:
                Triangle(icon, 8, 2, 4, 4);
                Disc(icon, 8, 10, 4, 2);
                Disc(icon, 6, 9, 1, 3);
                break;
            case IconKind.MapFertility:
                Rect(icon, 7, 6, 2, 8, 4);
                Disc(icon, 5, 6, 2, 2);
                Disc(icon, 11, 5, 2, 2);
                Disc(icon, 8, 3, 2, 2);
                break;
            case IconKind.MapResources:
                Triangle(icon, 8, 2, 6, 2);
                for (int y = 8; y <= 13; y++)
                {
                    int half = 13 - y;
                    Rect(icon, 8 - half, y, (half * 2) + 1, 1, 2);
                }

                Rect(icon, 7, 5, 2, 2, 4);
                break;
            case IconKind.Tribes:
                Disc(icon, 4, 5, 2, 2);
                Disc(icon, 11, 5, 2, 2);
                Disc(icon, 8, 7, 2, 4);
                Rect(icon, 2, 9, 4, 4, 2);
                Rect(icon, 10, 9, 4, 4, 2);
                Rect(icon, 6, 11, 4, 3, 4);
                break;
            case IconKind.Market:
                Disc(icon, 6, 7, 4, 2);
                Disc(icon, 10, 10, 4, 4);
                Rect(icon, 5, 6, 3, 1, 4);
                break;
            case IconKind.Era:
                Rect(icon, 3, 2, 10, 2, 2);
                Rect(icon, 3, 12, 10, 2, 2);
                for (int y = 4; y <= 7; y++)
                {
                    int half = 8 - y;
                    Rect(icon, 8 - half, y, (half * 2) + 1, 1, 4);
                }

                for (int y = 8; y <= 11; y++)
                {
                    int half = y - 7;
                    Rect(icon, 8 - half, y, (half * 2) + 1, 1, 4);
                }

                break;
            case IconKind.Chronicle:
                Rect(icon, 2, 3, 6, 10, 2);
                Rect(icon, 9, 3, 6, 10, 2);
                Rect(icon, 8, 2, 1, 12, 4);
                Rect(icon, 3, 6, 4, 1, 4);
                Rect(icon, 10, 6, 4, 1, 4);
                break;
            case IconKind.Legend:
                Rect(icon, 2, 3, 2, 2, 4);
                Rect(icon, 2, 7, 2, 2, 4);
                Rect(icon, 2, 11, 2, 2, 4);
                Rect(icon, 6, 3, 8, 2, 2);
                Rect(icon, 6, 7, 8, 2, 2);
                Rect(icon, 6, 11, 8, 2, 2);
                break;
            case IconKind.Borders:
                for (int x = 2; x <= 13; x += 3)
                {
                    Rect(icon, x, 2, 2, 2, 2);
                    Rect(icon, x, 12, 2, 2, 2);
                }

                for (int y = 2; y <= 13; y += 3)
                {
                    Rect(icon, 2, y, 2, 2, 2);
                    Rect(icon, 12, y, 2, 2, 2);
                }

                Rect(icon, 7, 7, 2, 2, 4);
                break;
            case IconKind.NewWorld:
                Disc(icon, 8, 8, 6, 2);
                Rect(icon, 2, 7, 12, 2, 4);
                Rect(icon, 7, 2, 2, 12, 4);
                break;
            case IconKind.Inspect:
                Ring(icon, 6, 6, 4, 2);
                Rect(icon, 10, 10, 2, 2, 2);
                Rect(icon, 11, 11, 3, 3, 4);
                break;
            case IconKind.FitWorld:
                Rect(icon, 2, 2, 5, 2, 2);
                Rect(icon, 2, 2, 2, 5, 2);
                Rect(icon, 9, 2, 5, 2, 2);
                Rect(icon, 12, 2, 2, 5, 2);
                Rect(icon, 2, 12, 5, 2, 2);
                Rect(icon, 2, 9, 2, 5, 2);
                Rect(icon, 9, 12, 5, 2, 2);
                Rect(icon, 12, 9, 2, 5, 2);
                break;
            case IconKind.People:
                Disc(icon, 8, 4, 3, 2);
                Rect(icon, 5, 8, 6, 6, 2);
                Rect(icon, 5, 8, 6, 2, 4);
                break;
            case IconKind.Settlement:
                for (int y = 3; y <= 7; y++)
                {
                    int half = y - 2;
                    Rect(icon, 8 - half, y, (half * 2) + 1, 1, 4);
                }

                Rect(icon, 4, 8, 9, 6, 2);
                Rect(icon, 7, 10, 3, 4, 4);
                break;
            case IconKind.Food:
                Rect(icon, 7, 4, 2, 10, 4);
                for (int i = 0; i < 4; i++)
                {
                    int y = 4 + (i * 2);
                    Rect(icon, 4, y, 3, 2, 2);
                    Rect(icon, 9, y, 3, 2, 2);
                }

                break;
            case IconKind.Warning:
                Triangle(icon, 8, 2, 7, 2);
                Rect(icon, 7, 7, 2, 4, 4);
                Rect(icon, 7, 12, 2, 2, 4);
                break;
            case IconKind.Star:
                Star(icon);
                break;
            case IconKind.Gear:
                Ring(icon, 8, 8, 5, 2);
                Disc(icon, 8, 8, 2, 4);
                Rect(icon, 7, 1, 2, 3, 2);
                Rect(icon, 7, 12, 2, 3, 2);
                Rect(icon, 1, 7, 3, 2, 2);
                Rect(icon, 12, 7, 3, 2, 2);
                break;
            case IconKind.War:
                // Два скрещённых клинка. Каждый в два пикселя толщиной: в один диагональ рвётся на точки.
                for (int i = 0; i < 10; i++)
                {
                    Set(icon, 3 + i, 12 - i, 2);
                    Set(icon, 4 + i, 12 - i, 2);
                    Set(icon, 12 - i, 12 - i, 2);
                    Set(icon, 11 - i, 12 - i, 2);
                }

                // Рукояти внизу и белые кончики сверху: понятно, где у меча какой конец.
                Rect(icon, 2, 12, 3, 2, 4);
                Rect(icon, 11, 12, 3, 2, 4);
                Set(icon, 13, 3, 3);
                Set(icon, 2, 3, 3);
                break;
            default:
                return;
        }

        AddOutline(icon);
    }

    // ---------------------------------------------------------------- фигуры

    private static void Chevron(Span<byte> icon, int x, int y)
    {
        for (int i = 0; i < 4; i++)
        {
            Set(icon, x + i, y + i, 2);
            Set(icon, x + i, y + i + 1, 2);
            Set(icon, x + i, y + 7 - i, 2);
            Set(icon, x + i, y + 8 - i, 2);
        }
    }

    private static void Triangle(Span<byte> icon, int cx, int top, int height, byte slot)
    {
        for (int row = 0; row < height; row++)
        {
            int half = row;
            Rect(icon, cx - half, top + row, (half * 2) + 1, 1, slot);
        }
    }

    private static void Star(Span<byte> icon)
    {
        int[] widths = { 1, 3, 5, 11, 9, 7, 9, 5 };
        int y = 2;
        foreach (int width in widths)
        {
            int half = width / 2;
            Rect(icon, 8 - half, y, width, 1, 2);
            y++;
        }

        Set(icon, 6, 9, 3);
        Set(icon, 8, 5, 3);
    }

    private static void Ring(Span<byte> icon, int cx, int cy, int radius, byte slot)
    {
        Disc(icon, cx, cy, radius, slot);
        Disc(icon, cx, cy, radius - 2, 0);
    }

    private static void Disc(Span<byte> icon, int cx, int cy, int radius, byte slot)
    {
        if (radius < 1)
        {
            return;
        }

        int limit = (radius * radius) + radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) <= limit)
                {
                    Set(icon, cx + dx, cy + dy, slot);
                }
            }
        }
    }

    private static void Rect(Span<byte> icon, int x, int y, int width, int height, byte slot)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                Set(icon, x + dx, y + dy, slot);
            }
        }
    }

    private static void Set(Span<byte> icon, int x, int y, byte slot)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
        {
            return;
        }

        icon[(y * Size) + x] = slot;
    }

    /// <summary>Контур в один пиксель вокруг значка.</summary>
    private static void AddOutline(Span<byte> icon)
    {
        Span<byte> copy = stackalloc byte[Pixels];
        icon[..Pixels].CopyTo(copy);

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
                    icon[(y * Size) + x] = 1;
                }
            }
        }
    }

    private static bool Filled(Span<byte> icon, int x, int y)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
        {
            return false;
        }

        return icon[(y * Size) + x] != 0;
    }

    private static ArtColor Pick(int slot, ArtColor main, ArtColor accent)
    {
        return slot switch
        {
            1 => Ink,
            2 => main,
            3 => PaperLight,
            4 => accent,
            _ => main,
        };
    }
}
