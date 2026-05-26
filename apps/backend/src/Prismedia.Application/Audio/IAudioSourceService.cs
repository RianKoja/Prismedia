namespace Prismedia.Application.Audio;

/// <summary>
/// Application port for locating original audio source files that can be streamed by the API host.
/// </summary>
public interface IAudioSourceService {
    /// <summary>
    /// Finds the source file for one audio track entity.
    /// </summary>
    /// <param name="id">Audio track entity identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>Source file metadata, or null when the track has no available source file.</returns>
    Task<AudioSourceFile?> GetSourceAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Source-file metadata needed by the API layer to serve direct audio playback.
/// </summary>
/// <param name="EntityId">Audio track entity identifier that owns the source file.</param>
/// <param name="Path">Absolute path to the source file on disk.</param>
/// <param name="ContentType">HTTP content type for the source file.</param>
/// <param name="DurationSeconds">Optional probed duration in seconds.</param>
public sealed record AudioSourceFile(
    Guid EntityId,
    string Path,
    string ContentType,
    double? DurationSeconds = null);
