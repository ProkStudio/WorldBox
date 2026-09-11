using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.World;
using WorldBox.Render;

namespace WorldBox.UI;

/// <summary>
/// Мини-карта в углу. Показывает весь мир и рамку текущего вида,
/// по клику переносит камеру. Текстура строится один раз на режим карты.
/// </summary>
public sealed class Minimap : IDisposable
{
    private static readonly Color PanelColor = new Color(10, 12, 16, 200);
    private static readonly Color BorderColor = new Color(255, 255, 255, 50);
    private static readonly Color ViewColor = new Color(94, 159, 232, 220);

    private readonly WorldMap _map;
    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly int _step;
    private readonly int _pixelWidth;
    private readonly int _pixelHeight;

    public Minimap(GraphicsDevice device, WorldMap map, int maxSize = 224)
    {
        _map = map;
        _step = Math.Max(1, (Math.Max(map.Width, map.Height) + maxSize - 1) / maxSize);
        _pixelWidth = Math.Max(1, map.Width / _step);
        _pixelHeight = Math.Max(1, map.Height / _step);
        _pixels = new Color[_pixelWidth * _pixelHeight];
        _texture = new Texture2D(device, _pixelWidth, _pixelHeight, false, SurfaceFormat.Color);
        Bounds = new Rectangle(0, 0, _pixelWidth, _pixelHeight);
        Rebuild(MapMode.Terrain);
    }

    /// <summary>Прямоугольник картинки на экране.</summary>
    public Rectangle Bounds { get; private set; }

    public void Rebuild(MapMode mode)
    {
        for (int y = 0; y < _pixelHeight; y++)
        {
            int sourceY = Math.Min(_map.Height - 1, y * _step);
            int row = sourceY * _map.Width;
            for (int x = 0; x < _pixelWidth; x++)
            {
                int sourceX = Math.Min(_map.Width - 1, x * _step);
                _pixels[(y * _pixelWidth) + x] = BiomePalette.For(mode, _map, row + sourceX);
            }
        }

        _texture.SetData(_pixels);
    }

    public void Layout(int viewportWidth, int viewportHeight, int margin = 16)
    {
        Bounds = new Rectangle(
            viewportWidth - _pixelWidth - margin,
            viewportHeight - _pixelHeight - margin,
            _pixelWidth,
            _pixelHeight);
    }

    public void Draw(SpriteBatch batch, Primitives primitives, Camera2D camera)
    {
        var frame = new Rectangle(Bounds.X - 4, Bounds.Y - 4, Bounds.Width + 8, Bounds.Height + 8);
        primitives.FillRect(batch, frame, PanelColor);
        batch.Draw(_texture, Bounds, Color.White);
        primitives.FrameRect(batch, frame, BorderColor);

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 0);
        float scaleX = Bounds.Width / (float)_map.Width;
        float scaleY = Bounds.Height / (float)_map.Height;
        var view = new Rectangle(
            Bounds.X + (int)(minX * scaleX),
            Bounds.Y + (int)(minY * scaleY),
            Math.Max(3, (int)((maxX - minX) * scaleX)),
            Math.Max(3, (int)((maxY - minY) * scaleY)));
        primitives.FrameRect(batch, view, ViewColor);
    }

    /// <summary>Если кликнули по мини-карте, возвращает точку мира в тайлах.</summary>
    public bool TryPick(Vector2 screen, out Vector2 worldTile)
    {
        if (!Bounds.Contains((int)screen.X, (int)screen.Y))
        {
            worldTile = Vector2.Zero;
            return false;
        }

        float u = (screen.X - Bounds.X) / Bounds.Width;
        float v = (screen.Y - Bounds.Y) / Bounds.Height;
        worldTile = new Vector2(u * _map.Width, v * _map.Height);
        return true;
    }

    public void Dispose() => _texture.Dispose();
}
