using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace WorldBox.Desktop;

/// <summary>Текущее и прошлое состояние клавиатуры и мыши. Без аллокаций за кадр.</summary>
public sealed class InputState
{
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _mouse;
    private MouseState _previousMouse;

    public Vector2 MousePosition => new Vector2(_mouse.X, _mouse.Y);

    public Vector2 MouseDelta => new Vector2(_mouse.X - _previousMouse.X, _mouse.Y - _previousMouse.Y);

    /// <summary>Щелчки колеса: один щелчок это 120 единиц.</summary>
    public float WheelDelta => (_mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120f;

    public bool RightDown => _mouse.RightButton == ButtonState.Pressed;

    public bool MiddleDown => _mouse.MiddleButton == ButtonState.Pressed;

    public bool LeftPressed => _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    public void Update()
    {
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _keyboard = Keyboard.GetState();
        _mouse = Mouse.GetState();
    }

    public bool IsDown(Keys key) => _keyboard.IsKeyDown(key);

    public bool WasPressed(Keys key) => _keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
}
