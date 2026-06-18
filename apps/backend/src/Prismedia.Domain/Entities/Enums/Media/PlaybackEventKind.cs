namespace Prismedia.Domain.Entities;

/// <summary>
/// Closed set of durable playback-history event kinds.
/// </summary>
public enum PlaybackEventKind {
    /// <summary>The entity reached an intentional completion/play-count event.</summary>
    [Code("completed")]
    Completed,

    /// <summary>The entity was likely abandoned quickly before meaningful playback progress.</summary>
    [Code("skipped")]
    Skipped
}
