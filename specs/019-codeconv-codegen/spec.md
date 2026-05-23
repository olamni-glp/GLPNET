# Feature Specification: codeconv-codegen — GEPA/DSPy-optimized Dart→C#/.NET code generation

**Feature Branch**: `019-codeconv-codegen`
**Created**: 2026-05-23
**Status**: Draft
**Input**: User description: "Final codeconv pipeline stage — generate compilable C#/.NET from the 130 ratified conversion plans + convspecs, filling the scaffolded out/csharp/ tree; codegen quality actively improved across the codebase using GEPA (reflective Pareto prompt optimization) on DSPy, driven by testing feedback + human review, processed in dependency-ordered batches. (C) hybrid architecture + composite metric as the starting proposal."

## Clarifications

### Session 2026-05-23

- Q: Architecture for the GEPA/DSPy optimization vs the deterministic production path? → A: (C) hybrid — a separate offline DSPy/GEPA process evolves the codegen instructions; production codegen is harness-driven sub-agents consuming the optimized instructions; the durable codegen step is a deterministic ingest + build/test gate (replay-safe). No in-package model client on the production/durable path.
- Q: Feedback-metric composition, human-review cadence, and batch-promotion gate? → A: Build pass is a hard gate (non-compiling = score floor). Compiling-candidate score = 0.6·ported-test-pass-rate + 0.4·normalized-human-review (1–5→0–1); before tests are in scope the score = normalized human-review alone. Human review is batch-sampled at max(3 files, 20% of the batch), scored 1–5 + free-text (free-text feeds the optimizer reflectively). Promotion gate = 100% build pass AND human median ≥ 4/5.
- Q: DSPy LM backend and GEPA budget cap? → A: The offline optimizer uses OpenAI via litellm (installed; no anthropic SDK), model configurable (default the strongest available OpenAI reasoning model); production codegen runs on the Claude Code harness, so OpenAI sees source only in the offline optimizer (accepted IP tradeoff). GEPA has a hard, configurable budget/rollout cap (conservative default), surfaced as a flag.
- Q: Are test files in scope, and when do ported-test results enter the metric? → A: Staged. Increment 1 (US1, MVP) = production `lib/` only; metric = build-pass gate + human review. Increment 2 (US3) = convert test files and add ported-test pass-rate to the metric. Tests-pass is not a day-one metric component.
- Q: Codegen state/schema and how the stage fits the durable builder? → A: New table `dart_codegen` (two-phase: codegen_started_at/completed_at, sha256_at_codegen_start, target_cs_path, build_status, test_pass_rate, human_review_score, open_escalation_count, codegen_run_id), migration `0007` (down_revision `0006`), `codeconv` schema only, CREATE TABLE IF NOT EXISTS. Tombstone keys appended after the plan keys (codegen_started_at/completed_at/target_cs_path/build_status/codegen_open_escalation_count). Escalation report at `.codeconv/conversion-code/_escalations-report.md`. The durable builder gains a `codegen` stage after `plan`; its DBOS step is the deterministic ingest + build/test gate (returns needs_agent_work if the `.cs` artifact is absent); GEPA optimization stays outside the durable pipeline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate compilable C# for the production tree, batched in dependency order (Priority: P1)

An engineer runs the codegen stage over the in-scope production source. For each file, the stage produces a C#/.NET source file in the scaffolded `out/csharp/` tree, derived from that file's ratified conversion plan + convspec + the C# interfaces of its already-generated dependencies. Files are processed in dependency (topological/SCC) order so each batch compiles against the C# already produced for its dependencies. A file counts as generated only when its produced code passes a build gate; anything that cannot be generated faithfully is escalated, never guessed.

**Why this priority**: This is the load-bearing value of the whole 012–018 pipeline — it is the first stage that emits actual C#. Without it the conversion never produces runnable code.

**Independent Test**: Run codegen over a small dependency-closed subset (e.g. a leaf file plus its dependents); verify each produced `out/csharp/...` file builds successfully and that re-running produces no spurious changes.

