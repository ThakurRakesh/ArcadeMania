using ArcadeMania.Core.Engine;
using Microsoft.Maui.Graphics;

namespace ArcadeMania.Core.Games;

// ======================= SNAKE =======================
public sealed class SnakeGame : GameBase
{
    const int Cols = 32, Rows = 18; const float Cell = 50f;
    readonly List<(int x, int y)> _body = new();
    (int x, int y) _dir, _next, _food;
    float _acc; bool _wrap;
    public override string ControlsHint => "D-pad / stick: steer • eat to grow";

    protected override void Reset()
    {
        _body.Clear();
        for (int i = 0; i < 4; i++) _body.Add((8 - i, 9));
        _dir = _next = (1, 0); _acc = 0; _wrap = V.Mode % 2 == 1;
        PlaceFood();
        Status = _wrap ? "Wrap-around walls" : "Solid walls";
    }

    void PlaceFood()
    {
        do _food = (Rng.Next(Cols), Rng.Next(Rows)); while (_body.Contains(_food));
    }

    protected override void Tick(float dt, InputState i)
    {
        if (i.Tapped(GameKey.Left) || i.Left) { if (_dir.x == 0) _next = (-1, 0); }
        else if (i.Tapped(GameKey.Right) || i.Right) { if (_dir.x == 0) _next = (1, 0); }
        else if (i.Tapped(GameKey.Up) || i.Up) { if (_dir.y == 0) _next = (0, -1); }
        else if (i.Tapped(GameKey.Down) || i.Down) { if (_dir.y == 0) _next = (0, 1); }

        _acc += dt;
        var step = Math.Max(0.045f, 0.13f / V.Speed - _body.Count * 0.0006f);
        if (_acc < step) return;
        _acc = 0; _dir = _next;
        var h = (x: _body[0].x + _dir.x, y: _body[0].y + _dir.y);
        if (_wrap) h = ((h.x + Cols) % Cols, (h.y + Rows) % Rows);
        if (h.x < 0 || h.y < 0 || h.x >= Cols || h.y >= Rows || _body.Contains(h)) { GameOver = true; return; }
        _body.Insert(0, h);
        if (h == _food) { Score += 10 * V.Difficulty; PlaceFood(); } else _body.RemoveAt(_body.Count - 1);
    }

    protected override void Render(ICanvas c)
    {
        c.FillColor = P.Background2;
        for (int x = 0; x < Cols; x++) for (int y = 0; y < Rows; y++)
            if ((x + y) % 2 == 0) c.FillRectangle(x * Cell, y * Cell, Cell, Cell);
        Glow(c, P.Danger);
        c.FillColor = P.Danger; c.FillEllipse(_food.x * Cell + 8, _food.y * Cell + 8, Cell - 16, Cell - 16);
        Glow(c, P.Primary, 12);
        for (int k = _body.Count - 1; k >= 0; k--)
        {
            var t = 1f - k / (float)Math.Max(1, _body.Count);
            c.FillColor = P.Primary.WithLuminosity(0.35f + 0.3f * t);
            c.FillRoundedRectangle(_body[k].x * Cell + 3, _body[k].y * Cell + 3, Cell - 6, Cell - 6, 14);
        }
        NoGlow(c);
    }
}

// ======================= BRICK BREAKER =======================
public sealed class BrickBreakerGame : GameBase
{
    float _px, _bx, _by, _vx, _vy; bool _launched; int _lives, _level;
    readonly List<(RectF r, int hp)> _bricks = new();
    const float PW = 220, PH = 22, R = 13;
    public override string ControlsHint => "Left/Right or drag: paddle • A / tap: launch";

    protected override void Reset() { _lives = 3; _level = 1; BuildLevel(); }

