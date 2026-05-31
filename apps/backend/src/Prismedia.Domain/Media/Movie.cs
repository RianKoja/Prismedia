using Prismedia.Domain.Capabilities;
using Prismedia.Domain.Entities;

namespace Prismedia.Domain.Media;

/// <summary>
/// Domain model for a single-film video release with one playable video child.
/// </summary>
public sealed class Movie : Entity {
    /// <summary>
    /// Creates a movie aggregate around one or more playable video children.
    /// </summary>
    /// <param name="id">Stable entity identifier.</param>
    /// <param name="title">Display title for the movie release.</param>
    /// <param name="videos">Playable video children that belong to this movie.</param>
    /// <param name="capabilities">Optional capability overrides loaded from persistence.</param>
    public Movie(
        Guid id,
        string title,
        IEnumerable<Entity>? videos = null,
        IEnumerable<EntityCapability>? capabilities = null)
        : base(id, title, capabilities) {
        foreach (var video in videos ?? []) {
            AddChild(video);
        }
    }

    public override EntityKind Kind => EntityKind.Movie;

    /// <summary>Playable video files that make up this movie release.</summary>
    public IReadOnlyList<Entity> Videos => ChildrenOf(EntityKind.Video);

    protected override IEnumerable<EntityCapability> CreateDefaultCapabilities() =>
    [
        new CapabilityDescription(),
        new CapabilityDates(),
        new CapabilitySource(),
        new CapabilityCredits()
    ];
}
