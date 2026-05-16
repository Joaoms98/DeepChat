using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _byId = new();

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<User?> FindByUsernameAsync(Username username, CancellationToken ct = default) =>
        Task.FromResult<User?>(_byId.Values.FirstOrDefault(u => u.Username == username));

    public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) =>
        Task.FromResult(_byId.Values.Any(u => u.Username == username));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }
}
