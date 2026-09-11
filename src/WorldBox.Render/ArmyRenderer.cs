using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;

namespace WorldBox.Render;

/// <summary>
/// Войска и осады на карте. Без этого слоя война выглядит как внезапная смена цвета
/// у города: войско невидимо, осада невидима, причина непонятна. Здесь войско — знамя цвета
/// своего народа, а осаждённый город обведён кольцом из засечек.
///
/// Важные цифры показаны формой, а не только цветом: размер знамени — число бойцов, квадратики
/// у древка — боевой дух, выкушенный угол знамени — кончился припас, число ярких засечек вокруг
/// города — насколько далеко зашла осада. Цветослепому игроку слой читается так же.
///
/// Знамёна держат постоянный размер в пикселях экрана: издали войско не превращается в точку,
/// вблизи не закрывает полгорода. За кадр ноль аллокаций, за экраном ничего не рисуется.
/// </summary>
public sealed class ArmyRenderer
{
    /// <summary>Ниже этого зума знамёна слипаются с городами и не рисуются.</summary>
    public const float MinZoom = 1.2f;

    /// <summary>С этого зума видна нитка к цели похода: раньше она только мусорит карту.</summary>
    public const float TargetZoom = 2.4f;

    /// <summary>Столько бойцов — уже большая рать: дальше знамя не растёт.</summary>
    private const float BigArmy = 150f;

    /// <summary>Сколько засечек в кольце осады.</summary>
    private const int SiegeTicks = 12;

    /// <summary>На сколько кусков рвётся нитка похода.</summary>
    private const int TargetDashes = 9;

    /// <summary>Ниже этого припаса войско голодает.</summary>
    private const float HungrySupply = 0.35f;

    private static readonly Color Shadow = new Color(10, 11, 15);
    private static readonly Color Pole = new Color(58, 44, 32);
    private static readonly Color HighMorale = new Color(126, 214, 106);
    private static readonly Color LowMorale = new Color(238, 128, 96);
    private static readonly Color SiegeLit = new Color(232, 96, 84);
    private static readonly Color SiegeDim = new Color(96, 54, 48);
    private static readonly Color Dead = new Color(150, 150, 150);

    public bool Visible { get; set; } = true;

    /// <summary>Сколько знамён ушло на экран в последнем кадре.</summary>
    public int DrawnArmies { get; private set; }

    /// <summary>Сколько колец осады ушло на экран в последнем кадре.</summary>
    public int DrawnSieges { get; private set; }

    public void Draw(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        ArmyStore armies,
        SettlementStore settlements,
        TribeStore tribes,
        float seconds)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(primitives);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(armies);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(tribes);

        DrawnArmies = 0;
        DrawnSieges = 0;

        if (!Visible || camera.Zoom < MinZoom)
        {
            return;
        }

