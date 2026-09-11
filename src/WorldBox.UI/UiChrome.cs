using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Render;

namespace WorldBox.UI;

/// <summary>
/// Общая отрисовка фона панелей. Если скин передан, берём круглые углы из него,
/// иначе рисуем теми же цветами простым прямоугольником.
/// Нужно, чтобы панели работали и в тестах, где никаких атласов нет.
/// </summary>
internal static class UiChrome
{
    internal static void Panel(SpriteBatch batch, Primitives primitives, UiSkin? skin, Rectangle rect)
    {
        if (skin != null)
        {
            skin.Panel(batch, rect);
            return;
        }

        primitives.FillRect(batch, rect, UiPalette.Panel);
        primitives.FrameRect(batch, rect, UiPalette.Border, 1);
    }

    internal static void Plate(SpriteBatch batch, Primitives primitives, UiSkin? skin, Rectangle rect, Color accent)
    {
        if (skin != null)
        {
            skin.Plate(batch, rect, accent);
            return;
        }

        primitives.FillRect(batch, rect, UiPalette.Panel);
        primitives.FrameRect(batch, rect, accent, 1);
    }
}
