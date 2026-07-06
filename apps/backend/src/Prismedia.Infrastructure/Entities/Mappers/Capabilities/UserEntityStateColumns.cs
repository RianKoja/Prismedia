using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Entities.Mappers.Capabilities;

/// <summary>
/// Shared helpers over the wide <see cref="UserEntityStateRow"/>: which column families
/// a row actually carries (playback vs reading progress vs opinion flags), and the
/// find-or-add upsert both capability mappers use.
/// </summary>
internal static class UserEntityStateColumns {
    /// <summary>True when the row records any playback engagement (videos/audio).</summary>
    internal static bool HasPlayback(UserEntityStateRow row) =>
        row.PlayCount > 0 ||
        row.SkipCount > 0 ||
        row.PlayDurationSeconds > 0 ||
        row.ResumeSeconds > 0 ||
        row.LastPlayedAt is not null ||
        row.CompletedAt is not null;

    /// <summary>True when the row records any reading progress (books/comics).</summary>
    internal static bool HasProgress(UserEntityStateRow row) =>
        row.ProgressCurrentEntityId is not null ||
        row.ProgressIndex != 0 ||
        row.ProgressTotal != 0 ||
        row.ProgressLocation is not null ||
        row.ProgressCompletedAt is not null;

    /// <summary>
    /// Finds the (user, entity) state row — preferring one already tracked in this unit of
    /// work so both mappers and the repository compose their writes — or adds a fresh one.
    /// </summary>
    internal static async Task<UserEntityStateRow> GetOrAddAsync(
        PrismediaDbContext db,
        Guid userId,
        Guid entityId,
        CancellationToken cancellationToken) {
        var tracked = db.ChangeTracker.Entries<UserEntityStateRow>()
            .FirstOrDefault(entry => entry.Entity.UserId == userId && entry.Entity.EntityId == entityId)
            ?.Entity;
        if (tracked is not null) {
            return tracked;
        }

        var row = await db.UserEntityStates.FindAsync([userId, entityId], cancellationToken);
        if (row is not null) {
            return row;
        }

        row = new UserEntityStateRow {
            UserId = userId,
            EntityId = entityId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UserEntityStates.Add(row);
        return row;
    }
}
