using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("SyssloappenDatabase")
    ?? throw new InvalidOperationException("The PostgreSQL connection string is missing.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Local Angular development uses an HTTP proxy. Other environments must
    // continue to send the authentication cookie over HTTPS only.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = false;
    // Custom events validate Adult and Child sessions and return API status codes
    // instead of redirects.
    options.EventsType = typeof(SessionCookieEvents);
});
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddScoped<ChoreRecurrenceGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Child accounts have no email. Adult email uniqueness is instead enforced by
        // the database's unique NormalizedEmail index, which permits multiple nulls.
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("child-pairing", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("child-fallback-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Render (and most container hosts) terminate HTTPS at an edge proxy and forward plain HTTP to
// the container. Without this, the app sees every request as HTTP and UseHttpsRedirection below
// would redirect-loop, and the Secure cookie policy would refuse to set the auth cookie. The
// proxy's address isn't known in advance, so trust the forwarded headers unconditionally — safe
// here because the container is only reachable through that proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// "Local" writes to wwwroot for dev; "Supabase" uploads to Supabase Storage for deployed
// environments. See docs/HANDOFF.md for the Storage:* configuration keys.
builder.Services.Configure<SupabaseStorageOptions>(
    builder.Configuration.GetSection(SupabaseStorageOptions.SectionName));
if (builder.Configuration["Storage:Provider"] == "Supabase")
{
    builder.Services.AddHttpClient<IRewardImageStorage, SupabaseRewardImageStorage>();
}
else
{
    builder.Services.AddScoped<IRewardImageStorage, LocalDiskRewardImageStorage>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Angular's local development proxy connects to the HTTP launch profile on
// port 5047. Redirecting that proxied request to HTTPS turns it into a browser
// CORS request, so HTTPS remains enforced outside Development only.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Outside Development, wwwroot also holds the built Angular app (see Dockerfile) — served as
// static files, with unmatched non-API routes falling back to index.html for client-side routing.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
}

app.Run();

// WebApplicationFactory uses this entry point to start the API during integration tests.
public partial class Program;
