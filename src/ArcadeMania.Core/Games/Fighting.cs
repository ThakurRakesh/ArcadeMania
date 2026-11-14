using ArcadeMania.Core.Engine;
using Microsoft.Maui.Graphics;

namespace ArcadeMania.Core.Games;

public enum FightStyle { Brawler, Karate, Boxer, Ninja, Swordsman, Spearman, Samurai, Knight }

/// <summary>1-v-1 versus fighter engine: walk, jump, block, light and heavy attacks, rounds, CPU opponent.</summary>
public abstract class FightGameBase : GameBase
{
    protected enum St { Idle, Walk, Jump, Light, Heavy, Block, Hit, KO }

    protected sealed class Fighter
    {
        public float X, Y, Vx, Vy, Hp = 100, Timer, Cooldown; public int Face = 1; public St State; public bool Dealt;
        public bool Grounded => Y >= Ground - 0.5f;
        public Color Color = Colors.White;
    }

    protected const float Ground = 760f;
    protected Fighter Me = new(), Cpu = new();
    protected FightStyle Style;
    int _myWins, _cpuWins, _round; float _clock, _roundPause, _aiThink;
    protected abstract FightStyle[] Styles { get; }
    protected virtual float Reach => Style switch { FightStyle.Swordsman or FightStyle.Samurai => 150, FightStyle.Spearman => 190, FightStyle.Knight => 140, FightStyle.Boxer => 95, _ => 110 };
    protected virtual float Power => Style switch { FightStyle.Knight or FightStyle.Samurai => 1.3f, FightStyle.Ninja => 0.8f, _ => 1f };
    protected virtual float Agility => Style switch { FightStyle.Ninja => 1.35f, FightStyle.Knight => 0.75f, FightStyle.Boxer => 1.15f, _ => 1f };
    protected bool Armed => Style >= FightStyle.Swordsman;

    public override string ControlsHint => "Move/jump: D-pad • Down: block • A: light attack • B: heavy attack";

    protected override void Reset()
    {
        Style = Styles[(V.Seed / 3 + V.Mode) % Styles.Length];
        _myWins = _cpuWins = 0; _round = 1; NewRound();
    }

    void NewRound()
    {
        Me = new Fighter { X = 450, Y = Ground, Face = 1, Color = P.Primary };
        Cpu = new Fighter { X = W - 450, Y = Ground, Face = -1, Color = P.Secondary };
        _clock = 60; _roundPause = 1.2f;
    }

    protected override void Tick(float dt, InputState i)
    {
        if (_roundPause > 0)
        {
            _roundPause -= dt;
            if (_roundPause <= 0 && (Me.State == St.KO || Cpu.State == St.KO || _clock <= 0)) EndRound();
            if (_roundPause > 0) { Status = RoundText(); Physics(Me, dt); Physics(Cpu, dt); return; }
        }

        _clock -= dt;
        Control(Me, i.Left, i.Right, i.Tapped(GameKey.Up), i.Down, i.Tapped(GameKey.A), i.Tapped(GameKey.B));
        Ai(dt);
        foreach (var f in new[] { Me, Cpu }) { Animate(f, dt); Physics(f, dt); }
        Me.Face = Cpu.X > Me.X ? 1 : -1; Cpu.Face = -Me.Face;
        Resolve(Me, Cpu); Resolve(Cpu, Me);
        if (MathF.Abs(Me.X - Cpu.X) < 70 && MathF.Abs(Me.Y - Cpu.Y) < 100) { var push = (70 - MathF.Abs(Me.X - Cpu.X)) / 2 * Me.Face; Me.X -= push; Cpu.X += push; }

        if (Me.Hp <= 0 || Cpu.Hp <= 0 || _clock <= 0)
        {
            if (Me.Hp <= 0) Me.State = St.KO; if (Cpu.Hp <= 0) Cpu.State = St.KO;
            _roundPause = 2f;
        }
        Status = RoundText();
    }

    string RoundText() => $"{Style} • Round {_round} • You {_myWins} - {_cpuWins} CPU • {Math.Max(0, _clock):0}s";

    void EndRound()
    {
        if (Me.Hp >= Cpu.Hp) { _myWins++; Score += (int)(Me.Hp * 10 * V.Difficulty) + 500; } else _cpuWins++;
        if (_myWins == 2 || _cpuWins == 2) { GameOver = true; Status = _myWins == 2 ? "VICTORY!" : "DEFEATED"; Score += _myWins == 2 ? 3000 : 0; return; }
        _round++; NewRound();
    }

