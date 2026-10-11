using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuthStateDoctor.Blazor;

public static class AuthStateDoctorServerProbeExtensions
{
    public const string RoutePattern = "/_authdoctor/server-state";

    public static RouteHandlerBuilder MapAuthStateDoctorServerProbe(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "The Auth State Doctor server probe can only be mapped in Development or Testing environments.");
        }

        return endpoints.MapGet(RoutePattern, (HttpContext context) =>
        {
            if (!IsLoopback(context.Connection.RemoteIpAddress) || !IsLocalHost(context.Request.Host.Host))
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { authenticated = context.User.Identity?.IsAuthenticated == true });
        })
        .AllowAnonymous()
        .WithName("AuthStateDoctor.ServerState");
    }

    private static bool IsLoopback(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address);
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
