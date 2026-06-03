using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Videos;
using Prismedia.Contracts.Media;

namespace Prismedia.Infrastructure.Videos;

/// <summary>
/// Stream-copy (remux) HLS path for <see cref="HlsAssetService"/>. When a client can decode the
/// source video codec but not its container (for example a browser that hardware-decodes HEVC but
/// cannot demux MKV), the video is copied — not re-encoded — into an fMP4 HLS stream while the audio
/// is transcoded to AAC. Copying is near free (tens of seconds for a whole movie versus a slow,
/// CPU/GPU-bound transcode), so the client hardware-decodes the original stream and playback is
/// smooth with negligible server load, matching how other media servers serve HEVC to browsers.
/// </summary>
public sealed partial class HlsAssetService {
    // One whole-file remux generation per (item, audio track); the ffmpeg job runs to completion in
    // the background and the served files (init.mp4, seg_*.m4s, index.m3u8) appear as it progresses.
    private static readonly ConcurrentDictionary<string, RemuxGeneration> RemuxGenerations = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RemuxStartLocks = new();

    // A tracked stream-copy job: the running task, its kill switch, the owning entity, and when it
    // started — enough for the reaper to cancel it when the viewer leaves or it overruns its lifetime.
    private sealed record RemuxGeneration(
        Task Task,
        CancellationTokenSource Cancellation,
        Guid EntityId,
        DateTimeOffset StartedAtUtc);

    private async Task<HlsAsset?> TryGetRemuxAssetAsync(
        Guid id,
        VideoSourceFile source,
        string audioCacheKey,
        int? audioStreamIndex,
        string assetName,
        CancellationToken cancellationToken) {
        if (_processes is null) {
            return null;
        }

        var fileName = assetName switch {
            "stream.m3u8" or "index.m3u8" => "index.m3u8",
            "init.mp4" => "init.mp4",
            _ when assetName.StartsWith("seg_", StringComparison.OrdinalIgnoreCase) &&
                assetName.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase) => assetName,
            _ => null,
        };
        if (fileName is null) {
            return null;
        }

        var remuxDir = VirtualPath(id, "remux", audioCacheKey);
        await EnsureRemuxStartedAsync(id, source, audioCacheKey, audioStreamIndex, remuxDir, cancellationToken);

        var filePath = Path.Combine(remuxDir, fileName);
        if (!await WaitForRemuxFileAsync(id, audioCacheKey, filePath, cancellationToken)) {
            return null;
        }

