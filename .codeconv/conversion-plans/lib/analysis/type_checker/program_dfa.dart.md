---
path: lib/analysis/type_checker/program_dfa.dart
cycle_group_id: 6
scc_siblings: []
generated_at: 2026-05-21T14:46:48Z
source_sha256: bf0151e2d78f26961d8153beede8211ba2f823b127de7ec7fd673299658a6057
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/program_dfa.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/analysis/type_checker/program_dfa.dart` (659 lines, sha256 `bf0151e2…58a6057`). Imports: `type_ast.dart` (sibling AST module — `TypeEnvironment`, `TypeDef`, `TypeExpr` + its sealed leaves `ConstantAlt`, `ListNilAlt`, `ListConsAlt`, `StructAlt`, `DiffListAlt`, `PrimitiveModeAlt`, `TypeRef`, plus `ProcDecl`) and `mode.dart` (`Mode` enum + `Mode.produce`/`Mode.consume` aliases + `flip` getter).

Public surface (each independently emit-able C# unit):

1. `class DFAState` (lines 17–87) — four `final` fields (`baseName: String`, `isDual: bool`, `isFinal: bool`, `isProcedure: bool` defaulted false); two synthetic getters (`name` building the `baseName`/`baseName?` form; `dual` allocating a fresh state with `!isDual`); **eleven** boolean classifier getters (`isWildcard`, `isProducedWildcard`, `isConsumedWildcard`, `isIntegerType`, `isRealType`, `isNumberType`, `isStringType`, `isAnonymousFinal`, `isNumericType`, `isPrimitiveType`, `isUserDefinedType`); overridden `toString`, `==`, `hashCode`. Equality keys ONLY `baseName + isDual` (intentional partial-field identity).

2. `class TransitionLabel` (lines 96–137) — four fields (`symbol: String`, `arity: int`, `argIndex: int`, `mode: Mode?` nullable); ONE private generative ctor `_(...)`; TWO factory ctors `functor` (named-mode optional) + `constant` (calls `value.toString()`, arity=0, argIndex=0, mode=null); `dual` getter using null-aware `mode?.flip`; overridden `toString` (constant-fast-path + Unicode arrow modeStr `:↑`/`:↓`); overridden `==` / `hashCode` on ALL FOUR fields including the nullable `Mode?`.

3. `class Automaton` (lines 140–170) — three fields (`startState: DFAState`, `_transitions: Map<(DFAState, TransitionLabel), DFAState>` private, `acceptedPrimitives: Set<String>` with `const {}` default); ctor; `transition(from, label)` returning nullable lookup `_transitions[(from, label)]`; `transitions` getter returning the internal map by reference (Dart-comment-enforced read-only); `dual` getter building a fresh dictionary with dualized keys-and-values via `_transitions.entries` iteration.

4. `class ProgramDFA` (lines 173–196) — two `final Map<String, T>` fields (`states`, `automata`); two getters `getState(name)` / `getAutomaton(typeName)` that throw `StateError` on miss with interpolated message.

5. `class UnknownTypeError extends Error` (lines 199–205) — one `typeName` field, overridden `toString => 'UnknownTypeError: $typeName'`.

6. Top-level `buildProgramDFA(env)` (lines 210–278) — **four-phase imperative driver**: (a) seed system states + automata for `_`/`_?`/`Integer`/`Integer?`/`Real`/`Real?`/`Number`/`Number?`/`String`/`String?`/`_FINAL_`; (b) FIRST PASS over `env.types.entries` creating both `T` and `T?` states for each defined type; (c) SECOND PASS over `env.types.entries` building both `T` and `T?` automata via `_buildTypeAutomaton`; (d) procedure states + procedure automata over `env.procedures.entries`. The two-pass discipline over the same collection is explicitly commented as load-bearing (forward type references).

7. Seven private helpers: `_finalAutomaton(state)`, `_primitiveTypeAutomaton(state, finalState)`, `_buildTypeAutomaton(typeDef, states, {required isDual})`, `_addTypeTransitions(fromState, alt, contextMode, states, transitions, isDual)` (5-branch `if (alt is …)` chain over the closed AST sum, intentionally OMITTING `PrimitiveModeAlt` — comment line 380), `_resolveTypeExpr(typeExpr, states, isDual)` (handles `PrimitiveModeAlt` and `TypeRef`, throws `StateError`/`UnknownTypeError`), `_modeOf(typeExpr, contextMode)` (flips for input mode), `_buildProcedureAutomaton(procDecl, states)`, `_getFullTypeName(typeExpr)`.

8. `class LeafTerm` (lines 479–529) — eight fields, ONE private generative ctor, SIX public factory ctors (`writer`, `reader`, `integerConstant`, `realConstant`, `stringConstant`, `constant`). No `==`/`hashCode` overrides → Dart reference identity preserved.

9. `class LeafConsistencyResult` (lines 532–544) — three fields (`isConsistent: bool`, `type: DFAState?`, `reason: String?`), TWO factory ctors (`consistent(type)` / `inconsistent(reason)`).

10. Top-level `checkLeafConsistency(leaf, state, dfa)` (lines 551–658) — multi-branch dispatch: (1) variable mode check (reader↔consume, writer↔produce); (2a) anonymous-final fast-path; (2b) cascading `if (state.isXType)` ladder for Integer/Real/Number/String primitive states with appropriate literal-kind checks; (2c) wildcard-mode check; (2d) user-defined-type lookup via `automaton.transition(state, constLabel)` + `acceptedPrimitives` fallback for matching constant kinds.

Side-effect surface: zero I/O, zero global mutation, pure construction over the input `TypeEnvironment`. Concurrency: not thread-aware (consistent with the heap-fcp single-owning-context project policy). Stack discipline: `_resolveTypeExpr` is not recursive but is called recursively from `_addTypeTransitions` along each `TypeExpr` constructor arm — recursion depth bounded by AST depth.

## 2. Dart → C#/.NET Conversion Plan

Per the ratified convspec (17 constructs + 11 cached/new findings), each Dart construct maps to its C# counterpart as follows. Mirroring the spec's `target_decision` per `construct_key`:

→ **`dart.value_class.manual_eq_hashcode_two_field_state` (DFAState class)** → `sealed class DFAState : IEquatable<DFAState>` with four read-only auto-properties (`BaseName`, `IsDual`, `IsFinal`, `IsProcedure`); single public ctor `DFAState(string baseName, bool isDual, bool isFinal, bool isProcedure = false)`; hand-written `Equals(object?)` / `Equals(DFAState?)` / `GetHashCode()` keying ONLY on `BaseName` (ordinal) + `IsDual` — `IsFinal` and `IsProcedure` are intentionally excluded to mirror Dart's partial equality verbatim. `HashCode.Combine(BaseName, IsDual)`. `record` is rejected (would silently widen equality to all four fields per rf-csharp-record-uses-all-members-equality).

→ **`dart.value_class.derived_getter_returns_new_instance` (DFAState.dual)** → `public DFAState Dual => new DFAState(BaseName, !IsDual, IsFinal, IsProcedure);` as an expression-bodied read-only property (NOT a method) — preserves the Dart getter shape and pure-allocation contract.

→ **`dart.value_class.cluster_of_boolean_classifier_getters` (eleven `Is*` getters on DFAState)** → eleven expression-bodied `public bool IsXxx => …` properties. Literal-token comparisons use C# `string ==` (documented ordinal). The composite getters (`IsNumericType`, `IsPrimitiveType`, `IsUserDefinedType`) compose the leaves with the exact short-circuit order from Dart. `Name` getter: `public string Name => IsDual ? $"{BaseName}?" : BaseName;`.

→ **`dart.value_class.private_named_factory_constructors` (TransitionLabel)** → `sealed class TransitionLabel : IEquatable<TransitionLabel>` with four read-only auto-properties (`Symbol: string`, `Arity: int`, `ArgIndex: int`, `Mode: Mode?`); `private TransitionLabel(string symbol, int arity, int argIndex, Mode? mode)` generative ctor; two `public static TransitionLabel Functor(string name, int arity, int argIndex, Mode? mode = null)` and `public static TransitionLabel Constant(object value)` static factory methods. `Constant` invokes `value?.ToString() ?? ""` (defensive against C# annotated `ToString` returning null where Dart `Object.toString` is total). Property name `Mode` collides with the enum type name; this is resolved by C# member-access rules (`label.Mode` is the property, `Mode.Produce` is enum-qualified).

→ **`dart.value_class.full_field_equality_with_nullable_enum` (TransitionLabel)** → hand-written `Equals(object?)` / `Equals(TransitionLabel?)` comparing all four fields with lifted nullable-enum equality on `Mode?` (per rf-csharp-nullable-value-type-lifted-equality); `GetHashCode()` via `HashCode.Combine(Symbol, Arity, ArgIndex, Mode)`. `IEquatable<TransitionLabel>` implemented so dictionary lookups avoid boxing on the keyed-tuple's component path.

→ **`dart.value_class.derived_property_flipping_optional_field` (TransitionLabel.dual)** → `public TransitionLabel Dual => new TransitionLabel(Symbol, Arity, ArgIndex, Mode?.Flip());` — `?.` operator is token-and-semantics-identical between Dart and C# (rf-dart-csharp-null-aware-call-operator-identical).

→ **`dart.tostring.conditional_format_via_interpolation` (DFAState.toString + TransitionLabel.toString)** → both as `public override string ToString()`. DFAState: expression-bodied ternary. TransitionLabel: statement body with constant-arity fast-path + ternary for modeStr + interpolated final string `$"{Symbol}({Arity},{ArgIndex}){modeStr}"`. Unicode `↑` (U+2191) / `↓` (U+2193) preserved verbatim in UTF-8-with-BOM source. `Mode.produce` Dart alias → `ModeAliases.Produce` per mode.dart convspec (preserves source intent vs raw enum constant).

→ **`dart.value_class.tuple_record_key_in_map` (Automaton._transitions, _addTypeTransitions's transitions)** → C# native value tuples `(DFAState From, TransitionLabel Label)` as `Dictionary<(DFAState, TransitionLabel), DFAState>` keys. Element-wise equality delegates to each component's `Equals` — exactly the hand-written DFAState (BaseName+IsDual) and TransitionLabel (all four fields) overrides.

→ **`dart.collection.map_dict_clone_with_transformed_keys_and_values` (Automaton.dual)** → expression-and-statement body: pre-size `var newTransitions = new Dictionary<(DFAState, TransitionLabel), DFAState>(_transitions.Count);`, then `foreach (var entry in _transitions) { var (fromState, label) = entry.Key; newTransitions[(fromState.Dual, label.Dual)] = entry.Value.Dual; }`, then `return new Automaton(StartState.Dual, newTransitions, AcceptedPrimitives);`. Imperative loop chosen over LINQ `ToDictionary` (lower allocation, matches Dart shape, codebase convention).

→ **`dart.collection.map_indexer_nullable_lookup` (Automaton.transition)** → `public DFAState? Transition(DFAState from, TransitionLabel label) => _transitions.TryGetValue((from, label), out var to) ? to : null;`. This is the single highest-impact behavioural mismatch (Dart `Map[k]` returns null on miss; C# `Dictionary[k]` throws `KeyNotFoundException`).

→ **`dart.collection.unmodifiable_view_getter_returning_internal_map` (Automaton.transitions)** → `public IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState> Transitions => _transitions;`. Same instance exposed through a narrower interface — Dart's comment-level read-only contract promoted to type-level. No defensive copy.

→ **`dart.collection.immutable_set_field_with_const_default` (Automaton.acceptedPrimitives)** → `private readonly IReadOnlySet<string> _acceptedPrimitives;` with public read-only property view; ctor parameter `IReadOnlySet<string>? acceptedPrimitives = null` defaulting to `FrozenSet<string>.Empty` (NOT a shared mutable `HashSet`). Element comparer `StringComparer.Ordinal` at the upstream construction site.

→ **`dart.collection.factory_seeded_mutable_string_dictionary` (ProgramDFA + buildProgramDFA's local maps)** → `sealed class ProgramDFA` with two `private readonly Dictionary<string, T>` fields aliased from the caller. `GetState` / `GetAutomaton` use `TryGetValue` + explicit `throw new InvalidOperationException($"State not found: {name}");` (StateError → InvalidOperationException). All dictionary instances in `buildProgramDFA` constructed with `new Dictionary<string, T>(StringComparer.Ordinal)` per project convention.

→ **`dart.error_subclass.named_typed_error` (UnknownTypeError)** → `public sealed class UnknownTypeException : Exception { public string TypeName { get; } public UnknownTypeException(string typeName) : base($"UnknownTypeError: {typeName}") { TypeName = typeName; } }`. Class rename per BCL `…Exception` suffix convention. Message text preserved verbatim via base ctor.

→ **`dart.toplevel.driver_function_with_imperative_phase_loops` (buildProgramDFA)** → `public static ProgramDFA Build(TypeEnvironment env)` on `static class ProgramDfaBuilder` (or as a static factory on `ProgramDFA` itself — final placement is a codegen detail). Body preserves the four-phase structure verbatim; the two-pass discipline over `env.Types` (states-first-then-automata) is load-bearing for forward type references — explicitly preserved with the source comment. `foreach (var (typeName, typeDef) in env.Types)` for KeyValuePair deconstruction.

→ **`dart.helper.private_top_level_constructor_functions` (seven private helpers)** → seven `private static` methods on `ProgramDfaBuilder`. Each preserves its Dart positional+named parameter shape; the `required` Dart named-arg `isDual` becomes a non-defaulted C# parameter passed by name at call sites (`BuildTypeAutomaton(td, states, isDual: false)`).

→ **`dart.controlflow.if_else_type_pattern_chain_on_sealed_sum` (_addTypeTransitions, _resolveTypeExpr)** → C# *type-pattern* `switch` statement with declaration patterns (`case ConstantAlt c: …; break;`). `_addTypeTransitions`: explicit no-op `default:` arm with the source comment "PrimitiveModeAlt is handled in resolveTypeExpr — it's a leaf, not a constructor" — DO NOT throw on default (intentional source asymmetry). `_resolveTypeExpr`: handles `PrimitiveModeAlt` and `TypeRef`, default arm throws `InvalidOperationException` (true programmer error per rf-dart-error-vs-exception-to-csharp-exception).

→ **`dart.controlflow.xor_via_boolean_inequality` (_resolveTypeExpr)** → `bool baseIsComplement = typeExpr.IsInput; bool finalIsComplement = baseIsComplement != isDual; // XOR` — verbatim, comment preserved, NOT rewritten to `^`.

