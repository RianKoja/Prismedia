using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Contracts.Plugins;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Jobs.Handlers;

/// <summary>
/// Identifies multiple entities via a provider plugin, storing each result in the durable identify queue.
/// </summary>
public sealed class BulkIdentifyJobHandler(
    IBulkIdentifyProvider provider,
    ILogger<BulkIdentifyJobHandler> logger) : IJobHandler {
    public JobType Type => JobType.BulkIdentify;

    public async Task HandleAsync(JobContext context, CancellationToken cancellationToken) {
        var payload = BulkIdentifyPayload.Parse(context.Job.PayloadJson);
        var count = payload.EntityIds.Count;
        logger.LogInformation("BulkIdentify: starting {Count} entities with provider {Provider}", count, payload.Provider);

        for (var i = 0; i < count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            var entityId = payload.EntityIds[i];

            try {
                await provider.SearchAndQueueAsync(entityId, payload.Provider, payload.Query, payload.HideNsfw, cancellationToken);
            } catch (Exception ex) {
                logger.LogWarning(ex, "BulkIdentify: failed to identify entity {EntityId}", entityId);
            }

            await context.ReportProgressAsync((i + 1) * 100 / count, $"Identified {i + 1}/{count}", cancellationToken);
        }

        logger.LogInformation("BulkIdentify: completed {Count} entities", count);
    }
}

public sealed record BulkIdentifyPayload(
    IReadOnlyList<Guid> EntityIds,
    string Provider,
    IdentifyQuery? Query,
    bool HideNsfw) {
    public string ToJson() => JsonSerializer.Serialize(this);

    public static BulkIdentifyPayload Parse(string payloadJson) =>
        JsonSerializer.Deserialize<BulkIdentifyPayload>(payloadJson)
            ?? throw new InvalidOperationException("BulkIdentify payload is missing or invalid.");
}
