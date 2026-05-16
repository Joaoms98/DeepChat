namespace Galileo.Chat.Server.Configuration;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>How old a message must be before the purge service deletes it. Default 24h.</summary>
    public int MessageTtlHours { get; set; } = 24;

    /// <summary>How often the purge cycle runs. Default 5 min.</summary>
    public int PurgeIntervalMinutes { get; set; } = 5;

    /// <summary>UTC hour at which to run nightly VACUUM (0–23). Default 03:00 UTC.</summary>
    public int VacuumHourUtc { get; set; } = 3;

    public TimeSpan MessageTtl => TimeSpan.FromHours(MessageTtlHours);
    public TimeSpan PurgeInterval => TimeSpan.FromMinutes(PurgeIntervalMinutes);
}
