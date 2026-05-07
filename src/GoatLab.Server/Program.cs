using System.Threading.RateLimiting;
using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Alerts;
using GoatLab.Server.Services.Auth;
using GoatLab.Server.Services.Backup;
using GoatLab.Server.Services.Billing;
using GoatLab.Server.Services.Email;
using GoatLab.Server.Services.Jobs;
using GoatLab.Server.Services.Pdf;
using GoatLab.Server.Services.Pedigree;
using GoatLab.Server.Services.Plans;
using GoatLab.Server.Services.Push;
using GoatLab.Server.Services.Reports;
using GoatLab.Server.Services.Webhooks;
using Fido2NetLib;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Bootstrap logger — captures failures during host build before Serilog's
// configured pipeline is live. Replaced once the host is up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

// Load repo-root .env (if present) into process env so local `dotnet run` picks
// up secrets the same way docker-compose does in production. No-op when running
// in a container — those env vars come directly from compose.
DotEnvLoader.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

// Sentry. Only active when Sentry:Dsn is configured (env SENTRY_DSN).
// Captures uncaught exceptions via middleware + Error-level Serilog events.
var sentryDsn = builder.Configuration.GetValue<string>("Sentry:Dsn");
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.TracesSampleRate = builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0.0;
        o.SendDefaultPii = false;
    });
}

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext();

    // Feed Error+ Serilog events into Sentry for unified error tracking.
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        cfg.WriteTo.Sentry(s =>
        {
            s.Dsn = sentryDsn;
            s.MinimumEventLevel = Serilog.Events.LogEventLevel.Error;
            s.MinimumBreadcrumbLevel = Serilog.Events.LogEventLevel.Information;
        });
    }
});

// Map GOOGLE_MAPS_API_KEY env var into the IConfiguration tree. Docker-compose
// sets GoogleMaps__ApiKey directly, so this only handles the dev path where
// the .env file uses the friendlier uppercased name.
var mapsKeyFromEnv = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
if (!string.IsNullOrEmpty(mapsKeyFromEnv))
    builder.Configuration["GoogleMaps:ApiKey"] = mapsKeyFromEnv;

// EF Core + SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
builder.Services.AddDbContext<GoatLabDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

// Hosted SaaS vs self-hosted OSS distinction. Singleton because the value
// is config-driven and never changes after startup.
builder.Services.AddSingleton<IAppMode, AppMode>();

// Tenant context — scoped per request, populated by TenantContextMiddleware
// from the authenticated user's tenant_id claim.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminAuditLogger, AdminAuditLogger>();

// DataProtection: persist the key ring to a known path so auth cookies survive
// container rebuilds. Docker compose mounts a named volume at this path (see
// docker-compose.prod.yml). SetApplicationName pins the purpose string so keys
// aren't accidentally re-scoped by a container-id change.
var dpKeyPath = builder.Configuration.GetValue<string>("DataProtection:KeyPath")
                ?? "/root/.aspnet/DataProtection-Keys";
try { Directory.CreateDirectory(dpKeyPath); } catch { /* read-only FS in tests — fall through */ }
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeyPath))
    .SetApplicationName("GoatLab");

// ASP.NET Core Identity with cookie auth. Blazor WASM uses same-site cookies
// against its hosting server, so no JWT plumbing needed.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        // Config-driven. Flip to true in production via Identity:RequireConfirmedEmail
        // env var / user-secret once Smtp:Host is configured; otherwise signups
        // can't complete login because no email actually lands.
        options.SignIn.RequireConfirmedEmail =
            builder.Configuration.GetValue<bool>("Identity:RequireConfirmedEmail", false);
        // TOTP authenticator token provider — needed for 2FA QR setup flow.
        options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
    })
    .AddEntityFrameworkStores<GoatLabDbContext>()
    .AddDefaultTokenProviders()
    // Custom claims factory: ensures super_admin (and tenant_id) survive
    // cookie regeneration by the SecurityStampValidator. Without this, the
    // built-in factory only emits Identity's defaults — admin endpoints
    // start returning 403 ~30 minutes after login.
    .AddClaimsPrincipalFactory<GoatLab.Server.Services.Auth.AppClaimsPrincipalFactory>();

