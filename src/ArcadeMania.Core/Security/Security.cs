using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcadeMania.Core.Security;

/// <summary>AES-256-GCM authenticated encryption (military-grade). Layout: nonce(12) | tag(16) | ciphertext.</summary>
public static class AesGcmCrypto
{
    public const int KeySize = 32, NonceSize = 12, TagSize = 16;

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySize);

    public static byte[] Encrypt(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new CryptographicException("AES-256 key must be 32 bytes.");
        var output = new byte[NonceSize + TagSize + plain.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return output;
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> blob, ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new CryptographicException("AES-256 key must be 32 bytes.");
        if (blob.Length < NonceSize + TagSize) throw new CryptographicException("Ciphertext too short.");
        var plain = new byte[blob.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(blob[..NonceSize], blob[(NonceSize + TagSize)..], blob.Slice(NonceSize, TagSize), plain);
        return plain;
    }

    public static string EncryptString(string s, ReadOnlySpan<byte> key) => Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(s), key));
    public static string DecryptString(string b64, ReadOnlySpan<byte> key) => Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(b64), key));
}

/// <summary>Config values prefixed "enc:" are AES-256-GCM ciphertext; the key comes from the
/// ARCADEMANIA_MASTER_KEY environment variable (base64, 32 bytes) and is never committed. Plain values pass through (dev).</summary>
public static class ConfigProtector
{
    public const string Prefix = "enc:", KeyEnvVar = "ARCADEMANIA_MASTER_KEY";

    public static byte[]? KeyFromEnvironment()
    {
        var v = Environment.GetEnvironmentVariable(KeyEnvVar);
        if (string.IsNullOrWhiteSpace(v)) return null;
        try { var k = Convert.FromBase64String(v); return k.Length == AesGcmCrypto.KeySize ? k : null; } catch { return null; }
    }

    public static string Protect(string plain, byte[] key) => Prefix + AesGcmCrypto.EncryptString(plain, key);

    public static string Unprotect(string value, byte[]? key)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        if (key is null) throw new InvalidOperationException($"Encrypted config value found but {KeyEnvVar} is not set.");
        return AesGcmCrypto.DecryptString(value[Prefix.Length..], key);
    }
}

/// <summary>Supplies the per-install AES key (MAUI: OS keystore via SecureStorage; tests: in-memory).</summary>
public interface IKeyProvider { byte[] GetOrCreateKey(); }

public sealed class InMemoryKeyProvider : IKeyProvider
{
    private readonly byte[] _key = AesGcmCrypto.NewKey();
    public byte[] GetOrCreateKey() => _key;
}

/// <summary>Local, in-house, encrypted-at-rest storage for saves and best scores (no cloud, no paid services).</summary>
public sealed class EncryptedSaveStore
{
    private readonly string _path; private readonly IKeyProvider _keys; private readonly object _io = new();

    public EncryptedSaveStore(string directory, IKeyProvider keys)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "save.dat"); _keys = keys;
    }

    public Dictionary<string, int> Load()
    {
        lock (_io)
        {
            try
            {
                if (!File.Exists(_path)) return new();
                var json = AesGcmCrypto.Decrypt(File.ReadAllBytes(_path), _keys.GetOrCreateKey());
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
            }
            catch { return new(); }   // tampered/corrupt save → start fresh, never crash
        }
    }

    public void Save(IReadOnlyDictionary<string, int> data)
    {
        lock (_io)
        {
            var blob = AesGcmCrypto.Encrypt(JsonSerializer.SerializeToUtf8Bytes(data), _keys.GetOrCreateKey());
            var tmp = _path + ".tmp";
            File.WriteAllBytes(tmp, blob);
            File.Move(tmp, _path, overwrite: true);   // atomic replace
        }
    }
}

/// <summary>In-memory cache of best scores with write-through to the encrypted store.</summary>
public sealed class ScoreCache
{
    private readonly ConcurrentDictionary<string, int> _best;
    private readonly EncryptedSaveStore? _store;
    public ScoreCache(EncryptedSaveStore? store) { _store = store; _best = new(store?.Load() ?? new()); }

    public int Best(int gameNumber) => _best.TryGetValue(Key(gameNumber), out var v) ? v : 0;
    public int GamesPlayed => _best.Count(k => k.Key.StartsWith("g:"));

    public bool Submit(int gameNumber, int score)
    {
        if (score <= Best(gameNumber)) return false;
        _best[Key(gameNumber)] = score;
        try { _store?.Save(_best); } catch { /* storage full etc. — keep in memory, never crash */ }
        return true;
    }

    private static string Key(int n) => $"g:{n}";
}

/// <summary>Token-bucket rate limiter. Used to throttle input floods from macro/bot tools and score submissions.</summary>
public sealed class RateLimiter
{
    private readonly int _capacity; private readonly double _perSecond; private double _tokens; private long _last;
    private readonly Func<long> _clockMs;
    public RateLimiter(int capacity, double refillPerSecond, Func<long>? clockMs = null)
    { _capacity = capacity; _perSecond = refillPerSecond; _tokens = capacity; _clockMs = clockMs ?? (() => Environment.TickCount64); _last = _clockMs(); }

    public bool TryAcquire()
    {
        lock (this)
        {
            var now = _clockMs();
            _tokens = Math.Min(_capacity, _tokens + (now - _last) / 1000.0 * _perSecond); _last = now;
            if (_tokens < 1) return false;
            _tokens -= 1; return true;
        }
    }
}

/// <summary>Bot-interference guard: humans can't press buttons faster than ~30/s with perfectly regular timing.</summary>
public sealed class BotGuard
{
    private readonly RateLimiter _limiter = new(capacity: 40, refillPerSecond: 30);
    private readonly Queue<long> _intervals = new(); private long _lastPress = -1;
    public bool Suspicious { get; private set; }

    public bool AllowPress(long nowMs)
    {
        if (_lastPress >= 0) { _intervals.Enqueue(nowMs - _lastPress); if (_intervals.Count > 30) _intervals.Dequeue(); }
        _lastPress = nowMs;
        if (_intervals.Count == 30)
        {
            var avg = _intervals.Average(); var variance = _intervals.Average(v => (v - avg) * (v - avg));
            Suspicious = avg < 60 && variance < 4;     // machine-perfect rapid fire
        }
        return _limiter.TryAcquire() && !Suspicious;
    }
}

/// <summary>Anti-tamper: compares SHA-256 of shipped binaries with a signed-at-build manifest. On mismatch the app refuses to run and exits cleanly.</summary>
public static class IntegrityVerifier
{
    public static string Sha256File(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }

    /// <param name="manifest">file name → expected SHA-256 hex.</param>
    public static (bool ok, string? failedFile) Verify(string baseDir, IReadOnlyDictionary<string, string> manifest)
    {
        foreach (var (file, expected) in manifest)
        {
            var p = Path.Combine(baseDir, file);
            if (!File.Exists(p) || !string.Equals(Sha256File(p), expected, StringComparison.OrdinalIgnoreCase)) return (false, file);
        }
        return (true, null);
    }
}
