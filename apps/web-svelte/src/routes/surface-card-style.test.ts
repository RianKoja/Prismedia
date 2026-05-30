import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

describe("shared surface card styling", () => {
  it("uses a dark material recipe instead of blur-backed glass", async () => {
    const source = await readFile("src/app.css", "utf8");
    const rule = source.match(/\.surface-card\s*\{[\s\S]*?\n  \}/)?.[0] ?? "";

    expect(rule).toContain("color-mix(in srgb, var(--color-surface-2) 58%, var(--color-surface-1) 42%)");
    expect(rule).toContain("inset 0 -1px 0 rgba(0,0,0,0.35)");
    expect(rule).toContain("0 8px 24px rgba(0,0,0,0.42)");
    expect(rule).not.toContain("backdrop-filter");
  });
});
