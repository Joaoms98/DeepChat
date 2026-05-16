using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Tests.TestSupport;

namespace Galileo.Chat.Infrastructure.Tests.Persistence;

public sealed class RoomRepositoryTests
{
    [Fact]
    public async Task AddAsync_then_FindByName_round_trips_room_with_salt_intact()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new RoomRepository(db.Context);
        var room = TestData.NewRoom("backend");

        await repo.AddAsync(room);

        await using var fresh = db.NewContext();
        var found = await new RoomRepository(fresh).FindByNameAsync(RoomName.Create("backend"));

        found.Should().NotBeNull();
        found!.Id.Should().Be(room.Id);
        found.Name.Value.Should().Be("backend");
        found.Salt.Should().Equal(room.Salt);
    }

    [Fact]
    public async Task ListAll_returns_rooms_ordered_by_creation_time()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new RoomRepository(db.Context);

        await repo.AddAsync(TestData.NewRoom("backend", TestData.FixedNow.AddMinutes(1)));
        await repo.AddAsync(TestData.NewRoom("ops", TestData.FixedNow.AddMinutes(3)));
        await repo.AddAsync(TestData.NewRoom("frontend", TestData.FixedNow.AddMinutes(2)));

        var list = await repo.ListAllAsync();

        list.Select(r => r.Name.Value).Should().Equal("backend", "frontend", "ops");
    }

    [Fact]
    public async Task AddAsync_with_duplicate_name_throws_on_unique_index()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new RoomRepository(db.Context);
        await repo.AddAsync(TestData.NewRoom("backend"));

        var act = async () => await repo.AddAsync(TestData.NewRoom("backend"));

        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task UpdateAsync_persists_rotated_salt()
    {
        await using var db = await TestDb.CreateAsync();
        var repo = new RoomRepository(db.Context);
        var room = TestData.NewRoom();
        await repo.AddAsync(room);

        var newSalt = new byte[16];
        Array.Fill(newSalt, (byte)0x55);
        room.RotateSalt(newSalt);
        await repo.UpdateAsync(room);

        await using var fresh = db.NewContext();
        var found = await new RoomRepository(fresh).FindByIdAsync(room.Id);
        found!.Salt.Should().Equal(newSalt);
    }
}
