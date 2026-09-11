using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldBox.Render;

/// <summary>
/// Однопиксельная белая текстура и рисование прямоугольников и линий без ассетов.
/// Аллокаций во время рисования нет.
/// </summary>
public sealed class Primitives : IDisposable
{
    private static readonly Color[] WhitePixel = { Color.White };

    public Primitives(GraphicsDevice device)
    {
        Pixel = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        Pixel.SetData(WhitePixel);
    }

    public Texture2D Pixel { get; }

    public void FillRect(SpriteBatch batch, Rectangle rectangle, Color color)
        => batch.Draw(Pixel, rectangle, color);

    public void FillRect(SpriteBatch batch, float x, float y, float width, float height, Color color)
        => batch.Draw(Pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

    public void FrameRect(SpriteBatch batch, Rectangle rectangle, Color color, int thickness = 1)
    {
        batch.Draw(Pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        batch.Draw(Pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        batch.Draw(Pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        batch.Draw(Pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    public void Line(SpriteBatch batch, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length < 0.0001f)
        {
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X);
        batch.Draw(
            Pixel,
            from,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    public void Dispose() => Pixel.Dispose();
}
