using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using ArcadeMania.App.Services;
using ArcadeMania.Core.Engine;

namespace ArcadeMania.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.FullSensor,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
[IntentFilter([Android.Content.Intent.ActionMain], Categories = ["android.intent.category.LEANBACK_LAUNCHER"])]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>Xbox, PlayStation (DualShock 4 / DualSense) and generic Bluetooth/USB controllers, plus hardware keyboards.</summary>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        try
        {
            if (e is null || e.KeyCode == Keycode.Back) return base.DispatchKeyEvent(e);   // MAUI → page OnBackButtonPressed
            var down = e.Action == KeyEventActions.Down;
            if (down && e.RepeatCount > 0) return true;
            var fromPad = (e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad || (e.Source & InputSourceType.Dpad) == InputSourceType.Dpad;

            switch (e.KeyCode)
            {
                case Keycode.ButtonA: ControllerHub.PadButton(p => p with { South = down }); return true;    // ✕ / A
                case Keycode.ButtonB: ControllerHub.PadButton(p => p with { East = down }); return true;     // ○ / B
                case Keycode.ButtonX: ControllerHub.PadButton(p => p with { West = down }); return true;     // □ / X
                case Keycode.ButtonY: ControllerHub.PadButton(p => p with { North = down }); return true;    // △ / Y
                case Keycode.ButtonL1: ControllerHub.PadButton(p => p with { LeftShoulder = down }); return true;
                case Keycode.ButtonR1: ControllerHub.PadButton(p => p with { RightShoulder = down }); return true;
                case Keycode.ButtonStart: ControllerHub.PadButton(p => p with { Start = down }); return true;
                case Keycode.ButtonSelect: ControllerHub.PadButton(p => p with { Back = down }); return true;
                case Keycode.DpadUp when fromPad || ControllerHub.GameActive: ControllerHub.PadButton(p => p with { DUp = down }); return true;
                case Keycode.DpadDown when fromPad || ControllerHub.GameActive: ControllerHub.PadButton(p => p with { DDown = down }); return true;
                case Keycode.DpadLeft when fromPad || ControllerHub.GameActive: ControllerHub.PadButton(p => p with { DLeft = down }); return true;
                case Keycode.DpadRight when fromPad || ControllerHub.GameActive: ControllerHub.PadButton(p => p with { DRight = down }); return true;
                case Keycode.DpadCenter when fromPad: ControllerHub.PadButton(p => p with { South = down }); return true;
            }

            if (ControllerHub.GameActive)   // hardware keyboard in-game only; menus keep normal typing
            {
                GameKey? k = e.KeyCode switch
                {
                    Keycode.W => GameKey.Up, Keycode.S => GameKey.Down, Keycode.A => GameKey.Left, Keycode.D => GameKey.Right,
                    Keycode.Z or Keycode.J or Keycode.Space => GameKey.A, Keycode.X or Keycode.K => GameKey.B,
                    Keycode.Enter or Keycode.P => GameKey.Start, _ => null
                };
                if (k is not null) { ControllerHub.Key(k.Value, down); return true; }
            }
        }
        catch { /* never crash on input */ }
        return base.DispatchKeyEvent(e);
    }

    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        try
        {
            if (e is not null && (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick && e.Action == MotionEventActions.Move)
            {
                float x = e.GetAxisValue(Axis.X), y = e.GetAxisValue(Axis.Y);
                float hx = e.GetAxisValue(Axis.HatX), hy = e.GetAxisValue(Axis.HatY);
                ControllerHub.ControllerConnected = true;
                ControllerHub.PadButton(p => p with { LeftX = x, LeftY = y, DLeft = hx < -0.5f, DRight = hx > 0.5f, DUp = hy < -0.5f, DDown = hy > 0.5f });
                return true;
            }
        }
        catch { }
        return base.DispatchGenericMotionEvent(e);
    }
}
