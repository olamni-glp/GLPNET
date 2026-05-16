# Sources — pglite

The merged bridge that ships in this directory (`pglite_bridge.mjs`, `package.json`) is the consolidation of two upstream lineages: glpnet's own no-pg-gateway hand-rolled investigation at `docs/research/pgbridge-reference/`, and AIGRID's downstream descendant of that investigation at `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/` (pinned `@004a-opskit-sidecar-autospawn`, SHA `83b60585b886e06be9ea2d8954232649962b5d69`). The full classification of every distinguishing feature of either lineage — `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale` — is in [`../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`](../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md).

The citations below cover the entire installable surface of the pattern: **two files in this directory to copy verbatim** (the bridge + its npm manifest), **two glpnet-internal references** that retain historical / contextual value, and **four AIGRID upstream references** for the Python sidecar lifecycle, the SQLAlchemy engine helper, the psycopg type-loader patch, and consumer-side variant guidance.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `prereq-patterns/pglite/pglite_bridge.mjs` | this repo (canonical) | Copy | Merged Node TCP bridge speaking Postgres wire to a single PGLite WASM session. |
| `prereq-patterns/pglite/package.json` | this repo (canonical) | Copy | npm manifest pinning `@electric-sql/pglite@0.2.17`. |
| `docs/research/pgbridge-reference/bridge-direct.mjs` | this repo (canonical) | Read | Glpnet's pre-merge bridge — historical reference for the no-pg-gateway investigation and the two diagnosed bugs (PGLite implicit-Sync, pg-gateway 0.3.0-beta.4 response corruption). |
| `docs/research/pgbridge-reference/README.md` | this repo (canonical) | Read | Narrative of glpnet's bug-discovery journey and the bridge selection rationale. |
| `docs/research/pgbridge-reference/package.json` | this repo (canonical) | Read | Pre-merge npm manifest from glpnet's investigation (`pg-gateway` + `@electric-sql/pglite` pins). Superseded by the merged `prereq-patterns/pglite/package.json`; retained for archival traceability. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/pglite_bridge.mjs` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | The other pre-merge lineage — AIGRID's downstream descendant of glpnet's investigation. The skeleton onto which glpnet's no-pg-gateway startup and two bug fixes were grafted to produce the merged bridge. Source of `globalWorkChain` (A1), synthetic-`ROLLBACK` startup (A2), `endsAtFlushBoundary` (A3, cross-listed with G4), CLI surface `--data-dir/--port/--host/--daemon/--transport` (A9), `[bridge]` log prefix (A10), and `EADDRINUSE` exit-code split (A11). Full row-by-row classification in [`../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`](../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md). |
| `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/opskit_pglite_sidecar.py` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | Python daemon manager for the bridge: start/stop/status/restart with cross-platform detached-spawn flags. |
| `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_engine_kwargs.py` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Copy | Canonical SQLAlchemy `engine_kwargs`: pool_size=1, prepare_threshold=None. |
| `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_compat_loaders.py` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Copy | psycopg type-loader patches for OID-1184 (`timestamptz`) and OID-1114 (`timestamp`); `apply_to_engine(engine)` SQLAlchemy event-listener helper. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-side variant enumeration (DBOS, SQLAlchemy / Flask-SQLAlchemy, Alembic, psycopg) — the basis for [applicability.md](./applicability.md)'s carried-verbatim sections. |

## Per-source notes

### `prereq-patterns/pglite/pglite_bridge.mjs`

