import { describe, expect, it } from "vitest";
import { waveformForDisplay } from "./audio-waveform";

describe("audio waveform display helpers", () => {
  it("keeps signed waveform caches unchanged", () => {
    const signed = [-10, 12, -20, 30, -15, 18, -8, 9];

    expect(waveformForDisplay(signed)).toBe(signed);
  });

  it("rejects empty waveform payloads", () => {
    expect(waveformForDisplay([])).toBeNull();
  });
});
