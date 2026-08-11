<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Guarded term-traversal utilities (cycle-tolerant compiler walkers + PE/analyzer dedup)

**Feature Branch**: `077-guarded-term-traversal`  
**Created**: 2026-08-11  
**Status**: Draft  
**Input**: User description: "Guarded term-traversal utilities (cycle-tolerant compiler walkers + PE/analyzer dedup). FOUNDATIONAL, MANDATORY, FIRST — prerequisite of the occurs-check feature (F-069-1)."

## Context & Motivation

The GLP compiler back-end contains recursive term walkers that every assume a **finite, acyclic** `Term` graph and carry **no shared visited-set or depth/fuel guard**. When a cyclic `Term` reaches one of them — whether introduced by a self-referential substitution (the F-069-1 defect class) or by programmatic AST construction — the walker recurses without bound and the process dies with an **uncatchable `StackOverflowException`** during compilation.

The affected walkers were identified by the codex-adjudicated `/bk-3rtask` root-cause run `20260811T085855Z-8d6f`:

- `ApplySubstitution`, `ResolveTerm`, `ApplyRenaming` — **duplicated** across `partial_evaluator.cs` and `analyzer.cs` (two independently-maintained copies of the same machinery).
- Six term walkers in `codegen.cs` — codegen is the **blast-radius** because it never binds a variable, so a cyclic term flowing into it cannot be "resolved away" first.
- `ResolveGoal` in `project_linker.cs`.

Two design escalations from that run were **resolved by Gabi on 2026-08-11** and are binding on this feature:

1. **Defense-in-depth invariant** — EVERY compiler term walker MUST be cycle-tolerant. An occurs-check at bind time (the sibling feature) is necessary but not sufficient: cyclic terms can also arise from programmatic AST construction that never passes through a bind site. Cycle-tolerance at the walkers is the backstop.
2. **Dedup NOW** — the duplicated `analyzer.cs` ↔ `partial_evaluator.cs` unifier/substitution/resolve machinery MUST be consolidated into ONE shared module immediately, as the foundation onto which the occurs-check feature later lands its single change.

This feature is **foundational, mandatory, and first**: the occurs-checked-substitution-pipeline feature (closing F-069-1) has a hard dependency on the consolidated module this feature produces.

## Clarifications

### Session 2026-08-11

- Q: On a detected cycle (revisited node / exhausted fuel), what is the controlled outcome — hard-fail with a `CompileError`, or return the revisited node and let traversal terminate? → A: **Hard-fail, raising a `CompileError`** (a distinct, catchable compiler diagnostic). Rationale: (1) consistent with the sibling occurs-check feature's stated outcome ("graceful `UnifyFail`/`CompileError`"), so both the bind-time check and the traversal backstop surface the same diagnosable failure class; (2) returning a revisited node risks silently producing incorrect compiled output, violating the no-silent-failure discipline (DISCIPLINE §5.2) — a loud `CompileError` is diagnosable, a quietly-wrong codegen is not. This is a compiler-behaviour choice (defining behaviour where there was previously an uncatchable `StackOverflowException`), NOT a GLP language-definition change, so it is settled here without a §1.14 propose-first gate (see FR-007).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cyclic term no longer crashes the compiler (Priority: P1)

As a GLP developer (or any process that drives the compiler), when a cyclic `Term` reaches any compiler term walker, the compiler terminates that traversal in **bounded** time with a **catchable, diagnosable** outcome instead of an uncatchable `StackOverflowException` that takes down the whole process.

**Why this priority**: This is the whole point of the feature — it converts a fatal, uncatchable crash into a controlled, reportable condition, and it is the defense-in-depth backstop the resolved escalation mandates. Without it nothing else matters.

**Independent Test**: Construct a cyclic `Term` programmatically, feed it to each guarded walker (substitution, resolve, renaming, the codegen walkers, linker resolve), and confirm each returns/raises a controlled result in bounded time with no `StackOverflowException` and no process death.

**Acceptance Scenarios**:

1. **Given** a cyclic `Term` (e.g. `X = s(X)` shape) constructed directly, **When** it is passed to any guarded walker, **Then** the walker halts in bounded time with the agreed controlled outcome (see FR-004) and never overflows the stack.
2. **Given** the F-069-1 reproduction (a defined guard `p(X, s(X))` called as `p(Y, Y)` producing a self-referential substitution), **When** the program is compiled, **Then** compilation ends in a controlled, catchable outcome rather than a process-killing crash.
3. **Given** a normal acyclic `Term`, **When** it is passed through any guarded walker, **Then** the result is byte-for-byte identical to the pre-feature behaviour (no regression on the common path).

