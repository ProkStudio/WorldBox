using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldBox.Core;
using WorldBox.Core.Diagnostics;
using WorldBox.Core.Economy;
using WorldBox.Core.Eras;
using WorldBox.Core.People;
using WorldBox.Core.Roads;
using WorldBox.Core.Simulation;
using WorldBox.Core.Society;
using WorldBox.Core.Time;
using WorldBox.Core.Tribes;
using WorldBox.Core.War;
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
/// от каменного века до космоса — хозяйство со складами, ценами, голодом и торговыми
/// путями, война с отрядами, осадами, взятием городов и бунтами, и оболочка в едином
/// скине: нижняя панель инструментов, значки, круглые панели и таблички городов.
/// </summary>
public sealed class WorldBoxGame : Game
{
    /// <summary>Сколько людей селится на старте партии.</summary>
    private const int StartPeople = 420;

    /// <summary>Сколько народов зарождается на старте.</summary>
    private const int StartTribes = 7;

    /// <summary>Отступ между соседними панелями.</summary>
    private const int PanelGap = 12;

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
    private readonly MarketPanel _marketPanel = new MarketPanel();
    private readonly WarPanel _warPanel = new WarPanel();
    private readonly SocietyPanel _societyPanel = new SocietyPanel();
    private readonly PeopleRenderer _peopleRenderer = new PeopleRenderer();
    private readonly TradeRenderer _tradeRenderer = new TradeRenderer { Visible = false };
    private readonly ArmyRenderer _armyRenderer = new ArmyRenderer();
    private readonly TrafficRenderer _traffic = new TrafficRenderer();
    private readonly Toolbar _toolbar = new Toolbar();
    private readonly NamePlates _plates = new NamePlates();
    private readonly int _size;

    /// <summary>Таблица эпох из data/eras.json. Читается один раз на запуск игры.</summary>
    private readonly EraTable? _eraTable;

    /// <summary>Почему таблица эпох не прочиталась. Пустая строка, если всё хорошо.</summary>
    private readonly string _eraError;

    /// <summary>Таблица товаров из data/economy.json. Тоже читается один раз на запуск.</summary>
    private readonly EconomyTable? _economyTable;

    /// <summary>Почему таблица хозяйства не прочиталась. Пустая строка, если всё хорошо.</summary>
    private readonly string _economyError;

    /// <summary>Таблица войны из data/war.json. Читается один раз на запуск игры.</summary>
    private readonly WarTable? _warTable;

    /// <summary>Почему таблица войны не прочиталась. Пустая строка, если всё хорошо.</summary>
    private readonly string _warError;

    /// <summary>Таблица общества из data/society.json. Читается один раз на запуск игры.</summary>
    private readonly SocietyTable? _societyTable;

    /// <summary>Почему таблица общества не прочиталась. Пустая строка, если всё хорошо.</summary>
    private readonly string _societyError;

    private WorldState _world = null!;
    private SimulationLoop _loop = null!;
    private WorldMap _map = null!;
    private Population _people = null!;
    private PopulationSystem _populationSystem = null!;
    private TribeStore _tribes = null!;
    private SettlementStore _settlements = null!;
    private Territory _territory = null!;
    private SettlementSystem _settlementSystem = null!;
    private TerritorySystem _territorySystem = null!;
    private RoadNetwork _roads = null!;
    private RoadSystem _roadSystem = null!;
    private TribeTech _tech = null!;
    private EraSystem? _eraSystem;
    private TribeMarket? _market;
    private TradeNetwork? _routes;
    private EconomySystem? _economySystem;
    private Diplomacy _diplomacy = null!;
    private ArmyStore? _armies;
    private WarSystem? _warSystem;
    private SocietyState? _societyState;
    private ReligionStore? _religions;
    private CultureStore? _cultures;
    private SocietySystem? _society;
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
    private SocietyRenderer? _societyOverlay;
    private RoadRenderer? _roadRenderer;
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
    private bool _roadsVisible = true;
    private bool _armiesVisible = true;

    /// <summary>Игровое время для транспорта. На паузе не растёт, поэтому всё замирает вместе с миром.</summary>
    private float _trafficSeconds;

    /// <summary>Нажали F7: следующий кадр допишет свой замер в frames.txt.</summary>
    private bool _frameReportRequested;

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

        // Таблицы нужны раньше первого мира: по ним собирается цикл симуляции.
        _eraTable = EraTable.Load(out string eraError);
        _eraError = eraError;

