// Renders real 3840×2160 (4K UHD) frames from every game engine for the README.
// Usage: dotnet run -- <outputDir> [width] [height]
using ArcadeMania.Core.Catalog;
using ArcadeMania.Core.Engine;
using Microsoft.Maui.Graphics.Skia;

var outDir = args.Length > 0 ? args[0] : "shots";
int width = args.Length > 1 ? int.Parse(args[1]) : 3840, height = args.Length > 2 ? int.Parse(args[2]) : 2160;
Directory.CreateDirectory(outDir);

foreach (var b in GameCatalog.Bases)
{
    var entry = GameCatalog.All.First(e => e.Base.Key == b.Key && e.Variant.Difficulty is >= 3 and <= 6);
    var game = entry.Create(dark: true);
    var input = new InputState();
    var rnd = new Random(7);
    float t = 0; int restarts = 0;
    for (int f = 0; f < 60 * 6; f++, t += 1f / 60)
    {
        Drive(b.Key, input, rnd, f, t);
        game.Update(1f / 60, input);
        input.EndFrame();
        if (game.GameOver) { if (++restarts > 3) break; game.Restart(); f = 60 * 6 - 100; }
    }

    using var ctx = new SkiaBitmapExportContext(width, height, 1f);
    var c = ctx.Canvas;
    var s = Math.Min(width / GameBase.W, height / GameBase.H);
    c.Translate((width - GameBase.W * s) / 2, (height - GameBase.H * s) / 2);
    c.Scale(s, s);
    game.Draw(c);
    var path = Path.Combine(outDir, $"{b.Key}.png");
    using (var fs = File.Create(path)) ctx.WriteToStream(fs);
    Console.WriteLine($"{path}  #{entry.Number} {entry.Title}  score={game.Score}");
}

static void Drive(string key, InputState i, Random r, int f, float t)
{
    void Set(GameKey k, bool v) => i.Set(k, v);
    switch (key)
    {
        case "brawl" or "blade":
            Set(GameKey.Right, f % 120 < 50); Set(GameKey.A, f % 20 == 0); Set(GameKey.B, f % 45 == 0); break;
        case "hopper":
            Set(GameKey.A, f % 26 == 0); break;
        case "lander":
            Set(GameKey.Up, f % 10 < 6); break;
        case "farm":
            if (f % 15 == 0) { Set(GameKey.Right, true); } else Set(GameKey.Right, false);
            Set(GameKey.A, f % 15 == 5); Set(GameKey.B, f % 90 == 0); break;
        case "breaker" or "pong":
            i.PointerDown = true; i.PointerX = 800 + MathF.Sin(t * 2) * 500; i.PointerY = 450 + MathF.Sin(t * 3) * 300;
            Set(GameKey.A, f == 30); break;
        default:
            if (f % 12 == 0) foreach (var k in new[] { GameKey.Left, GameKey.Right, GameKey.Up, GameKey.Down }) Set(k, r.NextDouble() < 0.25);
            Set(GameKey.A, r.NextDouble() < 0.3); break;
    }
}
