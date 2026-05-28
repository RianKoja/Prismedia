# @prismedia/contracts

This TypeScript package owns frontend-only constants, media helpers, upload
helpers, and plugin protocol types shared by the Svelte app and TypeScript
plugin tooling.

It must not own server API contracts, database schema, queues, or worker
behavior. New .NET API request and response shapes belong in
`apps/backend/src/Prismedia.Contracts` so OpenAPI and Orval remain the public
contract source for HTTP surfaces.
