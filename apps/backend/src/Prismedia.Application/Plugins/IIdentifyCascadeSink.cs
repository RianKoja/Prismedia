using Prismedia.Contracts.Plugins;

namespace Prismedia.Application.Plugins;

/// <summary>
/// Receives the growing root proposal during a full-tree identify cascade so partial results can be
/// streamed somewhere durable (e.g. persisted onto the queue item) while the cascade is still
/// walking. The cascade invokes <see cref="OnEntityResolvedAsync"/> after each top-level structural
/// child of the root resolves, always passing the same root <c>ProposalId</c> so the partial roots
/// can update one persisted proposal in place.
/// </summary>
public interface IIdentifyCascadeSink {
    /// <summary>
    /// Called with the current partial root proposal after a structural child has been resolved and
    /// merged into it. Implementations should be resilient and fast; a failure here must not abort
    /// the cascade.
    /// </summary>
    /// <param name="partialRoot">The root proposal with the children resolved so far.</param>
    /// <param name="cancellationToken">Cancellation token for the running cascade.</param>
    Task OnEntityResolvedAsync(EntityMetadataProposal partialRoot, CancellationToken cancellationToken);
}
