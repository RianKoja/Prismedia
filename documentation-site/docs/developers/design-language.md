---
sidebar_position: 6
title: Design Language
description: Prism Noir Luxe visual direction for Prismedia.
---

# Design Language

Prismedia follows **Prism Noir Luxe**: a dark, refined media-console interface for a private library. The app should feel like high-end editing software and audio rack gear, not a generic SaaS dashboard.

## Principles

- **Controlled radii.** Use the shared radius scale from `radius-xs: 4px` through `radius-2xl: 24px`. Subtle softening is intentional; bubbly pills are not.
- **Material base, glass overlay.** Solid dark surfaces are the page structure. Glass is reserved for floating, interactive, or transient layers.
- **Brass is state.** Brass (`#f2c26a` / `#d59a2a`) marks active, selected, focused, and important state. It should glow, not sit as flat decoration.
- **Mobile first.** Touch layouts come first; desktop expands them.
- **Type has voices.** Cinzel for brand/display, Geist for product headings, Inter for body, JetBrains Mono for utility metadata.
- **Glow and motion carry feedback.** Do not rely on color alone for state.
- **Density is welcome.** Media libraries need scanning, comparison, and repeated action. Keep information dense but organized.

## Palette

| Token | Value | Use |
| --- | --- | --- |
| `bg` | `#07080b` | Page root. |
| `surface-1` | `#0b0e12` | Sidebar, wells, recessed areas. |
| `surface-2` | `#11161d` | Panels and primary containers. |
| `surface-3` | `#202734` | Elevated panels and drawers. |
| `surface-4` | `#2a3038` | Tooltips and contextual overlays. |
| `accent-600` | `#d59a2a` | Accent gradients and deeper active state. |
| `accent-500` | `#f2c26a` | Active labels, focus rings, glow source. |
| `text-primary` | `#f0ede3` | Headings and primary labels. |
| `text-secondary` | `#c8ccd4` | Body text. |
| `text-muted` | `#a4acb9` | Metadata and secondary labels. |

## Geometry

| Token | Value | Use |
| --- | --- | --- |
| `radius-xs` | `4px` | Small chips and inline badges. |
| `radius-sm` | `6px` | Cards, buttons, inputs. |
| `radius-md` | `10px` | Panels and modals. |
| `radius-lg` | `14px` | Large cards and drawers. |
| `radius-xl` | `18px` | Hero sections and large feature panels. |
| `radius-2xl` | `24px` | Full-bleed media containers. |

Do not use unmodified component-library defaults. Match the Prismedia token scale.

## Surface recipes

### Material panel

```css
background: linear-gradient(160deg, var(--color-surface-2), var(--color-surface-1));
border: 1px solid var(--border-subtle);
border-radius: var(--radius-md);
box-shadow: var(--shadow-panel);
```

### Glass card

```css
background: var(--color-overlay-glass);
backdrop-filter: blur(12px);
border: 1px solid var(--border-default);
border-radius: var(--radius-sm);
box-shadow: var(--shadow-card);
```

### Active state

```css
border-color: var(--border-accent-strong);
box-shadow: var(--shadow-glow-accent-strong);
color: var(--color-accent-500);
```

## UI checklist

Before shipping a UI change:

1. Check the smallest supported mobile viewport.
2. Check desktop density and scanability.
3. Confirm primary actions work without hover.
4. Confirm text fits inside controls and cards.
5. Confirm active/focus state is not color-only.
6. Confirm radii come from the shared scale.
7. Confirm the page does not read as generic SaaS or unmodified shadcn defaults.
