using GoatLab.Server.Services.ApiKeys;

namespace GoatLab.Tests;

public class ApiKeyGeneratorTests
{
    [Fact]
    public void Generated_key_has_gl_prefix_and_base64url_body()
    {
        var k = ApiKeyGenerator.Generate();
        Assert.StartsWith("gl_", k.Plaintext);
        // Base64url alphabet — no '+' or '/' or trailing '='.
        Assert.DoesNotContain('+', k.Plaintext);
        Assert.DoesNotContain('/', k.Plaintext);
        Assert.DoesNotContain('=', k.Plaintext);
        // 32 bytes → 43 base64url chars + "gl_" = 46.
        Assert.Equal(46, k.Plaintext.Length);
    }

    [Fact]
    public void Generated_keys_are_unique_across_calls()
    {
        var a = ApiKeyGenerator.Generate();
        var b = ApiKeyGenerator.Generate();
        Assert.NotEqual(a.Plaintext, b.Plaintext);
        Assert.NotEqual(a.KeyHash, b.KeyHash);
    }

    [Fact]
    public void HashPlaintext_is_deterministic_and_64_hex_chars()
    {
        var hash1 = ApiKeyGenerator.HashPlaintext("gl_example");
        var hash2 = ApiKeyGenerator.HashPlaintext("gl_example");
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public void Prefix_is_12_chars_and_matches_plaintext_start()
    {
        var k = ApiKeyGenerator.Generate();
        Assert.Equal(12, k.Prefix.Length);
        Assert.StartsWith(k.Prefix, k.Plaintext);
    }

    [Fact]
    public void Generate_hash_matches_independent_hash_of_plaintext()
    {
        var k = ApiKeyGenerator.Generate();
        Assert.Equal(k.KeyHash, ApiKeyGenerator.HashPlaintext(k.Plaintext));
    }
}
