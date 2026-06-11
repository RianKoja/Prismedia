using Prismedia.Contracts.Requests;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Requests;

/// <summary>
/// Hydrates a request detail with rich metadata (cast, ratings, artwork) from the
/// upstream metadata catalog after the Arr service supplies the requestable stub.
/// Enrichment is advisory: implementations return null instead of throwing when the
/// catalog is unreachable or unconfigured, and detail loading proceeds without it.
/// </summary>
public interface IRequestDetailEnrichmentSource {
    /// <summary>
    /// Loads enrichment for an external item, or null when none is available.
    /// </summary>
    /// <param name="kind">Request media kind; implementations may support a subset.</param>
    /// <param name="externalId">The detail's external id (TMDB id for movies, TVDB id for series).</param>
    Task<RequestDetailEnrichment?> GetAsync(RequestMediaKind kind, string externalId, CancellationToken cancellationToken);
}

/// <summary>
/// Rich metadata fetched from the catalog for one request detail. All fields are
/// optional supplements; <see cref="Apply"/> defines how they merge into the stub.
/// </summary>
public sealed record RequestDetailEnrichment(
    string? BackdropUrl,
    string? Certification,
    decimal? Rating,
    IReadOnlyList<RequestCastMember> Cast,
    IReadOnlyList<RequestRatingValue> Ratings,
    IReadOnlyList<string> CrewCredits) {

    /// <summary>
    /// Merges this enrichment into a service-provided detail stub. The service's own
    /// values win for scalar fields (it reflects what will actually be requested);
    /// enrichment fills gaps and contributes the structured cast. Ratings combine by
    /// source, preferring the catalog's fresh value for sources it owns.
    /// </summary>
    public RequestDetailResponse Apply(RequestDetailResponse detail) {
        var enrichedSources = Ratings.Select(rating => rating.Source).ToHashSet();
        var ratings = detail.Ratings
            .Where(rating => !enrichedSources.Contains(rating.Source))
            .Concat(Ratings)
            .OrderBy(rating => rating.Source)
            .ToArray();

        return detail with {
            BackdropUrl = detail.BackdropUrl ?? BackdropUrl,
            Certification = string.IsNullOrWhiteSpace(detail.Certification) ? Certification : detail.Certification,
            Rating = detail.Rating ?? Rating,
            Cast = detail.Cast.Count > 0 ? detail.Cast : Cast,
            Credits = detail.Credits.Count > 0 ? detail.Credits : CrewCredits,
            Ratings = ratings
        };
    }
}
