# Folder-Backed Gallery And Audio Scan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make image and audio scans treat root-level files as loose media while turning folders below the library root into nested gallery/audio-library containers.

**Architecture:** The scan handlers will classify directory groups relative to the library root, skip root container creation, and materialize child folders before files. The EF scan persistence adapter will accept optional container parents, relink existing rows, and add root-level stale cleanup for loose images and tracks. Existing detail API and Svelte pages already render child groups, so no UI contract change is required.

**Tech Stack:** C#/.NET 10, xUnit, EF Core InMemory provider, Prismedia scan application ports, `PrismediaDbContext`.

---

## File Structure

- Modify `apps/backend/src/Prismedia.Application/Jobs/Ports/ILibraryScanPersistence.cs`
  - Add optional parent arguments to gallery/audio-library/audio-track upsert methods.
  - Add cleanup methods for loose root-level images and audio tracks.
- Modify `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanGalleryJobHandler.cs`
  - Classify root-level images as loose.
  - Materialize folder-backed galleries in parent-before-child order.
  - Preserve folder hierarchy through parent gallery ids and sort order.
- Modify `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanAudioJobHandler.cs`
  - Classify root-level tracks as loose.
  - Materialize folder-backed audio libraries in parent-before-child order.
  - Preserve folder hierarchy through parent audio-library ids and sort order.
- Modify `apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.cs`
  - Persist optional structural parents for gallery/audio-library upserts.
  - Allow image/audio-track relinking to and from loose root-level state.
  - Implement loose root-level stale cleanup.
- Modify `apps/backend/tests/Prismedia.Api.Tests/ScanJobHandlerTests.cs`
  - Add handler-level tests for gallery and audio directory classification.
  - Update `FakeScanPersistence` for the new port signatures.
- Modify `apps/backend/tests/Prismedia.Infrastructure.Tests/LibraryScanPersistenceServiceTests.cs`
  - Add persistence tests for parent-aware upserts, loose relinking, and root-level stale cleanup.
- Modify `CHANGELOG.md`
  - Add a user-facing `Changed` entry under `## [Unreleased]`.

## Task 1: Update The Scan Persistence Port

**Files:**
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Ports/ILibraryScanPersistence.cs`

- [ ] **Step 1: Change the port signatures**

Replace the existing gallery/audio signatures:

```csharp
Task<Guid> UpsertImageAsync(string filePath, string title, Guid? galleryEntityId, long? sizeBytes, int sortOrder, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertGalleryAsync(string folderPath, string title, Guid libraryRootId, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertAudioTrackAsync(string filePath, string title, Guid audioLibraryId, int sortOrder, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertAudioLibraryAsync(string folderPath, string title, Guid libraryRootId, bool isNsfw, CancellationToken cancellationToken);
```

with:

```csharp
Task<Guid> UpsertImageAsync(string filePath, string title, Guid? galleryEntityId, long? sizeBytes, int sortOrder, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertGalleryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentGalleryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertAudioTrackAsync(string filePath, string title, Guid? audioLibraryId, int sortOrder, bool isNsfw, CancellationToken cancellationToken);
Task<Guid> UpsertAudioLibraryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentLibraryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken);
```

Add cleanup methods after `RemoveStaleGalleriesInRootAsync` and `RemoveStaleAudioLibrariesInRootAsync`:

```csharp
Task<int> RemoveStaleLooseImagesInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken);
Task<int> RemoveStaleLooseAudioTracksInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken);
```

- [ ] **Step 2: Build to verify expected compile failures**

Run:

```bash
dotnet build apps/backend/Prismedia.slnx
```

Expected: FAIL because the implementation and test doubles still use the old signatures.

- [ ] **Step 3: Commit only after Task 2 and Task 3 make the build green**

Do not commit this task by itself because it intentionally breaks compile.

## Task 2: Add Failing Handler Tests

**Files:**
- Modify: `apps/backend/tests/Prismedia.Api.Tests/ScanJobHandlerTests.cs`

- [ ] **Step 1: Extend `RecordingFileDiscovery` to support directory groups**

Replace the helper constructor and methods with this shape:

```csharp
private sealed class RecordingFileDiscovery(
    IReadOnlyList<string>? files = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? directoryGroups = null) : IFileDiscovery {
    public Task<IReadOnlyList<string>> DiscoverFilesAsync(
        string rootPath, MediaCategory category, bool recursive, CancellationToken cancellationToken) =>
        Task.FromResult(files ?? []);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> DiscoverFilesByDirectoryAsync(
        string rootPath, MediaCategory category, bool recursive, CancellationToken cancellationToken) =>
        Task.FromResult(directoryGroups ?? new Dictionary<string, IReadOnlyList<string>>());
}
```

Update existing calls from `new RecordingFileDiscovery([...])` to `new RecordingFileDiscovery(files: [...])`.

- [ ] **Step 2: Update `FakeScanPersistence` records and port methods**

Add tracking properties:

```csharp
public List<ImageRecord> UpsertedImages { get; } = [];
public List<GalleryRecord> UpsertedGalleries { get; } = [];
public List<AudioTrackRecord> UpsertedAudioTracks { get; } = [];
public List<AudioLibraryRecord> UpsertedAudioLibraries { get; } = [];
public IReadOnlyList<string> ValidLooseImagePaths { get; private set; } = [];
public IReadOnlyList<string> ValidGalleryPaths { get; private set; } = [];
public IReadOnlyList<string> ValidLooseAudioTrackPaths { get; private set; } = [];
public IReadOnlyList<string> ValidAudioLibraryPaths { get; private set; } = [];
```

Replace the image/audio fake methods with:

```csharp
public Task<Guid> UpsertImageAsync(string filePath, string title, Guid? galleryEntityId, long? sizeBytes, int sortOrder, bool isNsfw, CancellationToken cancellationToken) {
    var id = IdFor($"image:{filePath}");
    UpsertedImages.Add(new ImageRecord(id, filePath, title, galleryEntityId, sortOrder));
    return Task.FromResult(id);
}

public Task<Guid> UpsertGalleryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentGalleryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken) {
    var id = IdFor($"gallery:{folderPath}");
    UpsertedGalleries.Add(new GalleryRecord(id, folderPath, title, libraryRootId, parentGalleryId, sortOrder));
    return Task.FromResult(id);
}

