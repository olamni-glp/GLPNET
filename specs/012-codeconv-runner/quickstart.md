# Quickstart: 012-codeconv-runner

End-to-end smoke for the feature. Assumes a fresh checkout on Windows 11 with Node ≥ 20, Python ≥ 3.11, .NET 8 SDK, and the existing `.D2NET/pgdb/` workspace data present.

## Prerequisites

- Working tree at `D:\BSTDEV\research\GLP\GLPNET\` clean (or all WIP committed).
- `node --version` ≥ 20.
- `python --version` ≥ 3.11.
- `dotnet --version` ≥ 8.
- Pre-existing `.D2NET/pgdb/` (so migration has work to do; if absent, US2 verification is skipped).
- No bridge running (`Get-Process node -ErrorAction SilentlyContinue` shows no `pglite_bridge.mjs` invocations).

## Flow A — Bridge cross-process exclusion (US1, SC-001, SC-002, SC-003)

```powershell
# 1. Install bridge npm deps (one-time per checkout)
cd prereq-patterns\pglite
npm install
cd ..\..

# 2. Start bridge manually (escape hatch, not the auto-spawn path)
node prereq-patterns\pglite\pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon &
# wait for BRIDGE_READY on stdout; then:

# 3. Verify sidecar exists
Get-Content .pgdb\bridge.json   # → {host, port, pid, started_at, data_dir, role, managed_by}

# 4. Verify second-bridge attempt fails fast (SC-001)
node prereq-patterns\pglite\pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon
# Expected: stderr "[bridge] BRIDGE_LOCK_HELD pid=<...> at 127.0.0.1:<...>"
# Expected: exit code 5

# 5. Force-kill the running bridge and verify lock auto-release (SC-002)
$pid = (Get-Content .pgdb\bridge.json | ConvertFrom-Json).pid
Stop-Process -Id $pid -Force
Start-Sleep -Milliseconds 250
node prereq-patterns\pglite\pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon
# Expected: BRIDGE_READY emitted within 1 second (no manual lock cleanup)
```

## Flow B — D2NET migration to unified bridge (US2, SC-004, SC-005)

```powershell
# 1. (Stop any running bridge from Flow A.)
$pid = (Get-Content .pgdb\bridge.json | ConvertFrom-Json).pid
Stop-Process -Id $pid -Force
Remove-Item .pgdb -Recurse -Force   # reset to truly fresh state

# 2. Run migration (build the migrate tool first if needed)
dotnet build tools\d2net\D2Net.sln
tools\d2net\src\D2Net.PgdbMigrate\bin\Debug\net8.0\d2net-pgdb-migrate.exe

# Expected output:
#   d2net-pgdb-migrate: planning…
#     source .D2NET/pgdb:  present (...)
#     target .pgdb:        absent
#     plan: backup → move
#     → taking backup: .D2NET/pgdb.bak.20260509T...Z
#     → moving .D2NET/pgdb → .pgdb
#   SUCCESS

# 3. Verify
Test-Path .D2NET\pgdb        # False
Test-Path .pgdb              # True
Test-Path .D2NET\pgdb.bak.*  # True (backup retained)
Test-Path .D2NET\D2NET-Settings.json  # True (UNCHANGED per FR-007)

# 4. Run an existing D2NET command to verify regression-free behaviour (SC-005)
/D2NET-init list   # via Claude slash; or directly:
tools\d2net\src\D2Net.Init\bin\Debug\net8.0\d2net-init.exe --list
# Expected: D2NET reads/writes against .pgdb (auto-spawning bridge if needed). Same observable output as before migration.
```

## Flow C — codeconv runner + discover (US3, US4, SC-006 through SC-013)

```powershell
# 1. Install codeconv Python package (one-time per checkout)
python -m pip install -e codeconv

# 2. Verify bridge auto-spawn from a Python client
codeconv doctor
# Expected: bridge auto-spawns; sidecar valid; schemas present (dbos, codeconv, plus any D2NET schema)

# 3. List registered tools
codeconv list
# Expected output (one line per tool):
#   discover  Walk glp_runtime_net/ and inventory .dart files into codeconv schema + tombstones.

