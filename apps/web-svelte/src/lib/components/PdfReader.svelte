<script lang="ts">
  import { onMount, tick } from "svelte";
  import { AlertTriangle, List, X } from "@lucide/svelte";
  import { apiAssetUrl as toApiUrl } from "$lib/api/orval-fetch";
  import ReaderShell from "$lib/components/reader/ReaderShell.svelte";

  interface TocEntry {
    label: string;
    pageIndex: number | null;
    subitems: TocEntry[];
  }

  interface Props {
    sourceUrl: string;
    title?: string;
    presentation?: "overlay" | "page";
    closeIcon?: "close" | "back";
    initialPage?: number;
    onClose: () => void;
    onPageChange?: (page: number, pageCount: number) => void;
  }

  let {
    sourceUrl,
    title = "Document",
    presentation = "overlay",
    closeIcon = "close",
    initialPage = 0,
    onClose,
    onPageChange,
  }: Props = $props();

  let shell = $state<ReturnType<typeof ReaderShell>>();
  let scrollEl = $state<HTMLDivElement>();
  let ready = $state(false);
  let errorMessage = $state<string | null>(null);
  let pageCount = $state(0);
  let currentPage = $state(0);
  let toc = $state.raw<TocEntry[]>([]);
  let tocOpen = $state(false);

  const hasToc = $derived(toc.length > 0);
  const pageIndexes = $derived(Array.from({ length: pageCount }, (_, i) => i));

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pdf: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pdfjsLib: any = null;
  let wrappers: HTMLDivElement[] = [];
  const rendered = new Set<number>();
  const rendering = new Set<number>();
  let renderScale = 1;
  let observer: IntersectionObserver | null = null;
  // Placeholder page dimensions (from page 1) so the scroll height — and therefore each page's
  // offsetTop — is correct before pages lazily render. Without this, resume can't locate a page.
  let baseWidth = 0;
  let baseHeight = 0;

  function computeScale(pageWidthPx: number): number {
    if (!scrollEl) return 1;
    // Fit the page width to the available column (capped so very wide pages stay readable).
    const available = Math.min(scrollEl.clientWidth - 24, 1100);
    return available > 0 && pageWidthPx > 0 ? available / pageWidthPx : 1;
  }

  async function renderPage(index: number) {
    if (!pdf || rendered.has(index) || rendering.has(index)) return;
    const wrapper = wrappers[index];
    if (!wrapper) return;
    rendering.add(index);
    try {
      const page = await pdf.getPage(index + 1);
      const unscaled = page.getViewport({ scale: 1 });
      if (!renderScale || renderScale === 1) renderScale = computeScale(unscaled.width);
      const cssViewport = page.getViewport({ scale: renderScale });
      const dpr = Math.min(globalThis.devicePixelRatio || 1, 2);
      const deviceViewport = page.getViewport({ scale: renderScale * dpr });

      wrapper.style.height = `${cssViewport.height}px`;
      wrapper.style.width = `${cssViewport.width}px`;

      const canvas = document.createElement("canvas");
      canvas.width = Math.ceil(deviceViewport.width);
      canvas.height = Math.ceil(deviceViewport.height);
      canvas.style.width = `${cssViewport.width}px`;
      canvas.style.height = `${cssViewport.height}px`;
      canvas.className = "pdf-canvas";
      const ctx = canvas.getContext("2d");
      await page.render({ canvasContext: ctx, viewport: deviceViewport }).promise;

      const textLayerDiv = document.createElement("div");
      textLayerDiv.className = "pdf-text-layer";
      textLayerDiv.style.width = `${cssViewport.width}px`;
      textLayerDiv.style.height = `${cssViewport.height}px`;
      textLayerDiv.style.setProperty("--scale-factor", String(renderScale));
      const textLayer = new pdfjsLib.TextLayer({
        textContentSource: page.streamTextContent(),
        container: textLayerDiv,
        viewport: cssViewport,
      });
      await textLayer.render();

      wrapper.replaceChildren(canvas, textLayerDiv);
      rendered.add(index);
    } catch {
      // leave the placeholder; it will retry if it scrolls back into view
    } finally {
      rendering.delete(index);
    }
  }

  function unloadPage(index: number) {
    if (!rendered.has(index)) return;
    const wrapper = wrappers[index];
    if (wrapper) wrapper.replaceChildren();
    rendered.delete(index);
  }

  function updateCurrentPage() {
    if (!scrollEl || pageCount === 0) return;
    const mid = scrollEl.scrollTop + scrollEl.clientHeight / 2;
    let next = currentPage;
    for (let i = 0; i < wrappers.length; i++) {
      const w = wrappers[i];
      if (w && w.offsetTop <= mid) next = i;
      else break;
    }
    if (next !== currentPage) {
      currentPage = next;
      onPageChange?.(currentPage, pageCount);
    }
  }

  let scrollRaf = 0;
  function handleScroll() {
    shell?.showControls();
    if (scrollRaf) return;
    scrollRaf = requestAnimationFrame(() => {
      scrollRaf = 0;
      updateCurrentPage();
    });
  }

  function scrollToPage(index: number, behavior: ScrollBehavior = "smooth") {
    const wrapper = wrappers[Math.max(0, Math.min(index, wrappers.length - 1))];
    if (wrapper && scrollEl) {
      scrollEl.scrollTo({ top: wrapper.offsetTop - 8, behavior });
    }
  }

  function goPrev() {
    scrollToPage(currentPage - 1);
  }

  function goNext() {
    scrollToPage(currentPage + 1);
  }

  function normalizeOutline(items: unknown, resolve: (dest: unknown) => Promise<number | null>): Promise<TocEntry[]> {
    if (!Array.isArray(items)) return Promise.resolve([]);
    return Promise.all(
      items.map(async (item) => {
        const entry = item as { title?: unknown; dest?: unknown; items?: unknown };
        const label = typeof entry.title === "string" ? entry.title.trim() : "";
        const pageIndex = await resolve(entry.dest).catch(() => null);
        const subitems = await normalizeOutline(entry.items, resolve);
        return { label, pageIndex, subitems };
      }),
    ).then((entries) => entries.filter((e) => e.label.length > 0 || e.subitems.length > 0));
  }

  function openToc(entry: TocEntry) {
    if (entry.pageIndex === null) return;
    tocOpen = false;
    scrollToPage(entry.pageIndex);
  }

  onMount(() => {
    let disposed = false;

    void (async () => {
      try {
        pdfjsLib = await import("pdfjs-dist/build/pdf.mjs");
        const workerModule = await import("pdfjs-dist/build/pdf.worker.min.mjs?url");
        pdfjsLib.GlobalWorkerOptions.workerSrc = (workerModule as { default: string }).default;

        const absoluteUrl = toApiUrl(sourceUrl) ?? sourceUrl;
        const response = await fetch(absoluteUrl);
        if (!response.ok) throw new Error(`Failed to load PDF (${response.status})`);
        const data = await response.arrayBuffer();
        if (disposed) return;

        pdf = await pdfjsLib.getDocument({ data }).promise;
        if (disposed) return;
        pageCount = pdf.numPages;

        // Size placeholders from the first page so the scroll height (and each page's offsetTop)
        // is correct before pages lazily render — required for resume and the scrollbar.
        try {
          const first = await pdf.getPage(1);
          const vp = first.getViewport({ scale: 1 });
          renderScale = computeScale(vp.width);
          baseWidth = vp.width * renderScale;
          baseHeight = vp.height * renderScale;
        } catch {
          baseWidth = scrollEl ? Math.min(scrollEl.clientWidth - 24, 1100) : 800;
          baseHeight = baseWidth * 1.414;
        }

        // Resolve the outline (chapters) to page indexes.
        try {
          const outline = await pdf.getOutline();
          if (outline?.length) {
            toc = await normalizeOutline(outline, async (dest) => {
              const explicit = typeof dest === "string" ? await pdf.getDestination(dest) : dest;
              if (!Array.isArray(explicit) || !explicit[0]) return null;
              return await pdf.getPageIndex(explicit[0]);
            });
          }
        } catch {
          /* outline is optional */
        }

        ready = true;
        await tick();
        if (disposed) return;

        // Size placeholders and observe them for lazy rendering.
        for (const wrapper of wrappers) {
          if (wrapper) {
            wrapper.style.width = `${baseWidth}px`;
            wrapper.style.height = `${baseHeight}px`;
          }
        }
        observer = new IntersectionObserver(
          (entries) => {
            for (const e of entries) {
              const index = Number((e.target as HTMLElement).dataset.pageIndex);
              if (Number.isNaN(index)) continue;
              if (e.isIntersecting) void renderPage(index);
              else unloadPage(index);
            }
          },
          { root: scrollEl, rootMargin: "200% 0px" },
        );
        for (const wrapper of wrappers) if (wrapper) observer.observe(wrapper);

        if (initialPage > 0) {
          currentPage = Math.min(initialPage, pageCount - 1);
          await tick();
          scrollToPage(currentPage, "auto");
        }
      } catch (err) {
        if (!disposed) errorMessage = err instanceof Error ? err.message : String(err);
      }
    })();

    return () => {
      disposed = true;
      observer?.disconnect();
      observer = null;
      if (scrollRaf) cancelAnimationFrame(scrollRaf);
      try {
        pdf?.destroy?.();
      } catch {
        /* ignore */
      }
      pdf = null;
    };
  });
