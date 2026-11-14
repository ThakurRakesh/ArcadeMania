using ArcadeMania.Core.Engine;
using ArcadeMania.Core.Games;

namespace ArcadeMania.Core.Catalog;

public sealed record BaseGame(string Key, string Title, GameCategory Category, string Icon, string Description, Func<GameBase> Factory);

public sealed record CatalogEntry(int Number, BaseGame Base, VariantSpec Variant)
{
    public string Title => $"{Base.Title}: {Variant.Flavor}";
    public string Subtitle => $"#{Number:00000} • {Base.Category} • Lv {Variant.Difficulty} • {Variant.Speed:0.0}x";
    public string Icon => Base.Icon;
    public GameBase Create(bool dark = true) { var g = Base.Factory(); g.Init(Variant, dark); return g; }
}

/// <summary>12 hand-built engines × deterministic variants = 10,000 playable games.</summary>
public static class GameCatalog
{
    public const int TotalGames = 10_000;

    public static readonly IReadOnlyList<BaseGame> Bases =
    [
        new("snake", "Snake", GameCategory.Arcade, "🐍", "Eat, grow, don't bite yourself.", () => new SnakeGame()),
        new("breaker", "Brick Breaker", GameCategory.Arcade, "🧱", "Smash every brick with the ball.", () => new BrickBreakerGame()),
        new("shooter", "Star Shooter", GameCategory.Arcade, "🚀", "Blast enemy waves, dodge their fire.", () => new SpaceShooterGame()),
        new("pong", "Paddle Duel", GameCategory.Arcade, "🏓", "Classic paddle tennis vs CPU.", () => new PongGame()),
        new("hopper", "Sky Hopper", GameCategory.Arcade, "🐦", "Flap through the gaps.", () => new SkyHopperGame()),
        new("blocks", "Block Drop", GameCategory.Arcade, "🟪", "Stack falling blocks, clear lines.", () => new BlockDropGame()),
        new("life", "Life Lab", GameCategory.Simulation, "🧬", "Cellular-automata sandbox with 8 rule sets.", () => new LifeLabGame()),
        new("lander", "Lunar Lander", GameCategory.Simulation, "🌙", "Physics landing with gravity and fuel.", () => new LanderGame()),
        new("farm", "Tiny Farm", GameCategory.Simulation, "🌾", "Plant, grow, harvest, buy land.", () => new TinyFarmGame()),
        new("eco", "Eco System", GameCategory.Simulation, "🦊", "Balance rabbits and foxes.", () => new EcoSimGame()),
        new("brawl", "Street Brawl", GameCategory.Fighting, "🥊", "1v1 hand-to-hand fighting, best of 3.", () => new StreetBrawlGame()),
        new("blade", "Blade Duel", GameCategory.Fighting, "⚔️", "Weapon duels: sword, spear, katana, knight.", () => new BladeDuelGame()),
    ];

    private static readonly Lazy<IReadOnlyList<CatalogEntry>> _all = new(Build);
    public static IReadOnlyList<CatalogEntry> All => _all.Value;

    private static IReadOnlyList<CatalogEntry> Build()
    {
        var list = new List<CatalogEntry>(TotalGames);
        // Interleave bases so the full list mixes genres; numbering is stable forever.
        for (int n = 0; n < TotalGames; n++)
        {
            var b = Bases[n % Bases.Count];
            list.Add(new CatalogEntry(n + 1, b, VariantSpec.Create(n + 1)));
        }
        return list;
    }

    public static int CountIn(GameCategory? cat) => cat is null ? TotalGames : All.Count(e => e.Base.Category == cat);

    public static IEnumerable<CatalogEntry> Query(GameCategory? category, string? search, string? baseKey = null)
    {
        IEnumerable<CatalogEntry> q = All;
        if (category is not null) q = q.Where(e => e.Base.Category == category);
        if (!string.IsNullOrWhiteSpace(baseKey)) q = q.Where(e => e.Base.Key == baseKey);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = int.TryParse(s.TrimStart('#'), out var num)
                ? q.Where(e => e.Number == num)
                : q.Where(e => e.Title.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        return q;
    }

    public static CatalogEntry Random(Random? r = null) => All[(r ?? System.Random.Shared).Next(TotalGames)];
}
