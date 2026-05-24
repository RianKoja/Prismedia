<script lang="ts">
  import { onDestroy, type Snippet } from "svelte";
  import {
    Album,
    BookOpen,
    Building2,
    Calendar,
    Clock3,
    Disc3,
    Film,
    Flame,
    FolderOpen,
    Hash,
    Image,
    Images,
    Layers,
    Music,
    Star,
    Tag,
    Users,
  } from "@lucide/svelte";
  import { getRatingValue, isNsfw } from "$lib/api/capabilities";
  import OverflowTicker from "$lib/components/OverflowTicker.svelte";
  import {
    getThumbnailAsset,
    hasHoverPreview,
    iconForKind,
    placeholderGradient,
    resolveEntityThumbnailHref,
    toAspectRatioNumeric,
    toAspectRatioValue,
    type EntityThumbnailCard,
    type EntityThumbnailMetaIcon,
  } from "$lib/entities/entity-thumbnail";
  import { ENTITY_KIND } from "$lib/entities/entity-codes";
  import { loadTrickplayFrames, type TrickplayFrame } from "@prismedia/ui-svelte";

  type EntityThumbnailTitleAlign = "left" | "center" | "right";
  type EntityThumbnailTitleSize = "default" | "compact";

  interface Props {
    card: EntityThumbnailCard;
    layout?: "grid" | "list";
    linkable?: boolean;
    mediaOnly?: boolean;
    hoverPreviewsEnabled?: boolean;
    onActivate?: (card: EntityThumbnailCard) => void;
    onSelectedChange?: (selected: boolean) => void;
    selectable?: boolean;
    selectMode?: boolean;
    selected?: boolean;
    subtitleContent?: Snippet<[EntityThumbnailCard]>;
    titleAlign?: EntityThumbnailTitleAlign;
    titleSize?: EntityThumbnailTitleSize;
  }

  let {
    card,
    layout = "grid",
    linkable = true,
    mediaOnly = false,
    hoverPreviewsEnabled = true,
    onActivate,
    onSelectedChange,
    selectable = false,
    selectMode = false,
    selected = false,
    subtitleContent,
    titleAlign = "left",
    titleSize = "default",
  }: Props = $props();

  let pointerRatio = $state<number | null>(null);
  let imageFailed = $state(false);
  let hoverBroken = $state(false);
  let lastSrc = $state<string | undefined>(undefined);
  let hoverIntentTimer: number | null = null;
  let latestPointerRatio = 0.5;
  let pointerScrubbing = false;
  let capturedPointerId: number | null = null;
  let scrubStartClientX = 0;
  let scrubStartClientY = 0;
  let scrubPointerType = "mouse";
  let suppressNextActivation = false;

  let spriteFrames = $state<TrickplayFrame[] | null>(null);
  let spriteError = $state(false);

  const isSpriteHover = $derived(card.hover.kind === "sprite");
  const isImageSequenceHover = $derived(card.hover.kind === "image-sequence");
  const sequenceAssets = $derived(card.hover.kind === "image-sequence" ? card.hover.assets : []);
  const asset = $derived(getThumbnailAsset(card, hoverBroken || isSpriteHover ? null : pointerRatio));
  const aspectRatio = $derived(toAspectRatioValue(card.aspectRatio));
  const aspectRatioNumeric = $derived(toAspectRatioNumeric(card.aspectRatio));
  const imageOnly = $derived(mediaOnly || card.entity.kind === ENTITY_KIND.bookPage);
  const containerAspectRatio = $derived(imageOnly ? undefined : `${aspectRatioNumeric * 3} / 4`);
  const imageFit = $derived(card.fit ?? "cover");
  const placeholderIcon = $derived(iconForKind(card.entity.kind));
  const sequenceRestCover = $derived(
    isImageSequenceHover && !card.cover && sequenceAssets.length > 0 ? sequenceAssets[0] : null,
  );
  const showPlaceholder = $derived(
    isSpriteHover ? !card.cover : sequenceRestCover ? false : !asset || imageFailed,
  );
  const gradient = $derived(placeholderGradient(card.entity.title));

  const activeSequenceIndex = $derived.by(() => {
    if (!isImageSequenceHover || hoverBroken || pointerRatio === null || sequenceAssets.length === 0) return -1;
    const clamped = Math.max(0, Math.min(1, pointerRatio));
    return Math.min(sequenceAssets.length - 1, Math.floor(clamped * sequenceAssets.length));
  });
  const activeSequenceAsset = $derived(
    activeSequenceIndex >= 0 ? sequenceAssets[activeSequenceIndex] ?? null : null,
  );

  const activeSpriteFrame = $derived.by(() => {
    if (!isSpriteHover || !spriteFrames || pointerRatio === null) return null;
    const clamped = Math.max(0, Math.min(1, pointerRatio));
    const idx = Math.min(spriteFrames.length - 1, Math.floor(clamped * spriteFrames.length));
    return spriteFrames[idx] ?? null;
  });

  const spriteDims = $derived.by(() => {
    if (!spriteFrames) return { width: 0, height: 0 };
    return {
      width: spriteFrames.reduce((max, f) => Math.max(max, f.x + f.width), 0),
      height: spriteFrames.reduce((max, f) => Math.max(max, f.y + f.height), 0),
    };
  });

  async function ensureSpriteLoaded() {
    if (!isSpriteHover || spriteFrames || spriteError) return;
    const hover = card.hover as { kind: "sprite"; spriteUrl?: string; vttUrl: string };
    try {
      if (hover.spriteUrl && typeof globalThis.Image !== "undefined") {
        const img = new globalThis.Image();
        img.src = hover.spriteUrl;
      }
      spriteFrames = await loadTrickplayFrames(hover.vttUrl);
    } catch (err) {
      console.warn("Failed to load thumbnail trickplay frames", err);
      spriteError = true;
    }
  }


  function clearHoverIntentTimer() {
    if (!hoverIntentTimer) return;
    window.clearTimeout(hoverIntentTimer);
    hoverIntentTimer = null;
  }

  function capturePointer(element: HTMLElement, pointerId: number) {
    element.setPointerCapture?.(pointerId);
    capturedPointerId = pointerId;
  }

  function releaseCapturedPointer(element: HTMLElement) {
    if (capturedPointerId === null) return;
    element.releasePointerCapture?.(capturedPointerId);
    capturedPointerId = null;
  }

  function activateHoverPreview() {
    if (!hoverPreviewsEnabled || !hoverable) return;
    pointerRatio = latestPointerRatio;
    void ensureSpriteLoaded();
  }

  $effect(() => {
    if (asset?.src !== lastSrc) {
      lastSrc = asset?.src;
      imageFailed = false;
    }
  });
  $effect(() => {
    if (!hoverPreviewsEnabled && pointerRatio !== null) {
      clearHover();
    }
  });
  const hoverable = $derived(hasHoverPreview(card) && !hoverBroken && !spriteError);
  const nsfw = $derived(isNsfw(card.entity.capabilities));
  const rating = $derived(getRatingValue(card.entity.capabilities));
  const bottomLeft = $derived(card.custom?.bottomLeft);
  const href = $derived(linkable ? resolveEntityThumbnailHref(card) : undefined);
  const inSelectMode = $derived(selectMode && selectable);
  const effectiveHref = $derived(inSelectMode ? undefined : href);
  const selectionRole = $derived(
    onActivate && !effectiveHref
      ? "button"
      : inSelectMode || (!href && selectable) ? "checkbox" : href ? undefined : "group",
  );
  const selectionTabIndex = $derived(effectiveHref ? undefined : 0);

  function updatePointerRatio(event: PointerEvent) {
    if (!hoverable) return;
    const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect();
    latestPointerRatio = bounds.width > 0 ? (event.clientX - bounds.left) / bounds.width : 0;
    if (pointerRatio !== null) pointerRatio = latestPointerRatio;
  }

  function handlePointerEnter(event: PointerEvent) {
    if (!hoverPreviewsEnabled) return;
    updatePointerRatio(event);
    clearHoverIntentTimer();
    hoverIntentTimer = window.setTimeout(() => {
      hoverIntentTimer = null;
      activateHoverPreview();
    }, 140);
  }

  function handlePointerMove(event: PointerEvent) {
    if (!hoverPreviewsEnabled) return;
    if (!pointerScrubbing && scrubPointerType === "touch") {
      const deltaX = event.clientX - scrubStartClientX;
      const deltaY = event.clientY - scrubStartClientY;
      if (Math.abs(deltaX) < 12 || Math.abs(deltaX) <= Math.abs(deltaY) * 1.25) return;
      pointerScrubbing = true;
      suppressNextActivation = true;
      capturePointer(event.currentTarget as HTMLElement, event.pointerId);
      updatePointerRatio(event);
      pointerRatio = latestPointerRatio;
      void ensureSpriteLoaded();
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (pointerScrubbing) {
      updatePointerRatio(event);
      if (Math.abs(event.clientX - scrubStartClientX) > 6) {
        suppressNextActivation = true;
      }
      event.preventDefault();
      event.stopPropagation();
      if (pointerRatio !== null) void ensureSpriteLoaded();
      return;
    }

    updatePointerRatio(event);
    if (pointerRatio !== null) void ensureSpriteLoaded();
  }

  function handlePointerDown(event: PointerEvent) {
    if (!hoverPreviewsEnabled || !hoverable) return;
    scrubStartClientX = event.clientX;
    scrubStartClientY = event.clientY;
    scrubPointerType = event.pointerType;
    pointerScrubbing = false;
    suppressNextActivation = false;
    clearHoverIntentTimer();
    if (event.pointerType === "touch") {
      return;
    }
    pointerScrubbing = true;
    updatePointerRatio(event);
    pointerRatio = latestPointerRatio;
    void ensureSpriteLoaded();
    capturePointer(event.currentTarget as HTMLElement, event.pointerId);
  }

  function handlePointerUp(event: PointerEvent) {
    if (!pointerScrubbing && scrubPointerType !== "touch") return;
    pointerScrubbing = false;
    scrubPointerType = "mouse";
    releaseCapturedPointer(event.currentTarget as HTMLElement);
  }

  function handlePointerCancel(event: PointerEvent) {
    releaseCapturedPointer(event.currentTarget as HTMLElement);
    clearHover();
  }

  function handlePointerLeave() {
    if (pointerScrubbing) return;
    clearHover();
  }

  function handleFocus() {
    if (!hoverPreviewsEnabled) return;
    pointerRatio = hoverable ? 0.5 : null;
    void ensureSpriteLoaded();
  }

  function clearHover() {
    clearHoverIntentTimer();
    pointerScrubbing = false;
    capturedPointerId = null;
    scrubPointerType = "mouse";
    pointerRatio = null;
  }

  function handleSelectionChange(event: Event) {
    const input = event.currentTarget as HTMLInputElement;
    onSelectedChange?.(input.checked);
  }

  function toggleSurfaceSelection() {
    if (!selectable) return;
    if (!inSelectMode && href) return;
    onSelectedChange?.(!selected);
  }

  function handleSurfaceClick(event: MouseEvent) {
    if (suppressNextActivation) {
      suppressNextActivation = false;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (onActivate && !effectiveHref) {
      onActivate(card);
      return;
    }

    toggleSurfaceSelection();
  }

  function handleSurfaceKeydown(event: KeyboardEvent) {
    if (event.key !== "Enter" && event.key !== " ") return;
    if (onActivate && !effectiveHref) {
      event.preventDefault();
      onActivate(card);
      return;
    }

    if (!selectable || (!inSelectMode && href)) return;
    event.preventDefault();
    toggleSurfaceSelection();
  }

  function stopSelectionActivation(event: Event) {
    event.stopPropagation();
  }

  function formatRating(value: number): string {
    if (value <= 0) return "";
    return String(Math.round(value));
  }

  onDestroy(() => {
    clearHoverIntentTimer();
  });
</script>

<svelte:element
  this={effectiveHref ? "a" : "article"}
  href={effectiveHref || undefined}
  role={selectionRole}
  tabindex={selectionTabIndex}
  class="entity-thumbnail"
  class:is-hovering={pointerRatio !== null}
  class:is-image-only={imageOnly}
  class:is-list={layout === "list"}
  class:is-selected={selected}
  class:is-select-mode={inSelectMode}
  style:aspect-ratio={layout === "list" ? undefined : containerAspectRatio ?? aspectRatio}
  aria-label={card.entity.title}
  aria-checked={!onActivate && (inSelectMode || (!href && selectable)) ? selected : undefined}
  onblur={clearHover}
  onclick={handleSurfaceClick}
  onfocus={handleFocus}
  onkeydown={handleSurfaceKeydown}
>
  <div
    class="media"
    class:has-placeholder={showPlaceholder}
    role="presentation"
    style:background={showPlaceholder ? gradient : undefined}
    onpointerenter={handlePointerEnter}
    onpointerdown={handlePointerDown}
    onpointermove={handlePointerMove}
    onpointerup={handlePointerUp}
    onpointercancel={handlePointerCancel}
    onpointerleave={handlePointerLeave}
  >
    {#if activeSequenceAsset}
      <img
        src={activeSequenceAsset.src}
        alt={activeSequenceAsset.alt}
        decoding="async"
        loading="lazy"
        style:object-fit={imageFit}
        onerror={() => {
          imageFailed = true;
          hoverBroken = true;
          clearHover();
        }}
      />
    {:else if sequenceRestCover}
      <img
        src={sequenceRestCover.src}
        alt={sequenceRestCover.alt}
        decoding="async"
        loading="lazy"
        style:object-fit={imageFit}
        onerror={() => { imageFailed = true; }}
      />
    {:else if isSpriteHover && card.cover}
      <img
        src={card.cover.src}
        alt={card.cover.alt}
        decoding="async"
        loading="lazy"
        style:object-fit={imageFit}
        class:sprite-active={activeSpriteFrame !== null}
        onerror={() => { imageFailed = true; }}
      />
    {:else if asset && !showPlaceholder}
      <img
        src={asset.src}
        alt={asset.alt}
        decoding="async"
        loading="lazy"
        style:object-fit={imageFit}
        onerror={() => {
          imageFailed = true;
          if (pointerRatio !== null) {
            hoverBroken = true;
            pointerRatio = null;
          }
        }}
      />
    {:else}
      <div class="placeholder-glow" aria-hidden="true"></div>
      <div class="placeholder" aria-hidden="true">
        {@render PlaceholderIcon({ icon: placeholderIcon })}
      </div>
    {/if}

    {#if activeSpriteFrame && card.hover.kind === "sprite" && spriteDims.width > 0}
      <div class="sprite-overlay" aria-hidden="true"
        style:background-image="url({card.hover.spriteUrl ?? activeSpriteFrame.url})"
        style:background-size="{(spriteDims.width / activeSpriteFrame.width) * 100}% {(spriteDims.height / activeSpriteFrame.height) * 100}%"
        style:background-position="{spriteDims.width <= activeSpriteFrame.width ? 0 : (activeSpriteFrame.x / (spriteDims.width - activeSpriteFrame.width)) * 100}% {spriteDims.height <= activeSpriteFrame.height ? 0 : (activeSpriteFrame.y / (spriteDims.height - activeSpriteFrame.height)) * 100}%"
        style:background-repeat="no-repeat"
      ></div>
    {/if}

    {#if isImageSequenceHover && sequenceAssets.length > 1 && !hoverBroken}
      <div class="sequence-rail" aria-hidden="true">
        {#each sequenceAssets as sequenceAsset, sequenceIndex (sequenceAsset.src)}
          <span class:is-active={activeSequenceIndex === sequenceIndex}></span>
        {/each}
      </div>
    {/if}


    {#if selectable}
      <input
        class="selection"
        class:is-selected={selected}
        type="checkbox"
        checked={selected}
        title={`Select ${card.entity.title}`}
        aria-label={`Select ${card.entity.title}`}
        onclick={stopSelectionActivation}
        onpointerdown={stopSelectionActivation}
        onchange={handleSelectionChange}
      />
    {/if}

    {#if !imageOnly && nsfw}
      <div class="badges top-badges">
        <span class="badge danger icon-only" title="NSFW" aria-label="NSFW">
          <Flame size={13} />
        </span>
      </div>
    {/if}

  </div>

  {#if !imageOnly}
    <div class="glass-info" class:has-subtitle={Boolean(card.subtitle || subtitleContent)}>
      {#if subtitleContent}
        <div class={`custom-above title-align-${titleAlign}`}>
          {@render subtitleContent(card)}
        </div>
      {/if}
      <div class="copy">
        <h3 class={`title-align-${titleAlign} title-size-${titleSize}`} title={card.entity.title} aria-label={card.entity.title}>
          {card.entity.title}
        </h3>
        {#if card.subtitle && !subtitleContent}
          <div class={`subtitle title-align-${titleAlign}`} title={card.subtitle}>
            <OverflowTicker text={card.subtitle} align={titleAlign} />
          </div>
        {/if}
      </div>

      {#if bottomLeft || rating > 0 || card.meta?.length}
        <div class="chips">
          {#if bottomLeft}
            <span class="chip chip-accent" title={bottomLeft.title ?? bottomLeft.label}>
              {bottomLeft.label}
            </span>
          {/if}
          {#if card.meta?.length}
            {#each card.meta as item (item.icon + item.label)}
              <span class="chip">
                {@render MetaIcon({ icon: item.icon })}
                {item.label}
              </span>
            {/each}
          {/if}
          {#if rating > 0}
            <span class="chip chip-rating" title="Rating">
              <Star size={11} />
              {formatRating(rating)}
            </span>
          {/if}
        </div>
      {/if}
    </div>
  {/if}
</svelte:element>

{#snippet PlaceholderIcon({ icon }: { icon: EntityThumbnailMetaIcon })}
  {#if icon === "video"}
    <div class="placeholder-frame">
      <Film class="placeholder-icon-framed" />
    </div>
  {:else if icon === "audio"}
    <div class="placeholder-audio">
      <Disc3 class="placeholder-disc" />
      <Music class="placeholder-note" />
    </div>
  {:else if icon === "person"}
    <Users class="placeholder-icon" />
  {:else if icon === "book"}
    <BookOpen class="placeholder-icon" />
  {:else if icon === "gallery"}
    <Layers class="placeholder-icon" />
  {:else if icon === "image"}
    <Image class="placeholder-icon" />
  {:else if icon === "studio"}
    <Building2 class="placeholder-icon" />
  {:else if icon === "tag"}
    <Tag class="placeholder-icon" />
  {:else if icon === "collection"}
    <FolderOpen class="placeholder-icon" />
  {:else}
    <Hash class="placeholder-icon" />
  {/if}
{/snippet}

{#snippet MetaIcon({ icon }: { icon: EntityThumbnailMetaIcon })}
  {#if icon === "audio"}
    <Music size={12} />
  {:else if icon === "book"}
    <BookOpen size={12} />
  {:else if icon === "calendar"}
    <Calendar size={12} />
  {:else if icon === "chapter"}
    <Album size={12} />
  {:else if icon === "collection"}
    <Layers size={12} />
  {:else if icon === "duration"}
    <Clock3 size={12} />
  {:else if icon === "gallery"}
    <Images size={12} />
  {:else if icon === "image"}
    <Images size={12} />
  {:else if icon === "person"}
    <Users size={12} />
  {:else if icon === "studio"}
    <Building2 size={12} />
  {:else if icon === "tag"}
    <Tag size={12} />
  {:else if icon === "video"}
    <Film size={12} />
  {:else}
    <Hash size={12} />
  {/if}
{/snippet}

<style>
  .entity-thumbnail {
    position: relative;
    display: flex;
    flex-direction: column;
    container-type: inline-size;
    content-visibility: auto;
    contain-intrinsic-size: auto 18rem;
    color: var(--color-text, #f4efe6);
    text-decoration: none;
    min-width: 0;
    transition:
      transform 200ms var(--ease-default, cubic-bezier(0.4, 0, 0.2, 1));
  }

  .entity-thumbnail:is(:hover, :focus-visible) {
    transform: translateY(-1px);
  }

  .entity-thumbnail.is-selected .media {
    border-color: rgb(242 194 106 / 0.6);
    box-shadow:
      inset 0 0 0 1px rgb(242 194 106 / 0.28),
      0 0 0 1px rgb(242 194 106 / 0.45),
      0 0 26px rgb(242 194 106 / 0.18),
      0 10px 22px rgb(0 0 0 / 0.4);
  }

  @media (prefers-reduced-motion: reduce) {
    .entity-thumbnail {
      transition: none;
    }

    .entity-thumbnail:is(:hover, :focus-visible) {
      transform: none;
    }
  }

  .entity-thumbnail.is-list {
    flex-direction: row;
    inline-size: 100%;
    min-block-size: 5.25rem;
    contain-intrinsic-size: auto 5.25rem;
    border: 1px solid rgb(255 255 255 / 0.08);
    background: rgb(12 12 13 / 0.92);
    box-shadow:
      inset 0 0 0 1px rgb(0 0 0 / 0.5),
      0 2px 6px rgb(0 0 0 / 0.32);
  }

  .entity-thumbnail.is-list .media {
    flex: 0 0 auto;
    width: clamp(5.5rem, 30%, 7.5rem);
    border: none;
    box-shadow: none;
    border-right: 1px solid rgb(255 255 255 / 0.1);
  }

  .media {
    position: relative;
    z-index: 2;
    flex: 3;
    min-height: 0;
    overflow: hidden;
    touch-action: pan-y;
    border: 1px solid rgb(255 255 255 / 0.08);
    border-radius: 6px;
    background:
      radial-gradient(circle at 50% 45%, rgb(255 255 255 / 0.08), transparent 34%),
      linear-gradient(135deg, rgb(15 16 18 / 0.96), rgb(28 25 20 / 0.92)),
      #111;
    box-shadow:
      inset 0 0 0 1px rgb(0 0 0 / 0.5),
      0 2px 6px rgb(0 0 0 / 0.32);
    transition:
      border-color 200ms var(--ease-default, cubic-bezier(0.4, 0, 0.2, 1)),
      box-shadow 200ms var(--ease-default, cubic-bezier(0.4, 0, 0.2, 1));
  }

  .entity-thumbnail:is(:hover, :focus-visible) .media {
    border-color: rgb(242 194 106 / 0.32);
    box-shadow:
      inset 0 0 0 1px rgb(0 0 0 / 0.5),
      0 0 0 1px rgb(242 194 106 / 0.18),
      0 10px 22px rgb(0 0 0 / 0.42),
      0 0 24px rgb(242 194 106 / 0.07);
  }

  .entity-thumbnail.is-list .media img {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
  }

  .media img,
  .placeholder {
    width: 100%;
    height: 100%;
  }

  .media img {
    display: block;
    object-fit: cover;
    object-position: center;
    transition:
      filter 160ms ease;
  }

  .entity-thumbnail:is(:hover, :focus-visible) .media img,
  .entity-thumbnail.is-hovering .media img {
    filter: saturate(1.06) contrast(1.04);
  }

  .media img.sprite-active,
  .media img:global(.sprite-active) {
    opacity: 0;
  }

  .sequence-rail {
    position: absolute;
    z-index: 3;
    left: 0.55rem;
    right: 0.55rem;
    bottom: 0.45rem;
    display: flex;
    gap: 0.18rem;
    pointer-events: none;
  }

  .sequence-rail span {
    min-width: 0;
    height: 0.16rem;
    flex: 1 1 0;
    background: rgb(255 255 255 / 0.24);
    box-shadow: 0 0 8px rgb(0 0 0 / 0.38);
    transition:
      background 120ms ease,
      box-shadow 120ms ease,
      transform 120ms ease;
  }

  .sequence-rail span.is-active {
    background: rgb(242 194 106 / 0.95);
    box-shadow: 0 0 10px rgb(242 194 106 / 0.55);
    transform: scaleY(1.35);
  }

  .sprite-overlay {
    position: absolute;
    inset: 0;
    z-index: 1;
  }

  .placeholder-glow {
    position: absolute;
    inset: 0;
    background:
      radial-gradient(circle at top, rgb(245 239 213 / 0.16), transparent 38%),
      linear-gradient(180deg, rgb(7 8 11 / 0.06) 0%, rgb(7 8 11 / 0.55) 100%);
    pointer-events: none;
  }

  .placeholder {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    height: 100%;
  }

  .placeholder-frame {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 3.5rem;
    height: 3.5rem;
    border: 1px solid rgb(242 194 106 / 0.25);
    background: rgb(0 0 0 / 0.3);
    backdrop-filter: blur(4px);
    box-shadow:
      inset 0 1px 0 rgb(255 255 255 / 0.08),
      0 0 24px rgb(0 0 0 / 0.35);
  }

  .placeholder :global(.placeholder-icon-framed) {
    width: 1.75rem;
    height: 1.75rem;
    color: rgb(231 211 175 / 0.85);
    filter: drop-shadow(0 0 14px rgb(242 194 106 / 0.24));
  }

  .placeholder :global(.placeholder-icon) {
    width: 2rem;
    height: 2rem;
    color: rgb(255 255 255 / 0.25);
  }

  .placeholder-audio {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .placeholder :global(.placeholder-disc) {
    width: 3.5rem;
    height: 3.5rem;
    color: rgb(255 255 255 / 0.15);
    animation: spin-disc 12s linear infinite;
  }

  .placeholder :global(.placeholder-note) {
    position: absolute;
    width: 1.5rem;
    height: 1.5rem;
    color: rgb(255 255 255 / 0.4);
  }

  @keyframes spin-disc {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
  }

  @media (prefers-reduced-motion: reduce) {
    .placeholder :global(.placeholder-disc) {
      animation: none;
    }
  }

  .glass-info {
    position: relative;
    z-index: 1;
    flex: 1;
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 0.2em;
    min-width: 0;
    min-height: 0;
    margin-top: -0.5rem;
    padding: 0 0.65rem;
    border: 1px solid rgb(255 255 255 / 0.07);
    border-top: none;
    border-radius: 0 0 6px 6px;
    background:
      linear-gradient(
        180deg,
        rgb(20 22 26 / 0.95) 0%,
        rgb(13 14 17) 100%
      );
    box-shadow:
      0 4px 12px rgb(0 0 0 / 0.4),
      inset 0 1px 0 rgb(255 255 255 / 0.04);
    overflow: hidden;
    pointer-events: none;
  }

  .glass-info.has-subtitle {
    gap: 0.15em;
  }

  .badges {
    position: absolute;
    z-index: 3;
    right: 0.45rem;
    left: 2.45rem;
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
    align-items: center;
    justify-content: flex-end;
    pointer-events: none;
  }

  .top-badges {
    top: 0.45rem;
  }

  .badge {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    border: 1px solid rgb(255 255 255 / 0.12);
    border-radius: var(--radius-xs, 4px);
    background: rgb(11 11 12 / 0.72);
    color: rgb(244 239 230 / 0.88);
    font-family: var(--font-mono, "JetBrains Mono", monospace);
    font-size: 0.66rem;
    line-height: 1;
    letter-spacing: 0;
    min-height: 1.35rem;
    padding: 0.25rem 0.38rem;
    backdrop-filter: blur(12px);
  }

  .badge :global(svg) {
    flex: 0 0 auto;
  }

  .danger {
    color: #ff806f;
    border-color: rgb(255 92 67 / 0.42);
    background: rgb(40 13 10 / 0.76);
    box-shadow: 0 0 14px rgb(255 92 67 / 0.12);
  }

  .icon-only {
    justify-content: center;
    inline-size: 1.35rem;
    padding-inline: 0;
  }

  .selection {
    position: absolute;
    top: 0.45rem;
    left: 0.45rem;
    z-index: 6;
    display: grid;
    inline-size: 1.55rem;
    block-size: 1.55rem;
    border: 1px solid rgb(255 255 255 / 0.12);
    border-radius: var(--radius-xs, 4px);
    background: rgb(11 11 12 / 0.72);
    appearance: none;
    cursor: pointer;
    opacity: 0;
    pointer-events: none;
    backdrop-filter: blur(12px);
    transition:
      opacity 120ms ease,
      border-color 120ms ease,
      box-shadow 120ms ease;
  }

  .entity-thumbnail:is(:hover, :focus-within) .selection,
  .entity-thumbnail.is-select-mode .selection,
  .entity-thumbnail.is-selected .selection,
  .selection:focus {
    opacity: 1;
    pointer-events: auto;
  }

  .selection::before {
    position: absolute;
    inset: 0.38rem;
    border: 1px solid rgb(244 239 230 / 0.7);
    background: rgb(0 0 0 / 0.16);
    content: "";
    pointer-events: none;
  }

  .selection::after {
    position: absolute;
    top: 0.58rem;
    left: 0.54rem;
    inline-size: 0.45rem;
    block-size: 0.24rem;
    border-bottom: 2px solid #0b0b0c;
    border-left: 2px solid #0b0b0c;
    content: "";
    opacity: 0;
    transform: rotate(-45deg);
  }

  .selection:checked,
  .selection.is-selected {
    border-color: rgb(242 194 106 / 0.74);
    box-shadow: 0 0 16px rgb(242 194 106 / 0.22);
  }

  .selection:checked::before,
  .selection.is-selected::before {
    border-color: rgb(242 194 106 / 0.95);
    background: linear-gradient(135deg, #f2c26a, #b8862e);
  }

  .selection:checked::after,
  .selection.is-selected::after {
    opacity: 1;
  }

  .copy {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .subtitle {
    overflow: hidden;
    margin: 0.1rem 0 0;
    color: rgb(196 201 212 / 0.82);
    font-family: var(--font-mono, "JetBrains Mono", monospace);
    font-size: 0.62rem;
    line-height: 1.25;
    text-overflow: ellipsis;
    white-space: nowrap;
    text-shadow: 0 1px 3px rgb(0 0 0 / 0.6);
  }

  .custom-above {
    display: flex;
    min-width: 0;
  }

  .custom-above.title-align-left {
    justify-content: flex-start;
  }

  .custom-above.title-align-center {
    justify-content: center;
  }

  .custom-above.title-align-right {
    justify-content: flex-end;
  }

  .entity-thumbnail.is-list .glass-info {
    flex: 1 1 0;
    min-width: 0;
    min-height: auto;
    justify-content: center;
    min-block-size: 5.25rem;
    margin-top: 0;
    padding: 0.72rem 0.9rem;
    border-radius: 0;
    background:
      linear-gradient(180deg, rgb(10 12 15 / 0.94), rgb(9 10 12 / 0.98)),
      #0a0b0d;
    border: none;
    box-shadow: none;
  }

  .entity-thumbnail.is-list .selection {
    opacity: 1;
    pointer-events: auto;
  }

  .entity-thumbnail.is-list .badges {
    right: 0.38rem;
    left: 2.2rem;
  }

  h3 {
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    margin: 0;
    min-width: 0;
    overflow: hidden;
    font-family: var(--font-heading, Geist, sans-serif);
    font-size: 0.82rem;
    font-weight: 620;
    line-height: 1.25;
    letter-spacing: -0.01em;
    white-space: normal;
    text-overflow: ellipsis;
    color: rgb(244 239 230 / 0.95);
  }

  .title-size-compact {
    font-size: 0.66rem;
    font-weight: 620;
    line-height: 1.15;
  }

  .title-align-left {
    text-align: left;
  }

  .title-align-center {
    text-align: center;
  }

  .title-align-right {
    text-align: right;
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: 0.2rem;
    max-block-size: 1.3rem;
    overflow: hidden;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: 0.2rem;
    min-width: 0;
    max-width: 100%;
    border: 1px solid rgb(255 255 255 / 0.1);
    border-radius: var(--radius-xs, 4px);
    background: rgb(255 255 255 / 0.06);
    color: rgb(244 239 230 / 0.72);
    font-family: var(--font-mono, "JetBrains Mono", monospace);
    font-size: 0.58rem;
    line-height: 1;
    min-height: 1.15rem;
    padding: 0.14rem 0.28rem;
    text-shadow: 0 1px 2px rgb(0 0 0 / 0.5);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .chip :global(svg) {
    flex: 0 0 auto;
    color: rgb(242 194 106 / 0.82);
  }

  .chip-accent {
    border-color: rgb(242 194 106 / 0.38);
    background: rgb(13 13 14 / 0.78);
    color: rgb(244 239 230 / 0.92);
    box-shadow: 0 0 8px rgb(242 194 106 / 0.08);
  }

  .chip-rating {
    border-color: rgb(242 193 95 / 0.5);
    background: linear-gradient(135deg, rgb(50 38 14 / 0.85), rgb(32 25 13 / 0.85));
    color: #f2c15f;
    box-shadow: 0 0 10px rgb(242 193 95 / 0.18), inset 0 1px 0 rgb(255 255 255 / 0.06);
    font-weight: 600;
  }

  .chip-rating :global(svg) {
    color: #f2c15f;
    filter: drop-shadow(0 0 3px rgb(242 193 95 / 0.4));
  }

  @container (max-width: 120px) {
    .glass-info {
      padding: 0 0.3rem;
    }

    h3 {
      -webkit-line-clamp: 1;
      line-clamp: 1;
      font-size: 0.6rem;
    }

    .subtitle,
    .custom-above,
    .chips {
      display: none;
    }
  }

  @container (max-width: 200px) and (min-width: 121px) {
    .glass-info {
      padding: 0 0.4rem;
    }

    h3 {
      font-size: 0.72rem;
    }

    .subtitle {
      display: none;
    }

    .chip {
      font-size: 0.5rem;
      min-height: 1rem;
      padding: 0.1rem 0.22rem;
    }
  }

  @media (max-width: 640px) {
    .badge {
      font-size: 0.61rem;
    }

    .chip {
      font-size: 0.56rem;
      min-height: 1.18rem;
    }
  }
</style>
