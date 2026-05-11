# GLP Known Issues

## Issue 1: Localize uses writer address where reader address is needed

**Status**: Open
**Discovered**: 2026-02-10
**Affects**: Multi-agent (madGLP) programs where a term with unbound variables is sent between agents

### Summary

The `localize()` function in `mad_helpers.dart` substitutes the writer address into the term where the spec requires the reader address. This causes `ground()` guards on the receiving agent to fail definitively instead of suspending.

### Root Cause

`localize()` takes a `freshAddrAllocator: int Function()` callback that returns only the writer address. The caller discards the reader address:

```dart
freshAddrAllocator: () {
  final (w, _) = runtime.heap.allocateVariable();  // allocates pair (writerN, readerN+1)
  return w;                                          // discards reader address
},
```

Inside `localize()`:

```dart
final writerAddr = freshAddrAllocator();
final readerAddr = writerAddr;  // WRONG: should be the actual reader address
```

When localizing `_w(p, i)` (incoming writer from remote agent), the spec says to replace it with `Y_q?` (the reader). But because `readerAddr == writerAddr`, the code substitutes `VarRef(writerAddr)` — a writer, not a reader.

### Consequence

On the receiving agent, the term contains a VarRef pointing to a writer cell. When `ground()` traverses the term and finds this unbound writer, it takes the "unbound writer → definitive failure" path (correct for single-agent SRSW, wrong here). The goal fails instead of suspending on the reader and waking when the remote assignment arrives.

### Observable Effect

In `three_agent_pipeline_boot.glp`, agent3's `consumer_init` receives a partially-bound list like `[got(1), got(2) | X2]` where X2 is a localized variable. Because X2 is a writer (should be reader), `ground(Ys?)` fails and the goal terminates instead of suspending until the rest of the list arrives. The test passes as a **false positive** because a failed goal reports agent completion (zero remaining goals).

### Fix Status

**Fixed**: Changed `freshAddrAllocator` signature from `int Function()` to `(int, int) Function()`, returning both `(writerAddr, readerAddr)`. Updated `localize()` and all 4 callers in `mad_context.dart`.

Note: this fix alone does NOT resolve the pipeline test failure — the root cause is in globalise/send (see Issue 2 and `docs/bug-send-globalise-localise.md`).

### Broader Concern: N+1 Arithmetic

The heap-pointer architecture spec states that writer and reader cells point to each other via cross-pointers, so address arithmetic (`writerAddr + 1`) should never be needed. However, `pairedReaderAddr()` in `heap_fcp.dart` has a fallback `return writerAddr + 1`. An audit should verify that no code depends on the N/N+1 allocation convention — all navigation between paired cells should use the cross-pointers.

### Files Involved

- `glp_runtime/lib/multiagent/mad_helpers.dart` — `localize()` function (lines 212-255)
- `glp_runtime/lib/multiagent/mad_context.dart` — all `freshAddrAllocator` callbacks
- `glp_runtime/lib/runtime/heap_fcp.dart` — `allocateVariable()`, `pairedReaderAddr()` fallback
- `programs/typed_book/multiagent_tests/three_agent_pipeline_boot.glp` — test that exercises the bug

### Test

After fixing all issues, `three_agent_pipeline_boot.glp` should show agent3's `consumer_init` suspending on `ground(Ys?)`, then waking when the full list `[got(1), got(2), got(3)]` arrives, then completing via `wrap` and `consume`.

---

## Issue 2: TermVar.pairedReaderAddr returns wrong address

**Status**: Fixed
**Discovered**: 2026-02-10
**Affects**: All multi-agent programs that send terms containing writers
**See also**: `docs/bug-send-globalise-localise.md`

### Summary

`TermVar.pairedReaderAddr` returned `addr` (the writer address itself) instead of the actual paired reader address from the heap. `TermVar` only stored a single address, with no way to look up the paired address.

### Fix

Redesigned `TermVar` to carry both `writerAddr` and `readerAddr` fields, populated by `_extractTermVarsRecursive()` using the heap's cross-pointer methods (`tryWriterForReader`, `pairedReaderAddr`). All call sites updated.

---

## Issue 3: Spurious write-back mechanism for localized _w variables

**Status**: Removed
**Discovered**: 2026-02-10
**Affects**: N/A (the mechanism was incorrect and has been removed)

### Summary

A write-back mechanism (`_registerWriteBackCallbacks`, `_sendWriteBack`) was added to handle the case where agent q localizes `_w(p, i)`, creates a fresh pair `(Y_q, Y_q?)`, and then binds Y_q locally. The write-back sent `_w(p, i) := T` back to agent p.

