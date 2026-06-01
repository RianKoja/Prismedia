using PDFtoImage;
using Prismedia.Application.Media;
using Prismedia.Infrastructure.Media.Processing;
using UglyToad.PdfPig;

namespace Prismedia.Infrastructure.Media.Books;

/// <summary>
/// Renders PDF pages to cached JPEGs (PDFtoImage) and reports page counts (PdfPig) so the
/// page-based reader can show a PDF like a comic, with stable per-page image URLs.
/// </summary>
public sealed class PdfPageRenderer(AssetPathService paths) : IPdfPageRenderer {
    // Wide enough to stay crisp on large displays; the reader scales it down to fit.
    private const int PageWidth = 1600;

    /// <inheritdoc />
    public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken) {
        try {
            using var document = PdfDocument.Open(pdfPath);
            return Task.FromResult(document.NumberOfPages);
        } catch {
            return Task.FromResult(0);
        }
    }

    /// <inheritdoc />
    public Task<string?> RenderPageAsync(string pdfPath, Guid bookId, int pageIndex, CancellationToken cancellationToken) {
        if (pageIndex < 0) {
            return Task.FromResult<string?>(null);
        }

        var outputPath = paths.BookPdfPagePath(bookId, pageIndex);
        try {
            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0) {
                return Task.FromResult<string?>(outputPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using (var pdfStream = File.OpenRead(pdfPath))
            using (var outputStream = File.Create(outputPath)) {
                Conversion.SaveJpeg(outputStream, pdfStream, leaveOpen: true, password: null, page: pageIndex,
                    options: new RenderOptions { Width = PageWidth, WithAspectRatio = true });
            }

            return Task.FromResult<string?>(new FileInfo(outputPath).Length > 0 ? outputPath : null);
        } catch {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { /* ignore */ }
            return Task.FromResult<string?>(null);
        }
    }
}
