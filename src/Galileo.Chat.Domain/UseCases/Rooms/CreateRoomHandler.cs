using System.Security.Cryptography;
using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.UseCases.Rooms;

public sealed class CreateRoomHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;

    public CreateRoomHandler(IRoomRepository rooms, IClock clock)
    {
        _rooms = rooms;
        _clock = clock;
    }

    public async Task<CreateRoomResult> HandleAsync(CreateRoomCommand cmd, CancellationToken ct = default)
    {
        var name = RoomName.Create(cmd.Name);
        if (await _rooms.FindByNameAsync(name, ct) is not null)
            throw new DomainException("Room already exists.");

        var salt = RandomNumberGenerator.GetBytes(Room.SaltLength);
        var room = Room.Create(name, salt, _clock.UtcNow);
        await _rooms.AddAsync(room, ct);

        return new CreateRoomResult(room.Id, room.Name.Value, room.Salt, room.CreatedAt);
    }
}

public sealed class GetRoomByNameHandler
{
    private readonly IRoomRepository _rooms;
    public GetRoomByNameHandler(IRoomRepository rooms) => _rooms = rooms;

    public async Task<CreateRoomResult?> HandleAsync(string name, CancellationToken ct = default)
    {
        var room = await _rooms.FindByNameAsync(RoomName.Create(name), ct);
        return room is null ? null : new CreateRoomResult(room.Id, room.Name.Value, room.Salt, room.CreatedAt);
    }
}
