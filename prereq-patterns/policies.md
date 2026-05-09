# Catalog policies

This file is the single canonical home of cross-cutting rules that apply across multiple patterns in this catalog. Affected patterns MUST cross-link to the relevant policy below; they MUST NOT restate the policy text. Drift between this file and a pattern's restatement is a defect by construction (see `## Cross-link rule` at the bottom of this file).

## Policy 1 — No cleartext auth tokens (FR-CC-1)

**Rule.** No pattern's data plane MAY persist authentication tokens in cleartext. A secret MAY be persisted in hashed form ONLY when (a) the pattern's `description.md` carries an explicit written justification and (b) the hash uses a memory-hard or work-factor-tuned password-hashing primitive — Argon2id, scrypt, or bcrypt.

**Specifics.** The minimum-bar family for secret material is exactly `{Argon2id, scrypt, bcrypt}`. Forbidden for secret material: raw SHA-1, SHA-2 (SHA-256/384/512), SHA-3, and MD5. The chosen algorithm and its parameters (e.g. Argon2id memory cost, time cost, parallelism) are named in the affected pattern's `description.md`, not here.

**Applies to.**

- `dbos`
- `flask-sqlalchemy-alembic-api`
- `background-task-manager`
- `local-secrets-store`

Plus any future pattern that touches secrets — add it to this list when the pattern lands.

**Concrete details live in.** [`local-secrets-store/description.md`](./local-secrets-store/description.md) (chosen v1 hash algorithm and parameters). [`background-task-manager/description.md`](./background-task-manager/description.md) (data-plane realisation of the forbidden-cleartext rule — registry rows store secrets-store metadata only, never the secret itself).

## Policy 2 — Non-config history off-repo to glpnet datalake (FR-CC-2)

**Rule.** Configuration history needed for ongoing operation lives in pglite (the canonical store from the [`pglite`](./pglite/description.md) pattern). Non-config history — operational logs, traces, metrics, audit events, agent-action telemetry, retry history, ephemeral records — MUST be routed to an off-repo destination and MUST NOT be committed to this Git repository.

**Specifics.** The destination convention for glpnet is `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`. This path lives off-repo as Policy 2 requires (sibling to the glpnet repo at `D:/BSTDEV/research/GLP/GLPNET/`, not inside it). Concrete bootstrap of the destination — creating the directory tree, defining ingest modes, writing settings — is OUT OF SCOPE of feature 011 and is explicitly deferred to a future glpnet feature; only the path convention is pinned here. The destination remains behind a swappable interface so a future backend (federated, sibling-of-repo, cloud-resident) is a backend swap, not a consumer rewrite. On a host without the destination bootstrapped, emitters buffer to a per-repo durable outbox (size-bounded, drop-oldest on overflow) and drain on host bootstrap.

**Inclusion list for pglite:** registry rows, current task state, prereq/dependency edges, schema, secrets-store metadata. Everything else defaults to the glpnet datalake destination. Adding a record type to the pglite list requires explicit written justification in the affected pattern's `description.md`.

**Applies to.**

- `dbos`
- `flask-sqlalchemy-alembic-api`
- `background-task-manager`

Plus any future pattern that emits non-config history — add it to this list when the pattern lands.

**Concrete details live in.** [`background-task-manager/description.md`](./background-task-manager/description.md) (the runtime config-key resolving the destination, the per-pattern destination filename, and the unreachable-destination fallback).

## External-sibling note

A separate AIGRID-side datalake convention named "BreenLake DuckLake" exists as an external sibling and may share the host with glpnet's datalake. BreenLake is NOT a glpnet artefact and glpnet's catalog does NOT depend on its presence. The sibling is mentioned here for traceability only; glpnet's Policy 2 destination is the path named under **Specifics.** above, independent of BreenLake's existence.

## Cross-link rule

Affected patterns MUST cross-link to the relevant policy section above. The cross-link is the affected pattern's machine-checkable assertion that it has read and is bound by the policy; anchor links to the relevant policy section are preferred (e.g. `[Policy 1](./policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1)`).

Affected patterns MUST NOT restate the policy text. They MAY reference the rule in their own words (e.g. "as required by [Policy 1](./policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1)"), but they MUST NOT copy the canonical `**Rule.**` paragraph verbatim. Drift between this file and a pattern's restatement is a defect by construction. v1 enforces the no-restatement invariant by review; a future linter is straightforward (string-similarity check across pattern files versus this file's `**Rule.**` paragraphs).
