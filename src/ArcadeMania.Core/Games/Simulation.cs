using ArcadeMania.Core.Engine;
using Microsoft.Maui.Graphics;

namespace ArcadeMania.Core.Games;

// ======================= LIFE LAB (cellular automata) =======================
public sealed class LifeLabGame : GameBase
{
    const int Cols = 80, Rows = 45; const float Cell = 20f;
    static readonly (string name, int[] birth, int[] survive)[] Rules =
    [
        ("Conway B3/S23", [3], [2, 3]), ("HighLife B36/S23", [3, 6], [2, 3]),
        ("Day & Night", [3, 6, 7, 8], [3, 4, 6, 7, 8]), ("Maze B3/S12345", [3], [1, 2, 3, 4, 5]),
        ("Coral B3/S45678", [3], [4, 5, 6, 7, 8]), ("2x2 B36/S125", [3, 6], [1, 2, 5]),
        ("Amoeba B357/S1358", [3, 5, 7], [1, 3, 5, 8]), ("Seeds B2/S", [2], [])
    ];
    bool[,] _g = new bool[Rows, Cols], _n = new bool[Rows, Cols];
    float _acc; bool _paused; int _gen; (string name, int[] birth, int[] survive) _rule;
    public override string ControlsHint => "Drag/tap: paint cells • A: pause • B: randomize • Left/Right: rule";

    protected override void Reset()
    {
        _rule = Rules[(V.Seed + V.Mode) % Rules.Length];
        Randomize(); _paused = false;
    }

    void Randomize()
    {
        _gen = 0;
        var density = 0.12 + V.Difficulty * 0.025;
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) _g[r, c] = Rng.NextDouble() < density;
    }

    protected override void Tick(float dt, InputState i)
    {
        if (i.Tapped(GameKey.A)) _paused = !_paused;
        if (i.Tapped(GameKey.B)) Randomize();
        if (i.Tapped(GameKey.Right) || i.Tapped(GameKey.Left))
        {
            var idx = Array.FindIndex(Rules, r => r.name == _rule.name);
            idx = (idx + (i.Tapped(GameKey.Right) ? 1 : Rules.Length - 1)) % Rules.Length;
            _rule = Rules[idx];
        }
        if (i.PointerDown)
        {
            int c = (int)(i.PointerX / Cell), r = (int)(i.PointerY / Cell);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                if (r + dy is >= 0 and < Rows && c + dx is >= 0 and < Cols) _g[r + dy, c + dx] = true;
        }
        Status = $"{_rule.name} • Gen {_gen}{(_paused ? " • PAUSED" : "")}";
        if (_paused || (_acc += dt) < 0.09f / V.Speed) return;
        _acc = 0; _gen++; Score = _gen;
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
        {
            int n = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                if ((dx | dy) != 0 && _g[(r + dy + Rows) % Rows, (c + dx + Cols) % Cols]) n++;
            _n[r, c] = _g[r, c] ? Array.IndexOf(_rule.survive, n) >= 0 : Array.IndexOf(_rule.birth, n) >= 0;
        }
        (_g, _n) = (_n, _g);
    }

    protected override void Render(ICanvas c)
    {
        Glow(c, P.Primary, 8);
        for (int r = 0; r < Rows; r++) for (int col = 0; col < Cols; col++)
            if (_g[r, col])
            {
                c.FillColor = Color.FromHsla((V.Hue + (r + col) / 250f) % 1f, .8f, .6f);
                c.FillRoundedRectangle(col * Cell + 1.5f, r * Cell + 1.5f, Cell - 3, Cell - 3, 5);
            }
        NoGlow(c);
    }
}

// ======================= LUNAR LANDER (physics sim) =======================
public sealed class LanderGame : GameBase
{
    float _x, _y, _vx, _vy, _ang, _fuel; int _level; bool _thrust;
    readonly List<PointF> _terrain = new(); float _padX, _padW;
    float Gravity => 60 + V.Difficulty * 9 + V.Mode * 14;
    public override string ControlsHint => "Up/A: thrust • Left/Right: rotate • land softly on the pad";

    protected override void Reset() { _level = 1; NewLevel(); }

