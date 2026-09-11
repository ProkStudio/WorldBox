using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Roads;

namespace WorldBox.Render;

/// <summary>
/// Дороги. Издали всё полотно уходит одной текстурой размером с карту — один
/// пиксель на тайл, один прямоугольник на кадр, пересборка только при изменении сети.
/// Вблизи текстура сменяется живым полотном: у каждого тайла своя колея, отводы к соседям,
/// у тракта — камни, у железной дороги — шпалы и рельсы, у шоссе — осевая разметка.
/// Вид меняется с эпохой сам собой: уровень полотна ставит система дорог, а не рисовалка.
/// </summary>
public sealed class RoadRenderer : IDisposable
{
    /// <summary>Не чаще чем раз в столько кадров пересобираем текстуру дорог.</summary>
    public const int RebuildEveryFrames = 10;

    /// <summary>С этого зума рисуется живое полотно, а не текстура.</summary>
    public const float DetailZoom = 13f;

    private static readonly Color TrailBody = new Color(151, 127, 95);
    private static readonly Color RoadBody = new Color(139, 113, 80);
    private static readonly Color HighwayBody = new Color(150, 145, 136);
    private static readonly Color RailBody = new Color(96, 88, 78);
    private static readonly Color AsphaltBody = new Color(76, 76, 84);
    private static readonly Color StoneTrim = new Color(178, 173, 164);
    private static readonly Color DirtTrim = new Color(168, 143, 106);
    private static readonly Color Sleeper = new Color(72, 56, 40);
    private static readonly Color RailMetal = new Color(196, 198, 206);
    private static readonly Color LaneMark = new Color(236, 216, 126);

    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly int _width;
    private readonly int _height;

    private int _builtVersion = -1;
    private int _sinceRebuild;

    public RoadRenderer(GraphicsDevice device, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер карты должен быть положительным.");
        }

