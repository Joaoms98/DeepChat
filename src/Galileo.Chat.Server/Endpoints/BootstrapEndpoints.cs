using Galileo.Chat.Crypto.Random;
using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Server.Endpoints;

/// <summary>
/// Anonymous register + room-creation endpoints. Safe only behind the IP
/// whitelist — assumes everyone on the LAN is trusted to bootstrap.
/// </summary>
public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Bootstrap");

        users.MapPost("/", async (RegisterRequest req, RegisterUserHandler handler, CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(
                    new RegisterUserCommand(req.Username, req.Nickname, req.Password), ct);
                return Results.Created($"/api/users/{result.Username}",
                    new { result.UserId, result.Username });
            }
            catch (DomainException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).AllowAnonymous();

        var rooms = app.MapGroup("/api/rooms").WithTags("Rooms");

        rooms.MapPost("/", async (CreateRoomRequest req, IRoomRepository repo, IClock clock, CancellationToken ct) =>
        {
            try
            {
                var name = RoomName.Create(req.Name);
                var existing = await repo.FindByNameAsync(name, ct);
                if (existing is not null)
                    return Results.Ok(ToDto(existing));

                var salt = SecureRandom.NewSalt();
                var room = Room.Create(name, salt, clock.UtcNow);
                await repo.AddAsync(room, ct);
                return Results.Created($"/api/rooms/{room.Name.Value}", ToDto(room));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        rooms.MapGet("/{name}", async (string name, IRoomRepository repo, CancellationToken ct) =>
        {
            try
            {
                var room = await repo.FindByNameAsync(RoomName.Create(name), ct);
                return room is null ? Results.NotFound() : Results.Ok(ToDto(room));
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }

    private static RoomDto ToDto(Room room) => new()
    {
        Id = room.Id,
        Name = room.Name.Value,
        SaltBase64 = Convert.ToBase64String(room.Salt)
    };
}
