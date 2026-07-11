<script lang="ts">
  import { Badge, Select } from "@prismedia/ui-svelte";
  import { DOWNLOAD_PROTOCOL, type DownloadProtocolCode } from "$lib/api/generated/codes";

  interface Props {
    availableProtocols: DownloadProtocolCode[];
    value?: DownloadProtocolCode;
    busy?: boolean;
    onchange: (protocol: DownloadProtocolCode) => void;
  }

  let {
    availableProtocols,
    value = DOWNLOAD_PROTOCOL.usenet,
    busy = false,
    onchange,
  }: Props = $props();

  const hasUsenet = $derived(availableProtocols.includes(DOWNLOAD_PROTOCOL.usenet));
  const hasTorrent = $derived(availableProtocols.includes(DOWNLOAD_PROTOCOL.torrent));
  const canChoose = $derived(hasUsenet && hasTorrent);
  const onlyProtocol = $derived(hasUsenet ? DOWNLOAD_PROTOCOL.usenet : hasTorrent ? DOWNLOAD_PROTOCOL.torrent : null);
  const options = [
    { value: DOWNLOAD_PROTOCOL.usenet, label: "Usenet" },
    { value: DOWNLOAD_PROTOCOL.torrent, label: "Torrent" },
  ];

  function choose(raw: string) {
    if (raw === DOWNLOAD_PROTOCOL.usenet || raw === DOWNLOAD_PROTOCOL.torrent) onchange(raw);
  }
</script>

<section class="space-y-2">
  <div>
    <h3 class="text-kicker text-text-primary">Preferred download type</h3>
    <p class="mt-1 text-[0.72rem] leading-relaxed text-text-muted">
      Prismedia searches this type first, then falls back when it finds no good results.
    </p>
  </div>

  {#if canChoose}
    <div class="max-w-xs">
      <Select
        size="sm"
        {options}
        {value}
        disabled={busy}
        ariaLabel="Preferred download type"
        onchange={choose}
      />
    </div>
  {:else if onlyProtocol}
    <div class="flex flex-wrap items-center gap-2 text-[0.78rem] text-text-muted">
      <Badge variant="default">{onlyProtocol === DOWNLOAD_PROTOCOL.usenet ? "Usenet only" : "Torrent only"}</Badge>
      <span>
        {onlyProtocol === DOWNLOAD_PROTOCOL.usenet
          ? "Add and enable a torrent client to choose a preference."
          : "Add and enable a usenet client to choose a preference."}
      </span>
    </div>
  {:else}
    <p class="text-[0.78rem] text-text-muted">No enabled download clients. Add one before choosing a preference.</p>
  {/if}
</section>
