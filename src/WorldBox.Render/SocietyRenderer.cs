using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldBox.Core.Society;
using WorldBox.Core.Tribes;

namespace WorldBox.Render;

/// <summary>
/// Слой общества: религии, культуры и формы власти одной текстурой размером с карту.
///
/// Самое важное здесь — два слоя краски. Сначала всё владение заливается бледным цветом
/// веры или культуры двора, а потом поверх него ярко рисуются круги вокруг городов —
/// у каждого своя вера. Именно так глазами видно, что карта религий не совпадает с картой государств:
/// в чужой державе попадаются островки другой веры.
///
/// Текстура собирается только при изменениях и не чаще раза в несколько кадров,
/// поэтому в обычном кадре слой стоит один прямоугольник.
/// </summary>
public sealed class SocietyRenderer : IDisposable
{
    /// <summary>Не чаще чем раз в столько кадров пересобираем текстуру.</summary>
    public const int RebuildEveryFrames = 12;

    private const float FillAlpha = 0.32f;
    private const float SeatAlpha = 0.8f;

    /// <summary>Цвет «ничего ещё не сложилось»: ни веры, ни культуры, ни власти.</summary>
    private static readonly Color Unknown = new Color(148, 148, 156);

    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly int _width;
    private readonly int _height;

    private int _builtSociety = -1;
    private int _builtTerritory = -1;
    private SocietyLayer _builtLayer = SocietyLayer.Off;
    private int _sinceRebuild;

    public SocietyRenderer(GraphicsDevice device, int width, int height)
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

    /// <summary>Какой слой показывать. Off — слой выключен и ничего не стоит.</summary>
    public SocietyLayer Layer { get; set; } = SocietyLayer.Off;

    /// <summary>Сколько городских кругов легло в текстуру в последнюю сборку.</summary>
    public int DrawnPatches { get; private set; }

    /// <summary>Заставляет пересобрать текстуру. Нужно после смены мира.</summary>
    public void Invalidate()
    {
        _builtSociety = -1;
        _builtTerritory = -1;
        _sinceRebuild = RebuildEveryFrames;
    }

    public void Draw(
        SpriteBatch batch,
        Territory territory,
        TribeStore tribes,
        SettlementStore settlements,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(tribes);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(religions);
        ArgumentNullException.ThrowIfNull(cultures);

        if (Layer == SocietyLayer.Off || territory.Width != _width || territory.Height != _height)
        {
            return;
        }

        _sinceRebuild++;
        bool stale = _builtLayer != Layer
            || _builtSociety != state.Version
            || _builtTerritory != territory.Version;

        if (stale && _sinceRebuild >= RebuildEveryFrames)
        {
            Rebuild(territory, tribes, settlements, state, religions, cultures);
            _builtLayer = Layer;
            _builtSociety = state.Version;
            _builtTerritory = territory.Version;
            _sinceRebuild = 0;
        }

        batch.Draw(_texture, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
    }

    private void Rebuild(
        Territory territory,
        TribeStore tribes,
        SettlementStore settlements,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures)
    {
        FillByState(territory, tribes, state, religions, cultures);

        DrawnPatches = 0;
        if (Layer != SocietyLayer.Ideology)
        {
            PaintSeats(territory, settlements, state, religions, cultures);
        }

        _texture.SetData(_pixels);
    }

    /// <summary>Первый слой: бледная заливка по тому, что признаёт двор.</summary>
    private void FillByState(
        Territory territory,
        TribeStore tribes,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures)
    {
        short[] owner = territory.Owner;

        for (int index = 0; index < _pixels.Length; index++)
        {
            short tribe = owner[index];
            if (tribe == TribeStore.None || !tribes.IsAlive(tribe))
            {
                _pixels[index] = Color.Transparent;
                continue;
            }

            _pixels[index] = CrownColor(tribe, state, religions, cultures) * FillAlpha;
        }
    }

    private Color CrownColor(
        short tribe,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures)
    {
        switch (Layer)
        {
            case SocietyLayer.Religion:
                short faith = state.Religion[tribe];
                return religions.IsAlive(faith) ? TribePalette.Of(religions.ColorIndex[faith]) : Unknown;

            case SocietyLayer.Culture:
                short culture = state.Culture[tribe];
                return cultures.IsAlive(culture) ? TribePalette.Of(cultures.ColorIndex[culture]) : Unknown;

            case SocietyLayer.Ideology:
                int form = state.Ideology[tribe];

                // Формы власти идут подряд, а соседние цвета палитры похожи:
                // шаг по палитре в пять разводит их в разные концы.
                return form >= 0 ? TribePalette.Of((byte)(form * 5)) : Unknown;

            default:
                return Unknown;
        }
    }

    /// <summary>
    /// Второй слой: вокруг каждого города яркий круг его собственного выбора.
    /// Сначала стоянки, потом деревни, потом города: крупный город перекрывает мелкие.
    /// </summary>
    private void PaintSeats(
        Territory territory,
        SettlementStore settlements,
        SocietyState state,
        ReligionStore religions,
        CultureStore cultures)
    {
        short[] owner = territory.Owner;
        int high = Math.Min(settlements.HighWater, state.SettlementCapacity);
        int painted = 0;

        for (byte level = 0; level <= SettlementStore.Town; level++)
        {
            for (int i = 0; i < high; i++)
            {
                if (!settlements.Alive[i] || settlements.Level[i] != level)
                {
                    continue;
                }

                Color color;
                if (Layer == SocietyLayer.Religion)
                {
                    short faith = state.SettlementReligion[i];
                    if (!religions.IsAlive(faith))
                    {
                        continue;
                    }

                    color = TribePalette.Of(religions.ColorIndex[faith]) * SeatAlpha;
                }
                else
                {
                    short culture = state.SettlementCulture[i];
                    if (!cultures.IsAlive(culture))
                    {
                        continue;
                    }

                    color = TribePalette.Of(cultures.ColorIndex[culture]) * SeatAlpha;
                }

                int reach = 3 + (settlements.Radius[i] * 2) + (level * 3);
                PaintDisc(owner, settlements.X[i], settlements.Y[i], reach, color);
                painted++;
            }
        }

        DrawnPatches = painted;
    }

    /// <summary>Круг влияния города. За границу владений краска не выходит: море ни во что не верит.</summary>
    private void PaintDisc(short[] owner, int centerX, int centerY, int reach, Color color)
    {
        int minX = Math.Max(0, centerX - reach);
        int maxX = Math.Min(_width - 1, centerX + reach);
        int minY = Math.Max(0, centerY - reach);
        int maxY = Math.Min(_height - 1, centerY + reach);
        int square = reach * reach;

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;
            int dy = y - centerY;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                if ((dx * dx) + (dy * dy) > square)
                {
                    continue;
                }

                int index = row + x;
                if (owner[index] == TribeStore.None)
                {
                    continue;
                }

                _pixels[index] = color;
            }
        }
    }

    public void Dispose()
    {
        _texture.Dispose();
    }
}