    void NewLevel()
    {
        _x = 200; _y = 120; _vx = 70; _vy = 0; _ang = 0; _fuel = 100;
        _terrain.Clear();
        _padW = Math.Max(120, 260 - _level * 20 - V.Difficulty * 8);
        _padX = Rand(500, W - _padW - 100);
        float y = H - 140;
        for (float x = 0; x <= W; x += 40)
        {
            if (x >= _padX && x <= _padX + _padW) { _terrain.Add(new PointF(x, H - 120)); continue; }
            y = Math.Clamp(y + Rand(-70, 70), H - 320, H - 40); _terrain.Add(new PointF(x, y));
        }
    }

    float GroundAt(float x)
    {
        for (int k = 1; k < _terrain.Count; k++)
            if (_terrain[k].X >= x)
            { var a = _terrain[k - 1]; var b = _terrain[k]; return a.Y + (b.Y - a.Y) * (x - a.X) / Math.Max(1, b.X - a.X); }
        return H;
    }

    protected override void Tick(float dt, InputState i)
    {
        if (i.Left) _ang -= 120 * dt; if (i.Right) _ang += 120 * dt;
        _ang = Math.Clamp(_ang, -90, 90);
        _thrust = (i.Up || i.A || i.PointerDown) && _fuel > 0;
        if (_thrust)
        {
            var rad = _ang * MathF.PI / 180;
            _vx += MathF.Sin(rad) * 260 * dt; _vy -= MathF.Cos(rad) * 260 * dt;
            _fuel -= 14 * dt;
        }
        _vy += Gravity * dt * V.Speed; _x += _vx * dt; _y += _vy * dt;
        _x = (_x + W) % W;
        if (_y + 24 >= GroundAt(_x))
        {
            bool onPad = _x > _padX + 15 && _x < _padX + _padW - 15;
            if (onPad && _vy < 95 && MathF.Abs(_vx) < 60 && MathF.Abs(_ang) < 14)
            { Score += (int)(500 + _fuel * 10) * V.Difficulty; _level++; NewLevel(); }
            else { GameOver = true; Status = "Crashed!"; return; }
        }
        Status = $"Lvl {_level} • Fuel {_fuel:0}% • V {_vy:0} • H {_vx:0}";
    }

    protected override void Render(ICanvas c)
    {
        for (int k = 0; k < 60; k++) { c.FillColor = P.Text.WithAlpha(.4f); c.FillCircle(k * 977 % W, k * 431 % (H / 2), 1.5f + k % 3); }
        var p = new PathF(); p.MoveTo(0, H);
        foreach (var t in _terrain) p.LineTo(t); p.LineTo(W, H); p.Close();
        c.FillColor = P.Background2; c.FillPath(p);
        c.StrokeColor = P.Secondary; c.StrokeSize = 4; c.DrawPath(p);
        Glow(c, P.Accent); c.FillColor = P.Accent; c.FillRoundedRectangle(_padX, H - 124, _padW, 10, 5); NoGlow(c);

        c.SaveState(); c.Translate(_x, _y); c.Rotate(_ang);
        if (_thrust) { Glow(c, P.Danger, 24); c.FillColor = P.Danger; c.FillEllipse(-10, 18, 20, 30 + Rng.Next(20)); }
        Glow(c, P.Primary, 12);
        c.FillColor = P.Primary; c.FillEllipse(-22, -28, 44, 40);
        c.StrokeColor = P.Primary; c.StrokeSize = 5; c.DrawLine(-14, 8, -26, 26); c.DrawLine(14, 8, 26, 26);
        c.FillColor = Colors.White.WithAlpha(.8f); c.FillCircle(0, -10, 8);
        c.RestoreState(); NoGlow(c);

        c.FillColor = P.Text.WithAlpha(.2f); c.FillRoundedRectangle(W - 260, 40, 200, 18, 9);
        c.FillColor = _fuel > 25 ? P.Secondary : P.Danger; c.FillRoundedRectangle(W - 260, 40, 2 * Math.Max(0, _fuel), 18, 9);
    }
}

