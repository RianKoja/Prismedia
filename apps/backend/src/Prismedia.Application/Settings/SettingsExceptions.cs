namespace Prismedia.Application.Settings;

/// <summary>
/// Raised when a request references an unknown app-global setting key.
/// </summary>
public sealed class SettingNotFoundException : KeyNotFoundException {
    public SettingNotFoundException(string key)
        : base($"Setting '{key}' was not found.") {
        Key = key;
    }

    /// <summary>Unknown setting key.</summary>
    public string Key { get; }
}

/// <summary>
/// Raised when a setting value fails registry validation.
/// </summary>
public sealed class SettingValidationException : ArgumentException {
    public SettingValidationException(string key, string message)
        : base(message) {
        Key = key;
    }

    /// <summary>Setting key whose value failed validation.</summary>
    public string Key { get; }
}

/// <summary>
/// Raised when a library root's path collides with one that already exists.
/// </summary>
public sealed class LibraryRootPathConflictException(string path)
    : InvalidOperationException($"A library root already exists at '{path}'.") {
    /// <summary>The path that already has a library root.</summary>
    public string Path { get; } = path;
}
