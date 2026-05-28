export function waveformForDisplay(waveform: number[]): number[] | null {
  const pairCount = Math.floor(waveform.length / 2);
  if (pairCount <= 0) return null;
  return waveform;
}
