# blazor-spa-bg-api

Status: draft

## What this produces

A research-grounded, experimentally-validated assembly pattern for shipping a browser-side **Blazor WebAssembly** SPA whose backend is the [`flask-sqlalchemy-alembic-api`](../flask-sqlalchemy-alembic-api/description.md) Flask service running as a background task, with a websocket content endpoint for live updates. The deliverable is an indexed pattern; the research and experimental validation produce a minimal Blazor-WASM client + a websocket-augmented Flask service that the consuming feature can copy and adapt.

This pattern is the only one in this catalog import with no on-disk hatzinor reference. It is research-driven by design — the trusted-web sources cited in [sources.md](./sources.md) cover Blazor WebAssembly hosting, Flask + websocket integration, and bg-task hosting, and any artefact glpnet produces during validation lands as an `Action: Copy` row under glpnet's branch.

## Why it matters

A common shape for AI-augmented developer tooling: a browser UI for inspection and command issuance, served by a background API that is local to the developer machine and that pushes live state to the UI as long-running operations progress. The pieces are individually well-trodden:

- **Blazor WebAssembly** — a static-served front-end that can be hosted from any web server (or directly from the bg API); the runtime is the browser's WASM engine. No server-side render dependency.
- **Flask + websocket** — Flask is the API substrate (per [`flask-sqlalchemy-alembic-api`](../flask-sqlalchemy-alembic-api/description.md)), and a websocket endpoint added via `flask-sock` (or equivalent) serves the live-update channel.
- **Background-task hosting** — the API runs as a daemon under [`background-task-manager`](../background-task-manager/description.md), with prereq edges to the pglite sidecar and any required SPA static-asset server.

What is novel about this assembly is composing them with the substrate's single-session-pool constraints honoured end-to-end: the websocket endpoint MUST NOT hold a SQLAlchemy session for the lifetime of the connection, or the single-session pool will deadlock against any other API request. The pattern's experimental validation will pin a specific event-pump pattern (poll-then-push, or a write-side publish channel) that satisfies this constraint.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it, and no on-disk reference implementation exists upstream. Read the trusted-web sources cited in [sources.md](./sources.md) for the canonical Blazor-WebAssembly hosting story and the Flask websocket story. Adapt to glpnet's environment by copying any experimentally-validated artefacts produced during research; until that validation lands, the pattern is research-grounded only. When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active`, fleshing out [applicability.md](./applicability.md), and updating [../directory.md](../directory.md)'s suffix.

## Cross-cutting policies

This pattern is NOT on either policy's `Applies to` list in v1. The Flask substrate it composes IS on Policy 1 + Policy 2's lists, but at a bg-api integration level (the [`flask-sqlalchemy-alembic-api`](../flask-sqlalchemy-alembic-api/description.md) pattern), not at this assembly's level. If a future revision wires the websocket-endpoint's connection logs into the glpnet datalake destination, that PR adds this pattern to [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2)'s `Applies to` list and adds a cross-link from this `description.md` then.