- Header comment (lines 1–27) — names the dual lineage and forbids `COPY ... FROM STDIN` against PGLite, full stop. PGLite WASM does not implement COPY-IN over the wire; do not issue it from any consumer.
- `globalWorkChain` (line 54, used at line 163) — global FIFO promise chain across all connections; this is the half of the single-session invariant that lives bridge-side. Every batch from every client is appended to this one chain so PGLite's shared session never sees concurrent `execProtocolRaw()` calls.
- `endsAtFlushBoundary()` (lines 94–106) plus the `FLUSH_TAGS` set (line 46) — wire-batch flush detection. The bridge waits until the buffered bytes end on a Sync / Flush / Terminate / CopyDone / CopyFail boundary before forwarding, so half-batches never hit PGLite mid-pipeline. **This is the implicit-Sync fix from glpnet's investigation.**
- Synthetic `ROLLBACK` on startup (line 126, `try { await pglite.exec('ROLLBACK'); } catch (_e) {}`) — clean-slate handshake. Because PGLite's session is shared across all bridge clients, a prior client that left a transaction in error state would poison every subsequent client until something rolls back.
- `pglite.execProtocolRaw()` call (line 165) — the only wire-protocol forwarding path. `pglite.exec()` (used for the synthetic ROLLBACK on line 126) and `pglite.execProtocolRaw()` (used for forwarded batches) are both PGLite 0.2.x API; `pglite.query()` is NOT used (it is a 0.3.x-only API).
- `BRIDGE_READY port=<port> pid=<pid>` stdout token (line 187) — direct-spawn discovery. Sidecar daemons that prefer `sidecar.json` + TCP-probe discovery may ignore stdout entirely.
- `parseArgs` (lines 197–210) — CLI surface: `--data-dir <path>`, `--port <int>`, `--host <ip>` (default `127.0.0.1`), `--daemon` (disables the stdin-end-exit so the bridge survives a detached startup with `stdin=DEVNULL`), `--transport` (accepted, currently ignored — forward-compat for future Unix-domain-socket transport).

### `prereq-patterns/pglite/package.json`

- Dependency line `"@electric-sql/pglite": "0.4.5"` — pin this exact version. The bridge code uses only the `PGlite.create()` / `pglite.exec()` / `pglite.execProtocolRaw()` surface, which is stable across `0.2.x`–`0.4.x` (no `0.3.x`-only `pglite.query()` blob API is used; COPY interception remains forbidden, A4). The earlier `0.2.17` pin was raised to `0.4.5` because `0.2.17`'s `execProtocolRaw` mishandled an **extended-protocol `ROLLBACK` while the session was in the aborted transaction state** — it returned a malformed fragment with no `ReadyForQuery`, hanging any extended-protocol client (psycopg3 / Npgsql / SQLAlchemy `engine.begin()`), and an extended `SAVEPOINT` in that state hard-crashed the WASM session. Verified by byte-level probe; `0.4.5` (PostgreSQL 17) fixes both. Two upgrade consequences are load-bearing: (1) **on-disk data dirs are NOT forward-compatible** across PGLite minor versions (`0.2.x` = PostgreSQL 16, `0.3.0+` = PostgreSQL 17) — an existing `0.2.x` cluster requires `pg_dump` → fresh `0.4.5` cluster → restore (official upgrade guide); (2) `0.4.x` emits a **doubled trailing `ReadyForQuery` on the error path** (`… E Z Z`) which `pglite_bridge.mjs` coalesces to the single one real Postgres sends (`coalesceTrailingReadyForQuery`) so `Npgsql`/`psqlODBC` do not desync. Full rationale: `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`.
- `"type": "module"` — the bridge uses ES module syntax (`import { ... } from 'node:net'`); keep this field or the bridge will not load.
- `"scripts": { "start": "node pglite_bridge.mjs" }` — convenient entry point. Production / sidecar invocations call `node pglite_bridge.mjs` directly with explicit args; `npm start` is for ad-hoc debugging.
- `"private": true` — this is a copy-target manifest, not a publishable npm package.

### `docs/research/pgbridge-reference/bridge-direct.mjs`

