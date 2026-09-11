using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldBox.Core;
using WorldBox.Core.Diagnostics;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Time;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;
using WorldBox.UI;

namespace WorldBox.Desktop;

/// <summary>
/// Окно и игровой цикл. Сейчас здесь: живая карта мира (биомы, реки, ресурсы),
/// шесть режимов карты, мини-карта, легенда, ближний план спрайтами 16x16
/// и первые жители, которые едят, кочуют, рожают и умирают.
/// </summary>
public sealed class WorldBoxGame : Game
{
    /// <summary>Сколько людей селится на старте партии.</summary>
    private const int StartPeople = 400;

    private static readonly Color Background = new Color(9, 11, 14);
    private static readonly Color BorderColor = new Color(94, 159, 232, 150);

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputState _input = new InputState();
    private readonly SimulationClock _clock = new SimulationClock();
    private readonly FrameStats _frameStats = new FrameStats(180);
    private readonly FrameStats _fpsStats = new FrameStats(60);
    private readonly FrameStats _simStats = new FrameStats(60);
    private readonly Stopwatch _frameWatch = new Stopwatch();
    private readonly DebugOverlay _overlay = new DebugOverlay();
    private readonly TileInspector _inspector = new TileInspector();
    private readonly BiomeLegend _legend = new BiomeLegend();
    private readonly PeopleRenderer _peopleRenderer = new PeopleRenderer();
    private readonly int _size;

    private WorldState _world = null!;
    private SimulationLoop _loop = null!;
    private WorldMap _map = null!;
    private Population _people = null!;
    private PopulationSystem _populationSystem = null!;
    private SpriteBatch _batch = null!;
    private Primitives _primitives = null!;
    private PixelFont _font = null!;
    private Camera2D _camera = null!;
    private TileRenderer? _tiles;
    private TileSpriteRenderer? _sprites;
    private Minimap? _minimap;

    private MapMode _mode = MapMode.Terrain;
    private GameSpeed _speedBeforePause = GameSpeed.X1;
    private double _generationMs;
    private int _seed;
    private bool _resizing;
    private bool _detailTiles = true;
    private bool _peopleVisible = true;

    public WorldBoxGame(int seed, int worldSize)
    {
        _size = worldSize;
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

        GenerateWorld(seed);
    }

