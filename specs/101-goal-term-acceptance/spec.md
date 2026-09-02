<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: Front-end goal-term acceptance completeness (parser + REPL goal builders, cross-runtime)

**Feature Branch**: `101-goal-term-acceptance`
**Created**: 2026-09-02
**Status**: Draft
**Input**: User description: "Front-end goal-term acceptance completeness (parser + REPL goal builders, cross-runtime)"

**Roadmap feature**: `front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime` (rank 21, WSJF 3.60 / RICE 3000, promoted)
**Marathon era**: `mrun-fb28dd92afe0`
**Engineer ruling selecting this work**: `Q-glpnetshiras-04` ("Front-end goal-term acceptance")

---

## Measured Baseline *(mandatory — this spec's scope is set by measurement, not by the recorded notes)*

The roadmap brief and `CLAUDE.md` name three defects. All three were re-measured on
build `54219ce8` before this spec was written. **Two of the three are already fixed**, the
third is **broader than recorded**, and **a fourth, previously unrecorded defect was found**.
The scope below reflects the measurement, not the notes.

| # | Recorded claim | Measured result | Verdict |
|---|---|---|---|
| L1 | "`=..` not allowed in clause bodies (parser bug). Works in clause heads only." | A module whose clause bodies contain only `Term =.. Parts?` and `Parts ..= Term?` loads cleanly through the full pipeline (SRSW → PE → type-check → compile). | **STALE — already works** |
| L2 | "Structs inside lists in REPL goals fail: `Unsupported list head type: StructTerm`." | `first_item([send(1,a), send(2,b)], Y).` → `Y = send(1, a)`, succeeds. Conjunctive and nested-list forms also succeed. Both list builders already branch on struct heads. | **STALE — already works** |
| L3 | "C# REPL `_SetupArgument` throws on `UnderscoreTerm` in top-level goals." | Confirmed in C#. **The Dart runtime has the identical gap**, at four distinct positions. The Gleam runtime refuses the same shapes by design. | **LIVE — and cross-runtime, not C#-only** |
| L4 | *(not previously recorded)* | An improper list tail is **silently discarded and treated as nil** in Dart and C#: `first_item([send(1,a)\|foo], Y).` returns `Y = send(1, a)` — byte-identical to the well-formed `[send(1,a)\|[]]`. | **LIVE — silent wrong answer** |

### L3 — measured failure surface (Dart, build `54219ce8`)

| Goal shape | Observed |
|---|---|
| `first_item([send(1,a)], _).` | `Exception: Unsupported argument type: UnderscoreTerm` |
| `first_item([send(1,_)], Y).` | `Exception: Unsupported struct argument type: UnderscoreTerm` |
| `first_item([_], Y).` | `Exception: Unsupported list head type: UnderscoreTerm` |
| `first_item([send(1,a)], _), first_item([send(2,b)], Z).` | `Exception: Unsupported argument type: UnderscoreTerm` (conjunction path) |

### Cross-runtime state today

| Runtime | Anonymous `_` in a goal | Improper list tail |
|---|---|---|
| Dart | Rejects with an internal exception | **Silently coerced to nil (wrong answer)** |
| C# | Rejects with an internal exception | **Silently coerced to nil (wrong answer)** |
| Gleam | Refuses loudly, named as a deferred shape | Refuses loudly, named as a frozen-semantics gap |

The Gleam port independently identified and documented both L3 and L4 when it was written,
recording them as deliberately-mirrored gaps rather than fixing them. It is the only runtime
that never returns a wrong answer for these inputs.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Anonymous variables are accepted in goals (Priority: P1)

A GLP programmer runs a goal at the REPL and does not care about one of the outputs, so they
write `_` in that position — the standard way to discard a value everywhere else in the
language. Today the goal does not run: it fails with an internal exception naming an internal
term class. The programmer must invent a throwaway variable name and mentally ignore the
result that comes back.

**Why this priority**: This is the defect that actually blocks work, it is reachable by the
most obvious possible user action, and it is the one the engineer's selection ruling named.
The failure text exposes internal class names rather than telling the programmer anything
actionable, and the same goal is rejected by two of the three runtimes, so a program cannot be
moved between them.

**Independent Test**: Load any module and run a goal with `_` in an argument position on each
runtime. Delivers value on its own: the discard idiom becomes usable at the REPL without any
other part of this feature.

**Acceptance Scenarios**:

