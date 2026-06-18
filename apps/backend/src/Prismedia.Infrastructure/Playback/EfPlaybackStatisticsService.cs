using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Playback;
using Prismedia.Contracts.Playback;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Playback;

/// <summary>
/// EF Core read projection for playback statistics.
/// </summary>
public sealed class EfPlaybackStatisticsService(PrismediaDbContext db) : IPlaybackStatisticsService {
    private const int TopEntityLimit = 12;
    private const int RecentEventLimit = 30;

    /// <inheritdoc />
    public async Task<PlaybackStatisticsResponse> GetAsync(
        PlaybackStatisticsQuery query,
        CancellationToken cancellationToken) {
        var rows = await QueryRows(query).ToArrayAsync(cancellationToken);

        var coverByEntity = await LoadCoverPathsAsync(rows.Select(row => row.EntityId).Distinct().ToArray(), cancellationToken);
        var completedCount = rows.Count(row => row.Kind == PlaybackEventKind.Completed);
        var skippedCount = rows.Count(row => row.Kind == PlaybackEventKind.Skipped);

        var topEntities = rows
            .GroupBy(row => new { row.EntityId, row.EntityKindCode, row.EntityTitle })
            .Select(group => {
                var entityId = group.Key.EntityId;
                return new PlaybackStatisticsEntity(
                    entityId,
                    group.Key.EntityKindCode.DecodeAs<EntityKind>(),
                    group.Key.EntityTitle,
                    coverByEntity.GetValueOrDefault(entityId),
                    group.Count(row => row.Kind == PlaybackEventKind.Completed),
                    group.Count(row => row.Kind == PlaybackEventKind.Skipped),
                    group.Max(row => row.OccurredAt));
            })
            .OrderByDescending(entity => entity.CompletedCount)
            .ThenByDescending(entity => entity.SkippedCount)
            .ThenByDescending(entity => entity.LastEventAt)
            .Take(TopEntityLimit)
            .ToArray();

        var recentEvents = rows
            .Take(RecentEventLimit)
            .Select(row => new PlaybackStatisticsEvent(
                row.EventId,
                row.EntityId,
                row.EntityKindCode.DecodeAs<EntityKind>(),
                row.EntityTitle,
                coverByEntity.GetValueOrDefault(row.EntityId),
                row.Kind,
                row.OccurredAt,
                row.PositionSeconds,
                row.DurationSeconds))
            .ToArray();

        var dailyEvents = rows
            .GroupBy(row => DateOnly.FromDateTime(row.OccurredAt.UtcDateTime.Date))
            .Select(group => new PlaybackStatisticsBucket(
                group.Key,
                group.Count(row => row.Kind == PlaybackEventKind.Completed),
                group.Count(row => row.Kind == PlaybackEventKind.Skipped)))
            .OrderBy(bucket => bucket.Date)
            .ToArray();

        return new PlaybackStatisticsResponse(
            query.From,
            query.To,
            rows.Length,
            completedCount,
            skippedCount,
            rows.Select(row => row.EntityId).Distinct().Count(),
            topEntities,
            recentEvents,
            dailyEvents);
    }

    private IQueryable<PlaybackStatisticsRow> QueryRows(PlaybackStatisticsQuery query) {
        var events = db.EntityPlaybackEvents.AsNoTracking()
            .Where(evt => evt.OccurredAt >= query.From && evt.OccurredAt < query.To);
        if (query.EventKind is { } eventKind) {
            events = events.Where(evt => evt.Kind == eventKind);
        }

        var rows =
            from evt in events
            join entity in db.Entities.AsNoTracking() on evt.EntityId equals entity.Id
            where !query.HideNsfw || !entity.IsNsfw
            select new {
                EventId = evt.Id,
                evt.EntityId,
                EntityKindCode = entity.KindCode,
                EntityTitle = entity.Title,
                evt.Kind,
                evt.OccurredAt,
                evt.PositionSeconds,
                evt.DurationSeconds
            };

        if (query.Kind is { } kind) {
            var kindCode = kind.ToCode();
            rows = rows.Where(row => row.EntityKindCode == kindCode);
        }

        return rows
            .OrderByDescending(row => row.OccurredAt)
            .ThenByDescending(row => row.EventId)
            .Select(row => new PlaybackStatisticsRow(
                row.EventId,
                row.EntityId,
                row.EntityKindCode,
                row.EntityTitle,
                row.Kind,
                row.OccurredAt,
                row.PositionSeconds,
                row.DurationSeconds));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadCoverPathsAsync(
        IReadOnlyCollection<Guid> entityIds,
        CancellationToken cancellationToken) {
        if (entityIds.Count == 0) {
            return new Dictionary<Guid, string>();
        }

        var files = await db.EntityFiles.AsNoTracking()
            .Where(file => entityIds.Contains(file.EntityId))
            .Where(file => EntityCoverSelection.CoverRoles.Contains(file.Role))
            .ToArrayAsync(cancellationToken);

        return files
            .GroupBy(file => file.EntityId)
            .Select(group => new { EntityId = group.Key, File = EntityCoverSelection.Select(group) })
            .Where(item => item.File is not null)
            .ToDictionary(item => item.EntityId, item => item.File!.Path);
    }

    private sealed record PlaybackStatisticsRow(
        Guid EventId,
        Guid EntityId,
        string EntityKindCode,
        string EntityTitle,
        PlaybackEventKind Kind,
        DateTimeOffset OccurredAt,
        double? PositionSeconds,
        double? DurationSeconds);
}