→ **LeafTerm (eight fields + six factories)** → `sealed class LeafTerm` with read-only auto-properties; one `private LeafTerm(...)` generative ctor; six `public static LeafTerm Writer/Reader/IntegerConstant/RealConstant/StringConstant/Constant(...)` expression-bodied factory methods. NO `IEquatable`, NO `record` — Dart preserves reference identity (no `==` override), C# preserves the same. Nullable fields `string? Name`, `Mode? Mode`, `object? Value`.

→ **LeafConsistencyResult (three fields + two factories)** → `sealed class LeafConsistencyResult` with `IsConsistent`, `Type: DFAState?`, `Reason: string?`; two `public static` factory methods `Consistent(DFAState type)` / `Inconsistent(string reason)`.

→ **checkLeafConsistency (top-level dispatch checker)** → `public static LeafConsistencyResult CheckLeafConsistency(LeafTerm leaf, DFAState state, ProgramDFA dfa)` on the same static host class. Structure preserved verbatim — the order of checks (variable-first → anonymous-final → primitive-type ladder → wildcard → user-defined) encodes the dispatch precedence. The cascading `if (state.IsXType)` ladder is an ordinary `if/else if` chain over boolean properties (no type-pattern needed — they ARE properties). `automaton.transition(state, constLabel)` invocation routes through the `TryGetValue`-based `Transition` defined above.

