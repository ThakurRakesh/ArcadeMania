using ArcadeMania.Core.Engine;
using Microsoft.Maui.Graphics;

namespace ArcadeMania.Core.Games;

// ======================= SKY HOPPER (flap) =======================
public sealed class SkyHopperGame : GameBase
{
    float _y, _vy, _spawn, _t; bool _started;
    readonly List<(float x, float gapY, bool scored)> _pipes = new();
    float Gap => Math.Max(190, 330 - V.Difficulty * 13);
    public override string ControlsHint => "A / Up / tap: flap through the gaps";

    protected override void Reset() { _y = H / 2; _vy = 0; _spawn = 0; _started = false; _pipes.Clear(); }

    protected override void Tick(float dt, InputState i)
    {
        _t += dt;
        var flap = i.Tapped(GameKey.A) || i.Tapped(GameKey.Up) || i.PointerTap;
        if (!_started) { _y = H / 2 + MathF.Sin(_t * 3) * 20; if (flap) _started = true; else return; }
        if (flap) _vy = -620;
        _vy += 1700 * dt; _y += _vy * dt;
        var speed = 330 * V.Speed;
        if ((_spawn -= dt) <= 0) { _pipes.Add((W + 60, Rand(Gap / 2 + 60, H - Gap / 2 - 60), false)); _spawn = 1.55f / V.Speed; }
        for (int k = _pipes.Count - 1; k >= 0; k--)
        {
            var p = _pipes[k]; p.x -= speed * dt;
            if (!p.scored && p.x < 400) { p.scored = true; Score += 10 * V.Difficulty; }
            if (MathF.Abs(p.x - 400) < 60 + 26 && MathF.Abs(_y - p.gapY) > Gap / 2 - 26) GameOver = true;
            if (p.x < -100) _pipes.RemoveAt(k); else _pipes[k] = p;
        }
        if (_y > H - 30 || _y < 0) GameOver = true;
    }

    protected override void Render(ICanvas c)
    {
        for (int k = 0; k < 5; k++)
        { c.FillColor = P.Background2.WithAlpha(.6f); c.FillEllipse((k * 380 - _t * 40 % 380 + 1900) % 1900 - 200, 120 + k * 37 % 200, 260, 90); }
        foreach (var p in _pipes)
        {
            c.FillColor = P.Secondary;
            c.FillRoundedRectangle(p.x - 60, -20, 120, p.gapY - Gap / 2 + 20, 16);
            c.FillRoundedRectangle(p.x - 60, p.gapY + Gap / 2, 120, H, 16);
        }
        c.FillColor = P.Background2; c.FillRectangle(0, H - 30, W, 30);
        Glow(c, P.Accent, 16);
        c.SaveState(); c.Rotate(Math.Clamp(_vy / 12, -30, 60), 400, _y);
        c.FillColor = P.Accent; c.FillEllipse(374, _y - 22, 52, 44);
        c.FillColor = Colors.White; c.FillCircle(410, _y - 8, 9);
        c.FillColor = P.Danger; c.FillRoundedRectangle(420, _y, 22, 10, 5);
        c.RestoreState(); NoGlow(c);
        if (!_started) Text(c, "Tap or press A to fly", W / 2, H / 2 + 140, 44, P.Text, HorizontalAlignment.Center);
    }
}

// ======================= BLOCK DROP (falling blocks puzzle) =======================
public sealed class BlockDropGame : GameBase
{
    const int Cols = 10, Rows = 20; const float Cell = 42f;
    static readonly int[][,] Shapes =
    [
        new[,] { { 1, 1, 1, 1 } }, new[,] { { 1, 1 }, { 1, 1 } }, new[,] { { 0, 1, 0 }, { 1, 1, 1 } },
        new[,] { { 1, 0, 0 }, { 1, 1, 1 } }, new[,] { { 0, 0, 1 }, { 1, 1, 1 } },
        new[,] { { 1, 1, 0 }, { 0, 1, 1 } }, new[,] { { 0, 1, 1 }, { 1, 1, 0 } }
    ];
    readonly int[,] _grid = new int[Rows, Cols];
    int[,] _piece = Shapes[0]; int _px, _py, _kind, _nextKind, _lines;
    float _fall, _repeat;
    float Ox => (W - Cols * Cell) / 2; const float Oy = 30;
    public override string ControlsHint => "Left/Right move • Up/B rotate • Down soft drop • A hard drop";

    protected override void Reset() { Array.Clear(_grid); _lines = 0; _nextKind = Rng.Next(7); Spawn(); }

    void Spawn()
    {
        _kind = _nextKind; _nextKind = Rng.Next(7);
        _piece = Shapes[_kind]; _px = Cols / 2 - _piece.GetLength(1) / 2; _py = 0;
        if (Collides(_piece, _px, _py)) GameOver = true;
    }

