# background-task-manager

Status: draft

## What this produces

A registry that tracks every background task running on the developer machine for this stack — its prerequisites, dependents, fire-up command, shutdown command, health probe, and lifecycle state — together with a coordinator that brings tasks up in dependency order and tears them down in safe-shutdown reverse order. The registry's own state lives in [`pglite`](../pglite/description.md), the always-required bootstrap and persistent store. Non-config history (operational logs, retry traces, telemetry, audit events) is routed to the off-repo glpnet datalake destination per [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2). Authentication tokens are never persisted in cleartext per [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1).

This pattern carries the **concrete realisations** of both cross-cutting policies, per the allocation discipline. Other patterns (`dbos`, `flask-sqlalchemy-alembic-api`) cross-link to the policies; this pattern says *how* the policies are realised in the data plane.

## Why it matters

As the stack grows past two or three background services, hand-rolling startup/shutdown order is a defect surface. The pattern is a registry pattern — explicit prereq/dependent edges + a single source of truth (pglite) — that prevents whole classes of "service started before its dep" bugs and makes safe-shutdown trivial (topological reverse traversal). The hatzinor `ulpani_pglite_sidecar.py` is the closest existing model upstream: a single-task daemon manager whose state lives in `sidecar.json` paired with pglite. This pattern generalises that from N=1 to N>1 and lifts the per-task state into a pglite-backed registry.

### Task-record schema

The registry table holds one row per task with the following fields:

| Field | Type | Notes |
|---|---|---|
| `id` | text PK | Stable identifier, e.g. `pglite-sidecar`, `bg-flask-api`. |
| `prereqs` | text[] | IDs of tasks that MUST be `running` before this task starts. |
| `dependents` | text[] | IDs of tasks that depend on this task; computed view (or maintained by the manager). |
| `fire_up_cmd` | text | The shell command that brings the task up. |
| `shutdown_cmd` | text | The shell command that takes the task down gracefully. |
| `health_probe` | text | Either a TCP probe (`tcp:127.0.0.1:<port>`), an HTTP probe (`http:<url>`), or a sentinel-file check (`file:<path>`). |
| `lifecycle_state` | text | One of `stopped`, `starting`, `running`, `stopping`, `failed`. |
| `secrets_ref` | text NULL | Pointer into [`local-secrets-store`](../local-secrets-store/description.md) when the task needs a secret. NEVER the secret itself. See [Policy 1 realisation](#policy-1-realisation-data-plane) below. |

### Why pglite is the always-required bootstrap

Every task in the registry has the pglite sidecar as a transitive prereq, because the registry's table itself lives in pglite. Bringing up the manager is therefore a two-step bootstrap: (1) bring the pglite sidecar up via the [`pglite`](../pglite/description.md) pattern's `cmd_start()`; (2) connect the manager to pglite and read the registry. There is no escape hatch where the manager runs without pglite — the design intentionally collapses the bootstrap order to a single fixed root.

### Policy 1 realisation (data plane)

Per [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1), the registry's data plane MUST NOT persist authentication tokens in cleartext. The realisation:

- The `secrets_ref` column stores **only a pointer** into the [`local-secrets-store`](../local-secrets-store/description.md) — never the secret itself. The secret material is fetched at task-fire-up time via the secrets-store's `fetch(name)` interface, passed to the spawned process via environment variable or command-line argument, and never written back to pglite.
- If the registry needs to store a hash of a secret (e.g. for a credential-presentation challenge), it uses the chosen v1 hash algorithm and parameters named in [`local-secrets-store/description.md`](../local-secrets-store/description.md). The hash algorithm itself is not chosen here; the cross-link to `local-secrets-store` is.

### Policy 2 realisation (non-config history off-repo)

Per [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2), non-config history routes off-repo to the glpnet datalake destination. The realisation:

- **Runtime config-key**: `GLPNET_DATALAKE_PATH` — an environment variable resolved at process start. If unset, defaults to `D:/BSTDEV/research/glpnet-datalake/background-task-manager/<data-class>/<partition>.parquet`. Concrete bootstrap of the datalake tree is deferred to a future glpnet feature; the env var resolves whether or not the destination is bootstrapped (the unreachable-destination fallback below covers the not-bootstrapped case).
- **Per-pattern destination filename**: each consuming pattern's destination follows the convention `<pattern-or-app>/<data-class>/<partition>.parquet` under the configured root. For this pattern itself: `background-task-manager/<data-class>/<partition>.parquet`. A consuming pattern that emits its own non-config history (e.g. `flask-sqlalchemy-alembic-api`'s request log) names a different sub-path, e.g. `flask-sqlalchemy-alembic-api/<data-class>/<partition>.parquet`.
- **Swappable-interface boundary**: the manager talks to a `DatalakeDestination` interface with the four methods `connect()`, `write_record(table_name, record_dict)`, `flush()`, `health_probe()`. The v1 backend implements this against the local Parquet partitioning. A v2 (federated, sibling-of-repo, or cloud-resident) is a backend swap behind this interface, not a consumer rewrite.
- **Unreachable-destination fallback**: if `connect()` fails (file locked, disk full, destination not yet bootstrapped, drive missing), the manager logs a degraded-mode warning to stderr, buffers up to `BUFFER_BYTES_MAX` (default 16 MiB) of records in an in-process queue, and retries `connect()` on a back-off (initially 5 s, doubling to 5 min). On overflow, the oldest records are dropped (the buffer is non-durable by design — the durable record is the live registry in pglite). The fallback path **MUST NOT** be inside this repo's working tree; if for some reason a developer wants to inspect the buffer, it is dumped to stderr or to a `--debug-buffer-dump <path>` argument outside the repo. Committing the buffer into this repo as a workaround is forbidden.

### Inclusion-list compliance for pglite

Per [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2)'s inclusion list, the pglite-resident records for this pattern are: registry rows (one per task), current task state (the `lifecycle_state` column), prereq/dependency edges (the `prereqs` and `dependents` columns), the schema itself, and secrets-store metadata (the `secrets_ref` column). Everything else — fire-up traces, lifecycle-event log, health-probe history, retry telemetry — defaults to the glpnet datalake destination.

## How a feature uses this pattern

This pattern is `Status: draft`. The CLI shape (`bgtm bootstrap`, `bgtm register`, `bgtm up`, `bgtm down`, `bgtm status`, `bgtm logs`) and the registry schema above are the conceptual surface; the concrete implementation lands in the first glpnet feature that adopts this pattern. Promotion to `active` happens once a downstream consumer has exercised the registry end-to-end with at least two interdependent tasks (e.g. the pglite sidecar + a downstream Flask API).

The closest existing on-disk model is hatzinor's `ulpani_pglite_sidecar.py` (single-task variant); see [sources.md](./sources.md) for the citation. The reusable parts of that reference (cross-platform detached spawn; idempotent start/stop; readiness-probe loop) generalise directly; everything else is per-task.