---

### User Story 2 - One shared traversal utility, not eleven ad-hoc recursions (Priority: P1)

As a compiler maintainer, I want the cycle-guard logic to live in ONE shared traversal utility that all ~11 walkers route through, so the guard is fixed once and can never drift between call sites.

**Why this priority**: The resolved "defense-in-depth" invariant requires *every* walker to be cycle-tolerant. Per DISCIPLINE §1.3 (fix infrastructure, not symptoms), the correct implementation is one shared guarded-traversal utility, not a repeated guard pasted into eleven places. This is co-P1 with US1 because a per-site guard would be the wrong structure even if it passed US1's tests.

**Independent Test**: Verify that each of the ~11 identified walkers delegates its cycle-guarding to the single shared utility (visited-set / fuel bound), and that there is exactly one implementation of that guard.

**Acceptance Scenarios**:

1. **Given** the shared traversal utility, **When** any of the ~11 walkers traverses a term, **Then** it obtains its cycle-guard from that single utility.
2. **Given** a change to the guard policy, **When** it is made in the shared utility, **Then** all ~11 walkers observe the change with no per-site edits.

---

### User Story 3 - PE/analyzer substitution machinery consolidated (dedup NOW) (Priority: P1)

As a compiler maintainer, I want the duplicated unifier/substitution/resolve machinery in `analyzer.cs` and `partial_evaluator.cs` merged into ONE shared module, so the occurs-check feature has a single place to land and the two copies can never diverge again.

**Why this priority**: The resolved "dedup NOW" escalation makes this mandatory and foundational — the occurs-check feature depends on it. Doing the occurs-check on two divergent copies is exactly the trap this feature exists to remove.

**Independent Test**: Confirm `ApplySubstitution` / `ResolveTerm` / `ApplyRenaming` (and the unify/substitution/resolve machinery they belong to) exist as a single shared implementation consumed by both `analyzer.cs` and `partial_evaluator.cs`, with no second copy remaining.

**Acceptance Scenarios**:

1. **Given** the consolidated module, **When** `analyzer.cs` and `partial_evaluator.cs` perform unification/substitution/resolution, **Then** both invoke the same shared implementation.
2. **Given** the consolidation, **When** the full REPL + engine test suites run, **Then** results are identical to the pre-feature baseline (behaviour-preserving refactor).

---

### Edge Cases

- **Cyclic term at maximum nesting**: a deeply nested but ultimately cyclic term must still be caught by the visited-set / fuel bound, not merely by a fixed recursion-depth heuristic that a legitimate deep acyclic term could also trip.
- **Legitimate deep acyclic term**: a large but acyclic term (deep lists, wide structures) MUST NOT be falsely flagged as cyclic — the guard must distinguish revisiting a node from merely going deep.
- **Shared (DAG) subterms without a cycle**: a term that shares a subterm via multiple parents (a DAG, not a cycle) MUST traverse successfully and MUST NOT be treated as cyclic.
- **Cycle discovered mid-traversal after partial output**: for walkers that build output as they go (e.g. codegen), the controlled outcome must be well-defined even when the cycle is discovered after some output has been produced.
- **Behaviour parity on the acyclic common path**: the guard must add no observable change for the overwhelmingly common acyclic input.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The compiler MUST provide a single shared term-traversal utility that bounds every traversal via a visited-set and/or a fuel/step bound, such that no traversal of any `Term` graph — cyclic or acyclic — can recurse without bound.
- **FR-002**: Every identified compiler term walker MUST route its traversal through the shared utility. The identified set is: `ApplySubstitution`, `ResolveTerm`, `ApplyRenaming` (consolidated), the six `codegen.cs` walkers, and `project_linker.cs`'s `ResolveGoal` (~11 walkers total).
- **FR-003**: The duplicated unifier/substitution/resolve machinery currently maintained separately in `analyzer.cs` and `partial_evaluator.cs` MUST be consolidated into ONE shared module consumed by both, leaving no second copy.
- **FR-004**: When the shared utility detects a cycle (a revisited node / exhausted fuel), it MUST hard-fail by raising a distinct, catchable `CompileError` — never a `StackOverflowException`, never silent process death, and never a silently-returned revisited node that could yield incorrect compiled output. *(Decided at `/bk-clarify` 2026-08-11; see Clarifications.)*
- **FR-005**: On acyclic input, every guarded walker MUST produce results identical to the pre-feature behaviour (behaviour-preserving on the common path; no regression in the existing REPL/engine suites).
- **FR-006**: The guard MUST distinguish a genuine cycle (a node reachable from itself) from a merely deep or DAG-shared acyclic term, so that legitimate large acyclic terms are not falsely rejected.
- **FR-007**: This feature MUST NOT change the GLP language definition or what a well-formed program means. It hardens traversal against an input that previously produced undefined behaviour (a crash); it introduces no new guard, predicate, directive, or type-system feature. *(Distinguishes this feature from the sibling occurs-check feature, which does carry a §1.14 language-authority question.)*
- **FR-008**: The change MUST be confined to the compiler back-end (`out/csharp/lib/compiler/`) — specifically `analyzer.cs`, `codegen.cs`, `partial_evaluator.cs`, `project_linker.cs`, plus any new shared-module file(s) under the same tree. No runtime/kernel/`self.glp`/language-surface files are touched.

