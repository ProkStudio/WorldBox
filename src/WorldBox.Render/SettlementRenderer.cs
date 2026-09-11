using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Поселения вблизи: вместо цветного квадратика рисуется постройка из атласа декора,
/// под ней — мостовая с улицами, а над ней флажок цвета народа. Вид постройки зависит
/// от эпохи и уровня поселения: шалаш, дом, каменный дом, башня, замок. Издалека работает
/// значок из <see cref="TerritoryRenderer"/>: спрайт размером в пиксель всё равно не прочитать.
///
/// Мостовая — как в оригинале: город стоит не на траве, а на вымощенном дворе, из которого
/// в четыре стороны выходят дороги. У лагеря вместо камня утоптанная земля.
/// </summary>
public sealed class SettlementRenderer
{
    /// <summary>Ниже этого зума постройки не рисуются, остаются значки.</summary>
    public const float MinZoom = 12f;

    /// <summary>Мостовая включается позже построек: мелкие камни издали превращаются в серую кашу.</summary>
    public const float PavingZoom = 16f;

    /// <summary>Размер одного камня мостовой в тайлах: четыре на четыре камня на клетку.</summary>
    private const float Cell = 0.25f;

    private static readonly Color ShadowColor = new Color(12, 16, 20) * 0.35f;
    private static readonly Color PoleColor = new Color(66, 48, 34);
    private static readonly Color StoneColor = new Color(152, 148, 140);
    private static readonly Color StoneDark = new Color(116, 112, 106);
    private static readonly Color StoneLight = new Color(182, 178, 170);
    private static readonly Color DirtColor = new Color(146, 118, 84);
    private static readonly Color DirtDark = new Color(112, 90, 64);

    private readonly DecorAtlas _atlas;

    public SettlementRenderer(DecorAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
    }

    public bool Visible { get; set; } = true;

    /// <summary>Сколько построек ушло в последний кадр.</summary>
    public int DrawnBuildings { get; private set; }