- Pre-merge glpnet bridge (156 lines). Read for orientation on the no-pg-gateway investigation: same hand-rolled startup as the merged bridge; same `endsAtFlushBoundary` batching; no `globalWorkChain` (per-socket sequential dispatch only); no synthetic `ROLLBACK`; uses CLI flags `--pgdir` / `--port` / `--bind` superseded by the merged bridge's `--data-dir` / `--port` / `--host`.
- Includes a `CREATE TABLE IF NOT EXISTS t (x INT); INSERT INTO t VALUES (1), (2), (3);` test-schema seed (lines 19–21) that was an investigation harness — intentionally NOT carried into the merged bridge (`dropped-with-rationale` per `pglite-merge-analysis.md` row G9).
- This file remains in the glpnet repo for archival reasons (see `docs/research/pgbridge-reference/MIGRATED.md`); it is NOT the bridge to copy into a new feature.

### `docs/research/pgbridge-reference/README.md`

- Narrates which of the three reference bridges (`bridge-traced.mjs`, `bridge-batched.mjs`, `bridge-direct.mjs`) was used for which diagnostic step, why pg-gateway 0.3.0-beta.4 was eventually skipped, and how the resulting bridge gives Npgsql / psqlODBC compatibility.
- Read to understand the *why* behind the merged bridge's hand-rolled startup. The merged bridge inherits this lineage transitively via the AIGRID skeleton.

### `docs/research/pgbridge-reference/package.json`