</script>

<ReaderShell
  bind:this={shell}
  {title}
  {presentation}
  {closeIcon}
  {onClose}
  onPrev={goPrev}
  onNext={goNext}
  onActivate={goNext}
>
  {#snippet counter()}{pageCount > 0 ? `Page ${currentPage + 1} / ${pageCount}` : ""}{/snippet}

  {#snippet controls()}
    {#if hasToc}
      <button
        type="button"
        onclick={() => (tocOpen = true)}
        class="reader-mode-button"
        aria-label="Table of contents"
        title="Contents"
      >
        <List class="h-4 w-4" />
        <span class="hidden sm:inline">Contents</span>
      </button>
    {/if}
  {/snippet}

  <div class="pdf-stage" bind:this={scrollEl} onscroll={handleScroll}>
    {#if ready}
      <div class="pdf-pages">
        {#each pageIndexes as index (index)}
          <div class="pdf-page" data-page-index={index} bind:this={wrappers[index]}></div>
        {/each}
      </div>
    {/if}

    {#if errorMessage}
      <div class="pdf-message">
        <AlertTriangle class="h-5 w-5" />
        <p>{errorMessage}</p>
      </div>
    {:else if !ready}
      <div class="pdf-message">
        <p>Opening document…</p>
      </div>
    {/if}
  </div>
</ReaderShell>

{#if tocOpen}
  {@const closeToc = () => (tocOpen = false)}
  <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
  <div class="toc-overlay" onclick={closeToc}>
    <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
    <div class="toc-panel" role="dialog" aria-label="Table of contents" tabindex="-1" onclick={(e) => e.stopPropagation()}>
      <div class="toc-header">
        <span class="toc-title">Contents</span>
        <button type="button" class="reader-mode-button" onclick={closeToc} aria-label="Close contents" title="Close">
          <X class="h-4 w-4" />
        </button>
      </div>
      <nav class="toc-list">
        {@render tocItems(toc, 0)}
      </nav>
    </div>
  </div>
{/if}

{#snippet tocItems(items: TocEntry[], depth: number)}
  {#each items as entry (entry.label + (entry.pageIndex ?? ""))}
    <button
      type="button"
      class="toc-item"
      style={`padding-left: ${0.85 + depth * 0.9}rem`}
      disabled={entry.pageIndex === null}
      onclick={() => openToc(entry)}
    >
      {entry.label}
    </button>
    {#if entry.subitems.length > 0}
      {@render tocItems(entry.subitems, depth + 1)}
    {/if}
  {/each}
{/snippet}

<style>
  .pdf-stage {
    position: absolute;
    inset: 0;
    overflow-y: auto;
    overflow-x: hidden;
    background: #0b0c0f;
    overscroll-behavior: contain;
  }

  .pdf-pages {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    padding: max(3.5rem, env(safe-area-inset-top)) 0 4rem;
  }

  .pdf-page {
    position: relative;
    background: #1a1c22;
    box-shadow: 0 0 24px rgba(0, 0, 0, 0.5);
  }

  :global(.pdf-canvas) {
    display: block;
  }

  /* pdf.js text layer: transparent, selectable text positioned over the canvas. */
  :global(.pdf-text-layer) {
    position: absolute;
    inset: 0;
    overflow: hidden;
    line-height: 1;
    text-size-adjust: none;
    forced-color-adjust: none;
    transform-origin: 0 0;
    z-index: 2;
  }

  :global(.pdf-text-layer span),
  :global(.pdf-text-layer br) {
    position: absolute;
    white-space: pre;
    color: transparent;
    cursor: text;
    transform-origin: 0% 0%;
  }

  :global(.pdf-text-layer ::selection) {
    background: rgba(242, 194, 106, 0.35);
  }

  .pdf-message {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    gap: 0.6rem;
    color: var(--color-text-secondary);
    text-align: center;
    pointer-events: none;
  }

  .reader-mode-button {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    border: 1px solid var(--color-border-default);
    background: var(--color-overlay-heavy);
    padding: 0.45rem 0.65rem;
    border-radius: var(--radius-sm);
    color: var(--color-text-secondary);
    font-size: 0.72rem;
    line-height: 1;
    backdrop-filter: blur(var(--glass-blur-sm));
    transition:
      border-color var(--duration-normal) var(--ease-mechanical),
      color var(--duration-normal) var(--ease-mechanical),
      box-shadow var(--duration-normal) var(--ease-mechanical);
  }

  .reader-mode-button:hover,
  .reader-mode-button:focus-visible {
    border-color: var(--color-border-accent-strong);
    color: var(--color-text-accent-bright);
    box-shadow: var(--shadow-glow-accent);
    outline: none;
  }

  .toc-overlay {
    position: fixed;
    inset: 0;
    z-index: 2147483100;
    display: flex;
    justify-content: flex-start;
    background: rgba(4, 5, 7, 0.55);
    backdrop-filter: blur(var(--glass-blur-sm));
  }

  .toc-panel {
    display: flex;
    width: min(22rem, 86vw);
    height: 100%;
    flex-direction: column;
    border-right: 1px solid var(--color-border-default);
    background: var(--color-surface-1, #0e1014);
    box-shadow: var(--shadow-glow-accent);
  }

  .toc-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    padding: max(0.75rem, env(safe-area-inset-top)) 0.85rem 0.75rem;
    border-bottom: 1px solid var(--color-border-default);
  }

  .toc-title {
    font-family: var(--font-mono, monospace);
    font-size: 0.68rem;
    letter-spacing: 0.16em;
    text-transform: uppercase;
    color: var(--color-text-accent-bright);
  }

  .toc-list {
    flex: 1 1 auto;
    overflow-y: auto;
    padding: 0.4rem 0.4rem 1.5rem;
  }

  .toc-item {
    display: block;
    width: 100%;
    border-radius: var(--radius-sm);
    padding: 0.5rem 0.85rem;
    text-align: left;
    font-size: 0.82rem;
    color: var(--color-text-secondary);
  }

  .toc-item:hover:not(:disabled),
  .toc-item:focus-visible {
    background: var(--color-overlay-heavy);
    color: var(--color-text-accent-bright);
    outline: none;
  }

  .toc-item:disabled {
    color: var(--color-text-muted);
    cursor: default;
  }
</style>
