using Prismedia.Contracts.Entities;

namespace Prismedia.Contracts.Media;

/// <summary>
/// API-facing detail shape for a page-based book, comic, or manga entity.
/// </summary>
public sealed record BookDetail : EntityDetail {
    /// <summary>Book category code.</summary>
    public required string BookType { get; init; }

    /// <summary>Physical format code that selects reader and detail behavior.</summary>
    public required string Format { get; init; }

    /// <summary>Selected cover page entity, when one is set.</summary>
    public required Guid? CoverPageId { get; init; }
}