### Key Entities *(include if feature involves data)*

- **Term graph**: the compiler's in-memory representation of a GLP term; may be acyclic (the normal case), a DAG (shared subterms, still acyclic), or — pathologically — cyclic. The unit of traversal.
- **Shared traversal utility**: the single new component that carries the visited-set / fuel bound and the cycle-detection policy; the one place the guard is defined.
- **Consolidated substitution module**: the single merged home of the unify/substitution/resolve machinery (`ApplySubstitution` / `ResolveTerm` / `ApplyRenaming`) previously duplicated across `analyzer.cs` and `partial_evaluator.cs`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Feeding a cyclic `Term` to any of the ~11 guarded walkers produces a controlled, catchable outcome in bounded time in 100% of cases — zero `StackOverflowException`s, zero process deaths.
- **SC-002**: The F-069-1 reproduction compiles to a controlled outcome (no crash), and the SC-003 fuzz corpus from feature 069 can run cyclic-`=` inputs directly, without the non-cyclic-scoping workaround previously required.
- **SC-003**: The cycle-guard logic has exactly ONE implementation; all ~11 walkers route through it (verified by inspection/tests — no second guard copy exists).
- **SC-004**: The unify/substitution/resolve machinery has exactly ONE shared implementation consumed by both `analyzer.cs` and `partial_evaluator.cs` — the pre-existing second copy is gone.
- **SC-005**: The full REPL test suite and the C# engine build/tests are green with results identical to the pre-feature baseline (behaviour-preserving on all acyclic inputs; no new failures).
- **SC-006**: A legitimate deep acyclic term and a DAG-shared acyclic term both traverse successfully and are NOT falsely reported as cyclic.

## Assumptions

- **Resolved at `/bk-clarify` (2026-08-11)**: the controlled outcome on a detected cycle (FR-004) is **hard-fail raising a `CompileError`** — see Clarifications. This was a compiler-behaviour choice (defined behaviour replacing an uncatchable crash), not a GLP language change, so it needed no §1.14 propose-first-with-Udi gate. The sibling occurs-check feature is where the §1.14 reject-vs-accept language question lives.
- The visited-set / fuel-bound approach is assumed to be the implementation vehicle; the specific mechanism (identity visited-set, structural visited-set, fuel counter, or a combination) is a `/bk-plan` decision.
- The ~11 walkers enumerated by 3rtask run `20260811T085855Z-8d6f` are assumed complete; `/bk-plan` will re-verify the enumeration against current `out/csharp/lib/compiler/` source before routing.
- This feature is behaviour-preserving on all currently-valid (acyclic) programs; the only behaviour change is for inputs that previously crashed the compiler.
- **Dependency**: the occurs-checked-substitution-pipeline feature (F-069-1) is blocked-by this feature and will land its single occurs-check change on the consolidated module produced here.
- The existing REPL suite (`test/run_all_tests.sh`) and the C# engine build/tests are the authoritative regression signal for the behaviour-preservation criteria.
