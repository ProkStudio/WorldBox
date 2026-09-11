using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;

namespace WorldBox.UI;

/// <summary>
/// Легенда карты в правом верхнем углу. Для ландшафта и ресурсов показывает долю
/// каждого типа, для числовых режимов — цветовую шкалу. Переключается клавишей L.
/// Пересобирается только при смене мира или режима, каждый кадр только рисуется.
/// </summary>
public sealed class BiomeLegend
{
    private const int Scale = 2;
    private const int MaxRows = 12;
    private const int PanelWidth = 330;
    private const int Padding = 10;
    private const int SwatchSize = 14;
    private const int GradientSteps = 12;
    private const int GradientHeight = 18;
    private const int Margin = 16;

    private static readonly Color PanelColor = new Color(10, 12, 16, 200);
    private static readonly Color BorderColor = new Color(255, 255, 255, 40);
    private static readonly Color TitleColor = new Color(150, 190, 240);
    private static readonly Color TextColor = new Color(228, 232, 238);
    private static readonly Color EmptyColor = new Color(20, 22, 28);

    private readonly TextBuilder _line = new TextBuilder(64);
    private readonly Color[] _colors = new Color[MaxRows];
    private readonly string[] _names = new string[MaxRows];
    private readonly float[] _shares = new float[MaxRows];

    private string _title = string.Empty;
    private int _rowCount;
    private bool _gradient;

    public bool Visible { get; set; } = true;

    /// <summary>Считает содержимое легенды для текущей карты и режима.</summary>
    public void Rebuild(WorldMap map, MapMode mode)
    {
        ArgumentNullException.ThrowIfNull(map);

        _title = Strings.Get(MapModes.NameKey(mode));
        _rowCount = 0;
        _gradient = false;

        switch (mode)
        {
            case MapMode.Terrain:
                BuildBiomes(map);
                break;
            case MapMode.Resources:
                BuildResources(map);
                break;
            default:
                BuildGradient(map, mode);
                break;
        }
    }

    public void Draw(SpriteBatch batch, PixelFont font, Primitives primitives, int viewportWidth)
    {
        if (!Visible || _rowCount == 0)
        {
            return;
        }

        int lineHeight = font.LineHeight * Scale;
        int body = _gradient ? GradientHeight : _rowCount * lineHeight;
        int height = (Padding * 2) + lineHeight + 4 + body;
        int left = viewportWidth - PanelWidth - Margin;
        var panel = new Rectangle(left, Margin, PanelWidth, height);

        primitives.FillRect(batch, panel, PanelColor);
        primitives.FrameRect(batch, panel, BorderColor, 1);

        int x = left + Padding;
        int y = Margin + Padding;
        font.Draw(batch, _title, new Vector2(x, y), TitleColor, Scale);
        y += lineHeight + 4;

        if (_gradient)
        {
            int width = PanelWidth - (Padding * 2);
            int segment = Math.Max(1, width / _rowCount);
            for (int i = 0; i < _rowCount; i++)
            {
                var cell = new Rectangle(x + (i * segment), y, segment, GradientHeight);
                primitives.FillRect(batch, cell, _colors[i]);
            }

            return;
        }

        int percentX = left + PanelWidth - Padding - 70;
        for (int i = 0; i < _rowCount; i++)
        {
            var swatch = new Rectangle(x, y + ((lineHeight - SwatchSize) / 2), SwatchSize, SwatchSize);
            primitives.FillRect(batch, swatch, _colors[i]);
            primitives.FrameRect(batch, swatch, BorderColor, 1);
            font.Draw(batch, _names[i] ?? string.Empty, new Vector2(x + SwatchSize + 8, y), TextColor, Scale);

            _line.Clear().Append(_shares[i] * 100f, 1).Append('%');
            font.Draw(batch, _line.Span, new Vector2(percentX, y), TextColor, Scale);
            y += lineHeight;
        }
    }

    /// <summary>Биомы по убыванию площади: сразу видно, не выродился ли мир в одно болото.</summary>
    private void BuildBiomes(WorldMap map)
    {
        int total = map.TileCount;
        if (total <= 0)
        {
            return;
        }

        Span<bool> used = stackalloc bool[Biomes.Count];
        while (_rowCount < MaxRows)
        {
            int bestBiome = -1;
            int bestCount = 0;
            for (int b = 0; b < Biomes.Count; b++)
            {
                if (used[b])
                {
                    continue;
                }

                int count = map.BiomeCounts[b];
                if (count > bestCount)
                {
                    bestCount = count;
                    bestBiome = b;
                }
            }

            if (bestBiome < 0)
            {
                break;
            }

            used[bestBiome] = true;
            var biome = (Biome)bestBiome;
            _colors[_rowCount] = BiomePalette.Of(biome);
            _names[_rowCount] = Strings.Get(Biomes.NameKey(biome));
            _shares[_rowCount] = bestCount / (float)total;
            _rowCount++;
        }
    }

    /// <summary>Доля ресурсов считается от суши: в воде их всё равно не бывает.</summary>
    private void BuildResources(WorldMap map)
    {
        int land = map.LandTiles > 0 ? map.LandTiles : map.TileCount;
        if (land <= 0)
        {
            return;
        }

        for (int r = 1; r < ResourceKinds.Count && _rowCount < MaxRows; r++)
        {
            int count = map.ResourceCounts[r];
            if (count <= 0)
            {
                continue;
            }

            var kind = (ResourceKind)r;
            _colors[_rowCount] = BiomePalette.Of(kind);
            _names[_rowCount] = Strings.Get(ResourceKinds.NameKey(kind));
            _shares[_rowCount] = count / (float)land;
            _rowCount++;
        }
    }

    /// <summary>
    /// Шкала собирается из настоящих тайлов карты: для каждой ступени ищется первый
    /// тайл с подходящим значением и берётся его цвет. Так легенда всегда совпадает
    /// с картинкой, даже если палитру потом поменяют.
    /// </summary>
    private void BuildGradient(WorldMap map, MapMode mode)
    {
        _gradient = true;
        int steps = Math.Min(GradientSteps, MaxRows);
        int tiles = map.TileCount;

        for (int step = 0; step < steps; step++)
        {
            float low = step / (float)steps;
            float high = (step + 1) / (float)steps;
            Color color = step > 0 ? _colors[step - 1] : EmptyColor;

            for (int i = 0; i < tiles; i++)
            {
                float value = ValueOf(map, mode, i);
                bool inRange = value >= low && (value < high || (step == steps - 1 && value <= high));
                if (inRange)
                {
                    color = BiomePalette.For(mode, map, i);
                    break;
                }
            }

            _colors[step] = color;
        }

        _rowCount = steps;
    }

    private static float ValueOf(WorldMap map, MapMode mode, int index)
    {
        return mode switch
        {
            MapMode.Height => map.Elevation[index],
            MapMode.Temperature => map.Temperature[index],
            MapMode.Moisture => map.Moisture[index],
            MapMode.Fertility => map.Fertility[index],
            _ => 0f,
        };
    }
}
