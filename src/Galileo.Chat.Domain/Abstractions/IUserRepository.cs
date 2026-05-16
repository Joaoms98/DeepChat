using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> FindByUsernameAsync(Username username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
