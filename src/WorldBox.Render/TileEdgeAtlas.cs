using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;

namespace WorldBox.Render;

/// <summary>
/// Текстура кромок: четыре стороны на четыре варианта, каждая 16x16. Пиксели белые,
/// значение хранится в альфе, поэтому одна текстура красится в цвет любого биома.
/// Цвета сразу помножены на альфу: SpriteBatch с AlphaBlend ждёт premultiplied.
/// </summary>
public sealed class TileEdgeAtlas : IDisposable
{
    public const int TileSize = TileArt.TileSize;

    // 0 пусто, 1 полупрозрачно, 2 плотно.
    private static readonly byte[] Alphas = { 0, 120, 255 };

    private readonly Texture2D _texture;
    private bool _disposed;

    public TileEdgeAtlas(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        int width = EdgeArt.Variants * TileSize;
        int height = EdgeArt.Sides * TileSize;
        var pixels = new Color[width * height];
        Span<byte> mask = stackalloc byte[TileArt.Pixels];

        for (int side = 0; side < EdgeArt.Sides; side++)
        {
            for (int variant = 0; variant < EdgeArt.Variants; variant++)
            {
                EdgeArt.Build(side, variant, mask);

                int originX = variant * TileSize;
                int originY = side * TileSize;

                for (int y = 0; y < TileSize; y++)
                {
                    int row = (originY + y) * width;
                    for (int x = 0; x < TileSize; x++)
                    {
                        byte value = mask[(y * TileSize) + x];
                        int alpha = value < Alphas.Length ? Alphas[value] : 255;
                        pixels[row + originX + x] = new Color(alpha, alpha, alpha, alpha);
                    }
                }
            }
        }

        _texture = new Texture2D(device, width, height);
        _texture.SetData(pixels);
    }

    public Texture2D Texture => _texture;

    public Rectangle Source(int side, int variant)
    {
        int safeSide = ((side % EdgeArt.Sides) + EdgeArt.Sides) % EdgeArt.Sides;
        int safeVariant = ((variant % EdgeArt.Variants) + EdgeArt.Variants) % EdgeArt.Variants;
        return new Rectangle(safeVariant * TileSize, safeSide * TileSize, TileSize, TileSize);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _texture.Dispose();
    }
}
