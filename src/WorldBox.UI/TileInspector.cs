using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Eras;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Панель слева внизу: что за тайл под курсором и какой режим карты включён.
/// Именно отсюда игрок узнаёт, почему в этом месте выросла пустыня, чья это земля
/// и что мешает её хозяевам шагнуть в следующую эпоху.
/// </summary>
public sealed class TileInspector
{
    /// <summary>На сколько тайлов вокруг курсора ищем поселение.</summary>
    public const int SettlementRadius = 3;

    private const int PanelWidth = 560;
    private const int PanelLines = 9;

    private static readonly Color PanelColor = new Color(10, 12, 16, 200);
    private static readonly Color BorderColor = new Color(255, 255, 255, 45);
    private static readonly Color TextColor = new Color(226, 226, 226);
    private static readonly Color AccentColor = new Color(94, 159, 232);
    private static readonly Color DimColor = new Color(150, 150, 158);
    private static readonly Color BlockedColor = new Color(226, 168, 120);

    private readonly TextBuilder _line = new TextBuilder(192);

    public bool Visible { get; set; } = true;

    public int Scale { get; set; } = 2;

    public void Draw(
        SpriteBatch batch,
        PixelFont font,
        Primitives primitives,
        WorldMap map,
        MapMode mode,
        int tileX,
        int tileY,
        bool inside,
        double generationMs,
        int viewportHeight,
        Territory? territory = null,
        TribeStore? tribes = null,
        TribeTech? tech = null,
        EraTable? table = null,
        SettlementStore? settlements = null)
    {
        if (!Visible)
        {
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;
        var panel = new Rectangle(
            12,
            viewportHeight - (step * PanelLines) - (14 * scale) - 12,
            PanelWidth,
            (step * PanelLines) + (10 * scale));
        primitives.FillRect(batch, panel, PanelColor);
        primitives.FrameRect(batch, panel, BorderColor);

        int x = panel.X + (6 * scale);
        int y = panel.Y + (5 * scale);

        _line.Clear()
            .Append(Strings.Get("panel.map_mode")).Append(": ").Append(Strings.Get(MapModes.NameKey(mode)));
        font.Draw(batch, _line.Span, new Vector2(x, y), AccentColor, scale);
        y += step;

        if (!inside)
        {
            font.Draw(batch, Strings.Get("panel.no_tile"), new Vector2(x, y), TextColor, scale);
            DrawFooter(batch, font, map, generationMs, x, panel.Bottom - step - (4 * scale), scale);
            return;
        }

        int index = map.Index(tileX, tileY);
        var biome = (Biome)map.BiomeAt[index];
        var resource = (ResourceKind)map.ResourceAt[index];

        _line.Clear()
            .Append(Strings.Get(Biomes.NameKey(biome)))
            .Append("   ").Append(tileX).Append(", ").Append(tileY);
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("panel.height")).Append(' ').Append(Percent(map.Elevation[index])).Append("%   ")
            .Append(Strings.Get("panel.temperature")).Append(' ').Append(Celsius(map.Temperature[index])).Append("°");
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("panel.moisture")).Append(' ').Append(Percent(map.Moisture[index])).Append("%   ")
            .Append(Strings.Get("panel.fertility")).Append(' ').Append(Percent(map.Fertility[index])).Append('%');
        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        _line.Clear().Append(Strings.Get("panel.resource")).Append(' ');
        if (resource == ResourceKind.None)
        {
            _line.Append(Strings.Get("panel.nothing"));
        }
        else
        {
            _line.Append(Strings.Get(ResourceKinds.NameKey(resource)));
        }

        font.Draw(batch, _line.Span, new Vector2(x, y), TextColor, scale);
        y += step;

        y = DrawOwner(batch, font, territory, tribes, tech, table, index, x, y, step, scale);
        DrawSettlement(batch, font, settlements, tribes, tileX, tileY, x, y, scale);

