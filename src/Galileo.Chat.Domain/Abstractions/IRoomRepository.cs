using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Abstractions;

public interface IRoomRepository
{
    Task<Room?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> FindByNameAsync(RoomName name, CancellationToken ct = default);
    Task<IReadOnlyList<Room>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task UpdateAsync(Room room, CancellationToken ct = default);
}

/// <summary>Thin handler so the endpoint stays trivial.</summary>
public sealed class ListRoomsHandler
{
    private readonly IRoomRepository _rooms;
    public ListRoomsHandler(IRoomRepository rooms) => _rooms = rooms;

    public Task<IReadOnlyList<Room>> HandleAsync(CancellationToken ct = default) =>
        _rooms.ListAllAsync(ct);
}
