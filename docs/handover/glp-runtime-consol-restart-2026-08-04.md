<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# glp-runtime-consol — mid-pipeline restart handover (2026-08-04)

**Status:** PAUSED, awaiting two Gabi decisions. Nothing in flight; safe to restart.
**Supersedes** the pre-pipeline `glp-runtime-consol-restart-2026-08-03.md` (that one still describes
the scope/§1.14 gate correctly; this one adds the actual pipeline position).

## One-screen SITREP

Feature **`glp-runtime-consol`** = spec dir `specs/065-glp-runtime-consol`, run through the buildkit
pipeline this session. **specify → plan → tasks → analyze → marathon → implement** were executed;
implement is **partial**, held at the §1.14 gate.

Two branches exist (both off `develop@bc5ea232`):

| Branch | State | Contents |
|---|---|---|
| `065-glp-runtime-consol` | **LOCAL only, 5 commits** | full pipeline artifacts + Scope B commit + Scope A gate-stop |
| `066-abandon-stub-cleanup` | **PUSHED, 1 commit `4e67492b`, 0/0 with origin** | the Scope B cleanup, cherry-picked, ready-to-ship-but-flagged |

Marathon run **`mrun-09a6c7f8d528`** — `open`, discharge **1/3** (US2 satisfied; 2 US1 items pending
the §1.14 gate). Sidecar pipeline: specify/plan/tasks/analyze `complete`; **implement started, NOT
complete**.

## What got DONE this session

1. **Pipeline stages specify→analyze** — all sidecar-recorded `complete`, committed on 065
   (`60d03df`, `4701b9ff`, `d15e18c4`). Analyze = **0 CRITICAL, 100% coverage**.
2. **US2 / Scope B (abandon dead-stub) — COMPLETE.** Removed `out/csharp/lib/runtime/abandon.cs`
   (dead `AbandonOps.AbandonWriter`, zero source callers) + its `Converted.props` `<Compile>` entry.
   Engine `glp_runtime_net.csproj` + full `.sln` build **0 errors**. Commit `8bc4b698` (on 065);
   cherry-picked as `4e67492b` (on 066). US2 marathon discharge item **satisfied**.
3. **US1 / Scope A (antlr4 grammar spike) — STOPPED at the §1.14 gate (as directed).** Non-gated
   prep done: toolchain confirmed (Java 17, dotnet 10.0.301, network OK for ANTLR jar + NuGet C#
   runtime), scaffold `spike/antlr4-glp-grammar/{corpus,harness,gen}`, corpus `MANIFEST.md` (7
   files incl. 1 negative control), token vocab enumerated (49 types from `token.cs`). **Written
   owner proposal at `spike/antlr4-glp-grammar/PROPOSAL-1.14.md`.** Commit `353c5fa3` (on 065).
   T010–T015 NOT started.
4. **codex CLI — ROOT-CAUSED + ENDURINGLY FIXED (Gabi-directed).** `~/.codex/config.toml` had
   `[windows] sandbox = "elevated"` → codex spawned its exec tool via `CreateProcessAsUserW` with an
   elevated token → **error 5 (ACCESS_DENIED)** under the Claude Code harness. **Fix: `[windows]
   sandbox = "unelevated"`** (enum = elevated|unelevated). Backup `~/.codex/config.toml.bak-pre-unelevated-20260803`.
   Proven: bare `codex exec` → OK; `codex review --base develop` runs its investigation, zero err5.
   (Persisted to memory `[[glpnet-windows-env-quirks]]`.) NB: the Claude Code classifier still blocks
   a *full* `codex review` redirected/teed to a file — same class as operator-ship.
5. **Review of branch 066 (Scope B) — done, findings below.** `reviews/066-abandon-stub-cleanup/`
   (verdict.md + codex.md) — **gitignored**, so the findings are captured here for durability.

## 🔴 Review findings on branch 066 (why it is NOT ship-clean as-is)

