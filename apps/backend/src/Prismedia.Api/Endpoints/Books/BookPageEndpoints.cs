using Prismedia.Application.Entities;
using Prismedia.Application.Media;
using Prismedia.Contracts.Media;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

/// <summary>
/// Serves PDF books as page images so the page-based reader can display them like a comic:
/// a count endpoint plus an on-demand, disk-cached per-page renderer.
/// </summary>
internal static class BookPageEndpoints {
    internal static IEndpointRouteBuilder MapBookPageEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/books");

        group.MapGet("/{id:guid}/pages", async (
            Guid id,
            IEntityFileContentService files,
            IPdfPageRenderer renderer,
            CancellationToken cancellationToken) => {
            var source = await files.GetContentAsync(id, "source", cancellationToken);
            if (source is null || !File.Exists(source.Path)) {
                return Results.NotFound(new ApiProblem("book_source_not_found", $"Source file for book '{id}' was not found."));
            }

            var count = await renderer.GetPageCountAsync(source.Path, cancellationToken);
            return Results.Ok(new BookPageInfo(count));
        })
            .WithName("GetBookPageInfo")
            .WithSummary("Returns the page count for a PDF book.")
            .Produces<BookPageInfo>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/pages/{index:int}", async (
            Guid id,
            int index,
            IEntityFileContentService files,
            IPdfPageRenderer renderer,
            CancellationToken cancellationToken) => {
            var source = await files.GetContentAsync(id, "source", cancellationToken);
            if (source is null || !File.Exists(source.Path)) {
                return Results.NotFound(new ApiProblem("book_source_not_found", $"Source file for book '{id}' was not found."));
            }

            var rendered = await renderer.RenderPageAsync(source.Path, id, index, cancellationToken);
            if (rendered is null || !File.Exists(rendered)) {
                return Results.NotFound(new ApiProblem("book_page_not_found", $"Page {index} for book '{id}' could not be rendered."));
            }

            return Results.File(File.OpenRead(rendered), MediaContentTypes.ImageJpeg, enableRangeProcessing: true);
        })
            .WithName("GetBookPageImage")
            .WithSummary("Renders and serves a single PDF book page as an image.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        return routes;
    }
}

/// <summary>Page-count response for a PDF book.</summary>
/// <param name="Count">Total number of pages.</param>
public sealed record BookPageInfo(int Count);
