using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Roads;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Движение по дорогам. По готовым маршрутам ездит то, что народ уже умеет делать:
/// пешие носильщики, телеги, фургоны, поезда, машины, а над всем этим с современности летают
/// самолёты. Вид выбирается по эпохе народа и по уровню полотна: по рельсам всегда идёт
/// поезд, по шоссе — машины, по грунтовке веками скрипят телеги.
///
/// Состояния нет вообще: положение считается от времени и номера машины, поэтому транспорт
/// ничего не весит в симуляции и не ломает повторимость мира. На паузе время не идёт и всё
/// замирает: игра передаёт сюда своё игровое время, а не часы.
/// </summary>
public sealed class TrafficRenderer
{
    /// <summary>Ниже этого зума машина меньше пикселя — рисовать нечего.</summary>
    public const float MinZoom = 7f;

    /// <summary>С этого зума видны мелочи: колёса, тенты, дым, фары.</summary>
    public const float DetailZoom = 16f;

    private const byte Walker = 0;
    private const byte Cart = 1;
    private const byte Wagon = 2;
    private const byte Train = 3;
    private const byte Car = 4;

    private static readonly Color Shadow = new Color(10, 12, 16) * 0.38f;
    private static readonly Color Skin = new Color(226, 190, 150);
    private static readonly Color Pack = new Color(138, 106, 68);
    private static readonly Color WoodDark = new Color(96, 68, 42);
    private static readonly Color Wood = new Color(134, 98, 60);
    private static readonly Color Canopy = new Color(226, 219, 198);
    private static readonly Color Ox = new Color(84, 66, 52);
    private static readonly Color Iron = new Color(58, 58, 64);
    private static readonly Color Steel = new Color(148, 152, 162);
    private static readonly Color Smoke = new Color(214, 214, 214) * 0.5f;
    private static readonly Color Glass = new Color(122, 172, 206);
    private static readonly Color Headlight = new Color(255, 240, 170);
    private static readonly Color PlaneBody = new Color(224, 228, 236);
    private static readonly Color PlaneTrim = new Color(90, 96, 112);

    public bool Visible { get; set; } = true;

    /// <summary>Сколько единиц транспорта ушло в последний кадр. Нужно для замеров.</summary>
    public int DrawnVehicles { get; private set; }

    /// <summary>Сколько самолётов в воздухе.</summary>
    public int DrawnPlanes { get; private set; }

    /// <summary>Что ездит по такому полотну у народа такой эпохи.</summary>
    private static byte KindFor(int era, byte level)
    {
        if (level == RoadNetwork.Rail)
        {
            return Train;
        }

        if (level == RoadNetwork.Motorway || era >= 9)
        {
            return Car;
        }

        if (era >= 8)
        {
            // Нефть и электричество: первые грузовики ездят по старому тракту.
            return Car;
        }

        if (era >= 5)
        {
            return Wagon;
        }

        return era >= 2 ? Cart : Walker;
    }

    /// <summary>Тайлов в секунду.</summary>
    private static float SpeedOf(byte kind)
    {
        return kind switch
        {
            Train => 3.4f,
            Car => 4.2f,
            Wagon => 1.15f,
            Cart => 0.85f,
            _ => 0.55f,
        };
    }

