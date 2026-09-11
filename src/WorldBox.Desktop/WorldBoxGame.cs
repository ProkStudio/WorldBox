using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldBox.Core;
using WorldBox.Core.Diagnostics;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Simulation;
using WorldBox.Core.Time;
using WorldBox.Core.Tribes;
using WorldBox.Core.World;
using WorldBox.Render;
using WorldBox.Render.Text;
using WorldBox.UI;

namespace WorldBox.Desktop;

/// <summary>
/// Окно и игровой цикл. Сейчас здесь: живая карта мира (биомы, реки, ресурсы),
/// шесть режимов карты, мини-карта, легенда, ближний план спрайтами 16x16
/// с растительностью и постройками, жители, которые едят, кочуют, рожают и умирают,
/// племена со своими поселениями и границами, развитие народов по эпохам —
/// от каменного века до космоса — и оболочка: нижняя панель инструментов,
/// значки и таблички городов.
/// </summary>
public sealed class WorldBoxGame : Game
{
    /// <summary>Сколько людей селится на старте партии.</summary>
    private const int StartPeople = 420;

    /// <summary>Сколько народов зарождается на старте.</summary>
    private const int StartTribes = 7;

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
    private readonly TribePanel _tribePanel = new TribePanel();
    private readonly PeopleRenderer _peopleRenderer = new PeopleRenderer();
    private readonly Toolbar _toolbar = new Toolbar();
    private readonly NamePlates _plates = new NamePlates();
    private readonly int _size;

    /// <summary>Таблица эпох из data/eras.json. Читается один раз на запуск игры.</summary>
    private readonly EraTable? _eraTable;

    /// <summary>Почему таблица эпох не прочиталась. Пустая строка, если всё хорошо.</summary>
    private readonly string _eraError;

    private WorldState _world = null!;
    private SimulationLoop _loop = null!;
    private WorldMap _map = null!;
    private Population _people = null!;
    private PopulationSystem _populationSystem = null!;
    private TribeStore _tribes = null!;
    private SettlementStore _settlements = null!;
    private Territory _territory = null!;
    private SettlementSystem _settlementSystem = null!;
    private TribeTech _tech = null!;
    private EraSystem? _eraSystem;
    private SpriteBatch _batch = null!;
    private Primitives _primitives = null!;
    private PixelFont _font = null!;
    private Camera2D _camera = null!;
    private UiSkin _ui = null!;
    private DecorAtlas _decorAtlas = null!;
    private SettlementRenderer _buildings = null!;
    private TileRenderer? _tiles;
    private TileSpriteRenderer? _sprites;
    private DecorRenderer? _decor;
    private TerritoryRenderer? _borders;
    private Minimap? _minimap;

    private MapMode _mode = MapMode.Terrain;
    private GameSpeed _speedBeforePause = GameSpeed.X1;
    private double _generationMs;
    private int _seed;
    private bool _resizing;
    private bool _detailTiles = true;
    private bool _peopleVisible = true;
    private bool _bordersVisible = true;
    private bool _decorVisible = true;

    /// <summary>Панель торговли ещё не собрана, но кнопка уже помнит своё состояние.</summary>
    private bool _marketVisible;

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

        // Таблица эпох нужна раньше первого мира: по ней собирается цикл симуляции.
        _eraTable = EraTable.Load(out string eraError);
        _eraError = eraError;

