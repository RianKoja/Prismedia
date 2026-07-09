using Prismedia.Application.Jobs.Handlers;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Jobs.Handlers.Identity;

/// <summary>
/// Probes a video file for embedded text subtitle streams and sidecar subtitle files sitting
/// beside it (same filename, e.g. "Movie.en.srt"), normalizes all of them to WebVTT, and
/// records them in the entity_subtitles table.
/// </summary>
public sealed class ExtractSubtitlesJobHandler(
    ILogger<ExtractSubtitlesJobHandler> logger,
    IMediaProbe mediaProbe,
    IMediaAssetGenerator assets,
    IMediaProcessingStatePersistence persistence) : EntityFileJobHandler(logger, persistence) {
    public override JobType Type => JobType.ExtractSubtitles;

    protected override Task OnSourceFileNotFoundAsync(Guid entityId, CancellationToken cancellationToken) =>
        Persistence.MarkSubtitlesExtractedAsync(entityId, cancellationToken);

    protected override async Task ExecuteAsync(
        JobContext context, Guid entityId, string filePath, CancellationToken cancellationToken) {
        await context.ReportProgressAsync(10, "Probing subtitle streams", cancellationToken);

        var streams = await mediaProbe.ProbeSubtitleStreamsAsync(filePath, cancellationToken);
        var embeddedCount = 0;
        if (streams.Count == 0) {
            logger.LogInformation("ExtractSubtitles: no embedded text subtitles in {Label}", context.Job.TargetLabel);
        } else {
            await context.ReportProgressAsync(30, $"Extracting {streams.Count} subtitle streams", cancellationToken);

            var outputDir = assets.SubtitleDir(entityId);
            var extractedPaths = await assets.ExtractSubtitlesAsync(filePath, outputDir, streams, cancellationToken);

            foreach (var path in extractedPaths) {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var parts = fileName.Split('-');
                var language = parts.Length >= 2 ? parts[1] : "und";
                var indexStr = parts.Length >= 3 ? parts[2] : "0";
                int.TryParse(indexStr, out var streamIndex);

                var matchingStream = streams.FirstOrDefault(s => s.StreamIndex.ToString() == indexStr);
                var label = matchingStream?.Title;

                await Persistence.UpsertSubtitleAsync(entityId, language, label, "vtt",
                    EntitySubtitleSource.Embedded, path, matchingStream?.CodecName ?? "unknown", streamIndex, cancellationToken);
            }

            embeddedCount = extractedPaths.Count;
        }

        await context.ReportProgressAsync(70, "Checking for sidecar subtitle files", cancellationToken);
        var sidecarCount = await ExtractSidecarSubtitlesAsync(entityId, filePath, cancellationToken);

        await Persistence.MarkSubtitlesExtractedAsync(entityId, cancellationToken);

        logger.LogInformation(
            "ExtractSubtitles: recorded {Embedded} embedded and {Sidecar} sidecar subtitle tracks for {Label}",
            embeddedCount, sidecarCount, context.Job.TargetLabel);

        await context.ReportProgressAsync(100, $"Extracted {embeddedCount + sidecarCount} subtitles", cancellationToken);
    }

    /// <summary>
    /// Registers subtitle files sitting beside the video ("Movie.srt", "Movie.en.srt",
    /// "Movie.pt-BR.srt", "Movie.ai_translated.srt"), copying every one — vtt included — through
    /// ffmpeg into the app-owned subtitle cache the same way embedded streams are.
    /// </summary>
    private async Task<int> ExtractSidecarSubtitlesAsync(
        Guid entityId, string filePath, CancellationToken cancellationToken) {
        var candidates = SubtitleSidecarFileMatcher.FindCandidates(filePath);
        if (candidates.Count == 0) {
            return 0;
        }

        var outputDir = assets.SubtitleDir(entityId);
        var registered = 0;
        foreach (var candidate in candidates) {
            // Every sidecar — vtt included — is copied through ffmpeg into the app-owned cache dir
            // rather than serving the library path directly: the discovery-time symlink check only
            // proves the file was safe at scan time, and a served StoragePath must stay safe even if
            // the library-folder entry is swapped for a symlink afterward. Named after the sidecar
            // file itself (not a loop index), so the output path is stable across rescans regardless
            // of filesystem enumeration order.
            var outputPath = Path.Combine(outputDir, $"sidecar-{Path.GetFileName(candidate.Path)}.vtt");
            if (!await assets.ConvertSubtitleFileAsync(candidate.Path, outputPath, cancellationToken)) {
                logger.LogWarning("ExtractSubtitles: failed to convert sidecar subtitle {Path}", candidate.Path);
                continue;
            }

            // UpsertSidecarSubtitleAsync deconflicts the language itself when two sidecars for this
            // video resolve to the same one (very common — every untagged file defaults to "und").
            await Persistence.UpsertSidecarSubtitleAsync(
                entityId, candidate.Language, candidate.Label, outputPath, candidate.Extension, candidate.Path,
                cancellationToken);
            registered++;
        }

        return registered;
    }
}
