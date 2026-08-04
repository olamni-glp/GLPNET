<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# glp-runtime-consol — pipeline restart handover

**Date:** 2026-08-03 · **Author:** Olamnit session · **Status:** feature promoted, ready for `/bk-specify`
**Start point:** branch `develop` @ `14b39bd8` (PUSHED, clean, 0/0 with origin).

## What this is

A new roadmap feature **`glp-runtime-consol`** (epic `glp-runtime-gaps`, state **promoted**,
parallel-safe) seeded from a `/bk-3rtask` gap audit (run `20260803T134205Z-8bcd`; full curator
report at `.specify/3rtask/runs/20260803T134205Z-8bcd/curator_report.md`). The audit found **12 of
16 "open" runtime/engine roadmap features were already delivered** (wave-4/062 + specs/050) and
they have since been **closed**. `glp-runtime-consol` consolidates the only genuine remaining
GLP-runtime gaps.

## Scope (from the seed notes on the roadmap feature)

- **(A) antlr4-shared-grammar-spike** (the real gap): parsers are still HAND-WRITTEN across runtimes
  (`csharp/glp_schema_lang/parser/SchemaDslParser.cs` + the Dart parser); there is **no `.g4`
  grammar, no Antlr4 runtime, no dedicated spec/tasks** — only memos
  (`docs/research/repl-engine-separation/reconciliation/12-antlr4-shared-grammar-spike.md` + an
  ANTLR dossier). Deliver a `.g4` shared grammar + multi-target generation **feasibility spike**.
- **(B) abandon C# dead-stub cleanup**: obsolete/remove `out/csharp/lib/runtime/abandon.cs`
  (`AbandonOps.AbandonWriter` throws NotImplementedException). Abandon is already delivered as the
  **anonymous-writer discard** semantic (062 US5, "FCP has no dedicated abandon op") — this is
  dead code, low risk.

**Out of scope (do NOT re-fold):** qr-link-provisioning (kept as its own Distributed-connectivity
feature); #3 atomic-toolchain-installs + #10 batch-roadmap-advance (buildkit repo, not glpnet).

## 🔴 §1.14 language-authority gate (MUST honor)

Sub-scope (A)'s shared grammar touches the parser surface across 3 runtimes. **If it changes the
accepted GLP syntax, that is a language-surface change requiring explicit Gabi + Udi approval
BEFORE implement** (DISCIPLINE §1.14). Run it as a feasibility spike first; STOP-gate + written
proposal before any change to what the language accepts. Sub-scope (B) is dead-code removal, no gate.

## Pipeline to run (new session, in order)

1. `/bk-specify "glp-runtime-consol"` — creates the spec + feature branch (e.g. `NNN-glp-runtime-consol`
   off develop). (`buildkit-roadmap next`/`brief glp-runtime-consol` prints the exact command.)
2. `/bk-plan` → `/bk-tasks` → `/bk-analyze` (resolve any CRITICAL before implement).
3. `/bk-marathon` — open a durable run for the feature.  ⚠ **Version caveat:** installed
   `buildkit-marathon` was `2026.7.13.1` while recent runs were created by `2026.7.27.1`; the 062
   marathon discharge was blocked by this. If marathon open/resume errors on version, run
   `buildkit-deploy latest all` first (broad env change — do it deliberately).
4. `/bk-implement` — sequence (B) dead-stub cleanup first (no gate); (A) as a spike, STOP at the
   §1.14 gate before any syntax-affecting change.
5. `/bk-codexreview` — adversarial review of the diff.
6. `/bk-ship` (+ release) — GitFlow feature→develop→release→main. 🔴 `buildkit ship` is
   **classifier-blocked for the agent** — the OPERATOR runs it via the `!` prompt prefix:
   `! $env:PYTHONUTF8='1'; & 'D:\bstdev\research\buildkit\.venv313\Scripts\buildkit-ship.exe' --skip-preflight --no-edit`
   CalVer is buildkit-derived (`vYYYY.MM.DD.N`, never hand-picked). If the pipeline gate says
   `implement != complete`, reconcile with `python -m buildkit_cli.pipeline.sidecar reconcile implement`.
7. `/bk-close` — retrospective + action reconciliation for the feature.
8. Post-ship: roadmap advance the feature → released/closed; roadmap-sync export→publish→push develop.

## Environment gotchas (carried forward)

- All buildkit CLIs = the **venv313** exes: `D:\bstdev\research\buildkit\.venv313\Scripts\*.exe`
  (the PATH `buildkit` is an older build; its `ship` lacks `--skip-preflight`). Set `PYTHONUTF8=1`.
- Tests: `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart` before `bash test/run_all_tests.sh`
  (else it picks a dead Linux path). Gleam 1.17 from `glp_gleam/`. C# `dotnet` 10.0.301.
- Roadmap-sync commits go on **develop** (push-your-own-branch rule allows it here). On a push
  divergence: `git rebase origin/develop`, resolve the generated `.import-manifest.json` conflict by
  taking either side then `roadmap import --rebuild-manifest` (regenerate — never hand-merge), commit, push.
- COOP v2 live channel = `I:\coop\glpnet` (this host Olamnit; lead ariellas; primary gavriella).
  UTC mechanical (`date -u`); publish exports to `roadmap-sync\inbox\`.

## Prior context (done, for the record)

Wave-4/062 SHIPPED `v2026.08.02.1` (PRs #124/#125/#126); waves 2/4/5 released→closed; roadmap
synced to **18 epics / 96 features / 2576 lines**. See `docs/handover/062-wave4-safe-restart-2026-08-02.md`.

## Resume in one line (after mandatory reading)

▎ Run the full pipeline on the promoted feature glp-runtime-consol per docs/handover/glp-runtime-consol-restart-2026-08-03.md: /bk-specify → plan → tasks → analyze → /bk-marathon → implement (STOP at the §1.14 gate on the antlr4 grammar) → codexreview → operator-run ship → close.