    /// <summary>Сколько единиц на маршруте: длинный путь держит больше.</summary>
    private static int CountFor(byte kind, int pathLength)
    {
        int room = 1 + (pathLength / 18);

        int limit = kind switch
        {
            Train => 2,
            Car => 4,
            Wagon => 3,
            _ => 3,
        };

        return Math.Min(room, limit);
    }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        RoadNetwork roads,
        TribeStore tribes,
        float seconds)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentNullException.ThrowIfNull(tribes);

        DrawnVehicles = 0;
        DrawnPlanes = 0;

        if (!Visible || camera.Zoom < MinZoom || roads.Count == 0)
        {
            return;
        }

        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 3);

        Texture2D pixel = primitives.Pixel;
        bool detailed = camera.Zoom >= DetailZoom;
        int width = roads.Width;
        int vehicles = 0;
        int planes = 0;

        for (int route = 0; route < roads.Count; route++)
        {
            int length = roads.PathLength[route];
            if (length < 2)
            {
                continue;
            }

            // Маршрут целиком за экраном — ни одну машину не считаем.
            int fromX = roads.FromX[route];
            int fromY = roads.FromY[route];
            int toX = roads.ToX[route];
            int toY = roads.ToY[route];
            int boxMinX = Math.Min(fromX, toX) - length;
            int boxMaxX = Math.Max(fromX, toX) + length;
            int boxMinY = Math.Min(fromY, toY) - length;
            int boxMaxY = Math.Max(fromY, toY) + length;

            if (boxMaxX < minX || boxMinX > maxX || boxMaxY < minY || boxMinY > maxY)
            {
                continue;
            }

            short tribe = roads.FromTribe[route];
            if (!tribes.IsAlive(tribe))
            {
                tribe = roads.ToTribe[route];
                if (!tribes.IsAlive(tribe))
                {
                    continue;
                }
            }

            int era = tribes.Era[tribe];
            byte kind = KindFor(era, roads.RouteLevel[route]);
            Color paint = TribePalette.Of(tribes.ColorIndex[tribe]);
            float speed = SpeedOf(kind);
            float travel = length - 1;
            int count = CountFor(kind, length);
            int start = RoadNetwork.PathStart(route);

            for (int n = 0; n < count; n++)
            {
                uint hash = Hash(route, n);
                bool back = (hash & 1u) == 0u;
                float offset = ((hash >> 4) & 1023u) / 1024f;
                float phase = ((seconds * speed / travel) + offset) % 1f;
                if (back)
                {
                    phase = 1f - phase;
                }

                Sample(roads, start, length, width, phase, out float x, out float y, out float angle);

                if (x < minX || x > maxX || y < minY || y > maxY)
                {
                    continue;
                }

                if (back)
                {
                    angle += MathF.PI;
                }

                DrawVehicle(batch, pixel, kind, x, y, angle, paint, detailed, era, seconds, hash, roads, start, length, width, phase, back);
                vehicles++;
            }

            // Самолёт летит по прямой между городами: ему дорога не нужна.
            if (era >= 9 && ((route + era) % 3) == 0)
            {
                if (DrawPlane(batch, pixel, route, fromX, fromY, toX, toY, seconds, minX, minY, maxX, maxY, detailed))
                {
                    planes++;
                }
            }
        }

        DrawnVehicles = vehicles;
        DrawnPlanes = planes;
    }

    /// <summary>Где и куда смотрит точка на доле пути.</summary>
    private static void Sample(
        RoadNetwork roads,
        int start,
        int length,
        int width,
        float phase,
        out float x,
        out float y,
        out float angle)
    {
        float position = phase * (length - 1);
        int step = (int)position;
        if (step < 0)
        {
            step = 0;
        }

        if (step > length - 2)
        {
            step = length - 2;
        }

        float t = position - step;
        int a = roads.Path[start + step];
        int b = roads.Path[start + step + 1];

        float ax = (a % width) + 0.5f;
        float ay = (a / width) + 0.5f;
        float bx = (b % width) + 0.5f;
        float by = (b / width) + 0.5f;

        x = ax + ((bx - ax) * t);
        y = ay + ((by - ay) * t);
        angle = MathF.Atan2(by - ay, bx - ax);
    }

    private static void DrawVehicle(
        SpriteBatch batch,
        Texture2D pixel,
        byte kind,
        float x,
        float y,
        float angle,
        Color paint,
        bool detailed,
        int era,
        float seconds,
        uint hash,
        RoadNetwork roads,
        int start,
        int length,
        int width,
        float phase,
        bool back)
    {
        switch (kind)
        {
            case Train:
                DrawTrain(batch, pixel, x, y, angle, paint, detailed, era, seconds, roads, start, length, width, phase, back);
                return;
            case Car:
                DrawCar(batch, pixel, x, y, angle, paint, detailed, era, hash);
                return;
            case Wagon:
                DrawWagon(batch, pixel, x, y, angle, paint, detailed);
                return;
            case Cart:
                DrawCart(batch, pixel, x, y, angle, detailed);
                return;
            default:
                DrawWalker(batch, pixel, x, y, angle, paint, detailed);
                return;
        }
    }

    private static void DrawWalker(SpriteBatch batch, Texture2D pixel, float x, float y, float angle, Color paint, bool detailed)
    {
        Oriented(batch, pixel, x + 0.06f, y + 0.08f, 0.3f, 0.24f, angle, Shadow);
        Oriented(batch, pixel, x, y, 0.26f, 0.2f, angle, paint);

        if (detailed)
        {
            // Тюк за спиной и голова: до телег груз таскали на себе.
            Oriented(batch, pixel, x - (MathF.Cos(angle) * 0.14f), y - (MathF.Sin(angle) * 0.14f), 0.16f, 0.2f, angle, Pack);
            Oriented(batch, pixel, x + (MathF.Cos(angle) * 0.1f), y + (MathF.Sin(angle) * 0.1f), 0.12f, 0.12f, angle, Skin);
        }
    }

    private static void DrawCart(SpriteBatch batch, Texture2D pixel, float x, float y, float angle, bool detailed)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Oriented(batch, pixel, x + 0.08f, y + 0.1f, 0.8f, 0.5f, angle, Shadow);

        // Вол впереди, за ним ящик на двух колёсах.
        Oriented(batch, pixel, x + (cos * 0.34f), y + (sin * 0.34f), 0.34f, 0.28f, angle, Ox);
        Oriented(batch, pixel, x - (cos * 0.1f), y - (sin * 0.1f), 0.46f, 0.42f, angle, Wood);

        if (detailed)
        {
            Oriented(batch, pixel, x + (cos * 0.12f), y + (sin * 0.12f), 0.1f, 0.3f, angle, WoodDark);
            Oriented(batch, pixel, x - (cos * 0.2f) - (sin * 0.22f), y - (sin * 0.2f) + (cos * 0.22f), 0.16f, 0.1f, angle, WoodDark);
            Oriented(batch, pixel, x - (cos * 0.2f) + (sin * 0.22f), y - (sin * 0.2f) - (cos * 0.22f), 0.16f, 0.1f, angle, WoodDark);
        }
    }

    private static void DrawWagon(SpriteBatch batch, Texture2D pixel, float x, float y, float angle, Color paint, bool detailed)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Oriented(batch, pixel, x + 0.1f, y + 0.12f, 1f, 0.56f, angle, Shadow);
        Oriented(batch, pixel, x + (cos * 0.44f), y + (sin * 0.44f), 0.36f, 0.3f, angle, Ox);
        Oriented(batch, pixel, x - (cos * 0.08f), y - (sin * 0.08f), 0.7f, 0.46f, angle, Wood);

        // Тент фургона с цветной полосой хозяина.
        Oriented(batch, pixel, x - (cos * 0.08f), y - (sin * 0.08f), 0.58f, 0.5f, angle, Canopy);
        Oriented(batch, pixel, x - (cos * 0.28f), y - (sin * 0.28f), 0.1f, 0.46f, angle, paint);

        if (detailed)
        {
            Oriented(batch, pixel, x - (cos * 0.24f) - (sin * 0.26f), y - (sin * 0.24f) + (cos * 0.26f), 0.2f, 0.12f, angle, WoodDark);
            Oriented(batch, pixel, x - (cos * 0.24f) + (sin * 0.26f), y - (sin * 0.24f) - (cos * 0.26f), 0.2f, 0.12f, angle, WoodDark);
        }
    }

    private static void DrawTrain(
        SpriteBatch batch,
        Texture2D pixel,
        float x,
        float y,
        float angle,
        Color paint,
        bool detailed,
        int era,
        float seconds,
        RoadNetwork roads,
        int start,
        int length,
        int width,
        float phase,
        bool back)
    {
        Oriented(batch, pixel, x + 0.12f, y + 0.14f, 1.3f, 0.66f, angle, Shadow);
        Oriented(batch, pixel, x, y, 1.15f, 0.56f, angle, Iron);

        if (detailed)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            Oriented(batch, pixel, x + (cos * 0.42f), y + (sin * 0.42f), 0.2f, 0.5f, angle, Steel);

            if (era <= 8)
            {
                // Паровоз курит: два клубка встают и тают назад по ходу.
                float puff = seconds % 1.4f;
                Oriented(batch, pixel, x - (cos * puff * 0.8f), y - (sin * puff * 0.8f) - (puff * 0.3f), 0.3f + puff, 0.3f + puff, 0f, Smoke * (1f - (puff / 1.4f)));
            }
        }

        // Вагоны тянутся по тому же пути позади паровоза, а не по прямой.
        float travel = length - 1;
        for (int car = 1; car <= 3; car++)
        {
            float shift = car * 1.25f / travel;
            float tail = back ? phase + shift : phase - shift;
            if (tail < 0f || tail > 1f)
            {
                continue;
            }

            Sample(roads, start, length, width, tail, out float cx, out float cy, out float ca);
            Oriented(batch, pixel, cx + 0.1f, cy + 0.12f, 1.05f, 0.6f, ca, Shadow);
            Oriented(batch, pixel, cx, cy, 0.95f, 0.5f, ca, paint);

            if (detailed)
            {
                Oriented(batch, pixel, cx, cy, 0.8f, 0.18f, ca, Iron);
            }
        }
    }

    private static void DrawCar(SpriteBatch batch, Texture2D pixel, float x, float y, float angle, Color paint, bool detailed, int era, uint hash)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        bool truck = (hash & 4u) == 0u;
        float len = truck ? 0.9f : 0.66f;
        float wide = truck ? 0.44f : 0.38f;

        Oriented(batch, pixel, x + 0.08f, y + 0.1f, len + 0.14f, wide + 0.12f, angle, Shadow);

        // Цвет кузова приглушён до автомобильного: чистые цвета народа рябят.
        Color body = Color.Lerp(paint, Steel, truck ? 0.45f : 0.25f);
        Oriented(batch, pixel, x, y, len, wide, angle, body);

        if (!detailed)
        {
            return;
        }

        if (truck)
        {
            Oriented(batch, pixel, x - (cos * 0.16f), y - (sin * 0.16f), 0.5f, wide - 0.06f, angle, Canopy);
            Oriented(batch, pixel, x + (cos * 0.3f), y + (sin * 0.3f), 0.16f, wide - 0.1f, angle, Glass);
        }
        else
        {
            Oriented(batch, pixel, x + (cos * 0.08f), y + (sin * 0.08f), 0.22f, wide - 0.08f, angle, Glass);
        }

        if (era >= 9)
        {
            Oriented(batch, pixel, x + (cos * (len * 0.48f)) - (sin * 0.12f), y + (sin * (len * 0.48f)) + (cos * 0.12f), 0.08f, 0.08f, angle, Headlight);
            Oriented(batch, pixel, x + (cos * (len * 0.48f)) + (sin * 0.12f), y + (sin * (len * 0.48f)) - (cos * 0.12f), 0.08f, 0.08f, angle, Headlight);
        }
    }

    /// <summary>Самолёт над маршрутом. Возвращает правду, если оказался в кадре.</summary>
    private static bool DrawPlane(
        SpriteBatch batch,
        Texture2D pixel,
        int route,
        int fromX,
        int fromY,
        int toX,
        int toY,
        float seconds,
        int minX,
        int minY,
        int maxX,
        int maxY,
        bool detailed)
    {
        float dx = toX - fromX;
        float dy = toY - fromY;
        float span = MathF.Sqrt((dx * dx) + (dy * dy));
        if (span < 6f)
        {
            return false;
        }

        uint hash = Hash(route, 77);
        float phase = ((seconds * 7.5f / span) + (((hash >> 3) & 511u) / 512f)) % 1f;
        bool back = (hash & 2u) == 0u;
        float t = back ? 1f - phase : phase;

        float x = fromX + 0.5f + (dx * t);
        float y = fromY + 0.5f + (dy * t);
        float angle = MathF.Atan2(dy, dx) + (back ? MathF.PI : 0f);

        // Высота: самолёт смещён вверх, тень остаётся на земле — видно, что летит.
        float lift = 2.2f;
        if (x < minX || x > maxX || (y - lift) < minY || y > maxY)
        {
            return false;
        }

        Oriented(batch, pixel, x + 0.3f, y + 0.2f, 1.2f, 0.3f, angle, Shadow);
        Oriented(batch, pixel, x + 0.3f, y + 0.2f, 0.4f, 1f, angle, Shadow);

        float fy = y - lift;
        Oriented(batch, pixel, x, fy, 1.5f, 0.34f, angle, PlaneBody);
        Oriented(batch, pixel, x, fy, 0.42f, 1.35f, angle, PlaneBody);

        if (detailed)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            Oriented(batch, pixel, x - (cos * 0.62f), y - lift - (sin * 0.62f), 0.2f, 0.6f, angle, PlaneTrim);
            Oriented(batch, pixel, x + (cos * 0.6f), y - lift + (sin * 0.6f), 0.18f, 0.22f, angle, Glass);
        }

        return true;
    }

    /// <summary>Прямоугольник с поворотом вокруг своего центра, в тайлах.</summary>
    private static void Oriented(
        SpriteBatch batch,
        Texture2D pixel,
        float centerX,
        float centerY,
        float length,
        float width,
        float rotation,
        Color color)
    {
        batch.Draw(
            pixel,
            new Vector2(centerX, centerY),
            null,
            color,
            rotation,
            new Vector2(0.5f, 0.5f),
            new Vector2(length, width),
            SpriteEffects.None,
            0f);
    }

    private static uint Hash(int route, int index)
    {
        unchecked
        {
            uint h = (uint)(route * 374761393) + (uint)(index * 668265263) + 2654435761u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
