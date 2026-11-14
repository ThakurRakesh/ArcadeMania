using ArcadeMania.Core.Catalog;
using ArcadeMania.Core.Engine;
using ArcadeMania.Core.Security;
using Microsoft.Maui.Graphics;

namespace ArcadeMania.Tests;

public class StartupCrashTests
{
    [Fact]
    public void Catalog_has_exactly_10000_games_with_unique_numbers()
    {
        Assert.Equal(10_000, GameCatalog.All.Count);
        Assert.Equal(10_000, GameCatalog.All.Select(e => e.Number).Distinct().Count());
        Assert.All(GameCatalog.All, e => Assert.False(string.IsNullOrWhiteSpace(e.Title)));
    }

    [Fact]
    public void Every_category_is_populated()
    {
        foreach (GameCategory c in Enum.GetValues<GameCategory>()) Assert.True(GameCatalog.CountIn(c) > 1000);
    }

    [Fact]
    public void Variants_are_deterministic()
    {
        Assert.Equal(VariantSpec.Create(1234), VariantSpec.Create(1234));
        Assert.NotEqual(VariantSpec.Create(1), VariantSpec.Create(2));
    }

    public static IEnumerable<object[]> SampleGames()
    {
        // every base engine × several variants (incl. extreme speed/difficulty)
        for (int n = 1; n <= 12 * 8; n++) yield return new object[] { n };
        yield return new object[] { 9_999 }; yield return new object[] { 10_000 };
    }

    [Theory, MemberData(nameof(SampleGames))]
    public void Game_starts_runs_and_draws_without_crashing(int number)
    {
        var entry = GameCatalog.All[number - 1];
        var game = entry.Create();
        var input = new InputState();
        var rnd = new Random(number);
        var keys = Enum.GetValues<GameKey>();
        var canvas = new PictureCanvas(0, 0, GameBase.W, GameBase.H);

        for (int frame = 0; frame < 1800; frame++)          // 30 seconds @ 60 fps of random mashing
        {
            if (frame % 6 == 0) foreach (var k in keys) input.Set(k, rnd.NextDouble() < 0.3);
            input.PointerDown = rnd.NextDouble() < 0.2; input.PointerTap = rnd.NextDouble() < 0.05;
            input.PointerX = (float)rnd.NextDouble() * GameBase.W; input.PointerY = (float)rnd.NextDouble() * GameBase.H;
            game.Update(1f / 60f, input);
            input.EndFrame();
            if (frame % 30 == 0) game.Draw(canvas);
            if (game.GameOver) { game.Draw(canvas); game.Restart(); }
        }
        Assert.True(game.Score >= 0);
    }

    [Fact]
    public void Huge_frame_spike_is_clamped()
    {
        var g = GameCatalog.All[0].Create();
        g.Update(10f, new InputState());   // app resumed after 10 s in background
        g.Draw(new PictureCanvas(0, 0, 10, 10));
    }

    [Fact]
    public void Search_and_filters_work()
    {
        Assert.Single(GameCatalog.Query(null, "#42"));
        Assert.All(GameCatalog.Query(GameCategory.Fighting, null).Take(50), e => Assert.Equal(GameCategory.Fighting, e.Base.Category));
        Assert.NotEmpty(GameCatalog.Query(null, "snake"));
        Assert.Empty(GameCatalog.Query(null, "#0"));
    }
}

public class SecurityTests
{
    [Fact]
    public void Aes256Gcm_roundtrip_and_tamper_detection()
    {
        var key = AesGcmCrypto.NewKey();
        var blob = AesGcmCrypto.Encrypt("hello"u8, key);
        Assert.Equal("hello"u8.ToArray(), AesGcmCrypto.Decrypt(blob, key));
        blob[^1] ^= 1;
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => AesGcmCrypto.Decrypt(blob, key));
    }

    [Fact]
    public void ConfigProtector_handles_enc_and_plain_values()
    {
        var key = AesGcmCrypto.NewKey();
        var enc = ConfigProtector.Protect("secret", key);
        Assert.StartsWith("enc:", enc);
        Assert.Equal("secret", ConfigProtector.Unprotect(enc, key));
        Assert.Equal("plain", ConfigProtector.Unprotect("plain", null));
        Assert.Throws<InvalidOperationException>(() => ConfigProtector.Unprotect(enc, null));
    }

    [Fact]
    public void Encrypted_save_store_persists_and_survives_corruption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ma-" + Guid.NewGuid());
        var keys = new InMemoryKeyProvider();
        var cache = new ScoreCache(new EncryptedSaveStore(dir, keys));
        Assert.True(cache.Submit(7, 500));
        Assert.False(cache.Submit(7, 100));
        Assert.Equal(500, new ScoreCache(new EncryptedSaveStore(dir, keys)).Best(7));
        Assert.DoesNotContain("500", File.ReadAllText(Path.Combine(dir, "save.dat")));
        File.WriteAllText(Path.Combine(dir, "save.dat"), "garbage");
        Assert.Equal(0, new ScoreCache(new EncryptedSaveStore(dir, keys)).Best(7));   // no crash
    }

    [Fact]
    public void Rate_limiter_blocks_floods_and_refills()
    {
        long t = 0; var rl = new RateLimiter(3, 1, () => t);
        Assert.True(rl.TryAcquire()); Assert.True(rl.TryAcquire()); Assert.True(rl.TryAcquire());
        Assert.False(rl.TryAcquire());
        t += 1000; Assert.True(rl.TryAcquire());
    }

    [Fact]
    public void BotGuard_flags_machine_perfect_rapid_fire()
    {
        var g = new BotGuard(); long t = 0;
        for (int k = 0; k < 40; k++) { g.AllowPress(t); t += 20; }
        Assert.True(g.Suspicious);
        var human = new BotGuard(); var r = new Random(1); t = 0;
        for (int k = 0; k < 40; k++) { human.AllowPress(t); t += 90 + r.Next(80); }
        Assert.False(human.Suspicious);
    }

    [Fact]
    public void Integrity_verifier_detects_modified_file()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "a.dll"), "original");
        var manifest = new Dictionary<string, string> { ["a.dll"] = IntegrityVerifier.Sha256File(Path.Combine(dir, "a.dll")) };
        Assert.True(IntegrityVerifier.Verify(dir, manifest).ok);
        File.WriteAllText(Path.Combine(dir, "a.dll"), "patched");
        Assert.Equal("a.dll", IntegrityVerifier.Verify(dir, manifest).failedFile);
    }
}

public class GamepadTests
{
    [Fact]
    public void Xbox_and_PlayStation_layouts_map_to_same_keys()
    {
        var input = new InputState(); var map = new GamepadMapper();
        map.Apply(new PadSnapshot(-0.9f, 0, false, false, false, false, true, false, false, false, false, false, false, false), input);
        Assert.True(input.Left); Assert.True(input.A); Assert.True(input.Tapped(GameKey.A));
        map.Apply(new PadSnapshot(0.1f, 0, false, true, false, false, false, true, false, false, false, true, false, false), input);
        Assert.False(input.Left); Assert.True(input.Down); Assert.True(input.B); Assert.True(map.BackPressed);
    }
}
