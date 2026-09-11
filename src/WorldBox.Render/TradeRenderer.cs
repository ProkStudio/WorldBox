using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Economy;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Торговые пути на карте. Линия идёт из поселения в поселение и красится цветом народа-отправителя.
/// Чем больше товара прошло в последний прогон, тем ярче линия: оживлённые направления видно сразу,
/// а заглохшие тускнеют и исчезают вместе с путём на ближайшей пересборке сети.
/// Морские пути рисуются пунктиром — так на глаз отличаешь караван от корабля.
///
/// Рисуется в тайловых координатах поверх карты: матрица камеры уже в SpriteBatch.
/// Пути за экраном отсекаются по рамке видимых тайлов, поэтому при 512 путях в кадр
/// попадают десятки, а не все.
/// </summary>
public sealed class TradeRenderer
{
    /// <summary>Ниже этого зума линии сливаются в кашу и не рисуются.</summary>
    public const float MinZoom = 1.6f;

    /// <summary>На сколько кусков рвётся морской путь.</summary>
    private const int SeaDashes = 9;

    private const float QuietAlpha = 0.30f;
    private const float BusyAlpha = 0.95f;

    /// <summary>Такой груз за прогон считается оживлённой торговлей.</summary>
    private const float BusyVolume = 12f;

    private static readonly Color Shadow = new Color(10, 11, 15);

    public bool Visible { get; set; } = true;

    /// <summary>Сколько путей ушло на экран в последнем кадре.</summary>
    public int DrawnRoutes { get; private set; }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        TradeNetwork network,
        TribeStore tribes)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(tribes);

        DrawnRoutes = 0;
        if (!Visible || network.Count == 0 || camera.Zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 4);

        // Толщина держится в пикселях экрана: на дальнем зуме линия не пропадает, на ближнем не жиреет.
        float thickness = MathF.Max(0.05f, 1.8f / camera.Zoom);
        int drawn = 0;

        for (int i = 0; i < network.Count; i++)
        {
            int fromX = network.FromX[i];
            int fromY = network.FromY[i];
            int toX = network.ToX[i];
            int toY = network.ToY[i];

            if (Math.Max(fromX, toX) < minX
                || Math.Min(fromX, toX) > maxX
                || Math.Max(fromY, toY) < minY
                || Math.Min(fromY, toY) > maxY)
            {
                continue;
            }

            short tribe = network.FromTribe[i];
            Color color = tribes.IsAlive(tribe)
                ? TribePalette.Of(tribes.ColorIndex[tribe])
                : new Color(150, 150, 150);

            float share = MathF.Min(1f, network.Volume[i] / BusyVolume);
            float alpha = QuietAlpha + ((BusyAlpha - QuietAlpha) * share);

            var from = new Vector2(fromX + 0.5f, fromY + 0.5f);
            var to = new Vector2(toX + 0.5f, toY + 0.5f);

            if (network.Sea[i])
            {
                DrawDashed(batch, primitives, from, to, color * alpha, thickness);
            }
            else
            {
                // Тёмная подложка: цветная нитка читается и на светлом песке, и на тёмном лесу.
                primitives.Line(batch, from, to, Shadow * (alpha * 0.7f), thickness * 2.1f);
                primitives.Line(batch, from, to, color * alpha, thickness);
            }

            DrawPip(batch, primitives, from, color, thickness);
            DrawPip(batch, primitives, to, color, thickness);
            drawn++;
        }

        DrawnRoutes = drawn;
    }

    /// <summary>Морской путь: рвём линию на куски и рисуем через один.</summary>
    private static void DrawDashed(
        SpriteBatch batch,
        Primitives primitives,
        Vector2 from,
        Vector2 to,
        Color color,
        float thickness)
    {
        Vector2 step = (to - from) / SeaDashes;

        for (int i = 0; i < SeaDashes; i += 2)
        {
            Vector2 a = from + (step * i);
            Vector2 b = a + step;
            primitives.Line(batch, a, b, Shadow * 0.55f, thickness * 2.1f);
            primitives.Line(batch, a, b, color, thickness);
        }
    }

    /// <summary>Точка на конце пути: показывает, из какого поселения он растёт.</summary>
    private static void DrawPip(
        SpriteBatch batch,
        Primitives primitives,
        Vector2 point,
        Color color,
        float thickness)
    {
        float size = thickness * 2.6f;
        float half = size * 0.5f;
        primitives.FillRect(batch, point.X - half, point.Y - half, size, size, color);
    }
}