- Pre-merge npm manifest from glpnet's investigation. Pinned `pg-gateway` plus `@electric-sql/pglite` for the reference bridges (`bridge-traced.mjs`, `bridge-batched.mjs`) used during diagnosis; the final `bridge-direct.mjs` no longer needs `pg-gateway` and the merged bridge in this directory has shed it entirely.
- Superseded by `prereq-patterns/pglite/package.json` (which pins only `@electric-sql/pglite@0.2.17`, `"type": "module"`, and the single `start` script); retained alongside the rest of `pgbridge-reference/` for archival traceability per [../../docs/research/pgbridge-reference/MIGRATED.md](../../docs/research/pgbridge-reference/MIGRATED.md).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/pglite_bridge.mjs`

- The second pre-merge lineage. AIGRID's `pglite_bridge.mjs` self-identifies (line 3 of the file) as "originally adapted from GLPNET tools/d2net/src/D2Net.Init/pgbridge/bridge-direct.mjs", so it is itself a downstream descendant of glpnet's `bridge-direct.mjs`. The merged bridge in this directory follows AIGRID's structural skeleton (per the decision in [`../../specs/011-prereq-patterns-catalog/research.md`](../../specs/011-prereq-patterns-catalog/research.md) § B2) with glpnet's no-pg-gateway startup path and two bug fixes grafted on top.
- AIGRID-side contributions present in the merged bridge: `globalWorkChain` global FIFO (A1) — single-session interleaving fix; synthetic `ROLLBACK` on startup handshake (A2) — clean-slate per-connection invariant; `endsAtFlushBoundary()` (A3, cross-listed with glpnet G4); CLI flags `--data-dir / --port / --host / --daemon / --transport` (A9); unified `[bridge]` log prefix (A10); listen-error exit-code split (A11) distinguishing `EADDRINUSE` (5) from generic listen errors (2). External AIGRID lifecycle pieces — `sidecar.json` discovery (A13), Windows `DETACHED_PROCESS` (A14), stale `postmaster.pid` cleanup (A15), TCP readiness probe (A16) — live in `opskit_pglite_sidecar.py` and are cited above.
- AIGRID-side contributions intentionally NOT carried into the merged bridge: the `COPY ... FROM STDIN` interception path (A4–A8). PGLite WASM does not implement COPY-IN over the wire; AIGRID's interception attempted to translate it via the 0.3.x `pglite.query(sql, [], { blob })` API, which glpnet's bridge does not use. Glpnet rejects the interception path categorically — callers MUST NOT issue `COPY FROM STDIN` against PGLite — which is what makes the `0.2.17` pin self-consistent here. Full row-by-row rationale in [`../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`](../../specs/011-prereq-patterns-catalog/pglite-merge-analysis.md).
- Listed as `Read` (not `Copy`): the merged bridge in this directory IS the canonical glpnet artefact; AIGRID's `pglite_bridge.mjs` is an ancestor, retained as a citation target so a reader can audit the merge classification against both pre-merge files.

### `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/opskit_pglite_sidecar.py`

- `cmd_start()` — detached-process spawn with cross-platform handling. Windows uses `subprocess.CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS`; POSIX uses `start_new_session=True`. Reuse this exactly when authoring a glpnet-side sidecar — naive `subprocess.Popen` without these flags will tie the bridge to the parent's lifetime and kill it on parent exit.
- `cmd_stop()` and `cmd_status()` — symmetrical lifecycle commands. `stop` reads the persisted pid, sends SIGTERM (or its Windows equivalent), and unlinks the pid + status files; `status` reports running / stopped / stale based on pid liveness + TCP probe.
- Idempotency check — `start` is a no-op if a live pid + responsive TCP port are already on disk. Important so re-running `start` from a feature's process boot script is safe.
- Stale `postmaster.pid` cleanup — PGLite uses `postmaster.pid` as a single-instance lock, but its written pid is the WASM-internal `-42` rather than a real OS pid. After an unclean shutdown the file lingers and blocks startup. The sidecar unconditionally removes it before spawning the bridge.
- TCP readiness probe loop — `start` polls the listening port at 0.25s intervals until `READINESS_TIMEOUT` and only persists `sidecar.json` after a successful connect. The merged bridge ALSO emits `BRIDGE_READY port=...` on stdout for callers that prefer stdout-token synchronisation; both work.
- Persisted state shape — a one-line pid file plus a `sidecar.json` holding `{host, port, pid, data_dir, role, started_at, managed_by}`. The downstream feature reads `sidecar.json` to discover the port.
- Listed as `Read` (not `Copy`) because the AIGRID sidecar imports AIGRID-internal modules (`opskit._...`) and uses AIGRID-specific path constants — copying it verbatim into a glpnet feature would import broken paths. Use it as a structural model for a glpnet-local sidecar.

### `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_engine_kwargs.py`

- `pglite_engine_kwargs()` function — the canonical SQLAlchemy `engine_kwargs` builder: `pool_size=1`, `max_overflow=0`, `prepare_threshold=None`, `pool_pre_ping=False`, `pool_timeout=300`, plus an `application_name` connect-arg that lands in PG's `pg_stat_activity`. Optional `extra_connect_args` lets a caller layer in additional psycopg knobs.
- Module docstring — the project's stated invariant ("every PGLite consumer MUST serialise its DB access through a queue-of-one connection and disable psycopg's prepared-statement cache") and the consumer variant call-sites in the upstream repo. Read this docstring before modifying the function — it explains *why* each kwarg is set the way it is.
- `__all__ = ["pglite_engine_kwargs"]` — only the helper is intended for re-export. Self-contained module suitable for `Copy` action.

### `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_compat_loaders.py`

- Replaces psycopg 3 built-in OID-1184 (`timestamptz`) and OID-1114 (`timestamp`) loaders, which crash natively on PGLite WASM wire output (Windows `Windows fatal exception: access violation` originating in `psycopg/_cursor_base.py:_select_current_result`).
- `register_pglite_compat_loaders(adapters)` — for raw psycopg consumers; install on every new connection before any `SELECT` of timestamp columns.
- `apply_to_engine(engine)` — SQLAlchemy `connect` event listener that auto-installs the patches on every newly-checked-out psycopg connection. Wire `apply_to_engine(engine)` after every `create_engine(url, **pglite_engine_kwargs(...))` call.
- Patches are benign against real Postgres (return the same `datetime` shape as psycopg's built-ins), so engines that may run against either PGLite or real PG can install them unconditionally. Self-contained module suitable for `Copy` action.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/applicability.md`

