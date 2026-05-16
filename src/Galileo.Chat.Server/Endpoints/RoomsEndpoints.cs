using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Server.Endpoints;

/// <summary>GET /api/rooms (lista). POST e GET-by-name vivem em BootstrapEndpoints.</summary>
public static class RoomsEndpoints
{
    public static IEndpointRouteBuilder MapRoomsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms")
                       .WithTags("Rooms")
                       .RequireAuthorization();

        group.MapGet("/", async (ListRoomsHandler handler, CancellationToken ct) =>
        {
            var rooms = await handler.HandleAsync(ct);
            return Results.Ok(rooms.Select(r => new RoomDto
            {
                Id = r.Id,
                Name = r.Name.Value,
                SaltBase64 = Convert.ToBase64String(r.Salt)
            }));
        });

        return app;
    }
}