// ======================= TINY FARM (farming / economy sim) =======================
public sealed class TinyFarmGame : GameBase
{
    const int Cols = 6, Rows = 4; const float PW = 170, PHh = 150;
    readonly float[] _growth = new float[Cols * Rows]; readonly bool[] _planted = new bool[Cols * Rows];
    readonly bool[] _owned = new bool[Cols * Rows];
    int _coins, _cx, _cy, _day; float _clock, _rain;
    static readonly string[] Crops = ["Wheat", "Carrot", "Tomato", "Pumpkin"];
    string Crop => Crops[V.Mode];
    int SeedCost => 5 + V.Mode * 4; int SellPrice => 14 + V.Mode * 11; int PlotCost => 60 + V.Difficulty * 10;
    float Ox => (W - Cols * PW) / 2 - 200; const float Oy = 150;
    public override string ControlsHint => "Tap a plot / D-pad + A: plant, harvest or buy land • B: harvest all";

    protected override void Reset()
    {
        Array.Clear(_growth); Array.Clear(_planted); Array.Clear(_owned);
        for (int k = 0; k < 6; k++) _owned[k] = true;
        _coins = 40; _cx = _cy = 0; _day = 1; _clock = 0.25f; _rain = 0;
    }

    void Act(int idx)
    {
        if (!_owned[idx]) { if (_coins >= PlotCost) { _coins -= PlotCost; _owned[idx] = true; } return; }
        if (!_planted[idx]) { if (_coins >= SeedCost) { _coins -= SeedCost; _planted[idx] = true; _growth[idx] = 0; } return; }
        if (_growth[idx] >= 1) { _coins += SellPrice; _planted[idx] = false; }
    }

    protected override void Tick(float dt, InputState i)
    {
        if (i.Tapped(GameKey.Left)) _cx = (_cx + Cols - 1) % Cols;
        if (i.Tapped(GameKey.Right)) _cx = (_cx + 1) % Cols;
        if (i.Tapped(GameKey.Up)) _cy = (_cy + Rows - 1) % Rows;
        if (i.Tapped(GameKey.Down)) _cy = (_cy + 1) % Rows;
        if (i.Tapped(GameKey.A)) Act(_cy * Cols + _cx);
        if (i.Tapped(GameKey.B)) for (int k = 0; k < _growth.Length; k++) if (_planted[k] && _growth[k] >= 1) Act(k);
        if (i.PointerTap)
        {
            int c = (int)((i.PointerX - Ox) / PW), r = (int)((i.PointerY - Oy) / PHh);
            if (c is >= 0 and < Cols && r is >= 0 and < Rows && i.PointerX >= Ox && i.PointerY >= Oy) { _cx = c; _cy = r; Act(r * Cols + c); }
        }

        _clock += dt * 0.025f * V.Speed;
        if (_clock >= 1) { _clock -= 1; _day++; if (Rng.NextDouble() < 0.35) _rain = 0.3f; }
        if (_rain > 0) _rain -= dt * 0.02f;
        bool daylight = _clock is > 0.2f and < 0.8f;
        var growRate = (daylight ? 0.09f : 0.02f) * (_rain > 0 ? 1.8f : 1f) * V.Speed / (1 + V.Mode * 0.4f);
        for (int k = 0; k < _growth.Length; k++) if (_planted[k]) _growth[k] = Math.Min(1, _growth[k] + growRate * dt);

        Score = _coins;
        Status = $"Day {_day} • {(daylight ? "Day" : "Night")}{(_rain > 0 ? " • Rain" : "")} • {Crop}: seed {SeedCost} / sell {SellPrice}";
        if (_coins < SeedCost && !_planted.Any(p => p)) { GameOver = true; Status = "Bankrupt!"; }
    }

