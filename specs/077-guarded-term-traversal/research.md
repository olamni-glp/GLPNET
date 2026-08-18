<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Phase 0 Research: Guarded term-traversal utilities

Grounded against current source at `out/csharp/lib/compiler/` (re-verified this session; supersedes the "~11 walkers" 3rtask estimate).

## Decision 1 — The true walker inventory (~21 unguarded, 2 already guarded)

**Decision**: Route all of the following recursive `Term`/`Goal` walkers through the shared guard.

| File | Unguarded walkers (line) |
|---|---|
| `analyzer.cs` (`DefinedGuardEvaluator`) | `_ExtractAndMarkGroundedVars` (781), `_MarkVarsInTermAsTypeGrounded` (800), `_AnalyzeTerm` (823), `_CollectVarNames` (1080), `_ApplyRenaming` (1097), `_UnifyTerms` (1162), `_ApplySubstitution` (1338), `_ApplySubstitutionToGoal` (1377) |
| `partial_evaluator.cs` (`PartialEvaluator`) | `CollectVarNames` (397), `ApplyRenaming` (424), `UnifyTerms` (485), `ApplySubstitution` (679), `ApplySubstitutionToGoal` (712), `IsGround` (770) |
| `codegen.cs` | `_GenerateStructureElement` (369), `_IsGroundTerm` (670), `_GroundTermToValue` (687), `_GenerateArgumentStructureElement` (710), `_GenerateStructureElementInBody` (792), `_GenerateListTailInBody` (849) |
| `project_linker.cs` | `ResolveGoal` (378) — Goal-graph, identity-preserving |

**Already guarded (reference pattern, do not regress)**: `analyzer._ResolveTerm` (1300) and `partial_evaluator.ResolveTerm` (644) — both carry `HashSet<string> visited` keyed on `VarTerm.Name`, returning the revisited node to break substitution cycles.

**Rationale**: The escalation-mandated invariant is "EVERY term walker cycle-tolerant." The 3rtask "~11" was an estimate; the spec explicitly flagged that plan would re-verify. Routing all ~21 is what "defense-in-depth" requires.

**Alternatives considered**: Guard only the substitution/resolve family (the F-069-1 path). Rejected — the escalation resolution is explicit that a bind-time occurs-check is insufficient and every walker must be tolerant, because cyclic Terms can arise from programmatic AST construction that never passes a bind site.

## Decision 2 — Cycle-guard strategy: two flavors keyed to how cycles actually arise

**Decision**:
- **Substitution/resolve family** (`ApplySubstitution`, `UnifyTerms`, `ResolveTerm`, `ApplyRenaming`, `CollectVarNames`, the mark/ground walkers): cycles arise from the **substitution map** (`var-name → Term` with a self-reference, the F-069-1 shape). Guard with a **visited-set keyed on `VarTerm.Name`** — exactly the existing `ResolveTerm` mechanism, generalised into the shared module. On revisit of a name already on the active path → declare a cycle.
- **Structural family** (the six `codegen.cs` walkers, `project_linker.ResolveGoal`): these walk an AST tree that is normally acyclic; a cycle here can only come from a **programmatically-constructed cyclic `Term`** (no var-name to key on). Guard with a **fuel/step bound** (and/or a reference-identity visited-set via `ReferenceEqualityComparer`), since `Term` subclasses are reference types with default identity (`ast.cs`).

**Rationale**: Keying on var-name matches where real cycles come from (the substitution closure) and reuses the proven `ResolveTerm` guard; a fuel/identity bound covers the "constructed cyclic AST node" case the structural walkers face. One shared utility exposes both entry points so the guard is defined once (FR-001/SC-003).

**Alternatives considered**: A single universal reference-identity visited-set for everything. Rejected — the substitution cycles are *not* shared AST nodes (the map holds distinct Term objects that reference the same var *name*), so identity keying would miss them; var-name keying is required there. Pure fixed recursion-depth cap. Rejected — a legitimate deep acyclic term (FR-006 edge case) would be falsely rejected; the visited-set distinguishes revisiting from merely-deep.

## Decision 3 — The 5 consolidation divergences (dedup is behaviour-sensitive)

