using System.Net;
using GoatLab.Server.Services.Transfers;
using GoatLab.Server.Services.Webhooks;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

public class GoatTransferServiceTests
{
    private const int SellerTenantId = 1;
    private const int BuyerTenantId = 2;

    private static (TestDb db, GoatTransferService svc, CapturingEmailSender email) SetupFixture()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.AddRange(
            new Tenant { Id = SellerTenantId, Name = "Seller Farm", Slug = "seller", PlanId = 3 },
            new Tenant { Id = BuyerTenantId, Name = "Buyer Farm", Slug = "buyer", PlanId = 3 });
        db.Context.SaveChanges();
        db.Tenant.TenantId = SellerTenantId;
        var email = new CapturingEmailSender();
        var svc = new GoatTransferService(db.Context, db.Tenant, email, NullLogger<GoatTransferService>.Instance);
        return (db, svc, email);
    }

    private static int AddGoat(TestDb db, int tenantId, string name, int? sireId = null, int? damId = null)
    {
        var g = new Goat { TenantId = tenantId, Name = name, Gender = Gender.Female, SireId = sireId, DamId = damId };
        db.Context.Goats.Add(g);
        db.Context.SaveChanges();
        return g.Id;
    }

    [Fact]
    public async Task Initiate_creates_pending_row_emails_buyer_and_returns_token()
    {
        var (db, svc, email) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");

            var result = await svc.InitiateAsync(goatId, "buyer@example.com", "First litter", expiryDays: 7,
                sellerUserId: "seller-user-id", origin: "https://goatlab.app", ct: default);

            Assert.NotNull(result);
            Assert.StartsWith("gt_", result!.PlaintextToken);
            Assert.StartsWith("https://goatlab.app/transfer/", result.AcceptUrl);

            var transfer = db.Context.GoatTransfers.Single();
            Assert.Equal(GoatTransferStatus.Pending, transfer.Status);
            Assert.Equal("buyer@example.com", transfer.BuyerEmail);
            Assert.Equal(goatId, transfer.GoatId);
            Assert.Equal(SellerTenantId, transfer.FromTenantId);
            Assert.True(transfer.ExpiresAt > DateTime.UtcNow.AddDays(6));

            // Buyer got the email (SMTP isn't configured in tests → CapturingEmailSender catches it).
            var sent = Assert.Single(email.Sent);
            Assert.Equal("buyer@example.com", sent.To);
            Assert.Contains("Bella", sent.Subject);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Initiate_twice_for_same_goat_throws()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            await svc.InitiateAsync(goatId, "a@example.com", null, null, "seller", "https://x", default);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default));
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Accept_creates_seller_alert_and_moves_lactation_records()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var lactation = new Lactation
            {
                TenantId = SellerTenantId, GoatId = goatId,
                FreshenDate = DateTime.UtcNow.AddMonths(-3),
                LactationNumber = 2,
            };
            db.Context.Lactations.Add(lactation);
            db.Context.SaveChanges();
            db.Context.MilkTestDays.Add(new MilkTestDay
            {
                TenantId = SellerTenantId,
                LactationId = lactation.Id,
                TestDate = DateTime.UtcNow.AddDays(-7),
                TotalLbs = 8.2,
            });
            db.Context.TenantMembers.Add(new TenantMember
            {
                TenantId = BuyerTenantId, UserId = "buyer", Role = TenantRole.Owner,
            });
            db.Context.SaveChanges();

            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null,
                sellerUserId: "seller", origin: "https://x", ct: default);

            db.Tenant.TenantId = BuyerTenantId;
            await svc.AcceptAsync(init!.PlaintextToken, BuyerTenantId, "buyer", default);

            db.Context.ChangeTracker.Clear();

            // Lactation + MilkTestDay moved with the goat.
            var lac = db.Context.Lactations.IgnoreQueryFilters().Single(l => l.GoatId == goatId);
            Assert.Equal(BuyerTenantId, lac.TenantId);
            var testDay = db.Context.MilkTestDays.IgnoreQueryFilters().Single();
            Assert.Equal(BuyerTenantId, testDay.TenantId);

            // Seller got an in-app Alert.
            var alert = db.Context.Alerts.IgnoreQueryFilters().Single();
            Assert.Equal(SellerTenantId, alert.TenantId);
            Assert.Equal(AlertType.GoatTransferAccepted, alert.Type);
            Assert.Equal("GoatTransfer", alert.EntityType);
            Assert.Equal("/account/transfers", alert.DeepLink);
            Assert.Contains("Bella", alert.Title);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Resend_rotates_token_and_sends_fresh_email()
    {
        var (db, svc, email) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, expiryDays: 14,
                sellerUserId: "seller", origin: "https://x", ct: default);

            var originalHash = db.Context.GoatTransfers.Single().TokenHash;
            email.Sent.Clear(); // drop the initial invite so we only count the resend

            var ok = await svc.ResendAsync(init!.TransferId, "https://x", default);
            Assert.True(ok);

            db.Context.ChangeTracker.Clear();
            var transfer = db.Context.GoatTransfers.Single();
            Assert.NotEqual(originalHash, transfer.TokenHash); // token rotated

            // One invite email was sent on resend.
            var sent = Assert.Single(email.Sent);
            Assert.Equal("b@example.com", sent.To);
            Assert.Contains("Bella", sent.Subject);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Resend_rejects_non_pending_transfers()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default);

            // Cancel first; now it's in a terminal state.
            await svc.CancelAsync(init!.TransferId, default);

            var ok = await svc.ResendAsync(init.TransferId, "https://x", default);
            Assert.False(ok);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Accept_moves_goat_and_records_to_buyer_tenant_and_creates_pedigree_stubs()
    {
        var (db, svc, email) = SetupFixture();
        try
        {
            // Source pedigree: sire + dam + goat with some medical/weight/photo history.
            var sireId = AddGoat(db, SellerTenantId, "OldSire");
            var damId = AddGoat(db, SellerTenantId, "OldDam");
            var goatId = AddGoat(db, SellerTenantId, "Bella", sireId: sireId, damId: damId);

            db.Context.MedicalRecords.Add(new MedicalRecord
            {
                TenantId = SellerTenantId, GoatId = goatId,
                Title = "CDT booster", Date = DateTime.UtcNow.AddDays(-30),
            });
            db.Context.WeightRecords.Add(new WeightRecord
            {
                TenantId = SellerTenantId, GoatId = goatId,
                Weight = 42.5, Date = DateTime.UtcNow.AddDays(-10),
            });
            db.Context.GoatPhotos.Add(new GoatPhoto
            {
                TenantId = SellerTenantId, GoatId = goatId,
                FilePath = "media/photo.jpg", UploadedAt = DateTime.UtcNow,
            });
            db.Context.SaveChanges();

            // Buyer must be a member of BuyerTenantId for accept to succeed.
            db.Context.TenantMembers.Add(new TenantMember
            {
                TenantId = BuyerTenantId,
                UserId = "buyer-user-id",
                Role = TenantRole.Owner,
            });
            db.Context.SaveChanges();

            var init = await svc.InitiateAsync(goatId, "buyer@example.com", null, null,
                sellerUserId: "seller-user-id", origin: "https://x", ct: default);
            Assert.NotNull(init);

            // Flip caller-side context to the buyer before accept (matches real request flow).
            db.Tenant.TenantId = BuyerTenantId;

            var accept = await svc.AcceptAsync(init!.PlaintextToken, BuyerTenantId, "buyer-user-id", default);
            Assert.NotNull(accept);

            // ExecuteUpdateAsync bypasses the EF change tracker, so tracked
            // entities still carry the old TenantId — clear the tracker so
            // subsequent reads come fresh from the DB.
            db.Context.ChangeTracker.Clear();

            // 1. The goat moved to the buyer.
            var moved = db.Context.Goats.IgnoreQueryFilters().Single(g => g.Id == goatId);
            Assert.Equal(BuyerTenantId, moved.TenantId);

            // 2. Sire + Dam stubs were created as IsExternal in the buyer's tenant, and
            //    the goat's pedigree FKs now point at those stubs — not the originals.
            Assert.NotEqual(sireId, moved.SireId);
            Assert.NotEqual(damId, moved.DamId);
            var buyerSire = db.Context.Goats.IgnoreQueryFilters().Single(g => g.Id == moved.SireId);
            Assert.Equal(BuyerTenantId, buyerSire.TenantId);
            Assert.True(buyerSire.IsExternal);
            Assert.Equal("OldSire", buyerSire.Name);

            // 3. Original sire + dam are untouched on the seller side.
            var origSire = db.Context.Goats.IgnoreQueryFilters().Single(g => g.Id == sireId);
            Assert.Equal(SellerTenantId, origSire.TenantId);

            // 4. Child records moved too.
            var med = db.Context.MedicalRecords.IgnoreQueryFilters().Single(r => r.GoatId == goatId);
            Assert.Equal(BuyerTenantId, med.TenantId);
            var wt = db.Context.WeightRecords.IgnoreQueryFilters().Single(r => r.GoatId == goatId);
            Assert.Equal(BuyerTenantId, wt.TenantId);
            var photo = db.Context.GoatPhotos.IgnoreQueryFilters().Single(p => p.GoatId == goatId);
            Assert.Equal(BuyerTenantId, photo.TenantId);

            // 5. Transfer is marked accepted.
            var transfer = db.Context.GoatTransfers.Single();
            Assert.Equal(GoatTransferStatus.Accepted, transfer.Status);
            Assert.Equal(BuyerTenantId, transfer.ToTenantId);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Accept_fails_when_buyer_not_member_of_destination()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default);

            // Note: no TenantMember row added — buyer is NOT a member of BuyerTenantId.
            db.Tenant.TenantId = BuyerTenantId;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.AcceptAsync(init!.PlaintextToken, BuyerTenantId, "outsider-user-id", default));

            // Goat should not have moved.
            var stillThere = db.Context.Goats.IgnoreQueryFilters().Single(g => g.Id == goatId);
            Assert.Equal(SellerTenantId, stillThere.TenantId);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Accept_fails_when_buyer_plan_is_at_goat_cap()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            // Homestead plan (id 1 in the seed) caps at 10 goats. Put the buyer on
            // Homestead and fill them up — the transfer should be rejected with
            // the upgrade-required message instead of silently succeeding.
            var buyer = db.Context.Tenants.Single(t => t.Id == BuyerTenantId);
            buyer.PlanId = 1; // Homestead cap=10

            for (var i = 0; i < 10; i++)
            {
                db.Context.Goats.Add(new Goat
                {
                    TenantId = BuyerTenantId,
                    Name = $"Filler{i}",
                    Gender = Gender.Female,
                });
            }
            db.Context.TenantMembers.Add(new TenantMember
            {
                TenantId = BuyerTenantId, UserId = "buyer", Role = TenantRole.Owner,
            });
            db.Context.SaveChanges();

            var goatId = AddGoat(db, SellerTenantId, "Incoming");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default);

            db.Tenant.TenantId = BuyerTenantId;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.AcceptAsync(init!.PlaintextToken, BuyerTenantId, "buyer", default));

            Assert.Contains("10-goat limit", ex.Message);
            Assert.Contains("Upgrade", ex.Message);

            // Goat stays with the seller.
            var stillThere = db.Context.Goats.IgnoreQueryFilters().Single(g => g.Id == goatId);
            Assert.Equal(SellerTenantId, stillThere.TenantId);

            // Transfer stays Pending — the buyer can retry after upgrading.
            var transfer = db.Context.GoatTransfers.Single();
            Assert.Equal(GoatTransferStatus.Pending, transfer.Status);
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Accept_dispatches_webhooks_to_both_tenants()
    {
        var db = new TestDb();
        db.SeedDefaultPlans();
        db.Context.Tenants.AddRange(
            new Tenant { Id = SellerTenantId, Name = "Seller Farm", Slug = "seller", PlanId = 3 },
            new Tenant { Id = BuyerTenantId, Name = "Buyer Farm", Slug = "buyer", PlanId = 3 });
        db.Context.SaveChanges();
        try
        {
            // Seller subscribes to both initiated + accepted; buyer only to accepted
            // (buyer has no reason to hear about initiate since it's not on their side).
            db.Context.Webhooks.Add(new Webhook
            {
                TenantId = SellerTenantId, Name = "Seller hook",
                Url = "https://seller.test/hook", Secret = "s1",
                Events = "goat.transfer.initiated,goat.transfer.accepted", IsActive = true,
            });
            db.Context.Webhooks.Add(new Webhook
            {
                TenantId = BuyerTenantId, Name = "Buyer hook",
                Url = "https://buyer.test/hook", Secret = "s2",
                Events = "goat.transfer.accepted", IsActive = true,
            });
            db.Context.TenantMembers.Add(new TenantMember
            {
                TenantId = BuyerTenantId, UserId = "buyer", Role = TenantRole.Owner,
            });
            db.Context.SaveChanges();

            // Stub HTTP factory — both tenants' webhooks hit this, we count calls.
            var hookCalls = new List<string>();
            var http = new StubHttpClientFactory((req, _) =>
            {
                hookCalls.Add(req.RequestUri!.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok"),
                });
            });

            db.Tenant.TenantId = SellerTenantId;
            var webhooks = new WebhookDispatcher(db.Context, db.Tenant, http, NullLogger<WebhookDispatcher>.Instance);
            var email = new CapturingEmailSender();
            var svc = new GoatTransferService(
                db.Context, db.Tenant, email,
                NullLogger<GoatTransferService>.Instance, webhooks);

            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default);

            // Initiate fires one webhook (to seller).
            Assert.Single(hookCalls, u => u.Contains("seller.test"));
            Assert.DoesNotContain(hookCalls, u => u.Contains("buyer.test"));
            hookCalls.Clear();

            db.Tenant.TenantId = BuyerTenantId;
            await svc.AcceptAsync(init!.PlaintextToken, BuyerTenantId, "buyer", default);

            // Accept fires to BOTH tenants (fan-out).
            Assert.Contains(hookCalls, u => u.Contains("seller.test"));
            Assert.Contains(hookCalls, u => u.Contains("buyer.test"));
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public async Task Decline_marks_row_declined()
    {
        var (db, svc, _) = SetupFixture();
        try
        {
            var goatId = AddGoat(db, SellerTenantId, "Bella");
            var init = await svc.InitiateAsync(goatId, "b@example.com", null, null, "seller", "https://x", default);

            var ok = await svc.DeclineByTokenAsync(init!.PlaintextToken, "Too far", default);
            Assert.True(ok);

            var transfer = db.Context.GoatTransfers.Single();
            Assert.Equal(GoatTransferStatus.Declined, transfer.Status);
            Assert.Equal("Too far", transfer.DeclineReason);
        }
        finally { db.Dispose(); }
    }
}
