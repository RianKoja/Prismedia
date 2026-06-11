<script lang="ts">
  import type { EntityDetailDate } from "$lib/entities/entity-detail";

  /**
   * Labeled date run for EntityDetail hero meta rows. Renders each date as
   * "Label Value" (e.g. "Released Mar 20, 2026") so a bare date never appears
   * without saying what it is. Inherits the hero's `.meta-item` / `.meta-sep`
   * styling from EntityDetail.
   */
  interface Props {
    dates: EntityDetailDate[];
    /** Render a separator before the first date when other meta precedes it. */
    leadingSeparator?: boolean;
  }

  const { dates, leadingSeparator = false }: Props = $props();
</script>

{#each dates as date, i (date.code)}
  {#if leadingSeparator || i > 0}<span class="meta-sep"></span>{/if}
  <span class="meta-item">
    <span class="meta-item-label">{date.label}</span>
    {date.display}
  </span>
{/each}
