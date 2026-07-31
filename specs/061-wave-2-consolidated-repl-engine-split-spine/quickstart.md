# Quickstart — Wave 2: REPL Engine Split Spine (061)

## Build

```
dotnet build csharp/glp_engine_host/GlpEngineHost.csproj
dotnet build csharp/glp_repl_client/GlpReplClient.csproj
dotnet build csharp/glp_supervisor/GlpSupervisor.csproj
```

## Run the split (US1)

```
dotnet run --project csharp/glp_engine_host -- --listen 127.0.0.1:7461
dotnet run --project csharp/glp_repl_client -- --connect 127.0.0.1:7461
```

In the client: `load ./programs/tests/typed/<file>.glp` then `goal.` — output
must match the single-process REPL (`out/csharp/glp_repl`) for the same input.
(Path resolution mirrors the single-process REPL exactly: `/`, `./`, `../`
prefixes are used as-is; a bare name gets the REPL's `glp/` prefix.)

Verified end-to-end 2026-07-30 (T041): load → goal (`Y = 42`) → `:snapshot`
(seq=1, loud file-fallback degradation) → kill → `--from-snapshot latest`
(restored 1 unit, heap intact) → goal (`Y = 10`) on the restored engine.

## Snapshot (US2)

Client: `:snapshot` → `ACK seq=N` (or `DEFERRED` then seq advances at
quiescence; check `:status`). Store root defaults to the repo cluster via the
bridge; `--store <dir>` selects/forces the file fallback.

Start from a snapshot: `--from-snapshot latest` (or `--from-snapshot <seq>`).

## Supervised run (US3)

```
dotnet run --project csharp/glp_supervisor -- --engine csharp/glp_engine_host --listen 127.0.0.1:7461 --ping-interval 5s
```

Kill the engine PID externally → supervisor logs the crash, restarts from the
latest snapshot, engine answers PING again. `--status` / `--history` query
liveness + crash records.

## Kill-and-restart correctness test (US4, FR-033)

```
dotnet test csharp/glp_engine_host.tests --filter KillAndRestart
```

## Verification models (FR-040)

```
docs/research/repl-engine-separation/models/spin/run.ps1     # wire protocol (WSL2 SPIN)
docs/research/repl-engine-separation/models/tla/run.ps1      # crash/restore consistency (TLC)
docs/research/repl-engine-separation/models/uppaal/run.ps1   # timed liveness (verifyta)
```

Each writes/refreshes its RESULT.md with the real-tool verdict.

## Suites (baseline + re-test — Constitution VII)

```
bash test/run_all_tests.sh
dotnet test csharp/glp_engine_host.tests
```
