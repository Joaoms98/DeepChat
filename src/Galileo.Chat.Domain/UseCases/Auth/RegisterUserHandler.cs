using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.UseCases.Auth;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public RegisterUserHandler(IUserRepository users, IPasswordHasher hasher, IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(cmd.Password) || cmd.Password.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");

        var username = Username.Create(cmd.Username);
        var nickname = Nickname.Create(cmd.Nickname);

        if (await _users.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        var hash = _hasher.Hash(cmd.Password);
        var user = User.Register(username, nickname, hash, _clock.UtcNow);

        await _users.AddAsync(user, ct);
        return new RegisterUserResult(user.Id, user.Username.Value);
    }
}