public Task<Guid> UpsertAudioTrackAsync(string filePath, string title, Guid? audioLibraryId, int sortOrder, bool isNsfw, CancellationToken cancellationToken) {
    var id = IdFor($"audio-track:{filePath}");
    UpsertedAudioTracks.Add(new AudioTrackRecord(id, filePath, title, audioLibraryId, sortOrder));
    return Task.FromResult(id);
}

public Task<Guid> UpsertAudioLibraryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentLibraryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken) {
    var id = IdFor($"audio-library:{folderPath}");
    UpsertedAudioLibraries.Add(new AudioLibraryRecord(id, folderPath, title, libraryRootId, parentLibraryId, sortOrder));
    return Task.FromResult(id);
}

public Task<int> RemoveStaleLooseImagesInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken) {
    ValidLooseImagePaths = validPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    return Task.FromResult(0);
}

public Task<int> RemoveStaleGalleriesInRootAsync(Guid rootId, IReadOnlySet<string> validFolderPaths, CancellationToken cancellationToken) {
    ValidGalleryPaths = validFolderPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    return Task.FromResult(0);
}

public Task<int> RemoveStaleLooseAudioTracksInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken) {
    ValidLooseAudioTrackPaths = validPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    return Task.FromResult(0);
}

public Task<int> RemoveStaleAudioLibrariesInRootAsync(Guid rootId, IReadOnlySet<string> validFolderPaths, CancellationToken cancellationToken) {
    ValidAudioLibraryPaths = validFolderPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    return Task.FromResult(0);
}
```

Add record declarations near the existing book records:

```csharp
private sealed record ImageRecord(Guid Id, string SourcePath, string Title, Guid? GalleryEntityId, int SortOrder);
private sealed record GalleryRecord(Guid Id, string SourcePath, string Title, Guid LibraryRootId, Guid? ParentGalleryId, int? SortOrder);
private sealed record AudioTrackRecord(Guid Id, string SourcePath, string Title, Guid? AudioLibraryId, int SortOrder);
private sealed record AudioLibraryRecord(Guid Id, string SourcePath, string Title, Guid LibraryRootId, Guid? ParentLibraryId, int? SortOrder);
```

- [ ] **Step 3: Add the gallery handler test**

Add this test before `BookScanMaterializesFolderVolumesChaptersAndPages`:

```csharp
[Fact]
public async Task GalleryScanTreatsRootFilesAsLooseAndFoldersAsNestedGalleries() {
    var root = new LibraryRootData(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "/media/images",
        "Images",
        Enabled: true,
        Recursive: true,
        ScanVideos: false,
        ScanImages: true,
        ScanAudio: false,
        ScanBooks: false,
        IsNsfw: false);
    var persistence = new FakeScanPersistence([root]) {
        Settings = new LibrarySettingsData(
            AutoGenerateMetadata: false,
            AutoGenerateFingerprints: false,
            GeneratePhash: false,
            AutoGeneratePreview: false,
            GenerateTrickplay: false,
            TrickplayIntervalSeconds: 10,
            PreviewClipDurationSeconds: 8,
            ThumbnailQuality: 2,
            TrickplayQuality: 2)
    };
    var discovery = new RecordingFileDiscovery(directoryGroups: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) {
        ["/media/images"] = ["/media/images/root.png"],
        ["/media/images/Gallery"] = ["/media/images/Gallery/a.png"],
        ["/media/images/Gallery/A secondGallery"] = ["/media/images/Gallery/A secondGallery/b.png"]
    });
    var handler = new ScanGalleryJobHandler(
        NullLogger<ScanGalleryJobHandler>.Instance,
        discovery,
        persistence);
    var job = new JobRunSnapshot(
        Guid.NewGuid(),
        JobType.ScanGallery,
        JobRunStatus.Running,
        Progress: 0,
        Message: null,
        PayloadJson: $$"""{"libraryRootId":"{{root.Id}}"}""",
        TargetEntityKind: "library-root",
        TargetEntityId: root.Id.ToString(),
        TargetLabel: root.Label,
        CreatedAt: DateTimeOffset.UtcNow,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: null);

    await handler.HandleAsync(new JobContext(job, new RecordingJobQueue()), CancellationToken.None);

    Assert.Equal(["/media/images/Gallery", "/media/images/Gallery/A secondGallery"], persistence.UpsertedGalleries.Select(gallery => gallery.SourcePath).ToArray());
    var gallery = persistence.UpsertedGalleries[0];
    var nested = persistence.UpsertedGalleries[1];
    Assert.Null(gallery.ParentGalleryId);
    Assert.Equal(gallery.Id, nested.ParentGalleryId);

    Assert.Contains(persistence.UpsertedImages, image => image.SourcePath == "/media/images/root.png" && image.GalleryEntityId is null);
    Assert.Contains(persistence.UpsertedImages, image => image.SourcePath == "/media/images/Gallery/a.png" && image.GalleryEntityId == gallery.Id);
    Assert.Contains(persistence.UpsertedImages, image => image.SourcePath == "/media/images/Gallery/A secondGallery/b.png" && image.GalleryEntityId == nested.Id);
    Assert.Equal(["/media/images/root.png"], persistence.ValidLooseImagePaths);
    Assert.Equal(["/media/images/Gallery", "/media/images/Gallery/A secondGallery"], persistence.ValidGalleryPaths);
}
```

- [ ] **Step 4: Add the audio handler test**

Add this test after the gallery test:

```csharp
[Fact]
public async Task AudioScanTreatsRootTracksAsLooseAndFoldersAsNestedLibraries() {
    var root = new LibraryRootData(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "/media/audio",
        "Audio",
        Enabled: true,
        Recursive: true,
        ScanVideos: false,
        ScanImages: false,
        ScanAudio: true,
        ScanBooks: false,
        IsNsfw: false);
    var persistence = new FakeScanPersistence([root]) {
        Settings = new LibrarySettingsData(
            AutoGenerateMetadata: false,
            AutoGenerateFingerprints: false,
            GeneratePhash: false,
            AutoGeneratePreview: false,
            GenerateTrickplay: false,
            TrickplayIntervalSeconds: 10,
            PreviewClipDurationSeconds: 8,
            ThumbnailQuality: 2,
            TrickplayQuality: 2)
    };
    var discovery = new RecordingFileDiscovery(directoryGroups: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) {
        ["/media/audio"] = ["/media/audio/root.flac"],
        ["/media/audio/Album"] = ["/media/audio/Album/01.flac"],
        ["/media/audio/Album/Disc 2"] = ["/media/audio/Album/Disc 2/02.flac"]
    });
    var handler = new ScanAudioJobHandler(
        NullLogger<ScanAudioJobHandler>.Instance,
        discovery,
        persistence);
    var job = new JobRunSnapshot(
        Guid.NewGuid(),
        JobType.ScanAudio,
        JobRunStatus.Running,
        Progress: 0,
        Message: null,
        PayloadJson: $$"""{"libraryRootId":"{{root.Id}}"}""",
        TargetEntityKind: "library-root",
        TargetEntityId: root.Id.ToString(),
        TargetLabel: root.Label,
        CreatedAt: DateTimeOffset.UtcNow,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: null);

    await handler.HandleAsync(new JobContext(job, new RecordingJobQueue()), CancellationToken.None);

    Assert.Equal(["/media/audio/Album", "/media/audio/Album/Disc 2"], persistence.UpsertedAudioLibraries.Select(library => library.SourcePath).ToArray());
    var album = persistence.UpsertedAudioLibraries[0];
    var disc = persistence.UpsertedAudioLibraries[1];
    Assert.Null(album.ParentLibraryId);
    Assert.Equal(album.Id, disc.ParentLibraryId);

    Assert.Contains(persistence.UpsertedAudioTracks, track => track.SourcePath == "/media/audio/root.flac" && track.AudioLibraryId is null);
    Assert.Contains(persistence.UpsertedAudioTracks, track => track.SourcePath == "/media/audio/Album/01.flac" && track.AudioLibraryId == album.Id);
    Assert.Contains(persistence.UpsertedAudioTracks, track => track.SourcePath == "/media/audio/Album/Disc 2/02.flac" && track.AudioLibraryId == disc.Id);
    Assert.Equal(["/media/audio/root.flac"], persistence.ValidLooseAudioTrackPaths);
    Assert.Equal(["/media/audio/Album", "/media/audio/Album/Disc 2"], persistence.ValidAudioLibraryPaths);
}
```

- [ ] **Step 5: Run handler tests to verify they fail**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Api.Tests/Prismedia.Api.Tests.csproj --filter "FullyQualifiedName~ScanJobHandlerTests"
```