    void Control(Fighter f, bool left, bool right, bool jump, bool block, bool light, bool heavy)
    {
        if (f.State is St.Hit or St.KO or St.Light or St.Heavy) return;
        var sp = 380 * Agility * Math.Min(1.25f, V.Speed);
        f.Vx = left ? -sp : right ? sp : 0;
        if (block && f.Grounded) { f.State = St.Block; f.Vx = 0; return; }
        if (jump && f.Grounded) { f.Vy = -900; f.State = St.Jump; }
        if (light && f.Cooldown <= 0) Attack(f, St.Light, 0.28f);
        else if (heavy && f.Cooldown <= 0) Attack(f, St.Heavy, 0.5f);
        else if (f.Grounded && f.State != St.Light) f.State = f.Vx != 0 ? St.Walk : St.Idle;
    }

    void Attack(Fighter f, St kind, float t) { f.State = kind; f.Timer = t / Agility; f.Cooldown = t / Agility + 0.12f; f.Dealt = false; if (f.Grounded) f.Vx = 0; }

    void Ai(float dt)
    {
        if ((_aiThink -= dt) > 0) { Control(Cpu, _aiL, _aiR, false, _aiBlock, false, false); return; }
        _aiThink = Math.Max(0.06f, 0.4f - V.Difficulty * 0.033f);
        var dist = MathF.Abs(Cpu.X - Me.X); var toward = Me.X < Cpu.X;
        var aggro = 0.25f + V.Difficulty * 0.06f;
        _aiBlock = (Me.State is St.Light or St.Heavy) && dist < Reach + 40 && Rng.NextDouble() < V.Difficulty * 0.07;
        _aiL = _aiR = false;
        bool atk = false, hv = false, jump = false;
        if (dist > Reach * 0.85f) { _aiL = toward; _aiR = !toward; jump = Rng.NextDouble() < 0.05; }
        else if (Rng.NextDouble() < aggro) { atk = true; hv = Rng.NextDouble() < 0.35; }
        else if (Rng.NextDouble() < 0.3) { _aiL = !toward; _aiR = toward; }
        Control(Cpu, _aiL, _aiR, jump, _aiBlock, atk && !hv, atk && hv);
    }
    bool _aiL, _aiR, _aiBlock;

    void Animate(Fighter f, float dt)
    {
        f.Cooldown -= dt;
        if (f.State is St.Light or St.Heavy or St.Hit) { if ((f.Timer -= dt) <= 0) f.State = f.Grounded ? St.Idle : St.Jump; }
    }

    static void Physics(Fighter f, float dt)
    {
        f.Vy += 2400 * dt; f.X += f.Vx * dt; f.Y += f.Vy * dt;
        if (f.Y >= Ground) { f.Y = Ground; f.Vy = 0; if (f.State == St.Jump) f.State = St.Idle; }
        f.X = Math.Clamp(f.X, 80, W - 80);
        if (f.State == St.KO) f.Vx *= 0.9f;
    }

    void Resolve(Fighter a, Fighter d)
    {
        if (a.State is not (St.Light or St.Heavy) || a.Dealt) return;
        var total = (a.State == St.Light ? 0.28f : 0.5f) / Agility;
        if (a.Timer > total * 0.6f) return;                           // wind-up frames
        var range = Reach * (a.State == St.Heavy ? 1.15f : 1f);
        var dx = (d.X - a.X) * a.Face;
        if (dx < 0 || dx > range || MathF.Abs(a.Y - d.Y) > 120) return;
        a.Dealt = true;
        var dmg = (a.State == St.Light ? 6f : 13f) * Power * (a == Cpu ? 0.7f + V.Difficulty * 0.06f : 1f);
        if (d.State == St.Block && d.Face == -a.Face) { dmg *= 0.15f; d.X += a.Face * 25; }
        else { d.State = St.Hit; d.Timer = 0.25f; d.Vx = a.Face * 260; if (a.State == St.Heavy) d.Vy = -400; }
        d.Hp = Math.Max(0, d.Hp - dmg);
        if (a == Me) Score += (int)dmg * 10;
    }

