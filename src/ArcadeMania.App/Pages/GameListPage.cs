using ArcadeMania.App.Services;
using ArcadeMania.Core.Catalog;
using ArcadeMania.Core.Engine;

namespace ArcadeMania.App.Pages;

/// <summary>Virtualised list of up to 10,000 games with search and per-game-type filter.</summary>
public sealed class GameListPage : BasePage
{
    readonly GameCategory? _category;
    readonly CollectionView _list;
    readonly SearchBar _search;
    readonly Label _count;
    readonly HorizontalStackLayout _chips = new() { Spacing = 8, Padding = new Thickness(12, 4) };
    string? _baseKey;
    List<CatalogEntry> _items = new();
    int _selected = -1;
    CancellationTokenSource? _debounce;

    public GameListPage(GameCategory? category)
    {
        _category = category;
        _search = new SearchBar { Placeholder = "Search name or #number…", FontFamily = Theme.Body };
        _search.SetDynamicResource(SearchBar.TextColorProperty, "Text");
        _search.SetDynamicResource(SearchBar.PlaceholderColorProperty, "Muted");
        _search.TextChanged += (_, _) => Debounced();

        _count = new Label { FontFamily = Theme.Body, FontSize = 13, Margin = new Thickness(16, 0) };
        _count.SetDynamicResource(Label.TextColorProperty, "Muted");

        _chips.Add(Chip("All types", null));
        foreach (var b in GameCatalog.Bases.Where(b => category is null || b.Category == category)) _chips.Add(Chip($"{b.Icon} {b.Title}", b.Key));

        _list = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new GridItemsLayout(1, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = 10, VerticalItemSpacing = 10 },
            ItemTemplate = new DataTemplate(BuildCell), Margin = new Thickness(12, 0)
        };
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is CatalogEntry entry && !_keyboardSelecting)
            { _list.SelectedItem = null; await Navigation.PushAsync(new GamePage(entry)); }
        };
        _list.SizeChanged += (_, _) =>
        {   // responsive columns: phone 1, tablet 2-3, desktop / 4K 4-6
            if (_list.ItemsLayout is GridItemsLayout g) g.Span = Math.Clamp((int)(_list.Width / 340), 1, 6);
        };

        Content = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children =
            {
                Header(category?.ToString() ?? "All Games"),
                _search.Row(1),
                new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _chips }.Row(2),
                _count.Row(3),
                _list.Row(4)
            }
        };
        Refresh();
    }

    Button Chip(string text, string? key)
    {
        var b = Btn(text, () => { _baseKey = key; Refresh(); }, small: true);
        return b;
    }

    View BuildCell()
    {
        var icon = new Label { FontSize = 34, VerticalOptions = LayoutOptions.Center };
        icon.SetBinding(Label.TextProperty, nameof(CatalogEntry.Icon));
        var title = new Label { FontFamily = Theme.Body, FontSize = 17, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
        title.SetBinding(Label.TextProperty, nameof(CatalogEntry.Title));
        title.SetDynamicResource(Label.TextColorProperty, "Text");
        var sub = new Label { FontFamily = Theme.Body, FontSize = 13 };
        sub.SetBinding(Label.TextProperty, nameof(CatalogEntry.Subtitle));
        sub.SetDynamicResource(Label.TextColorProperty, "Muted");
        var best = new Label { FontFamily = Theme.Body, FontSize = 13 };
        best.SetBinding(Label.TextProperty, new Binding(nameof(CatalogEntry.Number), converter: new BestScoreConverter()));
        best.SetDynamicResource(Label.TextColorProperty, "Accent2");

        var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 14 };
        grid.Add(icon, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { title, sub, best } }, 1);
        var border = new Border { Content = grid, Padding = new Thickness(14, 12), StrokeThickness = 1.2, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 } };
        border.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        border.SetDynamicResource(Border.StrokeProperty, "Border");
        PointerHover.Attach(border);
        return border;
    }

    void Debounced()
    {
        _debounce?.Cancel(); _debounce = new();
        var token = _debounce.Token;
        Task.Delay(250, token).ContinueWith(t => { if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(Refresh); }, TaskScheduler.Default);
    }

    void Refresh()
    {
        try
        {
            _items = GameCatalog.Query(_category, _search.Text, _baseKey).ToList();
            _list.ItemsSource = _items;
            _count.Text = $"{_items.Count:N0} games";
            _selected = -1;
        }
        catch { _items = new(); _list.ItemsSource = _items; }
    }

    protected override void OnAppearing() { base.OnAppearing(); if (_items.Count > 0) _list.ItemsSource = _items.ToList(); }

    // Controller: Up/Down walks the list, A (✕) plays, Left/Right cycles game type, B (○) goes back.
    bool _keyboardSelecting;
    protected override void HandleNav(NavCommand cmd)
    {
        if (_items.Count == 0 || cmd is NavCommand.Back or NavCommand.Start) { base.HandleNav(cmd); return; }
        var span = (_list.ItemsLayout as GridItemsLayout)?.Span ?? 1;
        switch (cmd)
        {
            case NavCommand.Down: Select(_selected + span); break;
            case NavCommand.Up: Select(_selected - span); break;
            case NavCommand.Right: Select(_selected + 1); break;
            case NavCommand.Left: Select(_selected - 1); break;
            case NavCommand.Confirm:
                if (_selected >= 0) Navigation.PushAsync(new GamePage(_items[_selected]));
                else Select(0);
                break;
        }
    }

    void Select(int index)
    {
        _selected = Math.Clamp(index, 0, _items.Count - 1);
        _keyboardSelecting = true;
        _list.SelectedItem = _items[_selected];
        _keyboardSelecting = false;
        _list.ScrollTo(_selected, position: ScrollToPosition.MakeVisible, animate: false);
    }

    sealed class BestScoreConverter : IValueConverter
    {
        public object? Convert(object? value, Type t, object? p, System.Globalization.CultureInfo c) =>
            value is int n && AppServices.Scores.Best(n) is var b and > 0 ? $"🏆 Best {b:N0}" : "Not played yet";
        public object? ConvertBack(object? value, Type t, object? p, System.Globalization.CultureInfo c) => null;
    }
}
