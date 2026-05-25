using Prismedia.Application.Jobs.Ports;
using Prismedia.Contracts.Plugins;

namespace Prismedia.Infrastructure.Plugins;

/// <summary>
/// Adapts <see cref="IdentifyQueueService"/> for use by the bulk identify job handler.
/// Each call adds the entity to the durable queue and triggers a provider search.
/// </summary>
internal sealed class BulkIdentifyProviderAdapter(IdentifyQueueService queueService) : IBulkIdentifyProvider {
    public async Task SearchAndQueueAsync(
        Guid entityId,
        string provider,
        IdentifyQuery? query,
        bool hideNsfw,
        CancellationToken cancellationToken) {
        await queueService.SearchAsync(
            entityId,
            new IdentifyQueueSearchRequest(provider, query),
            hideNsfw,
            cancellationToken);
    }
}
