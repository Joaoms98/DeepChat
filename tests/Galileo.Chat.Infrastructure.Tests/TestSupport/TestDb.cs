using Galileo.Chat.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Galileo.Chat.Infrastructure.Tests.TestSupport;

/// <summary>
/// Real SQLite, in-memory database scoped to a single test. We deliberately do not
/// use EF Core's InMemory provider — it skips relational semantics (FK enforcement,
/// constraint violations, value converter quirks) and would let bugs through that
/// real SQLite would catch. The connection is kept open for the test's lifetime
/// because ":memory:" databases vanish when the connection closes.
/// </summary>
public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ChatDbContext Context { get; }

    private TestDb(SqliteConnection connection, ChatDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public static async Task<TestDb> CreateAsync()
    {
        // ForeignKeys=True must be set on the connection string. Setting it via
        // PRAGMA after open is silently ignored once any statement has run on
        // the connection (SQLite restriction).
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true
        };
        var connection = new SqliteConnection(csb.ToString());
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new ChatDbContext(options);
        await context.Database.MigrateAsync();

        return new TestDb(connection, context);
    }

    /// <summary>Reopens a fresh DbContext on the same in-memory database (verifies persistence across contexts).</summary>
    public ChatDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;
        return new ChatDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
