using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Art;
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
    private const int IconSpace = 22;

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
        SettlementStore? settlements = null,
        UiSkin? skin = null)
    {
        if (!Visible)
        {
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;
        var panel = new Rectangle(
            14,
            viewportHeight - (step * PanelLines) - (14 * scale) - 12,
            PanelWidth,
            (step * PanelLines) + (11 * scale));

        UiChrome.Panel(batch, primitives, skin, panel);

        int iconX = panel.X + (5 * scale);
        int x = iconX + (skin != null ? IconSpace : 0);
        int y = panel.Y + (5 * scale);

        _line.Clear()
            .Append(Strings.Get("panel.map_mode")).Append(": ").Append(Strings.Get(MapModes.NameKey(mode)));
        DrawLine(batch, font, skin, IconKind.Legend, iconX, x, y, step, UiPalette.Accent, scale);
        y += step;

        if (!inside)
        {
            font.Draw(batch, Strings.Get("panel.no_tile"), new Vector2(x, y), UiPalette.Text, scale);
            DrawFooter(batch, font, skin, map, generationMs, iconX, x, panel.Bottom - step - (4 * scale), step, scale);
            return;
        }

        int index = map.Index(tileX, tileY);
        var biome = (Biome)map.BiomeAt[index];
        var resource = (ResourceKind)map.ResourceAt[index];

        _line.Clear()
            .Append(Strings.Get(Biomes.NameKey(biome)))
            .Append("   ").Append(tileX).Append(", ").Append(tileY);
        DrawLine(batch, font, skin, IconKind.Inspect, iconX, x, y, step, UiPalette.Text, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("panel.height")).Append(' ').Append(Percent(map.Elevation[index])).Append("%   ")
            .Append(Strings.Get("panel.temperature")).Append(' ').Append(Celsius(map.Temperature[index])).Append("°");
        DrawLine(batch, font, skin, IconKind.MapHeight, iconX, x, y, step, UiPalette.Text, scale);
        y += step;

        _line.Clear()
            .Append(Strings.Get("panel.moisture")).Append(' ').Append(Percent(map.Moisture[index])).Append("%   ")
            .Append(Strings.Get("panel.fertility")).Append(' ').Append(Percent(map.Fertility[index])).Append('%');
        DrawLine(batch, font, skin, IconKind.MapMoisture, iconX, x, y, step, UiPalette.Text, scale);
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

        DrawLine(
            batch,
            font,
            skin,
            IconKind.MapResources,
            iconX,
            x,
            y,
            step,
            resource == ResourceKind.None ? UiPalette.TextMuted : UiPalette.Text,
            scale);
        y += step;

        y = DrawOwner(batch, font, skin, territory, tribes, tech, table, index, iconX, x, y, step, scale);
        DrawSettlement(batch, font, skin, settlements, tribes, tileX, tileY, iconX, x, y, step, scale);

        DrawFooter(batch, font, skin, map, generationMs, iconX, x, panel.Bottom - step - (4 * scale), step, scale);
    }

    /// <summary>Чья это земля и что у хозяев с развитием. Возвращает новую высоту курсора рисования.</summary>
    private int DrawOwner(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        Territory? territory,
        TribeStore? tribes,
        TribeTech? tech,
        EraTable? table,
        int index,
        int iconX,
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
            _line.Clear().Append(Strings.Get("panel.no_owner"));
            DrawLine(batch, font, skin, IconKind.Borders, iconX, x, y, step, UiPalette.TextMuted, scale);
            return y + step;
        }

        Color color = TribePalette.Of(tribes.ColorIndex[owner]);
        _line.Clear()
            .Append(Strings.Get("panel.owner")).Append(' ').Append(tribes.Name[owner] ?? string.Empty);
        DrawLine(batch, font, skin, IconKind.Borders, iconX, x, y, step, color, scale);
        y += step;

        if (table == null)
        {
            _line.Clear().Append(Strings.Get("panel.no_eras"));
            DrawLine(batch, font, skin, IconKind.Era, iconX, x, y, step, UiPalette.TextMuted, scale);
            return y + step;
        }

        int era = Math.Clamp(tribes.Era[owner], 0, table.Last);
        bool tracked = tech != null && (uint)owner < (uint)tech.Capacity;

        _line.Clear()
            .Append(Strings.Get("panel.era")).Append(' ').Append(Strings.Get(table.NameKeyOf(era))).Append("   ");

        Color stateColor = UiPalette.Text;
        IconKind icon = IconKind.Era;
        EraBlock block = tracked ? tech!.BlockOf(owner) : EraBlock.Ready;
        if (era + 1 >= table.Count)
        {
            block = EraBlock.Top;
        }

        switch (block)
        {
            case EraBlock.Top:
                _line.Append(Strings.Get("panel.era_top"));
                stateColor = UiPalette.Good;
                icon = IconKind.Star;
                break;

            case EraBlock.DarkAge:
                _line.Append(Strings.Get("panel.dark_age"));
                stateColor = UiPalette.Bad;
                icon = IconKind.Warning;
                break;

            case EraBlock.People:
                _line.Append(Strings.Get("panel.need_people")).Append(' ').AppendGrouped(table.MinPop[era + 1]);
                stateColor = UiPalette.Accent;
                icon = IconKind.People;
                break;

            case EraBlock.Resource:
                ResourceKind missing = EraRules.FirstMissing(tracked ? tech!.MissingResources[owner] : 0);
                _line.Append(Strings.Get("panel.need_resource")).Append(' ').Append(Strings.Get(ResourceKinds.NameKey(missing)));
                stateColor = UiPalette.Accent;
                icon = IconKind.MapResources;
                break;

            case EraBlock.Geography:
                GeoFeature feature = EraRules.FirstMissingGeo(tracked ? tech!.MissingGeo[owner] : (byte)0);
                _line.Append(Strings.Get("panel.need_geo")).Append(' ').Append(Strings.Get(EraRules.GeoNameKey(feature)));
                stateColor = UiPalette.Accent;
                icon = IconKind.MapTerrain;
                break;

            default:
                float share = tracked ? tech!.ProgressShare(table, era, owner) : 0f;
                _line.Append(Strings.Get("panel.progress")).Append(' ').Append((int)MathF.Round(share * 100f)).Append('%');
                break;
        }

        DrawLine(batch, font, skin, icon, iconX, x, y, step, stateColor, scale);
        return y + step;
    }

    /// <summary>Ближайшее поселение в нескольких тайлах от курсора.</summary>
    private void DrawSettlement(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        SettlementStore? settlements,
        TribeStore? tribes,
        int tileX,
        int tileY,
        int iconX,
        int x,
        int y,
        int step,
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
            : UiPalette.Text;

        DrawLine(batch, font, skin, IconKind.Settlement, iconX, x, y, step, color, scale);
    }

    private static string LevelKey(byte level)
    {
        if (level == SettlementStore.Town)
        {
            return "settlement.town";
        }

        return level == SettlementStore.Village ? "settlement.village" : "settlement.camp";
    }

    private void DrawFooter(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        WorldMap map,
        double generationMs,
        int iconX,
        int x,
        int y,
        int step,
        int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.seed")).Append(' ').Append(map.Seed)
            .Append("   ").Append(Strings.Get("panel.land")).Append(' ')
            .Append(Percent(map.LandTiles / (float)map.TileCount)).Append("%   ")
            .Append(Strings.Get("panel.generated")).Append(' ').Append(generationMs / 1000.0, 2).Append(" с");
        DrawLine(batch, font, skin, IconKind.Gear, iconX, x, y, step, UiPalette.TextMuted, scale);
    }

    /// <summary>Рисует собранную строку и её значок. Без скина значка просто не будет.</summary>
    private void DrawLine(
        SpriteBatch batch,
        PixelFont font,
        UiSkin? skin,
        IconKind icon,
        int iconX,
        int textX,
        int y,
        int step,
        Color color,
        int scale)
    {
        if (skin != null)
        {
            skin.Icon(batch, icon, iconX, y + ((step - IconAtlas.IconSize) / 2), 1);
        }

        font.Draw(batch, _line.Span, new Vector2(textX, y), color, scale);
    }

    private static int Percent(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 100f);

    /// <summary>Грубой перевод 0..1 в градусы: 0 это -25, 1 это +35.</summary>
    private static int Celsius(float value) => (int)MathF.Round(-25f + (Math.Clamp(value, 0f, 1f) * 60f));
}
