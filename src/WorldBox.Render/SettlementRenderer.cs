using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;

namespace WorldBox.Render;

/// <summary>
/// Поселения вблизи. Раньше на весь город рисовался один домик, и в ближнем плане мир
/// выглядел полем башенок. Теперь город — это кварталы: сетка участков, каждая третья линия
/// сетки оставлена под улицу, домов столько, сколько жителей, в центре главное здание с флагом,
/// по кромке стена с воротами и башнями, за стеной поля, а на поздних эпохах над кварталами
/// стоит заводской дым и горят окна. Издалека работают значки <see cref="TerritoryRenderer"/>:
/// спрайт размером в пиксель всё равно не прочитать.
///
/// Раскладка нигде не хранится: и участок, и вид дома считаются из номера поселения и номера
/// клетки сетки, поэтому картинка не дёргается между кадрами, а память под города не нужна.
/// Аллокаций в кадре нет. Дальность деталей растёт ступенями (дома, земля, мелочи, камни),
/// иначе на ближнем плане кадр съедала бы мостовая.
/// </summary>
public sealed class SettlementRenderer
{
    /// <summary>Ниже этого зума кварталы не рисуются, остаются значки на карте.</summary>
    public const float MinZoom = 12f;

    /// <summary>С этого зума под городом появляется земля: двор, улицы, поля.</summary>
    public const float GroundZoom = 15f;

    /// <summary>С этого зума видно мелочи: грядки, окна, дым.</summary>
    public const float DetailZoom = 20f;

    /// <summary>С этого зума ровная заливка двора заменяется отдельными камнями.</summary>
    public const float CobbleZoom = 26f;

    /// <summary>Шаг застройки в тайлах: один участок под один дом.</summary>
    private const float Plot = 1.15f;

    /// <summary>Каждая третья линия участков — улица, между улицами кварталы два на два.</summary>
    private const int Street = 3;

    /// <summary>Размер камня мостовой в тайлах: четыре на четыре камня на клетку.</summary>
    private const float Cell = 0.25f;

    /// <summary>Сколько жителей приходится на один дом.</summary>
    private const int PeoplePerHouse = 3;

    /// <summary>Предел домов на город. Держит кадр, когда город разрастается.</summary>
    private const int MaxHouses = 160;

    /// <summary>Во сколько раз площадь круга больше числа домов: улицы и дворы съедают участки.</summary>
    private const float HouseDensity = 1.25f;

    private static readonly Color ShadowColor = new Color(12, 16, 20) * 0.35f;
    private static readonly Color PoleColor = new Color(66, 48, 34);
    private static readonly Color StoneColor = new Color(152, 148, 140);
    private static readonly Color StoneDark = new Color(116, 112, 106);
    private static readonly Color StoneLight = new Color(182, 178, 170);
    private static readonly Color DirtColor = new Color(146, 118, 84);
    private static readonly Color DirtDark = new Color(112, 90, 64);
    private static readonly Color DirtLight = new Color(170, 142, 104);
    private static readonly Color AsphaltColor = new Color(78, 80, 86);
    private static readonly Color AsphaltDark = new Color(58, 60, 66);
    private static readonly Color AsphaltLight = new Color(104, 106, 112);
    private static readonly Color FieldSoil = new Color(120, 92, 60);
    private static readonly Color FieldCrop = new Color(198, 170, 96);
    private static readonly Color FieldFurrow = new Color(150, 124, 64);

    private readonly DecorAtlas _atlas;

    public SettlementRenderer(DecorAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
    }

    public bool Visible { get; set; } = true;

    /// <summary>Сколько построек ушло в последний кадр: главные здания, дома, башни.</summary>
    public int DrawnBuildings { get; private set; }

    /// <summary>Сколько клеток и камней двора ушло в последний кадр. Самая толстая часть замера.</summary>
    public int DrawnPavingCells { get; private set; }

    /// <summary>Сколько полей вокруг городов ушло в последний кадр.</summary>
    public int DrawnFields { get; private set; }

