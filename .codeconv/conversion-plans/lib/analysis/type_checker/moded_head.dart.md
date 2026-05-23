---
path: lib/analysis/type_checker/moded_head.dart
cycle_group_id: 14
scc_siblings: []
generated_at: 2026-05-21T15:12:00Z
source_sha256: 8e1cf1a9af1ccc77174921ef4c2df7845bce7406fc3930b69d791cd8f087d4e2
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/moded_head.dart

## 1. Source Analysis

`moded_head.dart` is a 454-line pure compiler-analysis library implementing
Definition 5.5 (moded head construction) of the GLP paper (per `docs/type
system/moded-head.md v0.8`). It is **stateless except for one library-private
mutable `int _anonVarCounter`** used to generate fresh anonymous-variable
names of the shape `_#1, _#2, …` within a single clause's scope. Inventory of
top-level declarations (in source order):

1. **`int _anonVarCounter = 0;`** (line 22) — library-private mutable counter.
2. **`void resetAnonVarCounter()`** (lines 26–28) — public, zero-arg, sets
   counter to 0; called by `modedHead` at clause start.
3. **`String _freshAnonVarName()`** (lines 32–35) — private; post-increments
   counter then returns the interpolated string `'_#$_anonVarCounter'`.
4. **`ModedTerm modedHead(ast.Goal head, ProcDecl decl, {TypeEnvironment? typeEnv})`**
   (lines 60–75) — public entry point for clause heads: resets the counter,
   validates arity (throws `ArityMismatchError`), builds the I/O moded term
   with parent mode `consume`, then applies the unconditional variable flip
   via `_ensureVariablesMatchModes`.
