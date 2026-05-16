using Galileo.Chat.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Galileo.Chat.Infrastructure.Persistence;

/// <summary>
/// One-shot initializer invoked from the host on startup: applies pending
/// migrations and the SQLite PRAGMAs we rely on for safe concurrent access
/// and reasonable durability/perf trade-offs.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly ChatDbContext _db;
    private readonly PersistenceOptions _opts;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ChatDbContext db,
        IOptions<PersistenceOptions> opts,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_opts.RunMigrationsOnStartup)
        {
            _logger.LogInformation("Applying database migrations...");
            await _db.Database.MigrateAsync(ct);
        }

        if (_opts.ApplySqlitePragmas)
        {
            await ApplySqlitePragmasAsync(ct);
        }
    }

    private async Task ApplySqlitePragmasAsync(CancellationToken ct)
    {
        // WAL: better concurrency for our read-heavy + frequent-write workload.
        // synchronous=NORMAL: durable to OS crash, avoids fsync per commit (WAL safe).
        // foreign_keys=ON: SQLite default is OFF, EF Core depends on this.
        // temp_store=MEMORY: temp tables/indexes live in RAM (faster, smaller disk footprint).
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;", ct);
        _logger.LogInformation("SQLite PRAGMAs applied (WAL, sync=NORMAL, fk=ON, temp=MEM).");
    }
}
