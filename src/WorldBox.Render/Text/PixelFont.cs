using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldBox.Render.Text;

/// <summary>
/// Растровый шрифт. Атлас собирается в памяти при старте из битовой строки в коде,
/// поэтому в репозитории нет ни шрифтовых файлов, ни контент-пайплайна MonoGame.
/// Поддерживает кириллицу, латиницу, цифры и знаки препинания.
/// </summary>
public sealed class PixelFont : IDisposable
{
    private const int LookupSize = 0x2200;

    private readonly Texture2D _atlas;
    private readonly byte[] _advance;
    private readonly short[] _lookup;

    private PixelFont(Texture2D atlas, byte[] advance, short[] lookup)
    {
        _atlas = atlas;
        _advance = advance;
        _lookup = lookup;
    }

    /// <summary>Высота одного глифа в пикселях при масштабе 1.</summary>
    public int GlyphHeight => PixelFontData.CellHeight;

    /// <summary>Шаг между строками при масштабе 1.</summary>
    public int LineHeight => PixelFontData.CellHeight + 3;

    public static PixelFont Create(GraphicsDevice device)
    {
        byte[] data = Convert.FromBase64String(PixelFontData.Packed);
        string chars = PixelFontData.Chars;
        int cell = PixelFontData.CellHeight;
        int stride = cell + 1;
        int count = chars.Length;
        if (data.Length < count * stride)
        {
            throw new InvalidOperationException("Данные шрифта не совпадают со списком символов.");
        }

        int width = count * PixelFontData.MaxWidth;
        var pixels = new Color[width * cell];
        var advance = new byte[count];
        var lookup = new short[LookupSize];
        for (int i = 0; i < LookupSize; i++)
        {
            lookup[i] = -1;
        }

        for (int glyph = 0; glyph < count; glyph++)
        {
            int offset = glyph * stride;
            advance[glyph] = data[offset];
            char symbol = chars[glyph];
            if (symbol < LookupSize)
            {
                lookup[symbol] = (short)glyph;
            }

            int left = glyph * PixelFontData.MaxWidth;
            for (int row = 0; row < cell; row++)
            {
                byte bits = data[offset + 1 + row];
                if (bits == 0)
                {
                    continue;
                }

                int rowStart = (row * width) + left;
                for (int column = 0; column < PixelFontData.MaxWidth; column++)
                {
                    if (((bits >> (7 - column)) & 1) != 0)
                    {
                        pixels[rowStart + column] = Color.White;
                    }
                }
            }
        }

        var atlas = new Texture2D(device, width, cell, false, SurfaceFormat.Color);
        atlas.SetData(pixels);
        return new PixelFont(atlas, advance, lookup);
    }

    public int Measure(ReadOnlySpan<char> text, int scale)
    {
        int total = 0;
        for (int i = 0; i < text.Length; i++)
        {
            total += (GlyphAdvance(text[i]) + 1) * scale;
        }

        return total;
    }

    public int Measure(string text, int scale) => Measure(text.AsSpan(), scale);

    public void Draw(SpriteBatch batch, ReadOnlySpan<char> text, Vector2 position, Color color, int scale = 1)
    {
        if (scale < 1)
        {
            scale = 1;
        }

        int x = (int)position.X;
        int y = (int)position.Y;
        int cell = PixelFontData.CellHeight;
        for (int i = 0; i < text.Length; i++)
        {
            char symbol = text[i];
            int glyph = GlyphIndex(symbol);
            if (glyph >= 0)
            {
                int inkWidth = Math.Min(PixelFontData.MaxWidth, _advance[glyph]);
                var source = new Rectangle(glyph * PixelFontData.MaxWidth, 0, inkWidth, cell);
                var destination = new Rectangle(x, y, inkWidth * scale, cell * scale);
                batch.Draw(_atlas, destination, source, color);
                x += (_advance[glyph] + 1) * scale;
            }
            else
            {
                x += 4 * scale;
            }
        }
    }

    public void Draw(SpriteBatch batch, string text, Vector2 position, Color color, int scale = 1)
        => Draw(batch, text.AsSpan(), position, color, scale);

    /// <summary>Текст с тенью: читается на любом фоне карты.</summary>
    public void DrawShadowed(SpriteBatch batch, ReadOnlySpan<char> text, Vector2 position, Color color, int scale = 1)
    {
        Draw(batch, text, new Vector2(position.X + scale, position.Y + scale), new Color(0, 0, 0, 180), scale);
        Draw(batch, text, position, color, scale);
    }

    private int GlyphIndex(char symbol)
    {
        if (symbol == '\u00a0')
        {
            symbol = ' ';
        }

        return symbol < LookupSize ? _lookup[symbol] : -1;
    }

    private int GlyphAdvance(char symbol)
    {
        int glyph = GlyphIndex(symbol);
        return glyph >= 0 ? _advance[glyph] : 3;
    }

    public void Dispose() => _atlas.Dispose();
}