        DrawFooter(batch, font, map, generationMs, x, panel.Bottom - step - (4 * scale), scale);
    }

    /// <summary>Чья это земля и что у хозяев с развитием. Возвращает новую высоту курсора рисования.</summary>
    private int DrawOwner(
        SpriteBatch batch,
        PixelFont font,
        Territory? territory,
        TribeStore? tribes,
        TribeTech? tech,
        EraTable? table,
        int index,
        int x,
        int y,
        int step,
        int scale)
    {
        if (territory == null || tribes == null)
        {
            return y;
        }

        short owner = territory.Owner[index];
        if (owner == TribeStore.None || !tribes.IsAlive(owner))
        {
            font.Draw(batch, Strings.Get("panel.no_owner"), new Vector2(x, y), DimColor, scale);
            return y + step;
        }

        Color color = TribePalette.Of(tribes.ColorIndex[owner]);
        _line.Clear()
            .Append(Strings.Get("panel.owner")).Append(' ').Append(tribes.Name[owner] ?? string.Empty);
        font.Draw(batch, _line.Span, new Vector2(x, y), color, scale);
        y += step;

        if (table == null)
        {
            font.Draw(batch, Strings.Get("panel.no_eras"), new Vector2(x, y), DimColor, scale);
            return y + step;
        }

        int era = Math.Clamp(tribes.Era[owner], 0, table.Last);
        bool tracked = tech != null && (uint)owner < (uint)tech.Capacity;

        _line.Clear()
            .Append(Strings.Get("panel.era")).Append(' ').Append(Strings.Get(table.NameKeyOf(era))).Append("   ");

        Color stateColor = TextColor;
        EraBlock block = tracked ? tech!.BlockOf(owner) : EraBlock.Ready;
        if (era + 1 >= table.Count)
        {
            block = EraBlock.Top;
        }

        switch (block)
        {
            case EraBlock.Top:
                _line.Append(Strings.Get("panel.era_top"));
                break;

            case EraBlock.DarkAge:
                _line.Append(Strings.Get("panel.dark_age"));
                stateColor = BlockedColor;
                break;

            case EraBlock.People:
                _line.Append(Strings.Get("panel.need_people")).Append(' ').AppendGrouped(table.MinPop[era + 1]);
                stateColor = BlockedColor;
                break;

            case EraBlock.Resource:
                ResourceKind missing = EraRules.FirstMissing(tracked ? tech!.MissingResources[owner] : 0);
                _line.Append(Strings.Get("panel.need_resource")).Append(' ').Append(Strings.Get(ResourceKinds.NameKey(missing)));
                stateColor = BlockedColor;
                break;

            case EraBlock.Geography:
                GeoFeature feature = EraRules.FirstMissingGeo(tracked ? tech!.MissingGeo[owner] : (byte)0);
                _line.Append(Strings.Get("panel.need_geo")).Append(' ').Append(Strings.Get(EraRules.GeoNameKey(feature)));
                stateColor = BlockedColor;
                break;

            default:
                float share = tracked ? tech!.ProgressShare(table, era, owner) : 0f;
                _line.Append(Strings.Get("panel.progress")).Append(' ').Append((int)MathF.Round(share * 100f)).Append('%');
                break;
        }

        font.Draw(batch, _line.Span, new Vector2(x, y), stateColor, scale);
        return y + step;
    }

    /// <summary>Ближайшее поселение в нескольких тайлах от курсора.</summary>
    private void DrawSettlement(
        SpriteBatch batch,
        PixelFont font,
        SettlementStore? settlements,
        TribeStore? tribes,
        int tileX,
        int tileY,
        int x,
        int y,
        int scale)
    {
        if (settlements == null)
        {
            return;
        }

        int nearest = -1;
        int best = int.MaxValue;
        int high = settlements.HighWater;

        for (int i = 0; i < high; i++)
        {
            if (!settlements.Alive[i])
            {
                continue;
            }

            int dx = Math.Abs(settlements.X[i] - tileX);
            int dy = Math.Abs(settlements.Y[i] - tileY);
            int distance = Math.Max(dx, dy);
            if (distance > SettlementRadius || distance >= best)
            {
                continue;
            }

            best = distance;
            nearest = i;
        }

        if (nearest < 0)
        {
            return;
        }

        _line.Clear()
            .Append(Strings.Get("panel.settlement")).Append(' ')
            .Append(Strings.Get(LevelKey(settlements.Level[nearest]))).Append(' ')
            .Append(settlements.Name[nearest] ?? string.Empty)
            .Append("   ").Append(Strings.Get("panel.people")).Append(' ').Append(settlements.People[nearest]);

        short tribe = settlements.Tribe[nearest];
        Color color = tribes != null && tribes.IsAlive(tribe)
            ? TribePalette.Of(tribes.ColorIndex[tribe])
            : TextColor;

        font.Draw(batch, _line.Span, new Vector2(x, y), color, scale);
    }

    private static string LevelKey(byte level)
    {
        if (level == SettlementStore.Town)
        {
            return "settlement.town";
        }

        return level == SettlementStore.Village ? "settlement.village" : "settlement.camp";
    }

    private void DrawFooter(SpriteBatch batch, PixelFont font, WorldMap map, double generationMs, int x, int y, int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.seed")).Append(' ').Append(map.Seed)
            .Append("   ").Append(Strings.Get("panel.land")).Append(' ')
            .Append(Percent(map.LandTiles / (float)map.TileCount)).Append("%   ")
            .Append(Strings.Get("panel.generated")).Append(' ').Append(generationMs / 1000.0, 2).Append(" с");
        font.Draw(batch, _line.Span, new Vector2(x, y), DimColor, scale);
    }

    private static int Percent(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 100f);

    /// <summary>Грубой перевод 0..1 в градусы: 0 это -25, 1 это +35.</summary>
    private static int Celsius(float value) => (int)MathF.Round(-25f + (Math.Clamp(value, 0f, 1f) * 60f));
}
