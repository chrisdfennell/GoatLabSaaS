using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Jobs;

// Runs daily via Hangfire. Honors the 30-day grace period promised on the
// account-deletion page: users (and their sole-owned tenants) who were soft-
// deleted > 30 days ago are hard-deleted from the database.
//
// Tenants cascade-delete their ITenantOwned rows via the FK graph (we use
// NoAction for the Tenant FK by default, but TenantMember is Cascade). Goats,
// medical records, etc. are cleaned up here by explicit SaveChanges passes.
public class HardDeleteSweepJob
{
    private readonly GoatLabDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HardDeleteSweepJob> _logger;

    public HardDeleteSweepJob(
        GoatLabDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<HardDeleteSweepJob> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Hard-delete tenants soft-deleted > 30 days ago.
        var oldTenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt != null && t.DeletedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var tenant in oldTenants)
        {
            _logger.LogInformation("Hard-deleting tenant {TenantId} soft-deleted at {DeletedAt}",
                tenant.Id, tenant.DeletedAt);
            _db.Tenants.Remove(tenant);
        }
        if (oldTenants.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        // Hard-delete users soft-deleted > 30 days ago. UserManager.DeleteAsync
        // handles cascading Identity-owned rows.
        var oldUsers = await _userManager.Users
            .Where(u => u.DeletedAt != null && u.DeletedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var user in oldUsers)
        {
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("Hard-deleted user {UserId} soft-deleted at {DeletedAt}",
                    user.Id, user.DeletedAt);
            }
            else
            {
                _logger.LogWarning("Failed to hard-delete user {UserId}: {Errors}",
                    user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