5. **`ModedTerm producedTerm(ast.Goal atom, ProcDecl decl, {TypeEnvironment? typeEnv})`**
   (lines 94–106) — public entry point for body atoms: NO counter reset
   (load-bearing — body atoms share the head's anon-var namespace), validates
   arity, builds the I/O moded term with parent mode `produce`, and does NOT
   flip variables (load-bearing — body atoms preserve reader/writer roles).
6. **`ModedTerm _buildIOModedTerm(ast.Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv)`**
   (lines 118–130) — private; iterates `term.args` by index, computing per-arg
   mode (`decl.isInputArg(i) ? Mode.consume : Mode.produce`) and recursing
   into `_buildModedSubterm`. Allocates a `ModedCompound(parentMode, functor,
   arity, modedArgs)`.
7. **`ModedTerm _buildModedSubterm(ast.Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv)`**
   (lines 142–198) — private; **wildcard fast-path guard** runs FIRST
   (`expectedType == null || expectedType is PrimitiveModeAlt` →
   `_buildOpaqueModedTerm`); then chained `is`-checks dispatch on `VarTerm`,
   `StructTerm` (uses `_getSubtermModes`), `ListTerm` (nil → `ModedConstant.nil`;
   non-nil → `_getListSubtermModes` + `ModedCompound.listCons`), `ConstTerm`,
   `UnderscoreTerm` (allocates fresh anon name via `_freshAnonVarName`).
   Throws `InvalidHeadError` for unknown subtypes.
8. **`List<(Mode, TypeExpr?)> _getSubtermModes(String functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)`**
   (lines 207–271) — private; returns per-arg `(combinedMode, subtermType)`
   by looking up `typeEnv.getType(typeName)`, finding the matching
   `StructAlt` / `DiffListAlt`, combining embedded modes via `combineMode`,
   and dualising types when `isInput` (the `?` marker). Returns
   `defaultModes` (parent mode propagated, null types) when lookup fails.
9. **`((Mode, TypeExpr?), (Mode, TypeExpr?)) _getListSubtermModes(Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)`**
   (lines 279–329) — private; same approach as `_getSubtermModes` but
   specialised to `ListConsAlt` returning a head/tail pair-of-pairs.
10. **`Mode _getEmbeddedMode(TypeExpr expr)`** (lines 338–347) — private;
    chained `is`-test classifier returning `consume`/`produce` based on
    `isInput` flag for `TypeRef` / `PrimitiveModeAlt`; falls through to
    `Mode.produce` for any other shape.
11. **`TypeExpr _dualType(TypeExpr expr)`** (lines 353–362) — private;
    polymorphic dualisation: `TypeRef.dual()` (instance method),
    `PrimitiveModeAlt` (allocates flipped instance), fall-through returns
    `expr` unchanged.
12. **`ModedTerm _buildOpaqueModedTerm(ast.Term term, Mode mode)`** (lines
    373–405) — private; structurally identical to `_buildModedSubterm` BUT
    with no type-driven sub-mode computation — all sub-children inherit
    `mode` unchanged. Used inside the wildcard fast-path branch.
13. **`ModedTerm _ensureVariablesMatchModes(ModedTerm term)`** (lines 415–431)
    — private; recurses through `ModedCompound` (rebuilds with flipped
    inner args), returns `ModedConstant` unchanged (structural sharing),
    flips `ModedVariable` `isReader` unconditionally.
14. **`class ArityMismatchError implements Exception`** (lines 438–444) —
    public; single `final String message` field, single-arg ctor,
    `toString()` override emits `'ArityMismatchError: $message'`.
15. **`class InvalidHeadError implements Exception`** (lines 447–453) —
    same shape as `ArityMismatchError` with `'InvalidHeadError: $message'`.

**Imports.** `mode.dart` (Mode enum + `combineMode`), `moded_term.dart`
(ModedTerm + ModedCompound + ModedConstant + ModedVariable + factory
`ModedConstant.nil` / `ModedCompound.listCons`), `type_ast.dart`
(`TypeExpr`, `TypeRef`, `PrimitiveModeAlt`, `StructAlt`, `DiffListAlt`,
`ListConsAlt`, `ProcDecl`, `TypeEnvironment`), `../../compiler/ast.dart`
prefixed `as ast` (`Goal`, `Term`, `VarTerm`, `StructTerm`, `ListTerm`,
`ConstTerm`, `UnderscoreTerm`).

**No IO, no async, no concurrency, no global mutation beyond the
per-clause `_anonVarCounter`.**

## 2. Dart → C#/.NET Conversion Plan

Every construct below mirrors the ratified convspec line-by-line:

1. **Host static class.** Wrap all top-level members in
   `public static class ModedHead` in namespace `Glp.Analysis.TypeChecker`
   (cached: `rf-csharp-static-class-no-toplevel-members`).
2. **Counter field** → `private static int _anonVarCounter = 0;` (cached:
   `rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field`).
   No `volatile`, no `Interlocked` — single-threaded contract preserved
   verbatim.
3. **Public reset** → `public static void ResetAnonVarCounter() {
   _anonVarCounter = 0; }`.
4. **Private fresh-name generator** →
   `private static string FreshAnonVarName() { _anonVarCounter++; return $"_#{_anonVarCounter}"; }`
   (cached: `rf-csharp-interpolated-string-equivalent-to-dart-interpolation`).
5. **`modedHead` → `Build`.** Public static method
   `public static ModedTerm Build(Goal head, ProcDecl decl, TypeEnvironment? typeEnv = null)`.
   Calls `ResetAnonVarCounter()`, throws `new ArityMismatchError($"Head arity {head.Arity} does not match declaration arity {decl.Arity}")`
   on arity mismatch, then `var ioTerm = BuildIOModedTerm(head, decl, Mode.Consume, typeEnv); return EnsureVariablesMatchModes(ioTerm);`.
   Named-optional parameter mapping is cached
   (`rf-dart-named-required-params-to-csharp-named-positional`).
6. **`producedTerm` → `ProducedTerm`.** Public static method
   `public static ModedTerm ProducedTerm(Goal atom, ProcDecl decl, TypeEnvironment? typeEnv = null)`.
   NO `ResetAnonVarCounter()` call, NO `EnsureVariablesMatchModes` call,
   root mode `Mode.Produce`. Source comment preserved as
   `// NOTE: do NOT reset the counter here — body atoms share the clause's anon-var namespace with the head`.
7. **`_buildIOModedTerm` → `BuildIOModedTerm`.** Private static, signature
   `private static ModedTerm BuildIOModedTerm(Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv)`.
   Pre-sized list `var modedArgs = new List<ModedTerm>(term.Args.Count);`,
   C-style `for (int i = 0; i < term.Args.Count; i++)` (NOT LINQ — three
   parallel index-aligned reads), ternary
   `decl.IsInputArg(i) ? Mode.Consume : Mode.Produce`. Final
   `return new ModedCompound(parentMode, term.Functor, term.Arity, modedArgs);`.
8. **`_buildModedSubterm` → `BuildModedSubterm`.** Private static; wildcard
   fast-path guard FIRST:
   `if (expectedType is null or PrimitiveModeAlt) return BuildOpaqueModedTerm(term, mode);`.
   Then type-pattern switch expression
   (cached: `rf-dart-extension-is-as-to-csharp-type-pattern-switch`):
   ```csharp
   return term switch {
       VarTerm v => new ModedVariable(v.Name, isReader: v.IsReader, structuralMode: mode),
       StructTerm s => /* lookup _getSubtermModes, recurse, wrap in ModedCompound */,
       ListTerm l when l.IsNil => ModedConstant.Nil(mode),
       ListTerm l => /* _getListSubtermModes; recurse head + tail; ModedCompound.ListCons */,
       ConstTerm c => new ModedConstant(mode, c.Value ?? "null"),
       UnderscoreTerm _ => new ModedVariable(FreshAnonVarName(), isReader: false, structuralMode: mode),
       _ => throw new InvalidHeadError($"Unknown term type: {term.GetType().Name}")
   };
   ```
   `c.Value ?? "null"` preserves null-coalesce semantics
   (cached: `rf-dart-csharp-null-aware-call-operator-identical`).
9. **`_getSubtermModes` → `GetSubtermModes`.** Return type
   `List<(Mode Mode, TypeExpr? Type)>` (named tuple elements;
   cached: `rf-dart3-record-to-csharp-valuetuple`). Pre-build defaults via
   pre-sized for-loop (preferred over `Enumerable.Repeat` — matches Dart
   `List.generate` eager semantics). Type-pattern `if (expectedType is
   TypeRef tr) { typeName = tr.Name; isDual = tr.IsInput; }`. Iterate
   `typeDef.Alternatives`; match `StructAlt` (functor + arity equal) →
   compute `combinedMode = CombineMode(parentMode, GetEmbeddedMode(argType))`
   and `subtermType = isDual ? DualType(argType) : argType`; match
   `DiffListAlt` (functor == "\\" and arity == 2) → produce two tuples for
   content + hole.
10. **`_getListSubtermModes` → `GetListSubtermModes`.** Return type
    `((Mode Mode, TypeExpr? Type) Head, (Mode Mode, TypeExpr? Type) Tail)`
    — named outer + inner tuple elements (cached:
    `rf-dart3-record-to-csharp-valuetuple`). Iterates
    `typeDef.Alternatives` searching for `ListConsAlt`; on match returns
    `((headMode, headType), (tailMode, tailType))` with same `combineMode`
    / `DualType` logic.
11. **`_getEmbeddedMode` → `GetEmbeddedMode`.** Expression-bodied switch:
    ```csharp
    private static Mode GetEmbeddedMode(TypeExpr expr) => expr switch {
        TypeRef tr => tr.IsInput ? Mode.Consume : Mode.Produce,
        PrimitiveModeAlt pma => pma.IsInput ? Mode.Consume : Mode.Produce,
        _ => Mode.Produce
    };
    ```
    Default-arm intentionally falls through to `Mode.Produce` (preserves
    Dart design intent — NOT a throwing arm).
12. **`_dualType` → `DualType`.** Expression-bodied switch:
    ```csharp
    private static TypeExpr DualType(TypeExpr expr) => expr switch {
        TypeRef tr => tr.Dual(),
        PrimitiveModeAlt pma => new PrimitiveModeAlt(!pma.IsInput, pma.Line, pma.Column),
        _ => expr
    };
    ```
13. **`_buildOpaqueModedTerm` → `BuildOpaqueModedTerm`.** Private static;
    type-pattern switch identical in structure to `BuildModedSubterm` but
    with NO type-driven sub-mode computation — every recursive call passes
    the SAME `mode`:
    ```csharp
    return term switch {
        VarTerm v => new ModedVariable(v.Name, isReader: v.IsReader, structuralMode: mode),
        ConstTerm c => new ModedConstant(mode, c.Value ?? "null"),
        UnderscoreTerm _ => new ModedVariable(FreshAnonVarName(), isReader: false, structuralMode: mode),
        ListTerm l when l.IsNil => ModedConstant.Nil(mode),
        ListTerm l => ModedCompound.ListCons(mode, BuildOpaqueModedTerm(l.Head!, mode), BuildOpaqueModedTerm(l.Tail!, mode)),
        StructTerm s => new ModedCompound(mode, s.Functor, s.Arity, s.Args.Select(arg => BuildOpaqueModedTerm(arg, mode)).ToList()),
        _ => throw new InvalidHeadError($"Unknown term type in opaque context: {term.GetType().Name}")
    };
    ```
    (cached: `rf-dart-list-map-tolist-to-csharp-linq-select-tolist`,
    `rf-dart-factory-ctor-const-default-to-csharp-static-factory`).
14. **`_ensureVariablesMatchModes` → `EnsureVariablesMatchModes`.** Private
    static; switch expression over the three `ModedTerm` subtypes:
    ```csharp
    return term switch {
        ModedCompound c => new ModedCompound(c.Mode, c.Functor, c.Arity,
            c.Args.Select(EnsureVariablesMatchModes).ToList()),
        ModedConstant k => k, // structural sharing — no-op
        ModedVariable v => new ModedVariable(v.Name, isReader: !v.IsReader, structuralMode: v.Mode),
        _ => throw new InvalidHeadError($"Unknown moded term type: {term.GetType().Name}")
    };
    ```
    Method-group `Select(EnsureVariablesMatchModes)` (NOT lambda) per
    Microsoft Learn idiom.
15. **`ArityMismatchError` / `InvalidHeadError`** → both as
    `public sealed class … : Exception` with single-string ctor
    `: base(message)` and `public override string ToString() =>
    $"{nameof(…)}: {Message}";` override matching Dart format (cached:
    `rf-dart-error-vs-exception-to-csharp-exception`). Dart `Error` suffix
    preserved verbatim per file-spec policy.

## 3. Decomposed Task Units

- T1. Create file skeleton — namespace `Glp.Analysis.TypeChecker`, `using` directives for `System`, `System.Collections.Generic`, `System.Linq`, plus the target-side analogues of `mode.cs`, `moded_term.cs`, `type_ast.cs`, `compiler/ast.cs`.
- T2. Open `public static class ModedHead { }` as the host for all top-level members.
- T3. Emit `private static int _anonVarCounter = 0;` field.
- T4. Emit `public static void ResetAnonVarCounter()` resetting the counter to 0.
- T5. Emit `private static string FreshAnonVarName()` that post-increments the counter and returns `$"_#{_anonVarCounter}"`.
- T6. Emit `public static ModedTerm Build(Goal head, ProcDecl decl, TypeEnvironment? typeEnv = null)` with the reset → arity check → BuildIOModedTerm(Mode.Consume) → EnsureVariablesMatchModes pipeline.
- T7. Emit `public static ModedTerm ProducedTerm(Goal atom, ProcDecl decl, TypeEnvironment? typeEnv = null)` with the NO-reset / Mode.Produce / NO-flip variant and the load-bearing comment.
- T8. Emit `private static ModedTerm BuildIOModedTerm(Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv)` with pre-sized list + C-style index loop + ternary per-arg mode + ModedCompound allocation.
- T9. Emit `private static ModedTerm BuildModedSubterm(Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv)` with the wildcard guard FIRST then type-pattern switch over VarTerm / StructTerm / ListTerm (nil + non-nil) / ConstTerm / UnderscoreTerm with throwing default.
- T10. Emit `private static List<(Mode Mode, TypeExpr? Type)> GetSubtermModes(string functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)` with type-driven combineMode + DualType branching and StructAlt + DiffListAlt handling.
- T11. Emit `private static ((Mode Mode, TypeExpr? Type) Head, (Mode Mode, TypeExpr? Type) Tail) GetListSubtermModes(Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)` with ListConsAlt match returning nested tuple.
- T12. Emit `private static Mode GetEmbeddedMode(TypeExpr expr)` expression-bodied switch with TypeRef / PrimitiveModeAlt arms + Mode.Produce fall-through.
- T13. Emit `private static TypeExpr DualType(TypeExpr expr)` expression-bodied switch with TypeRef.Dual() / PrimitiveModeAlt-ctor-flip / identity fall-through.
- T14. Emit `private static ModedTerm BuildOpaqueModedTerm(Term term, Mode mode)` with uniform-mode-inheritance type-pattern switch and throwing default.
- T15. Emit `private static ModedTerm EnsureVariablesMatchModes(ModedTerm term)` switch with ModedCompound (recurse via method group) + ModedConstant structural-sharing arm + ModedVariable unconditional flip + throwing default.
- T16. Emit `public sealed class ArityMismatchError : Exception` with `(string message) : base(message)` ctor and `ToString()` override matching Dart format.
- T17. Emit `public sealed class InvalidHeadError : Exception` with the same shape.
- T18. Wire `target_code_unit` path `lib/analysis/type_checker/moded_head.cs` and stamp tombstone `status: planned`.

## 4. Research Findings

none required — every construct's `research_finding_id` is a ratified cache
reuse from already-specced sibling files in this same directory
(`moded_term.dart`, `type_ast.dart`, `program_dfa.dart`,
`clause_validation.dart`, `mode.dart`, `prelude.dart`) per FR-024, with the
single new finding
(`rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field`)
already recorded in the convspec's Rationale & Research Provenance section
against authoritative Microsoft Learn citations.

## 5. Consistency Pass

- T1 (file skeleton + namespace) — fixed — derived from convspec
  `conversion_units` line 1 (`static class ModedHead in namespace
  Glp.Analysis.TypeChecker`).
- T2 (host static class) — fixed — derived from convspec
  `rf-csharp-static-class-no-toplevel-members` and `target_decision` of
  `dart.public_toplevel_fn.head_arity_validate_and_two_step_build`.
- T3 (counter field) — fixed — derived from convspec
  `dart.toplevel_mutable_int.fresh_name_counter_per_clause` and new finding
  `rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field`.
- T4 (`ResetAnonVarCounter`) — fixed — derived from convspec same construct.
- T5 (`FreshAnonVarName`) — fixed — derived from convspec same construct +
  `rf-csharp-interpolated-string-equivalent-to-dart-interpolation` cache.
- T6 (`Build`) — fixed — derived from convspec
  `dart.public_toplevel_fn.head_arity_validate_and_two_step_build`,
  including the named-optional parameter mapping
  (`rf-dart-named-required-params-to-csharp-named-positional`).
- T7 (`ProducedTerm`) — fixed — derived from convspec
  `dart.public_toplevel_fn.body_atom_no_flip_no_counter_reset` (three
  load-bearing semantic deltas preserved verbatim).
- T8 (`BuildIOModedTerm`) — fixed — derived from convspec
  `dart.private_toplevel_fn.io_moded_term_builder_per_arg_mode` (C-style
  index loop, NOT LINQ, with pre-sized list capacity hint).
- T9 (`BuildModedSubterm`) — fixed — derived from convspec
  `dart.private_recursive_dispatch.ast_term_subtype_switch_with_wildcard_fast_path`
  (wildcard guard ordering preserved as the load-bearing nuance).
- T10 (`GetSubtermModes`) — fixed — derived from convspec
  `dart3.record_return_type.list_of_two_field_anonymous_tuple` and
  `rf-dart3-record-to-csharp-valuetuple` cache (named tuple elements).
- T11 (`GetListSubtermModes`) — fixed — derived from convspec
  `dart3.record_return_type.nested_pair_of_pairs` (nested named ValueTuple).
- T12 (`GetEmbeddedMode`) — fixed — derived from convspec
  `dart.private_pure_classifier.mode_of_typeexpr` (default-arm intentionally
  `Mode.Produce`, not throwing).
- T13 (`DualType`) — fixed — derived from convspec
  `dart.private_pure_classifier.dual_of_typeexpr_polymorphic` (polymorphic
  dispatch preserved).
- T14 (`BuildOpaqueModedTerm`) — fixed — derived from convspec
  `dart.private_recursive_dispatch.opaque_inherits_mode_uniformly`
  (separate method, NOT factored with `BuildModedSubterm` via a flag).
- T15 (`EnsureVariablesMatchModes`) — fixed — derived from convspec
  `dart.private_recursive_dispatch.unconditional_variable_complement`
  (structural-sharing ModedConstant arm + method-group conversion).
- T16 + T17 (error classes) — fixed — derived from convspec
  `dart.exception_class.recoverable_signal_implements_exception_with_message`
  and `rf-dart-error-vs-exception-to-csharp-exception` cache.
- T18 (tombstone wiring) — fixed — derived from tombstone front-matter
  `target_path: lib/analysis/type_checker/moded_head.cs`.

## 6. Escalations

None.