Expected: FAIL. The new tests should fail because the handlers still create a root gallery/audio-library and do not pass parent containers.

## Task 3: Implement Handler Classification

**Files:**
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanGalleryJobHandler.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanAudioJobHandler.cs`

- [ ] **Step 1: Add path helper methods to both scan handlers**

Add these private helpers to each handler class:

```csharp
private static bool SamePath(string left, string right) =>
    string.Equals(
        Path.TrimEndingDirectorySeparator(left),
        Path.TrimEndingDirectorySeparator(right),
        StringComparison.OrdinalIgnoreCase);

private static int PathDepth(string rootPath, string path) {
    var relative = Path.GetRelativePath(rootPath, path);
    if (relative == ".") return 0;
    return relative
        .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
        .Length;
}

private static int SiblingSortOrder(string rootPath, string path, IReadOnlyCollection<string> candidatePaths) {
    var parent = Path.GetDirectoryName(path);
    return candidatePaths
        .Where(candidate => string.Equals(Path.GetDirectoryName(candidate), parent, StringComparison.OrdinalIgnoreCase))
        .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
        .Select((candidate, index) => new { candidate, index })
        .First(item => string.Equals(item.candidate, path, StringComparison.OrdinalIgnoreCase))
        .index;
}
```

- [ ] **Step 2: Replace gallery scan loop**

In `ScanGalleryJobHandler.ScanRootAsync`, replace the `foreach (var (dirPath, imageFiles) in dirGroups)` loop and final cleanup with this structure:

```csharp
var validGalleryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var validLooseImagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var galleryIdsByPath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
var processedDirs = 0;