        var isPlaylist = fileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        return new HlsAsset(
            filePath,
            isPlaylist ? MediaContentTypes.HlsPlaylist : MediaContentTypes.VideoMp4,
            // The playlist grows while the remux runs, so it must not be cached; the immutable
            // init/segment files can be cached aggressively.
            isPlaylist ? "no-cache" : "public, max-age=31536000, immutable");
    }

    private async Task EnsureRemuxStartedAsync(
        Guid id,
        VideoSourceFile source,
        string audioCacheKey,
        int? audioStreamIndex,
        string remuxDir,
        CancellationToken cancellationToken) {
        var key = $"{id}/{audioCacheKey}";
        if (RemuxGenerations.ContainsKey(key)) {
            return;
        }

        var startLock = RemuxStartLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await startLock.WaitAsync(cancellationToken);
        try {
            if (RemuxGenerations.ContainsKey(key)) {
                return;
            }

            var indexPath = Path.Combine(remuxDir, "index.m3u8");
            if (File.Exists(indexPath) &&
                (await File.ReadAllTextAsync(indexPath, cancellationToken)).Contains("#EXT-X-ENDLIST", StringComparison.Ordinal)) {
                // A previous run completed this remux; reuse the cached fMP4 HLS as-is.
                RemuxGenerations[key] = new RemuxGeneration(Task.CompletedTask, new CancellationTokenSource(), id, DateTimeOffset.UtcNow);
                return;
            }

            var cancellation = new CancellationTokenSource();
            RemuxGenerations[key] = new RemuxGeneration(
                GenerateRemuxAsync(id, source, audioStreamIndex, remuxDir, key, cancellation.Token),
                cancellation,
                id,
                DateTimeOffset.UtcNow);
        } finally {
            startLock.Release();
        }
    }

    private async Task GenerateRemuxAsync(
        Guid id,
        VideoSourceFile source,
        int? audioStreamIndex,
        string remuxDir,
        string key,
        CancellationToken cancellationToken) {
        try {
            if (Directory.Exists(remuxDir)) {
                Directory.Delete(remuxDir, recursive: true);
            }

            Directory.CreateDirectory(remuxDir);
            var options = await ResolveTranscoderOptionsAsync(cancellationToken);
            var arguments = RemuxArguments(source, audioStreamIndex, remuxDir);
            var result = await _processes!.RunAsync(options.FfmpegPath, arguments, environment: null, cancellationToken);
            if (result.ExitCode != 0) {
                _logger?.LogWarning(
                    "Remux generation failed for {VideoId}: {Error}",
                    id,
                    result.StandardError);
                // Let the next request retry from scratch rather than serving a half-written remux.
                RemuxGenerations.TryRemove(key, out _);
            }
        } catch (OperationCanceledException) {
            // The reaper or an explicit stop cancelled this copy; drop the entry so a later request
            // regenerates from scratch rather than waiting on a partial, abandoned remux.
            RemuxGenerations.TryRemove(key, out _);
        } catch (Exception ex) {
            _logger?.LogWarning(ex, "Remux generation errored for {VideoId}.", id);
            RemuxGenerations.TryRemove(key, out _);
        }
    }

    private IReadOnlyList<string> RemuxArguments(VideoSourceFile source, int? audioStreamIndex, string remuxDir) {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-y",
            "-loglevel",
            "error",
            "-nostats",
            "-i",
            source.Path,
            "-map",
            "0:v:0",
            "-map",
            audioStreamIndex is null ? "0:a:0?" : $"0:{audioStreamIndex.Value}?",
            "-map_metadata",
            "-1",
            "-map_chapters",
            "-1",
            "-c:v",
            "copy",
        };

        // Browsers (especially Safari/WebKit) require the hvc1 sample-entry tag for HEVC in fMP4; the
        // hev1 tag the source may carry does not play. Copying does not change the bitstream, only the tag.
        if (IsHevcCodec(source.VideoCodec)) {
            arguments.AddRange(["-tag:v", "hvc1"]);
        }

        arguments.AddRange(
        [
            "-c:a",
            "aac",
            "-ac",
            "2",
            "-b:a",
            "192k",
            "-ar",
            "48000",
            "-f",
            "hls",
            "-hls_time",
            SegmentDurationSeconds.ToString(),
            "-hls_segment_type",
            "fmp4",
            "-hls_fmp4_init_filename",
            "init.mp4",
            "-hls_playlist_type",
            "event",
            "-hls_flags",
            "independent_segments+temp_file",
            "-hls_list_size",
            "0",
            "-hls_segment_filename",
            Path.Combine(remuxDir, "seg_%05d.m4s"),
            Path.Combine(remuxDir, "index.m3u8"),
        ]);

        return arguments;
    }

    private async Task<bool> WaitForRemuxFileAsync(
        Guid id,
        string audioCacheKey,
        string filePath,
        CancellationToken cancellationToken) {
        var key = $"{id}/{audioCacheKey}";
        while (true) {
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0) {
                return true;
            }

            if (RemuxGenerations.TryGetValue(key, out var generation) && generation.Task.IsCompleted) {
                // Generation finished (or failed); the file will not appear if it is not there now.
                return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            }

            await Task.Delay(SegmentPollInterval, cancellationToken);
        }
    }

    private static bool IsHevcCodec(string? codec) =>
        codec is not null &&
        (codec.Equals("hevc", StringComparison.OrdinalIgnoreCase) ||
            codec.Equals("h265", StringComparison.OrdinalIgnoreCase));
}
