using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Ближний план: каждый тайл рисуется спрайтом 16x16 из атласа, вода анимируется,
/// на стыках биомов дорисовывается рваная кромка соседа и пена у берега.
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

    private static readonly int[] EdgeDx = { 0, 1, 0, -1 };
    private static readonly int[] EdgeDy = { -1, 0, 1, 0 };

    private readonly WorldMap _map;
    private readonly TileAtlas _atlas;
    private readonly TileEdgeAtlas _edges;
    private readonly byte[] _variants;
    private readonly byte[] _priority;
    private readonly bool[] _isWaterArt;
    private readonly Color[] _colors;
    private readonly Color[] _foam;
    private readonly Vector2 _scale = new Vector2(Overlap / TileAtlas.TileSize, Overlap / TileAtlas.TileSize);

    private bool _disposed;

    public TileSpriteRenderer(GraphicsDevice device, WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        _atlas = new TileAtlas(device);
        _edges = new TileEdgeAtlas(device);
        _variants = new byte[map.TileCount];

        for (int y = 0; y < map.Height; y++)
        {
            int row = y * map.Width;
            for (int x = 0; x < map.Width; x++)
            {
                _variants[row + x] = (byte)TileArt.VariantFor(x, y);
            }
        }

        // Свойства биома считаются один раз: в кадре остаётся только выборка по индексу.
        _priority = new byte[Biomes.Count];
        _isWaterArt = new bool[Biomes.Count];
        _colors = new Color[Biomes.Count];
        _foam = new Color[Biomes.Count];

        for (int index = 0; index < Biomes.Count; index++)
        {
            var biome = (Biome)index;
            _priority[index] = (byte)EdgeArt.Priority(biome);
            _isWaterArt[index] = TileArt.IsAnimated(biome);
            _colors[index] = ArtPalette.Base(biome).ToColor();
            _foam[index] = ArtPalette.Foam(biome).ToColor();
        }
    }

    public TileAtlas Atlas => _atlas;

    /// <summary>Кромки можно выключить: пригодится для замеров и слабых машин.</summary>
    public bool Transitions { get; set; } = true;

    /// <summary>Сколько спрайтов ушло в последний кадр. Нужно для замеров.</summary>
    public int DrawnTiles { get; private set; }

    public int DrawnEdges { get; private set; }

    public void Draw(SpriteBatch batch, Camera2D camera, double seconds)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(camera);

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 1);

        int width = _map.Width;
        int height = _map.Height;

        if (minX < 0)
        {
            minX = 0;
        }

        if (minY < 0)
        {
            minY = 0;
        }

        if (maxX > width - 1)
        {
            maxX = width - 1;
        }

        if (maxY > height - 1)
        {
            maxY = height - 1;
        }

        int frame = (int)((long)(seconds * FramesPerSecond) % TileArt.Frames);
        Texture2D tiles = _atlas.Texture;
        Texture2D edges = _edges.Texture;
        int drawnTiles = 0;
        int drawnEdges = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;
                Biome biome = _map.BiomeOf(index);
                int biomeId = (int)biome;
                var position = new Vector2(x, y);

                Rectangle source = _atlas.Source(biome, _variants[index], frame);
                batch.Draw(tiles, position, source, Color.White, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
                drawnTiles++;

                if (!Transitions)
                {
                    continue;
                }

                byte selfPriority = _priority[biomeId];

                for (int side = 0; side < EdgeArt.Sides; side++)
                {
                    int nx = x + EdgeDx[side];
                    int ny = y + EdgeDy[side];

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    Biome other = _map.BiomeOf((ny * width) + nx);
                    if (other == biome)
                    {
                        continue;
                    }

                    int otherId = (int)other;
                    Color tint;

                    if (_priority[otherId] > selfPriority)
                    {
                        // Сосед сильнее: его материал наползает на этот тайл.
                        tint = _colors[otherId];
                    }
                    else if (_isWaterArt[biomeId] && !_isWaterArt[otherId])
                    {
                        // Вода у суши получает светлую пену вместо резкой границы.
                        tint = _foam[biomeId];
                    }
                    else
                    {
                        continue;
                    }

                    Rectangle edge = _edges.Source(side, EdgeArt.VariantFor(x, y, side));
                    batch.Draw(edges, position, edge, tint, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
                    drawnEdges++;
                }
            }
        }

        DrawnTiles = drawnTiles;
        DrawnEdges = drawnEdges;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _atlas.Dispose();
        _edges.Dispose();
    }
}
