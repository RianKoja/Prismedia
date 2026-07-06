<script lang="ts">
  import { provideNsfw } from "$lib/nsfw/store.svelte";
  import IdentifyButton from "./IdentifyButton.svelte";

  interface Props {
    entityKind?: string;
    entityId: string;
    title?: string;
    label?: string;
  }

  let { entityKind, entityId, title, label }: Props = $props();

  provideNsfw(() => ({ initialMode: "show", allowed: true }));
</script>

<IdentifyButton {entityKind} {entityId} {title} {label} />
