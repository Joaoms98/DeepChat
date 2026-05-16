using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository : IRoomRepository
{
    private readonly ChatDbContext _db;

    public RoomRepository(ChatDbContext db) => _db = db;

    public Task<Room?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Room?> FindByNameAsync(RoomName name, CancellationToken ct = default) =>
        _db.Rooms.FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<Room>> ListAllAsync(CancellationToken ct = default) =>
        await _db.Rooms.AsNoTracking().OrderBy(r => r.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Room room, CancellationToken ct = default)
    {
        await _db.Rooms.AddAsync(room, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        _db.Rooms.Update(room);
        await _db.SaveChangesAsync(ct);
    }
}
