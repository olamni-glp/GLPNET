# Feature Specification: Chapter 10 — Interlaced Streams

**Feature Branch**: `011-tutorial-ch10`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch10/ch10-sources.md` + `GLP_ART.pdf` book pp 97–100 (PDF pp 109–112) + appendix Interlaced Streams Group Play (book p 156).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation (single use case per charter §1)

## Clarifications
- Ch 10's `interlace/3` and `collect_tips/3` are exactly the same Programs used in Ch 9 §9.3 for interlaced-stream groups. Ch 10 elevates them to their own chapter, framing them as the underlying data structure (blocklace) used by Chs 11 (cryptocurrencies) and 12 (consensus).
- Decision: this chapter's tutorial project is the **standalone, distributed-ledger flavour** of interlaced streams (per §10.4's 3-agent `p`/`q`/`r` invocation pattern), distinct from the Ch 9 §9.3 group-messaging variant.

## Source Programs (verified against PDF)
- §10.2 `streams/2` (entry point), `interlace/3` (block production), `collect_tips/3` (tip collection).
- §10.4 3-agent `p`/`q`/`r` invocation pattern.
- Appendix Interlaced Streams Group Play (book p 156) — companion play code; cross-reference at extraction time.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — 3-agent blocklace formation (Priority: P1)

Three agents `p`, `q`, `r` each produce blocks while observing the others' streams. Tip references cross-link the streams into a DAG.

**Independent Test**: load `olamni/tutorial/ch10/interlaced-streams-group/` project + `main_olamni_ch10_interlaced_streams.dart`. Run the 3-agent play. Expected: each agent's stream contains `block(Payload, Tips)` blocks; later agents' Tips reference earlier agents' tips creating partial ordering (per §10.3 trace and §10.4 deployment shape).

**Acceptance Scenarios**:
1. Project shape `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` per charter §2.2.
2. `agent.glp` contains §10.2 `streams/2` + `interlace/3` + `collect_tips/3` verbatim with `%%` paraphrase comments.
3. `actors.glp` (or `boot.glp`) wires the 3-agent invocation per §10.4 shape.
4. REPL trace shows tip-reference DAG matching §10.3 (Single Agent and Multiple Agents w/ Incomplete Streams).

### User Story 2 — Single-agent and incomplete-stream behaviour (Priority: P2)

Validates the suspension behaviour from §10.3 trace 2: with incomplete streams from agents P and Q, the computation suspends until more blocks are produced.

**Independent Test**: in REPL, run `streams(S, []).` (no other streams) — expects the single-agent base case to produce blocks with empty tips. Then run with incomplete `Ptail`/`Qtail` — expects suspension on the second block per §10.3.

**Acceptance Scenarios**:
1. `→ succeeds` for single-agent variant.
2. `→ suspended` for incomplete-stream variant — this is the EXPECTED outcome and must be documented as such.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Single project subdir `olamni/tutorial/ch10/interlaced-streams-group/`.
- **FR-002** Paired Flutter app `glp_multiagent/lib/main_olamni_ch10_interlaced_streams.dart`.
- **FR-003** `agent.glp` contains §10.2 Programs verbatim with `%%` paraphrase comments.
- **FR-004** Test scenarios cover both successful completion (single-agent) and expected suspension (incomplete streams) — the latter MUST be reported as the expected end-state, not as a failure.
- **FR-005** §10.5 (Security Properties) and §10.6 (Applications) are prose; referenced in headers.
- **FR-006** §10.7 Exercises out of scope per charter.
- **FR-007** Cross-reference Ch 9 §9.3 interlaced-stream-group project — note in headers that Ch 10 generalises that protocol to its own use case.
- **FR-008** REPL-test traces saved on disk per charter §Testing.
