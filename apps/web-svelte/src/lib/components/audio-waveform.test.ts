import { describe, expect, it } from "vitest";
import {
  isLegacyPositiveOnlyWaveform,
  waveformDisplayScale,
  waveformForDisplay,
} from "./audio-waveform";

describe("audio waveform display helpers", () => {
  it("uses a percentile scale so isolated bad peaks do not flatten the waveform", () => {
    const waveform = [
      -100, 100,
      -110, 120,
      -90, 105,
      -95, 100,
      -100, 115,
      -32768, 0,
    ];

    expect(waveformDisplayScale(waveform)).toBeLessThan(32768);
    expect(waveformDisplayScale(waveform)).toBeGreaterThanOrEqual(120);
  });

  it("falls back to one for silent or empty waveforms", () => {
    expect(waveformDisplayScale([])).toBe(1);
    expect(waveformDisplayScale([0, 0, 0, 0])).toBe(1);
  });

  it("mirrors stale positive-only waveform caches for display", () => {
    const stale = [0, 16191, 0, 16255, 0, 32575, 0, 32575];

    expect(isLegacyPositiveOnlyWaveform(stale)).toBe(true);
    expect(waveformForDisplay(stale)).toEqual([
      -16191, 16191,
      -16255, 16255,
      -32575, 32575,
      -32575, 32575,
    ]);
  });

  it("keeps signed waveform caches unchanged", () => {
    const signed = [-10, 12, -20, 30, -15, 18, -8, 9];

    expect(isLegacyPositiveOnlyWaveform(signed)).toBe(false);
    expect(waveformForDisplay(signed)).toBe(signed);
  });
});