    void BuildLevel()
    {
        _bricks.Clear();
        int rows = 4 + Math.Min(5, V.Difficulty / 2 + _level - 1), cols = 12;
        float bw = (W - 120) / cols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (V.Mode == 2 && (r + c) % 3 == 0) continue;                 // checker layout
                if (V.Mode == 3 && Math.Abs(c - cols / 2f + .5f) > rows - r + 2) continue; // pyramid
                _bricks.Add((new RectF(60 + c * bw + 4, 80 + r * 46, bw - 8, 36), 1 + (r < _level ? 1 : 0)));
            }
        _px = W / 2; Serve();
    }

    void Serve() { _launched = false; _bx = _px; _by = H - 90 - R; _vx = 0; _vy = 0; }

    protected override void Tick(float dt, InputState i)
    {
        var sp = 900f;
        if (i.Left) _px -= sp * dt;
        if (i.Right) _px += sp * dt;
        if (i.PointerDown) _px = i.PointerX;
        _px = Math.Clamp(_px, PW / 2, W - PW / 2);

        if (!_launched)
        {
            _bx = _px; _by = H - 90 - R;
            if (i.Tapped(GameKey.A) || i.PointerTap || i.Tapped(GameKey.Up))
            { _launched = true; var s = 560f * V.Speed + _level * 30; _vx = s * 0.55f; _vy = -s; }
            return;
        }

        _bx += _vx * dt; _by += _vy * dt;
        if (_bx < R) { _bx = R; _vx = Math.Abs(_vx); }
        if (_bx > W - R) { _bx = W - R; _vx = -Math.Abs(_vx); }
        if (_by < R) { _by = R; _vy = Math.Abs(_vy); }

        var paddle = new RectF(_px - PW / 2, H - 90, PW, PH);
        if (_vy > 0 && Hit(paddle, new RectF(_bx - R, _by - R, 2 * R, 2 * R)))
        {
            var off = (_bx - _px) / (PW / 2);
            var s = MathF.Sqrt(_vx * _vx + _vy * _vy) * 1.01f;
            _vx = s * off * 0.85f; _vy = -MathF.Sqrt(Math.Max(1, s * s - _vx * _vx));
        }

        for (int k = 0; k < _bricks.Count; k++)
        {
            var (r, hp) = _bricks[k];
            if (!Hit(r, new RectF(_bx - R, _by - R, 2 * R, 2 * R))) continue;
            if (_bx > r.Left && _bx < r.Right) _vy = -_vy; else _vx = -_vx;
            if (--hp <= 0) { _bricks.RemoveAt(k); Score += 10 * V.Difficulty; } else _bricks[k] = (r, hp);
            break;
        }

        if (_bricks.Count == 0) { _level++; Score += 500; BuildLevel(); }
        if (_by > H + 40) { if (--_lives <= 0) GameOver = true; else Serve(); }
        Status = $"Level {_level} • Lives {_lives}";
    }

    protected override void Render(ICanvas c)
    {
        foreach (var (r, hp) in _bricks)
        {
            c.FillColor = hp > 1 ? P.Accent : P.Secondary.WithLuminosity(0.4f + r.Y / 1600f);
            c.FillRoundedRectangle(r, 8);
        }
        Glow(c, P.Primary);
        c.FillColor = P.Primary; c.FillRoundedRectangle(_px - PW / 2, H - 90, PW, PH, 11);
        c.FillColor = Colors.White; c.FillEllipse(_bx - R, _by - R, 2 * R, 2 * R);
        NoGlow(c);
        if (!_launched) Text(c, "Press A or tap to launch", W / 2, H / 2 + 120, 40, P.Text, HorizontalAlignment.Center);
    }
}

// ======================= SPACE SHOOTER =======================
public sealed class SpaceShooterGame : GameBase
{
    float _sx, _sy, _fire, _spawn; int _lives; float _inv;
    readonly List<(float x, float y)> _shots = new(), _eshots = new();
    readonly List<(float x, float y, float t, float hp, float bx)> _enemies = new();
    readonly (float x, float y, float s)[] _stars = new (float, float, float)[140];
    public override string ControlsHint => "Move with D-pad/stick • A: fire (auto-fire on)";

    protected override void Reset()
    {
        _sx = W / 2; _sy = H - 120; _lives = 3; _inv = 0;
        _shots.Clear(); _eshots.Clear(); _enemies.Clear();
        for (int k = 0; k < _stars.Length; k++) _stars[k] = (Rand(0, W), Rand(0, H), Rand(40, 260));
    }

