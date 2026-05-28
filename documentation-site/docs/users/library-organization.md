---
sidebar_position: 3
title: Library Organization
description: How folders become videos, series, galleries, books, audio, and files.
---

# Library Organization

Prismedia treats your folder layout as a source of truth. You decide which folders are watched roots and which media types each root scans.

Use the **Files** workspace to inspect how Prismedia sees a root and to rescan, exclude, rename, move, upload, or delete when your media mount allows it.

## Videos, movies, and series

Video classification is based on folder depth below a watched root.

| Depth below root | Example | Becomes |
| --- | --- | --- |
| 0 | `/media/videos/Heat (1995).mkv` | Standalone video/movie |
| 1 | `/media/videos/My Show/Episode 01.mkv` | Episode in a flat series |
| 2 | `/media/videos/My Show/Season 01/S01E01.mkv` | Episode in a season |
| 3+ | `/media/videos/A/B/C/file.mkv` | Skipped as too deep |

Recognized season folders include `Season 1`, `Season 01`, `S01`, `Specials`, and similar forms. Specials map to season 0.

## Video filename hints

Filenames are parsed for title, year, season, and episode hints:

| Form | Parsed hint |
| --- | --- |
| `Movie Title (2007).mkv` | title + year |
| `Movie.Title.2007.1080p.mkv` | title + year after release tokens are stripped |
| `Show.S01E03.mkv` | season 1, episode 3 |
| `Show 1x03.mkv` | season 1, episode 3 |
| `Show - 053.mkv` | absolute episode 53 |

The parser is a fallback. Provider identify and user edits can enrich or replace parsed metadata.

## Sidecar metadata

Prismedia can merge metadata from sidecar files found beside media files. User edits made in the app are treated as intentional library state and should not be overwritten by routine rescans.

Typical sidecars:

- `.nfo`
- `.info.json`
- subtitle files such as `.srt`, `.vtt`, `.ass`

## Images and galleries

Loose image files become **Images** when the root has image scanning enabled.

Folders of images become **Galleries** when gallery scanning is enabled. Galleries can contain still images and web-style animated media such as MP4, WebM, and MOV items.

Use folders when a set of images should browse together. Leave images at the root when they should remain standalone.

## Books and comics

Books are scanned from `.cbz` and `.zip` archives.

| Layout | Becomes |
| --- | --- |
| `/media/books/One Shot.cbz` | Single book |
| `/media/books/Series/Chapter 001.cbz` | Chapter in a book/series |
| `/media/books/Series/Volume 01/Chapter 001.cbz` | Chapter inside a volume |

`ComicInfo.xml` inside an archive can provide title, series, issue/chapter, volume, page count, summary, publisher/studio, tags, and people/creator metadata.

## Audio

Audio roots are folder-oriented:

```text
/media/music
  /Album A
    01 Track.flac
    02 Track.flac
  /Artist B
    /Album B
      01 Track.mp3
```

Each folder can become an audio library; tracks inside become audio tracks. Probe jobs read duration, codec, embedded tags, and cover art where available.

## Scan exclusions

Use **Files -> Exclude** to keep a path out of future scans without deleting it from disk. Exclusions are reversible from the same context menu.

Generated samples, previews, and trailers are skipped by filename patterns when they look like derivative media rather than library items.

## When to rescan

Rescan when:

- Files were added, moved, renamed, or deleted outside Prismedia.
- You changed scan toggles on a root.
- You added sidecar metadata or subtitles.
- You removed an exclusion.
- You want Prismedia to reconcile linked file state after manual cleanup.

Scans are safe to run repeatedly.
