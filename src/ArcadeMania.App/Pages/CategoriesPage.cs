using ArcadeMania.App.Services;
using ArcadeMania.Core.Catalog;
using ArcadeMania.Core.Engine;

namespace ArcadeMania.App.Pages;

public sealed class CategoriesPage : BasePage
{
    public CategoriesPage() => Build();
    void Build()
    {
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center, Padding = 16 };
        flex.Add(Tile("🕹️", "Arcade", GameCategory.Arcade));
        flex.Add(Tile("🧪", "Simulation", GameCategory.Simulation));
        flex.Add(Tile("🥋", "Fighting", GameCategory.Fighting));
        flex.Add(Tile("🌐", "All Games", null));

        Content = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            Children = { Header("Choose a category"), new ScrollView { Content = flex }.Row(1) }
        };
    }

    View Tile(string icon, string name, GameCategory? cat)
    {
        var bases = GameCatalog.Bases.Where(b => cat is null || b.Category == cat).Select(b => $"{b.Icon} {b.Title}");
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = icon, FontSize = 54, HorizontalTextAlignment = TextAlignment.Center },
                Lbl(name, 30, "Text", Theme.Heading, bold: true),
                Lbl($"{GameCatalog.CountIn(cat):N0} games", 16, "Accent2"),
                Lbl(string.Join("  •  ", bases), 13, "Muted"),
            }
        };
        var border = new Border
        {
            Content = stack, Padding = 22, Margin = 10, WidthRequest = 300, MinimumHeightRequest = 240, StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 }
        };
        border.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        border.SetDynamicResource(Border.StrokeProperty, "Border");
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Navigation.PushAsync(new GameListPage(cat))) });
        PointerHover.Attach(border);
        RegisterFocusable(border);
        return border;
    }

    static Label Lbl(string t, double size, string colorKey, string font = Theme.Body, bool bold = false)
    {
        var l = new Label { Text = t, FontSize = size, FontFamily = font, HorizontalTextAlignment = TextAlignment.Center, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
        l.SetDynamicResource(Label.TextColorProperty, colorKey);
        return l;
    }
}
