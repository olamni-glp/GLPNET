# Contract — Keeper lifecycle (US3)

The keeper owns a **per-run isolated PGLite store** outside the repo and is a thin lifecycle over
`codeconv.bridge_client` pointed at the run's `data_dir` (= `<marathon-root>/<run_id>/pgdb`). It does **not**
re-implement a supervisor (D1/D5).

## Endpoint (FR-012)
- `start_keeper(run)` calls `bridge_client.acquire_or_discover(repo_root=<toolchain_repo_root>, data_dir=<store_root>/pgdb)`.
  `repo_root` is the **toolchain checkout** that owns the unified bridge script
  (`prereq-patterns/pglite/pglite_bridge.mjs` + its `node_modules`) — NOT the off-repo `store_root` (which holds
  no bridge asset) and NOT the run's scoped-commit `repo_dir` (a possibly-throwaway work-repo). The bridge is keyed
  on `data_dir`, so the per-run cluster stays off-repo and isolated while the script comes from the toolchain
  (the two are decoupled — `bridge_client`). This spawns the per-run bridge (speculative spawn; the `mkdir` of
  `<data_dir>.bridge.lock/` is the mutex) or fast-paths an existing fresh one, and registers this process as a
  consumer.
- The connection endpoint is the bridge **sidecar** (host/port/pid/heartbeat) under the store root; published
  on start and reused by subsequent operations. `engine_for(run)` builds the SQLAlchemy engine on it and runs
  `ensure_schema` once.
- `Endpoint` = `(host, port, pid, data_dir)`.

## Graceful shutdown (FR-013)
- `stop_keeper(run)` → `bridge_client.request_force_shutdown(data_dir=<store_root>/pgdb)` writes the
  non-destructive `.shutdown` marker; the bridge flushes pending state and exits on its next tick. The next
  `start_keeper` finds a consistent store needing **no** recovery.

## Stale-residue recovery (FR-014) — automatic, no manual deletion
- `recover_keeper(run)` (and the recovery path inside `engine_for`) runs `acquire_or_discover` again:
  - **Stale endpoint, dead process** (heartbeat older than freshness window, TCP unreachable): treated as
    recoverable residue — re-spawn via the `mkdir` mutex. (Edge case: "keeper endpoint stale but process
    dead" → recoverable, not a hard failure.)
  - The reused 024 doctrine: clear documented stale lock/lifecycle residue automatically; never require a
    manual file delete.

## Single-writer (FR-015) — refuse, distinct from recoverable
- The consumer registration is a kernel-fd lock; a **second concurrent writer** attaching to a *live* store
  is refused (or serialised) — surfaced as a `ConcurrentWriter`-class condition with a message **distinct**
  from a stale-residue condition (FR-016). A live bridge child is **never killed** to "recover".

## Store-unavailable & integrity (FR-016)
- A genuinely unavailable store or an integrity failure surfaces as a clear, actionable error
  (`StoreUnavailable` / `IntegrityFailure`) — separable, in message and exit code, from the recoverable
  stale-residue path. Non-NTFS/ReFS `data_dir` fails fast at exit 64 (`_check_data_dir_filesystem`).

## Isolation invariant
- The per-run store root MUST be **outside the working repo tree** (assert at resolve time; refuse a root
  inside the git top — FR-027). Default marathon root is a guaranteed-NTFS user-level path (research F4).
- The shared repo `<repo>/.pgdb/` cluster is never touched by the marathon keeper (Constitution VI-b
  deviation is scoped to the *separate* per-run cluster — see plan Complexity Tracking).
