namespace Prismedia.Infrastructure.Media.Persistence;

internal static class LibraryScanPathRules {
    public static bool IsDirectChildPath(string path, string parentPath) {
        var normalizedPath = NormalizePath(path);
        var normalizedParent = NormalizePath(parentPath);

        if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedParent)) {
            return false;
        }

        var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/').TrimEnd('/');
        return string.Equals(directory, normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathUnderRoot(string path, string rootPath) {
        var normalizedPath = NormalizePath(path);
        var normalizedRoot = NormalizePath(rootPath);

        if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedRoot)) {
            return false;
        }

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathCoveredByExclusion(string path, string excludedPath) {
        var normalizedPath = NormalizePath(path);
        var normalizedExcluded = NormalizePath(excludedPath);

        return normalizedPath.Equals(normalizedExcluded, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedExcluded + "/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedExcluded + "::", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
