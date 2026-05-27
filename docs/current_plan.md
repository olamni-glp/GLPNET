# Current Plan: 020 Trace-Equivalence Fidelity — Implementation (safe-restart ledger)

Started: 2026-05-27
Branch: `020-trace-equivalence-fidelity`
Source of truth for tasks: `specs/020-trace-equivalence-fidelity/tasks.md` (50 tasks, 8 phases)
Handoff: `specs/020-trace-equivalence-fidelity/HANDOFF-implement.md`

> This file is the **resumable checkpoint ledger** for a long, multi-cycle session
> (real `dspy.GEPA` + codegen refinement loops). On any restart — fresh session OR
> post-compaction — read this file FIRST, confirm POSITION, verify the last green
> baseline, then resume from the CURRENT marker. Never assume prior in-memory state.

---

## POSITION (update on every phase boundary)

- **Current phase**: Phase 3 — US1 oracle MVP (T013–T022). Pure oracle core landed.
- **Current task**: T016 (`corpus.py`) ← CURRENT. T013/T014/T015 (pure `normalize.py`/`relation.py`/`bytecode_diff.py`) DONE + SC-005 tests T020/T021 DONE (the Phase-3 restart-green bar). Remaining Phase 3: T016 corpus, T017 C# instrumentation (`@needs_runtime`), T018 capture, T019 compare/bytecode-diff CLI, T022 e2e (`@needs_runtime`). Phases 1–2 DONE.
- **Last green baseline**: T001 done 2026-05-27 — full suite 401 passed / 3 skipped / 11 bridge-spawn-timeout "failures", ALL reproduced GREEN in isolation (see flakiness note). Accepted as green. NOTE 2026-05-27: `-m "not needs_bridge and not needs_runtime"` does NOT shrink the suite much — most codegen/convspec tests use the bridge WITHOUT the `needs_bridge` marker (~7–9 s each, 439 total). For a truly fast pure run, name the pure test files explicitly (`test_equiv_*`, `test_fidelity_metric`, `test_trace_normalize`). A full-suite collision with concurrent sessions hung a run at 23:39→23:50 (killed); 1–131/439 were green with no regression before kill.
- **Last checkpoint commit**: `9710ce10` (2026-05-27) — Setup + Foundational (T001–T012), 14 pure tests green. NOT pushed (Gabi's call). T013–T015+T020–T021 checkpoint commit follows.
- **Last GEPA artifact written**: — (none; US3 not reached)

### Phase 1 Setup status — DONE
- [X] T001 — baseline accepted green (see above).
- [X] T002 `tools/equiv/` skeleton — auto-discovered; bare→status works.
- [X] T003 artifact dirs + READMEs (`equiv-manifest`, `codegen-prompt`, `conversion-equiv`).
- [X] T004 `@needs_runtime` marker in conftest (skips until built C# REPL).

### Carried finding (pre-existing, NOT introduced)
- **019 codegen bare-invocation bug**: `codeconv codegen` (no subcommand) crashes with
  `TypeError: status() missing 1 required positional argument: 'ctx'` — `ctx.invoke(status)`
  doesn't inject `ctx` under the installed Click/Typer. Equiv avoids it (delegates to
  `_run_status`). Flagged to Gabi; codegen fix deferred (019 code, needs approval).

---

## Restart procedure (do this on ANY new session / post-compaction)

1. **Start-of-session ritual** (CLAUDE.md): read `CLAUDE.md`, `docs/DISCIPLINE.md`,
   `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` → acknowledge each. The GLP
   trace-event semantics (three-phase HEAD/GUARD/BODY, SRSW, writer-MGU, three-valued
   unification) ARE the oracle's measured events — this reading is load-bearing for
   T007/T013/T014.
2. If **emerging from compaction**: STOP, tell Gabi, summarise from this ledger, ask
   how to proceed (CLAUDE.md mandate). Do not silently continue.
3. Read this file's **POSITION** block. Confirm branch is `020-trace-equivalence-fidelity`.
4. **Verify the last green baseline still holds** before touching code:
   `cd codeconv && .venv\Scripts\python -m pytest -q` (use `--test-concurrency=1`; PGLite cold-init ~7 s).
   If it is NOT green, STOP and report — do not stack new work on a red baseline.
5. Resume at the CURRENT task. Within a phase, follow the task order in `tasks.md`;
   `[P]` tasks may run together (different files).

---

## Phase checkpoints — each is a SAFE RESTART POINT

A phase is "checkpoint-green" when its pytest subset passes AND the 019 baseline is still
green (FR-019). At every checkpoint-green boundary: update POSITION above, then
**stage by name and commit** a checkpoint (do NOT `git add -A`; do NOT merge to main —
only Gabi merges). The commit is what makes the restart trivial.

- [X] **Phase 1 Setup** (T001–T004) — DONE 2026-05-27. baseline recorded; `tools/equiv/` skeleton + artifact dirs + `@needs_runtime` marker.
      Restart-green: 019 baseline green (modulo bridge flakiness) + `tools/equiv` auto-discovered + collect-only clean.
- [X] **Phase 2 Foundational** (T005–T012) — DONE 2026-05-27. migration `0008` (single head off `0007`), `trace.py`, `fidelity.py`, `manifest.py`, `subsystems.yml`, tombstone keys.
      Restart-green: 14 pure tests GREEN (T012 tier boundaries + offline single-head/chain). Manifest validated vs real inventory (0 ties/0 unclassified). Bridge-gated migrate-idempotency (T006/0008) deferred to full checkpoint run. **Runtime-free.**
- [~] **Phase 3 US1 oracle (MVP)** (T013–T022) — IN PROGRESS. DONE: T013 `normalize.py` (first-occ heap→logical relabel + writer-MGU causal edges; canonical wire-format `parse_dart`/`parse_csharp`), T014 `relation.py` (OUTCOME / STRICT total-order / DYNAMIC partial-order via causal-canonical-key iso), T015 `bytecode_diff.py`, T020 + T021 SC-005 batteries (+ T013 parser tests). **Restart-green ACHIEVED: T020 (no false divergence, incl. heap-relabel + independent-goal reorder) + T021 (no false equivalence, incl. eager-writer-bind) — 21/21 pure green.** REMAINING: T016 `corpus.py`, T017 C# instrumentation (`@needs_runtime`), T018 `capture`, T019 `compare`/`bytecode-diff` CLI (standalone, NO DBOS), T022 e2e (`@needs_runtime`).
      Finding (no escalation; R10/B1): live Dart `:trace` is reduction-level only — fine-grained UNIFY/WRITER_BIND/REACTIVATE/BYTECODE_OP events live in `:debug` per-op prints; `parse_dart` live-text wiring consumes both, finalized at T017/T022 against real captures.
- [ ] **Phase 4 US2 strict tier** (T023–T029) — `readiness.py`, durable `equiv` step wrapping US1 compare, stage wiring, `equiv next|status|ingest|retry|aggregate-escalations`, `/codeconv-equiv` skill.
      Restart-green: T029 strict-tier gate (`@needs_runtime`).
- [ ] **Phase 5 US3 real GEPA (OFFLINE)** (T030–T038) — rewire `codegen_opt` to `dspy.GEPA`, metric→`fidelity.py`, datasets, per-subsystem prompts + `_base.md`, `/codeconv-codegen-opt` extension.
      Restart-green: T037 (mocked-LM ≥ baseline + budget cap) + T038 (no-LM-import on production path). **See GEPA restart notes below.**
- [ ] **Phase 6 US4 dynamic tier** (T039–T042) — T039 mode DECISION (gate, precedes bulk multiagent gen) → T040 both modes behind the flag, reclassification, dynamic test.
      Restart-green: T042 (`@needs_runtime`). **Do NOT bulk-generate `multiagent` before T039 is recorded in contracts/subsystem_curriculum.md.**
- [ ] **Phase 7 US5 promotion** (T043–T045) — `equiv fidelity|promote|mark-stale`, `stale.py`, promotion-gate test.
      Restart-green: T045 (promote only at corpus full-equivalence).
- [ ] **Phase 8 Polish** (T046–T050) — FR-017 spec-violation surfacing, tombstone round-trip, docs, **T049 full 020+019 suite green + commit**, T050 SC roll-up.
      Restart-green: T049 full suite green.

---

## HARD GATES / carried risks (re-read before the relevant phase — from HANDOFF + plan)

1. **B1 bootstrapping (by design)**: US1 `@needs_runtime` tasks (T017 C#-REPL trace, T022 e2e)
   only fully run once US2 produces a runnable converted C# REPL. Build the pure core
   (T013–T015) + schema first; runtime-coupled tests land as the strict tier compiles.
   Pure modules are fully testable immediately — do not block on the REPL.
2. **Replay-safety (R12, top risk)**: NEVER spawn a REPL or read wall-clock inside the
   durable `equiv` step. Nondeterministic capture lives in the CLI / `/codeconv-equiv`
   skill ONLY; the step is a pure verdict ingest of recorded traces (019 `needs_agent_work`).
3. **LM containment (SC-008)**: `tools/equiv/`, `tools/codegen/`, `durable/` import NO
   dspy/litellm/openai. T038 guards it — keep green. GEPA/LM live ONLY in `tools/codegen_opt/`, offline.
4. **Migration single-head**: `0008` chains off the single `0007` head (historical dual-`0003` exists). T006 asserts one head.
5. **GLP authority**: trace event kinds (unify outcome / suspend / reactivate / writer-bind /
   bytecode-op) are GLP three-phase + SRSW + writer-MGU semantics. Do NOT invent events.
   If a needed event is absent from Dart `:trace`, STOP & report. If a divergence traces to
   a Dart original that violates the GLP spec → CLAUDE.md Bug-Protocol report, do NOT alter
   C# to match a wrong oracle (FR-017).
6. **Dart golden is read-only**: no change to `glp_runtime/`. Trace hooks go in the
   *converted* C# REPL (`out/csharp/`) only.

---

## GEPA / DSPy cycle restart notes (Phase 5, and any re-optimization)

The optimizer is the one long, non-deterministic, expensive, LM-bearing stage. Restart safety here is different from deterministic phases:

- **Offline + non-durable by design** — GEPA runs are NOT inside DBOS. A killed GEPA run loses
  only that run; it never corrupts the durable pipeline or the production path.
- **Hard budget cap (SC-006)**: every optimize invocation takes `--budget`; on cap it must
  return **best-so-far**. A restart re-runs from the carried-forward seed prompt, not from zero.
- **Carry-forward seed**: per-subsystem prompts descend from `_base.md`. The frozen prompt for
  each completed subsystem is the durable artifact in `.codeconv/codegen-prompt/<subsystem>.md`
  (checked in, with provenance front-matter). THAT file — not in-memory GEPA state — is the
  resume point. Record "Last GEPA artifact written" in POSITION after each subsystem freezes.
- **Metric identity**: the GEPA metric score MUST equal the production gate via
  `import tools.equiv.fidelity` (T031/SC-004). If they ever diverge, STOP — the optimizer is
  chasing a different target than the gate.
- **Tests use a MOCKED LM only** (T037). Never call a real LM in pytest.

---

## Bridge-test flakiness (operational — affects every checkpoint re-run)

The `@needs_bridge` tests (`test_depgraph_*`, `test_discover_*`, `test_phase7_verifications::test_schema_isolation`)
each cold-spawn a PGLite bridge (~7–13 s) + run the full migrate chain. Two failure modes observed
2026-05-27, BOTH non-regressions (identical symptom: `codeconv migrate: bridge unreachable:
timed out waiting for bridge to become reachable after 60.0s`):
1. **Concurrent-run collision** — a second `pytest` running at the same time (another session) made
   bridge spawns time each other out → 10 spurious failures. **Mitigation: never run the full
   codeconv suite while another pytest/bridge is live.** Check first:
   `Get-CimInstance Win32_Process -Filter "Name='python.exe'" | ? { $_.CommandLine -like '*pytest*' }`
   and `Name='node.exe'` for stray bridges.
2. **Transient cold-spawn timeout** — even solo, an occasional bridge spawn exceeds the 60 s window
   → 1 spurious failure; passes on isolated re-run (15 s).

**Checkpoint/restart rule**: a `@needs_bridge` failure that is the `bridge unreachable … 60.0s`
timeout is NOT a regression — re-run that test alone to confirm green before treating it as red.
A real regression is an assertion on data (row contents, diff, tombstone), not a spawn timeout.
The full suite takes ~1 h wall-clock; for fast iteration use pure-subset runs
(`-m "not needs_bridge and not needs_runtime"`) and reserve the full bridge run for phase checkpoints.

## Context

Feature 020 = a **fidelity layer over 019's deterministic codegen engine**: a deterministic,
LM-free differential equivalence oracle (Dart golden vs converted-C# candidate, normalized
causal/partial-order traces) + a tiered fidelity metric + real `dspy.GEPA` (offline,
per-subsystem) driving codegen toward trace-equivalence. MVP = US1 (the oracle alone, usable
as a conformance harness). 019's (C)-hybrid LM-containment invariant is preserved verbatim.

Commit discipline (CLAUDE.md): stage by name only; commit checkpoint-green boundaries; offer
Gabi the merge template at end; never merge to `main`. Every bridge-touching `codeconv` call:
`--data-dir C:/pglite/research/glpnet`, `--test-concurrency=1`.
