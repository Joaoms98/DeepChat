using Galileo.Chat.Domain.Exceptions;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Tests.Persistence;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task AddAsync_then_FindById_returns_persisted_user()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new UserRepository(db.Context);
        var user = TestData.NewUser();

        await repo.AddAsync(user);

        await using var fresh = db.NewContext();
        var freshRepo = new UserRepository(fresh);
        var found = await freshRepo.FindByIdAsync(user.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
        found.Username.Value.Should().Be("alice");
        found.Nickname.Value.Should().Be("Alice");
        found.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task FindByUsername_returns_match_case_insensitively_via_normalization()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new UserRepository(db.Context);
        await repo.AddAsync(TestData.NewUser("alice"));

        var found = await repo.FindByUsernameAsync(Username.Create("ALICE"));

        found.Should().NotBeNull();
        found!.Username.Value.Should().Be("alice");
    }

    [Fact]
    public async Task ExistsByUsername_returns_false_for_unknown_user()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new UserRepository(db.Context);

        var exists = await repo.ExistsByUsernameAsync(Username.Create("ghost"));

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_with_duplicate_username_throws_on_unique_index()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new UserRepository(db.Context);
        await repo.AddAsync(TestData.NewUser("alice", "Alice A"));

        var dup = TestData.NewUser("alice", "Alice B");
        var act = async () => await repo.AddAsync(dup);

        // SQLite raises a UNIQUE constraint violation; EF wraps as DbUpdateException.
        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task UpdateAsync_persists_changed_nickname_and_LastLoginAt()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new UserRepository(db.Context);
        var user = TestData.NewUser();
        await repo.AddAsync(user);

        user.ChangeNickname(Nickname.Create("Alice da Silva"));
        user.RecordLogin(TestData.FixedNow.AddHours(1));
        await repo.UpdateAsync(user);

        await using var fresh = db.NewContext();
        var found = await new UserRepository(fresh).FindByIdAsync(user.Id);
        found!.Nickname.Value.Should().Be("Alice da Silva");
        found.LastLoginAt.Should().Be(TestData.FixedNow.AddHours(1));
    }

    [Fact]
    public async Task FindByUsername_throws_DomainValidationException_if_DB_has_invalid_username()
    {
        // Direct INSERT bypassing the converter — proves that corruption surfaces
        // at query time rather than producing an invalid in-memory entity.
        await using var db = await TestDb.CreateAsync();
        await db.Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Username, Nickname, PasswordHash, CreatedAt, IsActive) " +
            "VALUES ('11111111-1111-1111-1111-111111111111', '  ', 'X', 'h', '2026-05-15', 1);");

        var repo = new UserRepository(db.Context);
        var act = async () => await repo.FindByIdAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        await act.Should().ThrowAsync<DomainValidationException>();
    }
}