        GenerateWorld(seed);
    }

    protected override void Initialize()
    {
        Strings.Load();
        Window.Title = Strings.Get("app.title");
        Window.AllowUserResizing = true;

        // Без таблицы эпох игра работает, но народы застревают в каменном веке.
        // Молча это прятать нельзя, поэтому пишем причину в консоль.
        if (_eraError.Length > 0)
        {
            Console.Error.WriteLine(_eraError);
        }

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

        // Атласы интерфейса и декора от мира не зависят и собираются один раз.
        _ui = new UiSkin(GraphicsDevice);
        _decorAtlas = new DecorAtlas(GraphicsDevice);
        _buildings = new SettlementRenderer(_decorAtlas);

        RebuildGraphics();
        base.LoadContent();
    }

    protected override void UnloadContent()
    {
        _tiles?.Dispose();
        _sprites?.Dispose();
        _decor?.Dispose();
        _borders?.Dispose();
        _minimap?.Dispose();
        _decorAtlas.Dispose();
        _ui.Dispose();
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

        // Панель инструментов знает про курсор до клика: иначе подсветка отстаёт на кадр.
        Vector2 mouse = _input.MousePosition;
        int mouseX = (int)mouse.X;
        int mouseY = (int)mouse.Y;
        _toolbar.Layout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _toolbar.Update(mouseX, mouseY);

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

        if (_input.WasPressed(Keys.F6))
        {
            // Растительность и постройки — самый тяжёлый слой вблизи, его удобно сравнивать.
            _decorVisible = !_decorVisible;
            if (_decor != null)
            {
                _decor.Visible = _decorVisible;
            }

            _buildings.Visible = _decorVisible;
        }

        if (_input.WasPressed(Keys.E))
        {
            // Панель народов: кто где живёт и что мешает шагнуть в следующую эпоху.
            _tribePanel.Visible = !_tribePanel.Visible;
        }

        if (_input.WasPressed(Keys.G))
        {
            _marketVisible = !_marketVisible;
        }

        if (_input.WasPressed(Keys.T))
        {
            _bordersVisible = !_bordersVisible;
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
            NewWorld();
        }

        if (_input.WasPressed(Keys.R))
        {
            GenerateWorld(_seed);
            RebuildGraphics();
        }

        if (_input.WasPressed(Keys.Space))
        {
            TogglePause();
        }

        if (_input.WasPressed(Keys.D1))
        {
            SetSpeed(GameSpeed.X1);
        }

        if (_input.WasPressed(Keys.D2))
        {
            SetSpeed(GameSpeed.X4);
        }

        if (_input.WasPressed(Keys.D3))
        {
            SetSpeed(GameSpeed.X16);
        }

        if (_input.WasPressed(Keys.D4))
        {
            SetSpeed(GameSpeed.X64);
        }

        if (_input.WasPressed(Keys.Home))
        {
            _camera.FitToWorld();
        }

        if (_input.LeftPressed)
        {
            // Клик по панели не должен уходить в карту и в мини-карту.
            if (_toolbar.Contains(mouseX, mouseY))
            {
                Apply(_toolbar.HitTest(mouseX, mouseY));
            }
            else if (_minimap != null && _minimap.TryPick(mouse, out Vector2 target))
            {
                _camera.Position = target;
            }
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

    /// <summary>Действие кнопки панели. Всё то же самое доступно с клавиатуры.</summary>
    private void Apply(ToolbarAction action)
    {
        switch (action)
        {
            case ToolbarAction.TogglePause:
                TogglePause();
                break;
            case ToolbarAction.Speed1:
                SetSpeed(GameSpeed.X1);
                break;
            case ToolbarAction.Speed2:
                SetSpeed(GameSpeed.X4);
                break;
            case ToolbarAction.Speed3:
                SetSpeed(GameSpeed.X16);
                break;
            case ToolbarAction.Speed4:
                SetSpeed(GameSpeed.X64);
                break;
            case ToolbarAction.MapTerrain:
                SetMapMode(MapMode.Terrain);
                break;
            case ToolbarAction.MapHeight:
                SetMapMode(MapMode.Height);
                break;
            case ToolbarAction.MapTemperature:
                SetMapMode(MapMode.Temperature);
                break;
            case ToolbarAction.MapMoisture:
                SetMapMode(MapMode.Moisture);
                break;
            case ToolbarAction.MapFertility:
                SetMapMode(MapMode.Fertility);
                break;
            case ToolbarAction.MapResources:
                SetMapMode(MapMode.Resources);
                break;
            case ToolbarAction.ToggleTribes:
                _tribePanel.Visible = !_tribePanel.Visible;
                break;
            case ToolbarAction.ToggleMarket:
                _marketVisible = !_marketVisible;
                break;
            case ToolbarAction.ToggleLegend:
                _legend.Visible = !_legend.Visible;
                break;
            case ToolbarAction.ToggleBorders:
                _bordersVisible = !_bordersVisible;
                break;
            case ToolbarAction.ToggleInspector:
                _inspector.Visible = !_inspector.Visible;
                break;
            case ToolbarAction.NewWorld:
                NewWorld();
                break;
            case ToolbarAction.FitWorld:
                _camera.FitToWorld();
                break;
            default:
                break;
        }
    }

    private void TogglePause()
    {
        if (_clock.Speed == GameSpeed.Paused)
        {
            _clock.Speed = _speedBeforePause;
            return;
        }

        _speedBeforePause = _clock.Speed;
        _clock.Speed = GameSpeed.Paused;
    }

    private void SetSpeed(GameSpeed speed)
    {
        _clock.Speed = speed;
        _speedBeforePause = speed;
    }

    /// <summary>Номер скорости от 1 до 4 для подсветки кнопок.</summary>
    private int SpeedIndex()
    {
        GameSpeed speed = _clock.Speed == GameSpeed.Paused ? _speedBeforePause : _clock.Speed;
        return speed switch
        {
            GameSpeed.X64 => 4,
            GameSpeed.X16 => 3,
            GameSpeed.X4 => 2,
            _ => 1,
        };
    }

    /// <summary>Следующий сид считается из текущего, часы не участвуют: цепочка миров повторима.</summary>
    private void NewWorld()
    {
        GenerateWorld(unchecked((_seed * 1664525) + 1013904223));
        RebuildGraphics();
        _camera.FitToWorld();
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
    /// Близко и в режиме ландшафта рисуем спрайты и поверх них растительность, иначе —
    /// текстуры чанков. Затем границы держав, постройки и сами люди.
    /// Вблизи дальние значки поселений гаснут: их заменяют дома и замки.
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
            _decor?.Draw(_batch, _camera);
        }
        else
        {
            _tiles?.Draw(_batch, _camera);
        }

        if (_bordersVisible && _borders != null)
        {
            _borders.MarkersVisible = !detail;
            _borders.Draw(_batch, _primitives, _camera, _territory, _tribes, _settlements);
        }

        if (detail)
        {
            _buildings.Draw(_batch, _primitives, _camera, _tribes, _settlements);
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
        short largest = _tribes.Largest();

        // В шапке показываем эпоху самого развитого народа: по ней же считается сжатие времени.
        string? eraName = null;
        if (_eraTable != null)
        {
            int topEra = _eraSystem != null ? _eraSystem.TopEra : 0;
            eraName = Strings.Get(_eraTable.NameKeyOf(topEra));
        }

        var info = new OverlayInfo
        {
            Fps = _fpsStats.PerSecond,
            FrameMs = _frameStats.Average,
            FrameMsWorst = _frameStats.Percentile(0.99),
            SimMs = _simStats.Average,
            Ticks = _world.Tick,
            Year = _world.Year,
            YearsPerTick = _world.YearsPerTick,
            EraName = eraName,
            Speed = _clock.Speed,
            IsBehind = _clock.IsBehind,
            Zoom = _camera.Zoom,
            VisibleTiles = _camera.TilesOnScreenVertically,
            CursorX = cursorX,
            CursorY = cursorY,
            CursorInside = inside,
            WorldWidth = _world.Width,
            WorldHeight = _world.Height,
            People = _people.Count,
            Births = _populationSystem.LastBirths,
            Deaths = _populationSystem.LastDeaths,
            Tribes = _tribes.Count,
            Settlements = _settlements.Count,
            LargestTribe = _tribes.NameOf(largest),
        };

        int viewportWidth = GraphicsDevice.Viewport.Width;
        int viewportHeight = GraphicsDevice.Viewport.Height;

        // Таблички городов идут первыми: панели должны ложиться поверх них.
        _plates.Draw(_batch, _font, _ui, _camera, _tribes, _settlements, viewportWidth, viewportHeight);

        _overlay.Draw(_batch, _font, _primitives, in info, viewportWidth, viewportHeight);
        _legend.Draw(_batch, _font, _primitives, viewportWidth);
        _minimap?.Draw(_batch, _primitives, _camera);
        _tribePanel.Draw(_batch, _font, _primitives, _tribes, _tech, _eraTable, viewportWidth, viewportHeight);
        _inspector.Draw(
            _batch,
            _font,
            _primitives,
            _map,
            _mode,
            cursorX,
            cursorY,
            inside,
            _generationMs,
            viewportHeight,
            _territory,
            _tribes,
            _tech,
            _eraTable,
            _settlements);

        // Панель инструментов рисуется последней: подсказка должна быть поверх всего.
        var toolbarState = new ToolbarState(
            _clock.Speed == GameSpeed.Paused,
            SpeedIndex(),
            _mode,
            _tribePanel.Visible,
            _marketVisible,
            _legend.Visible,
            _bordersVisible,
            _inspector.Visible);

        _toolbar.Draw(_batch, _font, _ui, toolbarState, viewportWidth, viewportHeight);
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

    /// <summary>Считает новую карту, селит народы и начинает партию заново. Графика здесь не трогается.</summary>
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
        _tribes = new TribeStore();
        _settlements = new SettlementStore();
        _territory = new Territory(_size, _size, _tribes.Capacity);
        _tech = new TribeTech(_tribes.Capacity);

        TribeSeeder.Seed(_world, _people, _tribes, _settlements, _territory, StartTribes, StartPeople / StartTribes);

        _populationSystem = new PopulationSystem(_people, null, _tech);
        _settlementSystem = new SettlementSystem(_people, _tribes, _settlements, _territory);

        if (_eraTable != null)
        {
            // Первая эпоха задаёт длину тика сразу, иначе первые десять тиков шли бы чужим шагом.
            _world.YearsPerTick = _eraTable.YearsPerTickOf(0);
            _eraSystem = new EraSystem(_eraTable, _tribes, _territory, _tech);
            _loop = new SimulationLoop(_world, _populationSystem, _settlementSystem, _eraSystem);
        }
        else
        {
            _eraSystem = null;
            _loop = new SimulationLoop(_world, _populationSystem, _settlementSystem);
        }

        _clock.Reset();
        _clock.Speed = GameSpeed.X1;
        _speedBeforePause = GameSpeed.X1;
        _simStats.Clear();
        _borders?.Invalidate();
    }

    private void RebuildGraphics()
    {
        _tiles?.Dispose();
        _tiles = new TileRenderer(GraphicsDevice, _map);
        _tiles.Mode = _mode;
        _tiles.BuildAll();

        _sprites?.Dispose();
        _sprites = new TileSpriteRenderer(GraphicsDevice, _map);

        // Где что растёт, считается один раз на мир, а не каждый кадр.
        _decor?.Dispose();
        _decor = new DecorRenderer(_decorAtlas, _map);
        _decor.Visible = _decorVisible;
        _buildings.Visible = _decorVisible;

        _borders?.Dispose();
        _borders = new TerritoryRenderer(GraphicsDevice, _map.Width, _map.Height);

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
        _toolbar.Layout(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _resizing = false;
    }
}
