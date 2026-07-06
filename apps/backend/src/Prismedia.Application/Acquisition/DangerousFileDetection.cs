namespace Prismedia.Application.Acquisition;

/// <summary>
/// Detects executable / dangerous files in a completed download's payload. A payload carrying one is
/// held for manual review instead of imported — a release whose "video" is a <c>.scr</c> is malware,
/// and silently importing (or silently skipping) it would either endanger the library host or leave
/// the acquisition stuck with no explanation. Extension lists mirror Sonarr's, which this gate was
/// modeled on.
/// </summary>
public static class DangerousFileDetection {
    // prism-vocab: external — the extension vocabulary matches Sonarr's DangerousExtensions +
    // ExecutableExtensions sets so behavior is drop-in familiar.
    private static readonly IReadOnlySet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        ".arj", ".lnk", ".lzh", ".ps1", ".scr", ".vbs", ".zipx",
        ".bat", ".cmd", ".exe", ".sh", ".msi", ".com", ".pif"
    };

    /// <summary>The first dangerous file among the payload paths, or null when the payload is clean.</summary>
    public static string? FindDangerousFile(IEnumerable<string> filePaths) =>
        filePaths.FirstOrDefault(path => DangerousExtensions.Contains(Path.GetExtension(path)));

    /// <summary>
    /// True when a release TITLE already names a dangerous file (e.g. "Some Book.epub.exe") — the
    /// pre-download counterpart of the payload gate, so an obvious fake-release payload is rejected
    /// before it is ever grabbed. Only the title's trailing extension is considered; a dangerous token
    /// mid-title ("exe files explained") stays acceptable.
    /// </summary>
    public static bool IsDangerousTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) && DangerousExtensions.Contains(Path.GetExtension(title.Trim()));
}
