<script lang="ts">
  import type { Component, Snippet } from "svelte";
  import { ChevronRight } from "@lucide/svelte";
  import EntityThumbnail from "$lib/components/thumbnails/EntityThumbnail.svelte";
  import { toAspectRatioNumeric, type EntityThumbnailCard } from "$lib/entities/entity-thumbnail";

  /**
   * Horizontal scrolling shelf of entity thumbnails with a standard header.
   *
   * Sizing modes:
   * - `width` (default): every card gets the same width and its height follows
   *   its own aspect ratio. Right for single-kind shelves where every card
   *   shares a shape.
   * - `height`: every card gets the same image height and its width follows its
   *   aspect ratio. Right for mixed-kind shelves (videos next to posters next
   *   to album squares) where uniform widths would make the row ragged.
   *
   * Customization is snippet-based: `headerAccessory` adds content beside the
   * "View all" link and `item` replaces the default thumbnail renderer per card.
   */
  interface Props {
    label: string;
    icon?: Component;
    cards: EntityThumbnailCard[];
    /** "View all" destination; omit to hide the link. */
    href?: string | null;
    sizing?: "width" | "height";
    headerAccessory?: Snippet;
    item?: Snippet<[EntityThumbnailCard]>;
  }

  const { label, icon: Icon, cards, href = null, sizing = "width", headerAccessory, item }: Props = $props();

  function itemWidthStyle(card: EntityThumbnailCard): string {
    if (sizing === "width") return "clamp(140px, 18vw, 220px)";
    return `calc(var(--shelf-h) * ${toAspectRatioNumeric(card.aspectRatio).toFixed(4)})`;
  }
</script>

<section>
  <div class="flex items-center justify-between mb-4 px-3">
    <h2 class="text-lg font-semibold flex items-center gap-2">
      {#if Icon}
        <Icon class="w-4.5 h-4.5 text-accent-500" />
      {/if}
      {label}
    </h2>
    <div class="flex items-center gap-3">
      {@render headerAccessory?.()}
      {#if href}
        <a
          {href}
          class="inline-flex items-center gap-1 text-xs text-text-muted hover:text-text-accent transition-colors"
        >
          View all
          <ChevronRight class="h-3.5 w-3.5" />
        </a>
      {/if}
    </div>
  </div>

  <div
    class="flex gap-3 overflow-x-auto pt-1 pb-5 snap-x snap-mandatory scrollbar-hidden px-3"
    style:--shelf-h={sizing === "height" ? "clamp(150px, 16vw, 200px)" : undefined}
  >
    {#each cards as card (card.entity.id)}
      <div class="flex-none snap-start" style:width={itemWidthStyle(card)}>
        {#if item}
          {@render item(card)}
        {:else}
          <EntityThumbnail {card} />
        {/if}
      </div>
    {/each}
  </div>
</section>
