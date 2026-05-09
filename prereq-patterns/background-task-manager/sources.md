# Sources — background-task-manager

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. AIGRID's `prereq-patterns/background-task-manager/` index reaches into hatzinor's `ulpani_pglite_sidecar.py` (the closest single-task model) plus systemd / supervisord canonical references for the multi-task vocabulary, DuckDB DuckLake reference for the off-repo destination substrate, and three RFC-level secret-hash references (Argon2id RFC 9106; scrypt RFC 7914; bcrypt USENIX paper). Glpnet has no own implementation today.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — registry schema, pglite-as-bootstrap, Policy 1 + Policy 2 concrete realisations. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for the four topology shapes. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's upstream-source citations: hatzinor sidecar, systemd unit-dependency vocabulary, supervisord, DuckDB DuckLake, Argon2id/scrypt/bcrypt RFCs, and forward-references to local-secrets-store + policies.md. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/description.md`

- The registry table schema (id, prereqs, dependents, fire_up_cmd, shutdown_cmd, health_probe, lifecycle_state, secrets_ref) is the load-bearing data shape a glpnet adopter MUST honour.
- Policy 1 + Policy 2 concrete realisations — `secrets_ref` as pointer-only (never the secret); off-repo datalake destination behind a swappable-interface boundary; unreachable-destination fallback to in-process buffer (NEVER into the repo working tree).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/applicability.md`

- Per-topology H3s: minimal (pglite alone); single-dep (`prereqs=['pglite-sidecar']`; auto-prereqs option); two-services-with-edge (topological order over the prereq DAG; cycles refused at registration); safe-shutdown (reverse traversal; no-running-dependents guard; SIGTERM → 5s → SIGKILL escalation).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/background-task-manager/sources.md`

- Cites `D:/BSTDEV/lang/hatzinor_ai-ddp/src/ulpani_pglite_sidecar.py@develop` (`olamni-research/hatzinor_ai-data-driven-publishing@develop`, Action: Read) — the closest single-task daemon model. Reusable parts: cross-platform detached spawn (Windows `CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS`; POSIX `start_new_session=True`); idempotent start/stop; TCP readiness-probe loop. Per-task cleanup (e.g. stale `postmaster.pid`) does NOT generalise.
- Cites systemd unit-dependency canonical reference (`https://www.freedesktop.org/software/systemd/man/latest/systemd.unit.html`) — vocabulary mapping (Requires=, Wants=, After=, Before=) and lifecycle states (inactive ≈ stopped; activating ≈ starting; active ≈ running; deactivating ≈ stopping; failed ≈ failed).
- Cites supervisord (`http://supervisord.org/configuration.html`) as alternative reference for the simpler `priority=` model.
- Cites DuckDB DuckLake (`https://duckdb.org/docs/extensions/ducklake.html`) — substrate-relevant for the off-repo destination at the AIGRID side; for glpnet, the destination per Policy 2 is the parquet-based `glpnet-datalake/` tree, but the swappable interface absorbs either.
- Cites three secret-hash RFCs: Argon2id RFC 9106 (`https://datatracker.ietf.org/doc/html/rfc9106`), scrypt RFC 7914 (`https://datatracker.ietf.org/doc/html/rfc7914`), and the bcrypt USENIX 1999 paper. Concrete chosen-algorithm parameters live in `local-secrets-store/description.md`.
- Cites two sibling forward-references: `prereq-patterns/policies.md` (the cross-cutting rules); `prereq-patterns/local-secrets-store/description.md` (the secrets-store the `secrets_ref` column points into).
