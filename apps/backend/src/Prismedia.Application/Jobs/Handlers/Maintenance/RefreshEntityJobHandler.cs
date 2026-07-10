using Microsoft.Extensions.Logging;
using Prismedia.Application.Files;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Jobs.Handlers.Maintenance;

/// <summary>
/// Re-runs the processing pipeline (probe, fingerprint, preview, subtitles) for a single entity
/// and all of its structural children. Designed for "rescan this item" actions from detail pages.
/// </summary>
public sealed class RefreshEntityJobHandler(
    ILogger<RefreshEntityJobHandler> logger,
    IEntityRefreshTreePersistence refreshTree,
    ILibraryScanRootPersistence scanRoots,
    IDownstreamNeedsPersistence downstreamNeeds,
    IMaintenancePersistence maintenance,
    ISubtitleSidecarDiscovery subtitleSidecars,
    IVideoScanPersistence videos) : IJobHandler {

    public JobType Type => JobType.RefreshEntity;

    public async Task HandleAsync(JobContext context, CancellationToken cancellationToken) {
        if (!Guid.TryParse(context.Job.TargetEntityId, out var entityId)) {
            logger.LogWarning("RefreshEntity: missing or invalid TargetEntityId");
            return;
        }

        var tree = await refreshTree.GetEntityTreeAsync(entityId, cancellationToken);
        if (tree.Count == 0) {
            logger.LogWarning("RefreshEntity: entity {EntityId} not found", entityId);
            return;
        }

        await context.ReportProgressAsync(10, $"Found {tree.Count} entities to refresh", cancellationToken);

        await InvalidateChangedSubtitleSidecarsAsync(tree, cancellationToken);

        var settings = await scanRoots.GetSettingsAsync(cancellationToken);
        var ids = tree.Select(e => e.Id).ToList();
        foreach (var entity in tree) {
            if (EntityKindRegistry.TryGet(entity.KindCode, out var kind)) {
                await maintenance.ClearGeneratedPreviewAssetsAsync(kind, entity.Id, cancellationToken);
            }
        }

        var needs = await downstreamNeeds.CheckDownstreamNeedsBatchAsync(ids, cancellationToken);

        var jobRequests = new List<EnqueueJobRequest>();
        foreach (var entity in tree) {
            if (!needs.TryGetValue(entity.Id, out var entityNeeds) ||
                !EntityKindRegistry.TryGet(entity.KindCode, out var kind)) {
                continue;
            }

            var idStr = entity.Id.ToString();

            switch (kind) {
                case EntityKind.Video:
                    if (settings.AutoGenerateMetadata && entityNeeds.NeedsProbe)
                        jobRequests.Add(new EnqueueJobRequest(JobType.ProbeVideo, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Probe));
                    if (FingerprintGating.ShouldFingerprint(settings, entityNeeds))
                        jobRequests.Add(new EnqueueJobRequest(JobType.FingerprintVideo, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Fingerprint));
                    // An explicit refresh re-runs subtitle reconciliation even when the last
                    // successful signature is unchanged. That also refreshes embedded streams and
                    // repairs a missing app-owned asset without waiting for a library scan delta.
                    if (entityNeeds.NeedsSubtitleExtraction || !string.IsNullOrWhiteSpace(entity.SourcePath))
                        jobRequests.Add(new EnqueueJobRequest(JobType.ExtractSubtitles, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Sidecar));
                    if ((settings.AutoGeneratePreview && entityNeeds.NeedsPreview) || (settings.GenerateTrickplay && entityNeeds.NeedsTrickplay))
                        jobRequests.Add(new EnqueueJobRequest(JobType.GeneratePreview, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Preview));
                    break;
                case EntityKind.Image:
                    if (FingerprintGating.ShouldFingerprint(settings, entityNeeds))
                        jobRequests.Add(new EnqueueJobRequest(JobType.FingerprintImage, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Fingerprint));
                    if (entityNeeds.NeedsPreview)
                        jobRequests.Add(new EnqueueJobRequest(JobType.GenerateImageThumbnail, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Thumbnail));
                    break;
                case EntityKind.AudioTrack:
                    if (entityNeeds.NeedsProbe)
                        jobRequests.Add(new EnqueueJobRequest(JobType.ProbeAudio, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Probe));
                    if (FingerprintGating.ShouldFingerprint(settings, entityNeeds))
                        jobRequests.Add(new EnqueueJobRequest(JobType.FingerprintAudio, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Fingerprint));
                    if (entityNeeds.NeedsPreview)
                        jobRequests.Add(new EnqueueJobRequest(JobType.GenerateAudioWaveform, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Waveform));
                    break;
                case EntityKind.BookPage:
                    if (entityNeeds.NeedsPreview)
                        jobRequests.Add(new EnqueueJobRequest(JobType.GenerateBookPageThumbnail, TargetEntityKind: entity.KindCode, TargetEntityId: idStr, TargetLabel: entity.Title, Priority: JobPriorities.Thumbnail));
                    break;
            }
        }

        if (jobRequests.Count > 0) {
            var enqueued = await context.EnqueueBatchAsync(jobRequests, cancellationToken);
            logger.LogInformation("RefreshEntity: queued {Enqueued}/{Total} downstream jobs for {Label}",
                enqueued, jobRequests.Count, tree[0].Title);
        } else {
            logger.LogInformation("RefreshEntity: no downstream work needed for {Label}", tree[0].Title);
        }

        await context.ReportProgressAsync(100, $"Queued {jobRequests.Count} jobs", cancellationToken);
    }

    private async Task InvalidateChangedSubtitleSidecarsAsync(
        IReadOnlyList<EntityRefreshTarget> tree,
        CancellationToken cancellationToken) {
        var targets = tree
            .Where(entity =>
                string.Equals(entity.KindCode, EntityKindRegistry.Video.Code, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entity.SourcePath))
            .ToArray();
        if (targets.Length == 0) {
            return;
        }

        var sourcePaths = targets
            .Select(target => target.SourcePath!)
            .Distinct(FileSystemPathComparison.Comparer)
            .ToArray();
        var discoveries = await subtitleSidecars.DiscoverAsync(sourcePaths, cancellationToken);
        var discoveryByPath = discoveries
            .GroupBy(discovery => discovery.VideoPath, FileSystemPathComparison.Comparer)
            .ToDictionary(group => group.Key, group => group.Last(), FileSystemPathComparison.Comparer);
        var states = new List<VideoSubtitleSidecarState>(targets.Length);
        foreach (var target in targets) {
            if (!discoveryByPath.TryGetValue(target.SourcePath!, out var discovery) || !discovery.IsComplete) {
                throw new IOException(
                    "Adjacent subtitle discovery was incomplete; the entity refresh was not started.");
            }

            states.Add(new VideoSubtitleSidecarState(target.Id, discovery.Signature));
        }

        await videos.InvalidateSubtitleStateAsync(states, cancellationToken);
    }
}
