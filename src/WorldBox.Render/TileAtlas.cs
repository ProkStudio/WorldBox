using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Атлас процедурных тайлов 16x16. Собирается один раз при старте: для каждого биома
/// четыре варианта рисунка и четыре кадра анимации. Цвет берётся из <see cref="BiomePalette"/>,
/// рисунок — из <see cref="TileArt"/>. Размер атласа 256x304 пикселя, это менее сотни килобайт.
/// </summary>
public sealed class TileAtlas : IDisposable
{
    public const int TileSize = TileArt.TileSize;

    // 0 базовый, 1 тень, 2 блик, 3 деталь.
    private static readonly float[] ShadeFactors = { 1f, 0.84f, 1.18f, 0.66f };

    private readonly Texture2D _texture;
    private readonly int _columns;
    private bool _disposed;

    public TileAtlas(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        _columns = TileArt.Variants * TileArt.Frames;
        int width = _columns * TileSize;
        int height = Biomes.Count * TileSize;
        var pixels = new Color[width * height];
        Span<byte> tile = stackalloc byte[TileArt.Pixels];

        for (int index = 0; index < Biomes.Count; index++)
        {
            var biome = (Biome)index;
            Color baseColor = BiomePalette.Of(biome);
            int frames = TileArt.FrameCount(biome);

            for (int variant = 0; variant < TileArt.Variants; variant++)
            {
                for (int frame = 0; frame < TileArt.Frames; frame++)
                {
                    TileArt.Build(biome, variant, frame % frames, tile);

                    int originX = ((variant * TileArt.Frames) + frame) * TileSize;
                    int originY = index * TileSize;

                    for (int y = 0; y < TileSize; y++)
                    {
                        int row = (originY + y) * width;
                        for (int x = 0; x < TileSize; x++)
                        {
                            pixels[row + originX + x] = Shade(baseColor, tile[(y * TileSize) + x]);
                        }
                    }
                }
            }
        }

        _texture = new Texture2D(device, width, height);
        _texture.SetData(pixels);
    }

    public Texture2D Texture => _texture;

    public int Width => _texture.Width;

    public int Height => _texture.Height;

    /// <summary>Сколько кадров анимации у этого биома.</summary>
    public static int FrameCount(Biome biome)
    {
        return TileArt.FrameCount(biome);
    }

    /// <summary>Прямоугольник в атласе для биома, варианта рисунка и кадра анимации.</summary>
    public Rectangle Source(Biome biome, int variant, int frame)
    {
        int index = (int)biome;
        if (index < 0 || index >= Biomes.Count)
        {
            index = 0;
        }

        int frames = TileArt.FrameCount(biome);
        int safeVariant = ((variant % TileArt.Variants) + TileArt.Variants) % TileArt.Variants;
        int safeFrame = frames <= 1 ? 0 : ((frame % frames) + frames) % frames;
        int column = (safeVariant * TileArt.Frames) + safeFrame;

        return new Rectangle(column * TileSize, index * TileSize, TileSize, TileSize);
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

    private static Color Shade(Color color, byte shade)
    {
        float factor = shade < ShadeFactors.Length ? ShadeFactors[shade] : 1f;
        return new Color(
            (int)MathHelper.Clamp(color.R * factor, 0f, 255f),
            (int)MathHelper.Clamp(color.G * factor, 0f, 255f),
            (int)MathHelper.Clamp(color.B * factor, 0f, 255f));
    }
}
