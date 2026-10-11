using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using AuthStateDoctor.Blazor;
using AuthStateDoctor.DiagnosticFixture.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthDoctorFixture";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

// These endpoints exist only for the synthetic fixture. Refuse non-loopback requests and
// require a same-origin Origin header for state-changing POSTs.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_authdoctor"))
    {
        var remoteAddress = context.Connection.RemoteIpAddress;
        var host = context.Request.Host.Host;
        var localHost = host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress) || !localHost)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var expectedOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            if (!origin.Equals(expectedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }
        else if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthStateDoctorServerProbe();

app.MapGet("/_authdoctor/protected-probe", () => Results.NoContent()).RequireAuthorization();

app.MapPost("/_authdoctor/login", async (HttpContext context) =>
{
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "synthetic-fixture-user")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.NoContent();
});

app.MapPost("/_authdoctor/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
