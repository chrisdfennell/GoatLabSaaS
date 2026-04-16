using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

// Load repo-root .env (if present) into process env so local `dotnet run` picks
// up secrets the same way docker-compose does in production. No-op when running
// in a container — those env vars come directly from compose.
DotEnvLoader.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

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

// Tenant context — scoped per request, populated by TenantContextMiddleware
// from the authenticated user's tenant_id claim.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminAuditLogger, AdminAuditLogger>();

// ASP.NET Core Identity with cookie auth. Blazor WASM uses same-site cookies
// against its hosting server, so no JWT plumbing needed.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false; // enable later when we wire email
    })
    .AddEntityFrameworkStores<GoatLabDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "goatlab.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
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

// Controllers + Swagger. Global [Authorize] filter: every controller requires
// an authenticated user by default. Individual endpoints (register, login,
// health checks) opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SuperAdminPolicy.Name, p => p
        .RequireAuthenticatedUser()
        .RequireClaim(SuperAdminPolicy.ClaimType, "true"));
});

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
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

// CORS for development
builder.Services.AddCors();

var app = builder.Build();

// Auto-migrate on startup + seed super-admins from config
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GoatLabDbContext>();
    db.Database.Migrate();

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await SuperAdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
}

app.UseHttpsRedirection();
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

app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>(); // must run after UseAuthentication
app.UseAuthorization();
// Maintenance mode runs after auth/authorization so the middleware can read the
// caller's super_admin claim and let admins through.
app.UseMiddleware<MaintenanceModeMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
