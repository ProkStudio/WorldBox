using WorldBox.Core.World;

namespace WorldBox.Core.Art;

/// <summary>
/// Маски переходов между биомами. Для каждой стороны тайла и каждого варианта рисуется
/// рваная кромка: 0 пусто, 1 полупрозрачно, 2 плотно. Цвет подставляет слой отрисовки —
/// он берёт цвет соседнего биома, который наползает на этот тайл. Благодаря этому вода
/// не обрывается по клетке, а перетекает в песок, лес в степь и так далее.
/// </summary>
public static class EdgeArt
{
    /// <summary>0 верх, 1 право, 2 низ, 3 лево.</summary>
    public const int Sides = 4;

    public const int Variants = 4;

    /// <summary>Глубже этого кромка не заходит: до четырёх плотных рядов плюс ряд ряби.</summary>
    public const int MaxDepth = 5;

    // Кто на кого наползает. Порядок строк совпадает с перечислением TileTexture.
    private static readonly byte[] Priorities =
    {
        5, // вода
        4, // лёд
        3, // песок
        1, // дюны
        1, // луга
        1, // степь
        0, // лес
        2, // камень
        2, // снег
        3, // болото
    };

    /// <summary>Сколько классов поверхности описано в таблице приоритетов.</summary>
    public static int PriorityCount => Priorities.Length;

    public static int Priority(TileTexture texture)
    {
        int index = (int)texture;
        return index >= 0 && index < Priorities.Length ? Priorities[index] : 0;
    }

    public static int Priority(Biome biome)
    {
        return Priority(TileArt.ClassOf(biome));
    }

    /// <summary>Вариант кромки для клетки и стороны: границы не выглядят штампованными.</summary>
    public static int VariantFor(int x, int y, int side)
    {
        return (int)(Hash(x, y, 4093 + (side * 31)) % Variants);
    }

    /// <summary>Заполняет буфер маской кромки: 0 пусто, 1 полупрозрачно, 2 плотно.</summary>
    public static void Build(int side, int variant, Span<byte> mask)
    {
        if (mask.Length < TileArt.Pixels)
        {
            throw new ArgumentException("Буфер маски меньше 16x16.", nameof(mask));
        }

        mask[..TileArt.Pixels].Clear();

        int safeSide = ((side % Sides) + Sides) % Sides;
        int safeVariant = ((variant % Variants) + Variants) % Variants;
        int salt = (safeSide * 131) + (safeVariant * 977) + 17;

        for (int along = 0; along < TileArt.TileSize; along++)
        {
            int depth = 2 + (int)(Hash(along, safeSide, salt) % 3u);

            for (int step = 0; step < depth; step++)
            {
                Set(mask, safeSide, along, step, 2);
            }

            if (Hash(along, safeSide + 7, salt) % 2u == 0u)
            {
                Set(mask, safeSide, along, depth, 1);
            }
        }
    }

    /// <summary>Кладёт пиксель в системе координат стороны: along вдоль кромки, depth вглубь тайла.</summary>
    private static void Set(Span<byte> mask, int side, int along, int depth, byte value)
    {
        int x;
        int y;

        switch (side)
        {
            case 0:
                x = along;
                y = depth;
                break;
            case 1:
                x = TileArt.TileSize - 1 - depth;
                y = along;
                break;
            case 2:
                x = along;
                y = TileArt.TileSize - 1 - depth;
                break;
            default:
                x = depth;
                y = along;
                break;
        }

        if (x < 0 || y < 0 || x >= TileArt.TileSize || y >= TileArt.TileSize)
        {
            return;
        }

        mask[(y * TileArt.TileSize) + x] = value;
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
