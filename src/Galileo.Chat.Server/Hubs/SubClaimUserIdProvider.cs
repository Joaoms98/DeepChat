using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Galileo.Chat.Server.Hubs;

/// <summary>
/// SignalR's default IUserIdProvider reads ClaimTypes.NameIdentifier. With
/// the modern Microsoft.IdentityModel.JsonWebTokens stack the JWT 'sub' claim
/// is NOT auto-mapped to NameIdentifier, so Clients.User(userIdGuid) silently
/// routes to nobody. This provider reads 'sub' directly so PostPrivateMessage
/// reaches the real user.
/// </summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
