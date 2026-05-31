using Prismedia.Contracts.Entities;
using Prismedia.Contracts.Movies;

namespace Prismedia.Api.Endpoints;

/// <summary>Maps movie catalog endpoints for first-class movie entities.</summary>
public static class MovieEndpoints {
    /// <summary>Registers list and detail routes for movies.</summary>
    public static RouteGroupBuilder MapMovieEndpoints(this IEndpointRouteBuilder routes) =>
        routes.MapEntityKindRoutes(
            "/api/movies",
            "movie",
            "Movies",
            "ListMovies",
            "GetMovie",
            typeof(EntityListResponse),
            typeof(MovieDetail));
}