## 3. Decomposed Task Units

- T1: Emit `sealed class DFAState : IEquatable<DFAState>` with four read-only auto-properties, single ctor, `Name`/`Dual` properties, eleven `Is*` classifier properties, `ToString`, and hand-written `Equals`/`GetHashCode` on `BaseName+IsDual` only.
- T2: Emit `sealed class TransitionLabel : IEquatable<TransitionLabel>` with four auto-properties, private generative ctor, two static `Functor`/`Constant` factory methods, `Dual` property using `Mode?.Flip()`, `ToString` with arrow modeStr, hand-written `Equals`/`GetHashCode` on all four fields.
- T3: Emit `sealed class Automaton` with `StartState`, `_transitions` Dictionary, `_acceptedPrimitives` IReadOnlySet (default `FrozenSet<string>.Empty`); ctor; `Transition` method via `TryGetValue`; `Transitions` `IReadOnlyDictionary` property; `Dual` property building a fresh pre-sized dictionary.
- T4: Emit `sealed class ProgramDFA` with `_states`/`_automata` Dictionary fields, ctor, `GetState`/`GetAutomaton` via `TryGetValue` + explicit `InvalidOperationException` throw.
- T5: Emit `sealed class UnknownTypeException : Exception` with `TypeName` property, ctor passing `$"UnknownTypeError: {typeName}"` to base.
- T6: Emit `sealed class LeafTerm` with eight read-only nullable-aware auto-properties, private generative ctor, six static factory methods (`Writer`, `Reader`, `IntegerConstant`, `RealConstant`, `StringConstant`, `Constant`); no `IEquatable`.
- T7: Emit `sealed class LeafConsistencyResult` with three fields, two static factory methods `Consistent(state)` / `Inconsistent(reason)`.
- T8: Emit `static class ProgramDfaBuilder` with public static `Build(TypeEnvironment env)` preserving the four-phase imperative structure (system seed → defined-type states pass → defined-type automata pass → procedure states+automata); dictionaries constructed with `StringComparer.Ordinal`.
- T9: Emit seven `private static` helpers on `ProgramDfaBuilder`: `FinalAutomaton`, `PrimitiveTypeAutomaton`, `BuildTypeAutomaton` (with named-arg `isDual`), `AddTypeTransitions` (type-pattern switch with explicit no-op default + source comment for the PrimitiveModeAlt omission), `ResolveTypeExpr` (type-pattern switch with throwing default), `ModeOf`, `BuildProcedureAutomaton`, `GetFullTypeName`.
- T10: Emit `public static LeafConsistencyResult CheckLeafConsistency(LeafTerm leaf, DFAState state, ProgramDFA dfa)` preserving the variable-first/anonymous-final/primitive-ladder/wildcard/user-defined dispatch order.
- T11: Codegen-stage policy: file saved as UTF-8 (with BOM or `<auto-generated>` declaration) to preserve `↑` (U+2191) / `↓` (U+2193) arrows in `TransitionLabel.ToString`.

