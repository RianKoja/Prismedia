using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Security;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Security;

/// <summary>EF-backed engagement-state cloning for first-run admin creation.</summary>
public sealed class EfUserEngagementCloner : IUserEngagementCloner {
    private readonly PrismediaDbContext _db;

    public EfUserEngagementCloner(PrismediaDbContext db) {
        _db = db;
    }

    /// <inheritdoc />
    public async Task CloneFromAnyUserAsync(Guid targetUserId, CancellationToken cancellationToken) {
        if (await _db.UserEntityStates.AnyAsync(state => state.UserId == targetUserId, cancellationToken)) {
            return;
        }

        var sourceUserId = await _db.UserEntityStates.AsNoTracking()
            .Where(state => state.UserId != targetUserId)
            .OrderBy(state => state.UserId)
            .Select(state => (Guid?)state.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (sourceUserId is null) {
            return;
        }

        var sourceRows = await _db.UserEntityStates.AsNoTracking()
            .Where(state => state.UserId == sourceUserId)
            .ToArrayAsync(cancellationToken);
        foreach (var source in sourceRows) {
            _db.UserEntityStates.Add(new UserEntityStateRow {
                UserId = targetUserId,
                EntityId = source.EntityId,
                IsFavorite = source.IsFavorite,
                RatingValue = source.RatingValue,
                PlayCount = source.PlayCount,
                SkipCount = source.SkipCount,
                PlayDurationSeconds = source.PlayDurationSeconds,
                ResumeSeconds = source.ResumeSeconds,
                LastPlayedAt = source.LastPlayedAt,
                CompletedAt = source.CompletedAt,
                ProgressCurrentEntityId = source.ProgressCurrentEntityId,
                ProgressUnit = source.ProgressUnit,
                ProgressIndex = source.ProgressIndex,
                ProgressTotal = source.ProgressTotal,
                ProgressMode = source.ProgressMode,
                ProgressLocation = source.ProgressLocation,
                ProgressCompletedAt = source.ProgressCompletedAt,
                UpdatedAt = source.UpdatedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
