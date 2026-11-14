using System.Reflection;
using ArcadeMania.App.Services;
using ArcadeMania.Core.Engine;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Gaming.Input;
using Windows.System;

namespace ArcadeMania.App.WinPlatform;

public static class WinCursor
{
    static readonly PropertyInfo? CursorProp = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void SetHand(UIElement el)
    {
        try { CursorProp?.SetValue(el, InputSystemCursor.Create(InputSystemCursorShape.Hand)); } catch { }
    }
}

public static class WinWindow
{
    static AppWindow? _appWindow;

    public static void Attach(Microsoft.UI.Xaml.Window window)
    {
        try
        {
            _appWindow = window.AppWindow;
            if (_appWindow.Presenter is OverlappedPresenter p) p.Maximize();   // start big: 1080p / 1440p / 4K monitors
        }
        catch { }
        HookKeyboard(window);
        GamepadPoller.Start(window.DispatcherQueue);
    }

    public static void ToggleFullScreen()
    {
        try
        {
            if (_appWindow is null) return;
            _appWindow.SetPresenter(_appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen
                ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen);
        }
        catch { }
    }

    // ---------- keyboard: arrows/WASD, Z/J/Space = A, X/K = B, Enter/P = Start, Esc = Back, F11 = full screen ----------
    static void HookKeyboard(Microsoft.UI.Xaml.Window window)
    {
        void Hook()
        {
            if (window.Content is not UIElement root) return;
            root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) => e.Handled = OnKey(e.Key, true)), true);
            root.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler((_, e) => e.Handled = OnKey(e.Key, false)), true);
        }
        if (window.Content is not null) Hook();
        else window.Activated += (_, _) => { if (!_hooked) { _hooked = true; Hook(); } };
    }
    static bool _hooked;

    static bool OnKey(VirtualKey key, bool down)
    {
        if (key == VirtualKey.F11 && down) { ToggleFullScreen(); return true; }
        if (key == VirtualKey.Escape) { if (down) ControllerHub.Back(); return true; }
        if (!ControllerHub.GameActive) return false;   // menus/search keep normal keyboard behaviour
        GameKey? k = key switch
        {
            VirtualKey.Left or VirtualKey.A => GameKey.Left, VirtualKey.Right or VirtualKey.D => GameKey.Right,
            VirtualKey.Up or VirtualKey.W => GameKey.Up, VirtualKey.Down or VirtualKey.S => GameKey.Down,
            VirtualKey.Z or VirtualKey.J or VirtualKey.Space => GameKey.A,
            VirtualKey.X or VirtualKey.K => GameKey.B,
            VirtualKey.Enter or VirtualKey.P => GameKey.Start,
            _ => null
        };
        if (k is null) return false;
        ControllerHub.Key(k.Value, down);
        return true;
    }
}

/// <summary>Polls Xbox pads (Windows.Gaming.Input.Gamepad) and PlayStation DualShock 4 / DualSense or other HID pads (RawGameController).</summary>
public static class GamepadPoller
{
    static Microsoft.UI.Dispatching.DispatcherQueueTimer? _timer;

    public static void Start(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        if (_timer is not null) return;
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(8);
        _timer.Tick += (_, _) => { try { Poll(); } catch { } };
        _timer.Start();
    }

    static void Poll()
    {
        if (Gamepad.Gamepads.Count > 0)
        {
            ControllerHub.ControllerConnected = true;
            var r = Gamepad.Gamepads[0].GetCurrentReading();
            bool B(GamepadButtons b) => (r.Buttons & b) == b;
            ControllerHub.Pad(new PadSnapshot(
                (float)r.LeftThumbstickX, -(float)r.LeftThumbstickY,
                B(GamepadButtons.DPadUp), B(GamepadButtons.DPadDown), B(GamepadButtons.DPadLeft), B(GamepadButtons.DPadRight),
                B(GamepadButtons.A), B(GamepadButtons.B), B(GamepadButtons.X), B(GamepadButtons.Y),
                B(GamepadButtons.Menu), B(GamepadButtons.View), B(GamepadButtons.LeftShoulder), B(GamepadButtons.RightShoulder)));
            return;
        }

        if (RawGameController.RawGameControllers.Count > 0)
        {
            // Sony layout over HID: 0 □, 1 ✕, 2 ○, 3 △, 4 L1, 5 R1, 8 Share/Create, 9 Options; axes 0/1 = left stick; switch 0 = D-pad
            var c = RawGameController.RawGameControllers[0];
            var buttons = new bool[c.ButtonCount]; var switches = new GameControllerSwitchPosition[c.SwitchCount]; var axes = new double[c.AxisCount];
            c.GetCurrentReading(buttons, switches, axes);
            bool Btn(int i) => i < buttons.Length && buttons[i];
            var sw = switches.Length > 0 ? switches[0] : GameControllerSwitchPosition.Center;
            bool Has(params GameControllerSwitchPosition[] p) => p.Contains(sw);
            ControllerHub.ControllerConnected = true;
            ControllerHub.Pad(new PadSnapshot(
                axes.Length > 0 ? (float)(axes[0] * 2 - 1) : 0, axes.Length > 1 ? (float)(axes[1] * 2 - 1) : 0,
                Has(GameControllerSwitchPosition.Up, GameControllerSwitchPosition.UpLeft, GameControllerSwitchPosition.UpRight),
                Has(GameControllerSwitchPosition.Down, GameControllerSwitchPosition.DownLeft, GameControllerSwitchPosition.DownRight),
                Has(GameControllerSwitchPosition.Left, GameControllerSwitchPosition.UpLeft, GameControllerSwitchPosition.DownLeft),
                Has(GameControllerSwitchPosition.Right, GameControllerSwitchPosition.UpRight, GameControllerSwitchPosition.DownRight),
                Btn(1), Btn(2), Btn(0), Btn(3), Btn(9), Btn(8), Btn(4), Btn(5)));
            return;
        }
        ControllerHub.ControllerConnected = false;
    }
}
