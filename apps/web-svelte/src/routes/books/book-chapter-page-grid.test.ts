import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

describe("book chapter page grid", () => {
  it("defaults page thumbnails to media wall mode while keeping metadata recoverable", async () => {
    const source = await readFile("src/routes/books/[id]/chapters/[chapterId]/+page.svelte", "utf8");
    const thumbnailSource = await readFile("src/lib/components/thumbnails/EntityThumbnail.svelte", "utf8");

    expect(source).toContain('prefsKey={`book-${book.id}-chapter-${chapter.id}-pages`}');
    expect(source).toContain("initialMediaWall");
    expect(thumbnailSource).not.toContain("card.entity.kind === ENTITY_KIND.bookPage");
  });
});
