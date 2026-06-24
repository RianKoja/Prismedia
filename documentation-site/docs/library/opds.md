---
sidebar_position: 5
title: OPDS Reader Apps
description: Connect third-party ebook and comic readers to Prismedia with OPDS.
---

# OPDS Reader Apps

Prismedia exposes an **OPDS 1.2** catalog for third-party ebook and comic apps. Use it to browse visible books, search, load covers, and download/acquire files from your Prismedia library.

```text
http://your-prismedia-host:8008/opds
```

If Prismedia is behind HTTPS, use the public HTTPS origin instead:

```text
https://prismedia.example.com/opds
```

## Quick setup in a third-party reader

Use these settings in any app that supports OPDS 1.x / OPDS 1.2 catalogs:

| Reader field | Value |
| --- | --- |
| Catalog type | OPDS, OPDS 1.x, or Atom OPDS. |
| Server / catalog URL | `https://prismedia.example.com/opds` or `http://your-prismedia-host:8008/opds`. |
| Authentication | HTTP Basic Auth, if the app asks. |
| Username | A Jellyfin-compatible Prismedia profile username, for example `Prismedia`. |
| Password | The Prismedia API key from **Settings -> API Access**. |

After saving the catalog, browse **Libraries**, **Recently Added**, **Authors**, or **Series**. Series navigation groups child books; library acquisition feeds only show the individual downloadable books.

:::tip Apps that do not resend credentials for covers/downloads
Some OPDS clients authenticate the catalog request but then fetch cover or download links without the `Authorization` header. If covers or downloads fail while the catalog loads, put the Prismedia API key query parameter on the catalog URL:

```text
https://prismedia.example.com/opds?ApiKey=YOUR_API_KEY
```

Prismedia will carry that existing API-key query parameter into OPDS cover and acquisition links. Treat the full URL as a secret.
:::

:::note Books must be in a book-scanning library
A directory only appears in OPDS after it is configured as a Prismedia library root with **Scan books** enabled and the library scan has completed. Video-only, audio-only, image-only, disabled, or unsupported-format roots are excluded from OPDS.
:::

## What the catalog includes

The root catalog links to:

- Recently Added
- Libraries
- Authors
- Series
- Collections
- Tags
- Search

Publication entries include the title, author credits, summary, tags, series metadata, cover/thumbnail links, and one acquisition link when the source file is in a supported format.

## Supported formats

OPDS publishes downloadable books and comics in these formats:

| Format | File extensions | Media type |
| --- | --- | --- |
| EPUB | `.epub` | `application/epub+zip` |
| PDF | `.pdf` | `application/pdf` |
| CBZ / ZIP comics | `.cbz`, `.zip` | `application/vnd.comicbook+zip` or `application/zip` |
| CBR comics | `.cbr` | `application/vnd.comicbook-rar` |

Unsupported book files are excluded from OPDS feeds and return `404` from direct OPDS download links.

## Authentication

Every `/opds` route requires Prismedia authentication. There are no anonymous OPDS catalog, cover, or download URLs.

Most reader apps should use HTTP Basic Auth:

| Field | Value |
| --- | --- |
| Username | A Jellyfin-compatible profile username, such as `Prismedia`. |
| Password | The Prismedia API key from **Settings -> API Access**. |

OPDS also accepts the same API key and Jellyfin-compatible token transports documented in [Authentication & API Keys](../deployment/authentication.md):

- `X-Prismedia-Api-Key: <key>`
- `Authorization: Bearer <api-key-or-session-token>`
- Jellyfin token headers
- `?ApiKey=<key>` or `?api_key=<key>`

Some reader apps drop auth headers when they fetch covers or acquisition links. If that happens, configure the app to include the API key query parameter on the OPDS base URL, for example `https://prismedia.example.com/opds?ApiKey=...`. Treat URLs containing API keys as secrets.

## NSFW filtering

OPDS uses the same Jellyfin-compatible profile visibility rules as client apps:

- Profiles without NSFW permission only see SFW books and SFW navigation metadata.
- Profiles with NSFW permission can see NSFW books if the library is otherwise available.
- API-key-only OPDS requests are conservative and hide NSFW content.
- Direct book detail, cover, and download routes enforce the same visibility checks as feeds.

Hidden books do not contribute authors, tags, collections, series, counts, covers, search results, or acquisition links.

## Troubleshooting reader setup

| Symptom | Check |
| --- | --- |
| The app shows an SSO login page or cannot add the catalog. | Bypass proxy/forward-auth for `/opds([/?].*)?$` and retry. A request without OPDS credentials should return Prismedia `401`, not an IdP HTML page. |
| The catalog authenticates but shows no books. | Confirm the source directory is a Prismedia library root with **Scan books** enabled, the root is enabled, the scan completed, and the files are EPUB/PDF/CBZ/CBR-compatible. |
| Covers or downloads fail but browsing works. | Use Basic Auth if available. If the reader still drops auth on linked resources, add `?ApiKey=YOUR_API_KEY` to the OPDS base URL. |
| A reader still shows old entries after a fix or rescan. | Refresh/re-sync the OPDS catalog in the app, or remove and re-add the OPDS server. Many readers cache feeds aggressively. |
| NSFW items are missing. | Use a Jellyfin-compatible profile that allows NSFW content. API-key-only catalog requests intentionally hide NSFW content. |

## Reverse proxies

If your reverse proxy uses SSO or forward auth, bypass that proxy auth for `/opds([/?].*)?$` the same way Jellyfin client routes bypass it. The OPDS routes still require Prismedia auth, but reader apps cannot complete an interactive SSO redirect.

See [Reverse Proxy & Auth Middleware](../deployment/reverse-proxy.md) for Authelia, Authentik, Traefik, and Nginx examples.

## Known limitations

OPDS support is catalog, search, cover, and acquisition focused. It does not provide OPDS 2.0 JSON, OPDS-PSE page streaming, annotations, bookmarks, or cross-reader progress sync. Prismedia's built-in reader progress remains available in the web app.
