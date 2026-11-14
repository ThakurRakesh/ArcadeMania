using System.Diagnostics;
using ArcadeMania.App.Services;
using ArcadeMania.Core.Catalog;
using ArcadeMania.Core.Engine;

namespace ArcadeMania.App.Pages;

/// <summary>Runs one game. Vector rendering scales the 1600×900 world to the native pixel grid,
/// so a 4K (3840×2160) display is rendered at full 4K, not upscaled.</summary>
public sealed class GamePage : BasePage
{
    readonly CatalogEntry _entry;
    GameBase _game;
    readonly GraphicsView _view;
    readonly GameDrawable _drawable;
    readonly Label _score = new(), _status = new(), _res = new();
    readonly Border _overlay;
    readonly Label _overlayTitle = new(), _overlayText = new();
    readonly Grid _touchPad;
    readonly Button _pauseBtn;
    IDispatcherTimer? _timer;
    readonly Stopwatch _clock = new();
    bool _paused, _reported;
    int _frames; double _fpsT; int _fps;

    public GamePage(CatalogEntry entry)
    {
        _entry = entry;
        _game = entry.Create(dark: true);
        _drawable = new GameDrawable(() => _game);
        _view = new GraphicsView { Drawable = _drawable, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        _view.SetDynamicResource(GraphicsView.BackgroundColorProperty, "Bg");
        _view.StartInteraction += (_, e) => Pointer(e.Touches.FirstOrDefault(), true, true);
        _view.DragInteraction += (_, e) => Pointer(e.Touches.FirstOrDefault(), true, false);
        _view.EndInteraction += (_, _) => ControllerHub.GameInput.PointerDown = false;
        _view.CancelInteraction += (_, _) => ControllerHub.GameInput.PointerDown = false;

        foreach (var l in new[] { _score, _status, _res }) { l.FontFamily = Theme.Body; l.VerticalOptions = LayoutOptions.Center; }
        _score.FontSize = 20; _score.FontAttributes = FontAttributes.Bold; _score.SetDynamicResource(Label.TextColorProperty, "Accent2");
        _status.FontSize = 13; _status.SetDynamicResource(Label.TextColorProperty, "Muted"); _status.LineBreakMode = LineBreakMode.TailTruncation;
        _res.FontSize = 11; _res.SetDynamicResource(Label.TextColorProperty, "Muted");

        _pauseBtn = Btn("⏸ Pause", TogglePause, small: true);
        var top = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 8, Padding = new Thickness(10, 8)
        };
        top.SetDynamicResource(Grid.BackgroundColorProperty, "Surface");
        top.Add(Btn("◀ Back", GoBack, small: true), 0);
        top.Add(Btn("⌂ Home", GoHome, small: true), 1);
        var title = new Label { Text = $"{entry.Icon} {entry.Title}", FontFamily = Theme.Heading, FontSize = 22, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
        title.SetDynamicResource(Label.TextColorProperty, "Text");
        top.Add(new VerticalStackLayout { Children = { title, _status }, VerticalOptions = LayoutOptions.Center }, 2);
        top.Add(new VerticalStackLayout { Children = { _score, _res }, VerticalOptions = LayoutOptions.Center }, 3);
        top.Add(_pauseBtn, 4);
        top.Add(Btn("⛶", FullScreen.Toggle, small: true), 5);
        top.Add(Btn("✕ Exit", ConfirmExit, small: true, danger: true), 6);

        _overlayTitle.FontFamily = Theme.Heading; _overlayTitle.FontSize = 44; _overlayTitle.HorizontalTextAlignment = TextAlignment.Center; _overlayTitle.SetDynamicResource(Label.TextColorProperty, "Accent2");
        _overlayText.FontFamily = Theme.Body; _overlayText.FontSize = 18; _overlayText.HorizontalTextAlignment = TextAlignment.Center; _overlayText.SetDynamicResource(Label.TextColorProperty, "Text");
        _overlay = new Border
        {
            IsVisible = false, Padding = 30, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, MaximumWidthRequest = 560,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    _overlayTitle, _overlayText,
                    Btn("▶ Play again", Restart, primary: true),
                    Btn("⏭ Next game", () => Replace(GameCatalog.All[_entry.Number % GameCatalog.TotalGames])),
                    Btn("🎲 Random game", () => Replace(GameCatalog.Random())),
                    Btn("◀ Back to games", GoBack)
                }
            }
        };
        _overlay.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        _overlay.SetDynamicResource(Border.StrokeProperty, "Accent");

        _touchPad = BuildTouchPad();
        _touchPad.IsVisible = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;

        var hint = new Label { Text = $"{_game.ControlsHint}   •   🎮 Start/Options = pause", FontFamily = Theme.Body, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(6, 2) };
        hint.SetDynamicResource(Label.TextColorProperty, "Muted");

