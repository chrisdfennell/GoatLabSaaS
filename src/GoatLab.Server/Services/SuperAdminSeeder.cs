using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace GoatLab.Server.Services;

/// <summary>
/// Startup seeder for cross-tenant admins.
///
/// Two modes:
/// 1. Elevate — if an email in SuperAdmin:Emails matches an existing user,
///    flip IsSuperAdmin = true. Safe for prod.
/// 2. Bootstrap — if SuperAdmin:BootstrapPassword is set AND the email has no
///    account yet, create the account, give it a default tenant, and elevate.
///    Intended for local dev so the first admin doesn't have to register via
///    the UI before being seeded. Missing password = mode disabled.
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var emails = config.GetSection("SuperAdmin:Emails").Get<string[]>() ?? Array.Empty<string>();
        if (emails.Length == 0) return;

        var bootstrapPassword = config["SuperAdmin:BootstrapPassword"];
        var bootstrapTenantName = config["SuperAdmin:BootstrapTenantName"] ?? "Admin Farm";

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<GoatLabDbContext>();
        var tenantContext = services.GetRequiredService<ITenantContext>();

        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                if (string.IsNullOrEmpty(bootstrapPassword))
                {
                    logger.LogInformation("SuperAdmin seed: {Email} not yet registered — skipping (no BootstrapPassword set).", email);
                    continue;
                }

                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = email.Split('@')[0],
                    IsSuperAdmin = true,
                };
                var createResult = await userManager.CreateAsync(user, bootstrapPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("SuperAdmin seed: couldn't create {Email}: {Errors}",
                        email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    continue;
                }

                tenantContext.BypassFilter = true;
                var tenant = new Tenant
                {
                    Name = bootstrapTenantName,
                    Slug = Slugify(bootstrapTenantName),
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();

                db.TenantMembers.Add(new TenantMember
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Role = TenantRole.Owner,
                });
                await db.SaveChangesAsync();

                logger.LogWarning("SuperAdmin seed: BOOTSTRAPPED {Email} with tenant '{Tenant}'. Change the password after first login.",
                    email, tenant.Name);
                continue;
            }

            if (user.IsSuperAdmin) continue;
            user.IsSuperAdmin = true;
            await userManager.UpdateAsync(user);
            logger.LogInformation("SuperAdmin seed: elevated {Email}.", email);
        }
    }

    private static string Slugify(string name)
    {
        var s = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }
}
