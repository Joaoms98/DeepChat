using Galileo.Chat.Domain.Common;
using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;

namespace Galileo.Chat.Domain.Entities;

public sealed class User
{
    public Guid Id { get; }
    public Username Username { get; }
    public Nickname Nickname { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; }

    private User(Guid id, Username username, Nickname nickname, string passwordHash, DateTime createdAt)
    {
        Id = id;
        Username = username;
        Nickname = nickname;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public static User Register(Username username, Nickname nickname, string passwordHash, DateTime utcNow)
    {
        Guard.NotNullOrWhiteSpace(passwordHash);
        return new User(Guid.NewGuid(), username, nickname, passwordHash, utcNow);
    }

    public void RecordLogin(DateTime utcNow)
    {
        if (!IsActive)
            throw new DomainException("Cannot record login on a deactivated user.");
        LastLoginAt = utcNow;
    }

    public void ChangeNickname(Nickname newNickname)
    {
        if (!IsActive)
            throw new DomainException("Cannot change nickname on a deactivated user.");
        Nickname = newNickname;
    }

    public void ChangePassword(string newHash)
    {
        Guard.NotNullOrWhiteSpace(newHash);
        if (!IsActive)
            throw new DomainException("Cannot change password on a deactivated user.");
        PasswordHash = newHash;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>Used by EF Core / persistence to rehydrate without re-validating invariants.</summary>
    public static User Rehydrate(Guid id, Username username, Nickname nickname, string passwordHash,
        DateTime createdAt, DateTime? lastLoginAt, bool isActive)
    {
        var user = new User(id, username, nickname, passwordHash, createdAt)
        {
            LastLoginAt = lastLoginAt,
            IsActive = isActive
        };
        return user;
    }
}