The two copies are **not** identical. Each divergence is reconciled explicitly; the full REPL + engine suite is the gate that the merge is behaviour-preserving (FR-005).

| # | Divergence | analyzer copy | PE copy | Reconciliation |
|---|---|---|---|---|
| 1 | **Anonymous-var predicate** (opens `UnifyTerms`) | `_IsAnonymous` = `UnderscoreTerm` OR `VarTerm.Name.StartsWith('_')` (1283) | `IsUnderscore` = `UnderscoreTerm` OR `Name == "_"` exact (628) | **Behaviour-affecting** — a var `_Foo` is skipped by analyzer, unified by PE. Shared unifier must pick one. **Plan: preserve BOTH via a parameter** (`Func<Term,bool> isAnonymous`) so each caller keeps its current semantics; do NOT silently flatten. Flag for `/bk-analyze` + owner review. |
| 2 | **Fresh-var prefix** | `PE_{n}`, counter `int _varCounter` (1075/908) | `PE{n}`, counter `long _varCounter` (389/80) | Cosmetic to output *names* but names are compared elsewhere. Preserve per-caller prefix via a parameter; standardise counter to `long`. |
| 3 | **Underscore filter in renaming** | skips `!name.StartsWith('_')` (1074) | skips `name != "_"` (388) | Same root as #1; tie to the same `isAnonymous` parameter so #1 and #3 stay consistent. |
| 4 | **`TransformClause` control flow** | unit-clause / remaining-guard arms only, no proc set (978) | +`BuiltinProcedures` passthrough, +`allProcedures`→`CompileError` arm, takes `allProcedures` param (184) | These are PE-orchestration concerns, NOT shared-primitive concerns. **Keep `TransformClause` in its owning class**; only the shared *primitives* (unify/subst/resolve/renaming) move to the shared module. |
| 5 | **`ResolveSubstitution` return type** | `IReadOnlyDictionary<string,Term>` (1290) | `Dictionary<string,Term>` (636) | Shared signature returns `IReadOnlyDictionary`; PE-internal callers that mutate get a local copy. |

