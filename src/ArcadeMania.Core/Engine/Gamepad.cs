namespace ArcadeMania.Core.Engine;

/// <summary>Platform-neutral controller snapshot. Platform code (Windows.Gaming.Input / Android InputDevice)
/// fills this for Xbox, PlayStation (DualShock 4 / DualSense) and generic HID pads.</summary>
public record struct PadSnapshot(
    float LeftX, float LeftY,               // -1..1, +Y = down
    bool DUp, bool DDown, bool DLeft, bool DRight,
    bool South, bool East, bool West, bool North, // Xbox A/B/X/Y  ==  PlayStation ✕/○/□/△
    bool Start, bool Back, bool LeftShoulder, bool RightShoulder);

/// <summary>Maps a pad snapshot onto game keys. South/West → A (confirm/attack), East/North → B.
/// Back (View / Share / Create) is reserved for navigating back, never exits the app.</summary>
public sealed class GamepadMapper
{
    public const float DeadZone = 0.35f;
    private PadSnapshot _prev;

    public bool BackPressed { get; private set; }
    public bool StartPressed { get; private set; }

    public void Apply(in PadSnapshot s, InputState input)
    {
        input.Set(GameKey.Left, s.DLeft || s.LeftX < -DeadZone);
        input.Set(GameKey.Right, s.DRight || s.LeftX > DeadZone);
        input.Set(GameKey.Up, s.DUp || s.LeftY < -DeadZone);
        input.Set(GameKey.Down, s.DDown || s.LeftY > DeadZone);
        input.Set(GameKey.A, s.South || s.West || s.RightShoulder);
        input.Set(GameKey.B, s.East || s.North || s.LeftShoulder);
        input.Set(GameKey.Start, s.Start);
        BackPressed = s.Back && !_prev.Back;
        StartPressed = s.Start && !_prev.Start;
        _prev = s;
    }
}