## 4. Research Findings

none required — all research is already cached in the ratified convspec (eleven findings: rf-csharp-record-uses-all-members-equality, rf-csharp-property-vs-method-pure-getter, rf-csharp-string-equality-ordinal-by-default, rf-csharp-nullable-value-type-lifted-equality, rf-dart-csharp-null-aware-call-operator-identical, rf-csharp-interpolated-string-equivalent-to-dart-interpolation, rf-dart3-record-to-csharp-valuetuple, rf-csharp-dictionary-foreach-iteration-keyvaluepair, rf-csharp-dictionary-indexer-throws-vs-trygetvalue, rf-csharp-ireadonlydictionary-narrowed-public-view, rf-csharp-dictionary-stringcomparer-ordinal-discipline, rf-dart-error-vs-exception-to-csharp-exception, plus four cached cross-file findings re-used: rf-dart-extension-is-as-to-csharp-type-pattern-switch / rf-dart-factory-ctor-const-default-to-csharp-static-factory / rf-dart-const-set-to-csharp-frozenset-ordinal / rf-csharp-static-class-no-toplevel-members / rf-csharp-boolean-equality-operators-trivial / rf-csharp-private-vs-internal-library-helpers). Every Microsoft Learn URL and verbatim query is recorded in the convspec §"Rationale & Research Provenance".

