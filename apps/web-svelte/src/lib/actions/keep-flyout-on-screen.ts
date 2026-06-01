export interface FlyoutViewportRect {
  left: number;
  right: number;
  width: number;
}

export interface FlyoutViewportBounds {
  width: number;
  gutter?: number;
}

export interface KeepFlyoutOnScreenOptions {
  gutter?: number;
}

/**
 * Returns the horizontal translation needed to keep a flyout inside the layout viewport.
 *
 * @param rect Current flyout bounds from `getBoundingClientRect()`.
 * @param bounds Viewport width plus the desired edge gutter.
 * @returns CSS px to translate the flyout on the X axis.
 */
export function getViewportConstrainedShift(
  rect: FlyoutViewportRect,
  { width, gutter = 12 }: FlyoutViewportBounds,
): number {
  const safeGutter = Math.max(0, gutter);
  const safeWidth = Math.max(0, width);
  const availableWidth = Math.max(0, safeWidth - safeGutter * 2);

  if (rect.width >= availableWidth) {
    return safeGutter - rect.left;
  }

  if (rect.left < safeGutter) {
    return safeGutter - rect.left;
  }

  const maxRight = safeWidth - safeGutter;
  if (rect.right > maxRight) {
    return maxRight - rect.right;
  }

  return 0;
}

/**
 * Svelte action that nudges an anchored flyout horizontally back inside the viewport.
 */
export function keepFlyoutOnScreen(
  node: HTMLElement,
  options: KeepFlyoutOnScreenOptions = {},
): { destroy: () => void; update: (nextOptions?: KeepFlyoutOnScreenOptions) => void } {
  let currentOptions = options;
  let frame = 0;
  let resizeObserver: ResizeObserver | undefined;

  function gutter(): number {
    return currentOptions.gutter ?? 12;
  }

  function viewportWidth(): number {
    return window.visualViewport?.width ?? window.innerWidth;
  }

  function applyLayout() {
    frame = 0;
    const edgeGutter = gutter();
    node.style.boxSizing = "border-box";
    node.style.maxWidth = `calc(100vw - ${edgeGutter * 2}px)`;
    node.style.transform = "";

    const shift = getViewportConstrainedShift(node.getBoundingClientRect(), {
      width: viewportWidth(),
      gutter: edgeGutter,
    });
    node.style.transform = shift === 0 ? "" : `translateX(${Math.round(shift)}px)`;
  }

  function schedule() {
    if (frame) return;
    frame = window.requestAnimationFrame(applyLayout);
  }

  schedule();
  window.addEventListener("resize", schedule);
  window.addEventListener("scroll", schedule, { capture: true, passive: true });
  window.visualViewport?.addEventListener("resize", schedule);
  window.visualViewport?.addEventListener("scroll", schedule);

  if (typeof ResizeObserver !== "undefined") {
    resizeObserver = new ResizeObserver(schedule);
    resizeObserver.observe(node);
  }

  return {
    update(nextOptions: KeepFlyoutOnScreenOptions = {}) {
      currentOptions = nextOptions;
      schedule();
    },
    destroy() {
      if (frame) window.cancelAnimationFrame(frame);
      window.removeEventListener("resize", schedule);
      window.removeEventListener("scroll", schedule, { capture: true });
      window.visualViewport?.removeEventListener("resize", schedule);
      window.visualViewport?.removeEventListener("scroll", schedule);
      resizeObserver?.disconnect();
    },
  };
}