    protected override void Render(ICanvas c)
    {
        var light = MathF.Max(0.15f, MathF.Sin(_clock * MathF.PI));
        c.FillColor = Color.FromHsla(.58f, .5f, .15f + .45f * light); c.FillRectangle(0, 0, W, H);
        var sx = _clock * W; c.FillColor = _clock is > 0.2f and < 0.8f ? Colors.Gold : Colors.WhiteSmoke;
        c.FillCircle(sx, 110 - MathF.Sin(_clock * MathF.PI) * 60, 40);
        c.FillColor = Color.FromHsla(.28f, .45f, .2f + .2f * light); c.FillRectangle(0, Oy - 30, W, H);
        if (_rain > 0) { c.StrokeColor = Colors.LightBlue.WithAlpha(.5f); c.StrokeSize = 2; for (int k = 0; k < 120; k++) { var x = (k * 137 + _clock * 9000) % W; var y = (k * 71 + _clock * 30000) % H; c.DrawLine(x, y, x - 6, y + 22); } }

        for (int r = 0; r < Rows; r++) for (int col = 0; col < Cols; col++)
        {
            int k = r * Cols + col; float x = Ox + col * PW, y = Oy + r * PHh;
            c.FillColor = _owned[k] ? Color.FromRgb(110, 72, 40) : Colors.Black.WithAlpha(.25f);
            c.FillRoundedRectangle(x + 6, y + 6, PW - 12, PHh - 12, 14);
            if (!_owned[k]) Text(c, $"Buy {PlotCost}", x + PW / 2, y + PHh / 2 + 10, 26, Colors.White, HorizontalAlignment.Center);
            else if (_planted[k])
            {
                var g = _growth[k]; var ripe = g >= 1;
                c.FillColor = ripe ? P.Accent : Color.FromHsla(.3f, .6f, .35f + .2f * g);
                for (int s = 0; s < 3; s++) c.FillEllipse(x + 30 + s * 40, y + PHh - 40 - 70 * g, 26, 20 + 70 * g);
                if (ripe) Text(c, "Ready!", x + PW / 2, y + 40, 24, Colors.White, HorizontalAlignment.Center);
            }
            if (col == _cx && r == _cy) { c.StrokeColor = P.Accent; c.StrokeSize = 6; c.DrawRoundedRectangle(x + 4, y + 4, PW - 8, PHh - 8, 16); }
        }
        float px = Ox + Cols * PW + 60;
        c.FillColor = Colors.Black.WithAlpha(.35f); c.FillRoundedRectangle(px, Oy, 380, 300, 20);
        Text(c, $"Coins: {_coins}", px + 30, Oy + 70, 40, Colors.Gold);
        Text(c, $"Crop: {Crop}", px + 30, Oy + 130, 30, Colors.White);
        Text(c, $"Land: {_owned.Count(o => o)}/{_owned.Length}", px + 30, Oy + 180, 30, Colors.White);
        Text(c, $"Growing: {_planted.Count(p => p)}", px + 30, Oy + 230, 30, Colors.White);
    }
}

// ======================= ECO SIM (predator / prey ecosystem) =======================
public sealed class EcoSimGame : GameBase
{
    sealed class Agent { public float X, Y, Vx, Vy, Energy, Age; public bool Fox; }
    readonly List<Agent> _agents = new(); readonly List<PointF> _grass = new();
    readonly List<(int r, int f)> _history = new();
    float _t, _sample, _grassT; const float Field = 720;
    public override string ControlsHint => "Tap: plant grass • A: add rabbit • B: add fox • keep both alive";

    protected override void Reset()
    {
        _agents.Clear(); _grass.Clear(); _history.Clear(); _t = 0;
        for (int k = 0; k < 40; k++) Add(false); for (int k = 0; k < 3 + V.Difficulty / 3; k++) Add(true);
        for (int k = 0; k < 120; k++) _grass.Add(new PointF(Rand(20, W - 20), Rand(20, Field - 20)));
    }

    void Add(bool fox, float? x = null, float? y = null) =>
        _agents.Add(new Agent { Fox = fox, X = x ?? Rand(40, W - 40), Y = y ?? Rand(40, Field - 40), Energy = fox ? 60 : 30 });

