using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FlightCrewStudio.Components;
using FlightCrewStudio.Components.Account;
using FlightCrewStudio.Data;
using FlightCrewStudio.Services;
using FlightCrewStudio.Services.Admin;
using FlightCrewStudio.Services.Export;
using FlightCrewStudio.Services.GlobaLog;
using FlightCrewStudio.Services.Sync;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// --- User types: "Admin" policy for administrators ---
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdministratorHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminOptions.Policy, p => p.RequireAuthenticatedUser().AddRequirements(new AdministratorRequirement()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// MySQL via Pomelo. An explicit server version avoids a connection attempt at startup/build.
var serverVersionString = builder.Configuration["Database:ServerVersion"];
var serverVersion = string.IsNullOrWhiteSpace(serverVersionString)
    ? new MySqlServerVersion(new Version(8, 0, 36))
    : ServerVersion.Parse(serverVersionString);

void ConfigureDb(DbContextOptionsBuilder options) =>
    options.UseMySql(connectionString, serverVersion,
        mySql => mySql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));

// A factory for components (short-lived contexts) plus a scoped context for Identity.
builder.Services.AddDbContextFactory<ApplicationDbContext>(ConfigureDb);
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Email: use SMTP when configured, otherwise a no-op sender (links are logged instead of sent).
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]))
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// --- GlobaLog sync services ---
builder.Services.Configure<GlobaLogOptions>(builder.Configuration.GetSection(GlobaLogOptions.SectionName));
builder.Services.AddSingleton<ICredentialProtector, AesGcmCredentialProtector>();
builder.Services.AddScoped<IGlobaLogClientFactory, GlobaLogClientFactory>();
builder.Services.AddSingleton<SyncProgressTracker>();
builder.Services.AddSingleton<ISyncService, SyncService>();
builder.Services.AddScoped<ReferenceDataImporter>();
builder.Services.AddScoped<FlightCrewStudio.Services.DayNight.DayNightRecalculator>();

var app = builder.Build();

// Apply migrations on startup (retrying while MySQL comes up).
await ApplyMigrationsAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Note: TLS is expected to be terminated by a reverse proxy in front of the container,
// so HTTPS redirection is not forced here. Re-enable if the app is exposed directly.
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Logbook downloads (PDF / CSV) posted from the Export page.
app.MapExportEndpoints();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
            await AdministratorHandler.EnsureAdministratorsAsync(db,
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminOptions>>().Value, logger);
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/10); retrying in 5s…", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

public partial class Program { }
