# Safe-Restart Handover — madGLP writer-reader lane + coordination convergence

**Date:** 2026-08-14
**Author:** Claude (olamnit, glpnet compiler/engine lane)
**Status:** In Progress — two lanes advanced; next session continues 079 from `/bk-plan`

---

## Restart order (objective, per CLAUDE.md Multi-Stage Persistence)

1. `buildkit-roadmap next` / `status` — the roadmap is authoritative for WHAT.
2. Active feature: `.specify/feature.json` → **`specs/079-madglp-writer-reader-discipline`** (specify+clarify COMPLETE).
3. Pipeline position: `python -m buildkit_cli.pipeline.cli switch 079-madglp-writer-reader-discipline` → shows next stage = **plan**.

## NEXT (do this first in the new session)

**Continue lane 2 = `079-madglp-writer-reader-discipline` through the pipeline: `/bk-plan → /bk-tasks → /bk-analyze → /bk-implement → /bk-codexreview → /bk-ship → /bk-close`.** Engineer-approved (2026-08-14) to run to full completion.

- Branch `079-madglp-writer-reader-discipline` (pushed, spec+clarify committed). `.specify/feature.json` points at it; sidecar specify+clarify = complete.
- **Verified source facts (spec is grounded in these):**
  - `glp_runtime/lib/runtime/heap_fcp.dart:236 pairedReaderAddr()` — line **242** `return writerAddr + 1;` is the fallback to remove (FR-001/FR-002: fail loud when the cross-pointer `readerForWriter()` returns null instead of guessing +1).
  - `glp_runtime/lib/multiagent/mad_helpers.dart:61-64` — `GlobalSendSpawn.readerAddr` doc says "reader to watch" but holds an onBind **writer** key → rename/re-doc (FR-006).
  - `docs/bug-send-globalise-localise.md` + `three_agent_pipeline_boot` (in `glp_runtime/test/multiagent/multiagent_glp_test.dart`) → verify to a verdict (FR-005).
- 🔴 **Core-file + maGLP constraints (CLAUDE.md):** `heap_fcp.dart` is a core file. FR-008 = audit-first, behaviour-preserving; **surface the core diff explicitly before it lands.** maGLP rule: modify only `lib/multiagent/` freely; core `heap_fcp.dart` change is the audit's single deliberate touch (removing dead-in-correct-state fallback). FR-009 (ESCALATE E5): confirm scope after inspecting heap_fcp.dart; split any residual that proves bigger than an audit-close.
- **NOT §1.14** (implementation audit; FCP cross-pointer architecture unchanged).
- **Test Protocol:** baseline recorded this session = REPL suite green through Section S (`scratchpad/baseline_madglp.txt`), aborts at 064 Section T (the known unguarded-abort PR #158 fixes, NOT merged to develop yet — orthogonal). For madglp, also run `cd glp_runtime && dart test test/multiagent/` before+after. `DART=C:/src/flutter/bin/cache/dart-sdk/bin/dart.exe`, `DOTNET_ROOT=C:\Users\smbuser\AppData\Local\Microsoft\dotnet`.
- **SHIP-TOKEN (fleet):** at most one host in ship→close fleet-wide (ariella/gavriella agreed). Acquire/announce the SHIP-TOKEN in coop BEFORE `/bk-ship`. Also the SYNC-TOKEN (one host per sync round; completion = peer import, not local replay-verify PASS).

## Lane 1 = occurs-check (078) — BLOCKED on Udi, do not implement

- `specs/078-occurs-checked-substitution` — specify+clarify COMPLETE. **FR-002 (UnifyFail vs CompileError) is the §1.14 decision, recorded OPEN, selects NEITHER — awaiting Udi.** Roadmap `occurs-checked-substitution-pipeline` is now `promoted` (ariella promoted it 2026-08-14) but promotion ≠ §1.14 authorisation. Do NOT run plan/implement until Udi rules. ariella notes the callee-end §1.14 question is with the engineer now and may move both lanes at once.

## Coordination fix — TRIANGULATED, captured, promoted

- Feature `coordination-feature-stream-durable-superset-fix` = **promoted #13 (4.25/2625)**. Three independent /bk-3rtask runs converged (ariella `2855` plan, olamnit `ba84` code root-cause, gavriella `74d2` 7-defects). Curator: `.specify/3rtask/runs/20260813T192429Z-ba84/curator_report.md`. Key: D1 (no committed allocate) + D4 (no WP retire) are BOTH CLASS-B = expose committed CLI writers over op types that already exist (`transition` in OP_TYPES; but reconcile.py:33 folds only allocate/claim + reconcile.py:169 R2 fold unimplemented → verb AND fold). Named F1 acceptance test: 3rtask merge must refuse on empty claim-key, never emit `corroborated`. This is the highest-leverage next feature (it's what restarts the WP stream) but is fleet-coordination-authority → needs engineer go-ahead to /bk-specify.

## Open items / owed
- **Merges parked (classifier-gated):** PRs #158 (064 Section-T fix — merging it fixes the suite baseline), #159, #160, #161, #162, #163, + branches 078/079. PR #153 still ends the recurring `.import-manifest.json` conflict.
- **Board deploy-pin broken on olamnit:** scheduler engine-resolution refuses (pinned to an uninstalled buildkit version; running 2026.8.10.1). `buildkit-deploy latest` advanced the registry but the scheduler engine pin persists — use `BUILDKIT_ENGINE_OVERRIDE` for board ops. Pipeline (sidecar+roadmap) unaffected. This is a `per-host-toolchain-contract` instance.
- **Coop:** R1 ACK of ariella's 9-feature redistribution posted (`131135Z`); occurs-check promote + lane-2 claim ACK'd; cursor aligned to my newest published leg `002859Z` (→ now `132840Z`). No transport-pick from gavriella yet.
- **065 spec-dir identity** (`065-ynet-consolidation` vs `065-glp-runtime-consol`) — resolve before any 065 resume (mine).

## Latest state
- Roadmap: 20 epics / 115 features / **3584 lines**; export `olamnit__glpnet__20260814T132840Z.json` published; replay-verify ✓; dedupe 0 twins.
- Marathon: this lane is not yet under a marathon run id; `/bk-marathon` can wrap 079's plan→ship→close for durability.
