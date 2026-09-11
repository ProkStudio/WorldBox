using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.People;

namespace WorldBox.Render;

/// <summary>
/// Жители на карте. Пока это точки: цвет говорит о голоде, размер — о зуме.
/// На близком зуме под точкой рисуется тёмная подложка, иначе человечки теряются на траве.
/// Издали люди вообще не рисуются: там будет тепловая карта плотности и границы государств.
/// Спрайты человечков с анимацией ходьбы появятся когда у людей появятся занятия и племена.
/// </summary>
public sealed class PeopleRenderer
{
    /// <summary>Дальше этого зума точки сливаются в кашу и только едят кадр.</summary>
    public const float MinZoom = 3f;

    /// <summary>С этого зума включается подложка под точкой.</summary>
    public const float DetailZoom = 12f;

    private static readonly Color Healthy = new Color(246, 226, 186);
    private static readonly Color Starving = new Color(206, 92, 66);
    private static readonly Color Shadow = new Color(8, 9, 12) * 0.7f;

    /// <summary>Сколько точек ушло в последний кадр. Нужно для замеров.</summary>
    public int DrawnPeople { get; private set; }

    public void Draw(SpriteBatch batch, Primitives primitives, Camera2D camera, Population people)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(people);

        DrawnPeople = 0;

        float zoom = camera.Zoom;
        if (zoom < MinZoom)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 1);

        // Точка не должна становиться меньше полутора пикселей экрана, иначе люди пропадают.
        float size = MathF.Max(0.34f, 1.6f / zoom);
        float shadowPad = 1.2f / zoom;
        bool detailed = zoom >= DetailZoom;

        var scale = new Vector2(size, size);
        var shadowScale = new Vector2(size + (shadowPad * 2f), size + (shadowPad * 2f));
        Texture2D pixel = primitives.Pixel;

        int high = people.HighWater;
        int drawn = 0;

        for (int i = 0; i < high; i++)
        {
            if (!people.Alive[i])
            {
                continue;
            }

            int x = people.X[i];
            int y = people.Y[i];

            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                continue;
            }

            // Смещение внутри тайла, чтобы соседи не сливались в один квадрат.
            uint hash = Hash(i);
            float offsetX = 0.12f + (((hash >> 3) & 7u) * 0.085f);
            float offsetY = 0.12f + (((hash >> 9) & 7u) * 0.085f);
            var position = new Vector2(x + offsetX, y + offsetY);

            float hunger = people.Hunger[i];
            Color color = Color.Lerp(Healthy, Starving, MathHelper.Clamp(hunger, 0f, 1f));

            if (detailed)
            {
                var shadowPosition = new Vector2(position.X - shadowPad, position.Y - shadowPad);
                batch.Draw(pixel, shadowPosition, null, Shadow, 0f, Vector2.Zero, shadowScale, SpriteEffects.None, 0f);
            }

            batch.Draw(pixel, position, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            drawn++;
        }

        DrawnPeople = drawn;
    }

    private static uint Hash(int value)
    {
        unchecked
        {
            uint h = (uint)(value * 374761393) + 2654435761u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