// API key bearer scheme. Coexists with the Identity cookie scheme (the default
// for browser sessions). The global authorization policy below accepts either.
// Callers send `Authorization: Bearer gl_<secret>`; ApiKeyAuthHandler hashes,
// looks up the row, and produces a principal with the tenant_id claim.
//
// In production we run behind Caddy, which terminates HTTPS and forwards to
// Kestrel over HTTP. Without this, ASP.NET sees the request as `http://...`
// and `CookieSecurePolicy.Always` would block the auth cookie from being set.
// Restricting to KnownNetworks/KnownProxies in this single-host VPS setup
// would just complicate ops; we trust the loopback proxy.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Buyer cookie scheme is a separate, lightweight auth path for marketplace
// buyers (no tenant, no password — magic link to email). Stored under a
// distinct cookie so a buyer signing in doesn't tangle with a seller
// session on the same browser, and vice versa.
builder.Services
    .AddAuthentication()
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.SchemeName, _ => { })
    .AddCookie("Buyer", options =>
    {
        options.Cookie.Name = "goatlab.buyer";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Always require HTTPS in production. ForwardedHeaders middleware
        // (registered at the top of the pipeline) makes the request scheme
        // reflect Caddy's external HTTPS so this works behind the proxy.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(60);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            // SSR /buyer/* pages redirect to the magic-link page. JSON
            // /api/buyer/* gets a 401 instead of an HTML login page.
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect("/buyer/sign-in?returnUrl=" + Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString));
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// IMemoryCache holds the short-lived WebAuthn challenge between register-start
// and register-complete (and between login-start and login-complete).
builder.Services.AddMemoryCache();

// WebAuthn / FIDO2. RP ID is the registrable domain (e.g. "localhost" for dev,
// "goatlab.example" in prod). Origins must be the full scheme+host+port of the
// Blazor client. Configure via WebAuthn:RpId / WebAuthn:Origins (array).
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration.GetValue<string>("WebAuthn:RpId") ?? "localhost";
    options.ServerName = "GoatLab";
    options.Origins = new HashSet<string>(
        builder.Configuration.GetSection("WebAuthn:Origins").Get<string[]>()
        ?? new[] { "http://localhost:8080", "http://localhost:8095", "https://localhost:5001" });
    options.TimestampDriftTolerance = 300000;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "goatlab.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Same prod-HTTPS-only story as the buyer scheme.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    // API returns 401/403 instead of redirecting to a login page (WASM handles UI).
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Antiforgery: surface the standard ASP.NET token under a custom header so
// fetch-based POSTs from SSR pages can carry the token without rebuilding
// FormData. Form-field name stays the default __RequestVerificationToken.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Controllers + Swagger. Global [Authorize] filter: every controller requires
// an authenticated user by default. Individual endpoints (register, login,
// health checks) opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    // Explicitly bind the policy to the Identity cookie scheme. Without this,
    // the policy defaults to whatever the framework picks as the active scheme
    // for the request — which can be ApiKey when the global auth filter has
    // both schemes active, and the API-key principal has no super_admin claim.
    options.AddPolicy(SuperAdminPolicy.Name, p => p
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(SuperAdminPolicy.ClaimType, "true"));
});

// AddControllersWithViews (not plain AddControllers) so MVC Razor views work.
// We use them only for the public /breeds pages — every other controller is
// pure Web API. The view infrastructure adds ~0 runtime cost when unused.
builder.Services.AddControllersWithViews(options =>
{
    // Global policy accepts either the Identity cookie OR an API key bearer.
    // Controllers that must be cookie-only (e.g. minting API keys) opt out
    // with an explicit [Authorize(AuthenticationSchemes = ...)] override.
    var policy = new AuthorizationPolicyBuilder(
            IdentityConstants.ApplicationScheme,
            ApiKeyAuthOptions.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
})
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "GoatLab API", Version = "v1" });
});

// CORS. The Blazor WASM client is served by this same server, so in production
// everything is same-origin and CORS is a no-op. Only applied in Development
// (where the client may run on a different port) or when Cors:AllowedOrigins
// is explicitly configured for cross-origin deployments.
builder.Services.AddCors();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<GoatLabDbContext>("database");

