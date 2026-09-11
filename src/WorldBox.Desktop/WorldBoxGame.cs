using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldBox.Core;
using WorldBox.Core.Diagnostics;
using WorldBox.Core.Simulation;
using WorldBox.Core.Time;
using WorldBox.Render;
using WorldBox.Render.Text;
using WorldBox.UI;

namespace WorldBox.Desktop;

/// <summary>
/// Окно и игровой цикл. Срез S0: пустой мир, камера и счётчики.
/// Рисуется сетка мира, чтобы было видно движение камеры и границы карты.
/// Срез S1 заменит сетку настоящей картой биомов.
/// </summary>
public sealed class WorldBoxGame : Game
{
    private static readonly Color Background = new Color(9, 11, 14);
    private static readonly Color WorldFill = new Color(24, 29, 38);
    private static readonly Color CellFill = new Color(31, 38, 49);
    private static readonly Color GridColor = new Color(255, 255, 255, 26);
    private static readonly Color BorderColor = new Color(94, 159, 232, 160);

    private const int CellTiles = 32;

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputState _input = new InputState();
    private readonly SimulationClock _clock = new SimulationClock();
    private readonly FrameStats _frameStats = new FrameStats(180);
    private readonly FrameStats _fpsStats = new FrameStats(60);
    private readonly FrameStats _simStats = new FrameStats(60);
    private readonly Stopwatch _frameWatch = new Stopwatch();
    private readonly DebugOverlay _overlay = new DebugOverlay();
    private readonly WorldState _world;
    private readonly SimulationLoop _loop;

    private SpriteBatch _batch = null!;
    private Primitives _primitives = null!;
    private PixelFont _font = null!;
    private Camera2D _camera = null!;
    private GameSpeed _speedBeforePause = GameSpeed.X1;
    private bool _resizing;

    public WorldBoxGame(int seed, int worldSize)
    {
        _world = new WorldState(worldSize, worldSize, seed);
        _loop = new SimulationLoop(_world);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef,
        };

        // Фиксированный шаг у нас свой, встроенный в MonoGame не нужен.
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
        Strings.Load();
        Window.Title = Strings.Get("app.title");
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
        _camera = new Camera2D(_world.Width, _world.Height, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new Primitives(GraphicsDevice);
        _font = PixelFont.Create(GraphicsDevice);
        base.LoadContent();
    }

    protected override void UnloadContent()
    {
        _font.Dispose();
        _primitives.Dispose();
        _batch.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        _frameWatch.Restart();
        double deltaSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        if (deltaSeconds > 0)
        {
            _fpsStats.Add(gameTime.ElapsedGameTime.TotalMilliseconds);
        }

        _input.Update();
        HandleInput((float)deltaSeconds);
        _camera.Update((float)deltaSeconds);

        int ticks = _clock.Advance(deltaSeconds);
        if (ticks > 0)
        {
            long start = Stopwatch.GetTimestamp();
            _loop.RunTicks(ticks);
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            _simStats.Add(ms / ticks);
        }

        base.Update(gameTime);
    }

    private void HandleInput(float deltaSeconds)
    {
        if (_input.WasPressed(Keys.Escape))
        {
            Exit();
            return;
        }

        if (_input.WasPressed(Keys.F3))
        {
            _overlay.Visible = !_overlay.Visible;
        }

        if (_input.WasPressed(Keys.F1))
        {
            _overlay.HintVisible = !_overlay.HintVisible;
        }

        if (_input.WasPressed(Keys.Space))
        {
            if (_clock.Speed == GameSpeed.Paused)
            {
                _clock.Speed = _speedBeforePause;
            }
            else
            {
                _speedBeforePause = _clock.Speed;
                _clock.Speed = GameSpeed.Paused;
            }
        }

        if (_input.WasPressed(Keys.D1))
        {
            _clock.Speed = GameSpeed.X1;
        }

        if (_input.WasPressed(Keys.D2))
        {
            _clock.Speed = GameSpeed.X4;
        }

        if (_input.WasPressed(Keys.D3))
        {
            _clock.Speed = GameSpeed.X16;
        }

        if (_input.WasPressed(Keys.D4))
        {
            _clock.Speed = GameSpeed.X64;
        }

        if (_input.WasPressed(Keys.Home))
        {
            _camera.FitToWorld();
        }

        if (_input.RightDown || _input.MiddleDown)
        {
            Vector2 delta = _input.MouseDelta;
            if (delta != Vector2.Zero)
            {
                _camera.PanByPixels(delta.X, delta.Y);
            }
        }

        float wheel = _input.WheelDelta;
        if (wheel != 0f)
        {
            _camera.ZoomBy(wheel, _input.MousePosition);
        }

        // Скорость сдвига клавишами одинакова в пикселях экрана на любом зуме.
        float tilesPerSecond = 900f / MathF.Max(_camera.Zoom, 0.0001f);
        if (_input.IsDown(Keys.LeftShift) || _input.IsDown(Keys.RightShift))
        {
            tilesPerSecond *= 2.5f;
        }

        float dx = 0f;
        float dy = 0f;
        if (_input.IsDown(Keys.A) || _input.IsDown(Keys.Left))
        {
            dx -= 1f;
        }

        if (_input.IsDown(Keys.D) || _input.IsDown(Keys.Right))
        {
            dx += 1f;
        }

        if (_input.IsDown(Keys.W) || _input.IsDown(Keys.Up))
        {
            dy -= 1f;
        }

        if (_input.IsDown(Keys.S) || _input.IsDown(Keys.Down))
        {
            dy += 1f;
        }

        if (dx != 0f || dy != 0f)
        {
            _camera.PanByTiles(dx * tilesPerSecond * deltaSeconds, dy * tilesPerSecond * deltaSeconds);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Background);

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, _camera.View);
        DrawWorldPlaceholder();
        _batch.End();

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawOverlay();
        _batch.End();

