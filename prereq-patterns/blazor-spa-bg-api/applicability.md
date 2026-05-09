# Applicability — blazor-spa-bg-api

Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.

The upstream catalog's `applicability.md` covers consumer-class adaptation for four assembly shapes: a static-served Blazor SPA (CORS / origin considerations), a bg API on a developer laptop (the simplest case), the same bg API hosted under [`background-task-manager`](../background-task-manager/description.md) (the production-shape on a developer laptop), and the websocket endpoint serving live data from the Flask + SQLAlchemy + Alembic API (the most-novel sub-case — substrate-friendly endpoint shapes like poll-then-push and publish-channel + write-side trigger). See [sources.md](./sources.md) for the citation. When a glpnet consumer arrives, that feature's PR adds substantive `### <consumer-name>` sections here.
