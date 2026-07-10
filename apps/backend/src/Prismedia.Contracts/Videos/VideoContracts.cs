using Prismedia.Contracts.Entities;

namespace Prismedia.Contracts.Videos;

/// <summary>
/// API-facing video detail shape combining video metadata with shared entity capabilities.
/// </summary>
public sealed record VideoDetail : EntityDetail {
    /// <summary>Relationship edge metadata for credited people shown on detail pages.</summary>
    public required IReadOnlyList<EntityCreditMetadata> CreditMetadata { get; init; }

    /// <summary>When managed embedded and adjacent subtitles were last reconciled, when known.</summary>
    public required DateTimeOffset? SubtitlesExtractedAt { get; init; }
}
