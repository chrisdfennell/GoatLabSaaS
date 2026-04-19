using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.ApiKeys;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.DTOs;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Transfers;

// End-to-end farm-to-farm transfer of a single goat.
//
// Initiate (seller): creates a Pending GoatTransfer with a 14-day expiry, sends
// the buyer a magic-link email, returns the plaintext token once so the seller
// can copy-paste if email delivery fails. Token shape: `gt_` + base64url(32).
//
// Preview (anon): buyer or anyone with the link can see what's on offer —
// goat name, pedigree, counts of records that will transfer. Reveals nothing
// beyond those counts (no photos, no medical details) to anonymous viewers.
//
// Accept (buyer): atomically reassigns the goat and its child records into the
// buyer's chosen tenant. Creates IsExternal Sire/Dam stubs so pedigree links
// survive. Breeding/kidding/sales/calendar records that reference OTHER goats
// stay on the seller side intact (they're the seller's history, not the goat's).
//
// Decline / Cancel: soft-status only, no data changes.
public class GoatTransferService
{
    private const string TokenPrefix = "gt_";
    private const int DefaultExpiryDays = 14;
    private const int MaxExpiryDays = 60;

    private readonly GoatLabDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAppEmailSender _email;
    private readonly ILogger<GoatTransferService> _logger;

    public GoatTransferService(
        GoatLabDbContext db,
        ITenantContext tenantContext,
        IAppEmailSender email,
        ILogger<GoatTransferService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _logger = logger;
    }

    // ---------- Initiate ----------
    public async Task<InitiateTransferResponse?> InitiateAsync(
        int goatId,
        string buyerEmail,
        string? message,
        int? expiryDays,
        string sellerUserId,
        string origin,
        CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int fromTenantId) return null;

        var goat = await _db.Goats.FirstOrDefaultAsync(g => g.Id == goatId, ct);
        if (goat is null) return null;

        // Prevent starting a second transfer while one is already pending for this goat.
        var existing = await _db.GoatTransfers
            .FirstOrDefaultAsync(t => t.GoatId == goatId
                && t.FromTenantId == fromTenantId
                && t.Status == GoatTransferStatus.Pending
                && t.ExpiresAt > DateTime.UtcNow, ct);
        if (existing is not null)
            throw new InvalidOperationException("There is already a pending transfer for this goat. Cancel it first.");

        var expiry = Math.Clamp(expiryDays ?? DefaultExpiryDays, 1, MaxExpiryDays);

        // Mint token: gt_ + base64url(32) (46 chars), store only hash + 12-char prefix.
        var plaintext = TokenPrefix + Base64UrlRandom(32);
        var hash = ApiKeyGenerator.HashPlaintext(plaintext);
        var displayPrefix = plaintext.Substring(0, 12);

        var transfer = new GoatTransfer
        {
            FromTenantId = fromTenantId,
            GoatId = goatId,
            InitiatedByUserId = sellerUserId,
            BuyerEmail = buyerEmail.Trim().ToLowerInvariant(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Status = GoatTransferStatus.Pending,
            TokenPrefix = displayPrefix,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiry),
        };
        _db.GoatTransfers.Add(transfer);
        await _db.SaveChangesAsync(ct);

        var acceptUrl = $"{origin.TrimEnd('/')}/transfer/{plaintext}";

