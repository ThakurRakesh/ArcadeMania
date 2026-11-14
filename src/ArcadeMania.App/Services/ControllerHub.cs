using ArcadeMania.Core.Engine;
using ArcadeMania.Core.Security;

namespace ArcadeMania.App.Services;

public enum NavCommand { Up, Down, Left, Right, Confirm, Back, Start }

/// <summary>Single entry point for every input device: Xbox / PlayStation / generic gamepads, keyboard and on-screen buttons.
/// Games read <see cref="GameInput"/>; menus listen to <see cref="Nav"/>.</summary>
public static class ControllerHub
{
    public static InputState GameInput { get; } = new();
    public static event Action<NavCommand>? Nav;
    public static bool GameActive { get; set; }
    public static bool ControllerConnected { get; set; }

    static readonly GamepadMapper Mapper = new();
    static readonly BotGuard Guard = new();
    static PadSnapshot _pad;
    static readonly object Gate = new();

    // ---------- keyboard / on-screen buttons ----------
    public static void Key(GameKey key, bool down)
    {
        var wasDown = GameInput.Held(key);
        if (down && !wasDown && !Guard.AllowPress(Environment.TickCount64)) return;   // bot / macro flood
        GameInput.Set(key, down);
        if (down && !wasDown) RaiseFor(key);
    }

    public static void Back() => Raise(NavCommand.Back);

    // ---------- gamepads ----------
    public static void Pad(PadSnapshot s)
    {
        lock (Gate)
        {
            Span<bool> before = stackalloc bool[7];
            for (int k = 0; k < 7; k++) before[k] = GameInput.Held((GameKey)k);
            Mapper.Apply(s, GameInput);
            _pad = s;
            for (int k = 0; k < 7; k++) if (!before[k] && GameInput.Held((GameKey)k)) RaiseFor((GameKey)k);
            if (Mapper.BackPressed) Raise(NavCommand.Back);
        }
    }

    /// <summary>Android sends buttons and axes as separate events; merge them here.</summary>
    public static void PadButton(Func<PadSnapshot, PadSnapshot> change) { lock (Gate) Pad(change(_pad)); }

    static void RaiseFor(GameKey k) => Raise(k switch
    {
        GameKey.Up => NavCommand.Up, GameKey.Down => NavCommand.Down, GameKey.Left => NavCommand.Left,
        GameKey.Right => NavCommand.Right, GameKey.A => NavCommand.Confirm, GameKey.Start => NavCommand.Start,
        _ => GameActive ? NavCommand.Confirm : NavCommand.Back   // B = back in menus
    }, k == GameKey.B && GameActive);

    static void Raise(NavCommand cmd, bool suppress = false)
    {
        if (suppress) return;   // in-game, B is an action button, not navigation
        MainThread.BeginInvokeOnMainThread(() => { try { Nav?.Invoke(cmd); } catch { /* never crash on input */ } });
    }
}
