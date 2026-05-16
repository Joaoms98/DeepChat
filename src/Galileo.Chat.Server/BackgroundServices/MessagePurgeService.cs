using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Infrastructure.Persistence;
using Galileo.Chat.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galileo.Chat.Server.BackgroundServices;

/// <summary>
/// Periodically removes messages older than the configured TTL and runs a
/// nightly VACUUM. The 24h retention window is a non-negotiable privacy
/// requirement — if this service is down, alerts must fire.
/// </summary>
public sealed class MessagePurgeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RetentionOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<MessagePurgeService> _logger;

    public MessagePurgeService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RetentionOptions> options,
        IClock clock,
        ILogger<MessagePurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessagePurgeService starting (TTL={Hours}h, interval={Minutes}m).",
            _options.CurrentValue.MessageTtlHours, _options.CurrentValue.PurgeIntervalMinutes);

        // Initial sweep on boot — server may have been down across the TTL window.
        await PurgeOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(_options.CurrentValue.PurgeInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PurgeOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task PurgeOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var messages = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

            var now = _clock.UtcNow;
            var cutoff = now - _options.CurrentValue.MessageTtl;

            var purgedMsgs = await messages.PurgeOlderThanAsync(cutoff, ct);
            var purgedSessions = await sessions.PurgeExpiredAsync(now, ct);

            if (purgedMsgs > 0 || purgedSessions > 0)
            {
                _logger.LogInformation(
                    "Purged {Messages} expired messages and {Sessions} expired sessions (cutoff={Cutoff:u}).",
                    purgedMsgs, purgedSessions, cutoff);
            }

            if (now.Hour == _options.CurrentValue.VacuumHourUtc && now.Minute < _options.CurrentValue.PurgeIntervalMinutes)
            {
                var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                await db.Database.ExecuteSqlRawAsync("VACUUM;", ct);
                _logger.LogInformation("Nightly VACUUM completed at {Now:u}.", now);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // We never want a transient DB error to take the host down — a missed
            // purge cycle just means the next one will catch up. But the log
            // entry must be loud so monitoring picks it up.
            _logger.LogError(ex, "Purge cycle failed; will retry on next interval.");
        }
    }
}
