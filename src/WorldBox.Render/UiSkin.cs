using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;

namespace WorldBox.Render;

/// <summary>Цвета интерфейса в одном месте. Меняется здесь — меняется во всех панелях.</summary>
public static class UiPalette
{
    public static readonly Color Panel = new Color(28, 33, 45) * 0.94f;
    public static readonly Color PanelLight = new Color(38, 45, 60) * 0.95f;
    public static readonly Color Border = new Color(12, 15, 21) * 0.95f;
    public static readonly Color Shadow = new Color(0, 0, 0) * 0.35f;
    public static readonly Color Button = new Color(46, 54, 72) * 0.96f;
    public static readonly Color ButtonHover = new Color(66, 78, 102) * 0.98f;
    public static readonly Color ButtonActive = new Color(198, 138, 48) * 0.98f;
    public static readonly Color Text = new Color(240, 236, 226);
    public static readonly Color TextMuted = new Color(166, 176, 194);
    public static readonly Color Accent = new Color(244, 186, 76);
    public static readonly Color Good = new Color(126, 214, 106);
    public static readonly Color Bad = new Color(232, 96, 88);
}

/// <summary>
/// Скин интерфейса: панели с круглыми углами, кнопки, таблички и значки.
/// Угол делается текстурой 22x22, которая растягивается девятью кусками, поэтому панель
/// любого размера стоит девять вызовов рисования и ни одного ассета в репозитории.
/// </summary>
public sealed class UiSkin : IDisposable
{
    private const int Slice = 10;
    private const int Middle = 2;
    private const int TextureSize = (Slice * 2) + Middle;

    private readonly Texture2D _fill;
    private readonly Texture2D _border;
    private readonly IconAtlas _icons;
    private readonly bool _ownsIcons;
    private bool _disposed;

    public UiSkin(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        _fill = BuildRounded(device, false);
        _border = BuildRounded(device, true);
        _icons = new IconAtlas(device);
        _ownsIcons = true;
    }

    public UiSkin(GraphicsDevice device, IconAtlas icons)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(icons);

