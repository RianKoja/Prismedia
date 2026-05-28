<script lang="ts">
  import { useIdentifyDetailAction } from "./use-identify-detail-action.svelte";

  interface Props {
    entityId: string;
    entityKind: string;
  }

  let { entityId, entityKind }: Props = $props();

  const identifyAction = useIdentifyDetailAction(() => entityId, () => entityKind);
</script>

{#if identifyAction.action}
  <button
    type="button"
    class:active={identifyAction.action.active}
    onclick={identifyAction.action.onClick}
    title={identifyAction.action.title}
    aria-label={identifyAction.action.ariaLabel}
  >
    {identifyAction.action.label}
  </button>
{/if}