        // Запас по краям: кольцо осады шире города, а знамя торчит вверх на полтора десятка пикселей.
        camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, 12);

        // Один пиксель экрана в тайлах: все размеры ниже считаются в нём.
        float px = 1f / camera.Zoom;

        DrawnSieges = DrawSieges(batch, primitives, settlements, minX, minY, maxX, maxY, px, seconds);
        DrawnArmies = DrawArmies(batch, primitives, camera, armies, settlements, tribes, minX, minY, maxX, maxY, px);
    }

    /// <summary>Кольцо вокруг осаждённого города: яркие засечки считают ход осады.</summary>
    private static int DrawSieges(
        SpriteBatch batch,
        Primitives primitives,
        SettlementStore settlements,
        int minX,
        int minY,
        int maxX,
        int maxY,
        float px,
        float seconds)
    {
        int high = settlements.HighWater;
        int drawn = 0;

        // Медленное дыхание кольца: глаз ловит движение даже на краю экрана.
        float pulse = 0.72f + (0.28f * MathF.Sin(seconds * 3.4f));

        for (int i = 0; i < high; i++)
        {
            if (!settlements.Alive[i] || settlements.Siege[i] <= 0f)
            {
                continue;
            }

            int x = settlements.X[i];
            int y = settlements.Y[i];
            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                continue;
            }

            float cx = x + 0.5f;
            float cy = y + 0.5f;
            float radius = MathF.Max(settlements.Radius[i] + 1f, 10f * px);
            float length = MathF.Max(1.2f, 5f * px);
            float thickness = MathF.Max(0.06f, 2f * px);

            float share = Math.Clamp(settlements.Siege[i], 0f, 1f);
            int lit = 1 + (int)(share * (SiegeTicks - 1));

            for (int t = 0; t < SiegeTicks; t++)
            {
                float angle = t * MathF.Tau / SiegeTicks;
                float dx = MathF.Cos(angle);
                float dy = MathF.Sin(angle);
                var from = new Vector2(cx + (dx * radius), cy + (dy * radius));
                var to = new Vector2(cx + (dx * (radius + length)), cy + (dy * (radius + length)));

                bool hot = t < lit;
                Color color = hot ? SiegeLit * pulse : SiegeDim * 0.8f;
                primitives.Line(batch, from, to, Shadow * 0.55f, thickness * 2.1f);
                primitives.Line(batch, from, to, color, thickness);
            }

            drawn++;
        }

        return drawn;
    }

    /// <summary>Знамёна войск: размер — сила, квадратики — дух, нитка — куда идёт.</summary>
    private static int DrawArmies(
        SpriteBatch batch,
        Primitives primitives,
        Camera2D camera,
        ArmyStore armies,
        SettlementStore settlements,
        TribeStore tribes,
        int minX,
        int minY,
        int maxX,
        int maxY,
        float px)
    {
        int high = armies.HighWater;
        int drawn = 0;
        bool showTargets = camera.Zoom >= TargetZoom;

        for (int i = 0; i < high; i++)
        {
            if (!armies.Alive[i])
            {
                continue;
            }

            int x = armies.X[i];
            int y = armies.Y[i];
            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                continue;
            }

            short tribe = armies.Tribe[i];
            Color color = tribes.IsAlive(tribe) ? TribePalette.Of(tribes.ColorIndex[tribe]) : Dead;

            float cx = x + 0.5f;
            float cy = y + 0.5f;
            float strength = MathF.Min(1f, armies.Men[i] / BigArmy);
            float poleHeight = (11f + (5f * strength)) * px;
            float flagWidth = (6f + (6f * strength)) * px;
            float flagHeight = (4f + (3f * strength)) * px;
            float stroke = MathF.Max(0.05f, 1.6f * px);
            float top = cy - poleHeight;

            // Нитка к цели идёт первой: знамя должно лежать поверх неё.
            if (showTargets)
            {
                int target = armies.Target[i];
                if (target != ArmyStore.NoTarget
                    && (uint)target < (uint)settlements.Capacity
                    && settlements.Alive[target])
                {
                    var to = new Vector2(settlements.X[target] + 0.5f, settlements.Y[target] + 0.5f);
                    DrawDashed(batch, primitives, new Vector2(cx, cy), to, color * 0.55f, stroke * 0.8f);
                }
            }

            // Тёмная подложка под всё: знамя читается и на песке, и на тёмном лесу.
            primitives.Line(batch, new Vector2(cx, cy + (px * 2f)), new Vector2(cx, top), Shadow * 0.7f, stroke * 2.4f);
            primitives.Line(batch, new Vector2(cx, cy + (px * 2f)), new Vector2(cx, top), Pole, stroke);

            primitives.FillRect(
                batch,
                cx - px,
                top - px,
                flagWidth + (2f * px),
                flagHeight + (2f * px),
                Shadow * 0.75f);
            primitives.FillRect(batch, cx, top, flagWidth, flagHeight, color);

            // Блик вверху знамени: ткань перестаёт выглядеть плоским пятном.
            primitives.FillRect(batch, cx, top, flagWidth, MathF.Max(px, flagHeight * 0.28f), Color.White * 0.22f);

            // Кончился припас — у знамени выкушен угол. Форма, а не цвет: видно всем.
            if (armies.Supply[i] < HungrySupply)
            {
                float bite = MathF.Max(px, flagHeight * 0.5f);
                primitives.FillRect(batch, cx + flagWidth - bite, top + flagHeight - bite, bite, bite, Shadow * 0.85f);
            }

            DrawMoralePips(batch, primitives, armies.Morale[i], cx, cy, px);
            drawn++;
        }

        return drawn;
    }

    /// <summary>Боевой дух: от одного квадратика до трёх у основания древка.</summary>
    private static void DrawMoralePips(
        SpriteBatch batch,
        Primitives primitives,
        float morale,
        float cx,
        float cy,
        float px)
    {
        int pips = 1 + (int)(Math.Clamp(morale, 0f, 1f) * 2.99f);
        float size = MathF.Max(px, 2f * px);
        float step = size + px;
        float baseY = cy + (px * 2f);
        Color color = morale >= 0.5f ? HighMorale : LowMorale;

        primitives.FillRect(batch, cx - (px * 2f), baseY, (step * pips) + (px * 3f), size + (px * 2f), Shadow * 0.7f);

        for (int p = 0; p < pips; p++)
        {
            primitives.FillRect(batch, cx - px + (step * p), baseY + px, size, size, color);
        }
    }

    /// <summary>Пунктир к цели похода: рвём линию на куски и рисуем через один.</summary>
    private static void DrawDashed(
        SpriteBatch batch,
        Primitives primitives,
        Vector2 from,
        Vector2 to,
        Color color,
        float thickness)
    {
        Vector2 step = (to - from) / TargetDashes;

        for (int i = 0; i < TargetDashes; i += 2)
        {
            Vector2 a = from + (step * i);
            Vector2 b = a + step;
            primitives.Line(batch, a, b, Shadow * 0.45f, thickness * 2.1f);
            primitives.Line(batch, a, b, color, thickness);
        }
    }
}
