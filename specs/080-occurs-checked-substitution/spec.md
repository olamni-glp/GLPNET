<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: Occurs-checked substitution pipeline (compiler bind-time occurs-check)

**Feature Branch**: `080-occurs-checked-substitution`
**Created**: 2026-08-14
**Status**: Draft — 🔴 BLOCKED on a §1.14 language-authority decision by Udi (see Clarifications / FR-002)
**Input**: User description: "Occurs-checked substitution pipeline (compiler bind-time occurs-check) … the core question (UnifyFail vs CompileError when the occurs-check fires) is Udi's express decision to make. The spec MUST present both options as an OPEN clarification for Udi and MUST NOT decide the semantics."

## Context

Feature 077 (`guarded-term-traversal`, released `v2026.08.13.1`) made the C# compiler's shared term
walkers **tolerate** a cyclic `Term`: a cycle that reaches a walker now raises a catchable
`CompileError` instead of an uncatchable `StackOverflowException` (defence-in-depth). It did **not**
stop the cycle from being *created*. This feature is the producer-side complement: an **occurs-check at
the bind sites** so a self-referential substitution is never built in the first place — closing the
root cause of the F-069-1 crash class rather than only surviving it.

The crash class: a defined guard such as `p(X, s(X))` called as `p(Y, Y)` drives unification to bind
`Y ↦ s(Y)` — a substitution whose value contains the variable it binds. Applying that substitution
recurses forever. The bind sites live in the compiler's unification/substitution family, now
consolidated by 077 onto the shared `out/csharp/lib/compiler/term_traversal.cs` (formerly duplicated in
`partial_evaluator.cs` and `analyzer.cs`).

🔴 **This is a GLP language-authority (§1.14) feature.** Whether a bind that would create a cycle
should *fail cleanly* or *hard-reject at compile time* changes **what GLP accepts** — it is a change to
the language definition, not merely its implementation. Per CLAUDE.md §1.14 and DISCIPLINE.md §1.14,
that decision requires Udi's express approval. **This spec proposes the change and frames the decision;
it does NOT decide it, and no implementation proceeds until Udi rules.**

## Clarifications

### 🔴 Session 2026-08-14 — OPEN, awaiting Udi (§1.14)