    protected override void Tick(float dt, InputState i)
    {
        var sp = 700f * Math.Min(1.3f, V.Speed);
        if (i.Left) _sx -= sp * dt; if (i.Right) _sx += sp * dt;
        if (i.Up) _sy -= sp * dt; if (i.Down) _sy += sp * dt;
        if (i.PointerDown) { _sx += (i.PointerX - _sx) * Math.Min(1, dt * 10); _sy += (i.PointerY - 80 - _sy) * Math.Min(1, dt * 10); }
        _sx = Math.Clamp(_sx, 40, W - 40); _sy = Math.Clamp(_sy, H * 0.35f, H - 50);

        for (int k = 0; k < _stars.Length; k++)
        { var s = _stars[k]; s.y += s.s * dt; if (s.y > H) s.y = 0; _stars[k] = s; }

        _fire -= dt;
        if (_fire <= 0) { _shots.Add((_sx, _sy - 40)); if (V.Mode >= 2) { _shots.Add((_sx - 30, _sy - 20)); _shots.Add((_sx + 30, _sy - 20)); } _fire = i.A ? 0.09f : 0.18f; }
        for (int k = _shots.Count - 1; k >= 0; k--) { var s = _shots[k]; s.y -= 1300 * dt; if (s.y < -20) _shots.RemoveAt(k); else _shots[k] = s; }

        _spawn -= dt;
        if (_spawn <= 0) { _enemies.Add((Rand(80, W - 80), -40, 0, 1 + V.Difficulty / 4, 0)); _spawn = Math.Max(0.25f, 1.1f - V.Difficulty * 0.07f - Score / 20000f); }
        for (int k = _enemies.Count - 1; k >= 0; k--)
        {
            var e = _enemies[k];
            e.t += dt; e.y += (110 + 18 * V.Difficulty) * V.Speed * dt;
            if (e.bx == 0) e.bx = e.x;
            e.x = e.bx + MathF.Sin(e.t * 2 + k) * 120;
            if (Rng.NextDouble() < 0.002 * V.Difficulty) _eshots.Add((e.x, e.y + 30));
            for (int s = _shots.Count - 1; s >= 0; s--)
                if (MathF.Abs(_shots[s].x - e.x) < 36 && MathF.Abs(_shots[s].y - e.y) < 34) { _shots.RemoveAt(s); e.hp--; }
            if (e.hp <= 0) { Score += 25 * V.Difficulty; _enemies.RemoveAt(k); continue; }
            if (e.y > H + 50) { _enemies.RemoveAt(k); continue; }
            if (_inv <= 0 && MathF.Abs(e.x - _sx) < 50 && MathF.Abs(e.y - _sy) < 45) { Damage(); _enemies.RemoveAt(k); continue; }
            _enemies[k] = e;
        }
        for (int k = _eshots.Count - 1; k >= 0; k--)
        {
            var s = _eshots[k]; s.y += 480 * V.Speed * dt;
            if (_inv <= 0 && MathF.Abs(s.x - _sx) < 30 && MathF.Abs(s.y - _sy) < 30) { Damage(); _eshots.RemoveAt(k); continue; }
            if (s.y > H) _eshots.RemoveAt(k); else _eshots[k] = s;
        }
        _inv -= dt;
        Status = $"Lives {_lives}";
    }

    void Damage() { _inv = 1.5f; if (--_lives <= 0) GameOver = true; }

    protected override void Render(ICanvas c)
    {
        foreach (var s in _stars) { c.FillColor = P.Text.WithAlpha(s.s / 300f); c.FillCircle(s.x, s.y, s.s / 110f); }
        Glow(c, P.Accent, 10);
        c.FillColor = P.Accent; foreach (var s in _shots) c.FillRoundedRectangle(s.x - 4, s.y - 16, 8, 32, 4);
        c.FillColor = P.Danger; foreach (var s in _eshots) c.FillCircle(s.x, s.y, 9);
        Glow(c, P.Secondary, 14);
        foreach (var e in _enemies)
        {
            c.FillColor = P.Secondary;
            var p = new PathF(); p.MoveTo(e.x, e.y + 30); p.LineTo(e.x - 40, e.y - 20); p.LineTo(e.x, e.y - 5); p.LineTo(e.x + 40, e.y - 20); p.Close();
            c.FillPath(p);
        }
        if (_inv <= 0 || (int)(_inv * 10) % 2 == 0)
        {
            Glow(c, P.Primary, 20);
            c.FillColor = P.Primary;
            var p = new PathF(); p.MoveTo(_sx, _sy - 45); p.LineTo(_sx - 38, _sy + 30); p.LineTo(_sx, _sy + 14); p.LineTo(_sx + 38, _sy + 30); p.Close();
            c.FillPath(p);
            c.FillColor = P.Accent; c.FillEllipse(_sx - 8, _sy + 18, 16, 24 + Rng.Next(10));
        }
        NoGlow(c);
    }
}

