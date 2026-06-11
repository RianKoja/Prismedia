using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Requests;
using Prismedia.Contracts.Entities;
using Prismedia.Contracts.Requests;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;

namespace Prismedia.Infrastructure.Requests;

/// <summary>
/// TMDB wire vocabulary for the adult-inclusive request search enrichment.
/// </summary>
public static class TmdbProtocol {
    public const string SearchMovieUrl = "https://api.themoviedb.org/3/search/movie";
    public const string MovieDetailUrlBase = "https://api.themoviedb.org/3/movie/";
    public const string TvDetailUrlBase = "https://api.themoviedb.org/3/tv/";
    public const string FindUrlBase = "https://api.themoviedb.org/3/find/";
    public const string QueryParam = "query";
    public const string IncludeAdultParam = "include_adult";
    public const string ApiKeyParam = "api_key";
    public const string AppendToResponseParam = "append_to_response";
    public const string ExternalSourceParam = "external_source";
    public const string TvdbExternalSource = "tvdb_id";
    public const string MovieDetailAppend = "credits,release_dates";
    public const string TvDetailAppend = "credits,content_ratings";

    public const string Results = "results";
    public const string TvResults = "tv_results";
    public const string Adult = "adult";
    public const string Id = "id";
    public const string Title = "title";
    public const string Overview = "overview";
    public const string PosterPath = "poster_path";
    public const string BackdropPath = "backdrop_path";
    public const string ReleaseDate = "release_date";
    public const string VoteAverage = "vote_average";
    public const string VoteCount = "vote_count";
    public const string Credits = "credits";
    public const string CastList = "cast";
    public const string CrewList = "crew";
    public const string Name = "name";
    public const string Character = "character";
    public const string Job = "job";
    public const string ProfilePath = "profile_path";
    public const string ReleaseDates = "release_dates";
    public const string ContentRatings = "content_ratings";
    public const string CountryCode = "iso_3166_1";
    public const string Certification = "certification";
    public const string ContentRating = "rating";

    public const string PosterImageBase = "https://image.tmdb.org/t/p/w342";
    public const string BackdropImageBase = "https://image.tmdb.org/t/p/w780";
    public const string BackdropLargeImageBase = "https://image.tmdb.org/t/p/w1280";
    public const string ProfileImageBase = "https://image.tmdb.org/t/p/w185";
}

/// <summary>
/// Resolves the API key of the enabled TMDB metadata provider, shared by every
/// TMDB-backed request source. Null when no enabled TMDB provider holds a key.
/// </summary>
public static class TmdbApiKeyResolver {
    public static async Task<string?> ResolveAsync(PrismediaDbContext db, CancellationToken cancellationToken) =>
        await db.ProviderConfigs
            .AsNoTracking()
            .Where(config => config.ProviderCode == ExternalIdProviders.Tmdb && config.Enabled)
            .Join(
                db.ProviderCredentials.AsNoTracking().Where(credential => credential.CredentialKey == RequestProviderHttp.ApiKeyCredential),
                config => config.Id,
                credential => credential.ProviderConfigId,
                (config, credential) => credential.EncryptedValue)
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Searches TMDB with <c>include_adult</c> using the configured TMDB metadata provider's API key.
/// Radarr's metadata text search omits adult titles even though Radarr resolves and adds them by
/// explicit TMDB id, so NSFW request searches merge these results into the Radarr answer; detail
/// and submit then flow through the normal <c>tmdb:</c> lookup path. Returns an empty list when
/// no enabled TMDB provider with an API key exists or the lookup fails — never an error, since
/// this only widens results.
/// </summary>
public sealed class TmdbAdultMovieSearchSource(HttpClient http, PrismediaDbContext db) : IAdultMovieSearchSource {
    public async Task<IReadOnlyList<RequestSearchResult>> SearchAsync(Guid serviceId, string query, CancellationToken cancellationToken) {
        try {
            var apiKey = await TmdbApiKeyResolver.ResolveAsync(db, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey)) {
                return [];
            }

            var url = $"{TmdbProtocol.SearchMovieUrl}" +
                $"?{TmdbProtocol.QueryParam}={Uri.EscapeDataString(query)}" +
                $"&{TmdbProtocol.IncludeAdultParam}=true" +
                $"&{TmdbProtocol.ApiKeyParam}={Uri.EscapeDataString(apiKey)}";
            using var response = await http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty(TmdbProtocol.Results, out var results) ||
                results.ValueKind != JsonValueKind.Array) {
                return [];
            }

            return results.EnumerateArray()
                .Where(item => item.TryGetProperty(TmdbProtocol.Adult, out var adult) && adult.ValueKind == JsonValueKind.True)
                .Select(item => MapAdultMovie(serviceId, item))
                .Where(result => !string.IsNullOrWhiteSpace(result.ExternalId) && !string.IsNullOrWhiteSpace(result.Title))
                .ToArray();
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            return [];
        }
    }

    private static RequestSearchResult MapAdultMovie(Guid serviceId, JsonElement item) =>
        new(serviceId,
            RequestProviderKind.Radarr,
            RequestMediaKind.Movie,
            Text(item, TmdbProtocol.Id) ?? string.Empty,
            Text(item, TmdbProtocol.Title) ?? string.Empty,
            null,
            YearFromDate(Text(item, TmdbProtocol.ReleaseDate)),
            Text(item, TmdbProtocol.Overview),
            ImageUrl(TmdbProtocol.PosterImageBase, Text(item, TmdbProtocol.PosterPath)),
            ImageUrl(TmdbProtocol.BackdropImageBase, Text(item, TmdbProtocol.BackdropPath)),
            Rating(item),
            null,
            AdultCertifications.Implied,
            null,
            [],
            false,
            null,
            null,
            true);

    private static string? ImageUrl(string baseUrl, string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : baseUrl + path;

    private static string? Text(JsonElement item, string name) {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value)) {
            return null;
        }

        return value.ValueKind switch {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static decimal? Rating(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty(TmdbProtocol.VoteAverage, out var value) &&
        value.TryGetDecimal(out var rating) && rating > 0
            ? rating
            : null;

    private static int? YearFromDate(string? value) =>
        DateTimeOffset.TryParse(value, out var date) ? date.Year : null;
}
