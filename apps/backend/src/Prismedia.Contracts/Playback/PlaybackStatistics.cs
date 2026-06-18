using Prismedia.Domain.Entities;

namespace Prismedia.Contracts.Playback;

/// <summary>
/// Time-bounded playback statistics built from durable playback-history events.
/// </summary>
/// <param name="From">Inclusive lower bound used for the statistics window.</param>
/// <param name="To">Exclusive upper bound used for the statistics window.</param>
/// <param name="TotalEvents">Total event count in the window.</param>
/// <param name="CompletedCount">Completed playback count in the window.</param>
/// <param name="SkippedCount">Skip count in the window.</param>
/// <param name="DistinctEntityCount">Number of unique entities with events in the window.</param>
/// <param name="TopEntities">Most active entities in the window.</param>
/// <param name="RecentEvents">Most recent playback events in the window.</param>
/// <param name="DailyEvents">Daily event buckets for timeline charts.</param>
public sealed record PlaybackStatisticsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalEvents,
    int CompletedCount,
    int SkippedCount,
    int DistinctEntityCount,
    IReadOnlyList<PlaybackStatisticsEntity> TopEntities,
    IReadOnlyList<PlaybackStatisticsEvent> RecentEvents,
    IReadOnlyList<PlaybackStatisticsBucket> DailyEvents);

/// <summary>Playback statistics for one entity.</summary>
public sealed record PlaybackStatisticsEntity(
    Guid Id,
    EntityKind Kind,
    string Title,
    string? CoverUrl,
    int CompletedCount,
    int SkippedCount,
    DateTimeOffset LastEventAt);

/// <summary>Recent playback-history event summary.</summary>
public sealed record PlaybackStatisticsEvent(
    Guid Id,
    Guid EntityId,
    EntityKind EntityKind,
    string EntityTitle,
    string? CoverUrl,
    PlaybackEventKind Kind,
    DateTimeOffset OccurredAt,
    double? PositionSeconds,
    double? DurationSeconds);

/// <summary>Daily playback event bucket.</summary>
public sealed record PlaybackStatisticsBucket(
    DateOnly Date,
    int CompletedCount,
    int SkippedCount);
