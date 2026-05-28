# pHash Contribution

Perceptual hashes (pHash) let Prismedia identify a video against StashBox-protocol community indexes by content rather than by name. Prismedia's pHash pipeline intentionally matches Stash's pipeline so values cluster with the existing fingerprint database.

## Why compatibility matters

The point of a pHash is that two encodings of the same video can produce the same hash. If Prismedia computed a slightly different value, identify-by-fingerprint would become less useful.

The implementation matches Stash's video pHash flow:

- Frame selection.
- ffmpeg seek strategy.
- Frame scale.
- 5 by 5 montage layout.
- Hash function.
- Lowercase 16-character hex format.

The helper source lives in `infra/phash/main.go`.

## Generation summary

1. Sample 25 frames evenly from the source duration.
2. Use ffmpeg input seek and scale each frame to width `160`.
3. Compose a 5 by 5 montage of the sampled frames.
4. Run `goimagehash.PerceptionHash`.
5. Store the resulting hash on the video fingerprint record.

The unified Docker image builds the helper and copies it to `/usr/local/bin/prismedia-phash`. In development, set `PRISMEDIA_PHASH_BIN` if the helper is not on `PATH`.

## When pHash is computed

- During scan/probe work when pHash generation is enabled.
- During preview or generated-asset rebuilds that refresh video fingerprints.
- During explicit pHash backfill diagnostics.

If the helper binary is missing, the worker logs a warning and skips pHash for that item. Other scan and playback behavior continues.

## Contribution flow

```text
Identify -> Accept StashBox match -> Link remote record -> Submit fingerprints
```

Accepting a StashBox-origin match records the remote link and can queue fingerprint submission for available algorithms such as MD5, OpenSubtitles hash, and pHash. Submission attempts are logged for auditing and troubleshooting.

## Build locally

```bash
cd infra/phash
go mod tidy
go build -o prismedia-phash .
```

Run it directly:

```bash
./prismedia-phash /path/to/video.mkv
```

## Troubleshooting

**pHash generation skipped** means the binary was not found. Use the unified Docker image, put the helper on `PATH`, or set `PRISMEDIA_PHASH_BIN`.

**Hashes do not match a community index** usually means the helper or ffmpeg behavior drifted. Compare `infra/phash/main.go` against the Stash video pHash implementation before changing constants.

**Slow generation** is expected for large sources because the helper seeks and decodes multiple frames. Keep pHash concurrency conservative on small disks.

**Submission failures** usually come from endpoint credentials, endpoint rate limits, stale remote IDs, or endpoint support for a subset of fingerprint algorithms.

## References

- Stash source: `pkg/hash/videophash/phash.go`, `pkg/stashbox/scene.go`
- StashDB pHash FAQ: <https://guidelines.stashdb.org/docs/faq_getting-started/stashdb/whats-a-phash/>
- StashDB contribution guide: <https://guidelines.stashdb.org/docs/faq_getting-started/stashdb/contributing-to-stashdb/>
