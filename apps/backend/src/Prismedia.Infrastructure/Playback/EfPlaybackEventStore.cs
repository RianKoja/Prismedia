using Prismedia.Application.Playback;
using Prismedia.Application.Security;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Playback;

/// <summary>
/// EF Core implementation of the durable playback-history event append port. Events are
/// stamped with the current user (null marks pre-multi-user household history).
/// </summary>
public sealed class EfPlaybackEventStore(PrismediaDbContext db, ICurrentUserContext currentUser) : IPlaybackEventStore {
    /// <inheritdoc />
    public Task StageAsync(PlaybackEventAppend entry, CancellationToken cancellationToken) {
        db.EntityPlaybackEvents.Add(new EntityPlaybackEventRow {
            Id = Guid.NewGuid(),
            EntityId = entry.EntityId,
            UserId = currentUser.UserId == Guid.Empty ? null : currentUser.UserId,
            Kind = entry.Kind,
            OccurredAt = entry.OccurredAt,
            PositionSeconds = entry.PositionSeconds,
            DurationSeconds = entry.DurationSeconds,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AppendAsync(PlaybackEventAppend entry, CancellationToken cancellationToken) {
        await StageAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
