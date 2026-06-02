# Library Organization

Prismedia organizes video libraries into three first-class views of content:
**Videos** (every playable video file), **Movies** (single-file releases in a
same-named folder), and **Series** (multi-episode shows, with or without
seasons). Each library root has independent toggles for what to scan, and the
on-disk folder layout decides what every file becomes.

This doc explains the rules, shows the layouts that work, and calls out the
ones that don't.

---

## The video toggle

Each library root has a single **Videos** toggle configured in **Library
settings**. When on, every video file under the root is classified by its
folder depth (see below). When off, the root is skipped during video
scans.

---

## The depth rule

Prismedia looks at how deep under the library root each file sits and routes
it accordingly. Counting the library root as depth 0:

| Depth | What it is                            | Becomes                  |
|-------|---------------------------------------|--------------------------|
| 0     | File at the library root              | Video                    |
| 1     | Same-named single file in its folder, with no content subfolders | Movie |
| 1     | File inside one folder                | Episode (flat series)    |
| 2     | File inside two folders               | Episode (seasoned series)|
| ≥ 3   | File buried deeper                    | **Rejected** (skipped)   |

The movie rule runs before series detection and only matches the exact
`Folder Name/Folder Name.ext` shape, or a file that starts with the folder
name followed by a release suffix like `Bluray-1080p`, when that folder
contains no content subfolders and no other video files. Artwork, NFO files,
and generated sidecar folders such as `.trickplay` do not count as content
subfolders. Everything playable still appears in the Videos tab; movies
additionally appear in the Movies section.

Series detection is applied automatically: if every file under a series
folder lives directly inside it, the series is **flat** (one synthetic
season called Specials/Season 0). If at least one file lives inside a
recognized season folder, the whole series is treated as **seasoned**.

---

## Good layouts

### Loose video library

```
/library/movies
├── Blade Runner (1982).mkv
├── Heat (1995).mp4
└── No Country for Old Men (2007).mkv
```

All three files are depth 0 → standalone videos. They appear in the Videos tab
without creating movie entities. The Videos toggle must be on.

### Movies library

```
/library/movies
├── Blade Runner
│   └── Blade Runner.mp4
├── Friendship
│   └── Friendship.mkv
├── Friendship (2025)
│   ├── Friendship (2025) Bluray-1080p.mp4
│   ├── folder.jpg
│   ├── movie.nfo
│   └── Friendship (2025) Bluray-1080p.trickplay/
└── Heat
    └── Heat.mp4
```

Each folder contains exactly one movie media file and no content subfolders,
so each folder becomes a movie. The child media file still appears in Videos.

### Flat series (Case A)

```
/library/series
└── My Cool Show
    ├── My Cool Show - 01.mkv
    ├── My Cool Show - 02.mkv
    └── My Cool Show - 03.mkv
```

Every episode lives directly inside `My Cool Show/`, so the series is
flat. All three episodes land in synthetic Season 0.

### Series with seasons (Case B)

```
/library/series
└── Another Show
    ├── Season 01
    │   ├── S01E01.mkv
    │   └── S01E02.mkv
    └── Season 02
        ├── S02E01.mkv
        └── S02E02.mkv
```

Each episode is depth 2, inside a recognized season folder. Episodes are
placed into Season 1 / Season 2 accordingly.

### Mixed library

```
/library
├── Trailer Reel.mkv         ← video (depth 0)
├── Heat
│   └── Heat.mkv             ← movie + child video
└── Twin Peaks
    ├── Season 01
    │   └── S01E01.mkv       ← episode (depth 2)
    └── Season 02
        └── S02E01.mkv       ← episode (depth 2)
```

Files at the root stay as videos, same-named single-file folders become
movies, and season folders become series episodes.

### Specials folder

```
/library/series/Another Show
├── Season 01
│   └── S01E01.mkv
└── Specials
    └── Behind the Scenes.mkv
```

`Specials` is recognized as Season 0. Loose files at the series root in a
Case B series also fall back to Season 0.

---

## Bad layouts

### Too deep

```
/library/series/Show/Extras/Bonus/clip.mkv
```

`clip.mkv` is depth 3. Prismedia rejects it and logs a warning. Move it up
or remove it from the library.

### File outside the library root

Symlinks or paths that resolve outside the configured root are rejected.

---

## Filename conventions

Filenames are parsed for season/episode numbers, year hints, and a clean
display title. The parsers are forgiving but consistent layouts are easier
to spot-check.

