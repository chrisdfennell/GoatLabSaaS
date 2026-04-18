using System.Security.Cryptography;
using System.Text;

namespace GoatLab.Server.Services.ApiKeys;

// Static helpers for minting new API keys and hashing inbound ones. The
// plaintext form is "gl_" + 32 random bytes base64url-encoded. We store only
// SHA-256(plaintext) — so a stolen DB dump can't reconstruct live keys, and
// revocation is irreversible.
public static class ApiKeyGenerator
{
    public const string Prefix = "gl_";

    public static GeneratedKey Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = Prefix + Base64UrlEncode(bytes);
        var hash = HashPlaintext(plaintext);
        // 12-char display prefix so the UI can list keys without leaking them.
        var displayPrefix = plaintext.Substring(0, Math.Min(12, plaintext.Length));
        return new GeneratedKey(plaintext, displayPrefix, hash);
    }

    public static string HashPlaintext(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public record GeneratedKey(string Plaintext, string Prefix, string KeyHash);
