<script lang="ts">
  import { Clapperboard } from "@lucide/svelte";
  import EntityIndexPage from "$lib/components/entities/EntityIndexPage.svelte";
</script>

<EntityIndexPage
  kind="movie"
  title="Movies"
  icon={Clapperboard}
  prefsKey="movies"
  emptyTitle="No movies"
  emptyMessage="No movies in your library yet. Add a same-named movie folder and scan to get started."
  errorMessage="Failed to load movies."
/>
