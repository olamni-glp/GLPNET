---
description: "Task list for 027-refinement-verification-framework"
---

# Tasks: Iterative Refinement & Verification Framework

**Input**: Design documents from `/specs/027-refinement-verification-framework/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: No separate TDD test phase is requested. This is a methodology + research-validation feature;
the **three validation spikes ARE the acceptance tests** — each must run against its **real tool** (Lean 4 /
MLIR / SPIN) and record a reproducible `RESULT.md` (R13/R14, FR-070). Doc-completeness + no-API grep gates
serve as the remaining verification.

**Organization**: tasks grouped by the five user stories from spec.md. US1/US2/US3/US5 are P1; US4 is P2.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: US1–US5 (Setup/Foundational/Polish carry no story label)

## Path Conventions
- Framework artifacts: `docs/research/repl-engine-separation/reconciliation/`
- Validation spikes: `docs/research/repl-engine-separation/spikes/{lean,mlir,spin}/`
- Harness runtime: reuse `codeconv/.venv` (Python 3.11) — no new package
- ⚠ **No external LM API** anywhere (FR-012/073): LM steps run in Claude via Agent-tool/MCP only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: scaffold the spike subtree and confirm the harness runtime.

- [X] T001 Create the spike subtree skeleton: `docs/research/repl-engine-separation/spikes/{lean,mlir,spin}/`, each with placeholder `run.sh`, `run.ps1`, `tool-versions.txt`, and `RESULT.md` stubs
- [X] T002 [P] Confirm `codeconv/.venv` is usable for the harnesses and record the baseline Python version in each spike's `tool-versions.txt` (actual baseline: Python **3.14.3**; the "3.11" in this line was a stale assumption)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: gate inputs + finalize the ONE shared methodology file (edited by US1+US2; isolated here to
avoid cross-story file conflicts).

**⚠️ CRITICAL**: complete before US1/US2 begin.

- [X] T003 Read-only input gate: verify the 026 artifacts are present + authoritative on this branch — `REFINEMENT-METHOD.md`, `DECISIONS-FOR-OWNER.md`, `DECISIONS-LOG.md` (R1–R15), `DEFERRALS.md` (DEF-A3/B1/B2/H1) — under `docs/research/repl-engine-separation/reconciliation/`; STOP + report if any is missing
- [X] T004 Finalize `docs/research/repl-engine-separation/reconciliation/REFINEMENT-METHOD.md` as the authoritative framework artifact (FR-001): §1 loop, §2 pragmatic+formal metric model, §4 the **six formal-tooling slots** each with name + threshold-shape + dependency-pointer (FR-022, SC-004), §5 the **five Shapiro criteria** mandatory/advisory map + embedded-switch framing (FR-050/051, SC-005), the no-API rule, and the budget-cap discipline
- [X] T005 Finalize `docs/research/repl-engine-separation/reconciliation/DECISIONS-FOR-OWNER.md` (FR-002): prover choice (Lean 4 primary / Rocq alternative), MLIR dialect primitive spec, Shapiro mandatory/advisory mapping, tactic-loop depth limit — cross-linked to ratified R1–R15

**Checkpoint**: methodology file authoritative; tooling slots + Shapiro map present → US1/US2 can start.

---

## Phase 3: User Story 1 — Shared metric-combination template + framework skeleton (Priority: P1) 🎯 MVP

**Goal**: a reusable, owner-gated way for any successor seed (#2–#16) to state "done".

**Independent Test**: instantiate the template end-to-end for one reconciled seed (e.g. #5 result codec) and
confirm the table is well-formed (every metric has kind + concrete tool + measurable threshold) and the
formal tier is present because the seed touches a wire/byte contract (SC-008).

- [X] T006 [US1] Create `docs/research/repl-engine-separation/reconciliation/METRIC-COMBINATION-TEMPLATE.md`: the `name | kind (pragmatic\|formal) | tool | threshold` template (FR-003, R8) **plus a filled worked example** for #5 (≥1 formal row because it touches the byte contract — US1-AC1) **plus the host/infra rule** (a #8/#10 table may omit formal rows but must carry a per-Shapiro-criterion N/A justification — R9, US1-AC2) — DONE (block 05): template + #5 worked example (3 pragmatic + 4 formal rows) + #10 worked N/A justification across all 5 Shapiro criteria + #8 caveat
- [X] T007 [P] [US1] Create `docs/research/repl-engine-separation/reconciliation/INTERACTIVE-SPEC-STEP.md`: the owner-confirmation protocol (agent proposes metric table + tools → owner confirms/amends → recorded in the seed spec **before** task generation — FR-060, US1-AC3) and the rule that each seed's `PRE-SPECIFY` pointer surfaces `DECISIONS-LOG.md` + `DEFERRALS.md` (apply in-scope R-rows; action anchored DEF-rows — FR-061) — DONE (block 05): strict PROPOSE→CONFIRM/AMEND→RECORD→/buildkit-tasks ordering + PRE-SPECIFY (R-rows APPLIED, DEF-rows ACTIONED) feeding PROPOSE
- [X] T008 [US1] Validate the template by instantiating it for one reconciled seed in `METRIC-COMBINATION-TEMPLATE.md` (worked example from T006) and confirm well-formedness + formal-tier presence without inventing format (SC-001/008) — DONE (block 05): #5 instantiation validated — all 7 rows well-formed (kind + concrete runnable tool + measurable threshold), formal tier present (4 formal rows) because #5 touches the byte/wire contract

**Checkpoint**: a seed engineer can state "done" from the template alone.

---

## Phase 4: User Story 2 — Claude-only GEPA/DSPy refinement loop (Priority: P1)

**Goal**: every successor loops the same bounded, no-API way against its metric combination.

**Independent Test**: trace the loop description against `codeconv/src/codeconv/tools/codegen_opt/optimize.py`
`run_optimize` (~257–335) and confirm generate/propose/evaluate/budget map 1:1 and no external-API path is
reachable (SC-002/003).

- [X] T009 [US2] In `REFINEMENT-METHOD.md` §1 (finalized in T004), confirm + record the loop↔precedent seam mapping table (candidate↔`generate_fn`, proposer↔`propose_fn`, evaluator↔`score_instructions`, budget↔`BudgetCounter`; capped run → best-so-far) against `optimize.py:257–335` with zero unmatched seams (FR-011, SC-002, US2-AC1/AC2) — DONE (block 05): §1.1 seam-map table added, confirmed by reading the code; total mapping, DSPy step = `propose_fn` seam (no unmatched seam)
- [X] T010 [US2] Run the no-API grep gate over all framework artifacts: `grep -rEi 'OPENAI_API_KEY|litellm|(^|[^a-z])openai' docs/research/repl-engine-separation/` → zero matches on any refinement/verification path; record the result (FR-012, SC-003, US2-AC3) — DONE (block 05): PASS recorded in `reconciliation/NO-API-GATE.md` — spikes literally zero; all doc matches are the rule prohibiting the API, none an API path

**Checkpoint**: the loop discipline is documented + precedent-anchored + API-free.

---

## Phase 5: User Story 3 — Lean 4 tactic-loop sketch + REAL Lean validation spike (Priority: P1)

**Goal**: empirically prove a bounded Claude-over-MCP Lean tactic loop discharges one GLP property against a
**real Lean 4 install** — the methodology's highest-risk formal claim.

**Independent Test**: run the Python harness against a real Lean 4 toolchain (WSL2/container, R10) and confirm
it drives Claude as the model-agnostic tactic driver, enforces the attempt budget, exercises `sorry`-isolation +
escalation, and records proved/`sorry`-isolated + the tactic-attempt count (SC-006).

- [X] T011 [US3] Author `docs/research/repl-engine-separation/reconciliation/LEAN-TACTIC-LOOP.md` (FR-030–034): bounded tactic loop (generate tactic → Lean-LSP-MCP kernel feedback → lemma retrieval/repair → repeat), Claude as model-agnostic driver, budget **start 20 / tuned**, `sorry`-isolation + owner-escalation, Lean 4 primary / Rocq alternative (→ DEF-F-tooling), and the WSL2/container Windows setup path (R10/FR-033) — DONE (block 05): full tactic-loop spec keyed to real versions (Lean 4.30.0 / lean-lsp-mcp 0.26.2 / elan 4.2.3), APOLLO sorry-isolation, DEF-F-tooling AutoRocq→Claude pointer
- [X] T012 [US3] ⛓ **CRITICAL PATH** — Set up a real Lean 4 toolchain in WSL2 (`elan` + `lake`) and wire **Lean-LSP-MCP** to Claude; capture exact versions in `spikes/lean/tool-versions.txt` (FR-033/072, research §1) — elan 4.2.3 / lean 4.30.0 / lake 5.0.0 / lean-lsp-mcp 0.26.2 installed + kernel-verified; MCP *session-registration* deferred to T014 (harness time)
- [X] T013 [P] [US3] Author the concrete GLP property as `spikes/lean/<Property>.lean` — SRSW preservation on a toy clause (fallback: unification soundness on a toy term per research §1, PROP-1) — DONE (block 05): `spikes/lean/SRSWPreservation.lean` — injective-renaming-preserves-SRSW theorem with constant-type relaxation; elaborates in Lean 4.30.0 (exit 0, single deliberate `sorry` = the T014/T015 target)
- [X] T014 [US3] Implement `spikes/lean/harness.py` driving the bounded Claude-over-MCP tactic loop on the property, enforcing the budget and the `sorry`-isolation path; **no external API** — Claude via Agent/MCP, Lean kernel as deterministic local tooling (FR-073) — depends on T012, T013 — DONE (block 06): harness splices a candidate into a working copy, runs real `lean`, classifies proved|still-sorry|tactic-error, accounts budget (start 20) across invocations, enforces sorry-isolation+escalation on exhaustion; driver = Claude (Agent seam), oracle = lean kernel, no API
- [X] T015 [US3] Run the Lean spike against the real toolchain; record `spikes/lean/RESULT.md` (outcome proved|sorry-isolated + tactic-attempt count) and the reproduction `spikes/lean/run.sh`/`run.ps1` (FR-035/071, US3-AC1/AC2/AC3, SC-006/009) — depends on T014 — DONE (block 06): ✅ **PROVED** on real Lean 4.30.0 in **5/20 attempts** (core-Lean only; `maplen` counting lemma → `hcount` → constant-flag witness); driver=Claude/Agent seam, oracle=lean kernel, no API; proof.lean recorded, run.sh reproduces (exit 0)

**Checkpoint**: the Lean tactic-loop claim is backed by a recorded real-tool run, not desk research.

---

## Phase 6: User Story 4 — MLIR/GLP-dialect spec + REAL MLIR round-trip spike (Priority: P2)

**Goal**: empirically prove the GLP/FCP MLIR dialect round-trips a minimal IL fragment under
`decode(encode(p)) ≡ p` against **real MLIR** — de-risks #4 before it commits.

**Independent Test**: run the Python/MLIR harness on a minimal GLP IL fragment and confirm it realizes the four
primitives in a real MLIR dialect, demonstrates `decode(encode(p)) ≡ p`, and records the result — Claude
restricted to structural generation, the deterministic oracle the pass/fail metric (SC-007).

- [X] T016 [US4] Author `docs/research/repl-engine-separation/reconciliation/MLIR-GLP-DIALECT.md` (FR-040–042): the four primitives `HEAD-unify`/`GUARD-test`/`BODY-spawn`/`suspend-reactivate` each with GLP-semantic meaning, progressive-lowering intent, the `decode(encode(p)) ≡ p` primary deterministic criterion (Claude=structural only), and the mis-attributed `2502.06854` citation recorded as open (DEF-B2; candidate LingoDB VLDB 2022) — non-blocking — DONE (block 04): authoritative dialect spec, cross-linked to the recorded spike + REFINEMENT-METHOD §4 slot 2
- [X] T017 [US4] ⛓ **CRITICAL PATH** — Acquire **real MLIR Python bindings** (pip wheel first; WSL2 LLVM build with `-DMLIR_ENABLE_BINDINGS_PYTHON=ON` as fallback); capture versions in `spikes/mlir/tool-versions.txt` (FR-072, research §2) — DONE (block 03; escalation #1 resolved via option A — `mlir-python-bindings 22.0.0.2025112901` real compiled-LLVM `mlir.ir`, makslevental find-links, WSL2; verified in block 04's T019/T020 round-trip PASS). *(Checkbox flipped in block 07 — marathon state had it complete since block 03; the box was left stale.)*
- [X] T018 [P] [US4] Define the minimal GLP IL fragment (one clause touching each of the four primitives once) in `spikes/mlir/` (ILFRAG-1, research §2) — DONE (block 04): `spikes/mlir/ilfrag1.py` — `p(X, Y?) :- ground(X?) | q(Y).` as a Claude-free frozen dataclass (no MLIR/LM/IO)
- [X] T019 [US4] Implement `spikes/mlir/harness.py` (MLIR Python bindings) realizing the four primitives for the fragment and asserting round-trip identity `decode(encode(p)) == p`; Claude restricted to structural generation; deterministic oracle decides pass/fail (FR-043, US4-AC3) — depends on T017, T018 — DONE (block 04): real `mlir.ir` encode/decode over an unregistered `glp` dialect; structural + textual oracle; no LM on the verification path
- [X] T020 [US4] Run the MLIR spike against real MLIR; record `spikes/mlir/RESULT.md` (pass/fail on the fragment) + reproduction `spikes/mlir/run.sh`/`run.ps1` (FR-043/071, US4-AC1, SC-007/009) — depends on T019 — DONE (block 04): ✅ **PASS** against real compiled-LLVM MLIR (`mlir-python-bindings 22.0.0`, WSL2) — `decode(encode(p)) == p` True + textual idempotent True, exit 0; RESULT.md + run.sh/run.ps1 recorded + reproduced

**Checkpoint**: the MLIR-as-substrate claim is backed by a recorded real-tool round-trip.

---

## Phase 7: User Story 5 — Promela/SPIN wire-protocol spike + verification armoury (Priority: P1)

**Goal**: empirically prove a minimal front↔back protocol is deadlock-free + makes progress under **real SPIN**,
and stock the documented armoury for every wire/protocol seed.

**Independent Test**: run real SPIN on the minimal Promela model and confirm deadlock-freedom + progress (no
invalid end states), with the checked properties named and the run reproducible (SC-011).

- [X] T021 [P] [US5] Author `docs/research/repl-engine-separation/reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md` (FR-078/079): a ≥7-tool matrix — SPIN/Promela (default), TLA+/PlusCal, UPPAAL, NuSMV/nuXMV, mCRL2, FDR4, CADP — each with modeling paradigm, verification engine, primary strength, best-for use case; plus the seed-type selection guidance (SPIN default; TLA+ consensus/multi-client; UPPAAL timed; nuXMV symbolic/large; mCRL2/FDR4/CADP process-algebra/asynchronous) and the rule that SPIN protocol validation is mandatory in #2/#5/#6 metric tables with named safety+liveness properties (FR-076/077, SC-012) — DONE (block 05): 7-tool matrix + selection guidance + R14 mandatory-#2/#5/#6 rule + traceability; SPIN-RESULT.md forward-ref made accurate (recorded at T024/block 06)
- [X] T022 [US5] ⛓ **CRITICAL PATH** — Install **real SPIN** (native `spin.exe` + MinGW `gcc` for `pan.c`; WSL2 `spin` fallback); capture the version in `spikes/spin/tool-versions.txt` (FR-072, research §3) — SPIN 6.5.1 (prebuilt linux64) + gcc 13.3.0 in WSL2; full `spin -a`→`gcc pan.c`→`./pan` chain smoke-passed
- [X] T023 [P] [US5] Author `spikes/spin/front_back.pml`: a minimal front↔back request/response handshake (front sends request→awaits response; back awaits request→sends response) with named safety (deadlock-freedom, no unspecified receptions) and a named progress/liveness property; minimal handshake ONLY (full model deferred to #5/#6 — DEF-A3/FR-081) (HANDSHAKE-1) — DONE (block 05): two-proctype depth-1 model with xs/xr ownership + `request_eventually_answered` ltl; independently verified `spin -a`→`gcc pan.c`→`./pan` errors:0 (formal RESULT.md is T024/block 06)
- [X] T024 [US5] Run the SPIN spike against real SPIN (`spin -a front_back.pml` → compile `pan.c` → run with `-a`); record `spikes/spin/RESULT.md` (deadlock-freedom + progress verdict, or counterexample trace) + reproduction `spikes/spin/run.sh`/`run.ps1` (FR-080/071, US5-AC1/AC3, SC-011/009) — depends on T022, T023 — DONE (block 06): ✅ **PASS** real SPIN 6.5.1 — liveness `request_eventually_answered` errors:0 (fairness) + safety/deadlock errors:0 (invalid-end-states enabled); run.sh exit 0; RESULT.md recorded

**Checkpoint**: the sound-wire-protocol claim is backed by a recorded real-SPIN run; armoury documented.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: close the success criteria and the deferral trail.

- [X] T025 [P] Final no-API gate: re-run the `grep` over ALL framework artifacts + every spike harness/example → zero `OPENAI_API_KEY`/`litellm`/`openai` on a refinement/verification path (SC-003) — DONE (block 07): PASS — spikes tree literally zero (incl. harness.py/proof.lean/*.lean/*.pml); 21 doc matches all prohibitive (rule-stating); recorded in `NO-API-GATE.md`
- [X] T026 [P] Done-ness check against `quickstart.md`: six tooling slots (SC-004), five Shapiro criteria mapped (SC-005), template + worked example (SC-001/008), armoury ≥7 tools (SC-012), and three `RESULT.md` files recorded against real tools + reproducible (SC-006/007/009/010/011) — DONE (block 07): all 12 SC verified met; recorded in `DONE-NESS.md` §1 traceability table
- [X] T027 Update `DEFERRALS.md` statuses: confirm DEF-B1/DEF-H1 "partly de-risked" notes (minimal real-Lean / real-MLIR spikes delivered here; full proofs/MLIR-infra still at #4/#11/#12) and DEF-A3 anchored (full protocol model at #5/#6) — DONE (block 07): DEF-B1 (Lean PROVED), DEF-H1 (MLIR PASS), DEF-A3 (SPIN PASS) notes updated to cite delivered RESULT.md evidence; DEF-B2 citation still open (non-blocking, FR-042); rows not deleted
- [X] T028 Final framework review: confirm all five entity→requirement coverages (data-model.md map) are satisfied and SC-010's three highest-risk claims each have empirical evidence from a real-tool run — DONE (block 07): data-model map is actually NINE entities (E1–E9, "five" was a stale task count) — all 9 confirmed satisfied; SC-010's three claims (MLIR PASS, Lean PROVED, SPIN PASS) each have recorded real-tool evidence; recorded in `DONE-NESS.md` §2/§3

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (P1)**: no dependencies — start immediately.
- **Foundational (P2)**: depends on Setup — BLOCKS US1/US2 (they read/finalize `REFINEMENT-METHOD.md`).
- **US1/US2 (P1)**: depend on Foundational. US3/US4/US5 do **not** depend on Foundational (their docs are new
  files; their spikes need only their own tool env) — they can start as soon as Setup (T001) is done.
- **Polish (P8)**: depends on all spikes' `RESULT.md` existing (T015, T020, T024) + all docs authored.

### Critical path (longest chain) — the three real-tool environment setups
The dominant wall-clock cost is **T012 (Lean+MCP), T017 (MLIR bindings), T022 (SPIN)** — each gates its own
spike harness+run. These three are **independent of each other and of all docs**, so front-load and run them
in parallel first (research §5). Per-story chains: T012→T014→T015 · T017→T019→T020 · T022→T023→T024.

### User Story independence
- US1 ⟂ US2 ⟂ US3 ⟂ US4 ⟂ US5 — five independent vertical slices once their gate (Foundational for US1/US2,
  Setup for US3/US4/US5) is met. Each is independently testable via its own Independent Test.
- The only shared file is `REFINEMENT-METHOD.md`, finalized once in Foundational (T004) to avoid US1/US2 conflict.

### Within a story
- Doc/sketch authoring before the harness; tool env (CRITICAL) + subject (property/fragment/model) before the
  harness; harness before the recorded run.

---

## Parallel Opportunities

```text
# Front-load the three real-tool environment setups together (the critical path):
T012  Set up Lean 4 + Lean-LSP-MCP (WSL2)        → spikes/lean/tool-versions.txt
T017  Acquire MLIR Python bindings (wheel/WSL2)  → spikes/mlir/tool-versions.txt
T022  Install real SPIN (native/WSL2)            → spikes/spin/tool-versions.txt