| Form                                    | Parsed as                       |
|-----------------------------------------|---------------------------------|
| `Show.S01E03.mkv` / `Show s1e3.mkv`     | Season 1, Episode 3             |
| `Show 1x03.mkv`                         | Season 1, Episode 3             |
| `Show - Season 1 - Episode 3.mkv`       | Season 1, Episode 3             |
| `Show - 053.mkv`                        | Absolute episode 53             |
| `Movie Title (2007).mkv`                | Title `Movie Title`, year 2007  |
| `Movie.Title.2007.1080p.mkv`            | Title `Movie Title`, year 2007  |

Resolution tokens like `1080p`, `2160p`, `BluRay` are stripped from the
title automatically.

---

## Music libraries

Audio roots (the **Audio** scan toggle) follow a separate, stricter folder
contract that matches what every other music app expects. There are exactly
two supported layouts, plus a multi-disc extension — Prismedia does not chain
folders arbitrarily.

### Supported layouts

```text
# Album → Songs
Music/
  Evolve/
    01 - Next to Me.flac
    02 - Believer.flac

# Artist → Album → Songs
Music/
  Imagine Dragons/            ← Artist (a grouping, like a gallery is for images)
    Evolve/                   ← Album
      01 - Next to Me.flac
    Night Visions/            ← Album
      01 - Radioactive.flac
```

An **Artist** folder is a first-class grouping entity with its own metadata
and **members** (people credited with a role such as Drummer, Vocals, or
Composer — modeled exactly like a series links its cast). It gathers an
artist's albums; it is not itself playable.

### How folders are classified

Prismedia resolves the tree leaf-first, from the folders that directly hold
audio files:

| Folder contents                                   | Becomes                         |
|---------------------------------------------------|---------------------------------|
| Holds audio files directly                        | **Album**                       |
| Only subfolders, none holding audio directly      | **Artist** (groups albums)      |
| A disc subfolder *inside* an album                | **Section** of that album       |

Maximum nesting is **Artist → Album → Section**. Anything deeper is folded
into sections rather than creating more entities.

### Multi-disc albums (sections)

A disc/box-set subfolder inside an album becomes a **section** of that one
album rather than a separate album. Its tracks appear under a section heading
(the folder name) with track numbering that restarts per section:

```text
# Album with discs (no artist)        # Artist → Album with discs
Greatest Hits/                         Pink Floyd/
  Disc 1/                                The Wall/
    01 - ....flac                          Disc 1/
  Disc 2/                                    01 - ....flac
    01 - ....flac                          Disc 2/
                                             01 - ....flac
```

Section folders are recognized by name — `Disc 1`, `CD2`, `Side A`, `Vol. 3`,
`Part II`, `Disc One`, and similar. This is also how Prismedia tells
`Artist/Album/Songs` apart from `Album/Disc 1/Songs` when tracks sit two
folders deep: if a folder's track-bearing children are *all* disc-named, the
folder is an album with sections; otherwise it is an artist of albums.

Loose audio files sitting directly in the library root are kept as standalone
tracks.

### Metadata

Albums are identified against release providers (e.g. MusicBrainz releases)
and artists against artist providers (MusicBrainz artists), which fill the
artist's members, origin, formed year, and genres. Re-scans reconcile the
structure: if you reorganize an album into an `Artist/Album` layout, the next
scan moves it under the new artist grouping automatically.

---

## Metadata source priority

When Prismedia imports a file, it merges metadata from several sources in
this order (later sources override earlier ones for fields they provide):

1. **Filename parser** — last-resort fallback for title, year, season,
   and episode numbers.
2. **JSON sidecar** — `<filename>.info.json` next to the video. Movie
   folders may also use `movie.info.json` or `movie.json`. Lower priority
   than NFO. Provides title, details, date, studio, rating, tags, people,
   urls.
3. **NFO sidecar** — `<filename>.nfo` next to the video. Movie folders may
   also use `movie.nfo`. Highest priority. Provides title, plot, release
   date, studio, rating, tags, genres, urls, and credited people.

User edits made in the Prismedia UI are never overwritten by a re-scan.
The scanner only fills in fields that are currently empty.

---

## When to re-scan

A re-scan is safe and idempotent. It picks up new files, removes rows for
files that have been deleted from disk, and refreshes file size and
linkage. It will not touch user-edited fields like title overrides,
ratings, NSFW flags, watch progress, or the `organized` flag.

Re-scans are triggered automatically on a schedule and can be run
on-demand from the **Operations** dashboard.
