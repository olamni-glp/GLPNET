<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# glp-runtime-consol (065) — SHIPPED & CLOSED handover (2026-08-04)

**Status:** ✅ **065 SHIPPED `v2026.08.04.2` + CLOSED.** Full pipeline done. Safe restart.
**Supersedes** `glp-runtime-consol-restart-2026-08-04.md` (the mid-pipeline one).

## One-screen result

Feature **`065-glp-runtime-consol`** ran the entire buildkit pipeline this session and shipped:
specify→plan→tasks→analyze→**implement**→**codexreview**→**ship**→**close**.

- **US1 / Scope A (antlr4 shared-grammar spike)** — §1.14 gate **APPROVED (Gabi + Udi, 2026-08-04)**.
  Delivered: faithful `spike/antlr4-glp-grammar/Glp.g4` (grounded line-by-line in
  `out/csharp/lib/compiler/{lexer,token}.cs` + `parser.cs`, cross-checked vs `parser.dart`),
  generated C# parser (`gen/`, committed), coverage harness (`harness/`), **SC-001 coverage 7/7 =
  100%** ANTLR-vs-hand-written accept/reject parity, `REPORT.md` **verdict GO-WITH-CONDITIONS**.
  **SC-002 (IL parity) DEFERRED** — needs an ANTLR-tree→engine-AST lowering bridge (~250–400 LOC),
  scoped in REPORT §3 as a follow-up PREP feature. Corrected 2 stale doc premises (`=..` not
  head-only; struct-in-list REPL accepted). REPL baseline 547/547.
- **US2 / Scope B (abandon C# dead-stub)** — shipped inside 065 (`8bc4b698`, removes
  `out/csharp/lib/runtime/abandon.cs`).
- **codexreview** — bounded 2-cycle codex: 1 HIGH fixed (committed `gen/` so the harness builds
  from a clean checkout, `ce44ba0b`); 2 findings = the SC-002 deferral (scope decision, kept).

## Objective anchors (verify, don't trust this summary)

| Fact | Value |
|---|---|
| On branch after ship | `develop` @ `207cfa4a` (back-merge PR #135) |
| Tag | **`v2026.08.04.2`** (local+remote); `.1` = ariellas/064 |
| Ship PRs | feature #133 · release #134 · back-merge #135 (all merged) |
| main ⊆ develop | YES (back-merge landed) |
| Retro report | `.specify/retrospective/065-glp-runtime-consol/20260804T180155Z636507.md` (4 findings) |
| Roadmap | 18 epics / 99 features / **2740** journal lines, pushed develop `ee2e1fd4` earlier |
| COOP | receipts posted; my status `I:\coop\glpnet\status\olamnit.md` cursor `20260804T164800Z` |

## 🔴 Open follow-ups (next-session work — NONE blocking)

1. **066 (`066-abandon-stub-cleanup`, origin `6c9cb8f1`, UNSHIPPED)** — its C# `abandon.cs` removal
   (`4e67492b`) is now **redundant** (065 shipped the same removal). Its **unique, un-shipped value**
   is the **live Dart `glp_runtime/lib/runtime/abandon.dart` removal + dead import in `runtime.dart`
   + the codeconv inventory reconcile** (`40f6ec5d` + `6c9cb8f1`). Plan: **rebase 066 onto develop**
   (drops the dup `4e67492b`), keep the Dart+codeconv work, **renumber to 068+** at merge (gavriella's
   `066-wave6-consolidation` + `067-qr-link-provisioning` own 066/067), ship as next-free CalVer
   (**`.3`**). Or fold the Dart+codeconv removal into another feature.
2. **SC-002 PREP feature** — the ANTLR-tree→engine-AST lowering bridge (~250–400 LOC) to close IL
   parity + decide production adoption. Candidate roadmap feature **068+**. Not yet captured (needs
   engineer confirm via `/bk-roadmap`).
3. 🔴 **Security (fleet)** — `glpquick-cert/glpquick.key` + `glpquick.pfx` are **tracked private keys
   in git** across all clones (`.gitignore:114` is inert — files predate it). Reported by gavriella
   (153153Z) to the engineer; **do NOT fix from one host** (a history rewrite forks the fleet) — needs
   a coordinated call.
4. **codeconv tooling defect** — `reconcile` (even `--feature`) times out >2 min, and the roadmap
   `link`-scan downgrades post-implement states (ariellas `122707Z`). Buildkit-side fix. Retro finding
   `fnd-cb0bb98617`.

## Restart order (objective)

1. `git branch --show-current` → likely `develop`; `git tag | grep v2026.08.04` → `.2` present ⇒ 065
   is shipped, nothing to resume on 065.
2. `buildkit-roadmap next` / this doc for the next feature (066-follow-up or SC-002 PREP).
3. COOP: mirror `I:\coop\glpnet` → `D:`, read peers (gavriella/ariellas), poll inbox after cursor
   `20260804T164800Z`, ACK anything owed. Drive law G:=Olamnit / H:=Ariellas / I:=Gavriella.
4. Memory `[[glp-runtime-consol-restart]]` (updated to SHIPPED).

## Environment (carried)

- buildkit CLIs = `D:\bstdev\research\buildkit\.venv313\Scripts\*.exe`; `PYTHONUTF8=1`.
- Tests: `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart` before `bash test/run_all_tests.sh` (547).
- ANTLR spike: jar (gitignored) re-download per REPORT/T007; Java 17 at `C:\Users\smbuser\java\jdk-17.0.19+10\bin`; regen `gen/` per REPORT T011; harness builds from committed `gen/` (NuGet `Antlr4.Runtime.Standard` 4.13.1 + engine project ref).
- **codex works** (`~/.codex/config.toml [windows] sandbox=unelevated`). buildkit-ship runs from agent-Bash now (shipped 065 that way).
- Pre-existing untracked (NOT mine): `.specify/retrospective/062-*`, `test/parity/cross_runtime/results/*`.