// Email. Real SMTP sender when Smtp:Host is configured, otherwise a no-op that
// logs each attempt. Lets features like password reset ship before the mail
// server exists; flip on by setting Smtp:Host (via env or user-secrets).
//
// LoggingEmailSenderDecorator wraps whichever inner sender is registered so
// /admin/email-log records every send. The inner senders are registered as
// concrete classes (not via IAppEmailSender) so the decorator alone wins the
// interface.
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
var smtpHost = builder.Configuration.GetValue<string>($"{SmtpOptions.SectionName}:Host");
if (!string.IsNullOrWhiteSpace(smtpHost))
    builder.Services.AddSingleton<SmtpEmailSender>();
else
    builder.Services.AddSingleton<NullEmailSender>();
builder.Services.AddSingleton<IAppEmailSender>(sp =>
{
    IAppEmailSender inner = !string.IsNullOrWhiteSpace(smtpHost)
        ? sp.GetRequiredService<SmtpEmailSender>()
        : sp.GetRequiredService<NullEmailSender>();
    return new LoggingEmailSenderDecorator(
        inner,
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<LoggingEmailSenderDecorator>>());
});

// Billing. StripeBillingService reads Stripe:SecretKey/WebhookSecret at
// startup. Missing keys don't fail startup — endpoints error at call time so
// the rest of the app stays usable while Stripe credentials are configured.
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.AddScoped<IBillingService, StripeBillingService>();

// Plan-based feature gating. Scoped so FeatureGate caches the plan lookup
// within a single request.
builder.Services.AddScoped<IFeatureGate, FeatureGate>();

// Smart alerts (scanner) + Web Push (VAPID) + PDF generation (QuestPDF).
// QuestPDF requires its license type to be set once at startup. Community is
// free for revenue < $1M / < 10 employees — flag for review at scale.
builder.Services.AddScoped<AlertScannerService>();
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddScoped<PushService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<CoiCalculator>();
builder.Services.AddScoped<GoatLab.Server.Services.Pedigree.MateRecommendationService>();
builder.Services.AddScoped<GoatLab.Server.Services.Transfers.GoatTransferService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<GoatLab.Server.Services.Health.WithdrawalService>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Offsite database backup. No-op when Backup:Offsite:Enabled is false.
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
builder.Services.AddScoped<IBackupService, BackupService>();

// Lawyer-fillable Terms/Privacy values. All fields optional — pages fall back
// to bracketed placeholders when unset. See LegalOptions.cs for env var names.
builder.Services.Configure<GoatLab.Server.Services.Legal.LegalOptions>(
    builder.Configuration.GetSection(GoatLab.Server.Services.Legal.LegalOptions.SectionName));

// ADGA/AGS registry CSV import.
builder.Services.AddScoped<RegistryImportService>();

// Per-goat unified timeline aggregator.
builder.Services.AddScoped<GoatLab.Server.Services.Timeline.ITimelineService, GoatLab.Server.Services.Timeline.TimelineService>();

// Public-marketplace fan-out: notifies anonymous followers of a farm when
// the farm lists a new goat for sale. Fired from GoatsController.Update.
builder.Services.AddScoped<GoatLab.Server.Services.Marketplace.NewListingNotifier>();

// Vet share-link mints + validates time-bounded URLs that grant a vet
// read-access to one goat's medical history without signup.
builder.Services.AddScoped<GoatLab.Server.Services.Vet.VetShareLinkService>();

// Outbound webhooks. Dispatcher is scoped (shares the request's DbContext so
// event emission participates in the same unit of work as the write that
// triggered it). HttpClient is named "webhooks" with a 10s timeout applied
// per-request in the dispatcher.
builder.Services.AddHttpClient("webhooks");
builder.Services.AddScoped<WebhookDispatcher>();

// Hangfire. Recurring jobs live in Services/Jobs; registration happens after
// the host is built so DI is available. SQL Server storage reuses the app's
// connection string; Hangfire creates its own [HangFire] schema on first run.
// Disabled when running under the Testing environment (integration tests use
// SQLite / swap services and don't want Hangfire's SQL Server requirement).
var hangfireEnabled = !builder.Environment.IsEnvironment("Testing");
if (hangfireEnabled)
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15),
        }));
    builder.Services.AddHangfireServer();
}

