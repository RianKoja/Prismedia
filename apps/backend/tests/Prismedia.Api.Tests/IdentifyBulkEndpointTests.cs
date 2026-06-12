using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Prismedia.Application.Plugins;
using Prismedia.Contracts.Plugins;
using Prismedia.Infrastructure.Serialization;

namespace Prismedia.Api.Tests;

public sealed class IdentifyBulkEndpointTests {
    private static readonly JsonSerializerOptions CodecJson =
        new(JsonSerializerDefaults.Web) { Converters = { new CodecJsonConverterFactory() } };

    [Fact]
    public async Task StartBulkIdentifyRequestsOneSearchPerEntity() {
        using var factory = CreateFactory();
        using var client = factory.CreateAuthenticatedClient();
        var entityIds = new[] {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("11111111-1111-1111-1111-111111111112"),
        };

        using var response = await client.PostAsJsonAsync(
            "/api/identify/bulk",
            new IdentifyBulkStartRequest("tmdb", entityIds, new IdentifyQuery("Hint", null, null)),
            CodecJson);
        var body = await response.Content.ReadFromJsonAsync<IdentifyBulkAcceptedResponse>(CodecJson);

        var queue = factory.Services.GetRequiredService<RecordingIdentifyQueueService>();
        var call = Assert.Single(queue.BatchRequests);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Requested);
        Assert.Equal(2, body.Enqueued);
        Assert.Equal(entityIds, call.EntityIds);
        Assert.Equal("tmdb", call.Request.Provider);
        Assert.Equal("Hint", call.Request.Query?.Title);
    }

    [Fact]
    public async Task StartBulkIdentifyRejectsEmptyEntityList() {
        using var factory = CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/api/identify/bulk",
            new IdentifyBulkStartRequest("tmdb", [], null),
            CodecJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<RecordingIdentifyQueueService>().BatchRequests);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    services.AddSingleton<RecordingIdentifyQueueService>();
                    services.AddScoped<IIdentifyQueueService>(provider =>
                        provider.GetRequiredService<RecordingIdentifyQueueService>());
                });
            })
            .WithTestAuth();

    private sealed class RecordingIdentifyQueueService : IIdentifyQueueService {
        public List<(IReadOnlyList<Guid> EntityIds, IdentifyQueueSearchRequest Request, bool HideNsfw)> BatchRequests { get; } = [];

        public Task<IdentifyBulkAcceptedResponse> RequestSearchBatchAsync(
            IReadOnlyList<Guid> entityIds,
            IdentifyQueueSearchRequest request,
            bool hideNsfw,
            CancellationToken cancellationToken) {
            BatchRequests.Add((entityIds, request, hideNsfw));
            return Task.FromResult(new IdentifyBulkAcceptedResponse(entityIds.Count, entityIds.Count));
        }

        public Task<IReadOnlyList<IdentifyQueueItem>> ListAsync(bool includeCompleted, bool hideNsfw, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem> AddAsync(Guid entityId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem?> GetAsync(Guid entityId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem> RequestSearchAsync(Guid entityId, IdentifyQueueSearchRequest request, bool hideNsfw, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem> ApplyAsync(Guid entityId, ApplyIdentifyQueueItemRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem> SaveProposalAsync(Guid entityId, EntityMetadataProposal proposal, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentifyQueueItem?> DeleteAsync(Guid entityId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