1. **Given** a loaded module with `first_item/2`, **When** the programmer runs `first_item([send(1,a)], _).`, **Then** the goal runs to completion and reports success, with no binding reported for the discarded position.
2. **Given** the same module, **When** the programmer runs `first_item([send(1,_)], Y).` (anonymous inside a structure), **Then** the goal runs and `Y` is reported.
3. **Given** the same module, **When** the programmer runs `first_item([_], Y).` (anonymous as a list element), **Then** the goal runs and `Y` is reported.
4. **Given** the same module, **When** the programmer runs a conjunctive goal containing `_` in either conjunct, **Then** the goal runs and every named variable is reported.
5. **Given** the same goal text, **When** it is run on the Dart, C# and Gleam runtimes, **Then** all three accept it and report the same bindings.

---

### User Story 2 — A malformed goal term is refused, never silently altered (Priority: P2)

A programmer mistypes a list in a goal — `[a|foo]` instead of `[a|[]]` or `[a|T]`. Today the
Dart and C# runtimes silently discard the malformed tail, run the goal against a *different*
term than the one that was typed, and report success. The programmer receives a plausible
answer to a question they did not ask, with nothing on screen to indicate substitution.

**Why this priority**: A silent wrong answer is more damaging than a refusal — it cannot be
noticed, so it corrupts any conclusion drawn from the session. It ranks below US1 only because
it is reached by a typo rather than by an idiom people deliberately use. Note this is the
opposite failure direction from the rest of the feature: here the front end accepts too much.

**Independent Test**: Run a goal with an improper list tail and confirm the session refuses it
rather than answering. Testable with no part of US1 in place.

**Acceptance Scenarios**:

1. **Given** a loaded module, **When** the programmer runs a goal containing a list whose tail is neither a list nor a variable, **Then** the goal is refused with a message that identifies the malformed term, and no bindings are reported.
2. **Given** the same session, **When** the programmer corrects the tail to a proper list or a variable, **Then** the goal runs normally.
3. **Given** the same malformed goal, **When** it is run on the Dart, C# and Gleam runtimes, **Then** all three refuse it and none reports success.

---

### User Story 3 — The recorded limitations match the product (Priority: P3)

A contributor reads `CLAUDE.md` and `docs/known-issues.md` to find out what the front end
accepts. Two of the limitations recorded there are measurably false: they warn against
constructs that work today. The contributor writes around a restriction that does not exist,
or reports a bug that was fixed. A third entry points at a source location that no longer
holds the code.

**Why this priority**: It costs contributor time and credibility rather than blocking a task,
so it ranks last — but the correction is only durable if the corrected claims are pinned by
tests, otherwise the notes silently drift again.

**Independent Test**: Read the updated notes, run the regression tests that assert each
retired claim, and confirm the tests fail if the capability regresses.

**Acceptance Scenarios**:

1. **Given** the updated documentation, **When** a contributor looks up `=..` in clause bodies, **Then** it is described as supported, with the stale claim marked as retired and dated rather than deleted without trace.
2. **Given** the updated documentation, **When** a contributor looks up structs inside lists in REPL goals, **Then** it is described as supported and the superseded source location is corrected.
3. **Given** the regression suite, **When** either retired capability stops working, **Then** at least one test fails and names the capability.

---

### Edge Cases