        _fill = BuildRounded(device, false);
        _border = BuildRounded(device, true);
        _icons = icons;
        _ownsIcons = false;
    }

    public IconAtlas Icons => _icons;

    /// <summary>Панель со тенью, заливкой и обводкой — базовый блок всего интерфейса.</summary>
    public void Panel(SpriteBatch batch, Rectangle rect)
    {
        Panel(batch, rect, UiPalette.Panel, UiPalette.Border);
    }

    public void Panel(SpriteBatch batch, Rectangle rect, Color fill, Color border)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var shadow = new Rectangle(rect.X + 3, rect.Y + 4, rect.Width, rect.Height);
        DrawNine(batch, _fill, shadow, UiPalette.Shadow);
        DrawNine(batch, _fill, rect, fill);
        DrawNine(batch, _border, rect, border);
    }

    /// <summary>Кнопка панели инструментов. Активная светится янтарным, под мышкой — светлее.</summary>
    public void Button(SpriteBatch batch, Rectangle rect, bool active, bool hovered)
    {
        ArgumentNullException.ThrowIfNull(batch);

        Color fill = active ? UiPalette.ButtonActive : (hovered ? UiPalette.ButtonHover : UiPalette.Button);
        DrawNine(batch, _fill, rect, fill);
        DrawNine(batch, _border, rect, UiPalette.Border);

        if (hovered && !active)
        {
            var top = new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(2, rect.Height / 6));
            DrawNine(batch, _fill, top, new Color(255, 255, 255) * 0.10f);
        }
    }

    /// <summary>Табличка с именем поселения поверх карты.</summary>
    public void Plate(SpriteBatch batch, Rectangle rect, Color accent)
    {
        ArgumentNullException.ThrowIfNull(batch);

        DrawNine(batch, _fill, new Rectangle(rect.X + 2, rect.Y + 3, rect.Width, rect.Height), UiPalette.Shadow);
        DrawNine(batch, _fill, rect, UiPalette.Panel);
        DrawNine(batch, _border, rect, accent);
    }

    public void Icon(SpriteBatch batch, IconKind kind, Rectangle rect)
    {
        Icon(batch, kind, rect, Color.White);
    }

    public void Icon(SpriteBatch batch, IconKind kind, Rectangle rect, Color tint)
    {
        ArgumentNullException.ThrowIfNull(batch);

        batch.Draw(_icons.Texture, rect, _icons.Source(kind), tint);
    }

    /// <summary>Значок целым масштабом: пиксели остаются ровными и не замыливаются.</summary>
    public void Icon(SpriteBatch batch, IconKind kind, int x, int y, int scale)
    {
        int size = IconAtlas.IconSize * Math.Max(1, scale);
        Icon(batch, kind, new Rectangle(x, y, size, size), Color.White);
    }

    /// <summary>Вертикальный разделитель групп кнопок.</summary>
    public void Separator(SpriteBatch batch, int x, int y, int height)
    {
        ArgumentNullException.ThrowIfNull(batch);

        batch.Draw(_fill, new Rectangle(x, y, 2, height), new Rectangle(Slice, Slice, Middle, Middle), UiPalette.Border);
    }

    private static Texture2D BuildRounded(GraphicsDevice device, bool border)
    {
        int size = TextureSize;
        var pixels = new Color[size * size];
        float radius = Slice;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Math.Clamp(px, radius, size - radius);
                float cy = Math.Clamp(py, radius, size - radius);
                float dx = px - cx;
                float dy = py - cy;
                float distance = MathF.Sqrt((dx * dx) + (dy * dy));

                bool inside = distance <= radius;
                bool solid = border ? inside && distance > radius - 2f : inside;
                pixels[(y * size) + x] = solid ? Color.White : Color.Transparent;
            }
        }

        var texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Растяжка девятью кусками: углы остаются круглыми на любом размере.</summary>
    private static void DrawNine(SpriteBatch batch, Texture2D texture, Rectangle dest, Color color)
    {
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return;
        }

        if (dest.Width < (Slice * 2) + Middle || dest.Height < (Slice * 2) + Middle)
        {
            batch.Draw(texture, dest, color);
            return;
        }

        int s = Slice;
        int m = Middle;
        int innerWidth = dest.Width - (s * 2);
        int innerHeight = dest.Height - (s * 2);
        int right = dest.Right - s;
        int bottom = dest.Bottom - s;

        // углы
        batch.Draw(texture, new Rectangle(dest.X, dest.Y, s, s), new Rectangle(0, 0, s, s), color);
        batch.Draw(texture, new Rectangle(right, dest.Y, s, s), new Rectangle(s + m, 0, s, s), color);
        batch.Draw(texture, new Rectangle(dest.X, bottom, s, s), new Rectangle(0, s + m, s, s), color);
        batch.Draw(texture, new Rectangle(right, bottom, s, s), new Rectangle(s + m, s + m, s, s), color);

        // края
        batch.Draw(texture, new Rectangle(dest.X + s, dest.Y, innerWidth, s), new Rectangle(s, 0, m, s), color);
        batch.Draw(texture, new Rectangle(dest.X + s, bottom, innerWidth, s), new Rectangle(s, s + m, m, s), color);
        batch.Draw(texture, new Rectangle(dest.X, dest.Y + s, s, innerHeight), new Rectangle(0, s, s, m), color);
        batch.Draw(texture, new Rectangle(right, dest.Y + s, s, innerHeight), new Rectangle(s + m, s, s, m), color);

        // середина
        batch.Draw(
            texture,
            new Rectangle(dest.X + s, dest.Y + s, innerWidth, innerHeight),
            new Rectangle(s, s, m, m),
            color);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fill.Dispose();
        _border.Dispose();
        if (_ownsIcons)
        {
            _icons.Dispose();
        }
    }
}