if (dirGroups.TryGetValue(root.Path, out var looseImages)) {
    for (var i = 0; i < looseImages.Count; i++) {
        var filePath = looseImages[i];
        var title = Path.GetFileNameWithoutExtension(filePath);
        validLooseImagePaths.Add(filePath);

        long? size = null;
        try { size = new FileInfo(filePath).Length; } catch { }

        var imageId = await Persistence.UpsertImageAsync(filePath, title, galleryEntityId: null, size, i, root.IsNsfw, cancellationToken);
        await EnqueueImageJobsAsync(context, settings, imageId, title, cancellationToken);
    }
}

var folderPaths = dirGroups.Keys
    .Where(path => !SamePath(path, root.Path))
    .OrderBy(path => PathDepth(root.Path, path))
    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

foreach (var dirPath in folderPaths) {
    var galleryTitle = Path.GetFileName(dirPath);
    validGalleryPaths.Add(dirPath);
    var parentPath = Path.GetDirectoryName(dirPath);
    var parentGalleryId = parentPath is not null && galleryIdsByPath.TryGetValue(parentPath, out var parentId)
        ? parentId
        : (Guid?)null;
    var sortOrder = SiblingSortOrder(root.Path, dirPath, folderPaths);

    var galleryId = await Persistence.UpsertGalleryAsync(dirPath, galleryTitle, root.Id, parentGalleryId, sortOrder, root.IsNsfw, cancellationToken);
    galleryIdsByPath[dirPath] = galleryId;

    var validImagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var imageFiles = dirGroups[dirPath];
    for (var i = 0; i < imageFiles.Count; i++) {
        var filePath = imageFiles[i];
        var title = Path.GetFileNameWithoutExtension(filePath);
        validImagePaths.Add(filePath);

        long? size = null;
        try { size = new FileInfo(filePath).Length; } catch { }

        var imageId = await Persistence.UpsertImageAsync(filePath, title, galleryId, size, i, root.IsNsfw, cancellationToken);
        await EnqueueImageJobsAsync(context, settings, imageId, title, cancellationToken);
    }

    await Persistence.RemoveStaleImagesInGalleryAsync(galleryId, validImagePaths, cancellationToken);
    processedDirs++;

    if (processedDirs % 10 == 0) {
        await context.ReportProgressAsync(processedDirs * 80 / Math.Max(1, folderPaths.Length),
            $"Processed {processedDirs}/{folderPaths.Length} directories", cancellationToken);
    }
}

await Persistence.RemoveStaleLooseImagesInRootAsync(root.Id, validLooseImagePaths, cancellationToken);
await Persistence.RemoveStaleGalleriesInRootAsync(root.Id, validGalleryPaths, cancellationToken);
await Persistence.UpdateRootLastScannedAsync(root.Id, cancellationToken);
```

Extract the duplicated image downstream enqueue logic into:

```csharp
private async Task EnqueueImageJobsAsync(
    JobContext context,
    LibrarySettingsData settings,
    Guid imageId,
    string title,
    CancellationToken cancellationToken) {
    if (settings.AutoGeneratePreview && !await Persistence.HasEntityFileAsync(imageId, EntityFileRole.Thumbnail, cancellationToken)) {
        await context.EnqueueIfNeededAsync(new EnqueueJobRequest(
            JobType.GenerateImageThumbnail, TargetEntityKind: "image",
            TargetEntityId: imageId.ToString(), TargetLabel: title), cancellationToken);
    }

    if (settings.AutoGenerateFingerprints && !await Persistence.HasEntityFingerprintAsync(imageId, FingerprintAlgorithm.Md5, cancellationToken)) {
        await context.EnqueueIfNeededAsync(new EnqueueJobRequest(
            JobType.FingerprintImage, TargetEntityKind: "image",
            TargetEntityId: imageId.ToString(), TargetLabel: title), cancellationToken);
    }
}
```

- [ ] **Step 3: Replace audio scan loop**

In `ScanAudioJobHandler.ScanRootAsync`, replace the `foreach (var (dirPath, audioFiles) in dirGroups)` loop and final cleanup with this structure:

```csharp
var validLibraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var validLooseTrackPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var libraryIdsByPath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
var processedDirs = 0;

