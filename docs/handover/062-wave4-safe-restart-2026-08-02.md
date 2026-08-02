<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature 062 wave-4 — Safe-restart handover (v2)

**Date:** 2026-08-02 · **Author:** Olamnit session · **Status:** ready-to-ship, ship pending operator `!` keystroke
**Anchor:** commit `386e48ce`, branch `062-wave-4-consolidated-parallel-safe-fillers`, PUSHED.
**Supersedes:** `062-wave4-safe-restart-2026-07-30.md` (the ship-barrier has since resolved; only the keystroke remains).

## Summary

All 37 tasks DONE + GREEN; `/bk-analyze` 0-critical / 100% coverage. Every ship gate is
satisfied: suites re-verified GREEN this session; operator (Gabi) go received; the CalVer
collision barrier is **moot** (see below). **The ONE remaining action is the ship itself,
which the Claude Code classifier BLOCKS for the agent** — it must be run by the operator via
the `!` prompt prefix (same constraint gavriella hit on wave-5).

## CalVer story (why the tag is NOT v2026.07.31.3)

- 2026-07-31: `.1` = wave-2/061 (ariellas, shipped), `.2` = wave-5/063 (gavriella, shipped).
- Olamnit announced `.2` (023608Z) → **crossed** gavriella's in-flight `.2` cut on the async
  channel → gavriella reassigned olamnit `.3`.
- **Then the UTC clock rolled** (twice: into 2026-08-01, then 2026-08-02) between the operator
  go and the keystroke. buildkit derives CalVer from the current UTC date, so the cut is now
  **`v2026.08.02.1`** (fresh-day `.1`) — the entire `.2`/`.3` same-day crossing is MOOT. No
  collision on a fresh day. **Do NOT hand-pick a tag** — buildkit computes it (verify with a
  `--dry-run`; whatever UTC day it runs, expect `vYYYY.MM.DD.1`).

## THE remaining action (operator runs; agent is classifier-blocked)

```
! $env:PYTHONUTF8='1'; & 'D:\bstdev\research\buildkit\.venv313\Scripts\buildkit-ship.exe' --skip-preflight --no-edit
```

- Correct CLI = the **venv313** exe (`D:\bstdev\research\buildkit\.venv313\Scripts\buildkit-ship.exe`).
  The PATH `buildkit` is a different/older build whose `ship` lacks `--skip-preflight`.
- Idempotent: if it hits the develop merge-conflict on buildkit feature-pointer files (develop
  is at `4090f666`/#123, ahead of the 062 branch point — gavriella hit the same on #123), resolve
  the two pointer files + re-run the same command; it resumes.
- Plan (dry-run confirmed): 062 → feature PR to `develop` → `release/vYYYY.MM.DD.1` → tag →
  back-merge to develop.

## Post-ship close-out (agent can do these once the tag lands)

1. Verify tag + the 3 PR numbers (feature / release / back-merge) on the remote.
2. Post **ACK-COMPLETE** to `I:\coop\glpnet\inbox\ariellas\` + `inbox\gavriella\` with the shipped
   tag + PR numbers; correct the record (cut = `vYYYY.MM.DD.1`, not `.3` — day rolled). Refresh
   `status\olamnit.md`.
3. Advance roadmap: wave-4 feature → released; the consolidated 062 features → closed.
   Marathon `mrun-7b8d08899272` → discharge/close.

## Test receipts — re-verified THIS session (2026-08-02), not relayed

| Suite | Result |
|---|---|
| Dart REPL `test/run_all_tests.sh` | 546/546 |
| Gleam `gleam test` (in `glp_gleam/`) | 514/514 |
| C# `glp_il_codec.tests` | 64/64 |
| C# `glp_link.tests` | 161/161 |
| C# `glp_wire_registry.tests` | 6/6 |
| C# engine sln + `glp_repl` exe | build, 0 errors |
| Three-way parity `run_differential.sh` (5 US5 goals) | 0 divergent |

The 5 US5 differential goals: `make_person` / `get_age` / `get_city` (nested WRITE/READ on
`programs/tests/typed/struct_demo.glp`), soft-fail `get_age(person(...,weight(...),...))`,
and `first_only([a,b,c],Y)` (abandon on `programs/tests/typed/abandon_stream.glp`).

## Environment gotchas

- Dart: `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart` before `bash test/run_all_tests.sh`
  (default detection picks the dead Linux path and errors "dart: not found"). `export PYTHONUTF8=1`.
- Gleam 1.17 on PATH; run from `glp_gleam/`. C# `dotnet` 10.0.301; rebuild REPL exe for parity:
  `dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug`.
- COOP v2 live channel = `I:\coop\glpnet` (this host = Olamnit, id `olamnit`; lead = ariellas;
  gavriella = primary). Old `G:\...\COOP` DEAD. Post to `inbox\ariellas\` + `inbox\gavriella\`;
  own only `status\olamnit.md`; UTC always mechanical (`date -u`); prepend, never overwrite.
- The `buildkit ship` classifier block is EXPECTED — engineer runs the `!` command.

## Resume in one line (after mandatory reading)

▎ Resume feature 062 from docs/handover/062-wave4-safe-restart-2026-08-02.md; verify HEAD 386e48ce
clean; if not yet shipped, hand Gabi the venv313 buildkit-ship `!` command (agent is
classifier-blocked); once the tag lands, do post-ship close-out (ACK-COMPLETE + roadmap/marathon close).
