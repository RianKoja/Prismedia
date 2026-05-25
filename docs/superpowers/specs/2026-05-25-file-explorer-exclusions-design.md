# File Explorer Exclusions Design

Prismedia's Files explorer will let users exclude a file or directory from library scans and later remove that exclusion from the same UI. Exclusions are scoped to a library root, survive rescans, and are deleted when their owning library root is deleted.

## Scope

- Add and remove exclusions from the Files context menu and detail toolbar.
- Persist exclusions as library-root-relative paths.
- Show excluded files and folders in the file explorer as muted, non-cataloged filesystem entries.
- Hide previews and linked entity cards for excluded detail views; show only basic filesystem properties and exclusion status.
- Make scan discovery skip excluded files and descendants of excluded directories.
- Make rescans remove existing catalog entities whose source paths are now excluded.
- Raise the Files context menu above neighboring panes so it is not visually clipped by the detail surface.

## Backend Design

The existing `media_file_ignores` table becomes the universal scan exclusion table. It will store `library_root_id`, normalized root-relative `path`, `kind`, `reason`, and timestamps. The primary key becomes `(library_root_id, path)`, with a cascade foreign key to `library_roots`.

`FilesService` will add two use cases:

- `ExcludeAsync(FileExclusionRequest)` validates the path under the root, rejects the root itself, gets basic filesystem detail, upserts the exclusion, and queues scan jobs for the root.
- `RemoveExclusionAsync(FileExclusionRequest)` deletes the exclusion row and queues scan jobs for the root.

File list and detail DTOs gain `Excluded`. Detail calls for excluded paths pass no linked entities to storage and set `CanPreview` false. The frontend can still render basic properties from the existing detail payload.

## Scan Design

`ILibraryScanPersistence` exposes root-scoped exclusion reads. `FileDiscoveryService` accepts absolute excluded paths and prunes them during traversal:

- An excluded file is not returned.
- An excluded directory and all descendants are not traversed.
- Hidden/generated file behavior remains unchanged.

Scan handlers load exclusions once per root and pass them to discovery. Existing stale cleanup removes catalog rows because excluded paths are absent from valid path sets. Persistence cleanup also treats excluded source paths as stale so a scanner regression cannot keep excluded entities alive.

## Frontend Design

`FileEntry` metadata carries `excluded`. `file-tree-state` preserves it in `FileTreeNodeMeta`. `FileTreePane` adds a muted style for excluded rows and raises the context menu stacking level.

Context menu and detail toolbar actions:

- Normal entries show `Exclude`.
- Excluded entries show `Remove exclusion`.
- Library roots do not expose exclusion actions.

`FileDetailPane` shows exclusion status in properties, disables preview rendering, and hides linked entity sections while excluded.

## Testing

Backend tests cover:

- Excluding a path persists root-scoped metadata and queues scans.
- Removing an exclusion deletes it and queues scans.
- Children/detail responses mark excluded paths.
- Excluded detail suppresses linked entities and previews.
- Discovery skips excluded files and directories.
- Scan cleanup removes catalog entities under excluded paths.
- The exclusion row cascades when a library root is removed.

Frontend tests cover:

- Context actions switch between `exclude` and `remove-exclusion`.
- File tree metadata preserves `excluded`.
- Detail rendering hides linked entities and previews for excluded paths.

## Release Note

Add a user-facing `Added` or `Fixed` changelog entry under `## [Unreleased]` describing file explorer scan exclusions and the context menu layering fix.
