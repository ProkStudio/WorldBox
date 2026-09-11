using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;

namespace WorldBox.Render;

/// <summary>
/// Атлас значков 16x16: один столбец, строка на каждый вид. Значки цветные,
/// поэтому рисуются с <c>Color.White</c>, а тонирование нужно только для приглушённого
/// состояния кнопки.
/// </summary>
public sealed class IconAtlas : IDisposable
{
    public const int IconSize = IconArt.Size;

    private readonly Texture2D _texture;
    private bool _disposed;

    public IconAtlas(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        int kinds = IconArt.KindCount;
        var pixels = new Color[IconSize * IconSize * kinds];
        Span<byte> icon = stackalloc byte[IconArt.Pixels];
        Span<Color> slots = stackalloc Color[IconArt.Slots];

        for (int kind = 0; kind < kinds; kind++)
        {
            var value = (IconKind)kind;
            slots[0] = Color.Transparent;
            for (int slot = 1; slot < IconArt.Slots; slot++)
            {
                slots[slot] = IconArt.Color(value, slot).ToColor();
            }

            IconArt.Build(value, icon);

            int originY = kind * IconSize;
            for (int y = 0; y < IconSize; y++)
            {
                int row = (originY + y) * IconSize;
                for (int x = 0; x < IconSize; x++)
                {
                    byte slot = icon[(y * IconSize) + x];
                    pixels[row + x] = slots[slot < IconArt.Slots ? slot : 0];
                }
            }
        }

        _texture = new Texture2D(device, IconSize, IconSize * kinds, false, SurfaceFormat.Color);
        _texture.SetData(pixels);
    }

    public Texture2D Texture => _texture;

    public Rectangle Source(IconKind kind)
    {
        int row = (int)kind;
        if (row < 0 || row >= IconArt.KindCount)
        {
            row = 0;
        }

        return new Rectangle(0, row * IconSize, IconSize, IconSize);
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
