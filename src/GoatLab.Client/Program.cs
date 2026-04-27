using GoatLab.Client;
using GoatLab.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient pointing back to the host (Server project)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// MudBlazor
builder.Services.AddMudServices();

// Authentication (cookie-based — server issues the auth cookie)
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("SuperAdmin", p => p
        .RequireAuthenticatedUser()
        .RequireClaim("super_admin", "true"));

    // User is signed in AND has picked a tenant — used to hide the farm-side
    // nav from pure-admin accounts that have no membership to anything.
    options.AddPolicy("HasTenant", p => p
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id"));
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());

// API Services
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<GoatService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<BreedingService>();
builder.Services.AddScoped<MilkService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<PastureService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CareGuideService>();
builder.Services.AddScoped<ToolsService>();
builder.Services.AddScoped<LeafletService>();
builder.Services.AddScoped<GoogleMapsService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<AdminPlansService>();
builder.Services.AddScoped<AdminHealthService>();
builder.Services.AddScoped<TenantSettingsService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TwoFactorService>();
builder.Services.AddScoped<BarnService>();
builder.Services.AddScoped<VoiceService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<PwaService>();
builder.Services.AddScoped<ShowService>();
builder.Services.AddScoped<OfflineQueueService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<ProtocolService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<AnnouncementsService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddScoped<AlertsService>();
builder.Services.AddScoped<PushService>();
builder.Services.AddScoped<CoiService>();
builder.Services.AddScoped<MateRecommendationsService>();
builder.Services.AddScoped<GoatTransfersService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<WaitlistService>();
builder.Services.AddScoped<ApiKeysService>();
builder.Services.AddScoped<WebhooksService>();
builder.Services.AddScoped<LegalSettingsService>();
builder.Services.AddScoped<AdminOpsService>();

await builder.Build().RunAsync();
