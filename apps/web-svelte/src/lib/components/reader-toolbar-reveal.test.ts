import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

/**
 * APP-139: the reader toolbar must not pop up on every page turn or while scrolling.
 * Navigation should leave the chrome alone; only a centre tap toggles it. The PdfReader
 * and BookFileReader can't be rendered in jsdom (pdf.js / foliate dynamic imports), so
 * these guard the source against re-introducing the navigation-reveal regression.
 */
describe("reader toolbar reveal", () => {
  it("does not force the EPUB toolbar open on page turns", async () => {
    const src = await readFile("src/lib/components/BookFileReader.svelte", "utf8");
    // goPrev/goNext only drive foliate now; they no longer reveal the chrome.
    expect(src).not.toContain("showControls");
    // A centre tap still toggles via the gesture overlay.
    expect(src).toContain("shell?.toggleControls()");
  });

  it("does not force the PDF toolbar open on page turns or while scrolling", async () => {
    const src = await readFile("src/lib/components/PdfReader.svelte", "utf8");
    // No navigation or scroll handler reveals the chrome.
    expect(src).not.toContain("showControls");
    // A clean tap toggles the chrome in scrolled mode too (no page turns there).
    expect(src).toContain('if (flow !== "paged") {');
    expect(src).toContain("shell?.toggleControls()");
  });
});