# Author the independent doc artifacts in parallel (different files):
T007 [US1] INTERACTIVE-SPEC-STEP.md
T011 [US3] LEAN-TACTIC-LOOP.md
T016 [US4] MLIR-GLP-DIALECT.md
T021 [US5] PROTOCOL-VERIFICATION-ARMOURY.md

# Author the spike subjects in parallel with their tool setups:
T013 [US3] <Property>.lean
T018 [US4] minimal IL fragment
T023 [US5] front_back.pml
```

---

## Implementation Strategy

### MVP First
1. Setup (T001–T002) → Foundational (T003–T005) → **US1 (T006–T008)** = the load-bearing deliverable: a seed
   can state "done" from the shared template. **STOP and VALIDATE** (SC-001/008). This alone is a viable MVP
   of the framework (spec US1 "Why priority").

### Incremental Delivery (then the empirical de-risking)
2. US2 (T009–T010) — loop discipline + no-API gate (cheap; mostly verification of the finalized methodology).
3. **US3 (Lean), US5 (SPIN)** — the two P1 real-tool spikes; front-load T012/T022 env setup. These close the
   methodology's two most load-bearing claims (Lean proving, sound wire protocol) and are required before the
   wire/language seeds (#2/#4/#5/#6) enter the pipeline.
4. **US4 (MLIR, P2)** — the round-trip spike; de-risks #4. Lower priority but still real-tool.
5. Polish (T025–T028) — SC closure + deferral-trail update.

### Notes
- ⚠ **Real tools required** — a docs-only completion does NOT satisfy R13/R14 (FR-070). The three `RESULT.md`
  files are the acceptance evidence.
- ⚠ **No external LM API** anywhere (FR-012/073) — grep-gated (T010, T025).
- Spikes are **minimal feasibility spikes** (FR-074): one GLP property, one IL fragment, one handshake — the
  full proofs/MLIR-infra/protocol-model stay at #4/#11/#12 and #5/#6.
- This feature touches no GLP runtime / `.glp` source / wire code — the REPL suite is not run here.
