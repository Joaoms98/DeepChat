namespace Galileo.Chat.Infrastructure.Options;

/// <summary>
/// Bound from "Persistence" section of appsettings.
/// Defaults are sane for a local/embedded SQLite deployment.
/// </summary>
public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    /// <summary>
    /// SQLite connection string. Leave default for "Data Source=deepchat.db".
    /// Tests inject "Data Source=:memory:" with a long-lived connection.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=deepchat.db;Cache=Shared";

    /// <summary>
    /// When true (production default), a Migrate() runs at startup.
    /// Tests/dev may disable to keep schema management explicit.
    /// </summary>
    public bool RunMigrationsOnStartup { get; set; } = true;

    /// <summary>
    /// When true, applies WAL + safety PRAGMAs after Migrate().
    /// </summary>
    public bool ApplySqlitePragmas { get; set; } = true;
}