// ======================= PONG =======================
public sealed class PongGame : GameBase
{
    float _py, _ay, _bx, _by, _vx, _vy, _serveDelay; int _ps, _as;
    const float PH = 150, PW = 22, R = 14;
    public override string ControlsHint => "Up/Down or drag: paddle • first to 7";

    protected override void Reset() { _py = _ay = H / 2; _ps = _as = 0; Serve(1); }

    void Serve(int dir)
    {
        _bx = W / 2; _by = H / 2; _serveDelay = 0.8f;
        var s = 620f * V.Speed; _vx = s * dir; _vy = Rand(-s * .5f, s * .5f);
    }

    protected override void Tick(float dt, InputState i)
    {
        if (i.Up) _py -= 820 * dt; if (i.Down) _py += 820 * dt;
        if (i.PointerDown) _py = i.PointerY;
        _py = Math.Clamp(_py, PH / 2, H - PH / 2);

        var aiSpeed = 260 + V.Difficulty * 55f;
        var target = _vx > 0 ? _by : H / 2;
        _ay += Math.Clamp(target - _ay, -aiSpeed * dt, aiSpeed * dt);
        _ay = Math.Clamp(_ay, PH / 2, H - PH / 2);

        if ((_serveDelay -= dt) > 0) return;
        _bx += _vx * dt; _by += _vy * dt;
        if (_by < R || _by > H - R) { _vy = -_vy; _by = Math.Clamp(_by, R, H - R); }

        if (_vx < 0 && _bx - R < 60 + PW && _bx > 40 && MathF.Abs(_by - _py) < PH / 2 + R) Bounce(_py, 1);
        if (_vx > 0 && _bx + R > W - 60 - PW && _bx < W - 40 && MathF.Abs(_by - _ay) < PH / 2 + R) Bounce(_ay, -1);

        if (_bx < -30) { _as++; Serve(-1); }
        if (_bx > W + 30) { _ps++; Score += 100 * V.Difficulty; Serve(1); }
        if (_ps >= 7 || _as >= 7) { if (_ps >= 7) Score += 1000; GameOver = true; Status = _ps >= 7 ? "You win!" : "CPU wins"; }
        else Status = $"You {_ps} : {_as} CPU";
    }

    void Bounce(float paddleY, int dir)
    {
        var off = (_by - paddleY) / (PH / 2);
        var s = MathF.Min(1500, MathF.Sqrt(_vx * _vx + _vy * _vy) * 1.06f);
        _vx = dir * s * 0.85f; _vy = s * off * 0.6f;
    }

    protected override void Render(ICanvas c)
    {
        c.StrokeColor = P.Text.WithAlpha(.25f); c.StrokeSize = 6; c.StrokeDashPattern = [4, 4];
        c.DrawLine(W / 2, 0, W / 2, H); c.StrokeDashPattern = null;
        Text(c, _ps.ToString(), W / 2 - 120, 140, 110, P.Text.WithAlpha(.4f), HorizontalAlignment.Center);
        Text(c, _as.ToString(), W / 2 + 120, 140, 110, P.Text.WithAlpha(.4f), HorizontalAlignment.Center);
        Glow(c, P.Primary); c.FillColor = P.Primary; c.FillRoundedRectangle(60, _py - PH / 2, PW, PH, 10);
        Glow(c, P.Secondary); c.FillColor = P.Secondary; c.FillRoundedRectangle(W - 60 - PW, _ay - PH / 2, PW, PH, 10);
        Glow(c, P.Accent); c.FillColor = P.Accent; c.FillCircle(_bx, _by, R);
        NoGlow(c);
    }
}