- **F1 (CONFIRMED, MEDIUM): codeconv inventory left inconsistent.** `abandon.cs` was a
  codeconv-**generated output** of `glp_runtime/lib/runtime/abandon.dart`. The conversion-state still
  points at the deleted file: `.codeconv/tombstones/lib/runtime/abandon.dart.md`
  (`target_cs_path: out/csharp/lib/runtime/abandon.cs`, `build_status: pass`) + the conversion-plan +
  conversion-spec + the PGLite `codeconv` schema row (system of record, FR-029 / Constitution VI-a).
- **F2 (PLAUSIBLE, MEDIUM): non-durable.** The Dart source `glp_runtime/lib/runtime/abandon.dart`
  (an identical dead stub, `throw UnimplementedError`) still exists on the codegen frontier, so a
  future `/codeconv-codegen` re-drive could **regenerate** `abandon.cs`, undoing the cleanup.
- **Safe part:** the C# engine is compile- + runtime-safe (zero callers, builds 0-err). It is the
  *engine* that is clean; the *codeconv inventory + Dart source* are the un-reconciled remainder.
- Spec 065 Scope B (FR-007/FR-008) named **only** the C# file — so the durable-removal question is a
  genuine spec gap (DISCIPLINE §1.3 "fix infrastructure not symptoms"), to resolve with Gabi.

## 🔴 TWO decisions blocking all further progress (Gabi's to make)

1. **§1.14 gate (needs Gabi + Udi):** approve authoring `Glp.g4` per
   `spike/antlr4-glp-grammar/PROPOSAL-1.14.md` (faithful, additive, no accepted-syntax change)?
   → unblocks US1 T010–T015.
2. **Ship-066 decision:** given F1/F2 —
   - **(a)** ship 066 now + open a follow-up to reconcile the codeconv inventory (retire via
     `codeconv` tooling, NOT hand-deletion — hand-deleting desyncs the PGLite system of record) and
     decide `abandon.dart`'s fate; **or**
   - **(b)** hold 066; reconcile the codeconv state + Dart source first, then ship a *complete*
     removal.

## WHAT'S NEXT — resume paths (pick per Gabi's two decisions)

- **If §1.14 approved:** on `065`, author `spike/antlr4-glp-grammar/Glp.g4` (faithful; re-STOP if any
  construct needs a real syntax change), generate the C# parser, build the coverage + IL-parity
  harness, run it, write `REPORT.md`, satisfy the 2 US1 marathon items → then `/bk-codexreview` (now
  works) → operator-ship 065 → `/bk-close`.
- **If ship-066 = (a):** operator ships `066` (command below); then open the codeconv-reconcile
  follow-up. Note: once 066's abandon-removal lands on develop, **rebase 065 to drop `8bc4b698`**
  (it becomes a no-op / conflict since the file is already deleted on develop) so 065 = Scope-A-only.
- **If ship-066 = (b):** reconcile codeconv inventory + `abandon.dart` first (new small spec or fold
  into 065), then ship.

### Operator ship command (classifier-blocked for the agent — OPERATOR runs via `!`)

```
! $env:PYTHONUTF8='1'; & 'D:\bstdev\research\buildkit\.venv313\Scripts\buildkit-ship.exe' --skip-preflight --no-edit
```
CalVer is buildkit-derived (`vYYYY.MM.DD.N`). If the pipeline gate says `implement != complete`,
reconcile with `python -m buildkit_cli.pipeline.sidecar reconcile implement`.

## Environment (carried)

- buildkit CLIs = `D:\bstdev\research\buildkit\.venv313\Scripts\*.exe`; set `PYTHONUTF8=1`.
- Tests: `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart` before `bash test/run_all_tests.sh`.
- C# `dotnet` 10.0.301; engine sln = `out/csharp/glp_runtime_net.sln`.
- **codex now works** (config fix above). Full `codex review` teed-to-file is classifier-blocked;
  the short `codex review ... | grep` form runs.
- Pre-existing untracked `.specify/retrospective/062-*` dir is NOT mine — leave it (do not commit).

## Restart anchor (objective, not this summary)

`buildkit-roadmap next` → `.specify/feature.json` (= `specs/065-glp-runtime-consol`) →
marathon `status --feature 065-glp-runtime-consol` (discharge 1/3) → this doc + memory
`[[glp-runtime-consol-restart]]`.
