# foliate-js (vendored)

Reflowable/fixed book rendering engine used by Prismedia's book reader for EPUB
(and, in Phase 2, PDF). Vendored rather than installed because foliate-js is not
published to npm.

- Source: https://github.com/johnfactotum/foliate-js
- Pinned commit: `78914aef4466eb960965702401634c2cb348e9b1`
- License: MIT (see `LICENSE`)

Embedding entry point is `view.js`, which defines the `<foliate-view>` custom
element. `vendor/` holds foliate's own bundled dependencies (zip.js, fflate, and
pdf.js/cmaps/standard_fonts under `vendor/pdfjs`). foliate renders both EPUB and PDF,
so Prismedia's single book reader uses it for both. `pdf.js` and `vendor/pdfjs` load
lazily and only when a PDF is opened.

Local modifications (re-apply when updating the pinned commit):

- `pdf.js`: the asset base in `pdfjsPath` was made relative (`./vendor/pdfjs/...`) so
  Vite's `new URL(..., import.meta.url)` resolution accepts it, and the two layer-CSS
  values are imported at build time via `?raw` instead of being fetched with a
  module-level top-level `await` (which our build target does not support).
- `vendor/pdfjs`: the `*.map` source maps (~7.7MB) were omitted; the runtime `.mjs`,
  CSS, `cmaps/`, and `standard_fonts/` are kept.

Dev/build files (`reader.js`, `reader.html`, `tests/`, `rollup/`, config) were not
vendored. Update by re-copying the runtime modules from a newer pinned commit.
