using Prismedia.Application.Audio;
using Prismedia.Application.Entities;
using Prismedia.Contracts.Entities;
using Prismedia.Contracts.Media;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

public static class AudioTrackEndpoints {
    public static RouteGroupBuilder MapAudioTrackEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapEntityKindRoutes(
            "/api/audio-tracks",
            "audio-track",
            "Audio",
            "ListAudioTracks",
            "GetAudioTrack",
            typeof(EntityListResponse),
            typeof(AudioTrackDetail));

        group.MapPost("/{id:guid}/play", async (
            Guid id,
            EntityCapabilityService capabilities,
            CancellationToken cancellationToken) =>
            EntityEndpointResults.ToResult(id, await capabilities.UpdatePlaybackAsync(
                id, resumeSeconds: 0, durationSeconds: null, completed: true, cancellationToken)))
            .WithName("RecordAudioTrackPlay")
            .WithTags("Audio")
            .Produces<EntityCard>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        routes.MapGet("/api/audio-stream/{id:guid}", StreamAudioAsync)
            .WithName("GetAudioStream")
            .WithTags("Audio")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status206PartialContent)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        routes.MapMethods("/api/audio-stream/{id:guid}", [HttpMethods.Head], StreamAudioAsync)
            .ExcludeFromDescription();

        return group;
    }

    private static async Task<IResult> StreamAudioAsync(
        Guid id,
        IAudioSourceService sourceFiles,
        CancellationToken cancellationToken) {
        var source = await sourceFiles.GetSourceAsync(id, cancellationToken);
        if (source is null) {
            return Results.NotFound(new ApiProblem("audio_stream_not_found", $"Audio stream '{id}' was not found."));
        }

        return Results.File(File.OpenRead(source.Path), source.ContentType, enableRangeProcessing: true);
    }
}