## 5. Consistency Pass

Cross-checked the plan against (a) the ratified convspec, (b) the tombstone `dependencies`/`callers` lists, and (c) sibling-file convspecs cited as cached idiom sources.

- **Dependencies (tombstone: `mode.dart`, `type_ast.dart`)** — plan consumes both: `Mode`/`Mode?` and `ModeAliases.Produce`/`Flip()` from mode.dart (convspec-cited), and the closed AST sum + `TypeEnvironment` / `TypeDef` / `ProcDecl` from type_ast.dart (convspec-cited). All assumptions about these sibling units come from already-converted convspecs — no new commitments introduced.
- **Callers (tombstone: `subtyping.dart`, `type_checker.dart`, `well_typed_clause.dart`, `well_typed_term.dart`, two test files)** — each caller consumes `Automaton.Transition` (now nullable + non-throwing), `Automaton.Transitions` (now `IReadOnlyDictionary`), `ProgramDFA.GetState`/`GetAutomaton` (still throwing `InvalidOperationException`), and `UnknownTypeException` (rename from `UnknownTypeError`). The nullable-Transition + readonly-Transitions changes are *intentional faithful tightenings* (Dart's behaviour preserved through the C# type system); the `UnknownTypeError` → `UnknownTypeException` rename is mandated by BCL convention and propagates to all callers that catch by name — derived from rf-dart-error-vs-exception-to-csharp-exception.
- **Cached idiom re-use (FR-024)** — re-uses idioms cached in `prelude.dart` (static-class-no-toplevel), `type_ast.dart` (type-pattern switch over closed sums; static-factory for `factory` ctors; ordinal-string-keyed FrozenSet), and `mode.dart` (`ModeAliases.Produce`/`Consume` + `Flip()`). No conflict.
- **Two-pass discipline in `buildProgramDFA`** — gap "fixed — derived from source comment line 240 ('Create states for ALL defined types FIRST … Automata may reference other types, so all states must exist before building automata')" and convspec `dart.toplevel.driver_function_with_imperative_phase_loops` nuance.
- **`PrimitiveModeAlt` intentional omission in `_addTypeTransitions`** — gap "fixed — derived from source comment line 380 ('PrimitiveModeAlt is handled in resolveTypeExpr — it's a leaf, not a constructor')" and convspec `dart.controlflow.if_else_type_pattern_chain_on_sealed_sum` nuance.
- **Partial-field equality on `DFAState`** — gap "fixed — derived from convspec rf-csharp-record-uses-all-members-equality + Dart source lines 79-86 showing `==` and `hashCode` keyed ONLY on `baseName + isDual`".
- **UTF-8 source encoding for `↑`/`↓` in `TransitionLabel.ToString`** — gap "fixed — derived from convspec `dart.tostring.conditional_format_via_interpolation` nuance ('UTF-8-with-BOM or … `<auto-generated>` … encoding requirement')".

No new escalations introduced beyond what the convspec already resolved (convspec `escalations: []`). All plan steps trace back to either source-file inspection, the convspec, or cached cross-file findings — no speculation.

## 6. Escalations

None.
