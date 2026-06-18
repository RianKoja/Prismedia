using Microsoft.Extensions.DependencyInjection;
using Prismedia.Application.Entities;

namespace Prismedia.Application.Playback;

/// <summary>
/// Tracks Jellyfin-compatible audio requests so third-party clients that only request the next
/// track can still produce quick-abandon skip events.
/// </summary>
public interface IJellyfinAudioPlaybackTracker {
    /// <summary>Observes an audio stream/file request.</summary>
    Task ObserveRequestAsync(JellyfinAudioPlaybackRequest request, CancellationToken cancellationToken);

    /// <summary>Observes a Jellyfin playback session progress-like report.</summary>
    Task ObserveProgressAsync(JellyfinAudioPlaybackProgress progress, CancellationToken cancellationToken);

    /// <summary>Marks the current client item as completed when a Jellyfin user-data endpoint reports it.</summary>
    void ObserveCompleted(JellyfinAudioPlaybackCompletion completion);
}

/// <summary>Jellyfin audio stream/file request observation.</summary>
public sealed record JellyfinAudioPlaybackRequest(
    string ClientKey,
    Guid ItemId,
    bool IsHeadRequest,
    bool IsRangeRequest);

/// <summary>Jellyfin session progress observation.</summary>
public sealed record JellyfinAudioPlaybackProgress(
    string ClientKey,
    Guid ItemId,
    long? PositionTicks);

/// <summary>Jellyfin completion observation.</summary>
public sealed record JellyfinAudioPlaybackCompletion(string ClientKey, Guid ItemId);

/// <summary>
/// In-memory implementation of Jellyfin audio skip inference.
/// </summary>
public sealed class JellyfinAudioPlaybackTracker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IJellyfinAudioPlaybackTracker {
    private static readonly TimeSpan SkipWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);
    private readonly object _gate = new();
    private readonly Dictionary<string, ClientPlaybackState> _states = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task ObserveRequestAsync(JellyfinAudioPlaybackRequest request, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.ClientKey) || request.IsHeadRequest) {
            return;
        }

        var now = timeProvider.GetUtcNow();
        SkipCandidate? candidate = null;

        lock (_gate) {
            PruneExpired(now);

            if (!_states.TryGetValue(request.ClientKey, out var previous)) {
                _states[request.ClientKey] = ClientPlaybackState.ForRequest(request.ItemId, now);
                return;
            }

            if (previous.ItemId == request.ItemId) {
                return;
            }

            if (ShouldRecordSkip(previous, now)) {
                candidate = new SkipCandidate(previous.ItemId, previous.MaxPositionSeconds);
                previous = previous with { SkipRecorded = true };
            }

            _states[request.ClientKey] = ClientPlaybackState.ForRequest(request.ItemId, now);
        }

        if (candidate is not null) {
            await RecordSkipAsync(candidate, now, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task ObserveProgressAsync(JellyfinAudioPlaybackProgress progress, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(progress.ClientKey)) {
            return Task.CompletedTask;
        }

        var now = timeProvider.GetUtcNow();
        var seconds = progress.PositionTicks is { } ticks && ticks > 0
            ? ticks / (double)TimeSpan.TicksPerSecond
            : 0;

        lock (_gate) {
            PruneExpired(now);

            if (!_states.TryGetValue(progress.ClientKey, out var previous)) {
                return Task.CompletedTask;
            }

            if (previous.ItemId != progress.ItemId) {
                return Task.CompletedTask;
            }

            _states[progress.ClientKey] = previous with {
                MaxPositionSeconds = Math.Max(previous.MaxPositionSeconds, seconds),
                LastObservedAt = now
            };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ObserveCompleted(JellyfinAudioPlaybackCompletion completion) {
        if (string.IsNullOrWhiteSpace(completion.ClientKey)) {
            return;
        }

        var now = timeProvider.GetUtcNow();
        lock (_gate) {
            PruneExpired(now);

            if (_states.TryGetValue(completion.ClientKey, out var previous) &&
                previous.ItemId == completion.ItemId) {
                _states[completion.ClientKey] = previous with {
                    Completed = true,
                    LastObservedAt = now
                };
                return;
            }

            _states[completion.ClientKey] = ClientPlaybackState.ForRequest(completion.ItemId, now) with {
                Completed = true
            };
        }
    }

    private static bool ShouldRecordSkip(ClientPlaybackState previous, DateTimeOffset now) =>
        !previous.Completed &&
        !previous.SkipRecorded &&
        now - previous.RequestedAt <= SkipWindow &&
        previous.MaxPositionSeconds <= SkipWindow.TotalSeconds;

    private async Task RecordSkipAsync(
        SkipCandidate candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var capabilities = scope.ServiceProvider.GetRequiredService<EntityCapabilityService>();
        await capabilities.RecordSkippedPlaybackAsync(
            candidate.ItemId,
            now,
            candidate.PositionSeconds,
            durationSeconds: null,
            cancellationToken);
    }

    private void PruneExpired(DateTimeOffset now) {
        var expired = _states
            .Where(pair => now - pair.Value.LastObservedAt > StateTtl)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var key in expired) {
            _states.Remove(key);
        }
    }

    private sealed record ClientPlaybackState(
        Guid ItemId,
        DateTimeOffset RequestedAt,
        DateTimeOffset LastObservedAt,
        double MaxPositionSeconds,
        bool Completed,
        bool SkipRecorded) {
        public static ClientPlaybackState ForRequest(Guid itemId, DateTimeOffset now) =>
            new(itemId, now, now, 0, Completed: false, SkipRecorded: false);
    }

    private sealed record SkipCandidate(Guid ItemId, double PositionSeconds);
}
