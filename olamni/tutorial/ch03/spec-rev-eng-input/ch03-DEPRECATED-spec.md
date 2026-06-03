# Feature Specification: Chapter 3 — GLP Core

**Feature Branch**: `004-tutorial-ch03`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch03/ch03-sources.md` + `GLP_ART.pdf` book pp 15–24 (PDF pp 27–36).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: cohesive-synthesis

## Clarifications
- §3.2 introduces **defined guards** for channels (`send`/`receive`/`new_channel`/`relay`/`make_pair`/`bind_response`). These are foundational for chs 8–9. Decision: each forms a standalone substantial Program (per charter §1: "section-driven, one file per substantial Program"); a chapter `useful-techniques.glp` is OPTIONAL and only collects the lookup-with-negation idiom from §3.2.

## Source Programs (verified against PDF)
- **Program 3.1: GLP Fair Stream Merger** — p 15, 3 clauses (`merge/3`).
- §3.2 inline blocks: `lookup/3` (guard negation, p 22), `channel/1` defined-guard type test + `process/2` (p 22), channel ops `send/3`, `receive/3`, `new_channel/2` (p 23), `relay/3` (p 23), `make_pair/2` (p 23), `bind_response/3` (p 23).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Run Program 3.1 GLP fair-merge (Priority: P1)

Learner runs the canonical GLP merge from p 15 with reader/writer annotations and SRSW comments.

**Independent Test**: load `ch03/ch-03-ex-01-glp-fair-stream-merger.glp`; run `merge([1,2,3],[a,b],Xs).`, expect fair interleaving and successful completion.

**Acceptance Scenarios**:
1. Program 3.1 verbatim with `%%` comments matching §3.1 prose; goal `merge([X|Xs],Ys,Zs)` shows term-matching outcomes from §3.1's Worked Examples 1–4.

### User Story 2 — Channel-abstraction primitives via defined guards (Priority: P2)

Learner studies §3.2 channel ops as **defined guards**: `send/3`, `receive/3`, `new_channel/2` are unit clauses that the compiler unfolds at guard sites.

**Independent Test**: load `ch03/ch-03-ex-02-channel-primitives.glp`; run `make_pair(C1, C2).`, expect `C1` and `C2` cross-linked.

**Acceptance Scenarios**:
1. Channel-op unit clauses + `relay/3` + `make_pair/2` from p 23, exactly as printed; comments paraphrase the partial-evaluation note ("compiler unfolds the defined guards…").

### User Story 3 — Response binding via `bind_response/3` (Priority: P2)

Learner studies the response-binding idiom that Ch 8 cold-call protocol depends on.

**Independent Test**: load `ch03/ch-03-ex-03-bind-response.glp`; query `bind_response(yes, accept(R), L).` and verify the channel-pair construction in the head (post-PE) matches §3.2 prose.

**Acceptance Scenarios**:
1. `bind_response/3` from p 23, both clauses, with comments showing pre-PE and post-PE forms.

### User Story 4 — Guard negation idiom `lookup/3` (Priority: P3)

Learner studies the `~(...)` negation form on `=?=` from §3.2.

**Independent Test**: load `ch03/ch-03-ex-04-lookup-with-negation.glp`; run `lookup(b, [(a,1),(b,2),(c,3)], V?).`, expect `V = 2`.

**Acceptance Scenarios**:
1. `lookup/3` clauses from p 22, comments distinguishing negatable (`~(=?=)`) vs non-negatable guards.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Output dir `olamni/tutorial/ch03/`.
- **FR-002** Files: `ch-03-ex-01-glp-fair-stream-merger.glp`, `ch-03-ex-02-channel-primitives.glp`, `ch-03-ex-03-bind-response.glp`, `ch-03-ex-04-lookup-with-negation.glp`.
- **FR-003** Each `.glp` file MUST load and execute its primary demo goal under the GLP REPL with `→ succeeds`.
- **FR-004** Worked Examples 1–4 (success/suspend/fail/writer-to-writer-fail) are encoded as comments inside `ch-03-ex-01`, not separate Programs.
- **FR-005** Formal 3.1 (Circular Term Semantics) and Defs 3.1–3.6, Lemma 3.9, Propositions 3.7/3.8/3.10 are out of scope per charter §1 (formal-track material).
- **FR-006** §3.3 Exercises out of scope per charter.
- **FR-007** REPL-only, no Flutter, no modules.
