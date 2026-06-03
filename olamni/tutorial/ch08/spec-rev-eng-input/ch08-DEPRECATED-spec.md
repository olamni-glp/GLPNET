# Feature Specification: Chapter 8 — The Grassroots Social Graph

**Feature Branch**: `009-tutorial-ch08`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch08/ch08-sources.md` + `GLP_ART.pdf` book pp 69–79 (PDF pp 81–91).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation

## Clarifications
- Use cases per charter §1: cold-call befriending, friend-mediated introduction, four-agent independent befriending. Each becomes a project subdir with cumulative agent code (charter §2.4 — later use cases include earlier-section clauses they need).

## Source Programs (verified against PDF)
See `ch08-sources.md` code-block index. Highlights:
- §8.2 `agent/3` initializer.
- §8.3 cold-call protocol: 5 `social_graph/3` clauses + `bind_response/6` (2 cl.) + `handle_accept/6` + `handle_response/6` (2 cl.) + `add_friend/4` + `response_stream/4` + `inject/4`.
- §8.4 friend-mediated intro: 4 `social_graph/3` clauses (`introduce`, `intro`, `accept_intro`, `reject_intro`) + catch-all.
- §8.6 utilities: `lookup/3`, `lookup_send/4`, `tag_stream/3`.
- §8.7 plays: `network2/2`, `alice_actor/2`, `bob_actor/2`, `play_alice_bob/0`, `network4/4`, `play_4agents/0`, `bob_wait_intro/2`, `play_introduction/0`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Cold-call befriending (Alice → Bob) (Priority: P1)

**Independent Test**: load project `olamni/tutorial/ch08/cold-call-befriending/` + companion Flutter app `main_olamni_ch08_cold_call.dart`; run `play_alice_bob` (or its boot wrapper). Expected end-state: Alice's friends list contains Bob, Bob's contains Alice, agents suspended on empty friend channels (per §8.7 Execution Trace).

**Acceptance Scenarios**:
1. Project shape `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` per charter §2.2.
2. `agent.glp` contains §8.2 `agent/3` + all §8.3 cold-call clauses + `bind_response`, `handle_accept`, `handle_response`, `add_friend`, `response_stream`, `inject`, plus §8.6 utilities.
3. `network.glp` contains §8.7 `network2/2` (2-agent switch).
4. `actors.glp` contains §8.7 `alice_actor/2` and `bob_actor/2`.
5. `boot.glp` wires it as `play_alice_bob`.
6. REPL trace matches §8.7 8-step execution trace.

### User Story 2 — Friend-mediated introduction (Alice introduces Bob to Carol) (Priority: P1)

**Independent Test**: load `olamni/tutorial/ch08/friend-mediated-introduction/` + `main_olamni_ch08_friend_mediated.dart`. Run `play_introduction` (3-phase: Alice cold-calls Bob, Alice cold-calls Carol, Alice introduces Bob to Carol). Expected end-state: all three are mutual friends.

**Acceptance Scenarios**:
1. `agent.glp` cumulative: §8.2 + §8.3 + §8.4 + utilities (per charter §2.4).
2. `network.glp` contains 3-agent network switch.
3. `actors.glp` contains the §8.7 2-phase `bob_actor` + `bob_wait_intro` + cold-call-only `carol_actor` + `alice_actor` driving 3 phases.
4. `boot.glp` wires the 3-phase play.

### User Story 3 — Four-agent independent befriending (Priority: P2)

**Independent Test**: load `olamni/tutorial/ch08/four-agent-graph/` + `main_olamni_ch08_four_agent.dart`. Run `play_4agents`. Expected end-state: graph reflects independent cold-call befriending operations across 4 agents.

**Acceptance Scenarios**:
1. `network.glp` contains §8.7 `network4/4` (12 clauses for n(n-1) = 12 pairs).
2. `actors.glp` contains 4 cold-call actors.
3. `boot.glp` wires `play_4agents`.

### User Story 4 — Useful techniques shared across use cases (Priority: P3)

`ch08/useful-techniques.glp` collects §8.6 `lookup/3`, `lookup_send/4`, `tag_stream/3` if these are NOT already provided by a higher-level shared lib.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Three project subdirs (cold-call, friend-mediated, four-agent) under `olamni/tutorial/ch08/`, each `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}`.
- **FR-002** Each project paired with a `glp_multiagent/lib/main_olamni_ch08_<use-case>.dart` cloned from `main_cssg_mad_modules.dart` per charter §2.2.
- **FR-003** Cumulative agent code per charter §2.4: friend-mediated includes cold-call clauses; four-agent uses cold-call only (no intro).
- **FR-004** Every clause carries a `%%` comment paraphrasing the matching prose paragraph (charter §1.5).
- **FR-005** Each project loads in the REPL with `→ succeeds` or `→ suspended` (suspended is normal for plays whose channels remain open at end).
- **FR-006** Each Flutter app builds and launches; trace log shows the expected message flow.
- **FR-007** §8.5 Channel Notation is prose; not encoded.
- **FR-008** §8.8 Exercises out of scope per charter.
- **FR-009** §8.7 catch-all clause (drops unknown messages with `otherwise`) included in every agent project's `agent.glp`.
- **FR-010** REPL-test traces saved on disk per charter §Testing.
