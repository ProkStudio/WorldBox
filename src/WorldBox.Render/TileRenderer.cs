using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Рисует карту кусками 64x64. Каждый кусок — отдельная текстура, которая пересчитывается
/// только когда там что-то изменилось. За кадр рисуется не больше пары десятков вызовов
/// вместо сотен тысяч тайлов.
/// </summary>
public sealed class TileRenderer : IDisposable
{
    public const int ChunkSize = 64;

    private readonly WorldMap _map;
    private readonly Texture2D[] _chunks;
    private readonly bool[] _dirty;
    private readonly Color[] _scratch = new Color[ChunkSize * ChunkSize];
    private readonly int _chunksX;
    private readonly int _chunksY;

    private MapMode _mode = MapMode.Terrain;

    public TileRenderer(GraphicsDevice device, WorldMap map)
    {
        _map = map;
        _chunksX = (map.Width + ChunkSize - 1) / ChunkSize;
        _chunksY = (map.Height + ChunkSize - 1) / ChunkSize;
        _chunks = new Texture2D[_chunksX * _chunksY];
        _dirty = new bool[_chunks.Length];

        for (int i = 0; i < _chunks.Length; i++)
        {
            int chunkX = i % _chunksX;
            int chunkY = i / _chunksX;
            int width = Math.Min(ChunkSize, map.Width - (chunkX * ChunkSize));
            int height = Math.Min(ChunkSize, map.Height - (chunkY * ChunkSize));
            _chunks[i] = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            _dirty[i] = true;
        }

        BuildAll();
    }

    public MapMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            Invalidate();
        }
    }

    public int ChunkCount => _chunks.Length;

    public int PendingChunks
    {
        get
        {
            int pending = 0;
            for (int i = 0; i < _dirty.Length; i++)
            {
                if (_dirty[i])
                {
                    pending++;
                }
            }

            return pending;
        }
    }

    /// <summary>Перерисовать всю карту сразу. Используется при смене мира.</summary>
    public void BuildAll()
    {
        for (int i = 0; i < _chunks.Length; i++)
        {
            BuildChunk(i);
        }
    }

    public void Invalidate()
    {
        for (int i = 0; i < _dirty.Length; i++)
        {
            _dirty[i] = true;
        }
    }

    /// <summary>Пометить кусок с этим тайлом как устаревший. Пригодится инструментам бога.</summary>
    public void InvalidateTile(int x, int y)
    {
        if (!_map.InBounds(x, y))
        {
            return;
        }

        int index = ((y / ChunkSize) * _chunksX) + (x / ChunkSize);
        _dirty[index] = true;
    }

    /// <summary>Догружает устаревшие куски порциями, чтобы не было рывка на одном кадре.</summary>
    public void Update(int maxChunksPerFrame = 8)
    {
        int built = 0;
        for (int i = 0; i < _chunks.Length && built < maxChunksPerFrame; i++)
        {
            if (_dirty[i])
            {
                BuildChunk(i);
                built++;
            }
        }
    }

    public void Draw(SpriteBatch batch, Camera2D camera)
    {
        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 0);
        int firstX = minX / ChunkSize;
        int lastX = maxX / ChunkSize;
        int firstY = minY / ChunkSize;
        int lastY = maxY / ChunkSize;

        for (int chunkY = firstY; chunkY <= lastY; chunkY++)
        {
            for (int chunkX = firstX; chunkX <= lastX; chunkX++)
            {
                Texture2D texture = _chunks[(chunkY * _chunksX) + chunkX];
                var destination = new Rectangle(
                    chunkX * ChunkSize,
                    chunkY * ChunkSize,
                    texture.Width,
                    texture.Height);
                batch.Draw(texture, destination, Color.White);
            }
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _chunks.Length; i++)
        {
            _chunks[i].Dispose();
        }
    }

    private void BuildChunk(int index)
    {
        Texture2D texture = _chunks[index];
        int chunkX = (index % _chunksX) * ChunkSize;
        int chunkY = (index / _chunksX) * ChunkSize;
        int width = texture.Width;
        int height = texture.Height;

        for (int y = 0; y < height; y++)
        {
            int row = (chunkY + y) * _map.Width;
            int target = y * width;
            for (int x = 0; x < width; x++)
            {
                _scratch[target + x] = BiomePalette.For(_mode, _map, row + chunkX + x);
            }
        }

        texture.SetData(_scratch, 0, width * height);
        _dirty[index] = false;
    }
}