    protected override void Render(ICanvas c)
    {
        // arena backdrop
        c.FillColor = P.Background2; c.FillRectangle(0, 0, W, Ground - 200);
        for (int k = 0; k < 9; k++) { c.FillColor = P.Background.WithAlpha(.6f); c.FillRectangle(k * 190 + 20, 260 - (k * 53 % 140), 140, Ground); }
        c.FillColor = P.Primary.WithLuminosity(.18f); c.FillRectangle(0, Ground + 40, W, H);
        c.StrokeColor = P.Accent.WithAlpha(.5f); c.StrokeSize = 3; c.DrawLine(0, Ground + 40, W, Ground + 40);

        DrawFighter(c, Me); DrawFighter(c, Cpu);
        Bar(c, 60, Me.Hp, P.Primary, false); Bar(c, W - 660, Cpu.Hp, P.Secondary, true);
        Text(c, $"{Math.Max(0, _clock):0}", W / 2, 90, 64, P.Text, HorizontalAlignment.Center);
        if (_roundPause > 0 && !GameOver)
            Text(c, Me.State == St.KO || Cpu.State == St.KO ? "K.O.!" : $"ROUND {_round} — FIGHT!", W / 2, H / 2 - 60, 90, P.Accent, HorizontalAlignment.Center);
    }

    void Bar(ICanvas c, float x, float hp, Color col, bool rtl)
    {
        c.FillColor = Colors.Black.WithAlpha(.5f); c.FillRoundedRectangle(x, 40, 600, 34, 17);
        c.FillColor = hp > 30 ? col : P.Danger;
        var w = 6 * hp; c.FillRoundedRectangle(rtl ? x + 600 - w : x, 40, w, 34, 17);
    }

    void DrawFighter(ICanvas c, Fighter f)
    {
        float x = f.X, y = f.Y, s = f.Face;
        var ko = f.State == St.KO;
        c.SaveState();
        if (ko) c.Rotate(-80 * s, x, y);
        Glow(c, f.Color, 16);
        c.StrokeColor = f.Color; c.StrokeLineCap = LineCap.Round; c.StrokeSize = 22;
        var bob = f.State == St.Walk ? MathF.Sin((float)(DateTime.UtcNow.TimeOfDay.TotalSeconds * 14)) * 18 : 0;
        float hipY = y - 120, shY = y - 240;
        // legs
        if (f.State == St.Heavy && !Armed) { c.DrawLine(x, hipY, x - 30 * s, y); c.DrawLine(x, hipY, x + 140 * s, hipY - 30); }
        else { c.DrawLine(x, hipY, x - 35 * s + bob, y); c.DrawLine(x, hipY, x + 35 * s - bob, y); }
        // torso + head
        c.StrokeSize = 30; c.DrawLine(x, hipY, x + 6 * s, shY);
        c.FillColor = f.Color; c.FillCircle(x + 8 * s, shY - 48, 36);
        c.FillColor = P.Background; c.FillRoundedRectangle(x + (s > 0 ? 6 : -40), shY - 60, 34, 10, 5);
        // arms
        c.StrokeSize = 18;
        float handX, handY;
        switch (f.State)
        {
            case St.Light: handX = x + (Armed ? 70 : 110) * s; handY = shY + 10; break;
            case St.Heavy when Armed: handX = x + 80 * s; handY = shY - 40; break;
            case St.Block: handX = x + 45 * s; handY = shY - 30; break;
            default: handX = x + 50 * s; handY = shY + 50; break;
        }
        c.DrawLine(x + 6 * s, shY + 10, handX, handY);
        c.DrawLine(x + 6 * s, shY + 10, x - 30 * s, shY + 70);
        if (Style == FightStyle.Boxer) { c.FillColor = P.Danger; c.FillCircle(handX, handY, 20); }
        if (Armed)
        {
            c.StrokeColor = Colors.Silver; c.StrokeSize = Style == FightStyle.Spearman ? 8 : 10;
            var len = Reach - 30; var ang = f.State switch { St.Light => 0f, St.Heavy => -0.9f + Math.Clamp(1 - f.Timer * 3, 0, 1) * 1.4f, St.Block => -1.5f, _ => -0.5f };
            c.DrawLine(handX, handY, handX + MathF.Cos(ang) * len * s, handY + MathF.Sin(ang) * len);
        }
        c.RestoreState(); NoGlow(c);
        if (f.State == St.Hit) { c.FillColor = Colors.White.WithAlpha(.8f); c.FillCircle(x, y - 200, 30); }
    }
}

public sealed class StreetBrawlGame : FightGameBase
{
    protected override FightStyle[] Styles => [FightStyle.Brawler, FightStyle.Karate, FightStyle.Boxer, FightStyle.Ninja];
}

public sealed class BladeDuelGame : FightGameBase
{
    protected override FightStyle[] Styles => [FightStyle.Swordsman, FightStyle.Spearman, FightStyle.Samurai, FightStyle.Knight];
}