if (dirGroups.TryGetValue(root.Path, out var looseTracks)) {
    for (var i = 0; i < looseTracks.Count; i++) {
        var filePath = looseTracks[i];
        var title = Path.GetFileNameWithoutExtension(filePath);
        validLooseTrackPaths.Add(filePath);

        var trackId = await Persistence.UpsertAudioTrackAsync(filePath, title, audioLibraryId: null, i, root.IsNsfw, cancellationToken);
        await EnqueueAudioTrackJobsAsync(context, settings, trackId, title, cancellationToken);
    }
}

var folderPaths = dirGroups.Keys
    .Where(path => !SamePath(path, root.Path))
    .OrderBy(path => PathDepth(root.Path, path))
    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

foreach (var dirPath in folderPaths) {
    var libraryTitle = Path.GetFileName(dirPath);
    validLibraryPaths.Add(dirPath);
    var parentPath = Path.GetDirectoryName(dirPath);
    var parentLibraryId = parentPath is not null && libraryIdsByPath.TryGetValue(parentPath, out var parentId)
        ? parentId
        : (Guid?)null;
    var sortOrder = SiblingSortOrder(root.Path, dirPath, folderPaths);

    var libraryId = await Persistence.UpsertAudioLibraryAsync(dirPath, libraryTitle, root.Id, parentLibraryId, sortOrder, root.IsNsfw, cancellationToken);
    libraryIdsByPath[dirPath] = libraryId;

    var validTrackPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var audioFiles = dirGroups[dirPath];
    for (var i = 0; i < audioFiles.Count; i++) {
        var filePath = audioFiles[i];
        var title = Path.GetFileNameWithoutExtension(filePath);
        validTrackPaths.Add(filePath);

        var trackId = await Persistence.UpsertAudioTrackAsync(filePath, title, libraryId, i, root.IsNsfw, cancellationToken);
        await EnqueueAudioTrackJobsAsync(context, settings, trackId, title, cancellationToken);
    }

    await Persistence.RemoveStaleAudioTracksInLibraryAsync(libraryId, validTrackPaths, cancellationToken);
    processedDirs++;

    if (processedDirs % 10 == 0) {
        await context.ReportProgressAsync(processedDirs * 80 / Math.Max(1, folderPaths.Length),
            $"Processed {processedDirs}/{folderPaths.Length} directories", cancellationToken);
    }
}

await Persistence.RemoveStaleLooseAudioTracksInRootAsync(root.Id, validLooseTrackPaths, cancellationToken);
await Persistence.RemoveStaleAudioLibrariesInRootAsync(root.Id, validLibraryPaths, cancellationToken);
await Persistence.UpdateRootLastScannedAsync(root.Id, cancellationToken);
```

Extract the duplicated audio downstream enqueue logic into:

```csharp
private async Task EnqueueAudioTrackJobsAsync(
    JobContext context,
    LibrarySettingsData settings,
    Guid trackId,
    string title,
    CancellationToken cancellationToken) {
    if (settings.AutoGenerateMetadata && !await Persistence.HasEntityTechnicalAsync(trackId, cancellationToken)) {
        await context.EnqueueIfNeededAsync(new EnqueueJobRequest(
            JobType.ProbeAudio, TargetEntityKind: "audio-track",
            TargetEntityId: trackId.ToString(), TargetLabel: title), cancellationToken);
    }

    if (settings.AutoGenerateFingerprints && !await Persistence.HasEntityFingerprintAsync(trackId, FingerprintAlgorithm.Md5, cancellationToken)) {
        await context.EnqueueIfNeededAsync(new EnqueueJobRequest(
            JobType.FingerprintAudio, TargetEntityKind: "audio-track",
            TargetEntityId: trackId.ToString(), TargetLabel: title), cancellationToken);
    }
}
```

- [ ] **Step 4: Run handler tests**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Api.Tests/Prismedia.Api.Tests.csproj --filter "FullyQualifiedName~ScanJobHandlerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/backend/src/Prismedia.Application/Jobs/Ports/ILibraryScanPersistence.cs apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanGalleryJobHandler.cs apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanAudioJobHandler.cs apps/backend/tests/Prismedia.Api.Tests/ScanJobHandlerTests.cs
git commit -m "feat(scan): classify folder-backed media containers"
```

