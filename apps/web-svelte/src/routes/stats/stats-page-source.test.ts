import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";

const source = readFileSync("src/routes/stats/+page.svelte", "utf8");

describe("stats page source", () => {
  it("uses the shared playback statistics API and generated code constants", () => {
    expect(source).toContain("fetchPlaybackStatistics");
    expect(source).toContain("PLAYBACK_EVENT_KIND");
    expect(source).toContain("ENTITY_KIND.");
  });

  it("renders playback thumbnails through the shared EntityThumbnail component", () => {
    expect(source).toContain("EntityThumbnail");
    expect(source).toContain("entityReferenceToThumbnailCard");
    expect(source).not.toContain("{#snippet entityArtwork");
  });
});
