using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Ближний план: каждый тайл рисуется спрайтом 16x16 из атласа, вода анимируется.
/// Дальний план остаётся за <see cref="TileRenderer"/>: там один тайл — один пиксель текстуры чанка.
/// Переключение по зуму: рисовать десятки тысяч спрайтов имеет смысл только когда их видно.
/// </summary>
public sealed class TileSpriteRenderer : IDisposable
{
    /// <summary>Ниже этого зума рисунок тайла всё равно не различим, работает дальний рендер.</summary>
    public const float MinZoom = 10f;

    // Спрайт 16x16 должен занять ровно один тайл мира. Процент накладки убирает
    // чёрные щели между соседями на дробном зуме.
    private const float Overlap = 1.01f;
    private const double FramesPerSecond = 5.0;

    private readonly WorldMap _map;
    private readonly TileAtlas _atlas;
    private readonly byte[] _variants;
    private readonly Vector2 _scale = new Vector2(Overlap / TileAtlas.TileSize, Overlap / TileAtlas.TileSize);

    private bool _disposed;

    public TileSpriteRenderer(GraphicsDevice device, WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        _atlas = new TileAtlas(device);
        _variants = new byte[map.TileCount];

        for (int y = 0; y < map.Height; y++)
        {
            int row = y * map.Width;
            for (int x = 0; x < map.Width; x++)
            {
                _variants[row + x] = (byte)TileArt.VariantFor(x, y);
            }
        }
    }

    public TileAtlas Atlas => _atlas;

    /// <summary>Сколько спрайтов ушло в последний кадр. Нужно для замеров.</summary>
    public int DrawnTiles { get; private set; }

    public void Draw(SpriteBatch batch, Camera2D camera, double seconds)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(camera);

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 1);

        if (minX < 0)
        {
            minX = 0;
        }

        if (minY < 0)
        {
            minY = 0;
        }

        if (maxX > _map.Width - 1)
        {
            maxX = _map.Width - 1;
        }

        if (maxY > _map.Height - 1)
        {
            maxY = _map.Height - 1;
        }

        int frame = (int)((long)(seconds * FramesPerSecond) % TileArt.Frames);
        Texture2D texture = _atlas.Texture;
        int drawn = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _map.Width;
            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;
                Biome biome = _map.BiomeOf(index);
                Rectangle source = _atlas.Source(biome, _variants[index], frame);
                var position = new Vector2(x, y);

                batch.Draw(texture, position, source, Color.White, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
                drawn++;
            }
        }

        DrawnTiles = drawn;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _atlas.Dispose();
    }
}
