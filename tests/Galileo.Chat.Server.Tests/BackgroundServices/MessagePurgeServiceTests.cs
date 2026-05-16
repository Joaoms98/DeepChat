using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Persistence;
using Galileo.Chat.Infrastructure.Persistence.Repositories;
using Galileo.Chat.Infrastructure.Time;
using Galileo.Chat.Server.BackgroundServices;
using Galileo.Chat.Server.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Galileo.Chat.Server.Tests.BackgroundServices;

public sealed class MessagePurgeServiceTests
{
    private static EncryptedPayload Payload()
    {
        var iv = new byte[EncryptedPayload.IvLength];
        var cipher = new byte[] { 1, 2, 3 };
        var tag = new byte[EncryptedPayload.TagLength];
        return EncryptedPayload.Create(iv, cipher, tag);
    }

    [Fact]
    public async Task RunCycle_deletes_messages_older_than_TTL()
    {
        var (services, conn) = await BuildContainerAsync();
        try
        {
            var now = DateTime.UtcNow;
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                var room = Guid.NewGuid();
                var sender = Guid.NewGuid();

                db.Messages.Add(Message.CreateBroadcast(sender, room, Payload(), now.AddHours(-25)));
                db.Messages.Add(Message.CreateBroadcast(sender, room, Payload(), now.AddHours(-30)));
                db.Messages.Add(Message.CreateBroadcast(sender, room, Payload(), now)); // fresh
                await db.SaveChangesAsync();
            }

            // BackgroundService.StartAsync returns once ExecuteAsync hits its first
            // await — we must give the initial PurgeOnceAsync time to complete before
            // signalling stop, otherwise the cancellation propagates to ExecuteDeleteAsync
            // and no rows are removed.
            var sut = ActivatorUtilities.CreateInstance<MessagePurgeService>(services);
            await sut.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await sut.StopAsync(CancellationToken.None);

            using var verify = services.CreateScope();
            var verifyDb = verify.ServiceProvider.GetRequiredService<ChatDbContext>();
            var remaining = await verifyDb.Messages.CountAsync();
            remaining.Should().Be(1);
        }
        finally
        {
            await conn.DisposeAsync();
        }
    }

    private static async Task<(ServiceProvider services, SqliteConnection conn)> BuildContainerAsync()
    {
        var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = ":memory:", ForeignKeys = true }.ToString());
        await conn.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<ChatDbContext>(opt => opt.UseSqlite(conn))
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<IMessageRepository, MessageRepository>()
            .AddScoped<ISessionRepository, SessionRepository>()
            .Configure<RetentionOptions>(o =>
            {
                o.MessageTtlHours = 24;
                o.PurgeIntervalMinutes = 1;
                o.VacuumHourUtc = -1;
            })
            .AddSingleton(NullLoggerFactory.Instance)
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
                          typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>))
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            await db.Database.MigrateAsync();
        }

        return (services, conn);
    }
}
