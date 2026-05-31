using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Videos;
using Prismedia.Contracts.Media;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Videos;

/// <summary>
/// EF-backed subtitle asset resolver for video subtitle tracks.
/// </summary>
public sealed class VideoSubtitleAssetService : IVideoSubtitleAssetService {
    private readonly PrismediaDbContext _db;
    private readonly ProcessExecutor _processExecutor;
    private readonly MediaToolOptions _mediaTools;

    /// <summary>
    /// Creates a subtitle asset resolver over the database context.
    /// </summary>
    /// <param name="db">Database context used to find subtitle rows.</param>
    /// <param name="processExecutor">Process runner used to extract embedded styled subtitles on demand.</param>
    /// <param name="mediaTools">Configured ffmpeg and ffprobe executable paths.</param>
    public VideoSubtitleAssetService(
        PrismediaDbContext db,
        ProcessExecutor processExecutor,
        MediaToolOptions mediaTools) {
        _db = db;
        _processExecutor = processExecutor;
        _mediaTools = mediaTools;
    }

    /// <summary>
    /// Finds the normalized WebVTT subtitle file for one subtitle track.
    /// </summary>
    /// <param name="videoId">Video entity identifier that owns the track.</param>
    /// <param name="trackId">Subtitle track identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>Subtitle asset metadata, or null when the track or file is unavailable.</returns>
    public async Task<VideoSubtitleAsset?> GetSubtitleAsync(
        Guid videoId,
        Guid trackId,
        CancellationToken cancellationToken) {
        var path = await _db.EntitySubtitles
            .AsNoTracking()
            .Where(row => row.EntityId == videoId && row.Id == trackId)
            .Select(row => row.StoragePath)
            .SingleOrDefaultAsync(cancellationToken);

        return ExistingAsset(path, MediaContentTypes.VttUtf8);
    }

    /// <summary>
    /// Finds the original ASS/SSA subtitle source for one subtitle track, when preserved.
    /// </summary>
    /// <param name="videoId">Video entity identifier that owns the track.</param>
    /// <param name="trackId">Subtitle track identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>Subtitle source metadata, or null when no raw source is available.</returns>
    public async Task<VideoSubtitleAsset?> GetSubtitleSourceAsync(
        Guid videoId,
        Guid trackId,
        CancellationToken cancellationToken) {
        var row = await _db.EntitySubtitles
            .AsNoTracking()
            .Where(subtitle => subtitle.EntityId == videoId && subtitle.Id == trackId)
            .Select(subtitle => new {
                subtitle.StoragePath,
                subtitle.SourcePath,
                subtitle.SourceFormat,
                subtitle.Source
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null ||
            string.IsNullOrWhiteSpace(row.SourcePath) ||
            !IsStyledSubtitleFormat(row.SourceFormat)) {
            return null;
        }

        var preservedAsset = ExistingAsset(row.SourcePath, MediaContentTypes.SsaUtf8);
        if (preservedAsset is not null) {
            return preservedAsset;
        }

        if (row.Source != EntitySubtitleSource.Embedded ||
            !int.TryParse(row.SourcePath, out var streamIndex)) {
            return null;
        }

        var sourcePath = await _db.EntityFiles
            .AsNoTracking()
            .Where(file => file.EntityId == videoId && file.Role == EntityFileRole.Source)
            .Select(file => file.Path)
            .SingleOrDefaultAsync(cancellationToken);

        if (!ExistingSource(sourcePath)) {
            return null;
        }

        var outputPath = EmbeddedSubtitleSourcePath(row.StoragePath, row.SourceFormat);
        if (File.Exists(outputPath)) {
            return new VideoSubtitleAsset(outputPath, MediaContentTypes.SsaUtf8);
        }

        var extracted = await ExtractEmbeddedStyledSubtitleAsync(
            sourcePath!,
            streamIndex,
            outputPath,
            cancellationToken);

        return extracted ? new VideoSubtitleAsset(outputPath, MediaContentTypes.SsaUtf8) : null;
    }

    private static VideoSubtitleAsset? ExistingAsset(string? path, string contentType) {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path)) {
            return null;
        }

        return new VideoSubtitleAsset(path, contentType);
    }

    private async Task<bool> ExtractEmbeddedStyledSubtitleAsync(
        string sourcePath,
        int streamIndex,
        string outputPath,
        CancellationToken cancellationToken) {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var result = await _processExecutor.RunAsync(
            _mediaTools.FfmpegPath,
            [
                "-y",
                "-v", "error",
                "-i", sourcePath,
                "-map", $"0:{streamIndex}",
                "-c:s", "copy",
                outputPath
            ],
            null,
            cancellationToken,
            lowPriority: true);

        return result.ExitCode == 0 && File.Exists(outputPath);
    }

    private static string EmbeddedSubtitleSourcePath(string storagePath, string sourceFormat) {
        var extension = string.Equals(sourceFormat, "ssa", StringComparison.OrdinalIgnoreCase)
            ? ".ssa"
            : ".ass";
        return Path.ChangeExtension(storagePath, extension);
    }

    private static bool ExistingSource(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Path.IsPathRooted(path) &&
        File.Exists(path);

    private static bool IsStyledSubtitleFormat(string? format) =>
        string.Equals(format, "ass", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, "ssa", StringComparison.OrdinalIgnoreCase);
}