**Acceptance Scenarios**:

1. **Given** a ratified plan + convspec for a leaf file with all dependencies already generated, **When** codegen runs for that file, **Then** a C# source file is written to its scaffolded target path and the produced code passes the build gate.
2. **Given** a file whose dependencies are not yet generated, **When** the batch frontier is computed, **Then** that file is not offered for generation until its dependencies are generated (dependency-before invariant).
3. **Given** a construct whose faithful C# cannot be established from plan + convspec + idiom KB, **When** codegen processes it, **Then** the file records a structured escalation and is marked conversion-blocked rather than emitting a guessed translation.

---

### User Story 2 - Actively improve codegen quality across the run via GEPA/DSPy optimization (Priority: P2)

Rather than one-shot generation, the codegen instructions are actively optimized: an optimization process evolves the codegen prompt against the measured feedback signal (build success, and — once available — ported-test results and human-review scores), so that later batches generate higher-quality C# than earlier ones. The optimized instructions are then used by the production codegen path.

**Why this priority**: Turns codegen from a fixed prompt into a system that measurably improves over the conversion, raising overall yield and reducing escalations/rework — the explicit "use GEPA/dspy actively" requirement.

**Independent Test**: Run the optimizer on a held-out set of files with a fixed feedback metric; verify the post-optimization codegen instructions score measurably higher on the metric than the baseline instructions on the same held-out set.

**Acceptance Scenarios**:

1. **Given** a baseline codegen instruction set and a feedback metric, **When** the optimizer runs within its budget cap, **Then** it produces a candidate instruction set with a metric score ≥ the baseline on the evaluation set.
2. **Given** the optimizer hits its configured budget/rollout cap, **When** the cap is reached, **Then** the run stops and returns the best instruction set found so far (no runaway cost).
3. **Given** an optimized instruction set, **When** the production codegen path runs, **Then** it uses the optimized instructions and the production path itself performs no open-ended/non-deterministic optimization.

---

### User Story 3 - Human review gates batches; testing feedback closes the loop (Priority: P2)

For each batch, automated feedback (build, and ported tests where available) is collected, and a sampled subset of the batch is presented for human review. A batch is promoted to "converted" only when its automated gate passes and the human-review gate is satisfied; reviewer feedback feeds back into the optimization signal.

**Why this priority**: The user explicitly requires testing feedback **and** human review with batched promotion; this is the quality control that makes generated code trustworthy.

**Independent Test**: Run a batch to completion; verify the system surfaces the automated results + a review sample, blocks promotion until the human gate is recorded, and that a failing human gate prevents promotion.

**Acceptance Scenarios**:

1. **Given** a completed batch with passing builds, **When** the human-review sample is reviewed and meets the gate threshold, **Then** the batch is promoted to converted.
2. **Given** a completed batch, **When** the human-review gate is not met, **Then** the batch is not promoted and is flagged for re-generation or escalation.
3. **Given** a converted batch, **When** the next batch is computed, **Then** it builds against the just-converted batch's C#.

---

### User Story 4 - Resumable, idempotent, escalation-aware state (Priority: P3)

Codegen tracks per-file state so a run can be interrupted and resumed without regenerating already-converted files, re-running produces no spurious diffs on unchanged inputs, source drift is detected, and open escalations are aggregated into an engineer-facing report that blocks only the affected files.

**Why this priority**: Matches the durability/idempotence/escalation discipline of every prior codeconv stage; needed for a 130-file run to be operable and trustworthy, but the core value (US1–US3) is demonstrable without it.

**Independent Test**: Interrupt a run mid-batch and resume; verify completed files are skipped, a re-run on unchanged inputs yields no diff (beyond timestamps), and a file with an open escalation is reported and excluded from "converted" counts.

**Acceptance Scenarios**:

1. **Given** an interrupted run, **When** codegen is re-invoked, **Then** already-converted files are skipped and only unfinished files proceed.
2. **Given** a source file whose content changed after its code was generated, **When** state is inspected, **Then** the file is reported as stale and is only regenerated under an explicit re-generate action.
3. **Given** one or more open escalations, **When** the escalation report is produced, **Then** it lists exactly the blocked files and the count matches the per-file state.

### Edge Cases

- A produced file fails the build gate (compile error): the failure is recorded as feedback; the file is retried/optimized or escalated — never silently accepted.
- The optimizer fails to improve over baseline, or times out / hits its budget cap: the best-so-far instructions are used and the situation is reported (no runaway, no naive fallback to a guessed translation).
- A circular-import group (SCC): all members are generated and gated as one coordinated batch; no member is promoted in isolation.
- A dependency's generated C# is missing or itself escalated: dependent files are held (dependency-before invariant), not generated against an absent interface.
- Source drift between plan-time and codegen-time: detected and reported stale; not silently regenerated.
- LM/optimizer unavailable, times out, or returns nothing usable: escalate with the reason; do not substitute a guess.
- Human-review sample is incomplete or the gate is ambiguous: the batch stays unpromoted until the gate is resolved.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST generate a C#/.NET source file for each in-scope source file, derived from that file's ratified conversion plan + convspec + the conversion-idiom KB, written to the file's scaffolded target path in `out/csharp/`.
- **FR-002**: The system MUST process files in the existing dependency (topological/SCC) order, generating dependencies before dependents, and treating each SCC as one coordinated batch.
- **FR-003**: The system MUST apply a build gate: produced code that does not compile MUST NOT count as generated; the build outcome is captured as feedback.
- **FR-004**: The system MUST actively optimize the codegen instructions against a measurable feedback metric so that optimized instructions score no worse than baseline on an evaluation set, and MUST stop at a configurable budget/rollout cap returning the best result found.
- **FR-005**: The production code-generation path MUST be deterministic and replay-safe (no open-ended optimization or non-reproducible model calls inside the durable pipeline); optimization MUST be confined to a separate offline process whose only output into production is the optimized instruction set ((C) hybrid, confirmed). The durable codegen step MUST be a deterministic ingest of the produced `.cs` plus its recorded build/test result, returning a typed "needs generation" signal when the artifact is absent (never raising); the offline optimizer's LM backend MUST NOT be invoked on the production/durable path.
- **FR-006**: The system MUST collect testing feedback and human review and gate batch promotion on both. Build pass is a hard gate (non-compiling output scores at the floor). The compiling-candidate score MUST be 0.6·ported-test-pass-rate + 0.4·normalized-human-review (1–5 mapped to 0–1); before test files are in scope the score MUST be the normalized human-review alone. Human review MUST be batch-sampled at max(3 files, 20% of the batch), recorded as a 1–5 score plus free-text. A batch MUST be promoted to "converted" only when 100% of its files build AND the human-review median is ≥ 4/5.
- **FR-007**: The system MUST escalate (with a structured, engineer-facing record) any construct whose faithful translation cannot be established from plan + convspec + idiom KB, and MUST NOT emit a guessed translation; open escalations block only the affected file.
- **FR-008**: The system MUST track per-file codegen state (started/completed, source hash at generation time, target path, build status, review/test outcomes, open-escalation count) so runs are resumable and idempotent, and MUST detect source drift (stale) and only regenerate under an explicit re-generate action.
- **FR-009**: The system MUST aggregate open escalations into a single engineer-facing report and keep its counts consistent with per-file state.
- **FR-010**: The system MUST reuse upstream artifacts read-only (dependency graph/order/SCC/status, plans, convspecs, idiom KB) and MUST NOT recompute or mutate them.
- **FR-011**: Generated code MUST honor recorded project conventions and the conversion-idiom KB (e.g., `*Error` type names retained verbatim; the `getX→LookupX` collision idiom), so the produced C# is consistent with the ratified plans/convspecs.
- **FR-012**: The system MUST convert files in two increments: Increment 1 (US1, MVP) generates production `lib/` code with the metric = build-pass gate + human review; Increment 2 (US3) converts the test files and adds ported-test pass-rate to the metric. Ported-test results are NOT part of the day-one metric.
- **FR-013**: The system MUST persist codegen state in a new `dart_codegen` table (two-phase started/completed, source hash at generation, target path, build status, test pass-rate, human-review score, open-escalation count, run id) added by a new migration `0007` (chained after `0006`) in the `codeconv` schema only, append-only; MUST append the corresponding codegen keys to each tombstone after the plan keys (round-trip consistent with prior stages); and MUST integrate as a `codegen` stage after `plan` in the durable builder, with GEPA optimization kept outside the durable pipeline.