        _width = width;
        _height = height;
        _pixels = new Color[width * height];
        _texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
    }

    public bool Visible { get; set; } = true;

    /// <summary>Сколько тайлов дороги отрисовано вблизи в последнем кадре.</summary>
    public int DrawnTiles { get; private set; }

    /// <summary>Заставляет пересобрать текстуру. Нужно после смены мира.</summary>
    public void Invalidate()
    {
        _builtVersion = -1;
        _sinceRebuild = RebuildEveryFrames;
    }

    public void Draw(SpriteBatch batch, Primitives primitives, Camera2D camera, RoadNetwork roads)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(roads);

        DrawnTiles = 0;
        if (!Visible || roads.Tiles == 0)
        {
            return;
        }

        _sinceRebuild++;
        if (_builtVersion != roads.Version && _sinceRebuild >= RebuildEveryFrames)
        {
            Rebuild(roads);
            _builtVersion = roads.Version;
            _sinceRebuild = 0;
        }

        if (camera.Zoom < DetailZoom)
        {
            batch.Draw(_texture, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            return;
        }

        DrawDetailed(batch, primitives, camera, roads);
    }

    private void Rebuild(RoadNetwork roads)
    {
        byte[] level = roads.Level;

        for (int i = 0; i < _pixels.Length; i++)
        {
            byte value = level[i];
            if (value == RoadNetwork.NoRoad)
            {
                _pixels[i] = Color.Transparent;
                continue;
            }

            // Издали дорога шириной в тайл выглядит жирной, поэтому полотно полупрозрачно:
            // линия читается, но не закрашивает биом целиком.
            _pixels[i] = BodyOf(value) * (value >= RoadNetwork.Highway ? 0.95f : 0.8f);
        }

        _texture.SetData(_pixels);
    }

    private void DrawDetailed(SpriteBatch batch, Primitives primitives, Camera2D camera, RoadNetwork roads)
    {
        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 1);

        if (minX < 0)
        {
            minX = 0;
        }

        if (minY < 0)
        {
            minY = 0;
        }

        if (maxX > _width - 1)
        {
            maxX = _width - 1;
        }

        if (maxY > _height - 1)
        {
            maxY = _height - 1;
        }

        Texture2D pixel = primitives.Pixel;
        byte[] level = roads.Level;
        int drawn = 0;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                byte value = level[row + x];
                if (value == RoadNetwork.NoRoad)
                {
                    continue;
                }

                DrawTile(batch, pixel, roads, x, y, value);
                drawn++;
            }
        }

        DrawnTiles = drawn;
    }

    private static void DrawTile(SpriteBatch batch, Texture2D pixel, RoadNetwork roads, int x, int y, byte level)
    {
        float width = WidthOf(level);
        float half = width * 0.5f;
        float centerX = x + 0.5f;
        float centerY = y + 0.5f;
        Color body = BodyOf(level);

        bool west = roads.LevelAt(x - 1, y) != RoadNetwork.NoRoad;
        bool east = roads.LevelAt(x + 1, y) != RoadNetwork.NoRoad;
        bool north = roads.LevelAt(x, y - 1) != RoadNetwork.NoRoad;
        bool south = roads.LevelAt(x, y + 1) != RoadNetwork.NoRoad;

        Quad(batch, pixel, centerX - half, centerY - half, width, width, body);

        if (west)
        {
            Quad(batch, pixel, x, centerY - half, 0.5f + half, width, body);
        }

        if (east)
        {
            Quad(batch, pixel, centerX - half, centerY - half, 0.5f + half, width, body);
        }

        if (north)
        {
            Quad(batch, pixel, centerX - half, y, width, 0.5f + half, body);
        }

        if (south)
        {
            Quad(batch, pixel, centerX - half, centerY - half, width, 0.5f + half, body);
        }

        // Угловой стык: если дорога ушла по диагонали, без заплатки осталась бы дырка.
        DrawCorner(batch, pixel, roads, x, y, 1, 1, east, south, width, body);
        DrawCorner(batch, pixel, roads, x, y, -1, 1, west, south, width, body);
        DrawCorner(batch, pixel, roads, x, y, 1, -1, east, north, width, body);
        DrawCorner(batch, pixel, roads, x, y, -1, -1, west, north, width, body);

        bool horizontal = west || east;

        switch (level)
        {
            case RoadNetwork.Rail:
                DrawRail(batch, pixel, x, y, centerX, centerY, width, horizontal);
                break;
            case RoadNetwork.Motorway:
                DrawLaneMarks(batch, pixel, x, y, centerX, centerY, width, horizontal);
                break;
            case RoadNetwork.Highway:
                DrawStones(batch, pixel, x, y, centerX, centerY, width, StoneTrim);
                break;
            case RoadNetwork.Paved:
            case RoadNetwork.Trail:
                DrawStones(batch, pixel, x, y, centerX, centerY, width, DirtTrim);
                break;
            default:
                break;
        }
    }

    private static void DrawCorner(
        SpriteBatch batch,
        Texture2D pixel,
        RoadNetwork roads,
        int x,
        int y,
        int stepX,
        int stepY,
        bool alongX,
        bool alongY,
        float width,
        Color body)
    {
        if (alongX || alongY)
        {
            return;
        }

        if (roads.LevelAt(x + stepX, y + stepY) == RoadNetwork.NoRoad)
        {
            return;
        }

        float half = width * 0.5f;
        float cornerX = stepX > 0 ? x + 1f : (float)x;
        float cornerY = stepY > 0 ? y + 1f : (float)y;
        Quad(batch, pixel, cornerX - half, cornerY - half, width, width, body);
    }

    /// <summary>Шпалы поперёк пути и две нитки рельсов вдоль.</summary>
    private static void DrawRail(
        SpriteBatch batch,
        Texture2D pixel,
        int x,
        int y,
        float centerX,
        float centerY,
        float width,
        bool horizontal)
    {
        const int Ties = 4;
        float step = 1f / Ties;
        float tie = width * 1.25f;
        float thickness = 0.07f;
        float rail = MathF.Max(0.045f, width * 0.16f);
        float offset = width * 0.28f;

        for (int i = 0; i < Ties; i++)
        {
            float along = (i + 0.5f) * step;
            if (horizontal)
            {
                Quad(batch, pixel, x + along - (thickness * 0.5f), centerY - (tie * 0.5f), thickness, tie, Sleeper);
            }
            else
            {
                Quad(batch, pixel, centerX - (tie * 0.5f), y + along - (thickness * 0.5f), tie, thickness, Sleeper);
            }
        }

        if (horizontal)
        {
            Quad(batch, pixel, x, centerY - offset - (rail * 0.5f), 1f, rail, RailMetal);
            Quad(batch, pixel, x, centerY + offset - (rail * 0.5f), 1f, rail, RailMetal);
        }
        else
        {
            Quad(batch, pixel, centerX - offset - (rail * 0.5f), y, rail, 1f, RailMetal);
            Quad(batch, pixel, centerX + offset - (rail * 0.5f), y, rail, 1f, RailMetal);
        }
    }

    /// <summary>Осевая разметка шоссе: короткие штрихи вдоль полотна.</summary>
    private static void DrawLaneMarks(
        SpriteBatch batch,
        Texture2D pixel,
        int x,
        int y,
        float centerX,
        float centerY,
        float width,
        bool horizontal)
    {
        float mark = MathF.Max(0.05f, width * 0.12f);
        float dash = 0.26f;

        for (int i = 0; i < 2; i++)
        {
            float along = 0.2f + (i * 0.45f);
            if (horizontal)
            {
                Quad(batch, pixel, x + along, centerY - (mark * 0.5f), dash, mark, LaneMark);
            }
            else
            {
                Quad(batch, pixel, centerX - (mark * 0.5f), y + along, mark, dash, LaneMark);
            }
        }
    }

    /// <summary>Камни и выбоины: полотно перестаёт быть ровной полоской краски.</summary>
    private static void DrawStones(
        SpriteBatch batch,
        Texture2D pixel,
        int x,
        int y,
        float centerX,
        float centerY,
        float width,
        Color color)
    {
        float size = MathF.Max(0.06f, width * 0.2f);

        for (int i = 0; i < 3; i++)
        {
            uint hash = Hash(x, y, i);
            float dx = ((hash & 63u) / 63f) - 0.5f;
            float dy = (((hash >> 6) & 63u) / 63f) - 0.5f;
            float spread = width * 0.8f;
            Quad(
                batch,
                pixel,
                centerX + (dx * spread) - (size * 0.5f),
                centerY + (dy * spread) - (size * 0.5f),
                size,
                size,
                color);
        }
    }

    /// <summary>Ширина полотна в тайлах: тропа узкая, шоссе широкое.</summary>
    public static float WidthOf(byte level)
    {
        return level switch
        {
            RoadNetwork.Trail => 0.26f,
            RoadNetwork.Paved => 0.40f,
            RoadNetwork.Highway => 0.50f,
            RoadNetwork.Rail => 0.34f,
            RoadNetwork.Motorway => 0.58f,
            _ => 0.3f,
        };
    }

    /// <summary>Цвет полотна по уровню.</summary>
    public static Color BodyOf(byte level)
    {
        return level switch
        {
            RoadNetwork.Trail => TrailBody,
            RoadNetwork.Paved => RoadBody,
            RoadNetwork.Highway => HighwayBody,
            RoadNetwork.Rail => RailBody,
            RoadNetwork.Motorway => AsphaltBody,
            _ => RoadBody,
        };
    }

    private static void Quad(
        SpriteBatch batch,
        Texture2D pixel,
        float left,
        float top,
        float width,
        float height,
        Color color)
    {
        batch.Draw(
            pixel,
            new Vector2(left, top),
            null,
            color,
            0f,
            Vector2.Zero,
            new Vector2(width, height),
            SpriteEffects.None,
            0f);
    }

    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)((salt + 1) * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    public void Dispose()
    {
        _texture.Dispose();
    }
}
