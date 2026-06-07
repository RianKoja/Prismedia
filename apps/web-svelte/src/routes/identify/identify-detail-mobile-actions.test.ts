import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

describe("identify detail mobile actions", () => {
  it("keeps Back to Search visible on mobile proposal reviews and desktop", async () => {
    const source = await readFile("src/routes/identify/[entityId]/+page.svelte", "utf8");

    expect(source).toContain("const backToSearchBusy = $derived(searching || store.identifyingId === current?.entityId);");
    expect(source).toMatch(
      /class="[^"]*inline-flex[^"]*h-10[^"]*w-full[^"]*md:hidden[^"]*"[\s\S]*?disabled=\{backToSearchBusy\}[\s\S]*?onclick=\{backToSearch\}[\s\S]*?Back to Search/,
    );
    expect(source).toMatch(
      /class="[^"]*hidden[^"]*md:inline-flex[^"]*"[\s\S]*?disabled=\{backToSearchBusy\}[\s\S]*?onclick=\{backToSearch\}[\s\S]*?Back to Search/,
    );
  });
});