        // Fire-and-forget email. If SMTP isn't configured, NullEmailSender just logs.
        var sellerTenantName = (await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == fromTenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct)) ?? "A farm";
        var (subject, html, text) = EmailTemplates.GoatTransferInvite(
            sellerTenantName, goat.Name, acceptUrl, transfer.ExpiresAt, transfer.Message);
        try
        {
            await _email.SendAsync(transfer.BuyerEmail, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transfer invite email failed for transfer {Id}", transfer.Id);
        }

        return new InitiateTransferResponse(transfer.Id, acceptUrl, plaintext);
    }

    // ---------- Seller: list + cancel ----------

    public async Task<IReadOnlyList<GoatTransferSummaryDto>> ListForSellerAsync(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int fromTenantId) return Array.Empty<GoatTransferSummaryDto>();

        await SweepExpiredAsync(fromTenantId, ct);

        return await _db.GoatTransfers
            .Where(t => t.FromTenantId == fromTenantId)
            .Include(t => t.Goat)
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new GoatTransferSummaryDto(
                t.Id,
                t.GoatId,
                t.Goat!.Name,
                t.BuyerEmail,
                t.Status.ToString(),
                t.CreatedAt,
                t.ExpiresAt,
                t.AcceptedAt,
                t.DeclinedAt,
                t.CancelledAt,
                t.TokenPrefix))
            .ToListAsync(ct);
    }

    public async Task<bool> CancelAsync(int transferId, CancellationToken ct)
    {
        if (_tenantContext.TenantId is not int fromTenantId) return false;

        var transfer = await _db.GoatTransfers
            .FirstOrDefaultAsync(t => t.Id == transferId && t.FromTenantId == fromTenantId, ct);
        if (transfer is null) return false;
        if (transfer.Status != GoatTransferStatus.Pending) return false;

        transfer.Status = GoatTransferStatus.Cancelled;
        transfer.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- Anon preview / decline ----------

    public async Task<GoatTransferPreviewDto?> PreviewByTokenAsync(string plaintextToken, CancellationToken ct)
    {
        var hash = ApiKeyGenerator.HashPlaintext(plaintextToken);
        var transfer = await _db.GoatTransfers
            .IgnoreQueryFilters()
            .Include(t => t.FromTenant)
            .Include(t => t.Goat)!.ThenInclude(g => g!.Sire)
            .Include(t => t.Goat)!.ThenInclude(g => g!.Dam)
            .Include(t => t.Goat)!.ThenInclude(g => g!.Photos)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (transfer is null) return null;

        // Auto-expire on read so buyers landing on a dead link get a clear status.
        if (transfer.Status == GoatTransferStatus.Pending && transfer.ExpiresAt <= DateTime.UtcNow)
        {
            transfer.Status = GoatTransferStatus.Expired;
            await _db.SaveChangesAsync(ct);
        }

        var goat = transfer.Goat;
        if (goat is null || transfer.FromTenant is null) return null;
        if (transfer.FromTenant.DeletedAt is not null) return null;

        var goatId = goat.Id;
        var medCount = await _db.MedicalRecords.IgnoreQueryFilters()
            .Where(r => r.GoatId == goatId && r.TenantId == transfer.FromTenantId).CountAsync(ct);
        var weightCount = await _db.WeightRecords.IgnoreQueryFilters()
            .Where(r => r.GoatId == goatId && r.TenantId == transfer.FromTenantId).CountAsync(ct);
        var milkCount = await _db.MilkLogs.IgnoreQueryFilters()
            .Where(r => r.GoatId == goatId && r.TenantId == transfer.FromTenantId).CountAsync(ct);
        var photoCount = goat.Photos.Count;

        var primaryPhoto = goat.Photos
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.UploadedAt)
            .Select(p => "/" + p.FilePath)
            .FirstOrDefault();

        return new GoatTransferPreviewDto(
            transfer.Id,
            transfer.Status.ToString(),
            transfer.ExpiresAt,
            transfer.FromTenant.Name,
            goat.Name,
            goat.Breed,
            goat.Gender.ToString(),
            goat.DateOfBirth,
            goat.EarTag,
            goat.RegistrationNumber,
            goat.Sire?.Name,
            goat.Dam?.Name,
            medCount,
            weightCount,
            milkCount,
            photoCount,
            transfer.Message,
            primaryPhoto);
    }

    public async Task<bool> DeclineByTokenAsync(string plaintextToken, string? reason, CancellationToken ct)
    {
        var hash = ApiKeyGenerator.HashPlaintext(plaintextToken);
        var transfer = await _db.GoatTransfers
            .IgnoreQueryFilters()
            .Include(t => t.FromTenant)
            .Include(t => t.Goat)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (transfer is null) return false;
        if (transfer.Status != GoatTransferStatus.Pending) return false;
        if (transfer.ExpiresAt <= DateTime.UtcNow) return false;

        transfer.Status = GoatTransferStatus.Declined;
        transfer.DeclinedAt = DateTime.UtcNow;
        transfer.DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _db.SaveChangesAsync(ct);

        if (transfer.Goat is not null)
        {
            try
            {
                var (subject, html, text) = EmailTemplates.GoatTransferDeclined(transfer.Goat.Name, transfer.DeclineReason);
                var sellerEmail = await LookupSellerEmailAsync(transfer.InitiatedByUserId, ct);
                if (!string.IsNullOrEmpty(sellerEmail))
                    await _email.SendAsync(sellerEmail, subject, html, text, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transfer-declined email failed for transfer {Id}", transfer.Id);
            }
        }
        return true;
    }

    // ---------- Accept — the heavy lifting ----------

    public async Task<AcceptTransferResponse?> AcceptAsync(
        string plaintextToken,
        int toTenantId,
        string buyerUserId,
        CancellationToken ct)
    {
        var hash = ApiKeyGenerator.HashPlaintext(plaintextToken);

        _tenantContext.BypassFilter = true;
        try
        {
            var transfer = await _db.GoatTransfers
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (transfer is null) return null;
            if (transfer.Status != GoatTransferStatus.Pending) return null;
            if (transfer.ExpiresAt <= DateTime.UtcNow) return null;
            if (transfer.FromTenantId == toTenantId)
                throw new InvalidOperationException("Cannot transfer a goat to the same farm it's already in.");

            // Verify buyer is owner/member of the destination tenant.
            var isMember = await _db.TenantMembers
                .AnyAsync(m => m.TenantId == toTenantId && m.UserId == buyerUserId, ct);
            if (!isMember)
                throw new UnauthorizedAccessException("You must be a member of the destination farm.");

            var toTenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == toTenantId && t.DeletedAt == null && t.SuspendedAt == null, ct);
            if (toTenant is null) throw new InvalidOperationException("Destination farm is unavailable.");

            var goat = await _db.Goats
                .Include(g => g.Sire)
                .Include(g => g.Dam)
                .FirstOrDefaultAsync(g => g.Id == transfer.GoatId && g.TenantId == transfer.FromTenantId, ct);
            if (goat is null) return null;

            using var txn = await _db.Database.BeginTransactionAsync(ct);

            // 1. Build IsExternal pedigree stubs in the destination so sire/dam
            //    links survive the move. The source sire/dam goats themselves
            //    stay on the seller's side untouched.
            int? newSireId = await CloneParentStubAsync(goat.Sire, toTenantId, ct);
            int? newDamId = await CloneParentStubAsync(goat.Dam, toTenantId, ct);

            var goatName = goat.Name;
            var fromTenantId = goat.TenantId;

            // 2. Re-assign child records' TenantId. The goat FK stays the same;
            //    only TenantId + (for the goat itself) Sire/DamId change.
            await _db.MedicalRecords.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.WeightRecords.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.FamachaScores.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.BodyConditionScores.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.MilkLogs.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.GoatPhotos.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);
            await _db.GoatDocuments.IgnoreQueryFilters()
                .Where(r => r.GoatId == goat.Id && r.TenantId == fromTenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.TenantId, toTenantId), ct);

            // 3. Move the goat itself and retarget pedigree FKs.
            goat.TenantId = toTenantId;
            goat.PenId = null;                 // buyer re-assigns on their side
            goat.IsListedForSale = false;      // no longer for sale after transfer
            goat.AskingPriceCents = null;
            goat.SireId = newSireId;
            goat.DamId = newDamId;
            goat.UpdatedAt = DateTime.UtcNow;

            // 4. Mark transfer complete.
            transfer.Status = GoatTransferStatus.Accepted;
            transfer.AcceptedAt = DateTime.UtcNow;
            transfer.AcceptedByUserId = buyerUserId;
            transfer.ToTenantId = toTenantId;

            await _db.SaveChangesAsync(ct);
            await txn.CommitAsync(ct);

            // 5. Email seller.
            try
            {
                var (subject, html, text) = EmailTemplates.GoatTransferAccepted(toTenant.Name, goatName);
                var sellerEmail = await LookupSellerEmailAsync(transfer.InitiatedByUserId, ct);
                if (!string.IsNullOrEmpty(sellerEmail))
                    await _email.SendAsync(sellerEmail, subject, html, text, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transfer-accepted email failed for transfer {Id}", transfer.Id);
            }

            return new AcceptTransferResponse(toTenantId, goat.Id);
        }
        finally { _tenantContext.BypassFilter = false; }
    }

    // ---------- Helpers ----------

    // Clones a seller-side parent into the buyer tenant as a pedigree stub.
    // If the parent is null, returns null. If the parent is already IsExternal
    // in the source, we still clone to keep the two tenants' pedigree rows independent.
    private async Task<int?> CloneParentStubAsync(Goat? parent, int toTenantId, CancellationToken ct)
    {
        if (parent is null) return null;

        var stub = new Goat
        {
            TenantId = toTenantId,
            Name = parent.Name,
            EarTag = parent.EarTag,
            Breed = parent.Breed,
            Gender = parent.Gender,
            DateOfBirth = parent.DateOfBirth,
            RegistrationNumber = parent.RegistrationNumber,
            Registry = parent.Registry,
            BreederName = parent.BreederName,
            IsExternal = true,
            Status = GoatStatus.Healthy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Goats.Add(stub);
        await _db.SaveChangesAsync(ct);
        return stub.Id;
    }

    private async Task<string?> LookupSellerEmailAsync(string userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

    private async Task SweepExpiredAsync(int fromTenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.GoatTransfers
            .Where(t => t.FromTenantId == fromTenantId
                        && t.Status == GoatTransferStatus.Pending
                        && t.ExpiresAt <= now)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, GoatTransferStatus.Expired), ct);
    }

    private static string Base64UrlRandom(int byteLen)
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(byteLen);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
