using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Границы держав и значки поселений.
/// Владения рисуются одной текстурой размером с карту: один пиксель — один тайл.
/// Так на экран уходит один прямоугольник вместо сотен тысяч, а пересборка идёт только когда
/// владельцы действительно менялись, и не чаще раза в несколько кадров.
/// Край владений рисуется ярче середины — получается чёткая политическая обводка.
/// Значок поселения меняется с эпохой народа: хижина, стена, башня, дым, ночные огни.
/// Вблизи значки выключаются флагом <see cref="MarkersVisible"/>: там их заменяют постройки
/// из <see cref="SettlementRenderer"/>.
/// </summary>
public sealed class TerritoryRenderer : IDisposable
{
    /// <summary>Не чаще чем раз в столько кадров пересобираем текстуру границ.</summary>
    public const int RebuildEveryFrames = 10;

    private const float FillAlpha = 0.26f;
    private const float BorderAlpha = 0.85f;

    private static readonly Color MarkerBase = new Color(10, 11, 15) * 0.9f;

    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly int _width;
    private readonly int _height;

    private int _builtVersion = -1;
    private int _sinceRebuild;

    public TerritoryRenderer(GraphicsDevice device, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер карты должен быть положительным.");
        }

        _width = width;
        _height = height;
        _pixels = new Color[width * height];
        _texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
    }

    public bool Visible { get; set; } = true;

    /// <summary>Рисовать ли дальние значки поселений. Вблизи их заменяют постройки.</summary>
    public bool MarkersVisible { get; set; } = true;

    /// <summary>Сколько значков поселений ушло в последний кадр.</summary>
    public int DrawnSettlements { get; private set; }

    /// <summary>Заставляет пересобрать текстуру на ближайшем кадре. Нужно после смены мира.</summary>
    public void Invalidate()
    {
        _builtVersion = -1;
        _sinceRebuild = RebuildEveryFrames;
    }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        Territory territory,
        TribeStore tribes,
        SettlementStore settlements)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);

        DrawnSettlements = 0;
        if (!Visible)
        {
            return;
        }

        _sinceRebuild++;
        if (_builtVersion != territory.Version && _sinceRebuild >= RebuildEveryFrames)
        {
            Rebuild(territory, tribes);
            _builtVersion = territory.Version;
            _sinceRebuild = 0;
        }

        batch.Draw(_texture, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        if (MarkersVisible)
        {
            DrawSettlements(batch, primitives, camera, tribes, settlements);
        }
    }

    private void Rebuild(Territory territory, TribeStore tribes)
    {
        short[] owner = territory.Owner;

        for (int y = 0; y < _height; y++)
        {
            int row = y * _width;

            for (int x = 0; x < _width; x++)
            {
                int index = row + x;
                short tribe = owner[index];

                if (tribe == TribeStore.None || !tribes.IsAlive(tribe))
                {
                    _pixels[index] = Color.Transparent;
                    continue;
                }

                bool edge = x == 0
                    || x == _width - 1
                    || y == 0
                    || y == _height - 1
                    || owner[index - 1] != tribe
                    || owner[index + 1] != tribe
                    || owner[index - _width] != tribe
                    || owner[index + _width] != tribe;

                Color color = TribePalette.Of(tribes.ColorIndex[tribe]);
                _pixels[index] = edge ? color * BorderAlpha : color * FillAlpha;
            }
        }

        _texture.SetData(_pixels);
    }

    private void DrawSettlements(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        TribeStore tribes,
        SettlementStore settlements)
    {
        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 2);

        float zoom = MathF.Max(camera.Zoom, 0.0001f);
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
            float size = level == SettlementStore.Town ? 3.4f : level == SettlementStore.Village ? 2.4f : 1.6f;

            // Значок не должен пропадать на дальнем зуме: держим минимум в пикселях экрана.
            float minTiles = (level == SettlementStore.Camp ? 4f : 6f) / zoom;
            if (size < minTiles)
            {
                size = minTiles;
            }

            float outline = MathF.Max(0.35f, 1.2f / zoom);
            float half = size * 0.5f;
            var inner = new Vector2(x + 0.5f - half, y + 0.5f - half);
            var outer = new Vector2(inner.X - outline, inner.Y - outline);

            float outerSize = size + (outline * 2f);
            batch.Draw(pixel, outer, null, MarkerBase, 0f, Vector2.Zero, new Vector2(outerSize, outerSize), SpriteEffects.None, 0f);
            batch.Draw(pixel, inner, null, TribePalette.Of(tribes.ColorIndex[tribe]), 0f, Vector2.Zero, new Vector2(size, size), SpriteEffects.None, 0f);

            // Эпоха видна без панелей: у бронзы появляется стена, у промышленности — дым.
            DrawDecor(batch, pixel, EraStyle.DecorOf(tribes.Era[tribe]), x + 0.5f, y + 0.5f, size, outline);
            drawn++;
        }

        DrawnSettlements = drawn;
    }

    /// <summary>Украшения значка. Рисуются только для видимых поселений, поэтому стоят копейки.</summary>
    private static void DrawDecor(
        SpriteBatch batch,
        Texture2D pixel,
        SettlementDecor decor,
        float centerX,
        float centerY,
        float size,
        float outline)
    {
        if (decor == SettlementDecor.Hut)
        {
            return;
        }

        float ringSize = size + (outline * 4f);
        float ringLeft = centerX - (ringSize * 0.5f);
        float ringTop = centerY - (ringSize * 0.5f);
        DrawRing(batch, pixel, ringLeft, ringTop, ringSize, outline, EraStyle.Wall);

        if (decor == SettlementDecor.Walls)
        {
            return;
        }

        float tower = MathF.Max(outline, size * 0.42f);
        DrawQuad(batch, pixel, centerX - (tower * 0.5f), ringTop - tower, tower, tower, EraStyle.Tower);

        if (decor == SettlementDecor.Smoke)
        {
            float puff = MathF.Max(outline, size * 0.3f);
            DrawQuad(batch, pixel, centerX - (puff * 1.7f), ringTop - tower - puff, puff, puff, EraStyle.Smoke);
            DrawQuad(batch, pixel, centerX + (puff * 0.7f), ringTop - tower - (puff * 1.9f), puff, puff, EraStyle.Smoke);
            return;
        }

        if (decor == SettlementDecor.Lights)
        {
            float dot = MathF.Max(outline, size * 0.22f);
            DrawQuad(batch, pixel, ringLeft - dot, ringTop - dot, dot, dot, EraStyle.Light);
            DrawQuad(batch, pixel, ringLeft + ringSize, ringTop - dot, dot, dot, EraStyle.Light);
            DrawQuad(batch, pixel, ringLeft - dot, ringTop + ringSize, dot, dot, EraStyle.Light);
            DrawQuad(batch, pixel, ringLeft + ringSize, ringTop + ringSize, dot, dot, EraStyle.Light);
        }
    }

    private static void DrawRing(
        SpriteBatch batch,
        Texture2D pixel,
        float left,
        float top,
        float size,
        float thickness,
        Color color)
    {
        DrawQuad(batch, pixel, left, top, size, thickness, color);
        DrawQuad(batch, pixel, left, top + size - thickness, size, thickness, color);
        DrawQuad(batch, pixel, left, top, thickness, size, color);
        DrawQuad(batch, pixel, left + size - thickness, top, thickness, size, color);
    }

    private static void DrawQuad(
        SpriteBatch batch,
        Texture2D pixel,
        float left,
        float top,
        float width,
        float height,
        Color color)
    {
        batch.Draw(pixel, new Vector2(left, top), null, color, 0f, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0f);
    }

    public void Dispose()
    {
        _texture.Dispose();
    }
}