    bool Collides(int[,] s, int px, int py)
    {
        for (int r = 0; r < s.GetLength(0); r++) for (int c = 0; c < s.GetLength(1); c++)
        {
            if (s[r, c] == 0) continue;
            int x = px + c, y = py + r;
            if (x < 0 || x >= Cols || y >= Rows || (y >= 0 && _grid[y, x] != 0)) return true;
        }
        return false;
    }

    static int[,] Rotate(int[,] s)
    {
        int h = s.GetLength(0), w = s.GetLength(1); var n = new int[w, h];
        for (int r = 0; r < h; r++) for (int c = 0; c < w; c++) n[c, h - 1 - r] = s[r, c];
        return n;
    }

    void Lock()
    {
        for (int r = 0; r < _piece.GetLength(0); r++) for (int c = 0; c < _piece.GetLength(1); c++)
            if (_piece[r, c] != 0 && _py + r >= 0) _grid[_py + r, _px + c] = _kind + 1;
        int cleared = 0;
        for (int r = Rows - 1; r >= 0; r--)
        {
            bool full = true; for (int c = 0; c < Cols; c++) full &= _grid[r, c] != 0;
            if (!full) continue;
            cleared++;
            for (int y = r; y > 0; y--) for (int c = 0; c < Cols; c++) _grid[y, c] = _grid[y - 1, c];
            for (int c = 0; c < Cols; c++) _grid[0, c] = 0;
            r++;
        }
        _lines += cleared;
        Score += new[] { 0, 100, 300, 500, 800 }[cleared] * (1 + _lines / 10);
        Spawn();
    }

    protected override void Tick(float dt, InputState i)
    {
        _repeat -= dt;
        bool rep = _repeat <= 0;
        if (i.Tapped(GameKey.Left) || (i.Left && rep)) { if (!Collides(_piece, _px - 1, _py)) _px--; _repeat = i.Tapped(GameKey.Left) ? 0.2f : 0.06f; }
        if (i.Tapped(GameKey.Right) || (i.Right && rep)) { if (!Collides(_piece, _px + 1, _py)) _px++; _repeat = i.Tapped(GameKey.Right) ? 0.2f : 0.06f; }
        if (i.Tapped(GameKey.Up) || i.Tapped(GameKey.B) || i.PointerTap)
        {
            var r = Rotate(_piece);
            foreach (var kick in new[] { 0, -1, 1, -2, 2 })
                if (!Collides(r, _px + kick, _py)) { _piece = r; _px += kick; break; }
        }
        if (i.Tapped(GameKey.A)) { while (!Collides(_piece, _px, _py + 1)) { _py++; Score += 2; } Lock(); return; }

        var interval = Math.Max(0.05f, (0.8f - (_lines / 10) * 0.07f - V.Difficulty * 0.03f) / V.Speed);
        if (i.Down) interval = 0.04f;
        if ((_fall += dt) < interval) return;
        _fall = 0;
        if (Collides(_piece, _px, _py + 1)) Lock(); else _py++;
        Status = $"Lines {_lines}";
    }

    Color KindColor(int k) => Color.FromHsla((V.Hue + k / 7f) % 1f, .75f, .58f);

    protected override void Render(ICanvas c)
    {
        c.FillColor = P.Background2; c.FillRoundedRectangle(Ox - 10, Oy - 10, Cols * Cell + 20, Rows * Cell + 20, 14);
        for (int r = 0; r < Rows; r++) for (int col = 0; col < Cols; col++)
            if (_grid[r, col] != 0) Block(c, col, r, KindColor(_grid[r, col] - 1));
        int gy = _py; while (!Collides(_piece, _px, gy + 1)) gy++;
        for (int r = 0; r < _piece.GetLength(0); r++) for (int col = 0; col < _piece.GetLength(1); col++)
            if (_piece[r, col] != 0)
            {
                c.FillColor = P.Text.WithAlpha(.12f);
                c.FillRoundedRectangle(Ox + (_px + col) * Cell + 2, Oy + (gy + r) * Cell + 2, Cell - 4, Cell - 4, 6);
                Block(c, _px + col, _py + r, KindColor(_kind));
            }
        Text(c, "NEXT", Ox + Cols * Cell + 80, 90, 34, P.Text);
        var n = Shapes[_nextKind];
        for (int r = 0; r < n.GetLength(0); r++) for (int col = 0; col < n.GetLength(1); col++)
            if (n[r, col] != 0) { c.FillColor = KindColor(_nextKind); c.FillRoundedRectangle(Ox + Cols * Cell + 80 + col * Cell, 130 + r * Cell, Cell - 4, Cell - 4, 6); }
    }

    void Block(ICanvas c, int x, int y, Color col)
    {
        if (y < 0) return;
        c.FillColor = col; c.FillRoundedRectangle(Ox + x * Cell + 2, Oy + y * Cell + 2, Cell - 4, Cell - 4, 6);
        c.FillColor = Colors.White.WithAlpha(.25f); c.FillRoundedRectangle(Ox + x * Cell + 6, Oy + y * Cell + 6, Cell - 12, 8, 4);
    }
}