        _economyTable = EconomyTable.Load(out string economyError);
        _economyError = economyError;

        _warTable = WarTable.Load(out string warError);
        _warError = warError;

        _societyTable = SocietyTable.Load(out string societyError);
        _societyError = societyError;

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

        // То же с хозяйством: без таблицы товаров нет ни складов, ни торговых путей.
        if (_economyError.Length > 0)
        {
            Console.Error.WriteLine(_economyError);
        }

        // И то же с войной: без таблицы войны народы никогда не поднимут войско.
        if (_warError.Length > 0)
        {
            Console.Error.WriteLine(_warError);
        }

        // Без таблицы общества не будет ни религий, ни культур, ни форм власти.
        if (_societyError.Length > 0)
        {
            Console.Error.WriteLine(_societyError);
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
        _societyOverlay?.Dispose();
        _roadRenderer?.Dispose();
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

        // Транспорт едет по игровому времени: на паузе стоит, на ускорении торопится.
        _trafficSeconds += (float)deltaSeconds * TrafficFactor();

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

        if (_input.WasPressed(Keys.F7))
        {
            // Числа с экрана глазом не спишешь: по этой клавише замер кадров уходит в файл.
            _frameReportRequested = true;
        }

        if (_input.WasPressed(Keys.F8))
        {
            // Дороги и транспорт гаснут вместе: пустое шоссе без машин выглядит странно.
            _roadsVisible = !_roadsVisible;
        }

        if (_input.WasPressed(Keys.F9))
        {
            // Выключатель войск: нужен, чтобы честно сравнить кадр с войной и без неё.
            _armiesVisible = !_armiesVisible;
        }

        if (_input.WasPressed(Keys.E))
        {
            // Панель народов: кто где живёт и что мешает шагнуть в следующую эпоху.
            _tribePanel.Visible = !_tribePanel.Visible;
        }

        if (_input.WasPressed(Keys.G))
        {
            ToggleMarket();
        }

        if (_input.WasPressed(Keys.V))
        {
            // Окно войны: кто с кем воюет, сколько войск и что война сделала с картой.
            _warPanel.Visible = !_warPanel.Visible;
        }

        if (_input.WasPressed(Keys.K))
        {
            // Слой общества по кругу: религии, культуры, формы власти, выключено.
            if (_societyOverlay != null)
            {
                _societyOverlay.Layer = SocietyLayers.Next(_societyOverlay.Layer);
            }
        }

        if (_input.WasPressed(Keys.C))
        {
            // Окно общества: во что верит двор, какой народ главный и крепка ли власть.
            _societyPanel.Visible = !_societyPanel.Visible;
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
                ToggleMarket();
                break;
            case ToolbarAction.ToggleWar:
                _warPanel.Visible = !_warPanel.Visible;
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

    /// <summary>Окно торговли и линии путей включаются вместе: цифры без карты мало что говорят.</summary>
    private void ToggleMarket()
    {
        _marketPanel.Visible = !_marketPanel.Visible;
        _tradeRenderer.Visible = _marketPanel.Visible;
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

    /// <summary>
    /// Насколько быстрее едет транспорт на ускорении. Скорость мира растёт в четыре раза
    /// за шаг, а машины — мягче: иначе на x64 они мелькают полосками и рябят в глазах.
    /// </summary>
    private float TrafficFactor()
    {
        return _clock.Speed switch
        {
            GameSpeed.Paused => 0f,
            GameSpeed.X4 => 2f,
            GameSpeed.X16 => 3f,
            GameSpeed.X64 => 4f,
            _ => 1f,
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
    /// текстуры чанков. Затем границы держав, торговые пути, постройки и сами люди.
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

        // Слой общества идёт сразу за границами: вера и культура — это про людей, а не про землю.
        if (_societyOverlay != null && _societyState != null && _religions != null && _cultures != null)
        {
            _societyOverlay.Draw(_batch, _territory, _tribes, _settlements, _societyState, _religions, _cultures);
        }

        // Дороги ложатся поверх границ: иначе полупрозрачная заливка державы гасит полотно.
        if (_roadsVisible && _roadRenderer != null)
        {
            _roadRenderer.Draw(_batch, _primitives, _camera, _roads);
        }

        if (_routes != null)
        {
            _tradeRenderer.Draw(_batch, _primitives, _camera, _routes, _tribes);
        }

        // Транспорт едет поверх дороги, но под домами: обоз заезжает за город, а не на него.
        if (_roadsVisible)
        {
            _traffic.Draw(_batch, _primitives, _camera, _roads, _tribes, _trafficSeconds);
        }

        if (detail)
        {
            _buildings.Draw(_batch, _primitives, _camera, _map, _tribes, _settlements, (float)seconds);
        }

        if (_peopleVisible)
        {
            _peopleRenderer.Draw(_batch, _primitives, _camera, _people);
        }

        // Войска рисуются последними: знамя должно читаться поверх домов, людей и дорог.
        if (_armiesVisible && _armies != null)
        {
            _armyRenderer.Draw(_batch, _primitives, _camera, _armies, _settlements, _tribes, (float)seconds);
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
            Wars = _warSystem != null ? _warSystem.ActiveWars : 0,
            Armies = _armies != null ? _armies.Count : 0,
            Sieges = CountSieges(),
        };

        if (_frameReportRequested)
        {
            _frameReportRequested = false;
            WriteFrameReport(in info);
        }

        int viewportWidth = GraphicsDevice.Viewport.Width;
        int viewportHeight = GraphicsDevice.Viewport.Height;

        // Таблички городов идут первыми: панели должны ложиться поверх них.
        _plates.Draw(_batch, _font, _ui, _camera, _tribes, _settlements, viewportWidth, viewportHeight);

        _overlay.Draw(_batch, _font, _primitives, in info, viewportWidth, viewportHeight, _ui);
        _legend.Draw(_batch, _font, _primitives, viewportWidth, _ui);
        _minimap?.Draw(_batch, _primitives, _camera, _ui);

        // Окно торговли встаёт ровно под статистикой, а без неё — в самый верх.
        _marketPanel.TopMargin = _overlay.Bounds.Height > 0 ? _overlay.Bounds.Bottom + PanelGap : 14;
        _marketPanel.Draw(_batch, _font, _primitives, _tribes, _market, _economyTable, _routes, viewportHeight, _ui);

        // Окно войны встаёт под торговым, а без него — на его место: панели не наезжают друг на друга.
        _warPanel.TopMargin = _marketPanel.Bounds.Height > 0
            ? _marketPanel.Bounds.Bottom + PanelGap
            : _marketPanel.TopMargin;
        _warPanel.Draw(
            _batch,
            _font,
            _primitives,
            _tribes,
            _armies,
            _diplomacy,
            _settlements,
            _warSystem,
            _world.YearsPerTick,
            viewportHeight,
            _ui);

        // Окно общества встаёт под окном войны: панели идут одной цепочкой сверху вниз.
        _societyPanel.TopMargin = _warPanel.Bounds.Height > 0
            ? _warPanel.Bounds.Bottom + PanelGap
            : _warPanel.TopMargin;
        _societyPanel.Draw(
            _batch,
            _font,
            _primitives,
            _tribes,
            _society,
            _societyState,
            _religions,
            _cultures,
            viewportHeight,
            _ui);

        // Панель народов ставится над мини-картой, иначе они перекрывают друг друга.
        _tribePanel.BottomMargin = _minimap != null
            ? viewportHeight - _minimap.Bounds.Y + PanelGap
            : Toolbar.ReservedHeight + PanelGap;

        _tribePanel.Draw(_batch, _font, _primitives, _tribes, _tech, _eraTable, viewportWidth, viewportHeight, _ui);
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
            _settlements,
            _ui);

        // Панель инструментов рисуется последней: подсказка должна быть поверх всего.
        var toolbarState = new ToolbarState(
            _clock.Speed == GameSpeed.Paused,
            SpeedIndex(),
            _mode,
            _tribePanel.Visible,
            _marketPanel.Visible,
            _warPanel.Visible,
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
        _territorySystem = new TerritorySystem(_tribes, _territory);

        // Дороги живут своей жизнью: полотно остаётся на земле и после гибели города.
        _roads = new RoadNetwork(_size, _size);
        _roadSystem = new RoadSystem(_tribes, _settlements, _roads);

        // Дипломатия нужна раньше хозяйства: между воюющими народами караваны не ходят.
        _diplomacy = new Diplomacy(_tribes.Capacity);

        // Хозяйство собирается раньше эпох: торговые ресурсы нужны самому первому переходу.
        if (_economyTable != null)
        {
            _market = new TribeMarket(_tribes.Capacity, _economyTable.Count);
            _routes = new TradeNetwork(_economyTable.Trade.MaxRoutes);
            _economySystem = new EconomySystem(_economyTable, _tribes, _settlements, _territory, _market, _routes, _diplomacy);
        }
        else
        {
            _market = null;
            _routes = null;
            _economySystem = null;
        }

        if (_eraTable != null)
        {
            // Первая эпоха задаёт длину тика сразу, иначе первые десять тиков шли бы чужим шагом.
            _world.YearsPerTick = _eraTable.YearsPerTickOf(0);
            _eraSystem = new EraSystem(_eraTable, _tribes, _territory, _tech, _market);
        }
        else
        {
            _eraSystem = null;
        }

        // Война собирается последней: ей нужны и народы, и земли, и амбары городов.
        if (_warTable != null)
        {
            _armies = new ArmyStore(_warTable.Armies.MaxArmies);
            _warSystem = new WarSystem(
                _warTable,
                _eraTable,
                _people,
                _tribes,
                _settlements,
                _territory,
                _armies,
                _diplomacy,
                _market);
        }
        else
        {
            _armies = null;
            _warSystem = null;
        }

        // Общество собирается после войны: ему нужны и границы, и города, и торговые пути.
        if (_societyTable != null)
        {
            _religions = new ReligionStore(_societyTable.Religion.MaxReligions);
            _cultures = new CultureStore(_societyTable.Culture.MaxCultures);
            _societyState = new SocietyState(_tribes.Capacity, _settlements.Capacity);
            _society = new SocietySystem(
                _societyTable,
                _tribes,
                _settlements,
                _societyState,
                _religions,
                _cultures,
                _market,
                _routes,
                _diplomacy);
        }
        else
        {
            _religions = null;
            _cultures = null;
            _societyState = null;
            _society = null;
        }

        _loop = BuildLoop();

        _clock.Reset();
        _clock.Speed = GameSpeed.X1;
        _speedBeforePause = GameSpeed.X1;
        _simStats.Clear();
        _trafficSeconds = 0f;
        _borders?.Invalidate();
        _societyOverlay?.Invalidate();
        _roadRenderer?.Invalidate();
    }

    /// <summary>
    /// Дописывает текущий замер кадров в frames.txt в рабочей папке игры.
    /// Таблицу кадров в docs/PERF.md иначе пришлось бы списывать с экрана глазом,
    /// а так каждое нажатие F7 добавляет в файл готовую строку для таблицы.
    /// </summary>
    private void WriteFrameReport(in OverlayInfo info)
    {
        var text = new StringBuilder(700);
        string scene = info.WorldWidth + "x" + info.WorldHeight
            + ", карта " + Strings.Get(MapModes.NameKey(_mode))
            + ", зум " + info.Zoom.ToString("0.0") + " пкс"
            + ", тайлов по высоте " + (int)info.VisibleTiles;

        text.Append("=== замер кадров ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine(" ===");
        text.Append("окно: ").Append(GraphicsDevice.Viewport.Width).Append('x').AppendLine(GraphicsDevice.Viewport.Height.ToString());
        text.Append("сцена: ").AppendLine(scene);
        text.Append("fps: ").Append(info.Fps.ToString("0"))
            .Append("   кадр: ").Append(info.FrameMs.ToString("0.00")).Append(" мс")
            .Append("   худший 1%: ").Append(info.FrameMsWorst.ToString("0.00")).Append(" мс")
            .Append("   симуляция: ").Append(info.SimMs.ToString("0.000")).AppendLine(" мс/тик");
        text.Append("мир: тик ").Append(info.Ticks)
            .Append(", год ").Append(info.Year.ToString("0"))
            .Append(", эпоха ").Append(info.EraName ?? "-")
            .Append(", скорость ").AppendLine(info.Speed.ToString());
        text.Append("жизнь: людей ").Append(info.People)
            .Append(", народов ").Append(info.Tribes)
            .Append(", поселений ").AppendLine(info.Settlements.ToString());
        text.Append("дороги: маршрутов ").Append(_roads.Count)
            .Append(", тайлов полотна ").Append(_roads.Tiles)
            .Append(", транспорта в кадре ").Append(_traffic.DrawnVehicles)
            .Append(", самолётов ").AppendLine(_traffic.DrawnPlanes.ToString());
        text.Append("война: войн ").Append(_warSystem != null ? _warSystem.ActiveWars : 0)
            .Append(", отрядов ").Append(_armies != null ? _armies.Count : 0)
            .Append(", знамён в кадре ").Append(_armyRenderer.DrawnArmies)
            .Append(", осад в кадре ").Append(_armyRenderer.DrawnSieges)
            .Append(", битв всего ").Append(_warSystem != null ? _warSystem.TotalBattles : 0)
            .Append(", городов взято ").AppendLine((_warSystem != null ? _warSystem.TotalCaptures : 0).ToString());
        text.Append("общество: религий ").Append(_society != null ? _society.ReligionCount : 0)
            .Append(", культур ").Append(_society != null ? _society.CultureCount : 0)
            .Append(", расколов ").Append(_society != null ? _society.SchismCount : 0)
            .Append(", обращений всего ").Append(_society != null ? _society.TotalConversions : 0)
            .Append(", смут ").Append(_society != null ? _society.TotalCollapses : 0)
            .Append(", стабильность ")
            .AppendLine(((int)MathF.Round((_society != null ? _society.AverageStability : 0f) * 100f)).ToString());
        text.Append("слои: ближний план ").Append(OnOff(_detailTiles))
            .Append(", люди ").Append(OnOff(_peopleVisible))
            .Append(", декор ").Append(OnOff(_decorVisible))
            .Append(", границы ").Append(OnOff(_bordersVisible))
            .Append(", дороги ").Append(OnOff(_roadsVisible))
            .Append(", войска ").Append(OnOff(_armiesVisible))
            .Append(", торговые пути ").Append(OnOff(_tradeRenderer.Visible))
            .Append(", панель народов ").Append(OnOff(_tribePanel.Visible))
            .Append(", легенда ").AppendLine(OnOff(_legend.Visible));
        text.Append("| ").Append(DateTime.Now.ToString("yyyy-MM-dd"))
            .Append(" | | ").Append(scene)
            .Append(" | ").Append(info.Fps.ToString("0"))
            .Append(" | ").Append(info.FrameMs.ToString("0.00"))
            .Append(" | ").Append(info.FrameMsWorst.ToString("0.00"))
            .AppendLine(" | |");
        text.AppendLine();

        try
        {
            File.AppendAllText(Path.Combine(Environment.CurrentDirectory, "frames.txt"), text.ToString(), Encoding.UTF8);
        }
        catch (IOException)
        {
            // Замер — дело необязательное: если файл занят, игра просто играет дальше.
        }
        catch (UnauthorizedAccessException)
        {
            // То же самое: папка только для чтения — не повод ронять игру.
        }
    }

    /// <summary>Короткая пометка для отчёта: включён слой или нет.</summary>
    private static string OnOff(bool value) => value ? "да" : "нет";

    /// <summary>Сколько городов сейчас в осаде. Один проход по списку поселений за кадр.</summary>
    private int CountSieges()
    {
        int count = 0;
        for (int i = 0; i < _settlements.HighWater; i++)
        {
            if (_settlements.Alive[i] && _settlements.Siege[i] > 0f)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Собирает цикл из того, что удалось загрузить: без таблиц игра всё равно живая.
    /// Порядок важен: люди и поселения раньше земель, земли раньше хозяйства и эпох,
    /// а список собирается один раз на мир, а не каждый тик.
    /// </summary>
    private SimulationLoop BuildLoop()
    {
        var systems = new List<ISimulationSystem>(9)
        {
            _populationSystem,
            _settlementSystem,
            _territorySystem,
            _roadSystem,
        };

        if (_economySystem != null)
        {
            systems.Add(_economySystem);
        }

        if (_eraSystem != null)
        {
            systems.Add(_eraSystem);
        }

        // Война идёт последней в тике: она смотрит на уже обновлённые границы, амбары и эпохи.
        if (_warSystem != null)
        {
            systems.Add(_warSystem);
        }

        // Общество замыкает тик: оно смотрит на уже взятые города и добавляет им недовольство.
        if (_society != null)
        {
            systems.Add(_society);
        }

        return new SimulationLoop(_world, systems.ToArray());
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

        // Выбранный слой общества переживает смену мира: иначе после N карта неожиданно гаснет.
        SocietyLayer layer = _societyOverlay?.Layer ?? SocietyLayer.Off;
        _societyOverlay?.Dispose();
        _societyOverlay = new SocietyRenderer(GraphicsDevice, _map.Width, _map.Height);
        _societyOverlay.Layer = layer;

        _roadRenderer?.Dispose();
        _roadRenderer = new RoadRenderer(GraphicsDevice, _map.Width, _map.Height);

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
