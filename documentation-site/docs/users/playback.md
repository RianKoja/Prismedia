---
sidebar_position: 5
title: Playback And Reading
description: Video, subtitles, image lightbox, book reader, audio, and resume.
---

# Playback And Reading

Prismedia plays, reads, and previews media through the same entity detail model. The exact controls change by media type, but ratings, metadata, artwork, links, and relationships stay consistent.

## Video playback

![Video detail](/img/screenshots/video-detail.png)

Videos use direct browser playback when possible. If the browser cannot play the source cleanly, Prismedia can generate HLS on demand through ffmpeg and serve the cached playlist and segments from the API.

Video detail pages include:

- Direct and HLS playback options.
- Trickplay thumbnails for timeline scrubbing.
- Custom preview frames.
- Subtitle track management.
- Dockable transcripts.
- Metadata, artwork, ratings, related entities, files, and provider IDs.

## Subtitles

Subtitle tracks can come from sidecar files, embedded streams, or manual upload. Supported text tracks are converted to WebVTT for browser playback.

![Subtitle settings](/img/screenshots/settings-subtitles.png)

Settings control auto-enable behavior, preferred languages, caption style, text size, and vertical position. Video pages also offer per-browser overrides from the player.

## Transcripts

The transcript tab shows cues for a selected subtitle track. Clicking a cue seeks the player. On desktop, the transcript can dock beside the video so playback and reading stay visible together.

![Transcript](/img/screenshots/transcript.png)

## Image lightbox

Images and gallery items open in the universal lightbox. It supports next/previous navigation, zoom, pan, animated media playback, metadata details, and linked entities.

## Book and comic reader

Books and comics open in a focused reader route. The reader supports:

- Resume from the last page.
- Paged spreads and vertical webtoon-style reading.
- Chapter and volume navigation.
- Mobile controls that stay out of the reading path while scrolling.

## Audio playback

Audio libraries show album-style track lists and nested sub-libraries. Tracks have waveform previews and can be played from detail pages or the persistent player.

![Audio playback](/img/screenshots/audio.png)

The player stays docked while you browse, supports shuffle and queue navigation, and keeps progress state on tracks.

## Resume state

Books and comics save reading progress. Video and audio playback progress is tracked so you can resume from where you stopped.

User progress is library state, not file metadata; rescans should not erase it.
