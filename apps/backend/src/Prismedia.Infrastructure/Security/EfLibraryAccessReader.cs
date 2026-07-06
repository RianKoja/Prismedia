using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Security;
using Prismedia.Infrastructure.Persistence;

namespace Prismedia.Infrastructure.Security;

/// <summary>EF-backed read port for per-user library access grants.</summary>
public sealed class EfLibraryAccessReader : ILibraryAccessReader {
    private readonly PrismediaDbContext _db;

    public EfLibraryAccessReader(PrismediaDbContext db) {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetAllowedRootIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await _db.UserLibraryAccess.AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => row.LibraryRootId)
            .ToArrayAsync(cancellationToken))
        .ToHashSet();
}
