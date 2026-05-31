using Prismedia.Contracts.Entities;

namespace Prismedia.Contracts.Movies;

/// <summary>
/// API-facing movie detail shape with projected playable video children.
/// </summary>
public sealed record MovieDetail : EntityDetail {
    /// <summary>Relationship edge metadata for credited people shown on detail pages.</summary>
    public required IReadOnlyList<EntityCreditMetadata> CreditMetadata { get; init; }
}
