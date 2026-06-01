namespace Prismedia.Application.Media;

/// <summary>
/// Renders pages of a PDF book to images so the page-based reader can display them like comic
/// pages. Implementations cache rendered pages on disk and reuse them across requests.
/// </summary>
public interface IPdfPageRenderer {
    /// <summary>Returns the number of pages in the PDF, or 0 when it cannot be read.</summary>
    /// <param name="pdfPath">Absolute path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken);

    /// <summary>
    /// Renders a single zero-based page to a cached JPEG and returns its disk path, or null when
    /// the page cannot be rendered. Re-renders are served from cache.
    /// </summary>
    /// <param name="pdfPath">Absolute path to the PDF file.</param>
    /// <param name="bookId">Owning book entity id, used to key the page cache.</param>
    /// <param name="pageIndex">Zero-based page index to render.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> RenderPageAsync(string pdfPath, Guid bookId, int pageIndex, CancellationToken cancellationToken);
}
