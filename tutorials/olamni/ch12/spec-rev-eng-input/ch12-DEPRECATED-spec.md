# Feature Specification: Chapter 12 — Constitutional Consensus

**Feature Branch**: `013-tutorial-ch12`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch12/ch12-sources.md` + `GLP_ART.pdf` book pp 115–127 (PDF pp 127–139).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation (single use case per charter §1)

## Clarifications
- Single project subdir per charter §1: full Constitutional Consensus protocol with 3-participant constitution per §12.8.
- Distinguish from Ch 11 (cryptocurrencies) and Ch 10 (interlaced streams) by the 3-round wave structure (Candidates / Endorsements / Ratifications) and dual-mode operation (low / high throughput).

## Source Programs (verified against PDF)
- §12.5: `wait_for_leader/2` (timeout primitive).
- §12.6: `compute_tau/3` (incremental τ).
- §12.7: agent-state `state(Blocklace, Mode, CurrentRound, Finalized, Pending)`; `agent/4` event loop; `handle_event/4`; `maybe_issue/7` (5 clauses for low/issue, low/endorse, low/ratify, high/dispatch, default); `leader/3`; `is_finalized/3`; `is_majority/2`; `update_finalized/3`; `find_newly_finalized/3`.
- §12.8: `genesis([alice, bob, carol], 0.5, 1000)` setup; low-throughput trace; high-throughput conflict trace.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Three-participant low-throughput finalization (Priority: P1)

Alice/Bob/Carol; Alice has transaction `tx_a` to propose; network is quiescent.

**Independent Test**: load `olamni/tutorial/ch12/constitutional-consensus/` project + `main_olamni_ch12_constitutional_consensus.dart`. Initialize with `genesis([alice, bob, carol], 0.5, 1000)`. Alice proposes `tx_a` in round 1; Bob, Carol, Alice all endorse in round 2; all ratify in round 3. Expected: `Finalized = [tx_a]` (per §12.8 Wave 1 trace).

**Acceptance Scenarios**:
1. Project shape `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` per charter §2.2.
2. `agent.glp` contains §12.7 `agent/4`, `handle_event/4`, all 5 `maybe_issue/7` clauses, `leader/3`, `is_finalized/3`, `is_majority/2`, `update_finalized/3`, `find_newly_finalized/3` verbatim.
3. `self.glp` contains the block type `block(Round, Payload, Pointers)` and constitution genesis type `genesis(P, Sigma, Delta)` per §12.3.
4. `actors.glp` drives the wave-1 scenario via the 3 agents.
5. `boot.glp` wires the play with the §12.8 `genesis` invocation.
6. REPL trace produces `Finalized = [tx_a]`.

### User Story 2 — High-throughput conflict resolution with formal leader (Priority: P1)

After Wave 1, Alice and Bob simultaneously propose `tx_a2` and `tx_b`. Carol detects the conflict; all switch to high-throughput mode. Wave 2's formal leader (round-robin index `WaveNum mod 3`) is selected; per §12.8, all endorse Alice's block (she is leader for wave 2).

**Independent Test**: same project, drive Wave 2 of §12.8. Expected: `Finalized = [tx_a, tx_a2]`. Then Wave 3 with Bob alone proposing `tx_b` — `Finalized = [tx_a, tx_a2, tx_b]`.

**Acceptance Scenarios**:
1. `actors.glp` and `boot.glp` extended for the 3-wave scenario.
2. Mode-transition (low → high → low) observable in the agent's `state.Mode` field.
3. Final `Finalized` stream matches §12.8 trace.

### User Story 3 — Useful techniques (Priority: P3)

`ch12/useful-techniques.glp` collects `is_majority/2`, `wait_for_leader/2` if shared.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Single project subdir `olamni/tutorial/ch12/constitutional-consensus/` with the full protocol.
- **FR-002** Paired Flutter app `glp_multiagent/lib/main_olamni_ch12_constitutional_consensus.dart`.
- **FR-003** `self.glp` contains: block type `block(Round, Payload, Pointers)`, constitution-genesis type, agent-state type `state(Blocklace, Mode, CurrentRound, Finalized, Pending)`.
- **FR-004** `agent.glp` contains §12.7 Programs verbatim + §12.5 `wait_for_leader/2` + §12.6 `compute_tau/3` with `%%` paraphrase comments per charter §1.5.
- **FR-005** Play exercises both low-throughput (§12.8 Wave 1) and high-throughput-with-conflict (§12.8 Waves 2–3) scenarios.
- **FR-006** Mode transitions observable in REPL trace.
- **FR-007** Final `Finalized` stream matches §12.8 trace `[tx_a, tx_a2, tx_b]` for the 3-wave scenario.
- **FR-008** §12.1 (Smart Contract vs Digital Social Contract table), §12.2 (FLP / eventual synchrony / constitution), §12.4 (wave structure), §12.9 (correctness arguments) are prose; referenced in headers.
- **FR-009** Attestation simplification (§12.1) noted in headers — the implementation assumes attested agents (no Byzantine simulation), which is why σ = 1/2 (simple majority) suffices instead of σ > 2/3.
- **FR-010** §12.10 Exercises out of scope per charter.
- **FR-011** REPL-test traces saved on disk per charter §Testing.
