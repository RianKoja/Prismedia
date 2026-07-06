using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Security;
using Prismedia.Domain.Capabilities;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;

namespace Prismedia.Infrastructure.Entities.Mappers.Capabilities;

/// <summary>
/// Hydrates and persists the playback capability against the current user's
/// <c>user_entity_states</c> row. Without an authenticated user (worker, system
/// context) the capability hydrates empty and persists nothing — playback state is a
/// user opinion, never a system fact.
/// </summary>
internal sealed class PlaybackCapabilityMapper(PrismediaDbContext db, ICurrentUserContext currentUser) : IEntityCapabilityMapper {
    public async Task HydrateAsync(Entity entity, CancellationToken cancellationToken) {
        var userId = currentUser.UserId;
        if (userId == Guid.Empty) {
            return;
        }

        var row = await db.UserEntityStates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.EntityId == entity.Id, cancellationToken);
        if (row is null || !UserEntityStateColumns.HasPlayback(row)) {
            return;
        }

        entity.RemoveCapability<CapabilityPlayback>();
        entity.AddCapability(new CapabilityPlayback(new CapabilityPlayback.State(
            row.PlayCount,
            row.SkipCount,
            TimeSpan.FromSeconds(row.PlayDurationSeconds),
            TimeSpan.FromSeconds(row.ResumeSeconds),
            row.LastPlayedAt,
            row.CompletedAt)));
    }

    // No-op by design: PersistAsync upserts the user-state row in place (find then update), so
    // playback state must survive a clear/persist cycle rather than be deleted and re-added.
    // Clearing here would drop accumulated play counts and resume positions during an entity re-save.
    public Task ClearAsync(Entity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task PersistAsync(Entity entity, CancellationToken cancellationToken) {
        var userId = currentUser.UserId;
        if (userId == Guid.Empty || entity.PlaybackCapability is not { Value: { } playback }) {
            return;
        }

        var row = await UserEntityStateColumns.GetOrAddAsync(db, userId, entity.Id, cancellationToken);
        row.PlayCount = playback.PlayCount;
        row.SkipCount = playback.SkipCount;
        row.PlayDurationSeconds = playback.PlayDuration.TotalSeconds;
        row.ResumeSeconds = playback.ResumeTime.TotalSeconds;
        row.LastPlayedAt = playback.LastPlayedAt;
        row.CompletedAt = playback.CompletedAt;
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
