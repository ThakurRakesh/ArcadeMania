using ArcadeMania.Core.Security;

namespace ArcadeMania.App.Services;

/// <summary>Per-install AES-256 key held in the OS keystore (Android Keystore / Windows DPAPI / iOS Keychain).</summary>
public sealed class SecureStorageKeyProvider : IKeyProvider
{
    const string Name = "arcademania.save.key.v1";
    byte[]? _cached;

    public byte[] GetOrCreateKey()
    {
        if (_cached is not null) return _cached;
        try
        {
            var existing = SecureStorage.Default.GetAsync(Name).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(existing)) return _cached = Convert.FromBase64String(existing);
            var key = AesGcmCrypto.NewKey();
            SecureStorage.Default.SetAsync(Name, Convert.ToBase64String(key)).GetAwaiter().GetResult();
            return _cached = key;
        }
        catch
        {
            // Keystore unavailable (e.g. reset device credentials): session-only key, saves start fresh.
            return _cached = AesGcmCrypto.NewKey();
        }
    }
}

public static class AppServices
{
    static ScoreCache? _scores;
    public static ScoreCache Scores => _scores ??= Create();

    static ScoreCache Create()
    {
        try { return new ScoreCache(new EncryptedSaveStore(Path.Combine(FileSystem.AppDataDirectory, "saves"), new SecureStorageKeyProvider())); }
        catch { return new ScoreCache(null); }
    }

    public static void ExitApp()
    {
#if ANDROID
        Platform.CurrentActivity?.FinishAffinity();
#else
        Application.Current?.Quit();
#endif
    }
}
