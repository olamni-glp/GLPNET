# Feature Specification: Chapter 9 — Social Networks

**Feature Branch**: `010-tutorial-ch09`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch09/ch09-sources.md` + `GLP_ART.pdf` book pp 81–96 (PDF pp 93–108) + appendix Social Networks Code (book pp 153–157).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation

## Clarifications
- Five use cases per chapter sections + companion appendix: Direct Messaging (§9.1 / app p 153), Feeds and Followers (§9.2 / app p 154), Manager-Based Group (§9.3 / app p 155), Interlaced-Stream Group (§9.3 / app p 156), Child-Safe Social Networking (§9.4 / app p 157). Appendix code is the canonical play form; cross-reference both at extraction time.

## Source Programs (verified against PDF)
See `ch09-sources.md` for the full code-block index. Major Programs per use case:
- §9.1: `dm_send/3`, `dm_receive/3`, `bind_resp/2`, `alice/3`, `alice_continue/3`, `bob/3`, `bob_continue/2`, `verify/2`, `report/2`, `play_dm/0`.
- §9.2: `make_feed/1`, `extend_feed/3`, `broadcast/3` (ground-guard), `get_feed/3`, `serve_feed/3`, play actors + `play_feed/0` + `play_continue/1`.
- §9.3 manager-based: `manager/3`, `manage/5` (4 cl.), `prepend_all/3`, `play_group_manager/0` + helpers.
- §9.3 interlaced-stream: `interlace/3`, `collect_tips/3`, `member/4`, `tag_messages/3`, `play_group_interlaced/0` + verifiers.
- §9.4 CSSN: `bind_resp/2`, `parent_smith/2`, `smith_wait/2`, `smith_done/2`, `parent_jones/2`, `jones_done/3`, `play_child_safe/0`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Direct messaging on a friend channel (Priority: P1)

**Independent Test**: load `olamni/tutorial/ch09/direct-messaging/` project + Flutter app; run `play_dm`. Expected: Alice sends `hello_bob`, Bob replies `hello_alice`, `verify` confirms exchange (per §9.1 Execution Trace).

### User Story 2 — Feeds and followers with broadcast (Priority: P1)

**Independent Test**: load `olamni/tutorial/ch09/feeds-and-followers/` + Flutter app; run `play_feed`. Expected: Bob and Carol both receive `feed([second_post, hello_world])` (newest first) per §9.2 Execution Trace.

### User Story 3 — Manager-based group messaging (Priority: P2)

**Independent Test**: load `olamni/tutorial/ch09/manager-based-group/` + Flutter app; run `play_group_manager`. Expected: Bob and Carol receive identical message sequences `[msg(carol, hello_from_carol), msg(bob, hello_from_bob)]` per §9.3 Execution Trace — total ordering via manager.

### User Story 4 — Interlaced-stream group messaging (Priority: P2)

**Independent Test**: load `olamni/tutorial/ch09/interlaced-stream-group/` + Flutter app; run `play_group_interlaced`. Expected: each member's stream contains blocks with cross-references creating partial ordering — Alice's block has empty tips (first), Bob's references Alice, Carol's references Bob (and transitively Alice). No bottleneck.

### User Story 5 — Child-Safe Social Networking approval protocol (Priority: P1)

**Independent Test**: load `olamni/tutorial/ch09/child-safe-networking/` + Flutter app; run `play_child_safe`. Expected: Parent-Smith sends `approval_request(smith, child_smith, Resp)`; Parent-Jones binds `Resp = approved`; Parent-Smith's `known(Resp?)` guard succeeds; verification confirms both sides agree (per §9.4 Execution Trace).

### User Story 6 — Useful techniques shared across CSSN use cases (Priority: P3)

`ch09/useful-techniques.glp` collects `bind_resp`, `get_feed`, `prepend_all`, `tag_messages` if shared across use cases.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Five project subdirs under `olamni/tutorial/ch09/`, each `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` per charter §2.2 (or simplified shape if a use case is single-process per the appendix code).
- **FR-002** Each paired with `glp_multiagent/lib/main_olamni_ch09_<use-case>.dart`.
- **FR-003** Code is taken VERBATIM from §9.1–9.4 AND/OR the matching appendix listing (book pp 153–157). Where the chapter and appendix differ, the appendix is canonical for the play orchestration; the chapter is canonical for the protocol clauses.
- **FR-004** §9.3 has TWO use cases (manager-based AND interlaced-stream); they appear as separate project subdirs.
- **FR-005** Every clause carries a `%%` paraphrase comment per charter §1.5.
- **FR-006** Each project must satisfy load + run + correct end-state per the Execution Traces in the chapter.
- **FR-007** Comparison table p 92 (manager vs interlaced) is referenced in each project's README/header but not encoded.
- **FR-008** §9.5 Exercises out of scope per charter.
- **FR-009** REPL-test traces saved on disk.