- An anonymous variable in **every** argument position of a goal (`p(_, _, _)`): each occurrence is independent and no two are aliased to one another.
- Repeated `_` in one goal must not be treated as the same variable — `_` is a writer nobody reads, so two occurrences are two distinct discards.
- `_` at a position the procedure declares as an **input** (a reader position): the type checker's existing rules apply unchanged; this feature does not relax them. `_?` remains invalid, as the language defines.
- A malformed tail nested inside an otherwise well-formed structure or list (`p(f([a|foo]))`) must be refused with the same clarity as at the top level.
- An empty list, a single-element list, and a list with a bare variable tail must all keep working exactly as they do today — these are the shapes the current fallback also catches, and the fix must not narrow them.
- A goal that is refused must leave the session usable: the next goal runs normally, with no leaked heap state from the refused attempt.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A goal argument that is an anonymous variable MUST be accepted and MUST run, in every position where a named variable is accepted today — top-level argument, structure argument, list element, and list tail.
- **FR-002**: FR-001 MUST hold for conjunctive goals as well as single goals.
- **FR-003**: Each occurrence of an anonymous variable in a goal MUST be independent of every other occurrence; no two occurrences may be aliased.
- **FR-004**: An anonymous goal argument MUST NOT be reported as a binding in the result, since it has no name to report against.
- **FR-005**: The system MUST NOT alter a goal term that it cannot faithfully represent. Where a goal term is malformed, the system MUST refuse the goal rather than substitute a different term and report success.
- **FR-006**: A refusal under FR-005 MUST identify the malformed term in terms the programmer typed, and MUST NOT be the sole notification via an internal class name.
- **FR-007**: After a refused goal, the session MUST remain usable for subsequent goals.
- **FR-008**: The Dart, C# and Gleam runtimes MUST agree on which goal terms they accept and which they refuse, for every shape covered by FR-001 through FR-005.
- **FR-009**: The regression suite MUST contain a test for each shape in FR-001 and FR-005 that fails if the shape's handling regresses.
- **FR-010**: The recorded limitations that measurement has retired MUST be corrected in the project documentation, each marked as retired with the date and the evidence, rather than silently removed.
- **FR-011**: Source locations cited in the retained documentation MUST name the file that currently holds the code.
- **FR-012**: This feature MUST NOT change what the GLP language accepts in clause heads, guards or bodies; its scope is confined to the acceptance of goal terms entered at a front end.

### Key Entities

- **Goal term**: a term a programmer enters at a front end to be executed, as distinct from a term appearing in a stored clause. This feature governs only the former.
- **Anonymous variable**: a writer with no paired reader, used to discard a value. Already defined by the language and already handled by the compiler, type checker and SRSW checker.
- **Improper list tail**: a list tail that is neither a further list nor a variable, and therefore denotes no list.
- **Runtime front end**: the component of each runtime that turns a parsed goal into the argument registers execution starts from. Three exist — Dart, C#, Gleam — and they must agree.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All four goal shapes recorded as failing in the Measured Baseline (L3) run successfully; the count of failing shapes goes from 4 to 0.
- **SC-002**: A goal containing a malformed list tail is refused by all three runtimes; the count of runtimes that answer it goes from 2 to 0.
- **SC-003**: For a shared set of goal shapes covering FR-001 to FR-005, the three runtimes return identical accept/refuse verdicts — 0 divergences.
- **SC-004**: A programmer can discard a goal result using the language's normal discard idiom on the first attempt, with no workaround and no invented variable name.
- **SC-005**: Every claim about front-end goal acceptance in the project documentation is backed by a test in the regression suite; 0 untested claims remain.
- **SC-006**: The existing regression suites remain green — no shape that works today stops working.

---

## Assumptions

- **Accepting an anonymous variable in a goal is completeness, not language change.** The anonymous variable is already part of the GLP language and is already handled by the parser, SRSW checker, type checker and compiler; only the front-end step that materialises goal arguments omits it. On that basis this work is treated as closing a gap in an existing surface, and does **not** require a §1.14 language-authority approval. If the engineer reads it otherwise, US1 becomes gated and this assumption must be revisited before implementation.
- **The meaning of an improper list tail is a language question and is excluded from scope.** US2 deliberately does not decide what `[a|foo]` denotes. It requires only that a term the system cannot faithfully represent be refused instead of silently replaced — which removes a wrong answer without deciding any new semantics. Assigning a meaning to such a term would be a §1.14 matter for Udi; the Gleam port reached the same conclusion independently and recorded it as a frozen-semantics gap.
- The three runtimes remain the full set of front ends in scope. No fourth runtime is assumed.
- The Gleam runtime's current loud refusals are treated as the correct reference behaviour for US2, and as the model for the parity required by FR-008.
- The Gleam runtime's conjunction path is currently deferred rather than implemented; FR-002's parity obligation for Gleam is bounded by whatever conjunction support exists there, and closing that deferral is not assumed to be part of this feature unless planning finds it cheap.
- The measured baseline was taken on build `54219ce8`. If implementation begins from a materially later build, the four measurements should be re-run before work starts, since two of the three original claims had already gone stale once.
- Correcting the documentation is in scope for this feature; re-verifying the *rest* of `docs/known-issues.md` is not.

---

## Out of Scope

- Any change to what clause heads, guards or bodies accept.
- Assigning a meaning to an improper list tail (§1.14 — Udi).
- Implementing the Gleam conjunction path, beyond what parity for the shapes above requires.
- A general audit of `docs/known-issues.md` outside the entries this feature retires.