    protected override void Initialize()
    {
        Strings.Load();
        Window.Title = Strings.Get("app.title");
        Window.AllowUserResizing = true;

        // Камера создаётся раньше подписки: событие смены размера трогает камеру.
        _camera = new Camera2D(_world.Width, _world.Height, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        Window.ClientSizeChanged += OnClientSizeChanged;
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new Primitives(GraphicsDevice);
        _font = PixelFont.Create(GraphicsDevice);
        RebuildGraphics();
        base.LoadContent();
    }

    protected override void UnloadContent()
    {
        _tiles?.Dispose();
        _sprites?.Dispose();
        _minimap?.Dispose();
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
        _tiles?.Update(8);

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

        bool shift = _input.IsDown(Keys.LeftShift) || _input.IsDown(Keys.RightShift);

        if (_input.WasPressed(Keys.F3))
        {
            _overlay.Visible = !_overlay.Visible;
        }

        if (_input.WasPressed(Keys.F2))
        {
            _inspector.Visible = !_inspector.Visible;
        }

        if (_input.WasPressed(Keys.F1))
        {
            _overlay.HintVisible = !_overlay.HintVisible;
        }

        if (_input.WasPressed(Keys.F4))
        {
            // Аварийный выключатель ближнего плана: полезен при замерах и на слабом железе.
            _detailTiles = !_detailTiles;
        }

        if (_input.WasPressed(Keys.F5))
        {
            _peopleVisible = !_peopleVisible;
        }

        if (_input.WasPressed(Keys.L))
        {
            _legend.Visible = !_legend.Visible;
        }

        if (_input.WasPressed(Keys.M))
        {
            SetMapMode(shift ? MapModes.Previous(_mode) : MapModes.Next(_mode));
        }

        if (_input.WasPressed(Keys.N))
        {
            // Следующий сид считается из текущего, часы не участвуют: цепочка миров повторима.
            GenerateWorld(unchecked((_seed * 1664525) + 1013904223));
            RebuildGraphics();
            _camera.FitToWorld();
        }

        if (_input.WasPressed(Keys.R))
        {
            GenerateWorld(_seed);
            RebuildGraphics();
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

        if (_input.LeftPressed && _minimap != null && _minimap.TryPick(_input.MousePosition, out Vector2 target))
        {
            _camera.Position = target;
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
        if (shift)
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
        DrawWorld(gameTime.TotalGameTime.TotalSeconds);
        DrawWorldBorder();
        _batch.End();

        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawInterface();
        _batch.End();

        base.Draw(gameTime);

        _frameWatch.Stop();
        _frameStats.Add(_frameWatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Близко и в режиме ландшафта рисуем спрайты, иначе — текстуры чанков.
    /// Режимы вроде высоты или влажности всегда идут дальним рендером: там нужны цвета, а не текстура.
    /// </summary>
    private void DrawWorld(double seconds)
    {
        bool detail = _detailTiles
            && _mode == MapMode.Terrain
            && _sprites != null
            && _camera.Zoom >= TileSpriteRenderer.MinZoom;

        if (detail)
        {
            _sprites!.Draw(_batch, _camera, seconds);
        }
        else
        {
            _tiles?.Draw(_batch, _camera);
        }

        if (_peopleVisible)
        {
            _peopleRenderer.Draw(_batch, _primitives, _camera, _people);
        }
    }

    private void DrawWorldBorder()
    {
        float thickness = MathF.Max(1f / _camera.Zoom, 0.4f);
        var topLeft = Vector2.Zero;
        var topRight = new Vector2(_map.Width, 0);
        var bottomLeft = new Vector2(0, _map.Height);
        var bottomRight = new Vector2(_map.Width, _map.Height);

        _primitives.Line(_batch, topLeft, topRight, BorderColor, thickness);
        _primitives.Line(_batch, bottomLeft, bottomRight, BorderColor, thickness);
        _primitives.Line(_batch, topLeft, bottomLeft, BorderColor, thickness);
        _primitives.Line(_batch, topRight, bottomRight, BorderColor, thickness);
    }

    private void DrawInterface()
    {
        Vector2 cursorWorld = _camera.ScreenToWorld(_input.MousePosition);
        int cursorX = (int)MathF.Floor(cursorWorld.X);
        int cursorY = (int)MathF.Floor(cursorWorld.Y);
        bool inside = _world.InBounds(cursorX, cursorY);

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
            CursorInside = inside,
            WorldWidth = _world.Width,
            WorldHeight = _world.Height,
        };

        int viewportWidth = GraphicsDevice.Viewport.Width;
        int viewportHeight = GraphicsDevice.Viewport.Height;

        _overlay.Draw(_batch, _font, _primitives, in info, viewportWidth, viewportHeight);
        _legend.Draw(_batch, _font, _primitives, viewportWidth);
        _minimap?.Draw(_batch, _primitives, _camera);
        _inspector.Draw(_batch, _font, _primitives, _map, _mode, cursorX, cursorY, inside, _generationMs, viewportHeight);
    }

    private void SetMapMode(MapMode mode)
    {
        _mode = mode;
        if (_tiles != null)
        {
            _tiles.Mode = mode;
        }

        _minimap?.Rebuild(mode);
        _legend.Rebuild(_map, mode);
    }

    /// <summary>Считает новую карту, селит людей и начинает партию заново. Графика здесь не трогается.</summary>
    private void GenerateWorld(int seed)
    {
        var watch = Stopwatch.StartNew();
        WorldMap map = WorldGenerator.Generate(_size, _size, seed);
        watch.Stop();

        _map = map;
        _seed = seed;
        _generationMs = watch.Elapsed.TotalMilliseconds;

        _world = new WorldState(_size, _size, seed);
        _world.SetMap(map);

        _people = new Population(Population.DefaultCapacity, _size, _size);
        PopulationSeeder.Seed(_world, _people, StartPeople);

        _populationSystem = new PopulationSystem(_people);
        _loop = new SimulationLoop(_world, _populationSystem);

        _clock.Reset();
        _clock.Speed = GameSpeed.X1;
        _speedBeforePause = GameSpeed.X1;
        _simStats.Clear();
    }

    private void RebuildGraphics()
    {
        _tiles?.Dispose();
        _tiles = new TileRenderer(GraphicsDevice, _map);
        _tiles.Mode = _mode;
        _tiles.BuildAll();

        _sprites?.Dispose();
        _sprites = new TileSpriteRenderer(GraphicsDevice, _map);

        _minimap?.Dispose();
        _minimap = new Minimap(GraphicsDevice, _map);
        _minimap.Rebuild(_mode);
        _minimap.Layout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        _legend.Rebuild(_map, _mode);
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
        _minimap?.Layout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _resizing = false;
    }
}
