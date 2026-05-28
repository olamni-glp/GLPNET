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

- **Current phase**: Phase 4 — US2 strict tier (T023–T029). Step-side plumbing landed; bulk codegen is the gated long-pole.
- **Current task**: **A (bulk codegen drive) IN-PROGRESS this session** — pre-req B (Converted.props append hook) landed at `bfd00a8a`; now driving `/codeconv-codegen` for the 75 ready→converted files. After bulk codegen produces a runnable C# REPL: T017 (live trace instrumentation), T022 (`@needs_runtime` e2e), then T026–T029 (CLI next/status/ingest/retry/escalations + `/codeconv-equiv` skill + strict-tier gate test). T025 (durable-stage wiring) DONE prior session.
- **Last green baseline**: 46/46 pure equiv tests + 15/15 isolated planagents (warm bridge) + 12/12 NEW `buildprops` pure tests + 5/5 ingest tests (one transient bridge-cold-spawn re-passed alone) green 2026-05-28. The full suite still has the pre-existing `@needs_bridge` skipif-not-a-marker flakiness — see Bridge-test flakiness section. NOTE: `-m "not needs_bridge"` filtering does NOT exclude these (skipif decorator ≠ pytest marker); for a fast pure run, name the pure test files explicitly.
- **Last checkpoint commits** (chain, 2026-05-28): `824b8d46` T016 corpus.py + reviewed `.codeconv/equiv-manifest/corpus.yml` (256 sources; book 141 exact) + materialized split into subsystems.yml · `2ae54423` T018/T019 capture/compare/bytecode-diff CLI (standalone deterministic verdict over recorded artifacts; **DB writes deferred to durable step** — Gabi decision b) + shared `codeconv.db.engine.connect` (Gabi decision a) · `58bfbf99` T023/T024 readiness + durable-step PURE core (`compute_step_result`) · `dc997583` T025 (wire `step_equiv` into `durable/steps.py` + `durable/workflows.py`) + C# REPL infrastructure (`out/csharp/glp_runtime_net.csproj`/`.sln` + `Converted.props` + `glp_repl/{glp_repl.csproj,Program.cs}` placeholder; `dotnet build` green) · `bfd00a8a` Converted.props codegen append hook in `tools/codegen/workflow.py` + new `tools/codegen/buildprops.py` module (idempotent add-before-build / revert-on-fail) + 12 pure tests — bulk-codegen pre-req B; existing ingest tests stay green (no-op when `Converted.props` absent). All commits on branch `020-trace-equivalence-fidelity`; NOT pushed (Gabi's call).
- **Last GEPA artifact written**: — (none; US3 not reached)
- **C# REPL state**: `out/csharp/` has 177 feature-016 scaffold stubs (Dart content under `.cs`); 0 codegen rows; 20 ready; 75 files in scope. `glp_runtime_net.sln` builds GREEN with `dotnet build` (lib compiles to empty assembly via `EnableDefaultCompileItems=false` + empty `Converted.props`; `glp_repl` exe builds via the placeholder `Program.cs`). Bulk codegen via `/codeconv-codegen` is what populates `Converted.props` and the actual library.

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
- [~] **Phase 3 US1 oracle (MVP)** (T013–T022) — DONE except runtime-gated tail. T013/T014/T015 (pure normalize/relation/bytecode_diff) + T016 (corpus.py + reviewed corpus.yml) + T018 (capture orchestration; live-spawn backend gated on T017) + T019 (compare/bytecode-diff standalone deterministic, NO DB write — Gabi decision b) + T020/T021 (SC-005 batteries). 46/46 pure equiv tests green. REMAINING (runtime-gated, blocked on bulk codegen → runnable C# REPL): T017 live C# REPL trace instrumentation (`@needs_runtime`), T022 e2e (`@needs_runtime`).
      Finding (no escalation; R10/B1): live Dart `:trace` is reduction-level only — fine-grained UNIFY/WRITER_BIND/REACTIVATE/BYTECODE_OP events live in `:debug` per-op prints; `parse_dart` live-text wiring consumes both, finalized at T017/T022 against real captures.
- [~] **Phase 4 US2 strict tier** (T023–T029) — STEP-SIDE PLUMBING LANDED. T023 `readiness.py` (PURE, four-state classify + curriculum order) + T024 durable equiv-step PURE core (`workflow.compute_step_result` + `step_equiv` bridge wrapper) + T025 (this session: registered `step_equiv` in `durable/steps.py`, placement note in `durable/workflows.py`; equiv mirrors codegen's separately-driven pattern — registered but NOT in `PER_UNIT_STAGES`, driven by `/codeconv-equiv`). REMAINING: T026 (`equiv next/status/ingest/retry` CLI), T027 (`equiv aggregate-escalations`), T028 (`/codeconv-equiv` skill), T029 (`@needs_runtime` strict-tier gate test). Most need the runnable C# REPL → bulk codegen first.
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

---

## Safe restart for A (bulk codegen drive) — 2026-05-28

**This is the next session's job.** Gabi's plan: B (infra) DONE this session → C (T025
durable wiring) DONE this session → **A (bulk codegen) in a NEW dedicated session**.

### Mission
Convert the 75 in-scope Dart files under `glp_runtime/lib/...` to C# under `out/csharp/lib/...`
via the `/codeconv-codegen` agent loop, in dependency order, until the strict-tier subsystems
(`heap` / `bytecode` / `compiler` / `runtime-core`) compile as a coherent library and a
runnable `glp_repl` executable can be built. This unblocks T017 (live trace instrumentation)
and T022 (e2e). The conversion is LLM-driven, build-gated, escalate-don't-guess (019 design).

### Starting state (committed; verify on entry)
- `out/csharp/glp_runtime_net.sln` + `glp_runtime_net.csproj` + `Converted.props` + `glp_repl/{glp_repl.csproj,Program.cs}` ALL build green (`dotnet build out/csharp/glp_runtime_net.sln` → 0 errors, ~6 s).
- 177 feature-016 scaffold stubs (Dart content under `.cs`) under `out/csharp/lib/`,
  EXCLUDED from compilation by `EnableDefaultCompileItems=false` + empty `Converted.props`.
- `codeconv codegen status` (`--data-dir C:/pglite/research/glpnet`): `files_total=75,
  codegen_ready=20, not_started=75, optimized_prompt=false, prompt_warning="no optimized
  prompt; using baseline"`. The 20 ready files (topo_level=0) are the leaves — start there.
- Per-subsystem optimized prompts (T036) NOT authored → baseline prompt is the active one.
  This is acceptable for the first pass (the bulk drive); GEPA refinement (US3) is later.

### What's needed before the first `/codeconv-codegen` invocation
1. **Append hook in `codeconv/src/codeconv/tools/codegen/workflow.py`** — on a successful
   build-gate accept, append `<Compile Include="lib/<rel>.cs" />` to `out/csharp/Converted.props`
   (idempotent — don't dup). Roughly 10–15 lines of code. The build-gate must run AFTER the
   include is in place, else the gate trivially passes on an unincluded file. Order:
   write `.cs` → append to `Converted.props` → `run_build` → on fail, optionally revert the
   append (or leave + let the next attempt overwrite). Keep the revert simple.
2. **Verify**: with `Converted.props` listing the first file, `dotnet build out/csharp/glp_runtime_net.sln`
   must still pass (catches a workflow misuse before bulk).

### Bulk drive loop (per file)
1. `codeconv --data-dir C:/pglite/research/glpnet codegen next --json` → pick one ready file.
2. Invoke `/codeconv-codegen` for that file. The skill orchestrates: read the plan + convspec +
   the Dart source, write `out/csharp/<rel>.cs`, run the build-gate (`dotnet build` via the
   new Converted.props), accept on pass / escalate on fail (escalation artifact under
   `.codeconv/codegen-escalations/<rel>.dart.md`).
3. On accept → `dart_codegen.codegen_completed_at` is set → the file unblocks its downstream.
4. Loop to next ready. Re-check `codegen status` periodically to confirm progress + green.

### Build-time test (cheap; run every ~5–10 files accepted)
- `dotnet build out/csharp/glp_runtime_net.sln --nologo -v quiet` → must stay GREEN.
- A red build is a regression in the LAST converted file → escalate (the build-gate should
  have caught it; if it slipped through, there's a workflow bug — STOP & report).

### Stopping conditions for the session
- All 75 files converted + library + REPL placeholder build green → ready for **T017**
  (replace `glp_repl/Program.cs` with the converted REPL + add `:trace` instrumentation
  per `contracts/trace_normalization.md`).
- Context pressure → checkpoint commit ("bulk codegen: N/75 files converted; build green"),
  update POSITION above, signal Gabi for the NEXT bulk session.

### Hard discipline (re-read first on entry)
- **Spec-first** (CLAUDE.md): conversion idioms come from `.codeconv/conversion-plans/<rel>.dart.md`
  + `.codeconv/conversion-specs/<rel>.dart.md` (017/018 artifacts) + `conversion_idioms` KB. The
  agent DOES NOT INVENT — it follows the plan/spec; ambiguities STOP and ask.
- **No LM call from `tools/codegen` / `tools/equiv` / `durable/`** (SC-008). The agent (Claude)
  IS the LM here; `tools/codegen` itself stays LM-free — the agent runs OUTSIDE the tool.
- **No `git add -A`**. Stage by name. Commit boundaries: every accepted file OR every N files
  (e.g., N=10) with a one-line checkpoint message.
- **`--data-dir C:/pglite/research/glpnet`** on every bridge-touching call (CLAUDE.md mandate).

### Why NOT this session
This is a long, LM-heavy, file-by-file synthesis loop. Doing it here would burn the context
window on a small number of files. The dedicated session starts fresh, with this ledger as
its restart map, and runs as long as it can. Signal Gabi when ready to start that session.

---

## Context

Feature 020 = a **fidelity layer over 019's deterministic codegen engine**: a deterministic,
LM-free differential equivalence oracle (Dart golden vs converted-C# candidate, normalized
causal/partial-order traces) + a tiered fidelity metric + real `dspy.GEPA` (offline,
per-subsystem) driving codegen toward trace-equivalence. MVP = US1 (the oracle alone, usable
as a conformance harness). 019's (C)-hybrid LM-containment invariant is preserved verbatim.

Commit discipline (CLAUDE.md): stage by name only; commit checkpoint-green boundaries; offer
Gabi the merge template at end; never merge to `main`. Every bridge-touching `codeconv` call:
`--data-dir C:/pglite/research/glpnet`, `--test-concurrency=1`.
