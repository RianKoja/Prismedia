using Prismedia.Contracts.Plugins;

namespace Prismedia.Application.Jobs.Ports;

/// <summary>
/// Port for bulk identify operations that search a provider and persist results to the durable queue.
/// </summary>
public interface IBulkIdentifyProvider {
    Task SearchAndQueueAsync(Guid entityId, string provider, IdentifyQuery? query, bool hideNsfw, CancellationToken cancellationToken);
}
