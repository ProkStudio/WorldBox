using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Tribes;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Таблички городов над картой: звёздочка, имя и число жителей.
/// Рисуются в координатах экрана, поэтому текст не плывёт на зуме и остаётся резким.
/// Чтобы карта не превратилась в свалку подписей, табличек не больше <see cref="MaxPlates"/>,
/// и новая не рисуется, если налезает на уже нарисованную.
/// </summary>
public sealed class NamePlates
{
    /// <summary>Ниже этого зума подписи не нужны: города становятся точками.</summary>
    public const float MinZoom = 6f;

    /// <summary>С этого зума подписываются даже лагеря.</summary>
    public const float CampZoom = 18f;

    public const int MaxPlates = 28;

    private readonly Rectangle[] _taken = new Rectangle[MaxPlates];
    private readonly TextBuilder _text = new TextBuilder(96);

    public bool Visible { get; set; } = true;

    /// <summary>Сколько табличек ушло в последний кадр.</summary>
    public int DrawnPlates { get; private set; }

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        UiSkin skin,
        Camera2D camera,
        TribeStore tribes,
        SettlementStore settlements,
        int viewportWidth,
        int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);

        DrawnPlates = 0;
        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 2);

        float zoom = camera.Zoom;
        float halfWidth = viewportWidth * 0.5f;
        float halfHeight = viewportHeight * 0.5f;
        int reserved = Toolbar.ReservedHeight;
        int iconSize = IconAtlas.IconSize;
        int textScale = 1;
        int plateHeight = Math.Max(iconSize + 6, (font.GlyphHeight * textScale) + 10);
        int count = 0;
        int high = settlements.HighWater;

        // Сначала города, потом деревни, потом лагеря: если места мало, останутся главные.
        for (int level = SettlementStore.Town; level >= SettlementStore.Camp && count < MaxPlates; level--)
        {
            if (level == SettlementStore.Camp && zoom < CampZoom)
            {
                continue;
            }

            for (int i = 0; i < high && count < MaxPlates; i++)
            {
                if (!settlements.Alive[i] || settlements.Level[i] != level)
                {
                    continue;
                }

                int tileX = settlements.X[i];
                int tileY = settlements.Y[i];
                if (tileX < minX || tileX > maxX || tileY < minY || tileY > maxY)
                {
                    continue;
                }

                short tribe = settlements.Tribe[i];
                if (!tribes.IsAlive(tribe))
                {
                    continue;
                }

                _text.Clear();
                string name = settlements.Name[i] ?? string.Empty;
                _text.Append(name);
                _text.Append(' ');
                _text.Append(settlements.People[i]);

                int textWidth = font.Measure(_text.Span, textScale);
                int width = textWidth + iconSize + 18;

                // Перевод тайлов в пиксели экрана вручную: камера смотрит в центр экрана.
                float screenX = ((tileX + 0.5f) - camera.Position.X) * zoom + halfWidth;
                float screenY = (tileY - camera.Position.Y) * zoom + halfHeight;

                int x = (int)(screenX - (width * 0.5f));
                int y = (int)(screenY - plateHeight - (zoom * 1.6f));

                if (x < 4 || y < 4 || x + width > viewportWidth - 4 || y + plateHeight > viewportHeight - reserved)
                {
                    continue;
                }

                var rect = new Rectangle(x, y, width, plateHeight);
                if (Overlaps(rect, count))
                {
                    continue;
                }

                Color accent = TribePalette.Of(tribes.ColorIndex[tribe]);
                skin.Plate(batch, rect, accent);
                skin.Icon(batch, IconKind.Star, new Rectangle(x + 6, y + ((plateHeight - iconSize) / 2), iconSize, iconSize));
                font.Draw(
                    batch,
                    _text.Span,
                    new Vector2(x + iconSize + 11, y + ((plateHeight - (font.GlyphHeight * textScale)) / 2)),
                    UiPalette.Text,
                    textScale);

                _taken[count] = rect;
                count++;
            }
        }

        DrawnPlates = count;
    }

    private bool Overlaps(Rectangle rect, int count)
    {
        var padded = new Rectangle(rect.X - 4, rect.Y - 3, rect.Width + 8, rect.Height + 6);
        for (int i = 0; i < count; i++)
        {
            if (_taken[i].Intersects(padded))
            {
                return true;
            }
        }

        return false;
    }
}
