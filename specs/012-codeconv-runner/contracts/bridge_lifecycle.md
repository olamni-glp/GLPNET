# Contract: Bridge lifecycle protocol

Source: spec FR-002, FR-003, FR-004, FR-005, FR-006, FR-030; clarifications Q2, Q3, Q10, Q16; research R1, R2, R3, R4, R9.

This contract is shared by EVERY PGLite-using tool in the repo (Python `codeconv`, .NET `D2Net.*`, future tools). All clients implement the same protocol.

## States

A bridge for `.pgdb/` is in exactly one of:
- **Absent** — no process is currently holding `.pgdb.bridge.lock`.
- **Starting** — a process holds the lock but has not yet emitted `BRIDGE_READY`.
- **Ready** — a process holds the lock, has written `.pgdb/bridge.json`, and has emitted `BRIDGE_READY` on its stdout.
- **Stopping** — a process holds the lock and is mid-shutdown (after receiving SIGTERM/SIGINT).

**Note on the lock path**: the lock is placed SIBLING to the data dir (`<data-dir>.bridge.lock`, i.e. `.pgdb.bridge.lock/` for the canonical case) rather than inside it. PGLite refuses to initialize a fresh data-dir that has any non-PG file present at init time, and `proper-lockfile` creates its lock as a directory; placing the lock outside `.pgdb/` is the simplest fix. Earlier draft wording placed the lock at `<data-dir>/.bridge.lock`; this contract supersedes that.

State transitions are externally observable only as **Lock held + sidecar present + TCP responsive** (= Ready) versus **Lock not held** (= Absent or transient between processes).

## Client startup (FR-006)

EVERY client MUST follow this exact sequence:

```
1. Resolve repo root (cwd or upward search to a marker — implementation choice).
2. ENSURE_DIR(.pgdb/)
3. lock_handle ← TRY_ACQUIRE_LOCK(.pgdb.bridge.lock, retries=0)
4. IF lock_handle.acquired:
     # Path A — bridge owner
     pipe ← SPAWN_DETACHED(node pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon)
     line ← READ_LINE(pipe.stdout, timeout=10s)
     IF line matches "BRIDGE_READY port=N pid=P":
         port, pid ← parse(line)
         ASSERT_FILE_EXISTS(.pgdb/bridge.json)
         CLOSE pipe.stdout, pipe.stderr (drain to bridge.log from now on)
         RETURN BridgeEndpoint(host=127.0.0.1, port=port, owned=lock_handle)
     ELSE:
         KILL spawned process; RELEASE lock; RAISE BridgeStartupTimeout
5. ELSE:
     # Path B — bridge consumer
     sidecar ← READ_JSON(.pgdb/bridge.json)
     IF sidecar absent OR malformed:
         RAISE BridgeRaceLost (no sidecar yet — caller may retry once after 250ms)
     RETURN BridgeEndpoint(host=sidecar.host, port=sidecar.port, owned=null)
```

**Invariants**:
- `retries=0` on lock acquisition is mandatory — clients fail fast (Clarification Q3 + SC-001).
- `--port 0` (ephemeral) is the default (R3); operators may override.
- The READ_LINE step BLOCKS the spawning client; this is correct — the client must not connect TCP before `BRIDGE_READY` (race vs `listen()`).
- Once `BRIDGE_READY` is consumed, the client closes its read end of the pipe. From that point, the bridge writes to `.pgdb/bridge.log` (FR-030) and never to a parent terminal.

## Bridge startup (server side)

```
1. Parse args (--data-dir, --port, --host, --daemon, --transport).
2. lock ← ACQUIRE_LOCK(<data-dir>/.bridge.lock, retries=0)
   IF lock fails:
     existing ← READ_JSON(<data-dir>/bridge.json)
     STDERR: "[bridge] BRIDGE_LOCK_HELD by pid=<existing.pid> at <existing.host>:<existing.port>"
     EXIT 5
3. pglite ← AWAIT PGlite.create(<data-dir>)
   IF fails: STDERR "[bridge] BRIDGE_ERROR pglite_init_failed <msg>"; EXIT 1
4. server ← createServer(connection_handler).listen(<port>, <host>)
   On listen success: resolved_port ← server.address().port
5. WRITE_ATOMIC(<data-dir>/bridge.json, {host, port: resolved_port, pid, started_at, data_dir, role: "primary", managed_by: "auto-spawn"})
6. STDOUT: "BRIDGE_READY port=<resolved_port> pid=<pid>\n"  (FLUSHED)
7. After --daemon: redirect stdout, stderr → <data-dir>/bridge.log (size-rotated 5MB×3, R9)
8. Install handlers:
     SIGTERM, SIGINT, beforeExit → graceful shutdown:
         server.close()
         await pglite.close()  (if API supports)
         try unlink bridge.json
         (kernel releases lock on exit)
```

## Lock semantics

- **Implementation**: `proper-lockfile` (Node bridge), `System.IO.FileStream FileShare.None` (.NET clients), `proper-lockfile`-equivalent or `fcntl.LOCK_EX|LOCK_NB` (Python clients).
- **Granularity**: whole `.pgdb/` directory, not per-table.
- **Release**: kernel-managed on process exit. Clients MUST NOT call `releaseLock()` explicitly except when intentionally relinquishing (which is unusual — bridge owners hold for life).
- **Stale handling**: NONE required by spec (Clarification Q3). Absent-of-lock IS authoritative.

## Sidecar trust rule (FR-006 step 3 + Edge Case)

A sidecar JSON file's existence does NOT imply a bridge is running. ONLY a held lock implies that. Clients that fail to read the sidecar after losing the lock race MUST retry the lock acquisition exactly once after 250ms (covers the brief window between `listen()` and `WRITE_ATOMIC(bridge.json)`).

## Shutdown

- Graceful: SIGTERM / SIGINT → server.close → bridge.json delete → exit (kernel releases lock).
- Crash / SIGKILL: kernel releases lock; bridge.json lingers; clients ignore lingering sidecar when lock is unheld.

## Failure modes and exit codes (bridge process)

| Code | Reason |
|------|--------|
| 0 | Graceful exit |
| 1 | PGLite init failed (`BRIDGE_ERROR pglite_init_failed`) |
| 2 | Generic listen error |
| 5 | Lock held by another bridge OR `EADDRINUSE` on explicit `--port` |
| 9 | Sidecar JSON write failed (cannot serve clients without it) |

## Acceptance tests

- `prereq-patterns/pglite/tests/lock_single_writer.test.mjs` — spawns two bridges in parallel; expects exactly one to reach Ready, the other to exit 5 within 1 s. Maps to SC-001.
- `prereq-patterns/pglite/tests/sidecar_roundtrip.test.mjs` — spawns one bridge, reads sidecar, opens TCP, sends `SELECT 1;`, expects `1` back.
- `codeconv/tests/test_bridge_client.py::test_post_kill_restart` — kills the bridge with SIGKILL/equivalent and asserts a fresh start succeeds within 1 s. Maps to SC-002.
- `codeconv/tests/test_bridge_client.py::test_lock_race_fallback` — spawns two `bridge_client.AcquireOrDiscover` calls in parallel; expects exactly one to take Path A, the other Path B, both ending with the same `(host, port)`.