### Why It Was Wrong

This mechanism does not exist in GLP. The data flow for `_w(p, i)` is strictly p→q: p assigns the writer, the `global_send` goal at p fires, and the value is delivered to q's entry. There is no reverse flow. If a program needs q→p flow (the receiver writes back), the sender must export the reader, producing `_r(p, i)`, and the `global_send` spawned at q by `localize` handles the outgoing direction.

### Resolution

Removed `_registerWriteBackCallbacks()`, `_sendWriteBack()`, and all call sites from `mad_context.dart`. Test programs that relied on this mechanism need to use the correct polarity (export reader for q→p flow).

---

## Issue 4: Type checker rejects well-typed `=` with reader argument

**Status**: Open
**Discovered**: 2026-02-10
**Affects**: Any typed program using `=` (unification) with a reader variable

### Summary

The type checker rejects the following well-typed clause:

```prolog
procedure bind_later(_).
bind_later(Done?) :- wait(1000) | done(Done).
```

Error: "Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)" for `Done` at the `=` call site (or equivalent body atom).

### Analysis

The prelude declares `=` as:

```prolog
procedure =(_?, _).
X = X?.
```

Position 0 is `_?` (reader), position 1 is `_` (writer). In the clause `bind_later(Done?)`, `Done` is the reader of the writer passed by the caller. Using `Done` as the first argument of `=` (the `_?` position) should be well-typed since `Done` is already a reader. The type checker incorrectly rejects this.

### Workaround

Use `done(Done)` instead of `Done = done` to avoid `=` entirely.

### Files Involved

- `glp_runtime/lib/analysis/type_checker/` — type checker implementation

---

## Issue 5: localize() spawn uses reader address; onBind needs writer address

**Status**: Fixed
**Discovered**: 2026-02-10
**Affects**: Multi-agent programs where a localized `_r(p, i)` should trigger a `global_send` back to agent p

### Summary

In `localize()`, processing `_r(p, i)` creates a `GlobalSendSpawn` with `readerAddr: readerAddr`. But `registerGlobalSendSpawns()` passes `spawn.readerAddr` to `heap.onBind()`, which is indexed by **writer** address. The callback never fires because the reader address is not a valid key for `onBind`.

### Fix

Changed `localize()` to pass `writerAddr` in the spawn's `readerAddr` field. The field name is misleading (it is actually the `onBind` key), but the semantics are now correct.

---

## Issue 6: globalize-reader entry stores reader address instead of writer address

**Status**: Fixed (part 1); part 2 removed
**Discovered**: 2026-02-10
**Affects**: Multi-agent programs where agent p globalizes a reader `X?` as `_r(p, i)`

### Summary

`globalize()` passed `v.addr` (the reader address) to `addGlobalizeEntry()`, which stores it as `writerAddr`. But `_handleReaderAssignment` later calls `bindVariable(entry.writerAddr, ...)` — passing a reader address to `bindVariable` is incorrect.

### Fix

Changed `globalize()` to pass `v.writerAddr` (the actual writer) to `addGlobalizeEntry()`.

### Note on onBind

A previous fix also added an onBind callback in `send()` for globalize-reader entries, using `_sendWriteBack`. This was incorrect — for `_r(p, i)`, agent p creates an entry and WAITS. The `global_send` is spawned at q by `localize`, not at p. Agent p does not send anything for `_r` entries. The onBind and write-back have been removed.

---

## Issue 7: DBOS-on-PGLite launch — four required hooks

**Status**: Mitigated (workarounds applied; revisit if DBOS upstream changes)
**Discovered**: 2026-05-10 during 012-codeconv-runner Phase 6
**Affects**: `codeconv migrate` (DBOS `dbos.launch()`) against the unified `.pgdb/` bridge

### Summary

A fresh DBOS launch against the PGLite bridge requires four non-default configuration choices. Removing any one of them produces a different failure mode (separate-DB connect failure, deadlock on advisory lock, SQL syntax error at line 5, or hang on LISTEN/NOTIFY). See `codeconv/src/codeconv/db/engine.py::setup_dbos` and `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py`.

### The four hooks