    /// <summary>Чем вымощен двор: чем позже эпоха, тем твёрже покрытие.</summary>
    private enum Surface : byte
    {
        Dirt = 0,
        Stone = 1,
        Asphalt = 2,
    }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        WorldMap map,
        TribeStore tribes,
        SettlementStore settlements,
        float seconds)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);

        DrawnBuildings = 0;
        DrawnPavingCells = 0;
        DrawnFields = 0;

        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        // Запас по краям большой: у города радиус до восьми тайлов плюс поля за стеной.
        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 14);

        Texture2D pixel = primitives.Pixel;
        bool ground = camera.Zoom >= GroundZoom;
        bool detail = camera.Zoom >= DetailZoom;
        bool cobble = camera.Zoom >= CobbleZoom;
        int high = settlements.HighWater;
        int cells = 0;
        int fields = 0;
        int buildings = 0;

        // Первый проход: земля всех городов. Иначе двор соседа ложится поверх готовых домов.
        if (ground)
        {
            for (int i = 0; i < high; i++)
            {
                if (!InView(tribes, settlements, i, minX, minY, maxX, maxY))
                {
                    continue;
                }

                CityPlan plan = Plan(tribes, settlements, i);
                cells += DrawGround(batch, pixel, map, in plan, cobble);
                fields += DrawFields(batch, pixel, map, in plan, detail);
            }
        }

        for (int i = 0; i < high; i++)
        {
            if (!InView(tribes, settlements, i, minX, minY, maxX, maxY))
            {
                continue;
            }

            CityPlan plan = Plan(tribes, settlements, i);
            byte colorIndex = tribes.ColorIndex[settlements.Tribe[i]];

            // Стена идёт до домов: она лежит по кромке круга, дома стоят внутри и её не закрывают.
            if (plan.Decor == SettlementDecor.Walls || plan.Decor == SettlementDecor.Castle)
            {
                buildings += DrawWalls(batch, primitives, map, in plan);
            }

            buildings += DrawQuarters(batch, pixel, map, in plan, colorIndex, seconds, detail);
        }

        DrawnBuildings = buildings;
        DrawnPavingCells = cells;
        DrawnFields = fields;
    }

    /// <summary>Живое поселение живого народа в видимом окне.</summary>
    private static bool InView(
        TribeStore tribes,
        SettlementStore settlements,
        int index,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        if (!settlements.Alive[index])
        {
            return false;
        }

        int x = settlements.X[index];
        int y = settlements.Y[index];
        if (x < minX || x > maxX || y < minY || y > maxY)
        {
            return false;
        }

        return tribes.IsAlive(settlements.Tribe[index]);
    }

    /// <summary>Раскладка города считается из номера поселения, уровня, эпохи и числа жителей.</summary>
    private static CityPlan Plan(TribeStore tribes, SettlementStore settlements, int index)
    {
        byte level = settlements.Level[index];
        short tribe = settlements.Tribe[index];
        int era = tribes.Era[tribe];
        int people = settlements.People[index];

        int floor = level == SettlementStore.Town ? 16 : (level == SettlementStore.Village ? 7 : 3);
        int houses = Math.Clamp(2 + (people / PeoplePerHouse), floor, MaxHouses);

        // Радиус подбирается так, чтобы участков под дома в круге вышло примерно столько,
        // сколько нужно домов: пересчитывать и сортировать участки в кадре не надо.
        float radiusPlots = MathF.Sqrt(houses / HouseDensity) + 0.6f;

        return new CityPlan(index, settlements.X[index], settlements.Y[index], level, era, houses, radiusPlots);
    }

    /// <summary>Кварталы: дома по участкам сверху вниз, в центре главное здание с флагом.</summary>
    private int DrawQuarters(
        SpriteBatch batch,
        Texture2D pixel,
        WorldMap map,
        in CityPlan plan,
        byte colorIndex,
        float seconds,
        bool detail)
    {
        Texture2D texture = _atlas.Texture;
        int ring = (int)MathF.Ceiling(plan.RadiusPlots);
        float limit = plan.RadiusPlots * plan.RadiusPlots;
        bool lights = detail && plan.Decor == SettlementDecor.Lights;
        bool smoke = plan.Decor == SettlementDecor.Smoke || plan.Decor == SettlementDecor.Lights;
        int buildings = 0;

        // Обход сверху вниз: нижние дома закрывают верхние, и квартал выглядит объёмным.
        for (int gy = -ring; gy <= ring; gy++)
        {
            for (int gx = -ring; gx <= ring; gx++)
            {
                if (((gx * gx) + (gy * gy)) > limit)
                {
                    continue;
                }

                if (gx == 0 && gy == 0)
                {
                    DrawCenter(batch, texture, pixel, in plan, colorIndex);
                    buildings++;
                    continue;
                }

                // Улицы: по ним ходят люди и въезжает транспорт, застройка их не занимает.
                if (Mod(gx, Street) == 0 || Mod(gy, Street) == 0)
                {
                    continue;
                }

                uint hash = Hash(gx, gy, plan.Salt);

                // Каждый девятый участок пустой: двор, огород, пустырь. Город перестаёт быть сеткой.
                if (hash % 9u == 0u)
                {
                    continue;
                }

                float px = plan.CenterX + (gx * Plot) + (((hash >> 3) & 3u) * 0.06f) - 0.09f;
                float py = plan.CenterY + (gy * Plot) + (((hash >> 7) & 3u) * 0.06f) - 0.09f;

                int tileX = (int)MathF.Floor(px);
                int tileY = (int)MathF.Floor(py);
                if (!map.InBounds(tileX, tileY) || !map.IsLand(map.Index(tileX, tileY)))
                {
                    continue;
                }

                float tiles = 0.9f + (((hash >> 11) & 3u) * 0.12f) + (plan.Era >= 7 ? 0.3f : 0f);
                DecorKind kind = HouseKind(plan.Era, hash);
                Rectangle source = _atlas.Source(kind, (int)((hash >> 15) % 3u));
                float scale = tiles / DecorAtlas.SpriteSize;

                batch.Draw(
                    pixel,
                    new Vector2(px - (tiles * 0.4f), py - (tiles * 0.12f)),
                    null,
                    ShadowColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(tiles * 0.8f, tiles * 0.18f),
                    SpriteEffects.None,
                    0f);

                batch.Draw(
                    texture,
                    new Vector2(px - (tiles * 0.5f), py - tiles),
                    source,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);

                buildings++;

                if (lights)
                {
                    DrawWindow(batch, pixel, px, py, tiles, hash, seconds);
                }

                if (smoke && hash % 11u == 0u)
                {
                    DrawSmoke(batch, pixel, px, py - tiles, hash, seconds);
                }
            }
        }

        return buildings;
    }

    /// <summary>Главное здание: ратуша, замок или большой шалаш, смотря по эпохе и уровню.</summary>
    private void DrawCenter(
        SpriteBatch batch,
        Texture2D texture,
        Texture2D pixel,
        in CityPlan plan,
        byte colorIndex)
    {
        DecorKind kind = DecorArt.BuildingFor(plan.Era, plan.Level);
        Rectangle source = _atlas.Source(kind, (plan.Index + plan.Era) % DecorArt.Variants);

        float tiles = plan.Level == SettlementStore.Town
            ? 3.4f
            : (plan.Level == SettlementStore.Village ? 2.6f : 1.8f);
        float scale = tiles / DecorAtlas.SpriteSize;
        float baseY = plan.CenterY + 0.6f;
        float top = baseY - tiles;

        batch.Draw(
            pixel,
            new Vector2(plan.CenterX - (tiles * 0.42f), baseY - (tiles * 0.14f)),
            null,
            ShadowColor,
            0f,
            Vector2.Zero,
            new Vector2(tiles * 0.84f, tiles * 0.2f),
            SpriteEffects.None,
            0f);

        batch.Draw(
            texture,
            new Vector2(plan.CenterX - (tiles * 0.5f), top),
            source,
            Color.White,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);

        DrawFlag(batch, pixel, colorIndex, plan.CenterX, top, tiles);
    }

    /// <summary>Вид дома в квартале: с эпохой шалаши сменяются домами, камнем и высотками.</summary>
    private static DecorKind HouseKind(int era, uint hash)
    {
        if (era >= 7)
        {
            return hash % 7u == 0u ? DecorKind.Tower : DecorKind.StoneHouse;
        }

        if (era >= 5 && hash % 17u == 0u)
        {
            return DecorKind.Tower;
        }

        if (era >= 3 && hash % 5u == 0u)
        {
            return DecorKind.StoneHouse;
        }

        return era >= 4 ? DecorKind.House : DecorKind.Hut;
    }

    /// <summary>Двор и улицы под городом. Вода не мостится: город обходит берег.</summary>
    private static int DrawGround(SpriteBatch batch, Texture2D pixel, WorldMap map, in CityPlan plan, bool cobble)
    {
        float radius = plan.RadiusTiles + 0.5f;
        float limit = radius * radius;
        int span = (int)MathF.Ceiling(radius);
        int centerTileX = plan.TileX;
        int centerTileY = plan.TileY;
        int cells = 0;

        for (int ty = centerTileY - span; ty <= centerTileY + span; ty++)
        {
            for (int tx = centerTileX - span; tx <= centerTileX + span; tx++)
            {
                float dx = tx + 0.5f - plan.CenterX;
                float dy = ty + 0.5f - plan.CenterY;
                if (((dx * dx) + (dy * dy)) > limit)
                {
                    continue;
                }

                if (!map.InBounds(tx, ty) || !map.IsLand(map.Index(tx, ty)))
                {
                    continue;
                }

                bool street = OnStreet(dx, dy);

                if (cobble)
                {
                    cells += DrawTilePaving(batch, pixel, tx, ty, plan.Salt, plan.Surface, street);
                    continue;
                }

                Color color = GroundColor(plan.Surface, Hash(tx, ty, plan.Salt), street);
                batch.Draw(
                    pixel,
                    new Vector2(tx, ty),
                    null,
                    color,
                    0f,
                    Vector2.Zero,
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
                cells++;
            }
        }

        return cells;
    }

    /// <summary>Клетка мостовой: шестнадцать камней с разным тоном и редкими выбоинами.</summary>
    private static int DrawTilePaving(
        SpriteBatch batch,
        Texture2D pixel,
        int tileX,
        int tileY,
        int salt,
        Surface surface,
        bool street)
    {
        int drawn = 0;

        for (int cy = 0; cy < 4; cy++)
        {
            for (int cx = 0; cx < 4; cx++)
            {
                uint hash = Hash((tileX * 4) + cx, (tileY * 4) + cy, salt);

                // Выбоина: сквозь камень прорастает земля, край двора перестаёт быть линейкой.
                if (hash % 14u == 0u)
                {
                    continue;
                }

                batch.Draw(
                    pixel,
                    new Vector2(tileX + (cx * Cell), tileY + (cy * Cell)),
                    null,
                    GroundColor(surface, hash, street),
                    0f,
                    Vector2.Zero,
                    new Vector2(Cell, Cell),
                    SpriteEffects.None,
                    0f);
                drawn++;
            }
        }

        return drawn;
    }

    /// <summary>Тон покрытия. На улице светлее: видно, где проезд, а где двор.</summary>
    private static Color GroundColor(Surface surface, uint hash, bool street)
    {
        uint tone = hash % 7u;

        switch (surface)
        {
            case Surface.Asphalt:
                if (street)
                {
                    return tone == 0u ? AsphaltColor : AsphaltLight;
                }

                return tone == 0u ? AsphaltLight : (tone <= 2u ? AsphaltDark : AsphaltColor);
            case Surface.Stone:
                if (street)
                {
                    return tone <= 2u ? StoneLight : StoneColor;
                }

                return tone == 0u ? StoneDark : (tone <= 2u ? StoneLight : StoneColor);
            default:
                if (street)
                {
                    return tone == 0u ? DirtColor : DirtLight;
                }

                return tone % 5u == 0u ? DirtDark : DirtColor;
        }
    }

    /// <summary>Попадает ли точка на линию улицы.</summary>
    private static bool OnStreet(float dx, float dy)
    {
        int nx = (int)MathF.Round(dx / Plot);
        int ny = (int)MathF.Round(dy / Plot);
        return Mod(nx, Street) == 0 || Mod(ny, Street) == 0;
    }

    /// <summary>Поля за стеной: грядки полосами и сарай у большого города.</summary>
    private int DrawFields(SpriteBatch batch, Texture2D pixel, WorldMap map, in CityPlan plan, bool detail)
    {
        if (plan.Level == SettlementStore.Camp || plan.Era < 1)
        {
            return 0;
        }

        int count = plan.Level == SettlementStore.Town ? 6 : 3;
        float distance = plan.WallRadius + 2.2f;
        int drawn = 0;

        for (int k = 0; k < count; k++)
        {
            uint hash = Hash(k, plan.Index, 30011);
            float angle = (((k + 0.5f) / count) * MathHelper.TwoPi) + (((hash % 100u) / 100f) - 0.5f);
            float fx = plan.CenterX + (MathF.Cos(angle) * distance);
            float fy = plan.CenterY + (MathF.Sin(angle) * distance);

            int tileX = (int)MathF.Floor(fx) - 1;
            int tileY = (int)MathF.Floor(fy) - 1;
            if (!Farmable(map, tileX, tileY))
            {
                continue;
            }

            // Земля поля: два на два тайла вспаханной почвы.
            batch.Draw(
                pixel,
                new Vector2(tileX, tileY),
                null,
                FieldSoil,
                0f,
                Vector2.Zero,
                new Vector2(2f, 2f),
                SpriteEffects.None,
                0f);

            if (detail)
            {
                // Грядки: полосы вдоль поля, каждая вторая светлее — видно, что посеяно.
                for (int row = 0; row < 8; row++)
                {
                    Color color = (row & 1) == 0 ? FieldCrop : FieldFurrow;
                    batch.Draw(
                        pixel,
                        new Vector2(tileX + 0.1f, tileY + 0.15f + (row * 0.24f)),
                        null,
                        color,
                        0f,
                        Vector2.Zero,
                        new Vector2(1.8f, 0.12f),
                        SpriteEffects.None,
                        0f);
                }
            }

            if (plan.Level == SettlementStore.Town && hash % 3u == 0u)
            {
                Rectangle source = _atlas.Source(DecorKind.Farm, (int)(hash % DecorArt.Variants));
                float tiles = 1.3f;
                float scale = tiles / DecorAtlas.SpriteSize;
                batch.Draw(
                    _atlas.Texture,
                    new Vector2(tileX + 1.9f - tiles, tileY + 2f - tiles),
                    source,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);
            }

            drawn++;
        }

        return drawn;
    }

    /// <summary>Поле ставится только на сплошную сушу: посевы посреди моря не нужны.</summary>
    private static bool Farmable(WorldMap map, int tileX, int tileY)
    {
        for (int y = tileY; y <= tileY + 1; y++)
        {
            for (int x = tileX; x <= tileX + 1; x++)
            {
                if (!map.InBounds(x, y) || !map.IsLand(map.Index(x, y)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Стена по кромке города: короткие звенья по кругу, четыре разрыва под ворота,
    /// у замковой эпохи по углам башни. Звено над водой пропускается: стена идёт по берегу.
    /// </summary>
    private int DrawWalls(SpriteBatch batch, Primitives primitives, WorldMap map, in CityPlan plan)
    {
        if (plan.Level == SettlementStore.Camp)
        {
            return 0;
        }

        const int Segments = 36;
        float step = MathHelper.TwoPi / Segments;
        float thickness = 0.3f;
        int towers = 0;

        for (int s = 0; s < Segments; s++)
        {
            float a1 = s * step;
            float a2 = (s + 1) * step;
            float middle = (a1 + a2) * 0.5f;

            // Ворота: у каждой из четырёх сторон разрыв, в него входит дорога.
            float turn = middle % MathHelper.PiOver2;
            if (turn < 0.12f || turn > MathHelper.PiOver2 - 0.12f)
            {
                continue;
            }

            float mx = plan.CenterX + (MathF.Cos(middle) * plan.WallRadius);
            float my = plan.CenterY + (MathF.Sin(middle) * plan.WallRadius);
            int tileX = (int)MathF.Floor(mx);
            int tileY = (int)MathF.Floor(my);
            if (!map.InBounds(tileX, tileY) || !map.IsLand(map.Index(tileX, tileY)))
            {
                continue;
            }

            var from = new Vector2(
                plan.CenterX + (MathF.Cos(a1) * plan.WallRadius),
                plan.CenterY + (MathF.Sin(a1) * plan.WallRadius));
            var to = new Vector2(
                plan.CenterX + (MathF.Cos(a2) * plan.WallRadius),
                plan.CenterY + (MathF.Sin(a2) * plan.WallRadius));

            primitives.Line(batch, from, to, EraStyle.Wall, thickness);
        }

        if (plan.Decor != SettlementDecor.Castle)
        {
            return 0;
        }

        Rectangle source = _atlas.Source(DecorKind.Tower, plan.Index % DecorArt.Variants);
        float tiles = 1.7f;
        float scale = tiles / DecorAtlas.SpriteSize;

        for (int corner = 0; corner < 4; corner++)
        {
            float angle = MathHelper.PiOver4 + (corner * MathHelper.PiOver2);
            float tx = plan.CenterX + (MathF.Cos(angle) * plan.WallRadius);
            float ty = plan.CenterY + (MathF.Sin(angle) * plan.WallRadius);
            int tileX = (int)MathF.Floor(tx);
            int tileY = (int)MathF.Floor(ty);
            if (!map.InBounds(tileX, tileY) || !map.IsLand(map.Index(tileX, tileY)))
            {
                continue;
            }

            batch.Draw(
                _atlas.Texture,
                new Vector2(tx - (tiles * 0.5f), ty - tiles),
                source,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f);
            towers++;
        }

        return towers;
    }

    /// <summary>Огонь в окне. Часть окон гаснет и загорается, чтобы город не выглядел мёртвым.</summary>
    private static void DrawWindow(
        SpriteBatch batch,
        Texture2D pixel,
        float px,
        float py,
        float tiles,
        uint hash,
        float seconds)
    {
        uint phase = (uint)(seconds * 0.4f) + (hash >> 19);
        if (phase % 7u == 0u)
        {
            return;
        }

        float size = tiles * 0.15f;
        batch.Draw(
            pixel,
            new Vector2(px - (tiles * 0.2f), py - (tiles * 0.5f)),
            null,
            EraStyle.Light,
            0f,
            Vector2.Zero,
            new Vector2(size, size),
            SpriteEffects.None,
            0f);
    }

    /// <summary>Дым из трубы: три клуба поднимаются и тают, положение считается от времени.</summary>
    private static void DrawSmoke(
        SpriteBatch batch,
        Texture2D pixel,
        float px,
        float roofY,
        uint hash,
        float seconds)
    {
        float offset = (hash % 100u) / 100f;

        for (int puff = 0; puff < 3; puff++)
        {
            float t = ((seconds * 0.22f) + offset + (puff * 0.33f)) % 1f;
            float size = 0.16f + (t * 0.3f);
            float y = roofY - 0.1f - (t * 1.8f);
            float x = px + (MathF.Sin((t * 4f) + offset) * 0.18f);
            Color color = EraStyle.Smoke * (1f - t) * 0.8f;

            batch.Draw(
                pixel,
                new Vector2(x - (size * 0.5f), y - size),
                null,
                color,
                0f,
                Vector2.Zero,
                new Vector2(size, size),
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>Флажок над главным зданием: по нему видно, чей это город.</summary>
    private static void DrawFlag(
        SpriteBatch batch,
        Texture2D pixel,
        byte colorIndex,
        float centerX,
        float roofY,
        float tiles)
    {
        float pole = tiles * 0.42f;
        float thickness = MathF.Max(0.06f, tiles * 0.06f);
        float flagWidth = tiles * 0.32f;
        float flagHeight = tiles * 0.2f;
        float poleX = centerX + (tiles * 0.24f);
        float poleTop = roofY - pole;

        batch.Draw(
            pixel,
            new Vector2(poleX, poleTop),
            null,
            PoleColor,
            0f,
            Vector2.Zero,
            new Vector2(thickness, pole + (tiles * 0.1f)),
            SpriteEffects.None,
            0f);

        batch.Draw(
            pixel,
            new Vector2(poleX + thickness, poleTop),
            null,
            TribePalette.Of(colorIndex),
            0f,
            Vector2.Zero,
            new Vector2(flagWidth, flagHeight),
            SpriteEffects.None,
            0f);
    }

    private static int Mod(int value, int period)
    {
        int rest = value % period;
        return rest < 0 ? rest + period : rest;
    }

    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    /// <summary>Раскладка одного города. Считается заново каждый кадр: это десяток действий.</summary>
    private readonly struct CityPlan
    {
        public CityPlan(int index, int tileX, int tileY, byte level, int era, int houses, float radiusPlots)
        {
            Index = index;
            TileX = tileX;
            TileY = tileY;
            Level = level;
            Era = era;
            Houses = houses;
            RadiusPlots = radiusPlots;
            CenterX = tileX + 0.5f;
            CenterY = tileY + 0.5f;
            RadiusTiles = radiusPlots * Plot;
            WallRadius = RadiusTiles + 0.7f;
            Decor = EraStyle.DecorOf(era);
            Salt = ((index + 1) * 7919) + 13;
            Surface = era >= 7
                ? Surface.Asphalt
                : (level > SettlementStore.Camp && era >= 2 ? Surface.Stone : Surface.Dirt);
        }

        public int Index { get; }

        public int TileX { get; }

        public int TileY { get; }

        public byte Level { get; }

        public int Era { get; }

        public int Houses { get; }

        public float RadiusPlots { get; }

        public float RadiusTiles { get; }

        public float WallRadius { get; }

        public float CenterX { get; }

        public float CenterY { get; }

        public SettlementDecor Decor { get; }

        public Surface Surface { get; }

        public int Salt { get; }
    }
}
