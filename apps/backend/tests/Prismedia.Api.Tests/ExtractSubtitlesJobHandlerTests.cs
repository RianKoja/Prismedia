using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Application.Jobs;
using Prismedia.Application.Jobs.Handlers.Identity;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Tests;

/// <summary>
/// Covers <see cref="ExtractSubtitlesJobHandler"/>'s sidecar-subtitle discovery: files sitting
/// beside the video (e.g. "Movie.pt-BR.srt", "Movie.ai_translated.srt") get normalized to vtt
/// (except vtt sidecars, referenced directly) and recorded as Source=Sidecar subtitle tracks,
/// regardless of whether the video has any embedded subtitle streams.
/// </summary>
public sealed class ExtractSubtitlesJobHandlerTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"prismedia-extract-subtitles-{Guid.NewGuid():N}");
    private readonly Guid _entityId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public ExtractSubtitlesJobHandlerTests() {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SidecarFilesAreConvertedAndRecordedEvenWithoutEmbeddedSubtitles() {
        var videoPath = CreateFile("Movie.mkv");
        CreateFile("Movie.pt-BR.srt");
        CreateFile("Movie.ai_translated.srt");

        var assets = new RecordingSubtitleAssetGenerator(_tempDir);
        var persistence = new SubtitlePersistence(videoPath);
        var handler = new ExtractSubtitlesJobHandler(
            NullLogger<ExtractSubtitlesJobHandler>.Instance, new FakeMediaProbe([]), assets, persistence);

        await handler.HandleAsync(new JobContext(BuildJob(), new NoopJobQueue()), CancellationToken.None);

        Assert.Equal(2, persistence.SidecarUpserts.Count);
        Assert.Contains(persistence.SidecarUpserts, u => u.Language == "pt-BR" && u.Label is null
            && u.SourceFormat == "srt" && u.SourcePath.EndsWith("Movie.pt-BR.srt", StringComparison.Ordinal)
            && File.Exists(u.StoragePath));
        Assert.Contains(persistence.SidecarUpserts, u => u.Language == "und" && u.Label == "Ai Translated"
            && u.SourceFormat == "srt" && u.SourcePath.EndsWith("Movie.ai_translated.srt", StringComparison.Ordinal)
            && File.Exists(u.StoragePath));
        Assert.True(persistence.MarkedExtracted);
    }

    [Fact]
    public async Task VttSidecarsAreReferencedDirectlyWithoutConversion() {
        var videoPath = CreateFile("Movie.mkv");
        var vttPath = CreateFile("Movie.en.vtt");

        var assets = new RecordingSubtitleAssetGenerator(_tempDir);
        var persistence = new SubtitlePersistence(videoPath);
        var handler = new ExtractSubtitlesJobHandler(
            NullLogger<ExtractSubtitlesJobHandler>.Instance, new FakeMediaProbe([]), assets, persistence);

        await handler.HandleAsync(new JobContext(BuildJob(), new NoopJobQueue()), CancellationToken.None);

        var upsert = Assert.Single(persistence.SidecarUpserts);
        Assert.Equal("en", upsert.Language);
        Assert.Equal(vttPath, upsert.StoragePath);
        Assert.Empty(assets.ConvertedInputs);
    }

    [Fact]
    public async Task FailedConversionSkipsTheCandidateWithoutThrowing() {
        var videoPath = CreateFile("Movie.mkv");
        CreateFile("Movie.es.srt");

        var assets = new RecordingSubtitleAssetGenerator(_tempDir) { FailConversion = true };
        var persistence = new SubtitlePersistence(videoPath);
        var handler = new ExtractSubtitlesJobHandler(
            NullLogger<ExtractSubtitlesJobHandler>.Instance, new FakeMediaProbe([]), assets, persistence);

        await handler.HandleAsync(new JobContext(BuildJob(), new NoopJobQueue()), CancellationToken.None);

        Assert.Empty(persistence.SidecarUpserts);
        Assert.True(persistence.MarkedExtracted);
    }

    [Fact]
    public async Task SidecarsThatResolveToTheSameLanguageAreBothSubmittedForPersistence() {
        // Language deconfliction (the entity_subtitles unique index is on (EntityId, Language,
        // Source)) is UpsertSidecarSubtitleAsync's job — see LibraryScanPersistenceServiceTests —
        // the handler just passes each candidate's resolved language through unchanged.
        var videoPath = CreateFile("Movie.mkv");
        CreateFile("Movie.srt");
        CreateFile("Movie.forced.srt");

        var assets = new RecordingSubtitleAssetGenerator(_tempDir);
        var persistence = new SubtitlePersistence(videoPath);
        var handler = new ExtractSubtitlesJobHandler(
            NullLogger<ExtractSubtitlesJobHandler>.Instance, new FakeMediaProbe([]), assets, persistence);

        await handler.HandleAsync(new JobContext(BuildJob(), new NoopJobQueue()), CancellationToken.None);

        Assert.Equal(2, persistence.SidecarUpserts.Count);
        Assert.Contains(persistence.SidecarUpserts, u => u.Language == "und" && u.Label == "Forced");
        Assert.Contains(persistence.SidecarUpserts, u => u.Language == "und" && u.Label is null);
    }

    [Fact]
    public async Task NoEmbeddedOrSidecarSubtitlesStillMarksExtraction() {
        var videoPath = CreateFile("Movie.mkv");

        var assets = new RecordingSubtitleAssetGenerator(_tempDir);
        var persistence = new SubtitlePersistence(videoPath);
        var handler = new ExtractSubtitlesJobHandler(
            NullLogger<ExtractSubtitlesJobHandler>.Instance, new FakeMediaProbe([]), assets, persistence);

        await handler.HandleAsync(new JobContext(BuildJob(), new NoopJobQueue()), CancellationToken.None);

        Assert.Empty(persistence.SidecarUpserts);
        Assert.True(persistence.MarkedExtracted);
    }

    private string CreateFile(string name) {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "content");
        return path;
    }

    private JobRunSnapshot BuildJob() => new(
        Guid.NewGuid(),
        JobType.ExtractSubtitles,
        JobRunStatus.Running,
        Progress: 0,
        Message: null,
        PayloadJson: "{}",
        TargetEntityKind: "video",
        TargetEntityId: _entityId.ToString(),
        TargetLabel: "Movie",
        CreatedAt: DateTimeOffset.UtcNow,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: null);

    private sealed class FakeMediaProbe(IReadOnlyList<SubtitleStreamData> streams) : IMediaProbe {
        public Task<VideoProbeData?> ProbeVideoAsync(string filePath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AudioProbeData?> ProbeAudioAsync(string filePath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImageProbeData?> ProbeImageAsync(string filePath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<SubtitleStreamData>> ProbeSubtitleStreamsAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(streams);
    }

    private sealed class RecordingSubtitleAssetGenerator(string tempDir) : IMediaAssetGenerator {
        public bool FailConversion { get; init; }
        public List<string> ConvertedInputs { get; } = [];

        public Task<bool> ConvertSubtitleFileAsync(string inputPath, string outputPath, CancellationToken cancellationToken) {
            ConvertedInputs.Add(inputPath);
            if (FailConversion) {
                return Task.FromResult(false);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "WEBVTT");
            return Task.FromResult(true);
        }

        public string SubtitleDir(Guid entityId) => Path.Combine(tempDir, "subtitles");

        public Task<IReadOnlyList<string>> ExtractSubtitlesAsync(string inputPath, string outputDir, IReadOnlyList<SubtitleStreamData> streams, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> GenerateVideoThumbnailAsync(string inputPath, string outputPath, double seekSeconds, int width, int height, int quality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> GeneratePreviewClipAsync(string inputPath, string outputPath, double startSeconds, int durationSeconds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> ExtractTrickplayFrameAsync(string inputPath, string outputPath, double seekSeconds, int width, int height, int jpegQuality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> ExtractTrickplayFramesBatchAsync(string inputPath, string outputDir, double duration, int intervalSeconds, int width, int height, int jpegQuality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> ComposeSpriteSheetAsync(string frameDir, string outputPath, int columns, int frameWidth, int frameHeight, int jpegQuality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> ComposeTiledJpegSheetsAsync(string frameDir, string outputDir, int columns, int rows, int frameWidth, int frameHeight, int jpegQuality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<(bool Thumbnail, bool Preview)> GenerateThumbnailAndPreviewAsync(string inputPath, string thumbnailPath, double thumbSeekSeconds, int thumbWidth, int thumbHeight, int thumbQuality, string previewPath, double previewStartSeconds, int previewDurationSeconds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> GenerateImageThumbnailAsync(string inputPath, string outputPath, int targetWidth, int quality, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int[]?> GenerateWaveformDataAsync(string inputPath, double durationSeconds, int pixelsPerSecond, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public string VideoThumbnailPath(Guid entityId) => throw new NotSupportedException();
        public string VideoPreviewPath(Guid entityId) => throw new NotSupportedException();
        public string VideoSpritePath(Guid entityId) => throw new NotSupportedException();
        public string VideoTrickplayVttPath(Guid entityId) => throw new NotSupportedException();
        public string TrickplayFrameDir(Guid entityId) => throw new NotSupportedException();
        public string TrickplayTileDir(Guid entityId, int width) => throw new NotSupportedException();
        public string ImageThumbnailPath(Guid entityId) => throw new NotSupportedException();
        public string ImagePreviewPath(Guid entityId) => throw new NotSupportedException();
        public string BookPageThumbnailPath(Guid entityId) => throw new NotSupportedException();
        public string BookCoverThumbnailPath(Guid entityId) => throw new NotSupportedException();
        public string AudioWaveformPath(Guid entityId) => throw new NotSupportedException();
        public string VideoThumbnailUrl(Guid entityId) => throw new NotSupportedException();
        public string VideoPreviewUrl(Guid entityId) => throw new NotSupportedException();
        public string VideoTrickplayVttUrl(Guid entityId) => throw new NotSupportedException();
        public string TrickplayPlaylistUrl(Guid entityId, int width) => throw new NotSupportedException();
        public string ImageThumbnailUrl(Guid entityId) => throw new NotSupportedException();
        public string ImagePreviewUrl(Guid entityId) => throw new NotSupportedException();
        public string BookPageThumbnailUrl(Guid entityId) => throw new NotSupportedException();
        public string BookCoverThumbnailUrl(Guid entityId) => throw new NotSupportedException();
        public string AudioWaveformUrl(Guid entityId) => throw new NotSupportedException();
        public string SubtitleUrl(Guid entityId, string fileName) => throw new NotSupportedException();
    }

    private sealed class SubtitlePersistence(string sourcePath) : IMediaProcessingStatePersistence {
        public bool MarkedExtracted { get; private set; }
        public List<(string Language, string? Label, string StoragePath, string SourceFormat, string SourcePath)> SidecarUpserts { get; } = [];

        public Task<string?> GetSourceFilePathAsync(Guid entityId, CancellationToken cancellationToken) => Task.FromResult<string?>(sourcePath);

        public Task MarkSubtitlesExtractedAsync(Guid entityId, CancellationToken cancellationToken) {
            MarkedExtracted = true;
            return Task.CompletedTask;
        }

        public Task UpsertSubtitleAsync(Guid entityId, string language, string? label, string format, EntitySubtitleSource source, string storagePath, string sourceFormat, int streamIndex, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpsertSidecarSubtitleAsync(Guid entityId, string language, string? label, string storagePath, string sourceFormat, string sourcePath, CancellationToken cancellationToken) {
            SidecarUpserts.Add((language, label, storagePath, sourceFormat, sourcePath));
            return Task.CompletedTask;
        }

        public Task UpsertEntityTechnicalAsync(Guid entityId, double? duration, int? width, int? height, double? frameRate, int? bitRate, int? sampleRate, int? channels, string? codec, string? container, string? format, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpsertMediaSourceAsync(Guid entityId, string path, MediaSourceProbeData source, IReadOnlyList<MediaStreamProbeData> streams, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpsertTrickplayInfoAsync(Guid entityId, TrickplayInfoData info, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpsertEntityFileAsync(Guid entityId, EntityFileRole role, string path, string? mimeType, long? sizeBytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpsertEntityFingerprintAsync(Guid entityId, FingerprintAlgorithm algorithm, string value, Guid? entityFileId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Guid?> GetSourceFileIdAsync(Guid entityId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAudioTrackTagsAsync(Guid entityId, string? artist, string? album, int? trackNumber, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<EntityTechnicalData?> GetEntityTechnicalAsync(Guid entityId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task MarkEntityProbeFailedAsync(Guid entityId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ClearProbeFailuresForPathsAsync(IReadOnlyCollection<string> sourcePaths, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopJobQueue : IJobQueueService {
        public Task<IReadOnlyList<JobRunSnapshot>> ListAsync(bool hideNsfw, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JobRunSnapshot>>([]);
        public Task<JobRunSnapshot> EnqueueAsync(JobType type, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<JobRunSnapshot> EnqueueAsync(EnqueueJobRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasPendingAsync(JobType type, string? targetEntityId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<int> EnqueueBatchAsync(IReadOnlyList<EnqueueJobRequest> requests, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> CancelAsync(JobType? type, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<bool> CancelRunAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<int> ClearFailuresAsync(JobType? type, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<JobRunSnapshot?> ClaimNextAsync(string workerId, CancellationToken cancellationToken, JobRunLane? lane = null) => Task.FromResult<JobRunSnapshot?>(null);
        public Task<int> RecoverStaleRunningAsync(string currentWorkerId, TimeSpan staleAfter, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task UpdateProgressAsync(Guid id, int progress, string? message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync(Guid id, string? message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task FailAsync(Guid id, string message, TimeSpan retryDelay, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<JobQueueCount>> GetQueueCountsAsync(bool hideNsfw, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JobQueueCount>>([]);
        public Task<int> PruneHistoryAsync(TimeSpan retention, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
