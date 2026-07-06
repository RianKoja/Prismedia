import { describe, expect, it } from "vitest";
import { initials } from "./initials";

describe("initials", () => {
  it("uses the first letters of two display-name words", () => {
    expect(initials("Paul Davis")).toBe("PD");
  });

  it("uses the first two letters of a single word", () => {
    expect(initials("Prismedia")).toBe("PR");
  });

  it("falls back to the username when the display name is empty", () => {
    expect(initials("", "paul")).toBe("PA");
    expect(initials(null, "paul")).toBe("PA");
  });

  it("returns a placeholder for degenerate input", () => {
    expect(initials("", "")).toBe("?");
    expect(initials(undefined, undefined)).toBe("?");
  });
});
