using Microsoft.EntityFrameworkCore;
using Prismedia.Contracts.Entities;
using Prismedia.Contracts.Plugins;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Plugins;

public sealed partial class EntityMetadataApplyService {
    /// <summary>
    /// Applies cascade metadata patch fields to an existing child entity.
    /// </summary>
    private async Task ApplyStructuralChildrenAsync(
        IReadOnlyList<EntityMetadataProposal> children,
        DateTimeOffset now,
        HashSet<Guid> visited,
        CancellationToken cancellationToken) {
        foreach (var child in children) {
            if (child.TargetEntityId is null) {
                continue;
            }

            if (!visited.Add(child.TargetEntityId.Value)) {
                continue;
            }

            var childEntity = await _db.Entities
                .FirstOrDefaultAsync(row => row.Id == child.TargetEntityId.Value && row.DeletedAt == null, cancellationToken);
            if (childEntity is null) {
                visited.Remove(child.TargetEntityId.Value);
                continue;
            }

            await ApplyPatchToEntityAsync(childEntity, child.Patch, child.Images, now, cancellationToken);
            var relationshipProposals = EntityMetadataProposalTraversal.Relationships(child);
            if (relationshipProposals.Count > 0 &&
                (child.Patch.Credits.Count > 0 || !string.IsNullOrWhiteSpace(child.Patch.Studio) || child.Patch.Tags.Count > 0)) {
                await ApplyRelationshipProposalsAsync(childEntity.Id, relationshipProposals, now, cancellationToken);
            }

            await ApplyStructuralChildrenAsync(EntityMetadataProposalTraversal.StructuralChildren(child), now, visited, cancellationToken);
            visited.Remove(child.TargetEntityId.Value);
        }
    }

    private async Task ApplyPatchToEntityAsync(
        EntityRow entity,
        EntityMetadataPatch patch,
        IReadOnlyList<ImageCandidate> images,
        DateTimeOffset now,
        CancellationToken cancellationToken) {
        if (!string.IsNullOrWhiteSpace(patch.Title)) {
            entity.Title = patch.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(patch.Description)) {
            await UpsertDescriptionAsync(entity.Id, patch.Description, now, cancellationToken);
        }

        if (patch.ExternalIds.Count > 0) {
            await UpsertExternalIdsAsync(entity.Id, patch.ExternalIds, patch.Urls, now, cancellationToken);
        }

        if (patch.Urls.Count > 0) {
            await UpsertUrlsAsync(entity.Id, patch.Urls, now, cancellationToken);
        }

        if (patch.Tags.Count > 0) {
            await ReplaceTagsAsync(entity.Id, patch.Tags, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(patch.Studio)) {
            await SetStudioAsync(entity.Id, patch.Studio, now, cancellationToken);
        }

        if (patch.Credits.Count > 0) {
            await ReplaceCreditsAsync(entity.Id, patch.Credits, now, cancellationToken);
        }

        if (patch.Dates.Count > 0) {
            await UpsertDatesAsync(entity.Id, patch.Dates, now, cancellationToken);
        }

        if (patch.Stats.Count > 0) {
            await UpsertStatsAsync(entity.Id, patch.Stats, now, cancellationToken);
        }

        if (patch.Positions.Count > 0) {
            var normalizedPositions = EntityMetadataPositionRules.Normalize(patch.Positions);
            await UpsertPositionsAsync(entity, normalizedPositions, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(patch.Classification)) {
            await UpsertClassificationAsync(entity.Id, patch.Classification, now, cancellationToken);
        }

        if (patch.Flags is not null) {
            await UpsertFlagsAsync(entity.Id, patch.Flags, now, cancellationToken);
        }

        if (images.Count > 0) {
            var image = images.FirstOrDefault(i => i.Kind is "still") ?? images.FirstOrDefault(i => i.Kind is "poster") ?? images[0];
            var role = image.Kind switch {
                "still" => EntityFileRole.Thumbnail,
                "poster" => EntityFileRole.Poster,
                _ => EntityFileRole.Thumbnail
            };
            await _artwork.DownloadPluginImageAsync(entity, image, role, now, cancellationToken);
        }

        entity.UpdatedAt = now;
    }
}
