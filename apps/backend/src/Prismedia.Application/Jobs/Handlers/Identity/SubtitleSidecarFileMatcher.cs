using System.Globalization;

namespace Prismedia.Application.Jobs.Handlers.Identity;

/// <summary>
/// A subtitle sidecar file discovered beside a video: same directory, same filename stem,
/// a recognized subtitle extension, and an optional language/description tag.
/// </summary>
/// <param name="Path">Absolute path to the sidecar file.</param>
/// <param name="Extension">Lowercase extension without the leading dot (srt, vtt, ass, ssa).</param>
/// <param name="Language">Resolved BCP-47/ISO language code, or "und" when the tag did not name a language.</param>
/// <param name="Label">Humanized free-text tag when it did not resolve to a language (e.g. "Ai Translated").</param>
public sealed record SubtitleSidecarFile(string Path, string Extension, string Language, string? Label);

/// <summary>
/// Recognizes subtitle sidecar files sitting next to a video file and resolves each candidate's
/// language or descriptive label the way common media managers (Plex, Jellyfin) name them:
/// "Movie.mkv" + "Movie.srt", "Movie.en.srt", "Movie.pt-BR.srt", "Movie.ai_translated.srt".
/// </summary>
public static class SubtitleSidecarFileMatcher {
    public static readonly IReadOnlyList<string> SupportedExtensions = [".srt", ".vtt", ".ass", ".ssa"];

    /// <summary>
    /// Finds subtitle sidecar files in the same directory as <paramref name="videoFilePath"/> whose
    /// filename stem is the video's stem, optionally followed by a dot-separated tag.
    /// </summary>
    public static IReadOnlyList<SubtitleSidecarFile> FindCandidates(string videoFilePath) {
        var dir = Path.GetDirectoryName(videoFilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) {
            return [];
        }

        var stem = Path.GetFileNameWithoutExtension(videoFilePath);
        var results = new List<SubtitleSidecarFile>();
        foreach (var path in Directory.EnumerateFiles(dir)) {
            var extension = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            var fileStem = Path.GetFileNameWithoutExtension(path);
            if (!fileStem.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Require an exact stem match or a dot-separated tag after it, so "Movie 2.srt" beside
            // "Movie.mkv" is not mistaken for a sidecar of "Movie.mkv".
            var tail = fileStem[stem.Length..];
            if (tail.Length > 0 && tail[0] != '.') {
                continue;
            }

            var (language, label) = ResolveTag(tail.Length > 0 ? tail[1..] : string.Empty);
            results.Add(new SubtitleSidecarFile(path, extension.TrimStart('.').ToLowerInvariant(), language, label));
        }

        return results;
    }

    private static (string Language, string? Label) ResolveTag(string tag) {
        if (tag.Length == 0) {
            return ("und", null);
        }

        string? language = null;
        var labelParts = new List<string>();
        foreach (var segment in tag.Split('.', StringSplitOptions.RemoveEmptyEntries)) {
            if (language is null && TryResolveLanguage(segment, out var resolved)) {
                language = resolved;
            } else {
                labelParts.Add(Humanize(segment));
            }
        }

        return (language ?? "und", labelParts.Count > 0 ? string.Join(" ", labelParts) : null);
    }

    /// <summary>
    /// Resolves a filename tag to a language code via <see cref="CultureInfo"/> — .NET's own ISO
    /// 639/BCP-47 database. The primary subtag (before any region) must match a real language's
    /// two- or three-letter ISO 639 code first; <see cref="CultureInfo.GetCultureInfo(string)"/>
    /// alone is not enough to gate this, since ICU happily builds a well-formed-but-nonsense
    /// culture (e.g. "ai-translated" parses as language "ai", region "TRANSLATED") for any
    /// hyphen-shaped tag instead of throwing.
    /// </summary>
    private static bool TryResolveLanguage(string segment, out string languageCode) {
        var normalized = segment.Replace('_', '-');
        var primary = normalized.Split('-')[0];
        var match = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(culture =>
                culture.TwoLetterISOLanguageName.Equals(primary, StringComparison.OrdinalIgnoreCase) ||
                culture.ThreeLetterISOLanguageName.Equals(primary, StringComparison.OrdinalIgnoreCase));

        if (match is null) {
            languageCode = string.Empty;
            return false;
        }

        try {
            // Canonicalizes casing (e.g. "pt-br" -> "pt-BR") and keeps a real region when present.
            languageCode = CultureInfo.GetCultureInfo(normalized).Name;
        } catch (CultureNotFoundException) {
            languageCode = match.TwoLetterISOLanguageName;
        }

        return true;
    }

    private static string Humanize(string segment) {
        var words = segment.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant()));
        return string.Join(' ', words);
    }
}
