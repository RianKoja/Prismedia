using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Videos;

namespace Prismedia.Infrastructure.Videos;

/// <summary>
/// Periodically reaps transcode and remux ffmpeg jobs whose playback session has gone silent — a
/// closed tab, a crashed client, or a long pause — or that have run past a hard lifetime ceiling, so
/// abandoned encodings cannot accumulate and saturate the host. Liveness is the heartbeat the player
/// already sends (<see cref="ITranscodeSessionService.Ping"/>); cancelling a job tears down its
/// ffmpeg process tree while leaving already-produced segments in the cache, so a reaped session
/// resumes from cache and only re-encodes from the frontier. This is the single, universal cleanup
/// path: every transcode and remux is keyed by item id, so one sweep covers them all.
/// </summary>
public sealed class TranscodeReaperService : BackgroundService {
    /// <summary>How often the reaper sweeps for abandoned jobs.</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A session counts as live while it has pinged within this window, and is dropped once it goes
    /// older. The player pings roughly every ten seconds while playing, so this tolerates a dozen
    /// missed heartbeats (brief stalls, pauses) before treating the viewer as gone.
    /// </summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromSeconds(120);

    /// <summary>Jobs younger than this are never reaped, so startup and brief gaps survive a sweep.</summary>
    private static readonly TimeSpan IdleGrace = TimeSpan.FromSeconds(60);

    /// <summary>Absolute ceiling: no single encoding outlives this, even if its session looks live.</summary>
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(6);

    private readonly ITranscodeSessionService _sessions;
    private readonly ILogger<TranscodeReaperService> _logger;

    /// <summary>Creates the reaper over the shared transcode session registry.</summary>
    public TranscodeReaperService(
        ITranscodeSessionService sessions,
        ILogger<TranscodeReaperService> logger) {
        _sessions = sessions;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(SweepInterval);
        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                try {
                    Sweep();
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Transcode reaper sweep failed.");
                }
            }
        } catch (OperationCanceledException) {
            // Host is shutting down; nothing to clean up here.
        }
    }

    private void Sweep() {
        var liveItemIds = _sessions.LiveItemIds(SessionTtl);
        var staleSessions = _sessions.ReapStaleSessions(SessionTtl);
        var reapedJobs = HlsAssetService.ReapOrphanedJobs(liveItemIds, IdleGrace, MaxLifetime);
        if (staleSessions > 0 || reapedJobs > 0) {
            _logger.LogInformation(
                "Transcode reaper cleared {StaleSessions} abandoned session(s) and cancelled {ReapedJobs} orphaned or expired ffmpeg job(s).",
                staleSessions,
                reapedJobs);
        }
    }
}