- Q (**LANGUAGE-AUTHORITY, Udi's to decide**): When the bind-time occurs-check detects that binding a
  variable `X` to a term containing `X` would create a cycle, what is GLP's defined behaviour?
  → **Option (a) `UnifyFail`** — the unification fails; the enclosing guard/clause fails cleanly by the
    existing three-valued rules (Success | Suspend | Fail). This is classic sound unification
    (`unify_with_occurs_check`). It makes `=` and defined-guard unification *sound* without rejecting
    any program that never actually triggers a cycle at run/compile time; a program that *would* form
    the cycle simply fails that reduction instead of crashing.
  → **Option (b) `CompileError`** — a hard, catchable compile-time rejection (consistent with 077's
    FR-004 cyclic-term diagnostic). The program does not compile. This treats a cycle-forming bind as a
    static defect rather than a runtime failure.
  → **This spec records BOTH and selects NEITHER.** FR-002 is written conditionally on the outcome. The
    remaining requirements (where the check runs, coverage, no false positives, regression proof) are
    invariant to the choice and are specified now.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A cycle-forming bind is caught at its source, not survived downstream (Priority: P1)

A GLP program whose defined-guard partial-evaluation would build a self-referential substitution
(F-069-1 shape) is handled by a defined, non-crashing outcome **at the bind site**, before any walker
attempts to apply the cyclic substitution.

**Why this priority**: This is the feature's reason to exist — it removes the crash class at the root.
077 already prevents the *crash*; US1 prevents the *cycle's creation*, which is the correct layer and
unblocks reasoning about substitution soundness.

**Independent Test**: Compile the F-069-1 repro corpus (the cyclic-`=` programs 069 had to exclude and
077 catalogued under `programs/tests/cyclic/`). Each yields the defined outcome (per Udi's §1.14 ruling)
**at the bind site**, with no `StackOverflowException` and no reliance on the downstream walker guard as
the catch of last resort.

**Acceptance Scenarios**:

1. **Given** a defined guard `p(X, s(X))` invoked `p(Y, Y)`, **When** the compiler reaches the bind
   `Y ↦ s(Y)`, **Then** the occurs-check fires and the defined §1.14 outcome is produced (UnifyFail
   *or* CompileError per the ruling) — never a cycle in the substitution map.
2. **Given** the same program, **When** it is compiled, **Then** no walker downstream of the bind site
   ever receives a cyclic term (the 077 walker guard is defence-in-depth, not the primary catch).

### User Story 2 - Non-cyclic programs are unaffected — no false positives (Priority: P1)

Every program that does not actually form a self-referential bind compiles exactly as before, with no
new rejections and no measurable regression.

**Why this priority**: An occurs-check that over-fires would reject sound programs — the same
false-positive risk 077's codexreview caught in the structural guard. Soundness of the *addition* is as
important as the addition.

**Independent Test**: The full REPL suite plus the 077 acyclic fixtures (`deep_acyclic`, `dag_shared`)
compile and pass unchanged; deep-but-acyclic and DAG-shared substitutions bind normally.

**Acceptance Scenarios**:

1. **Given** a deeply nested acyclic term or a DAG-shared subterm, **When** it is bound, **Then** the
   occurs-check permits it and compilation succeeds.
2. **Given** the pre-feature REPL suite baseline, **When** the suite is re-run after the change,
   **Then** the pass count is unchanged (no new rejections).

### User Story 3 - Both producer bind-copies carry the check identically (Priority: P2)

The occurs-check is applied once, on the consolidated shared module, so the historically-duplicated
`analyzer.cs` / `partial_evaluator.cs` bind paths cannot diverge.

**Why this priority**: 077's dedup made this possible; the divergence between the two copies is exactly
what let F-069-1 hide. Landing the check on the shared module (not re-duplicating it) is what makes the
fix durable.

**Independent Test**: The bind-time occurs-check has a single implementation on the shared module; a
probe exercises it through both the PE-origin and analyzer-origin call paths and observes identical
behaviour.

**Acceptance Scenarios**:

1. **Given** the consolidated `term_traversal.cs`, **When** either the PE path or the analyzer path
   performs a bind, **Then** both consult the same occurs-check with the same outcome.

### Edge Cases

- A bind where the variable occurs only *inside* a shared (DAG) subterm that is **not** an ancestor —
  must NOT fire (that is sharing, not a cycle).
- A chained substitution (`X ↦ Y`, `Y ↦ s(X)`) where the cycle is only visible after resolution —
  the check must consider the resolved value, not just the immediate right-hand side.
- Constant-type / ground right-hand sides — the check must be cheap and never fire.
- Anonymous / write-only variables — must follow the same rule as their named equivalents (no special
  case that reopens the crash).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The compiler MUST perform an occurs-check at each unification/substitution **bind site**
  in the consolidated producer module, detecting when binding a variable to a term would place that
  variable within its own binding (directly or through resolution of the current substitution).
- **FR-002** (🔴 **§1.14 — conditional on Udi's ruling; NOT decided here**): On a detected
  cycle-forming bind, GLP MUST produce the outcome Udi selects — **either** (a) a clean `UnifyFail`
  under the existing three-valued unification rules, **or** (b) a hard catchable `CompileError`. The
  implementation MUST NOT be written until this is ruled; the two options have different acceptance
  tests (a failing-reduction test vs. a compile-rejection test).
- **FR-003**: The occurs-check MUST be implemented **once**, on the shared consolidated module
  (`term_traversal.cs`), and consulted by **both** the partial-evaluator-origin and analyzer-origin
  bind paths — never re-duplicated.
- **FR-004**: The occurs-check MUST NOT reject any acyclic term, including deeply-nested acyclic terms
  and DAG-shared subterms (no false positives).
- **FR-005**: The occurs-check MUST consider the **resolved** value of the binding (so a cycle formed
  only through a chain of substitutions is detected), consistent with the resolve-family semantics 077
  consolidated.
- **FR-006**: The change MUST be defence-in-depth *complementary* to 077: 077's walker cycle-guard
  remains in place as the last-resort catch; this feature ensures it is not the primary mechanism for
  the F-069-1 class.
- **FR-007**: The feature MUST be proven by a regression corpus: every F-069-1 / cyclic-`=` program
  reaches the FR-002 outcome at the bind site, and the acyclic corpus + full REPL suite remain green.
- **FR-008** (process): No code implementing FR-002's semantics may land before Udi's §1.14 approval is
  recorded. The spec, plan, and clarification are the propose-first artifacts; `/bk-implement` is gated.

### Key Entities

- **Bind site**: a point in the producer pipeline where a variable is bound to a term (a write to the
  substitution map) — the ~9 sites consolidated onto the shared module.
- **Occurs-check**: the predicate "does variable `X` occur within the resolved term `T` it is about to
  be bound to?"
- **Cycle-forming bind**: a bind for which the occurs-check is true.
- **§1.14 outcome**: the language-defined response to a cycle-forming bind — UnifyFail or CompileError,
  Udi's decision.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the F-069-1 / cyclic-`=` regression corpus produces the defined §1.14 outcome at
  the bind site with zero `StackOverflowException`.
- **SC-002**: Zero false positives — the acyclic corpus (deep-nested + DAG-shared) and the full REPL
  suite show no new rejections versus the pre-feature baseline.
- **SC-003**: A single occurs-check implementation serves both producer bind paths (no duplicate
  copy), verified by a probe exercising both origins.
- **SC-004**: The F-069-1 crash class is closed at the producer layer — a fault-injection that would
  previously build a cyclic substitution is stopped at the bind site, not merely survived downstream.

## Assumptions

- 077 is released and its consolidated `term_traversal.cs` + walker cycle-guards are in place (this
  feature builds directly on that module; hard dependency).
- "Producer side" = the partial-evaluator + analyzer bind paths; the runtime/heap unifier is out of
  scope for this feature (it has its own FCP-derived architecture and any change there is a separate
  §1.14 discussion).
- Testing follows 077's adaptation: the C# compiler is exercised via the REPL suite + a console probe
  (no xUnit harness for `out/csharp`), since a cycle-forming AST can only be built programmatically.
- The §1.14 decision (FR-002) is Udi's; this spec deliberately leaves it open and blocks implementation
  on it. Producing spec + plan is the propose-first deliverable §1.14 requires.
