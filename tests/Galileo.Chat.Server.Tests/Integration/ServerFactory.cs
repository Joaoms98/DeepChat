using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Options;
using Galileo.Chat.Infrastructure.Persistence;
using Galileo.Chat.Server.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Galileo.Chat.Server.Tests.Integration;

/// <summary>
/// WebApplicationFactory wired against an in-memory SQLite database. The connection
/// stays open for the factory's lifetime so the schema survives across requests.
/// Each factory instance is its own isolated DB.
/// </summary>
public sealed class ServerFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _sqlite = new(
        new SqliteConnectionStringBuilder { DataSource = ":memory:", ForeignKeys = true }.ToString());

    public IList<string> AllowedIps { get; } = new List<string>();
    public string Secret { get; } = "test-secret-with-enough-entropy-for-jwt-key-derivation";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use "Development" so JwtBearer.RequireHttpsMetadata=false; TestServer
        // serves over HTTP-emulated transport, not real TLS.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:ConnectionString"] = "Data Source=:memory:",
                ["Persistence:RunMigrationsOnStartup"] = "true",
                ["Persistence:ApplySqlitePragmas"] = "false",  // PRAGMAs already on via cstring
                ["Jwt:Issuer"] = "DeepChat.Server",
                ["Jwt:Audience"] = "DeepChat.Client",
                ["Jwt:SecretKey"] = Secret,
                ["Jwt:AccessTokenLifetimeHours"] = "1",
                ["Network:AllowLoopback"] = "true",
                ["Serilog:MinimumLevel:Default"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the DbContext registration to point at our long-lived
            // in-memory SQLite connection.
            var dbDescriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>));
            services.Remove(dbDescriptor);

            _sqlite.Open();
            services.AddDbContext<ChatDbContext>(opt =>
                opt.UseSqlite(_sqlite,
                    sqlite => sqlite.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName)));

            // Override Network options at the DI layer so per-test changes apply
            // without rebuilding the whole factory. Requires consumers reading
            // IOptionsSnapshot<NetworkOptions> at request time — middleware uses
            // IOptions<...> which is a singleton, so we reconfigure post-hoc.
            services.PostConfigure<NetworkOptions>(opts =>
            {
                opts.AllowedIps.Clear();
                foreach (var ip in AllowedIps) opts.AllowedIps.Add(ip);
            });
        });
    }

    public async Task<RegisterUserResult> CreateUserAsync(string username, string nickname, string password)
    {
        using var scope = Services.CreateScope();
        var register = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        return await register.HandleAsync(new RegisterUserCommand(username, nickname, password));
    }

    public async Task<Room> CreateRoomAsync(string name)
    {
        using var scope = Services.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var salt = new byte[Room.SaltLength];
        Array.Fill(salt, (byte)0x55);
        var room = Room.Create(RoomName.Create(name), salt, clock.UtcNow);
        await rooms.AddAsync(room);
        return room;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _sqlite.Dispose();
        base.Dispose(disposing);
    }
}