- Source for the DBOS / SQLAlchemy / Alembic / psycopg sections in glpnet's [applicability.md](./applicability.md). Carried verbatim where the content describes consumer-class behaviour; scrubbed of AIGRID-internal call-site references (`patch_entry.py`, `ulpani_lms_apply_revision.py`, `ulpani_lms_dbos.py` are mentioned by name as upstream references but glpnet does not depend on them being on disk).
- `### Npgsql` and `### psqlODBC` sections in glpnet's applicability are **new** to this catalog (FR-018 superset rule). They document the .NET stack adaptations that glpnet's investigation discovered.
- Read to confirm any AIGRID-side update to consumer guidance has been mirrored in glpnet's catalog. The two are designed to track each other where the consumer surface overlaps.

## Deferred regression checks (SC-003 / SC-004)

These two checks are NOT run during the catalog-import feature that authored this file. They are documented here verbatim from `specs/011-prereq-patterns-catalog/quickstart.md` Flow D so the first glpnet feature that *adopts* the merged bridge has a turn-key procedure.

### SC-003 — Npgsql / psqlODBC connectivity (100 sequential cycles)

A `psqlODBC` client AND an `Npgsql` client each:
- Connect to the merged bridge.
- Run `SELECT 1`.
- Disconnect cleanly.

Run 100 sequential connect-query-disconnect cycles. **Pass criteria**: zero `lost synchronization with server` errors; both clients succeed every cycle.

Suggested invocation shape (PowerShell):

```powershell
# 1. Spawn bridge in background. Wait for BRIDGE_READY on stdout.
$bridge = Start-Process -FilePath node -ArgumentList @(
    'pglite_bridge.mjs', '--data-dir', '.\pgdir', '--port', '54400'
) -PassThru -RedirectStandardOutput .\bridge-stdout.log -NoNewWindow

# 2. Run 100 cycles per client (use your project's preferred ODBC / Npgsql harness).
1..100 | ForEach-Object {
    Test-PsqlOdbcConnect -Port 54400  # implementer-supplied helper
    Test-NpgsqlConnect -Port 54400
}

# 3. Tear down.
Stop-Process -Id $bridge.Id
```

Any `lost synchronization with server` (or Npgsql `Exception while reading from stream`) within the 100 cycles is a regression — investigate before merging the adopting feature.

### SC-004 — Psycopg-style invariant (concurrent pipeline)

Two simulated `psycopg` clients each fire a `Parse → Bind → Describe → Execute → Sync` pipeline concurrently. **Pass criteria**: responses are not interleaved on the wire; neither client sees `lost synchronization with server`; with `prepare_threshold=None` set, no `DuplicatePreparedStatement` errors.

Suggested invocation shape (Python):

```python
import asyncio, psycopg

async def client(idx, port):
    async with await psycopg.AsyncConnection.connect(
        host='127.0.0.1', port=port,
        user='postgres', password='postgres', dbname='postgres',
        prepare_threshold=None,
    ) as conn:
        for _ in range(50):
            async with conn.cursor() as cur:
                await cur.execute("SELECT %s", (idx,))
                assert (await cur.fetchone())[0] == idx

asyncio.run(asyncio.gather(client(1, 54400), client(2, 54400)))
```

Any assertion failure or `psycopg.OperationalError: lost synchronization with server` is a regression.

## How this catalog file is itself maintained

Per FR-017 (sources.md citation discipline) and FR-005 (format contracts self-contained), the line/column shape of this file conforms to [`../../specs/011-prereq-patterns-catalog/contracts/sources_md_format.md`](../../specs/011-prereq-patterns-catalog/contracts/sources_md_format.md). When AIGRID releases a new pinnable branch with substantive changes to its pglite cluster, the per-source notes here are re-examined — but glpnet's bridge file is the canonical glpnet artefact, not a tracked downstream of AIGRID's. Glpnet evolves the merged bridge independently once a feature adopts it.