// Rate limiting: per-endpoint policies (auth, register, transfer) were replaced
// by Google reCAPTCHA v3 verification on the same endpoints. We keep a generic
// global IP ceiling as a defense-in-depth backstop in case reCAPTCHA service
// has an outage or someone scripts around it with a token farm — generous
// enough to never affect real users (300 req/min/IP).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

// reCAPTCHA v3 — used by [RequireRecaptcha("action")] on auth + public POSTs.
builder.Services.Configure<GoatLab.Server.Services.Security.RecaptchaOptions>(
    builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddHttpClient<GoatLab.Server.Services.Security.IRecaptchaVerifier,
                                GoatLab.Server.Services.Security.RecaptchaVerifier>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Auto-migrate on startup + seed super-admins from config
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GoatLabDbContext>();
    db.Database.Migrate();

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await SuperAdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
}

// Honor X-Forwarded-* from Caddy so request.Scheme becomes "https" and the
// Secure cookie policy works. Must run before any middleware that inspects
// scheme/host (Serilog enrichment, redirects, auth).
app.UseForwardedHeaders();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
    // In dev we may have a self-signed cert and want to enforce HTTPS during
    // testing. In production the app runs behind a reverse proxy (QNAP /
    // Container Station) that terminates TLS — forcing another redirect here
    // would loop or break upstream routing.
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Serve uploaded media files
var mediaPath = Path.Combine(app.Environment.ContentRootPath, "media");
Directory.CreateDirectory(mediaPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaPath),
    RequestPath = "/media"
});

if (app.Environment.IsDevelopment())
{
    app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}
else
{
    var allowedOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (allowedOrigins is { Length: > 0 })
    {
        app.UseCors(p => p
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    }
    // Else: same-origin only (WASM client served by this server). No CORS middleware.
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>(); // must run after UseAuthentication
app.UseAuthorization();
// Maintenance mode runs after auth/authorization so the middleware can read the
// caller's super_admin claim and let admins through.
app.UseMiddleware<MaintenanceModeMiddleware>();

app.MapHealthChecks("/health");
if (hangfireEnabled)
{
    app.MapHangfireDashboard("/admin/jobs", new DashboardOptions
    {
        Authorization = new[] { new HangfireSuperAdminFilter() },
        DisplayStorageConnectionString = false,
    });

    // Register recurring jobs (daily). Cron expressions are UTC.
    RecurringJob.AddOrUpdate<TrialReminderJob>(
        "trial-reminder-daily",
        job => job.RunAsync(CancellationToken.None),
        "0 9 * * *"); // 09:00 UTC daily

    RecurringJob.AddOrUpdate<HardDeleteSweepJob>(
        "hard-delete-sweep-daily",
        job => job.RunAsync(CancellationToken.None),
        "0 3 * * *"); // 03:00 UTC daily

    // Offsite backup. Registered regardless of Enabled so the admin can see
    // it on the health page and toggle it via env without redeploying the
    // recurring-jobs set. The job itself checks Enabled and exits early.
    RecurringJob.AddOrUpdate<DatabaseBackupJob>(
        "offsite-backup-daily",
        job => job.RunAsync(CancellationToken.None),
        "0 4 * * *"); // 04:00 UTC daily

    // Smart alerts. Hourly so push notifications are timely; the scanner is
    // idempotent so re-runs within 24h don't duplicate alerts.
    RecurringJob.AddOrUpdate<AlertScanJob>(
        "alert-scan-hourly",
        job => job.RunAsync(CancellationToken.None),
        "0 * * * *"); // top of every hour, UTC

    // Email digest of the last 24h of alerts. 07:00 UTC ≈ early morning in
    // North America, late morning in Europe — fine for a daily summary.
    RecurringJob.AddOrUpdate<AlertDigestJob>(
        "alert-digest-daily",
        job => job.RunAsync(CancellationToken.None),
        "0 7 * * *");

    // Webhook retry sweep. Runs every 5 minutes; picks up deliveries whose
    // first attempt failed and whose backoff window has elapsed.
    RecurringJob.AddOrUpdate<WebhookRetryJob>(
        "webhook-retry",
        job => job.RunAsync(CancellationToken.None),
        "*/5 * * * *");
}

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "GoatLab host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
