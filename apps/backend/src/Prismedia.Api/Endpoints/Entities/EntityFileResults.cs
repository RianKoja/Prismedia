using System.IO.Compression;

namespace Prismedia.Api.Endpoints;

internal static class EntityFileResults {
    internal static async Task<IResult> StreamAsync(
        string path,
        string contentType,
        Func<IResult> notFound,
        CancellationToken cancellationToken,
        string? fileDownloadName = null) {
        if (TrySplitArchiveEntry(path, out var archivePath, out var memberPath)) {
            var archiveStream = await OpenArchiveEntryAsync(archivePath, memberPath, cancellationToken);
            return archiveStream is null
                ? notFound()
                : Results.File(archiveStream, contentType, fileDownloadName, enableRangeProcessing: true);
        }

        if (!File.Exists(path)) {
            return notFound();
        }

        return Results.File(
            File.OpenRead(path),
            contentType,
            fileDownloadName,
            enableRangeProcessing: true);
    }

    private static bool TrySplitArchiveEntry(
        string path,
        out string archivePath,
        out string memberPath) {
        var parts = path.Split("::", 2, StringSplitOptions.None);
        archivePath = parts.Length == 2 ? parts[0] : string.Empty;
        memberPath = parts.Length == 2 ? parts[1] : string.Empty;
        return parts.Length == 2 &&
               !string.IsNullOrWhiteSpace(archivePath) &&
               !string.IsNullOrWhiteSpace(memberPath);
    }

    private static async Task<Stream?> OpenArchiveEntryAsync(
        string archivePath,
        string memberPath,
        CancellationToken cancellationToken) {
        if (!File.Exists(archivePath)) {
            return null;
        }

        await using var archiveFile = File.OpenRead(archivePath);
        using var archive = new ZipArchive(archiveFile, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.GetEntry(memberPath);
        if (entry is null) {
            return null;
        }

        var output = new MemoryStream();
        await using (var entryStream = entry.Open()) {
            await entryStream.CopyToAsync(output, cancellationToken);
        }

        output.Position = 0;
        return output;
    }
}