    protected override void Tick(float dt, InputState i)
    {
        dt *= V.Speed; _t += dt;
        if (i.Tapped(GameKey.A)) Add(false);
        if (i.Tapped(GameKey.B)) Add(true);
        if (i.PointerTap && i.PointerY < Field) for (int k = 0; k < 8; k++) _grass.Add(new PointF(i.PointerX + Rand(-40, 40), i.PointerY + Rand(-40, 40)));
        if ((_grassT += dt) > 0.12f && _grass.Count < 400) { _grassT = 0; _grass.Add(new PointF(Rand(20, W - 20), Rand(20, Field - 20))); }

        var born = new List<Agent>();
        foreach (var a in _agents)
        {
            a.Age += dt; a.Energy -= dt * (a.Fox ? 5.5f + V.Difficulty * 0.3f : 3f);
            float tx = 0, ty = 0, best = a.Fox ? 260 : 160;
            if (a.Fox)
            {
                foreach (var b in _agents) if (!b.Fox) { var d = Dist(a, b.X, b.Y); if (d < best) { best = d; tx = b.X - a.X; ty = b.Y - a.Y; } }
            }
            else
            {
                foreach (var g in _grass) { var d = Dist(a, g.X, g.Y); if (d < best) { best = d; tx = g.X - a.X; ty = g.Y - a.Y; } }
                foreach (var f in _agents) if (f.Fox && Dist(a, f.X, f.Y) < 120) { tx = a.X - f.X; ty = a.Y - f.Y; break; }
            }
            var len = MathF.Sqrt(tx * tx + ty * ty);
            var sp = a.Fox ? 95f : 85f;
            if (len > 1) { a.Vx += (tx / len * sp - a.Vx) * dt * 3; a.Vy += (ty / len * sp - a.Vy) * dt * 3; }
            else { a.Vx += Rand(-80, 80) * dt; a.Vy += Rand(-80, 80) * dt; }
            a.X = Math.Clamp(a.X + a.Vx * dt, 10, W - 10); a.Y = Math.Clamp(a.Y + a.Vy * dt, 10, Field - 10);

            if (!a.Fox)
            {
                for (int g = _grass.Count - 1; g >= 0; g--) if (Dist(a, _grass[g].X, _grass[g].Y) < 14) { _grass.RemoveAt(g); a.Energy += 9; break; }
                if (a.Energy > 45) { a.Energy = 22; born.Add(new Agent { X = a.X, Y = a.Y, Energy = 20 }); }
            }
            else
            {
                foreach (var b in _agents) if (!b.Fox && b.Energy > 0 && Dist(a, b.X, b.Y) < 18) { b.Energy = -1; a.Energy += 28; break; }
                if (a.Energy > 110) { a.Energy = 55; born.Add(new Agent { Fox = true, X = a.X, Y = a.Y, Energy = 50 }); }
            }
        }
        _agents.RemoveAll(a => a.Energy <= 0 || a.Age > (a.Fox ? 70 : 40));
        if (_agents.Count < 600) _agents.AddRange(born);

        int rabbits = _agents.Count(a => !a.Fox), foxes = _agents.Count - rabbits;
        if ((_sample += dt) > 0.5f) { _sample = 0; _history.Add((rabbits, foxes)); if (_history.Count > 160) _history.RemoveAt(0); }
        Score = (int)_t;
        Status = $"Rabbits {rabbits} • Foxes {foxes} • {_t:0}s";
        if (rabbits == 0) { GameOver = true; Status = "Ecosystem collapsed — rabbits extinct"; }
    }

    static float Dist(Agent a, float x, float y) => MathF.Sqrt((a.X - x) * (a.X - x) + (a.Y - y) * (a.Y - y));

    protected override void Render(ICanvas c)
    {
        c.FillColor = Color.FromHsla(.3f, .35f, .2f); c.FillRectangle(0, 0, W, Field);
        c.FillColor = Color.FromHsla(.3f, .7f, .45f); foreach (var g in _grass) c.FillCircle(g.X, g.Y, 5);
        foreach (var a in _agents)
        {
            c.FillColor = a.Fox ? Color.FromRgb(232, 112, 40) : Colors.WhiteSmoke;
            c.FillCircle(a.X, a.Y, a.Fox ? 11 : 8);
            if (!a.Fox) { c.FillEllipse(a.X - 6, a.Y - 18, 4, 12); c.FillEllipse(a.X + 2, a.Y - 18, 4, 12); }
        }
        c.FillColor = P.Background2; c.FillRectangle(0, Field, W, H - Field);
        if (_history.Count < 2) return;
        int max = Math.Max(10, _history.Max(h => Math.Max(h.r, h.f)));
        void Line(Func<(int r, int f), int> sel, Color col)
        {
            var p = new PathF();
            for (int k = 0; k < _history.Count; k++) { var x = 40 + k * (W - 80) / 160f; var y = H - 15 - sel(_history[k]) * 140f / max; if (k == 0) p.MoveTo(x, y); else p.LineTo(x, y); }
            c.StrokeColor = col; c.StrokeSize = 4; c.DrawPath(p);
        }
        Line(h => h.r, Colors.WhiteSmoke); Line(h => h.f, Color.FromRgb(232, 112, 40));
    }
}
