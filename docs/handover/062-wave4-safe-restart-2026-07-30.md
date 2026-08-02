<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature 062 wave-4 — Safe-restart handover

**Date:** 2026-07-30 · **Author:** Olamnit session · **Status:** ready-to-ship (ship gated)
**Anchor:** commit `e2c3bc04` (+ a ship-prep commit on top), branch
`062-wave-4-consolidated-parallel-safe-fillers`, PUSHED.

## Summary

All 37 tasks are DONE and GREEN. `/bk-analyze` passed with **0 CRITICAL/HIGH, 100%
requirement coverage**. The **only remaining action is the ship**, which is deliberately
NOT executed — it is gated on fleet-lead (ariellas) CalVer `.N` coordination (COOP v2
directive #5) + operator go. A new session resumes exactly at that barrier.

## Current state — test receipts (all verified this session, not relayed)

| Suite | Result |
|---|---|
| Dart REPL `test/run_all_tests.sh` | **546/546** |
| Dart engine `dart test test/engine/` | 11/11 |
| Three-way parity `test/parity/run_differential.sh` (5 US5 goals) | **0 divergent** |
| C# `glp_il_codec.tests` | 64/64 |
| C# `glp_link.tests` | 161/161 |
| C# `glp_wire_registry.tests` | 6/6 |
| C# engine sln `out/csharp/glp_runtime_net.sln` | builds, 0 errors |
| Gleam `gleam test` (in `glp_gleam/`) | **514/514** |
| codeconv depgraph | 66/66 |

Known Gleam flake: a transient `tcp accept failed: Timeout` in `glp_gleam/test/link` is the
memory-documented spawned-process timing flake — re-run, not a regression.

## What was delivered this session

- **US5 §1.14 pins (T031–T033):** REPL Section **A32** + Section C `abandon_reader_bad.glp`;
  Dart engine unit `glp_runtime/test/engine/us5_nested_abandon_test.dart`; new fixtures
  `programs/tests/typed/{abandon_stream,abandon_reader_bad}.glp`. NO Dart structural change
  (parity-verify + regression pins only; §1.14 respected).
- **US3 (T015–T022) — full hardened capability:** `csharp/glp_il_codec/CompiledIlEnvelope.cs`
  (il_version / SHA-256 integrity_digest / source_metadata over the factored-out IlCodec) +
  receiver execute-on-B==local + hardening + role-aware Loopback **multi-accept**
  (`LoopbackTransport` rewrite + `MultiAcceptListener`) + **NetMQ ZeroMQ** PAIR transport
  (`csharp/glp_link/transports/ZmqTransport.cs`, new `LinkScheme.Zmq`, NetMQ 4.0.4.3 in
  `GlpLink.csproj`) + envelope-over-real-ZMQ execute-on-B.
- **Decision (a):** ZMQ = full integrated capability (NetMQ) — delivered.
- **Decision (b):** Gleam REPL conjunction-query MVP — delivered via a parallel `/bk-3rtask`
  team (commit `e17a9185`; `glp_gleam/src/glp/engine.gleam` + `engine/goal_boot.gleam` +
  `test/glp/repl/conjunction_query_test.gleam`).
- **Phase 8:** T034 `specs/062-.../TERMINAL-STATE.md` (SC-008 ledger), T035 codify win
  `cn-20260730T065537-b169f631`, T036 COOP UPDATE to `I:\coop\glpnet\inbox\ariellas\`, T037
  final sweep green. `/bk-analyze`: 0 critical, 100% coverage.

## RESUME — first actions of the new session (in order)

1. Read CLAUDE.md + the 3 mandatory docs (DISCIPLINE, typed-glp-manual, glp-cheat-sheet);
   acknowledge; then read this handover.
2. Verify HEAD is on branch `062-…` at the ship-prep commit (≥ `e2c3bc04`); `git status` clean
   (only the untracked `COOP/` + `glp_gleam/test/link/` dirs are expected).
3. **Ship step — the gated barrier (plan A):**
   a. Announce release intent + the chosen CalVer `.N` to lead ariellas via COOP v2:
      write `I:\coop\glpnet\inbox\ariellas\<mechanical-UTC>-olamnit-REQUEST-release-cut-calver.md`
      (UTC via `date -u`, C1a). Refresh `status\olamnit.md`.
   b. **WAIT for the lead's CONFIRM** (barrier C5; E6 — COOP authorises nothing on its own).
      Do NOT ship before that + operator go.
   c. On CONFIRM + operator go: run the suites yourself first (REPL + C# + Gleam — because
      `--skip-preflight` bypasses buildkit's pytest preflight, which does not match this repo),
      then `buildkit ship --skip-preflight` from the feature branch (GitFlow
      feature→develop→release→main; NEVER hand-merge to main).
   d. Post ACK-COMPLETE to ariellas with the shipped CalVer tag + PR numbers; advance the
      roadmap/marathon to closed.

## Environment gotchas

- Dart: `/c/src/flutter/bin/cache/dart-sdk/bin/dart`. **`export DART=<that>` before
  `bash test/run_all_tests.sh`** — the script's default detection picks the dead Linux path
  `/home/user/dart-sdk` and every test then errors as "dart: not found".
- C#: `dotnet` 10.0.301. Rebuild C# REPL exe (needed by the parity harness):
  `dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug`.
- Gleam: 1.17.0 on PATH; run from within `glp_gleam/`.
- COOP v2 live channel = `I:\coop\glpnet` (this host = Olamnit, id `olamnit`; lead = ariellas).
  Old `G:\...\COOP` is DEAD. Post UPDATEs/REQUESTs into `inbox\ariellas\`; own only
  `status\olamnit.md`; UTC always mechanical (`date -u`).
- Divergence protocol: never fudge a golden — STOP and report the three-way.

## Files changed this session (all committed + pushed)

- `programs/tests/typed/{abandon_stream,abandon_reader_bad}.glp`
- `glp_runtime/test/engine/us5_nested_abandon_test.dart`
- `test/run_all_tests.sh` (A32 block + Section C entry)
- `csharp/glp_il_codec/CompiledIlEnvelope.cs`,
  `csharp/glp_il_codec.tests/{CompiledIlEnvelopeTests,ReceiverExecuteOnBTests,ReceiverHardeningTests,ZmqEnvelopeExecuteTests}.cs`
- `csharp/glp_link/seam/LinkScheme.cs`,
  `csharp/glp_link/transports/{LoopbackTransport,MultiAcceptListener,ZmqTransport}.cs`,
  `csharp/glp_link/GlpLink.csproj`,
  `csharp/glp_link.tests/{MultiAcceptTests,ZmqTransportTests}.cs`
- `glp_gleam/src/glp/engine.gleam`, `glp_gleam/src/glp/engine/goal_boot.gleam`,
  `glp_gleam/test/glp/repl/conjunction_query_test.gleam`
- `specs/062-.../{tasks.md,TERMINAL-STATE.md}`, `.specify/codify/notes/cn-20260730T065537-b169f631.md`
