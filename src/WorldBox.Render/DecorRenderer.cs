using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Растительность и камни поверх земли. Что стоит на тайле, решается один раз при загрузке мира
/// и кладётся в два байтовых массива, поэтому в кадре остаётся только выборка по индексу.
/// Спрайт выше тайла: основание стоит на нижней кромке клетки, а крона нависает над соседом сверху.
/// Обход идёт сверху вниз, так что нижние деревья закрывают верхние и картинка выглядит объёмной.
/// Рисуется только на близком зуме: издалека вместо кроны был бы шум в один пиксель.
/// </summary>
public sealed class DecorRenderer : IDisposable
{
    /// <summary>Ниже этого зума декор не рисуется.</summary>
    public const float MinZoom = 12f;

    /// <summary>Во сколько тайлов по высоте рисуется спрайт декора.</summary>
    private const float SpriteTiles = 1.5f;

    private readonly DecorAtlas _atlas;
    private readonly bool _ownsAtlas;
    private byte[] _kinds;
    private byte[] _variants;
    private WorldMap _map;
    private bool _disposed;

    public DecorRenderer(GraphicsDevice device, WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(map);

        _atlas = new DecorAtlas(device);
        _ownsAtlas = true;
        _map = map;
        _kinds = new byte[map.TileCount];
        _variants = new byte[map.TileCount];
        Rebuild();
    }

    public DecorRenderer(DecorAtlas atlas, WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(map);

        _atlas = atlas;
        _ownsAtlas = false;
        _map = map;
        _kinds = new byte[map.TileCount];
        _variants = new byte[map.TileCount];
        Rebuild();
    }

    public DecorAtlas Atlas => _atlas;

    /// <summary>Декор можно выключить: пригодится для замеров и слабых машин.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Сколько спрайтов декора ушло в последний кадр.</summary>
    public int DrawnDecor { get; private set; }

    /// <summary>Новый мир: пересчитать, что где растёт.</summary>
    public void SetMap(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        if (_kinds.Length != map.TileCount)
        {
            _kinds = new byte[map.TileCount];
            _variants = new byte[map.TileCount];
        }

        Rebuild();
    }

    public void Draw(SpriteBatch batch, Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(camera);

        DrawnDecor = 0;
        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 2);

        Texture2D texture = _atlas.Texture;
        float scale = SpriteTiles / DecorAtlas.SpriteSize;
        var size = new Vector2(scale, scale);
        int width = _map.Width;
        int drawn = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;
                byte kind = _kinds[index];
                if (kind == 0)
                {
                    continue;
                }

                Rectangle source = _atlas.Source((DecorKind)kind, _variants[index]);

                // Основание спрайта стоит чуть ниже середины клетки, верх нависает над соседом.
                var position = new Vector2(
                    x + 0.5f - (SpriteTiles * 0.5f),
                    y + 1.05f - SpriteTiles);

                batch.Draw(texture, position, source, Color.White, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
                drawn++;
            }
        }

        DrawnDecor = drawn;
    }

    private void Rebuild()
    {
        int width = _map.Width;
        int height = _map.Height;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = row + x;
                Biome biome = _map.BiomeOf(index);
                int density = DecorArt.DensityPercent(biome);

                if (density <= 0)
                {
                    _kinds[index] = 0;
                    _variants[index] = 0;
                    continue;
                }

                uint roll = Hash(x, y, 5171) % 100u;
                if (roll >= (uint)density)
                {
                    _kinds[index] = 0;
                    _variants[index] = 0;
                    continue;
                }

                DecorKind kind = DecorArt.NatureFor(biome, Hash(x, y, 7919));
                _kinds[index] = (byte)kind;
                _variants[index] = (byte)(Hash(x, y, 104729) % DecorArt.Variants);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsAtlas)
        {
            _atlas.Dispose();
        }
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
