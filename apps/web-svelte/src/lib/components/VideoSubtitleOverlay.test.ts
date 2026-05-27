import { render, screen } from "@testing-library/svelte";
import { describe, expect, it } from "vitest";
import VideoSubtitleOverlay from "./VideoSubtitleOverlay.svelte";
import { defaultSubtitleAppearance } from "$lib/player/subtitle-appearance";

describe("VideoSubtitleOverlay", () => {
  it("renders styled text cues when text subtitles are active", () => {
    render(VideoSubtitleOverlay, {
      props: {
        activeCueText: "Line one\nLine two",
        appearance: {
          ...defaultSubtitleAppearance,
          fontScale: 1.25,
          positionPercent: 82,
          opacity: 0.75,
        },
        showTextCue: true,
      },
    });

    const caption = screen.getByText(/Line one/);
    expect(caption).toHaveClass("video-caption-stylized");
    expect(caption).toHaveStyle({ fontSize: "1.3125rem" });
  });

  it("suppresses text cues while an ASS renderer owns the active subtitle", () => {
    render(VideoSubtitleOverlay, {
      props: {
        activeCueText: "Hidden caption",
        appearance: defaultSubtitleAppearance,
        showTextCue: false,
      },
    });

    expect(screen.queryByText("Hidden caption")).toBeNull();
  });
});
