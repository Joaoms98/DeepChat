using System.Net;
using Galileo.Chat.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Galileo.Chat.Server.Middleware;

/// <summary>
/// Rejects requests whose remote IP is not in the configured whitelist (single
/// IPs or CIDR blocks). Runs BEFORE authentication so unauthorized hosts can't
/// even probe the auth surface.
/// </summary>
public sealed class IpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly NetworkOptions _opts;
    private readonly IReadOnlyList<IpRule> _rules;
    private readonly ILogger<IpWhitelistMiddleware> _logger;

    public IpWhitelistMiddleware(
        RequestDelegate next,
        IOptions<NetworkOptions> opts,
        ILogger<IpWhitelistMiddleware> logger)
    {
        _next = next;
        _opts = opts.Value;
        _logger = logger;
        _rules = _opts.AllowedIps.Select(IpRule.Parse).ToList();
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var remote = ResolveRemoteIp(ctx);

        // No remote IP usually means an in-process call (TestServer, named pipes,
        // local mocks). Treat as loopback when loopback is allowed.
        if (remote is null)
        {
            if (_opts.AllowLoopback)
            {
                await _next(ctx);
                return;
            }
            _logger.LogWarning("Rejected request with no resolvable remote IP.");
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (_opts.AllowLoopback && IPAddress.IsLoopback(remote))
        {
            await _next(ctx);
            return;
        }

        foreach (var rule in _rules)
        {
            if (rule.Match(remote))
            {
                await _next(ctx);
                return;
            }
        }

        _logger.LogWarning("Rejected request from unauthorized IP {Ip} on path {Path}",
            remote, ctx.Request.Path);
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
    }

    private IPAddress? ResolveRemoteIp(HttpContext ctx)
    {
        if (_opts.TrustForwardedHeaders)
        {
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',', 2)[0].Trim();
                if (IPAddress.TryParse(first, out var fwd))
                    return fwd;
            }
        }
        return ctx.Connection.RemoteIpAddress;
    }
}

public static class IpWhitelistMiddlewareExtensions
{
    public static IApplicationBuilder UseIpWhitelist(this IApplicationBuilder app)
        => app.UseMiddleware<IpWhitelistMiddleware>();
}
