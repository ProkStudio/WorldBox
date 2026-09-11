using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;

namespace WorldBox.Render;

/// <summary>
/// Атлас спрайтов декора 16x16 с прозрачностью: строка — вид декора, столбец — вариант рисунка.
/// Собирается один раз при старте и занимает 48x288 пикселей.
/// Цвета слотов берутся из <see cref="DecorArt.Color"/>, поэтому палитра остаётся в одном месте.
/// </summary>
public sealed class DecorAtlas : IDisposable
{
    public const int SpriteSize = DecorArt.Size;

    private readonly Texture2D _texture;
    private bool _disposed;

    public DecorAtlas(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        int kinds = DecorArt.KindCount;
        int width = DecorArt.Variants * SpriteSize;
        int height = kinds * SpriteSize;
        var pixels = new Color[width * height];
        Span<byte> sprite = stackalloc byte[DecorArt.Pixels];
        Span<Color> slots = stackalloc Color[DecorArt.Slots];

        for (int kind = 0; kind < kinds; kind++)
        {
            var decor = (DecorKind)kind;
            slots[0] = Color.Transparent;
            for (int slot = 1; slot < DecorArt.Slots; slot++)
            {
                slots[slot] = DecorArt.Color(decor, slot).ToColor();
            }

            for (int variant = 0; variant < DecorArt.Variants; variant++)
            {
                DecorArt.Build(decor, variant, sprite);

                int originX = variant * SpriteSize;
                int originY = kind * SpriteSize;

                for (int y = 0; y < SpriteSize; y++)
                {
                    int row = (originY + y) * width;
                    for (int x = 0; x < SpriteSize; x++)
                    {
                        byte slot = sprite[(y * SpriteSize) + x];
                        pixels[row + originX + x] = slots[slot < DecorArt.Slots ? slot : 0];
                    }
                }
            }
        }

        _texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
        _texture.SetData(pixels);
    }

    public Texture2D Texture => _texture;

    /// <summary>Прямоугольник спрайта в атласе.</summary>
    public Rectangle Source(DecorKind kind, int variant)
    {
        int row = (int)kind;
        if (row < 0 || row >= DecorArt.KindCount)
        {
            row = 0;
        }

        int column = ((variant % DecorArt.Variants) + DecorArt.Variants) % DecorArt.Variants;
        return new Rectangle(column * SpriteSize, row * SpriteSize, SpriteSize, SpriteSize);
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