# 4. Run discover on the canonical subtree (SC-006, SC-013)
Measure-Command { codeconv discover }
# Expected: ≤ 60 s; summary reports ~128 files processed (matches actual file count).

# 5. Verify tombstones written and database populated
(Get-ChildItem .codeconv\tombstones -Recurse -Filter *.dart.md | Measure-Object).Count
# Expected: 128

# 6. Idempotence (SC-008, SC-013)
Measure-Command { codeconv discover }
# Expected: ≤ 5 s; summary reports 128 skipped_idempotent, 0 processed.

# 7. Slash-command discoverability (SC-010)
# In Claude Code: type `/codeconv-` and verify both /codeconv-runner and /codeconv-discover appear.
```

## Flow D — Rebuild from tombstones (SC-007)

```powershell
# 1. Logical dump BEFORE schema drop
psql "host=127.0.0.1 port=$BRIDGE_PORT dbname=postgres user=postgres password=postgres" -c "\d codeconv.*"
# (or use codeconv doctor --dump-codeconv to emit a comparable structured dump)
codeconv doctor --dump-codeconv > before.txt

# 2. Drop the schema (preserves dbos, public, D2NET schemas)
psql ... -c "DROP SCHEMA codeconv CASCADE;"

# 3. Rebuild from tombstones
codeconv discover --from-tombstones
# Expected: 128 rows reconstructed; no .dart parse; ≤ 5 s.

# 4. Logical dump AFTER reconstruction
codeconv doctor --dump-codeconv > after.txt

# 5. Compare
git diff --no-index before.txt after.txt
# Expected: zero diff (SC-007).
```

## Flow E — Concurrent-stack safety (SC-003)

```powershell
# Run 100 sequential transactions from each of two stacks (Python via psycopg + .NET via Npgsql)
# against the same running bridge.

# Python side:
python specs\012-codeconv-runner\scripts\sc003_python_loop.py --port $BRIDGE_PORT --cycles 100

# .NET side (parallel terminal):
dotnet run --project specs\012-codeconv-runner\scripts\Sc003NpgsqlLoop -- --port $BRIDGE_PORT --cycles 100

# Expected: zero `lost synchronization with server`; zero `DuplicatePreparedStatement`; both clients complete cleanly.
```

## Flow F — Resume after kill (SC-009)

```powershell
# Start discover; kill it after ~5 seconds; re-invoke
$proc = Start-Process codeconv -ArgumentList 'discover' -PassThru
Start-Sleep -Seconds 5
Stop-Process -Id $proc.Id -Force

# Re-invoke
codeconv discover
# Expected: workflow resumes from the last completed file step; previously-processed files are NOT re-parsed.
# Inspect the discover_runs row to confirm files_processed > 0 from the prior partial run.
```

## Cleanup (between smoke runs)

```powershell
# Stop bridge cleanly
$pid = (Get-Content .pgdb\bridge.json -ErrorAction SilentlyContinue | ConvertFrom-Json).pid
if ($pid) { Stop-Process -Id $pid -Force }

# Reset PGLite cluster (destructive; for testing only)
Remove-Item .pgdb -Recurse -Force

# Reset tombstones (destructive; will be re-generated)
Remove-Item .codeconv\tombstones -Recurse -Force
```

## Common failure diagnostics

- **`BRIDGE_LOCK_HELD` but no PID exists in `.pgdb/bridge.json`**: lock-holder crashed before sidecar write. Per Edge Case: clients ignore sidecar when lock unheld and re-acquire. If this loops, check `proper-lockfile`'s lock file directly: `ls .pgdb/.bridge.lock`.
- **`pglite_init_failed`**: `.pgdb/postmaster.pid` may be stale. Manual recovery: `Remove-Item .pgdb\postmaster.pid` (per AIGRID `opskit_pglite_sidecar.py` cleanup pattern).
- **`Windows fatal exception: access violation` from psycopg**: `apply_to_engine()` was not called on the engine. Inspect `codeconv/db/engine.py`. Per FR-014.
- **`DuplicatePreparedStatement` from .NET**: `Pooling=false` missing from connection string OR `NpgsqlCommand.Prepare()` was called somewhere. Per FR-027.
