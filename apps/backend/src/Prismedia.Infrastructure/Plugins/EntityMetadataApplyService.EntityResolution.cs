using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Plugins;

public sealed partial class EntityMetadataApplyService {
    private static bool IsKindCompatible(string entityKind, string expectedKind) =>
        entityKind.Equals(expectedKind, StringComparison.OrdinalIgnoreCase) ||
        (entityKind.Equals(EntityKindRegistry.Movie.Code, StringComparison.OrdinalIgnoreCase) &&
            (expectedKind.Equals(EntityKindRegistry.Video.Code, StringComparison.OrdinalIgnoreCase) ||
             expectedKind.Equals("video-movie", StringComparison.OrdinalIgnoreCase))) ||
        (entityKind.Equals(EntityKindRegistry.Video.Code, StringComparison.OrdinalIgnoreCase) &&
            expectedKind.Equals("video-episode", StringComparison.OrdinalIgnoreCase));

    private async Task<EntityRow?> FindEntityByKindAndTitleAsync(string kind, string title, CancellationToken cancellationToken) =>
        _db.Entities.Local.FirstOrDefault(
            row => row.KindCode == kind && row.Title.Equals(title, StringComparison.OrdinalIgnoreCase) && row.DeletedAt == null)
        ?? await _db.Entities.FirstOrDefaultAsync(
            row => row.KindCode == kind && row.Title.ToLower() == title.ToLower() && row.DeletedAt == null,
            cancellationToken);

    private EntityRow CreateEntity(string kind, string title, DateTimeOffset now) {
        var entity = new EntityRow {
            Id = Guid.NewGuid(),
            KindCode = kind,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Entities.Add(entity);
        return entity;
    }
}
