---
sidebar_position: 2
title: Users & NSFW Servers
description: Sign Jellyfin clients in with Prismedia user accounts and split SFW/NSFW access per user.
---

# Users & NSFW Servers

Jellyfin clients sign in with a **username and password** — and in Prismedia those are simply your **user accounts**. The same credentials sign in to the web app, Jellyfin-compatible clients, and OPDS readers. There is no separate client key.

## Managing users

Administrators manage accounts in **Settings → Users**:

| Field | Meaning |
| --- | --- |
| **Username** | The name the client signs in as (must be unique). |
| **Display name** | Optional friendlier label. |
| **Role** | Administrators manage the server and see every library; members see granted libraries. |
| **Allow NSFW** | Whether this user may see NSFW-flagged content. |
| **Library access** | The libraries a member can see (admins always see everything). |
| **Enabled** | Disabled users cannot sign in; disabling ends their sessions. |

You can edit, disable, or delete users at any time, and reset a user's password from the same screen. Deleting or disabling a user, or resetting their password, ends their sessions — clients sign in again with the new credentials.

:::info Upgrading from pre-2.0
Former Jellyfin sign-in profiles become real member accounts automatically, and their password is the **previous server API key**, so connected clients keep working. See [Authentication & User Accounts](../deployment/authentication.md#upgrading-from-pre-20).
:::

## NSFW "servers"

Because NSFW visibility is **per user**, you can present the same library two ways and add each as a **separate server** in your client app:

```text
User "family"     Allow NSFW = off   →  client server A (no adult content)
User "me"         Allow NSFW = on    →  client server B (everything)
```

In Infuse/Manet you add two Jellyfin servers pointing at the same Prismedia URL, signing in as each user. The "family" server never shows NSFW items (they're filtered out of listings, search, artwork, and playback); the "me" server shows everything.

Per-user **library access** works the same way: a member's client only ever sees the libraries an administrator granted them.

## How sign-in works

1. The client sends the **username** and **password** to `/Users/AuthenticateByName`.
2. Prismedia verifies them against the enabled user account.
3. It issues a session token the client stores and sends on every request.
4. Each request is resolved back to its user, and library access, NSFW filtering, and per-user watch state apply accordingly.

Continue to [Connecting Infuse & Manet](./clients.md).
