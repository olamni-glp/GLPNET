# dbos

Status: draft

## What this produces

A working DBOS install bound to the local pglite database via the bridge + single-session pool from the [`pglite`](../pglite/description.md) pattern, with agent actions, agent-action Python tool calls, and hybrid agent/code flows registered as DBOS workflows. The deliverable is: an importable `get_dbos_app()` factory that returns a configured DBOS singleton, a one-time `bootstrap_dbos_schema()` step, and the per-call decorator pattern (`@DBOS.workflow()` / `@DBOS.step()`) by which a feature's actions become durable.

## Why it matters

DBOS gives every wrapped agent action exactly-once side-effect semantics under interruption, distraction, or process restart. Half-applied state stops being visible to subsequent reads — either the workflow committed, or it is replayed cleanly from the last checkpoint. Without this layer, every agent author re-derives the durability contract by hand and inevitably leaves a partial-failure surface where one tool call landed and the next didn't.

DBOS sitting on pglite is not free, though: pglite is a single-session WASM Postgres, and DBOS's defaults assume a multi-connection server. Three load-bearing constraints come from the [`pglite`](../pglite/description.md) pattern and MUST be honoured:

- `sys_db_pool_size=1` — DBOS's default `pool_size=20` deadlocks against the shared session.
- `use_listen_notify=False` — DBOS's LISTEN/NOTIFY thread otherwise holds the lone connection forever; with it off, DBOS polls `dbos.notifications` instead. Polling is acceptable for a single-operator workstation workload.
- A migration patch that strips `CREATE EXTENSION "uuid-ossp"` from DBOS's `migration_one`. PGLite ships without `uuid-ossp`; without the strip, DBOS startup fails on a fresh data directory.

Pre-flight: the `dbos` schema MUST exist in pglite before `get_dbos_app()` is called. The factory refuses to fall back to non-durable execution; if the schema is missing it raises `DbosSchemaMissingError` and the caller is expected to run a one-shot `bootstrap` command first. This is the durability contract: durable or not at all.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it. The full installable surface (the DBOS-on-pglite wiring module, the durability-contract integration tests, the refusal-to-start-without-schema invariant) is consolidated upstream in AIGRID's catalog; see [sources.md](./sources.md) for the citations and pin information. When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active`, fleshing out [applicability.md](./applicability.md) with substantive consumer-class content for the adopting glpnet stack, and updating [../directory.md](../directory.md)'s suffix.

## Cross-cutting policies

This pattern is on the [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1) `Applies to` list because DBOS's durable-execution surface may persist authentication-bearing context as part of a workflow's input. Concrete realisation lives elsewhere — see [`background-task-manager/description.md`](../background-task-manager/description.md) for the data-plane realisation and [`local-secrets-store/description.md`](../local-secrets-store/description.md) for the chosen v1 hash algorithm and parameters; this pattern only cross-links and does not restate the rule.

This pattern is also on the [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2) `Applies to` list. Workflow-state schema and current-checkpoint rows are pglite-resident (they are config history needed for ongoing operation per the inclusion list); workflow-execution telemetry and step-history records emitted for observability route to the off-repo glpnet datalake destination per the policy. Concrete details — runtime config-key, destination filename, unreachable-destination fallback — live in [`background-task-manager/description.md`](../background-task-manager/description.md).
