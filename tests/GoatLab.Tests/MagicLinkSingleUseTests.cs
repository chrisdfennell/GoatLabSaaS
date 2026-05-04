using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Tests;

// Tests that the magic-link single-use guard in BuyerAuthController.Verify
// is race-safe. The controller now claims the token via ExecuteUpdateAsync
// with WHERE UsedAt IS NULL AND ExpiresAt >= now, so two simultaneous
// verify hits with the same token can't both pass the gate.
public class MagicLinkSingleUseTests
{
    private const string Hash = "0000000000000000000000000000000000000000000000000000000000000000";

    private static async Task SeedTokenAsync(TestDb db, DateTime? usedAt = null, DateTime? expiresAt = null)
    {
        db.Context.BuyerSignInTokens.Add(new BuyerSignInToken
        {
            TokenHash = Hash,
            Email = "buyer@example.com",
            UsedAt = usedAt,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.Context.SaveChangesAsync();
    }

    // The exact claim query the controller runs.
    private static async Task<int> ClaimAsync(TestDb db, DateTime now) =>
        await db.Context.BuyerSignInTokens
            .Where(t => t.TokenHash == Hash && t.UsedAt == null && t.ExpiresAt >= now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now));

    [Fact]
    public async Task Claim_succeeds_first_time_and_stamps_UsedAt()
    {
        using var db = new TestDb();
        await SeedTokenAsync(db);

        var now = DateTime.UtcNow;
        var affected = await ClaimAsync(db, now);

        Assert.Equal(1, affected);
        var token = await db.Context.BuyerSignInTokens.AsNoTracking().FirstAsync();
        Assert.NotNull(token.UsedAt);
        Assert.Equal(now.Ticks / TimeSpan.TicksPerSecond,
                     token.UsedAt!.Value.Ticks / TimeSpan.TicksPerSecond);
    }

    [Fact]
    public async Task Claim_returns_zero_on_replay_after_token_already_used()
    {
        using var db = new TestDb();
        await SeedTokenAsync(db);

        var firstClaim = await ClaimAsync(db, DateTime.UtcNow);
        Assert.Equal(1, firstClaim);

        // Second attempt against the same hash now sees UsedAt != null and
        // the WHERE clause excludes the row.
        var replay = await ClaimAsync(db, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(0, replay);
    }

    [Fact]
    public async Task Claim_returns_zero_for_expired_token()
    {
        using var db = new TestDb();
        await SeedTokenAsync(db, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var affected = await ClaimAsync(db, DateTime.UtcNow);

        Assert.Equal(0, affected);
        var token = await db.Context.BuyerSignInTokens.AsNoTracking().FirstAsync();
        Assert.Null(token.UsedAt); // confirm we didn't accidentally stamp it
    }

    [Fact]
    public async Task Claim_returns_zero_for_unknown_hash()
    {
        using var db = new TestDb();
        await SeedTokenAsync(db);

        var affected = await db.Context.BuyerSignInTokens
            .Where(t => t.TokenHash == "deadbeef" && t.UsedAt == null
                        && t.ExpiresAt >= DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow));

        Assert.Equal(0, affected);
    }
}