1. **DB-name override**: `DBOSConfig(application_database_url=url, system_database_url=url, dbos_system_schema="dbos")`. DBOS defaults to a sibling `<dbname>_dbos_sys` database that PGLite cannot create; point both roles at `postgres` and isolate via the `dbos` schema (FR-015).
2. **Pool sizing**: `db_engine_kwargs["pool_size"]=5, max_overflow=5` AND `sys_db_pool_size=5`. The vendored `pglite_engine_kwargs` defaults to `pool_size=1`, which deadlocks DBOS's `run_migrations` (holds one connection across an inner `ensure_dbos_schema` call). PGLite serialisation is preserved client-side because the bridge's `globalWorkChain` queues queries.
3. **uuid-ossp rewrite preserves semicolon**: the SQLAlchemy `before_cursor_execute` filter substitutes `CREATE EXTENSION "uuid-ossp"` → `SELECT 1;` (with terminating `;`). Without the semicolon, the next CREATE TABLE concatenates in DBOS's multi-statement migration text and PGLite errors at line 5.
4. **Disable LISTEN/NOTIFY**: `use_listen_notify=False` in `DBOSConfig`. PGLite does not implement `NOTIFY` end-to-end; leaving DBOS to poll triggers mystery hangs.

### PGLite specifics worth knowing

- `SELECT current_database()` returns `'template1'` regardless of the requested `dbname` in the connection URL. PGLite routes everything to template1. Functionally fine — do not "fix" by changing the URL.
- `pg_try_advisory_lock(...)` works and returns BOOLEAN. The earlier "cannot unpack non-iterable NoneType object" trace was a bug in the SQLAlchemy `before_cursor_execute` filter (must always return a tuple under `retval=True`), not a PGLite limitation.

### Where this is enforced

- `codeconv/src/codeconv/db/engine.py::setup_dbos` — hooks 1, 2, 4.
- `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py::_install_sqlalchemy_uuid_ossp_filter` — hook 3.
- `codeconv/tests/test_engine.py::test_apply_to_engine_installed` — end-to-end smoke (codeconv side).

### Why this isn't fixed upstream

PGLite is feature 011's pre-req pattern; we treat it as a black-box dependency. The five workarounds above live in `codeconv/_vendor/dbos_pglite_patch.py` and `codeconv/db/engine.py` so a future DBOS or PGLite upgrade that lifts these limitations can drop them without touching consumer code.

---

## Issue 8: PGLite cluster files cannot live on exFAT

**Status**: Mitigated via `--data-dir` override (release `v2026.05.11-2`)
**Discovered**: 2026-05-11 when the freshly-merged `codeconv` was first invoked against the live repo
**Affects**: Any `codeconv` (or other unified-bridge consumer) invocation where the repo lives on an exFAT volume

### Summary

PGLite's WASM data files rely on POSIX-style file operations (atomic rename, advisory locks, certain mmap operations) that exFAT does not implement. When `.pgdb/` is created on exFAT, the bridge process crashes mid-DBOS-migration (typically around migration 4) — the client sees `psycopg.OperationalError: consuming input failed: server closed the connection unexpectedly` on a downstream query, and the bridge log is empty because the bridge died before flushing.

This is environment-dependent and does NOT show up in `pytest` runs, because pytest's `tmp_path` lives under `%TEMP%` on `C:` (NTFS). The bug surfaces only against the live repo on `D:\BSTDEV\research\GLP\GLPNET\` which is on a `Lexar`-labelled exFAT drive (`Get-Volume D` shows `FileSystem : exFAT`).

### Fix

Added a global `--data-dir <path>` flag to `codeconv` (cli.py / bridge_client.acquire_or_discover / workflow.run_discover). The flag decouples the PGLite cluster location from `--repo-root`. Point it at an NTFS directory:

```powershell
codeconv --data-dir $env:LOCALAPPDATA/codeconv-pgdb migrate
codeconv --data-dir $env:LOCALAPPDATA/codeconv-pgdb discover run
```

Sidecar (`<data-dir>/bridge.json`), OS lock (`<data-dir>.bridge.lock/`), consumer registrations (`<data-dir>.consumers/`), and force-shutdown marker (`<data-dir>/.shutdown`) all follow the override automatically.

### Detection

```powershell
Get-Volume <drive-letter> | Format-List FileSystem
```

If `FileSystem` is `exFAT`, `FAT32`, or anything other than `NTFS` / `ReFS`, you MUST use `--data-dir` to point the cluster at an NTFS path. The repo source files themselves are fine on exFAT.

### Why this isn't a code fix

PGLite is a WASM build of upstream PostgreSQL. It assumes a POSIX-class filesystem under the data directory. We cannot fix this in `codeconv` itself — only route around it. The flag and this doc-note are the mitigation.
