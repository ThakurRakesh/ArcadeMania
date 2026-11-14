using ArcadeMania.App.Services;

namespace ArcadeMania.App.Pages;

/// <summary>Every screen gets Back, Home and Exit; hardware/controller Back always goes to the previous screen (never exits);
/// D-pad / stick moves a highlight between buttons and A (✕) activates it.</summary>
public abstract class BasePage : ContentPage
{
    readonly List<View> _focusables = new();
    int _focus = -1;
    protected bool IsRoot { get; init; }

    protected BasePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        SetDynamicResource(BackgroundColorProperty, "Bg");
    }

    protected View Header(string title, bool showBackHome = true)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 8, Padding = new Thickness(12, 10)
        };
        grid.SetDynamicResource(Grid.BackgroundColorProperty, "Surface");
        if (showBackHome)
        {
            grid.Add(Btn("◀ Back", GoBack, small: true), 0);
            grid.Add(Btn("⌂ Home", GoHome, small: true), 1);
        }
        var t = new Label { Text = title, FontFamily = Theme.Heading, FontSize = 28, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        t.SetDynamicResource(Label.TextColorProperty, "Text");
        grid.Add(t, 2);
        Button? themeBtn = null;
        themeBtn = Btn(Theme.IsNight ? "☀ Day" : "☾ Night", () => { Theme.Toggle(); themeBtn!.Text = Theme.IsNight ? "☀ Day" : "☾ Night"; OnThemeChanged(); }, small: true);
        grid.Add(themeBtn, 3);
        grid.Add(Btn("✕ Exit", ConfirmExit, small: true, danger: true), 4);
        return grid;
    }

    protected virtual void OnThemeChanged() { }

    public Button Btn(string text, Action onClick, bool small = false, bool primary = false, bool danger = false)
    {
        var b = new Button
        {
            Text = text, FontFamily = Theme.Body, FontSize = small ? 15 : 22, CornerRadius = small ? 10 : 18,
            Padding = small ? new Thickness(14, 8) : new Thickness(28, 16), BorderWidth = 1.5, MinimumHeightRequest = 44
        };
        b.SetDynamicResource(Button.BackgroundColorProperty, primary ? "Accent" : "Surface");
        b.SetDynamicResource(Button.TextColorProperty, primary ? "OnAccent" : "Text");
        b.SetDynamicResource(Button.BorderColorProperty, danger ? "Muted" : "Border");
        b.Clicked += (_, _) => { try { onClick(); } catch { } };
        PointerHover.Attach(b);
        _focusables.Add(b);
        return b;
    }

    protected void RegisterFocusable(View v) => _focusables.Add(v);

    // ---------- navigation ----------
    protected async void GoBack()
    {
        try { if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync(); else ConfirmExit(); } catch { }
    }

    protected async void GoHome() { try { await Navigation.PopToRootAsync(); } catch { } }

    protected async void ConfirmExit()
    {
        try { if (await DisplayAlertAsync("Exit ArcadeMania", "Close the app?", "Exit", "Stay")) AppServices.ExitApp(); } catch { }
    }

    protected override bool OnBackButtonPressed() { if (IsRoot) ConfirmExit(); else GoBack(); return true; }

    // ---------- controller / keyboard focus ----------
    protected override void OnAppearing() { base.OnAppearing(); ControllerHub.Nav += HandleNav; }
    protected override void OnDisappearing() { ControllerHub.Nav -= HandleNav; base.OnDisappearing(); }

    protected virtual void HandleNav(NavCommand cmd)
    {
        var items = _focusables.Where(v => v.IsVisible && v.IsEnabled && v.Handler is not null).ToList();
        switch (cmd)
        {
            case NavCommand.Back: if (IsRoot) ConfirmExit(); else GoBack(); return;
            case NavCommand.Up or NavCommand.Left: Move(items, -1); return;
            case NavCommand.Down or NavCommand.Right: Move(items, 1); return;
            case NavCommand.Confirm:
                if (_focus >= 0 && _focus < items.Count)
                {
                    if (items[_focus] is Button b) b.SendClicked();
                    else if (items[_focus].GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault() is { } tap) tap.Command?.Execute(tap.CommandParameter);
                }
                else Move(items, 1);
                return;
        }
    }

    void Move(List<View> items, int dir)
    {
        if (items.Count == 0) return;
        if (_focus >= 0 && _focus < items.Count) Highlight(items[_focus], false);
        _focus = _focus < 0 ? 0 : (_focus + dir + items.Count) % items.Count;
        Highlight(items[_focus], true);
    }

    static void Highlight(View v, bool on)
    {
        v.Scale = on ? 1.06 : 1.0;
        if (v is Button b) b.SetDynamicResource(Button.BorderColorProperty, on ? "Accent2" : "Border");
        if (v is Border br) br.SetDynamicResource(Border.StrokeProperty, on ? "Accent2" : "Border");
        if (on) v.Focus();
    }
}