## Task 4: Add Failing Persistence Tests

**Files:**
- Modify: `apps/backend/tests/Prismedia.Infrastructure.Tests/LibraryScanPersistenceServiceTests.cs`

- [ ] **Step 1: Add parent-aware gallery/audio-library upsert tests**

Add these tests before `RemoveStaleVideosByRootRemovesRootPathVideosWithoutLinkedRoot`:

```csharp
[Fact]
public async Task UpsertGalleryStoresFolderParentAndSortOrder() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var service = new LibraryScanPersistenceService(db);

    var parentId = await service.UpsertGalleryAsync("/media/images/Gallery", "Gallery", rootId, parentGalleryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var childId = await service.UpsertGalleryAsync("/media/images/Gallery/Sub", "Sub", rootId, parentId, sortOrder: 2, isNsfw: false, CancellationToken.None);

    var child = await db.Entities.SingleAsync(entity => entity.Id == childId);
    Assert.Equal(parentId, child.ParentEntityId);
    Assert.Equal(2, child.SortOrder);
    Assert.Equal(rootId, db.GalleryDetails.Single(detail => detail.EntityId == childId).LibraryRootId);
}

[Fact]
public async Task UpsertAudioLibraryStoresFolderParentAndSortOrder() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var service = new LibraryScanPersistenceService(db);

    var parentId = await service.UpsertAudioLibraryAsync("/media/audio/Album", "Album", rootId, parentLibraryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var childId = await service.UpsertAudioLibraryAsync("/media/audio/Album/Disc 2", "Disc 2", rootId, parentId, sortOrder: 1, isNsfw: false, CancellationToken.None);

    var child = await db.Entities.SingleAsync(entity => entity.Id == childId);
    Assert.Equal(parentId, child.ParentEntityId);
    Assert.Equal(1, child.SortOrder);
    Assert.Equal(rootId, db.AudioLibraryDetails.Single(detail => detail.EntityId == childId).LibraryRootId);
}
```

- [ ] **Step 2: Add loose relinking tests**

Add:

```csharp
[Fact]
public async Task UpsertImageCanRelinkExistingImageBackToLooseRootFile() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    var service = new LibraryScanPersistenceService(db);
    var galleryId = await service.UpsertGalleryAsync("/media/images/Gallery", "Gallery", rootId, parentGalleryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var imageId = await service.UpsertImageAsync("/media/images/root.png", "root", galleryId, sizeBytes: 10, sortOrder: 3, isNsfw: false, CancellationToken.None);

    await service.UpsertImageAsync("/media/images/root.png", "root", galleryEntityId: null, sizeBytes: 10, sortOrder: 0, isNsfw: false, CancellationToken.None);

    var image = await db.Entities.SingleAsync(entity => entity.Id == imageId);
    Assert.Null(image.ParentEntityId);
    Assert.Null(image.SortOrder);
}

[Fact]
public async Task UpsertAudioTrackCanRelinkExistingTrackBackToLooseRootFile() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    var service = new LibraryScanPersistenceService(db);
    var libraryId = await service.UpsertAudioLibraryAsync("/media/audio/Album", "Album", rootId, parentLibraryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var trackId = await service.UpsertAudioTrackAsync("/media/audio/root.flac", "root", libraryId, sortOrder: 7, isNsfw: false, CancellationToken.None);

    await service.UpsertAudioTrackAsync("/media/audio/root.flac", "root", audioLibraryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);

    var track = await db.Entities.SingleAsync(entity => entity.Id == trackId);
    Assert.Null(track.ParentEntityId);
    Assert.Null(track.SortOrder);
}
```

- [ ] **Step 3: Add loose stale cleanup tests**

Add:

```csharp
[Fact]
public async Task RemoveStaleLooseImagesInRootRemovesOnlyMissingRootLevelImages() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    var now = DateTimeOffset.UtcNow;
    db.LibraryRoots.Add(new LibraryRootRow {
        Id = rootId,
        Path = "/media/images",
        Label = "Images",
        CreatedAt = now,
        UpdatedAt = now
    });
    var service = new LibraryScanPersistenceService(db);
    var looseKeep = await service.UpsertImageAsync("/media/images/keep.png", "keep", galleryEntityId: null, sizeBytes: 1, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var looseRemove = await service.UpsertImageAsync("/media/images/remove.png", "remove", galleryEntityId: null, sizeBytes: 1, sortOrder: 1, isNsfw: false, CancellationToken.None);
    var galleryId = await service.UpsertGalleryAsync("/media/images/Gallery", "Gallery", rootId, parentGalleryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var contained = await service.UpsertImageAsync("/media/images/Gallery/remove.png", "remove", galleryId, sizeBytes: 1, sortOrder: 0, isNsfw: false, CancellationToken.None);

    var removed = await service.RemoveStaleLooseImagesInRootAsync(rootId, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/media/images/keep.png" }, CancellationToken.None);

    Assert.Equal(1, removed);
    Assert.True(await db.Entities.AnyAsync(entity => entity.Id == looseKeep));
    Assert.False(await db.Entities.AnyAsync(entity => entity.Id == looseRemove));
    Assert.True(await db.Entities.AnyAsync(entity => entity.Id == contained));
}

[Fact]
public async Task RemoveStaleLooseAudioTracksInRootRemovesOnlyMissingRootLevelTracks() {
    await using var db = CreateContext();
    var rootId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    var now = DateTimeOffset.UtcNow;
    db.LibraryRoots.Add(new LibraryRootRow {
        Id = rootId,
        Path = "/media/audio",
        Label = "Audio",
        CreatedAt = now,
        UpdatedAt = now
    });
    var service = new LibraryScanPersistenceService(db);
    var looseKeep = await service.UpsertAudioTrackAsync("/media/audio/keep.flac", "keep", audioLibraryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var looseRemove = await service.UpsertAudioTrackAsync("/media/audio/remove.flac", "remove", audioLibraryId: null, sortOrder: 1, isNsfw: false, CancellationToken.None);
    var libraryId = await service.UpsertAudioLibraryAsync("/media/audio/Album", "Album", rootId, parentLibraryId: null, sortOrder: 0, isNsfw: false, CancellationToken.None);
    var contained = await service.UpsertAudioTrackAsync("/media/audio/Album/remove.flac", "remove", libraryId, sortOrder: 0, isNsfw: false, CancellationToken.None);

    var removed = await service.RemoveStaleLooseAudioTracksInRootAsync(rootId, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/media/audio/keep.flac" }, CancellationToken.None);

    Assert.Equal(1, removed);
    Assert.True(await db.Entities.AnyAsync(entity => entity.Id == looseKeep));
    Assert.False(await db.Entities.AnyAsync(entity => entity.Id == looseRemove));
    Assert.True(await db.Entities.AnyAsync(entity => entity.Id == contained));
}
```

- [ ] **Step 4: Run persistence tests to verify they fail**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests/Prismedia.Infrastructure.Tests.csproj --filter "FullyQualifiedName~LibraryScanPersistenceServiceTests"
```

Expected: FAIL because persistence has not implemented optional parents, loose relinking, or loose cleanup.

## Task 5: Implement Persistence Behavior

**Files:**
- Modify: `apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.cs`

- [ ] **Step 1: Update `UpsertImageAsync` existing-row relinking**

In the existing-row branch of `UpsertImageAsync`, replace the conditional parent update with:

```csharp
existing.UpdatedAt = DateTimeOffset.UtcNow;
if (galleryEntityId is not null) {
    await UpsertStructuralChildLinkAsync(
        galleryEntityId.Value,
        existing.Id,
        sortOrder,
        DateTimeOffset.UtcNow,
        cancellationToken);
} else {
    var tracked = await db.Entities.FindAsync([existing.Id], cancellationToken);
    if (tracked is not null) {
        tracked.ParentEntityId = null;
        tracked.SortOrder = null;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 2: Update `UpsertGalleryAsync` signature and body**

Change the method signature to:

```csharp
public async Task<Guid> UpsertGalleryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentGalleryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken)
```

In the existing branch, set:

```csharp
var tracked = await db.Entities.FindAsync([existing.Id], cancellationToken);
if (tracked is not null) {
    tracked.ParentEntityId = parentGalleryId;
    tracked.SortOrder = sortOrder;
    tracked.UpdatedAt = DateTimeOffset.UtcNow;
    if (isNsfw) tracked.IsNsfw = true;
}
```

In the new row, include:

```csharp
ParentEntityId = parentGalleryId,
SortOrder = sortOrder,
```

- [ ] **Step 3: Update `UpsertAudioTrackAsync` signature and relinking**

Change the method signature to:

```csharp
public async Task<Guid> UpsertAudioTrackAsync(string filePath, string title, Guid? audioLibraryId, int sortOrder, bool isNsfw, CancellationToken cancellationToken)
```

In the existing branch:

```csharp
existing.UpdatedAt = DateTimeOffset.UtcNow;
if (audioLibraryId is not null) {
    await UpsertStructuralChildLinkAsync(
        audioLibraryId.Value,
        existing.Id,
        sortOrder,
        DateTimeOffset.UtcNow,
        cancellationToken);
} else {
    var tracked = await db.Entities.FindAsync([existing.Id], cancellationToken);
    if (tracked is not null) {
        tracked.ParentEntityId = null;
        tracked.SortOrder = null;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

For the new entity row, set:

```csharp
ParentEntityId = audioLibraryId,
SortOrder = audioLibraryId is null ? null : sortOrder,
```

Only call `UpsertStructuralChildLinkAsync` when `audioLibraryId is not null`.

- [ ] **Step 4: Update `UpsertAudioLibraryAsync` signature and body**

Change the method signature to:

```csharp
public async Task<Guid> UpsertAudioLibraryAsync(string folderPath, string title, Guid libraryRootId, Guid? parentLibraryId, int? sortOrder, bool isNsfw, CancellationToken cancellationToken)
```

In the existing branch, set:

```csharp
var tracked = await db.Entities.FindAsync([existing.Id], cancellationToken);
if (tracked is not null) {
    tracked.ParentEntityId = parentLibraryId;
    tracked.SortOrder = sortOrder;
    tracked.UpdatedAt = DateTimeOffset.UtcNow;
    if (isNsfw) tracked.IsNsfw = true;
}
```

In the new row, include:

```csharp
ParentEntityId = parentLibraryId,
SortOrder = sortOrder,
```

- [ ] **Step 5: Add loose root cleanup methods**

Add methods near the existing stale cleanup methods:

```csharp
public async Task<int> RemoveStaleLooseImagesInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken) {
    var rootPath = await db.LibraryRoots.AsNoTracking()
        .Where(root => root.Id == rootId)
        .Select(root => root.Path)
        .FirstOrDefaultAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(rootPath)) {
        return 0;
    }

    var imageCode = EntityKindRegistry.Image.Code;
    var imageIds = await db.EntityFiles.AsNoTracking()
        .Where(file => file.Role == EntityFileRole.Source)
        .Join(
            db.Entities.AsNoTracking().Where(entity => entity.KindCode == imageCode && entity.ParentEntityId == null),
            file => file.EntityId,
            entity => entity.Id,
            (file, entity) => new { file.EntityId, file.Path })
        .Where(file => IsDirectChildFileOfRoot(file.Path, rootPath))
        .Select(file => file.EntityId)
        .ToListAsync(cancellationToken);

    return await RemoveStaleEntitiesBySourcePath(imageIds, validPaths, cancellationToken);
}

