import { describe, expect, it } from "vitest";
import { getViewportConstrainedShift } from "./keep-flyout-on-screen";

describe("keep flyout on screen", () => {
  it("leaves a flyout alone when it already fits", () => {
    expect(
      getViewportConstrainedShift({ left: 48, right: 248, width: 200 }, { width: 360, gutter: 12 }),
    ).toBe(0);
  });

  it("moves a flyout right when it bleeds past the left edge", () => {
    expect(
      getViewportConstrainedShift({ left: -24, right: 184, width: 208 }, { width: 360, gutter: 12 }),
    ).toBe(36);
  });

  it("moves a flyout left when it bleeds past the right edge", () => {
    expect(
      getViewportConstrainedShift({ left: 208, right: 416, width: 208 }, { width: 360, gutter: 12 }),
    ).toBe(-68);
  });

  it("pins oversized flyouts to the leading gutter", () => {
    expect(
      getViewportConstrainedShift({ left: -60, right: 340, width: 400 }, { width: 360, gutter: 12 }),
    ).toBe(72);
  });
});
