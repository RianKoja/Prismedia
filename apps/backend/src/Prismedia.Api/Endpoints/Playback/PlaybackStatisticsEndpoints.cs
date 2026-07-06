using Prismedia.Api.Security;
using Prismedia.Application.Playback;
using Prismedia.Contracts.Playback;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Endpoints;

internal static class PlaybackStatisticsEndpoints {
    internal static IEndpointRouteBuilder MapPlaybackStatisticsEndpoints(this IEndpointRouteBuilder routes) {
        routes.MapGet("/api/playback/statistics", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? kind,
            string? eventKind,
            bool? hideNsfw,
            bool? allUsers,
            HttpContext httpContext,
            IPlaybackStatisticsService statistics,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) => {
                if (!TryDecodeOptional<EntityKind>(kind, out var decodedKind)) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.InvalidEntityKind, $"Entity kind '{kind}' is not recognized."));
                }

                if (!TryDecodeOptional<PlaybackEventKind>(eventKind, out var decodedEventKind)) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.InvalidPlaybackEventKind, $"Playback event kind '{eventKind}' is not recognized."));
                }

                var upper = to ?? timeProvider.GetUtcNow();
                var lower = from ?? upper.AddDays(-365);
                if (lower >= upper) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.InvalidPlaybackStatisticsWindow, "The statistics start time must be before the end time."));
                }

                var response = await statistics.GetAsync(
                    new PlaybackStatisticsQuery(
                        lower,
                        upper,
                        decodedKind,
                        decodedEventKind,
                        NsfwVisibility.ShouldHide(hideNsfw, httpContext),
                        AllUsers: allUsers == true &&
                            httpContext.GetCurrentUser() is { Role: UserRole.Admin }),
                    cancellationToken);

                return Results.Ok(response);
            })
            .WithName("GetPlaybackStatistics")
            .WithSummary("Get Playback Statistics.")
            .Produces<PlaybackStatisticsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static bool TryDecodeOptional<TValue>(string? code, out TValue? value)
        where TValue : struct, Enum {
        value = null;
        if (string.IsNullOrWhiteSpace(code)) {
            return true;
        }

        if (code.Trim().TryDecodeAs<TValue>(out var decoded)) {
            value = decoded;
            return true;
        }

        return false;
    }
}
