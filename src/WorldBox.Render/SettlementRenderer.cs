using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Поселения вблизи: вместо цветного квадратика рисуется постройка из атласа декора,
/// а над ней флажок цвета народа. Вид постройки зависит от эпохи и уровня поселения:
/// шалаш, дом, каменный дом, башня, замок. Издалека работает значок из
/// <see cref="TerritoryRenderer"/>: спрайт размером в пиксель всё равно не прочитать.
/// </summary>
public sealed class SettlementRenderer
{
    /// <summary>Ниже этого зума постройки не рисуются, остаются значки.</summary>
    public const float MinZoom = 12f;

    private static readonly Color ShadowColor = new Color(12, 16, 20) * 0.35f;
    private static readonly Color PoleColor = new Color(66, 48, 34);

    private readonly DecorAtlas _atlas;

    public SettlementRenderer(DecorAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
    }

    public bool Visible { get; set; } = true;

    /// <summary>Сколько построек ушло в последний кадр.</summary>
    public int DrawnBuildings { get; private set; }

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
        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 4);

        Texture2D texture = _atlas.Texture;
        Texture2D pixel = primitives.Pixel;
        int high = settlements.HighWater;
        int drawn = 0;

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
}
