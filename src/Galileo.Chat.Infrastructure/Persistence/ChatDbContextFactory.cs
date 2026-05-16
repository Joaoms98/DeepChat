using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Galileo.Chat.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time so it can build the ChatDbContext
/// without booting the Server project. Do not use at runtime — production wiring
/// goes through DependencyInjection.AddInfrastructure.
/// </summary>
public sealed class ChatDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite("Data Source=deepchat.design.db",
                sql => sql.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName))
            .Options;

        return new ChatDbContext(options);
    }
}