        base.Draw(gameTime);

        _frameWatch.Stop();
        _frameStats.Add(_frameWatch.Elapsed.TotalMilliseconds);
    }

    private void DrawWorldPlaceholder()
    {
        _primitives.FillRect(_batch, new Rectangle(0, 0, _world.Width, _world.Height), WorldFill);

        _camera.VisibleTiles(out int minX, out int minY, out int maxX, out int maxY);
        int firstCellX = minX / CellTiles;
        int firstCellY = minY / CellTiles;
        int lastCellX = maxX / CellTiles;
        int lastCellY = maxY / CellTiles;

        for (int cellY = firstCellY; cellY <= lastCellY; cellY++)
        {
            for (int cellX = firstCellX; cellX <= lastCellX; cellX++)
            {
                if (((cellX + cellY) & 1) == 0)
                {
                    continue;
                }

                int x = cellX * CellTiles;
                int y = cellY * CellTiles;
                int width = Math.Min(CellTiles, _world.Width - x);
                int height = Math.Min(CellTiles, _world.Height - y);
                if (width > 0 && height > 0)
                {
                    _primitives.FillRect(_batch, new Rectangle(x, y, width, height), CellFill);
                }
            }
        }

        // Сетка появляется только вблизи, чтобы не рябило на общем плане.
        if (_camera.Zoom >= 6f)
        {
            float thickness = 1f / _camera.Zoom;
            for (int x = minX; x <= maxX; x++)
            {
                _primitives.Line(_batch, new Vector2(x, minY), new Vector2(x, maxY + 1), GridColor, thickness);
            }

            for (int y = minY; y <= maxY; y++)
            {
                _primitives.Line(_batch, new Vector2(minX, y), new Vector2(maxX + 1, y), GridColor, thickness);
            }
        }

        float borderThickness = MathF.Max(1f / _camera.Zoom, 0.5f);
        _primitives.Line(_batch, Vector2.Zero, new Vector2(_world.Width, 0), BorderColor, borderThickness);
        _primitives.Line(_batch, new Vector2(0, _world.Height), new Vector2(_world.Width, _world.Height), BorderColor, borderThickness);
        _primitives.Line(_batch, Vector2.Zero, new Vector2(0, _world.Height), BorderColor, borderThickness);
        _primitives.Line(_batch, new Vector2(_world.Width, 0), new Vector2(_world.Width, _world.Height), BorderColor, borderThickness);
    }

    private void DrawOverlay()
    {
        Vector2 cursorWorld = _camera.ScreenToWorld(_input.MousePosition);
        int cursorX = (int)MathF.Floor(cursorWorld.X);
        int cursorY = (int)MathF.Floor(cursorWorld.Y);

        var info = new OverlayInfo
        {
            Fps = _fpsStats.PerSecond,
            FrameMs = _frameStats.Average,
            FrameMsWorst = _frameStats.Percentile(0.99),
            SimMs = _simStats.Average,
            Ticks = _world.Tick,
            Year = _world.Year,
            Speed = _clock.Speed,
            IsBehind = _clock.IsBehind,
            Zoom = _camera.Zoom,
            VisibleTiles = _camera.TilesOnScreenVertically,
            CursorX = cursorX,
            CursorY = cursorY,
            CursorInside = _world.InBounds(cursorX, cursorY),
            WorldWidth = _world.Width,
            WorldHeight = _world.Height,
        };

        _overlay.Draw(_batch, _font, _primitives, in info, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_resizing)
        {
            return;
        }

        _resizing = true;
        _graphics.PreferredBackBufferWidth = Math.Max(800, Window.ClientBounds.Width);
        _graphics.PreferredBackBufferHeight = Math.Max(450, Window.ClientBounds.Height);
        _graphics.ApplyChanges();
        _camera.SetViewport(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _resizing = false;
    }
}
