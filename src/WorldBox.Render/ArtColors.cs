using Microsoft.Xna.Framework;
using WorldBox.Core.Art;

namespace WorldBox.Render;

/// <summary>
/// Мост между палитрой ядра и цветом MonoGame. Прозрачность домножается на все каналы:
/// SpriteBatch по умолчанию работает с предумноженной альфой, иначе полупрозрачные
/// панели выглядели бы выцветшими.
/// </summary>
public static class ArtColors
{
    public static Color ToColor(this ArtColor color)
    {
        return new Color(color.R, color.G, color.B);
    }

    public static Color ToColor(this ArtColor color, float alpha)
    {
        float k = alpha < 0f ? 0f : (alpha > 1f ? 1f : alpha);
        return new Color(color.R, color.G, color.B) * k;
    }

    public static ArtColor ToArtColor(this Color color)
    {
        return new ArtColor(color.R, color.G, color.B);
    }
}