public async Task<int> RemoveStaleLooseAudioTracksInRootAsync(Guid rootId, IReadOnlySet<string> validPaths, CancellationToken cancellationToken) {
    var rootPath = await db.LibraryRoots.AsNoTracking()
        .Where(root => root.Id == rootId)
        .Select(root => root.Path)
        .FirstOrDefaultAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(rootPath)) {
        return 0;
    }

    var trackCode = EntityKindRegistry.AudioTrack.Code;
    var trackIds = await db.EntityFiles.AsNoTracking()
        .Where(file => file.Role == EntityFileRole.Source)
        .Join(
            db.Entities.AsNoTracking().Where(entity => entity.KindCode == trackCode && entity.ParentEntityId == null),
            file => file.EntityId,
            entity => entity.Id,
            (file, entity) => new { file.EntityId, file.Path })
        .Where(file => IsDirectChildFileOfRoot(file.Path, rootPath))
        .Select(file => file.EntityId)
        .ToListAsync(cancellationToken);

    return await RemoveStaleEntitiesBySourcePath(trackIds, validPaths, cancellationToken);
}
```

Add helper near `IsPathUnderRoot`:

```csharp
private static bool IsDirectChildFileOfRoot(string path, string rootPath) {
    var normalizedRoot = NormalizePath(rootPath);
    var directory = Path.GetDirectoryName(path);
    if (string.IsNullOrWhiteSpace(directory)) {
        return false;
    }

    return NormalizePath(directory).Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 6: Run persistence tests**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests/Prismedia.Infrastructure.Tests.csproj --filter "FullyQualifiedName~LibraryScanPersistenceServiceTests"
```

Expected: PASS.

- [ ] **Step 7: Run handler tests again**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Api.Tests/Prismedia.Api.Tests.csproj --filter "FullyQualifiedName~ScanJobHandlerTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.cs apps/backend/tests/Prismedia.Infrastructure.Tests/LibraryScanPersistenceServiceTests.cs
git commit -m "fix(scan): persist nested gallery and audio folders"
```

## Task 6: Changelog And Full Verification

**Files:**
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add changelog entry**

Under `## [Unreleased]` and `### Changed`, add:

```markdown
- Image and audio scans now keep root-level loose files standalone while turning folders into nested galleries and audio libraries.
```

- [ ] **Step 2: Run focused tests**

Run:

```bash
dotnet test apps/backend/tests/Prismedia.Api.Tests/Prismedia.Api.Tests.csproj --filter "FullyQualifiedName~ScanJobHandlerTests"
dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests/Prismedia.Infrastructure.Tests.csproj --filter "FullyQualifiedName~LibraryScanPersistenceServiceTests"
```

Expected: both PASS.

- [ ] **Step 3: Run backend build**

Run:

```bash
dotnet build apps/backend/Prismedia.slnx
```

Expected: PASS.

- [ ] **Step 4: Run release check**

Run:

```bash
pnpm release:check
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CHANGELOG.md
git commit -m "docs: note folder-backed scan behavior"
```

## Self-Review

- Spec coverage: The tasks cover root-level loose media, folder-backed galleries/audio libraries, nested parent links, stale cleanup, no UI/API contract changes, and the changelog.
- Placeholder scan: This plan uses concrete tests, method signatures, helper code, commands, and expected outcomes.
- Type consistency: The new method signatures are introduced in the port, implemented in EF persistence, and consumed by both scan handlers and test doubles.