    /// <summary>Сколько камней мостовой ушло в последний кадр. Нужно для замеров: это самая толстая часть.</summary>
    public int DrawnPavingCells { get; private set; }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        TribeStore tribes,
        SettlementStore settlements)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);

        DrawnBuildings = 0;
        DrawnPavingCells = 0;
        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 6);

        Texture2D texture = _atlas.Texture;
        Texture2D pixel = primitives.Pixel;
        bool paving = camera.Zoom >= PavingZoom;
        int high = settlements.HighWater;
        int drawn = 0;
        int cells = 0;

        // Первый проход: вся земля. Иначе двор соседа ложится поверх уже нарисованного дома.
        if (paving)
        {
            for (int i = 0; i < high; i++)
            {
                if (!settlements.Alive[i])
                {
                    continue;
                }

                int x = settlements.X[i];
                int y = settlements.Y[i];
                if (x < minX || x > maxX || y < minY || y > maxY)
                {
                    continue;
                }

                if (!tribes.IsAlive(settlements.Tribe[i]))
                {
                    continue;
                }

                cells += DrawPaving(batch, pixel, i, x, y, settlements.Level[i]);
            }
        }

        for (int i = 0; i < high; i++)
        {
            if (!settlements.Alive[i])
            {
                continue;
            }

            int x = settlements.X[i];
            int y = settlements.Y[i];

            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                continue;
            }

            short tribe = settlements.Tribe[i];
            if (!tribes.IsAlive(tribe))
            {
                continue;
            }

            byte level = settlements.Level[i];
            int era = tribes.Era[tribe];
            DecorKind kind = DecorArt.BuildingFor(era, level);
            Rectangle source = _atlas.Source(kind, (i + era) % DecorArt.Variants);

            float tiles = level == SettlementStore.Town ? 3.2f : (level == SettlementStore.Village ? 2.4f : 1.7f);
            float scale = tiles / DecorAtlas.SpriteSize;
            var size = new Vector2(scale, scale);

            float centerX = x + 0.5f;
            float baseY = y + 1.1f;
            var position = new Vector2(centerX - (tiles * 0.5f), baseY - tiles);

            // Мягкая тень под постройкой: без неё дом выглядит наклейкой.
            batch.Draw(
                pixel,
                new Vector2(centerX - (tiles * 0.42f), baseY - (tiles * 0.14f)),
                null,
                ShadowColor,
                0f,
                Vector2.Zero,
                new Vector2(tiles * 0.84f, tiles * 0.2f),
                SpriteEffects.None,
                0f);

            batch.Draw(texture, position, source, Color.White, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);

            DrawFlag(batch, pixel, tribes.ColorIndex[tribe], centerX, position.Y, tiles);
            drawn++;
        }

        DrawnBuildings = drawn;
        DrawnPavingCells = cells;
    }

    /// <summary>
    /// Двор и улицы. Город даёт двор радиусом два тайла и три тайла дороги в каждую сторону,
    /// деревня — поменьше, лагерь — только утоптанный пятачок.
    /// </summary>
    private static int DrawPaving(SpriteBatch batch, Texture2D pixel, int index, int x, int y, byte level)
    {
        int radius = level == SettlementStore.Town ? 2 : (level == SettlementStore.Village ? 1 : 0);
        int road = level == SettlementStore.Town ? 3 : (level == SettlementStore.Village ? 2 : 0);
        bool stone = level > SettlementStore.Camp;
        int salt = (index + 1) * 7919;
        int cells = 0;

        for (int ty = -radius; ty <= radius; ty++)
        {
            for (int tx = -radius; tx <= radius; tx++)
            {
                // Скруглённый двор: углы квадрата срезаны, иначе город выглядит плиткой.
                if (Math.Abs(tx) + Math.Abs(ty) > radius + 1)
                {
                    continue;
                }

                cells += DrawTilePaving(batch, pixel, x + tx, y + ty, salt, stone);
            }
        }

        for (int step = radius + 1; step <= radius + road; step++)
        {
            cells += DrawTilePaving(batch, pixel, x + step, y, salt, stone);
            cells += DrawTilePaving(batch, pixel, x - step, y, salt, stone);
            cells += DrawTilePaving(batch, pixel, x, y + step, salt, stone);
            cells += DrawTilePaving(batch, pixel, x, y - step, salt, stone);
        }

        return cells;
    }

    /// <summary>Одна клетка мостовой: шестнадцать камней с разным тоном и редкими выбоинами.</summary>
    private static int DrawTilePaving(SpriteBatch batch, Texture2D pixel, int tileX, int tileY, int salt, bool stone)
    {
        int drawn = 0;

        for (int cy = 0; cy < 4; cy++)
        {
            for (int cx = 0; cx < 4; cx++)
            {
                uint h = Hash((tileX * 4) + cx, (tileY * 4) + cy, salt);

                // Выбоина: сквозь камень прорастает земля, край двора перестаёт быть линейкой.
                if (h % 14u == 0u)
                {
                    continue;
                }

                Color color;
                if (stone)
                {
                    uint tone = h % 7u;
                    color = tone == 0u ? StoneDark : (tone <= 2u ? StoneLight : StoneColor);
                }
                else
                {
                    color = h % 5u == 0u ? DirtDark : DirtColor;
                }

                batch.Draw(
                    pixel,
                    new Vector2(tileX + (cx * Cell), tileY + (cy * Cell)),
                    null,
                    color,
                    0f,
                    Vector2.Zero,
                    new Vector2(Cell, Cell),
                    SpriteEffects.None,
                    0f);
                drawn++;
            }
        }

        return drawn;
    }

    /// <summary>Флажок над крышей: по нему видно, чей это город, даже если постройки одинаковые.</summary>
    private static void DrawFlag(
        SpriteBatch batch,
        Texture2D pixel,
        byte colorIndex,
        float centerX,
        float roofY,
        float tiles)
    {
        float pole = tiles * 0.42f;
        float thickness = MathF.Max(0.06f, tiles * 0.06f);
        float flagWidth = tiles * 0.32f;
        float flagHeight = tiles * 0.2f;
        float poleX = centerX + (tiles * 0.24f);
        float poleTop = roofY - pole;

        batch.Draw(
            pixel,
            new Vector2(poleX, poleTop),
            null,
            PoleColor,
            0f,
            Vector2.Zero,
            new Vector2(thickness, pole + (tiles * 0.1f)),
            SpriteEffects.None,
            0f);

        batch.Draw(
            pixel,
            new Vector2(poleX + thickness, poleTop),
            null,
            TribePalette.Of(colorIndex),
            0f,
            Vector2.Zero,
            new Vector2(flagWidth, flagHeight),
            SpriteEffects.None,
            0f);
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
