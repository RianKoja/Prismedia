using System.Text.Json;
using Prismedia.Application.Requests;
using Prismedia.Contracts.Requests;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;

namespace Prismedia.Infrastructure.Requests;

/// <summary>
/// Hydrates movie and series request details straight from TMDB once the Arr service
/// has supplied the requestable stub: structured cast with headshots, community
/// rating, director/writer credits, certification, and a large backdrop. Series ids
/// arrive as TVDB ids (Sonarr's external id), so they resolve through TMDB's find
/// endpoint first. Returns null — never an error — when the TMDB metadata provider
/// is unconfigured, the item is unknown, or the catalog is unreachable.
/// </summary>
public sealed class TmdbRequestEnrichmentSource(HttpClient http, PrismediaDbContext db) : IRequestDetailEnrichmentSource {
    private const int MaxCastMembers = 14;
    private const int MaxCrewCredits = 6;

    public async Task<RequestDetailEnrichment?> GetAsync(RequestMediaKind kind, string externalId, CancellationToken cancellationToken) {
        try {
            if (kind is not (RequestMediaKind.Movie or RequestMediaKind.Series) || string.IsNullOrWhiteSpace(externalId)) {
                return null;
            }

            var apiKey = await TmdbApiKeyResolver.ResolveAsync(db, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey)) {
                return null;
            }

            return kind == RequestMediaKind.Movie
                ? await EnrichMovieAsync(externalId, apiKey, cancellationToken)
                : await EnrichSeriesAsync(externalId, apiKey, cancellationToken);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            return null;
        }
    }

    private async Task<RequestDetailEnrichment?> EnrichMovieAsync(string tmdbId, string apiKey, CancellationToken cancellationToken) {
        using var document = await GetJsonAsync(
            $"{TmdbProtocol.MovieDetailUrlBase}{Uri.EscapeDataString(tmdbId)}" +
            $"?{TmdbProtocol.AppendToResponseParam}={TmdbProtocol.MovieDetailAppend}" +
            $"&{TmdbProtocol.ApiKeyParam}={Uri.EscapeDataString(apiKey)}",
            cancellationToken);
        var root = document.RootElement;

        return new RequestDetailEnrichment(
            ImageUrl(TmdbProtocol.BackdropLargeImageBase, Text(root, TmdbProtocol.BackdropPath)),
            MovieCertification(root),
            VoteAverage(root),
            CastMembers(root),
            RatingValues(root),
            CrewCredits(root));
    }

    private async Task<RequestDetailEnrichment?> EnrichSeriesAsync(string tvdbId, string apiKey, CancellationToken cancellationToken) {
        var tmdbId = await FindSeriesTmdbIdAsync(tvdbId, apiKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(tmdbId)) {
            return null;
        }

        using var document = await GetJsonAsync(
            $"{TmdbProtocol.TvDetailUrlBase}{Uri.EscapeDataString(tmdbId)}" +
            $"?{TmdbProtocol.AppendToResponseParam}={TmdbProtocol.TvDetailAppend}" +
            $"&{TmdbProtocol.ApiKeyParam}={Uri.EscapeDataString(apiKey)}",
            cancellationToken);
        var root = document.RootElement;

        return new RequestDetailEnrichment(
            ImageUrl(TmdbProtocol.BackdropLargeImageBase, Text(root, TmdbProtocol.BackdropPath)),
            SeriesCertification(root),
            VoteAverage(root),
            CastMembers(root),
            RatingValues(root),
            CrewCredits(root));
    }

    /// <summary>Resolves a Sonarr TVDB id to a TMDB series id through TMDB's find endpoint.</summary>
    private async Task<string?> FindSeriesTmdbIdAsync(string tvdbId, string apiKey, CancellationToken cancellationToken) {
        using var document = await GetJsonAsync(
            $"{TmdbProtocol.FindUrlBase}{Uri.EscapeDataString(tvdbId)}" +
            $"?{TmdbProtocol.ExternalSourceParam}={TmdbProtocol.TvdbExternalSource}" +
            $"&{TmdbProtocol.ApiKeyParam}={Uri.EscapeDataString(apiKey)}",
            cancellationToken);

        return document.RootElement.TryGetProperty(TmdbProtocol.TvResults, out var results) &&
            results.ValueKind == JsonValueKind.Array &&
            results.GetArrayLength() > 0
                ? Text(results[0], TmdbProtocol.Id)
                : null;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken) {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static IReadOnlyList<RequestCastMember> CastMembers(JsonElement root) =>
        ArrayOf(Prop(root, TmdbProtocol.Credits), TmdbProtocol.CastList)
            .Take(MaxCastMembers)
            .Select(member => new RequestCastMember(
                Text(member, TmdbProtocol.Name) ?? string.Empty,
                Text(member, TmdbProtocol.Character),
                ImageUrl(TmdbProtocol.ProfileImageBase, Text(member, TmdbProtocol.ProfilePath))))
            .Where(member => member.Name.Length > 0)
            .ToArray();

    private static IReadOnlyList<string> CrewCredits(JsonElement root) =>
        ArrayOf(Prop(root, TmdbProtocol.Credits), TmdbProtocol.CrewList)
            // prism-vocab: external — TMDB crew job names at their single parse site.
            .Where(member => Text(member, TmdbProtocol.Job) is "Director" or "Writer" or "Screenplay")
            .Select(member => $"{Text(member, TmdbProtocol.Name)} ({Text(member, TmdbProtocol.Job)})")
            .Distinct()
            .Take(MaxCrewCredits)
            .ToArray();

    private static IReadOnlyList<RequestRatingValue> RatingValues(JsonElement root) =>
        VoteAverage(root) is { } value
            ? [new RequestRatingValue(RequestRatingSource.Tmdb, value, 10m, Int(root, TmdbProtocol.VoteCount))]
            : [];

    /// <summary>US theatrical certification from the movie's release_dates block.</summary>
    private static string? MovieCertification(JsonElement root) =>
        ArrayOf(Prop(root, TmdbProtocol.ReleaseDates), TmdbProtocol.Results)
            // prism-vocab: external — TMDB country code at its single parse site.
            .Where(country => Text(country, TmdbProtocol.CountryCode) == "US")
            .SelectMany(country => ArrayOf(country, TmdbProtocol.ReleaseDates))
            .Select(release => Text(release, TmdbProtocol.Certification))
            .FirstOrDefault(certification => !string.IsNullOrWhiteSpace(certification));

    /// <summary>US content rating from the series' content_ratings block.</summary>
    private static string? SeriesCertification(JsonElement root) =>
        ArrayOf(Prop(root, TmdbProtocol.ContentRatings), TmdbProtocol.Results)
            // prism-vocab: external — TMDB country code at its single parse site.
            .Where(country => Text(country, TmdbProtocol.CountryCode) == "US")
            .Select(country => Text(country, TmdbProtocol.ContentRating))
            .FirstOrDefault(rating => !string.IsNullOrWhiteSpace(rating));

    private static decimal? VoteAverage(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(TmdbProtocol.VoteAverage, out var value) &&
        value.TryGetDecimal(out var rating) && rating > 0
            ? Math.Round(rating, 1)
            : null;

    private static JsonElement Prop(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : default;

    private static IEnumerable<JsonElement> ArrayOf(JsonElement element, string name) {
        var value = Prop(element, name);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    }

    private static string? Text(JsonElement item, string name) {
        var value = Prop(item, name);
        return value.ValueKind switch {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static int? Int(JsonElement item, string name) {
        var value = Prop(item, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : null;
    }

    private static string? ImageUrl(string baseUrl, string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : baseUrl + path;
}