### Key Entities *(include if feature involves data)*

- **Generated code unit**: the produced C#/.NET source for one source file, at its scaffolded target path; the deliverable of the stage.
- **Codegen state record**: per-file two-phase state (started/completed, source hash at generation, target path, build status, test result, human-review score, open-escalation count, run id).
- **Optimized instruction set**: the codegen prompt/instructions evolved by the offline optimizer; the only optimizer output consumed by the production path.
- **Batch**: a dependency-ordered group of files (an SCC is one indivisible batch) processed and gated together.
- **Feedback record**: build result + (where available) ported-test result + human-review score for a file/batch, used both as the promotion gate and the optimization signal.
- **Codegen escalation**: a structured record of an undecidable construct that blocks the affected file's conversion.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every in-scope source file either yields a build-passing generated C# file or a recorded escalation — zero files in an indeterminate state.
- **SC-002**: ≥ 95% of in-scope production files yield build-passing generated code without manual code editing (escalations excluded).
- **SC-003**: Optimized codegen instructions score measurably higher on the feedback metric than the baseline instructions on a held-out evaluation set (active improvement demonstrated).
- **SC-004**: No batch is promoted to "converted" without both its automated gate passing and its human-review gate satisfied.
- **SC-005**: An interrupted run resumes without regenerating completed files, and a re-run on unchanged inputs produces no output diff beyond timestamps (idempotent + resumable).
- **SC-006**: Optimization never exceeds its configured budget/rollout cap; a capped run still returns a usable instruction set.
- **SC-007**: Open escalations are surfaced in one report whose counts match per-file state, and each blocks only its own file.

## Assumptions

- **Architecture (confirmed — Clarifications 2026-05-23)**: (C) hybrid — a separate offline optimization process evolves the codegen instructions; the production code-generation path is harness-driven and deterministic/replay-safe, consuming only the optimized instructions. This preserves the cross-pipeline rule that the durable pipeline stays deterministic and replay-safe with no in-package model client on the production path.
- Inputs are ready and ratified: 130 conversion plans, 130 convspecs, the conversion-idiom KB, all with zero open escalations; the `out/csharp/{lib,test}` scaffold tree exists (from the scaffold stage).
- Upstream stages (discover/depgraph/init/scaffold/mirror/convspec/planagents) are consumed read-only and are not re-run by this feature.
- The target framework and build/test toolchain used for the feedback signal are already available in the environment.
- The optimizer's model backend and any external transmission of source carry the previously-accepted IP-exposure tradeoff; the production path does not introduce new external transmission beyond the established harness model.
- Work is confined to the conversion toolchain, its skills, the generated-code tree, and the conversion-artifact tree; no change to the source runtime or to non-target languages.
- This feature governs codegen; the originating pipeline specs (012–018) remain as lineage and are consumed, not modified.

## Out of Scope

- Re-running or modifying the upstream discover/depgraph/init/scaffold/mirror/convspec/planagents stages.
- Resolving existing escalations (there are none open).
- Hand-writing the C# output (the point is generated + actively-optimized output).
- Converting or running anything outside the conversion target tree.