**Rationale**: DISCIPLINE IV-b (preserve working internals) + II (no workarounds). The correct consolidation moves only the genuinely-shared primitives and parameterises the two legitimate behavioural differences (#1/#3 anonymous semantics, #2 prefix) rather than picking a winner that could silently change what compiles. Differences #4/#5 stay in the owning classes or are normalised to the wider type.

**Alternatives considered**: Flatten to one semantics for #1 (e.g. adopt PE's exact `== "_"`). Rejected as an unreviewed behaviour change to analyzer's grounding — it would change what unifies during analysis; must not be decided silently in a "dedup" (that would be the exact §II workaround-in-disguise trap). Parameterisation preserves behaviour; any future unification of the two semantics is a separate, owner-approved decision.

## Decision 4 — FR-004 cyclic outcome: raise `CompileError`

**Decision**: On a declared cycle the shared guard raises `new CompileError(message, line, column, phase: <owning-phase>)` using the existing type at `error.cs:13`. Reuse the caller's phase (`"analyzer"` / `"codegen"`); optionally add a `"term_traversal"` mapping in `CategoryFromPhase` (`error.cs:33`) — a mapping addition, not a language change.

**Rationale**: Clarified FR-004 = hard-fail. `CompileError` is the established catchable compiler exception used throughout these files; it is caught by the REPL/engine compile driver and surfaced as a diagnostic. A cyclic term therefore becomes a normal compile diagnostic. NOT modeled as a new `UnifyResult` subtype (`unify_result.cs`) because most walkers don't return `UnifyResult`, and a thrown `CompileError` uniformly covers all ~21 sites.

**Alternatives considered**: New `UnifyCyclic : UnifyResult`. Rejected — only the unify path returns `UnifyResult`; the structural/codegen walkers return other types, so an exception is the only uniform signal. Returning the revisited node everywhere (the existing `ResolveTerm` behaviour). Rejected by the clarify decision (silent-wrong-output risk) — though note `ResolveTerm`'s existing return-node behaviour is *inside* the resolve closure and is preserved there; the *hard-fail* applies at the outer guard when a genuine unbreakable cycle is detected.

## Open items for `/bk-analyze` / owner

- **Divergence #1 (anonymous semantics)** is the one behaviour-sensitive point. The plan preserves both via parameterisation; `/bk-analyze` should confirm the parameterisation is faithful and flag whether the owner wants the two eventually unified (a separate feature).
- The `ResolveTerm` return-revisited-node behaviour (existing) vs FR-004 outer hard-fail must be reconciled coherently in the shared module: inner resolve may still short-circuit a self-reference to terminate, but an unresolvable structural cycle surfaces as `CompileError`. Tasks must make this boundary explicit and tested.

---

## Decision 5 — Codexreview hardening (2026-08-12, run `20260812T175553Z`, converged@3)

A plan-first adversarial review (local Claude reviewers + codex CLI, 3 cycles) surfaced and closed
five defects; the deterministic merge converged at cycle 3 (both teams 0 new findings). Full suite
554/554 throughout.

1. **Codegen false-positive (HIGH, reachable regression).** `codegen._GenerateListTailInBody`
   `Enter`ed a node then delegated the SAME object to `_GenerateStructureElementInBody`, which
   re-`Enter`ed it on the shared identity guard → a spurious `CyclicTermError` on a valid ACYCLIC
   body partial-list nested in a struct (e.g. `box([1|Xs?])`). Empirically reproduced through the C#
   REPL. Fix: guard only the `ListTerm` self-recursion arm; the variable/other-tail arm delegates
   without a second `Enter`. Regression-guarded by `deep_acyclic.glp`.

2. **Fuel-only guard is INEFFECTIVE for the shared walkers (HIGH).** The first fix routed
   `UnifyTerms`/`CollectVarNames`/`ApplyRenaming`/`ApplySubstitution` through a fuel-only
   `Step()` (8M budget). codex correctly showed this never fires: a cyclic term recurses unbounded
   in DEPTH and overflows the .NET stack (~thousands of frames) long before 8M fuel is spent — and a
   fuel bound low enough to pre-empt the stack would falsely reject a legitimately deep (depth-6000)
   acyclic term. **Lesson: for recursive-descent walkers, IDENTITY (catch the cycle at its period),
   not fuel, is the load-bearing guard.** Fix: `StructuralGuard.Scope(term)` (RAII `using var`,
   identity + fuel backstop) on every shared walker. Safe here because these walkers re-descend into
   DISTINCT children (never re-delegate the same node — the codegen footgun); balanced Exit keeps
   DAG/deep terms passing.

3. **`ResolveTerm` reconciliation (resolves the Decision-4 open item).** Its structural arms now
   carry the identity guard (a programmatically structural-cyclic subst value → catchable
   `CompileError`); its **VarTerm arm keeps the pre-existing return-revisited behaviour** for a
   var-name substitution cycle (Decision-1 do-not-regress). A structural cycle cannot reach it
   through the VarTerm arm alone (it must pass a guarded structural node; `visited` is copied across
   the structural boundary). This is the "inner resolve may short-circuit a self-reference to
   terminate, but an unresolvable structural cycle surfaces as `CompileError`" boundary the plan
   asked for.

4. **Positive cycle-detection coverage + fail-loud tests.** `term_traversal_probe` (InternalsVisibleTo)
   now calls the REAL walkers with a constructed self-referential `StructTerm` and asserts a catchable
   `CompileError`, plus deep(6000)/DAG no-false-positive. `deep_acyclic.glp`/`dag_shared.glp` added;
   Section T-3/T-4 FAIL LOUD on a missing fixture/probe (a silent skip is what hid defect #1).

**Deliberate boundary (not a defect):** `ApplySubstitutionToGoal`'s goal-NESTING recursion is a
Goal-graph, not a Term walk; a cyclic Goal graph is the linker `ResolveGoal` family's concern (guarded
via `EnterGoal`/`ExitGoal`). The var-name `active` set on `ApplySubstitution` and the identity `Scope`
guard coexist (they catch disjoint cycle classes; whichever trips first raises the same diagnostic).
