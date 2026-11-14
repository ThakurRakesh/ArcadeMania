using Microsoft.Maui.Graphics;

namespace ArcadeMania.Core.Engine;

public enum GameCategory { Arcade, Simulation, Fighting }

public enum GameKey { Left, Right, Up, Down, A, B, Start }

/// <summary>Unified input from touch buttons, keyboard, pointer and gamepads (Xbox / PlayStation / generic).</summary>
public sealed class InputState
{
    private readonly bool[] _held = new bool[7];
    private readonly bool[] _tap = new bool[7];
    private readonly object _gate = new();

    public float PointerX, PointerY;      // world coordinates
    public bool PointerDown;
    public bool PointerTap;

    public bool Held(GameKey k) { lock (_gate) return _held[(int)k]; }
    public bool Tapped(GameKey k) { lock (_gate) return _tap[(int)k]; }

    public bool Left => Held(GameKey.Left);
    public bool Right => Held(GameKey.Right);
    public bool Up => Held(GameKey.Up);
    public bool Down => Held(GameKey.Down);
    public bool A => Held(GameKey.A);
    public bool B => Held(GameKey.B);

    public void Set(GameKey k, bool down)
    {
        lock (_gate)
        {
            var i = (int)k;
            if (down && !_held[i]) _tap[i] = true;
            _held[i] = down;
        }
    }

    /// <summary>Called by the host after every Update.</summary>
    public void EndFrame()
    {
        lock (_gate) Array.Clear(_tap);
        PointerTap = false;
    }

    public void ReleaseAll()
    {
        lock (_gate) { Array.Clear(_held); Array.Clear(_tap); }
        PointerDown = false; PointerTap = false;
    }
}

/// <summary>Colour palette generated from a hue so every variant looks different.</summary>
public sealed record Palette(Color Background, Color Background2, Color Primary, Color Secondary, Color Accent, Color Danger, Color Text)
{
    public static Palette FromHue(float hue, bool dark)
    {
        float H(float o) => (hue + o) % 1f;
        return dark
            ? new Palette(Color.FromHsla(H(0), .35f, .07f), Color.FromHsla(H(0), .30f, .13f),
                Color.FromHsla(H(0), .80f, .60f), Color.FromHsla(H(.33f), .75f, .58f),
                Color.FromHsla(H(.12f), .90f, .65f), Color.FromHsla(0f, .85f, .58f), Color.FromRgb(237, 228, 213))
            : new Palette(Color.FromHsla(H(0), .25f, .94f), Color.FromHsla(H(0), .25f, .86f),
                Color.FromHsla(H(0), .55f, .42f), Color.FromHsla(H(.33f), .55f, .40f),
                Color.FromHsla(H(.12f), .70f, .45f), Color.FromHsla(0f, .70f, .48f), Color.FromRgb(61, 80, 97));
    }
}

/// <summary>Deterministic parameters that turn one base game into thousands of distinct variants.</summary>
public sealed record VariantSpec(int Seed, float Speed, int Difficulty, int Mode, float Hue, string Flavor)
{
    private static readonly string[] Adjectives =
    [
        "Neon", "Turbo", "Retro", "Cosmic", "Crimson", "Frost", "Shadow", "Golden", "Hyper", "Pixel",
        "Storm", "Jade", "Solar", "Lunar", "Thunder", "Velvet", "Iron", "Crystal", "Ember", "Quantum",
        "Midnight", "Savage", "Arctic", "Desert", "Jungle", "Ocean", "Volcanic", "Mystic", "Atomic", "Royal"
    ];
    private static readonly string[] Nouns =
        ["Rush", "Classic", "Blitz", "Nights", "Zero", "Prime", "Legends", "Fury", "Odyssey", "Zen",
         "Arena", "Circuit", "Frontier", "Overdrive", "Revival", "Mania", "Saga", "Showdown", "Dash", "Echo"];

    public static VariantSpec Create(int seed)
    {
        var r = new Random(unchecked(seed * 7919 + 17));
        var speed = 0.75f + (float)r.NextDouble() * 0.9f;          // 0.75x – 1.65x
        var difficulty = 1 + r.Next(10);                            // 1 – 10
        var mode = r.Next(4);                                       // game-specific rule switch
        var hue = (float)r.NextDouble();
        var flavor = $"{Adjectives[r.Next(Adjectives.Length)]} {Nouns[r.Next(Nouns.Length)]}";
        return new VariantSpec(seed, speed, difficulty, mode, hue, flavor);
    }
}

/// <summary>Base class for every game. Games draw into a fixed 1600×900 virtual world;
/// the host scales it as vectors to the device's native resolution (crisp at 4K / 2160p and beyond).</summary>
public abstract class GameBase
{
    public const float W = 1600f, H = 900f;

    public VariantSpec V { get; private set; } = VariantSpec.Create(0);
    public Palette P { get; private set; } = Palette.FromHue(0, true);
    protected Random Rng { get; private set; } = new(0);

    public int Score { get; protected set; }
    public bool GameOver { get; protected set; }
    public string Status { get; protected set; } = "";
    public abstract string ControlsHint { get; }

    public void Init(VariantSpec v, bool dark = true)
    {
        V = v;
        P = Palette.FromHue(v.Hue, dark);
        Restart();
    }

    public void Restart()
    {
        Rng = new Random(V.Seed);
        Score = 0; GameOver = false; Status = "";
        Reset();
    }

    protected abstract void Reset();
    protected abstract void Tick(float dt, InputState input);
    protected abstract void Render(ICanvas c);

    public void Update(float dt, InputState input)
    {
        dt = Math.Clamp(dt, 0f, 0.05f);       // avoid tunnelling after a stall
        if (!GameOver) Tick(dt, input);
    }

    public void Draw(ICanvas c)
    {
        c.FillColor = P.Background;
        c.FillRectangle(0, 0, W, H);
        Render(c);
    }

    // ---------- drawing helpers ----------
    protected static void Text(ICanvas c, string s, float x, float y, float size, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left, bool bold = true)
    {
        c.FontColor = color;
        c.FontSize = size;
        c.Font = bold ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
        var w = 1400f;
        var left = align switch { HorizontalAlignment.Center => x - w / 2, HorizontalAlignment.Right => x - w, _ => x };
        c.DrawString(s, left, y - size, w, size * 1.6f, align, VerticalAlignment.Top);
    }

    protected static void Glow(ICanvas c, Color color, float blur = 18f) => c.SetShadow(new SizeF(0, 0), blur, color);
    protected static void NoGlow(ICanvas c) => c.SetShadow(SizeF.Zero, 0, Colors.Transparent);

    protected static bool Hit(RectF a, RectF b) => a.IntersectsWith(b);
    protected float Rand(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
}
