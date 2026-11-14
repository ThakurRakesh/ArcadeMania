using ArcadeMania.App.Services;
using ArcadeMania.Core.Catalog;

namespace ArcadeMania.App.Pages;

/// <summary>Start screen — no login. PLAY → Categories → pick a game.</summary>
public sealed class PlayPage : BasePage
{
    public PlayPage() { IsRoot = true; Build(); }

    void Build()
    {
        var title = new Label { Text = "ArcadeMania", FontFamily = Theme.Heading, FontSize = 72, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        title.SetDynamicResource(Label.TextColorProperty, "Accent2");
        var sub = new Label { Text = $"{GameCatalog.TotalGames:N0} games in 1 • Arcade • Simulation • Fighting", FontFamily = Theme.Body, FontSize = 18, HorizontalTextAlignment = TextAlignment.Center };
        sub.SetDynamicResource(Label.TextColorProperty, "Muted");

        var play = Btn("▶  PLAY", async () => await Navigation.PushAsync(new CategoriesPage()), primary: true);
        play.FontSize = 34; play.Padding = new Thickness(70, 22); play.HorizontalOptions = LayoutOptions.Center;

        var random = Btn("🎲  Random game", async () => await Navigation.PushAsync(new GamePage(GameCatalog.Random())));
        random.HorizontalOptions = LayoutOptions.Center;

        var pad = new Label { Text = "🎮 Xbox / PlayStation controllers, keyboard and touch supported", FontFamily = Theme.Body, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center };
        pad.SetDynamicResource(Label.TextColorProperty, "Muted");

        var played = new Label { Text = AppServices.Scores.GamesPlayed > 0 ? $"Best scores saved for {AppServices.Scores.GamesPlayed} games" : "", FontFamily = Theme.Body, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center };
        played.SetDynamicResource(Label.TextColorProperty, "Muted");

        Content = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            Children =
            {
                Header("", showBackHome: false),
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 22, Padding = new Thickness(20, 40), VerticalOptions = LayoutOptions.Center,
                        Children = { title, sub, new BoxView { HeightRequest = 20, Color = Colors.Transparent }, play, random, pad, played }
                    }
                }.Row(1)
            }
        };
    }
}

static class ViewExt
{
    public static T Row<T>(this T v, int row) where T : BindableObject { Grid.SetRow(v, row); return v; }
}
