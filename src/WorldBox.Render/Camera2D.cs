using Microsoft.Xna.Framework;

namespace WorldBox.Render;

/// <summary>
/// Камера над картой. Всё считается в тайлах, зум — это сколько пикселей экрана
/// занимает один тайл. Камера физически не может выйти за край мира.
/// </summary>
public sealed class Camera2D
{
    /// <summary>Во сколько раз меняется зум за один щелчок колеса.</summary>
    public const float ZoomStep = 1.18f;

    /// <summary>Скорость сглаживания зума. Больше — резче.</summary>
    public const float ZoomSmoothing = 18f;

    /// <summary>Максимальное приближение: 48 пикселей на тайл, видны отдельные человечки.</summary>
    public const float MaxZoom = 48f;

    private Vector2 _position;
    private Vector2 _anchor;
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private bool _hasAnchor;

    public Camera2D(int worldWidth, int worldHeight, int viewportWidth, int viewportHeight)
    {
        WorldWidth = Math.Max(1, worldWidth);
        WorldHeight = Math.Max(1, worldHeight);
        ViewportWidth = Math.Max(1, viewportWidth);
        ViewportHeight = Math.Max(1, viewportHeight);
        FitToWorld();
    }

    public int WorldWidth { get; private set; }

    public int WorldHeight { get; private set; }

    public int ViewportWidth { get; private set; }

    public int ViewportHeight { get; private set; }

    /// <summary>Центр экрана в координатах тайлов.</summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = value;
            ClampPosition();
        }
    }

    /// <summary>Пикселей на один тайл.</summary>
    public float Zoom => _zoom;

    public float TargetZoom => _targetZoom;

    /// <summary>Дальше отдаляться некуда: весь мир уже влез в окно.</summary>
    public float MinZoom => MathF.Max(
        ViewportWidth / (float)WorldWidth,
        ViewportHeight / (float)WorldHeight);

    public void SetViewport(int width, int height)
    {
        ViewportWidth = Math.Max(1, width);
        ViewportHeight = Math.Max(1, height);
        _targetZoom = Math.Clamp(_targetZoom, MinZoom, MaxZoom);
        _zoom = Math.Clamp(_zoom, MinZoom, MaxZoom);
        ClampPosition();
    }

    public void SetWorldSize(int width, int height)
    {
        WorldWidth = Math.Max(1, width);
        WorldHeight = Math.Max(1, height);
        FitToWorld();
    }

    /// <summary>Показать весь мир целиком.</summary>
    public void FitToWorld()
    {
        _zoom = MinZoom;
        _targetZoom = _zoom;
        _hasAnchor = false;
        _position = new Vector2(WorldWidth * 0.5f, WorldHeight * 0.5f);
        ClampPosition();
    }

    /// <summary>Перетаскивание мышью: смещение задано в пикселях экрана.</summary>
    public void PanByPixels(float dxPixels, float dyPixels)
    {
        _position.X -= dxPixels / _zoom;
        _position.Y -= dyPixels / _zoom;
        ClampPosition();
    }

    /// <summary>Сдвиг клавишами: скорость в тайлах в секунду на текущем зуме.</summary>
    public void PanByTiles(float dxTiles, float dyTiles)
    {
        _position.X += dxTiles;
        _position.Y += dyTiles;
        ClampPosition();
    }

    /// <summary>Колесо мыши. Точка под курсором остаётся на месте.</summary>
    public void ZoomBy(float steps, Vector2 screenAnchor)
    {
        if (steps == 0f)
        {
            return;
        }

        _targetZoom = Math.Clamp(_targetZoom * MathF.Pow(ZoomStep, steps), MinZoom, MaxZoom);
        _anchor = screenAnchor;
        _hasAnchor = true;
    }

    public void SetZoom(float zoom)
    {
        _targetZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _zoom = _targetZoom;
        _hasAnchor = false;
        ClampPosition();
    }

    /// <summary>Плавное доведение зума до цели. Вызывать раз за кадр.</summary>
    public void Update(float deltaSeconds)
    {
        if (MathF.Abs(_targetZoom - _zoom) < 0.0005f)
        {
            _zoom = _targetZoom;
            _hasAnchor = false;
            return;
        }

        Vector2 anchorWorldBefore = _hasAnchor ? ScreenToWorld(_anchor) : _position;
        float t = 1f - MathF.Exp(-deltaSeconds * ZoomSmoothing);
        _zoom = MathHelper.Lerp(_zoom, _targetZoom, Math.Clamp(t, 0f, 1f));
        if (_hasAnchor)
        {
            Vector2 anchorWorldAfter = ScreenToWorld(_anchor);
            _position += anchorWorldBefore - anchorWorldAfter;
        }

        ClampPosition();
    }

    /// <summary>Матрица для SpriteBatch.Begin.</summary>
    public Matrix View =>
        Matrix.CreateTranslation(-_position.X, -_position.Y, 0f) *
        Matrix.CreateScale(_zoom, _zoom, 1f) *
        Matrix.CreateTranslation(ViewportWidth * 0.5f, ViewportHeight * 0.5f, 0f);

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        return new Vector2(
            ((screen.X - (ViewportWidth * 0.5f)) / _zoom) + _position.X,
            ((screen.Y - (ViewportHeight * 0.5f)) / _zoom) + _position.Y);
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        return new Vector2(
            ((world.X - _position.X) * _zoom) + (ViewportWidth * 0.5f),
            ((world.Y - _position.Y) * _zoom) + (ViewportHeight * 0.5f));
    }

    /// <summary>Прямоугольник видимых тайлов с запасом. Нужен для отсечения лишнего.</summary>
    public void VisibleTiles(out int minX, out int minY, out int maxX, out int maxY, int padding = 1)
    {
        Vector2 topLeft = ScreenToWorld(Vector2.Zero);
        Vector2 bottomRight = ScreenToWorld(new Vector2(ViewportWidth, ViewportHeight));
        minX = Math.Clamp((int)MathF.Floor(topLeft.X) - padding, 0, WorldWidth - 1);
        minY = Math.Clamp((int)MathF.Floor(topLeft.Y) - padding, 0, WorldHeight - 1);
        maxX = Math.Clamp((int)MathF.Ceiling(bottomRight.X) + padding, 0, WorldWidth - 1);
        maxY = Math.Clamp((int)MathF.Ceiling(bottomRight.Y) + padding, 0, WorldHeight - 1);
    }

    /// <summary>Сколько тайлов влезает по вертикали. По этому числу выбирается уровень детализации.</summary>
    public float TilesOnScreenVertically => ViewportHeight / _zoom;

    private void ClampPosition()
    {
        float halfWidth = ViewportWidth / (2f * _zoom);
        float halfHeight = ViewportHeight / (2f * _zoom);

        if (halfWidth * 2f >= WorldWidth)
        {
            _position.X = WorldWidth * 0.5f;
        }
        else
        {
            _position.X = Math.Clamp(_position.X, halfWidth, WorldWidth - halfWidth);
        }

        if (halfHeight * 2f >= WorldHeight)
        {
            _position.Y = WorldHeight * 0.5f;
        }
        else
        {
            _position.Y = Math.Clamp(_position.Y, halfHeight, WorldHeight - halfHeight);
        }
    }
}
