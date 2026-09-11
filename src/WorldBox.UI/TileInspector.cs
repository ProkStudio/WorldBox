using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Панель слева внизу: что за тайл под курсором и какой режим карты включён.
/// Именно отсюда игрок узнаёт, почему в этом месте выросла пустыня, а не лес.
/// </summary>
public sealed class TileInspector
{
    private static readonly Color PanelColor = new Color(10, 12, 16, 200);
    private static readonly Color BorderColor = new Color(255, 255, 255, 45);
    private static readonly Color TextColor = new Color(226, 226, 226);
    private static readonly Color AccentColor = new Color(94, 159, 232);

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
        int viewportHeight)
    {
        if (!Visible)
        {
            return;
        }

        int scale = Math.Max(1, Scale);
        int step = font.LineHeight * scale;
        var panel = new Rectangle(12, viewportHeight - (step * 6) - (14 * scale) - 12, 470, (step * 6) + (10 * scale));
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

        DrawFooter(batch, font, map, generationMs, x, panel.Bottom - step - (4 * scale), scale);
    }

    private void DrawFooter(SpriteBatch batch, PixelFont font, WorldMap map, double generationMs, int x, int y, int scale)
    {
        _line.Clear()
            .Append(Strings.Get("panel.seed")).Append(' ').Append(map.Seed)
            .Append("   ").Append(Strings.Get("panel.land")).Append(' ')
            .Append(Percent(map.LandTiles / (float)map.TileCount)).Append("%   ")
            .Append(Strings.Get("panel.generated")).Append(' ').Append(generationMs / 1000.0, 2).Append(" с");
        font.Draw(batch, _line.Span, new Vector2(x, y), new Color(150, 150, 158), scale);
    }

    private static int Percent(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 100f);

    /// <summary>Грубой перевод 0..1 в градусы: 0 это -25, 1 это +35.</summary>
    private static int Celsius(float value) => (int)MathF.Round(-25f + (Math.Clamp(value, 0f, 1f) * 60f));
}
