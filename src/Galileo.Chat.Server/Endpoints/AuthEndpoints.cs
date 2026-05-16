using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Server.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext ctx, LoginHandler handler, CancellationToken ct) =>
        {
            var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            try
            {
                var result = await handler.HandleAsync(
                    new LoginCommand(request.Username, request.Password, remoteIp), ct);

                return Results.Ok(new LoginResponse
                {
                    Token = result.Token,
                    ExpiresAt = result.ExpiresAt,
                    UserId = result.UserId,
                    Nickname = result.Nickname
                });
            }
            catch (AuthenticationFailedException)
            {
                // Generic 401 — never leak whether the user exists or which step failed.
                return Results.Unauthorized();
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("login");

        return app;
    }
}