        Content = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            Children =
            {
                top,
                new Grid { Children = { _view, _overlay } }.Row(1),
                hint.Row(2),
                _touchPad.Row(3)
            }
        };
    }

    // ---------- loop ----------
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ControllerHub.GameActive = true;
        ControllerHub.GameInput.ReleaseAll();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(8);
        _timer.Tick -= OnTick; _timer.Tick += OnTick;
        _clock.Restart(); _timer.Start();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        ControllerHub.GameActive = false;
        ControllerHub.GameInput.ReleaseAll();
        Report();
        base.OnDisappearing();
    }

    void OnTick(object? sender, EventArgs e)
    {
        var dt = (float)_clock.Elapsed.TotalSeconds; _clock.Restart();
        try
        {
            var input = ControllerHub.GameInput;
            if (input.Tapped(GameKey.Start)) TogglePause();
            if (!_paused && !_overlay.IsVisible) _game.Update(dt, input);
            input.EndFrame();
            if (_game.GameOver && !_overlay.IsVisible) ShowGameOver();
            _view.Invalidate();
        }
        catch (Exception)
        {
            // A game must never take the app down: restart this game cleanly.
            _game = _entry.Create(); _drawable.Reset();
            _status.Text = "Recovered from an error — game restarted";
        }

        _frames++; _fpsT += dt;
        if (_fpsT >= 0.5)
        {
            _fps = (int)Math.Round(_frames / _fpsT); _frames = 0; _fpsT = 0;
            var d = DeviceDisplay.Current.MainDisplayInfo.Density;
            _res.Text = $"{_view.Width * d:0}×{_view.Height * d:0} px • {_fps} fps";
            _score.Text = $"Score {_game.Score:N0}";
            if (!_paused) _status.Text = string.IsNullOrEmpty(_game.Status) ? _entry.Subtitle : _game.Status;
        }
    }

    void Pointer(PointF p, bool down, bool tap)
    {
        var (x, y) = _drawable.ToWorld(p.X, p.Y);
        var i = ControllerHub.GameInput;
        i.PointerX = x; i.PointerY = y; i.PointerDown = down;
        if (tap) i.PointerTap = true;
    }

    void TogglePause()
    {
        if (_game.GameOver) return;
        _paused = !_paused;
        _pauseBtn.Text = _paused ? "▶ Resume" : "⏸ Pause";
        _status.Text = _paused ? "Paused" : _status.Text;
    }

    void ShowGameOver()
    {
        var best = AppServices.Scores.Best(_entry.Number);
        var isNew = Report();
        _overlayTitle.Text = isNew ? "🏆 New best!" : "Game over";
        _overlayText.Text = $"{(string.IsNullOrEmpty(_game.Status) ? "" : _game.Status + "\n")}Score {_game.Score:N0}   •   Best {Math.Max(best, _game.Score):N0}";
        _overlay.IsVisible = true;
    }

    bool Report()
    {
        if (_reported || _game.Score <= 0) return false;
        _reported = true;
        return AppServices.Scores.Submit(_entry.Number, _game.Score);
    }

    void Restart()
    {
        Report();
        _overlay.IsVisible = false; _paused = false; _reported = false; _pauseBtn.Text = "⏸ Pause";
        _game.Restart();
    }

    async void Replace(CatalogEntry next)
    {
        try
        {
            Report();
            Navigation.InsertPageBefore(new GamePage(next), this);
            await Navigation.PopAsync(false);
        }
        catch { }
    }

    protected override void HandleNav(NavCommand cmd)
    {
        if (cmd == NavCommand.Back) { GoBack(); return; }
        if (_overlay.IsVisible) base.HandleNav(cmd);   // controller can pick overlay buttons
    }

    // ---------- touch controls (phones/tablets) ----------
    Grid BuildTouchPad()
    {
        Button Pad(string text, GameKey key, double size = 64)
        {
            var b = new Button { Text = text, FontSize = 24, WidthRequest = size, HeightRequest = size, CornerRadius = (int)(size / 2), Padding = 0, Opacity = 0.9 };
            b.SetDynamicResource(Button.BackgroundColorProperty, "Surface");
            b.SetDynamicResource(Button.TextColorProperty, "Text");
            b.Pressed += (_, _) => ControllerHub.Key(key, true);
            b.Released += (_, _) => ControllerHub.Key(key, false);
            PointerHover.Attach(b);
            return b;
        }

        var dpad = new Grid
        {
            RowDefinitions = { new(64), new(64), new(64) }, ColumnDefinitions = { new(64), new(64), new(64) }, RowSpacing = 4, ColumnSpacing = 4,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center
        };
        dpad.Add(Pad("▲", GameKey.Up), 1, 0); dpad.Add(Pad("◀", GameKey.Left), 0, 1);
        dpad.Add(Pad("▶", GameKey.Right), 2, 1); dpad.Add(Pad("▼", GameKey.Down), 1, 2);

        var a = Pad("A", GameKey.A, 84); a.SetDynamicResource(Button.BackgroundColorProperty, "Accent"); a.SetDynamicResource(Button.TextColorProperty, "OnAccent");
        var b = Pad("B", GameKey.B, 72);
        var buttons = new HorizontalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Children = { b, a } };

        var g = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, Padding = new Thickness(16, 6, 16, 14) };
        g.Add(dpad, 0); g.Add(buttons, 1);
        return g;
    }
}

/// <summary>Letterboxes the 1600×900 world into any screen (phone portrait → 4K TV) and maps touches back to world space.</summary>
public sealed class GameDrawable(Func<GameBase> game) : IDrawable
{
    float _scale = 1, _ox, _oy;

    public void Reset() { _scale = 1; _ox = _oy = 0; }

    public (float x, float y) ToWorld(float sx, float sy) => ((sx - _ox) / _scale, (sy - _oy) / _scale);

    public void Draw(ICanvas canvas, RectF rect)
    {
        _scale = Math.Min(rect.Width / GameBase.W, rect.Height / GameBase.H);
        if (_scale <= 0) return;
        _ox = (rect.Width - GameBase.W * _scale) / 2; _oy = (rect.Height - GameBase.H * _scale) / 2;
        canvas.Antialias = true;
        canvas.SaveState();
        canvas.Translate(_ox, _oy);
        canvas.Scale(_scale, _scale);
        canvas.ClipRectangle(0, 0, GameBase.W, GameBase.H);
        try { game().Draw(canvas); } catch { /* skip a bad frame rather than crash */ }
        canvas.RestoreState();
    }
}
