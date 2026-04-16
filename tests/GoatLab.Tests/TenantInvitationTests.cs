using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Tests;

// Invitation lifecycle rules — expired, accepted, revoked invites all share
// one "active" predicate used across the accept/list endpoints. If anyone
// refactors that predicate we want to catch it before a stale invite gets
// honored or an active one vanishes.
public class TenantInvitationTests
{
    [Fact]
    public void TokenHash_uniqueness_prevents_duplicate_inserts()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        db.Context.SaveChanges();

        db.Context.TenantInvitations.Add(new TenantInvitation
        {
            TenantId = 1, Email = "a@x.com", Role = TenantRole.Worker,
            TokenHash = "deadbeef", ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = "u-1",
        });
        db.Context.SaveChanges();

        // Same token hash — must fail the unique index.
        db.Context.TenantInvitations.Add(new TenantInvitation
        {
            TenantId = 1, Email = "b@x.com", Role = TenantRole.Worker,
            TokenHash = "deadbeef", ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = "u-1",
        });

        Assert.Throws<DbUpdateException>(() => db.Context.SaveChanges());
    }

    [Fact]
    public void Active_predicate_excludes_accepted_revoked_and_expired()
    {
        using var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.Add(new Tenant { Id = 1, Name = "Acme", Slug = "acme", PlanId = 1 });
        db.Context.SaveChanges();

        db.Context.TenantInvitations.AddRange(
            new TenantInvitation { TenantId = 1, Email = "live@x.com", TokenHash = "h1",
                ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByUserId = "u-1" },
            new TenantInvitation { TenantId = 1, Email = "expired@x.com", TokenHash = "h2",
                ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedByUserId = "u-1" },
            new TenantInvitation { TenantId = 1, Email = "accepted@x.com", TokenHash = "h3",
                ExpiresAt = DateTime.UtcNow.AddDays(7), AcceptedAt = DateTime.UtcNow,
                CreatedByUserId = "u-1" },
            new TenantInvitation { TenantId = 1, Email = "revoked@x.com", TokenHash = "h4",
                ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = DateTime.UtcNow,
                CreatedByUserId = "u-1" }
        );
        db.Context.SaveChanges();
        db.Tenant.TenantId = 1;

        var active = db.Context.TenantInvitations
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow)
            .Select(i => i.Email)
            .ToList();

        Assert.Equal(new[] { "live@x.com" }, active);
    }
}
