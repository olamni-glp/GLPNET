# Conversion Spec — lib/analysis/type_checker/subtyping.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/subtyping.dart
source_sha256: 57b232507c21c6081aca3fcc70d7d7c850b562a16fe52abdef790d058a330224
target_code_unit: lib/analysis/type_checker/subtyping.cs
constructs:
  - construct_key: dart.toplevel_public_bool_predicate_thin_dispatch
    source_form: >-
      bool isSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa) {
      return _isSubtype(stateA, stateB, dfa, <String>{}); }
    target_decision: >-
      Emit as `public static bool IsSubtype(DFAState stateA, DFAState stateB,
      ProgramDFA dfa)` on a single host static class
      `public static class Subtyping` in namespace `Glp.Analysis.TypeChecker`
      (mirroring the file path, identical convention to
      `ClauseValidation` / `ProgramDfaBuilder`). Body is a one-statement
      delegate to the private static helper that seeds the visited set:
      `return CheckSubtype(stateA, stateB, dfa, new HashSet<string>(
      StringComparer.Ordinal));`. The Dart set literal `<String>{}` is a
      mutable empty `Set<String>`; the equivalent C# is
      `new HashSet<string>(StringComparer.Ordinal)` — ordinal comparer
      mandated because the set's elements are deterministic state-pair
      keys (`"<aName>:<bName>"`) built from `DFAState.baseName` tokens
      that are case-sensitive (e.g. `"Integer"`, `"_FINAL_"`, `"_"`),
      identical ordinal-discipline rationale to
      `dart-string-keyed-map-to-csharp-ordinal-dictionary` (cached from
      `prelude.dart` / `program_dfa.dart`). Public method PascalCased per
      .NET conventions; private helper renamed `CheckSubtype` (NOT
      `_IsSubtype`) — C# has no leading-underscore convention for
      privacy, `private` modifier is the equivalent.
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Reusing the cached idiom (FR-024 cache from
      `prelude.dart` / `clause_validation.dart` / `program_dfa.dart`).
      Dart top-level functions have no C# equivalent — every method must
      be a member of a type, so a `static class Subtyping` hosts the
      public predicate and its private recursive engine. The
      `new HashSet<string>(StringComparer.Ordinal)` seed is non-trivial:
      C# `HashSet<string>` uses `EqualityComparer<string>.Default` (which
      IS ordinal for `string`) BUT the project convention from prior
      specs is to pass `StringComparer.Ordinal` EXPLICITLY so the
      contract is reviewable in a PR diff. Reference-vs-value: `DFAState`
      / `ProgramDFA` are reference types in both languages — pass by
      reference identity in both, no boxing.
  - construct_key: dart.private_recursive_coinductive_predicate_with_visited_set
    source_form: >-
      bool _isSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa,
      Set<String> visited) { final pairKey = '${stateA.name}:${stateB.name}';
      if (visited.contains(pairKey)) return true; visited.add(pairKey);
      if (stateA == stateB) return true; assert(!stateA.isDual &&
      !stateB.isDual); if (stateB.isWildcard || stateB.isAnonymousFinal)
      return true; if (stateA.isWildcard || stateA.isAnonymousFinal)
      return false; if (stateA.isPrimitiveType || stateB.isPrimitiveType)
      { return _checkPrimitiveSubtype(stateA, stateB); } final automA =
      dfa.getAutomaton(stateA.name); final automB = dfa.getAutomaton(
      stateB.name); for (final entry in automA.transitions.entries) {
      final (fromState, label) = entry.key; if (fromState != stateA)
      continue; final targetA = entry.value; final targetB =
      automB.transition(stateB, label); if (targetB == null) return
      false; if (targetA == targetB) continue; if (!_checkTargetSubtype(
      targetA, targetB, dfa, visited)) return false; } return true; }
    target_decision: >-
      Emit as `private static bool CheckSubtype(DFAState stateA,
      DFAState stateB, ProgramDFA dfa, HashSet<string> visited)` on the
      same `Subtyping` host static class. Body preserves the Dart
      structure verbatim, in declaration order — order is load-bearing
      (coinductive memo-check FIRST, then reflexivity, then mode-shape
      assert, then wildcard top/bottom rules, then primitive lattice,
      then transition iteration). Specific mappings:
        (1) `final pairKey = '${stateA.name}:${stateB.name}';` →
            `string pairKey = $"{stateA.Name}:{stateB.Name}";` —
            interpolation idiom cached from `program_dfa.dart`. NOTE:
            `DFAState.Name` is the C# property that already encodes
            `isDual` as `"X?"` (see `program_dfa.dart` convspec —
            `Name` getter returns `IsDual ? $"{BaseName}?" : BaseName`),
            so the pair key is unambiguous across dual/non-dual states.
        (2) `if (visited.contains(pairKey)) return true; visited.add(
            pairKey);` → `if (visited.Contains(pairKey)) return true;
            visited.Add(pairKey);`. Side-effecting mutation of the
            shared `visited` set across recursive calls is preserved —
            it carries the coinductive *assumption set* downward.
        (3) `if (stateA == stateB) return true;` →
            `if (stateA.Equals(stateB)) return true;` — Dart `==` on
            `DFAState` invokes the overridden operator (value equality
            on `baseName + isDual`); C# `==` on reference type
            `DFAState` would default to reference equality unless an
            `operator ==` is defined (it is NOT in the
            `program_dfa.dart` spec — only `IEquatable<DFAState>` +
            `Equals` overrides). Therefore MUST use `stateA.Equals(
            stateB)` (or equivalently `EqualityComparer<DFAState>.
            Default.Equals(...)`) to invoke the partial-equality
            semantics established by `program_dfa.dart`'s
            `dart-value-class-partial-equality-to-csharp-iequatable`
            idiom. This is the SINGLE most-load-bearing nuance in this
            file: a naive `stateA == stateB` port would silently
            change the coinductive base case from "value-equal" to
            "reference-equal", breaking subtyping correctness because
            the same logical state can be constructed twice (e.g. via
            `DFAState.Dual` which allocates fresh on each call —
            cached idiom note from `program_dfa.dart`).
        (4) `assert(!stateA.isDual && !stateB.isDual);` →
            `Debug.Assert(!stateA.IsDual && !stateB.IsDual);` using
            `System.Diagnostics.Debug`. Dart `assert` is compiled out
            in release mode (production) — C# `Debug.Assert` is
            similarly compiled out unless `DEBUG` is defined, matching
            the runtime cost profile exactly. Microsoft Learn:
            `Debug.Assert` "Checks for a condition; if the condition
            is false, displays a message box that shows the call
            stack" (conditional on `DEBUG`). NOT `Trace.Assert` (which
            is unconditional) and NOT `throw new InvalidOperation
            Exception` (which would change the semantics from
            development-only invariant to production-checked).
        (5) `if (stateB.isWildcard || stateB.isAnonymousFinal) return
            true;` → `if (stateB.IsWildcard || stateB.IsAnonymousFinal)
            return true;`. Direct port — boolean classifier properties
            cached as expression-bodied per
            `dart-boolean-classifier-getter-to-csharp-expression-property`
            (`program_dfa.dart`). Short-circuit `||` semantics are
            identical in both languages.
        (6) Symmetric line for `stateA`: `if (stateA.IsWildcard ||
            stateA.IsAnonymousFinal) return false;`.
        (7) `if (stateA.isPrimitiveType || stateB.isPrimitiveType) {
            return _checkPrimitiveSubtype(stateA, stateB); }` →
            `if (stateA.IsPrimitiveType || stateB.IsPrimitiveType)
            return CheckPrimitiveSubtype(stateA, stateB);`. Note the
            structural choice: the Dart returns immediately when EITHER
            operand is primitive — this is load-bearing because the
            primitive helper handles the mixed primitive-vs-non-primitive
            mismatch (returns false via `baseName` inequality). DO NOT
            "optimise" to checking both operands are primitive before
            delegating — that would silently change behaviour for
            asymmetric cases.
        (8) `final automA = dfa.getAutomaton(stateA.name);` →
            `Automaton automA = dfa.GetAutomaton(stateA.Name);`
            (similarly for `automB`). Dart `getAutomaton` throws
            `UnknownTypeError` on a miss (per `program_dfa.dart`); the
            C# equivalent throws `UnknownTypeException` (cached
            mapping `dart-error-class-recoverable-signal-to-csharp-exception`).
            Behaviour preserved: a missing automaton aborts the
            subtype check with the same recoverable signal.
        (9) `for (final entry in automA.transitions.entries) { final
            (fromState, label) = entry.key; ... }` → C# `foreach (var
            kvp in automA.Transitions) { var (fromState, label) =
            kvp.Key; ... }`. `Automaton.Transitions` is the C#
            `IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState>`
            from `program_dfa.dart`'s convspec; iterating yields
            `KeyValuePair<(DFAState, TransitionLabel), DFAState>`. The
            tuple key is destructured with `var (fromState, label) =
            kvp.Key;` — Microsoft Learn ValueTuple deconstruction
            assignment. The Dart record-pattern destructure `final
            (fromState, label) = entry.key;` maps 1-for-1 to this C#
            tuple deconstruction.
       (10) `if (fromState != stateA) continue;` →
            `if (!fromState.Equals(stateA)) continue;` — same partial-
            equality nuance as (3): MUST use `.Equals` (not `!=`) to
            invoke the overridden value-equality from
            `program_dfa.dart`. Filter narrows iteration to outgoing
            transitions from the *start* of A's automaton (matching
            the source comment "Only check transitions from the start
            state of A").
       (11) `final targetA = entry.value; final targetB =
            automB.transition(stateB, label);` → `DFAState targetA =
            kvp.Value; DFAState? targetB = automB.Transition(stateB,
            label);`. Per `program_dfa.dart`'s
            `dart-map-nullable-indexer-to-csharp-trygetvalue` idiom,
            `Automaton.Transition(...)` returns `DFAState?` (nullable
            reference, with `TryGetValue` inside). NO `!` forgiveness
            operator on the access.
       (12) `if (targetB == null) return false;` →
            `if (targetB is null) return false;` — `is null` is the
            recommended idiomatic null-check in modern C# (Microsoft
            Learn pattern-matching guidance). Under C# nullable
            reference types flow analysis, the compiler narrows
            `targetB` to non-null on the success path, so subsequent
            `.Equals(targetA)` does NOT require `!`. Strict improvement
            over Dart `!`-bang.
       (13) `if (targetA == targetB) continue;` →
            `if (targetA.Equals(targetB)) continue;` — partial-equality
            (3) again. Comment "Skip trivially equal targets" is
            preserved as `// Skip trivially equal targets`.
       (14) `if (!_checkTargetSubtype(targetA, targetB, dfa, visited))
            return false;` → `if (!CheckTargetSubtype(targetA, targetB,
            dfa, visited)) return false;`. The `visited` set is
            passed by reference (HashSet is a reference type — Dart
            `Set` is also reference-semantics) so mutations inside
            the recursive call accumulate in the SAME set instance
            visible to the caller. Behaviour identical.
       (15) Final `return true;` — port verbatim.
    idiom_id: dart-coinductive-recursive-predicate-with-shared-visited-set-to-csharp
    research_finding_id: rf-csharp-debug-assert-conditional-on-debug-symbol
    nuance: >-
      This construct combines several first-seen Dart→C# nuances that
      must be addressed explicitly per spec US2 AS4 (the "never gloss"
      rule). FIRST-SEEN IDIOM (defining row): a coinductive recursive
      predicate that uses a mutable shared visited set as the
      *assumption set* for cycle detection. C# port is mechanical at
      the surface BUT three nuances are load-bearing: (a) `==` on
      `DFAState` MUST be `.Equals` because the type implements
      `IEquatable<DFAState>` with hand-written value equality on a
      strict subset of fields (`baseName + isDual` only) — the cached
      idiom `dart-value-class-partial-equality-to-csharp-iequatable`
      from `program_dfa.dart` is the underpinning. Reference equality
      via raw `==` would silently change the coinductive base case.
      (b) `assert(...)` MUST become `Debug.Assert` (NOT
      `Trace.Assert`, NOT a thrown exception) to preserve the
      *development-only* invariant cost profile of the Dart source —
      Dart asserts are stripped in production builds and `Debug.Assert`
      is compiled out unless the `DEBUG` symbol is defined (Microsoft
      Learn). (c) The shared mutable `visited` HashSet is passed
      reference-by-value (the variable holds a reference to the same
      object); mutation across recursive frames is preserved 1:1
      because both languages share that semantic for collection types.
      The set is constructed once at the public entry point and
      threaded downward — NOT re-allocated per recursive call (which
      would lose the assumption-set coinductive correctness). Order of
      checks (memo → reflexivity → mode-shape assert → wildcard top/
      bottom → primitive lattice → transition iteration) is
      load-bearing for spec section 4.1 of `subtyping.md` and the
      paper's Definition 4.7; the C# port preserves declaration order
      verbatim.
  - construct_key: dart.helper.target_subtype_dispatch_three_case_mode_shape
    source_form: >-
      bool _checkTargetSubtype(DFAState targetA, DFAState targetB,
      ProgramDFA dfa, Set<String> visited) { if (!targetA.isDual &&
      !targetB.isDual) { return _isSubtype(targetA, targetB, dfa,
      visited); } if (targetA.isDual && targetB.isDual) { final innerA =
      dfa.getState(targetA.baseName); final innerB = dfa.getState(
      targetB.baseName); return _isSubtype(innerB, innerA, dfa,
      visited); } return false; }
    target_decision: >-
      Emit as `private static bool CheckTargetSubtype(DFAState targetA,
      DFAState targetB, ProgramDFA dfa, HashSet<string> visited)` on
      the `Subtyping` host static class. Body preserves the three-case
      dispatch as three sequential `if` statements (NOT a `switch` —
      see nuance):
        Case 1: `if (!targetA.IsDual && !targetB.IsDual) return
            CheckSubtype(targetA, targetB, dfa, visited);` —
            covariant recursion when both targets are output types.
        Case 2: `if (targetA.IsDual && targetB.IsDual) { var innerA =
            dfa.GetState(targetA.BaseName); var innerB =
            dfa.GetState(targetB.BaseName); return CheckSubtype(
            innerB, innerA, dfa, visited); }` — contravariant
            recursion: arguments REVERSED (`innerB` then `innerA`).
            This is the mode-inversion / contravariance pivot
            documented in the file header ("at mode inversion points
            the direction reverses (contravariance)").
        Case 3: `return false;` — mixed mode-shape (one dual, one
            output) is incompatible. The Dart source comment "Mixed
            → incompatible mode structure" is preserved as
            `// Case 3: Mixed → incompatible mode structure`.
      `ProgramDFA.GetState(string name)` is per `program_dfa.dart`'s
      convspec (throws `InvalidOperationException` on a miss via
      `TryGetValue`). `targetA.BaseName` is the bare type name
      WITHOUT the `?` dual marker — this is intentional: the
      contravariant recursion descends into the underlying output
      type, not the dual itself.
    idiom_id: dart-mode-shape-three-case-dispatch-to-csharp-if-chain
    research_finding_id: rf-csharp-variance-via-runtime-mode-flag-dispatch
    nuance: >-
      FIRST-SEEN IDIOM (defining row). Three-case dispatch on a
      *runtime* mode-shape pair (output × output / dual × dual /
      mixed). This is NOT a type-pattern switch because the operand
      types are identical (`DFAState`) — the discrimination is on a
      *runtime field value* (`IsDual` boolean), so a `switch` on the
      operand type would not help. A `switch` on a synthesised
      `(targetA.IsDual, targetB.IsDual)` tuple key IS syntactically
      possible but reduces clarity vs. three sequential `if`
      statements that mirror the Dart source 1:1. The CONTRAVARIANCE
      nuance is load-bearing: in case 2 the recursive call passes
      `(innerB, innerA)` — REVERSED — and the Dart comment
      `// REVERSED` is preserved as `// REVERSED` to anchor reader
      attention; a maintainer accidentally writing
      `CheckSubtype(innerA, innerB, ...)` would silently turn
      contravariance into covariance, breaking subtyping soundness
      for input positions (paper Section 4.6, Definition 4.7).
      Reference-vs-value: `DFAState` is reference type; the `var
      innerA = dfa.GetState(targetA.BaseName);` call returns a shared
      reference to the canonical state instance in
      `ProgramDFA._states` — no allocation, no copy. Mode-shape
      inspection is via the `IsDual` boolean property (cached from
      `dart-boolean-classifier-getter-to-csharp-expression-property`).
  - construct_key: dart.helper.primitive_lattice_subtype_check
    source_form: >-
      bool _checkPrimitiveSubtype(DFAState stateA, DFAState stateB) {
      if (stateB.isWildcard) return true; if (stateA.isWildcard)
      return false; if (stateA.isIntegerType && stateB.isNumberType)
      return true; if (stateA.isRealType && stateB.isNumberType)
      return true; return stateA.baseName == stateB.baseName; }
    target_decision: >-
      Emit as `private static bool CheckPrimitiveSubtype(DFAState
      stateA, DFAState stateB)` on the `Subtyping` host static class.
      Body is five sequential `return`/`if return` statements, port
      verbatim:
        `if (stateB.IsWildcard) return true;`
        `if (stateA.IsWildcard) return false;`
        `if (stateA.IsIntegerType && stateB.IsNumberType) return true;`
        `if (stateA.IsRealType && stateB.IsNumberType) return true;`
        `return string.Equals(stateA.BaseName, stateB.BaseName,
         StringComparison.Ordinal);`
      Source comments preserved verbatim:
        `// _ is top for output types (handled in caller, but be safe)`
        `// Integer <: Number`
        `// Real <: Number`
        `// Otherwise must be identical`
      The final `baseName` comparison uses explicit
      `StringComparison.Ordinal` per the codebase-wide ordinal
      discipline (`dart-string-keyed-map-to-csharp-ordinal-dictionary`
      cached idiom). C# `string` `==` operator IS ordinal by default
      (Microsoft Learn) but the project convention from
      `program_dfa.dart` / `type_ast.dart` / `clause_validation.dart`
      is to make ordinal explicit at every site so the contract is
      reviewable. The Integer/Real/Number lattice is a 2-step Hasse
      diagram (`Integer <: Number`, `Real <: Number`); no shared join
      between Integer and Real (other than Number).
    idiom_id: dart-primitive-lattice-cascading-classifier-to-csharp-if-chain
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      FIRST-SEEN IDIOM (defining row). A small primitive-type lattice
      encoded as a cascading sequence of boolean-classifier
      conjunctions. The defensive-against-caller note "_ is top for
      output types (handled in caller, but be safe)" is load-bearing —
      this helper duplicates the wildcard top/bottom check from the
      coinductive caller (`CheckSubtype`) so that
      `CheckPrimitiveSubtype` remains a TOTAL function callable in
      isolation; preserve the duplication, do NOT delete it as
      "dead code". Lattice shape: Integer ≤ Number, Real ≤ Number, all
      other primitive pairs compared by `BaseName` ordinal equality.
      Asymmetry: `stateB.IsWildcard` returns `true` (B is top), but
      `stateA.IsWildcard` returns `false` because at this point we
      know B is NOT wildcard (would have returned earlier) so A=`_`
      vs B=concrete-type is NOT a subtype — `_` is top, not bottom,
      for output types. C# string-equality nuance (ordinal by
      default, Microsoft Learn) is invoked explicitly to anchor
      ordinal discipline; the same nuance was recorded for
      `clause_validation.dart`'s `StartsWith` site. NO null safety
      concern (both states are non-null at this call site, enforced
      by the public predicate signature). NO reference-vs-value
      hazard (boolean primitives + string ordinal compare).
conversion_units:
  - "namespace Glp.Analysis.TypeChecker { public static class Subtyping { ... } }"
  - "public static bool IsSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa) => CheckSubtype(stateA, stateB, dfa, new HashSet<string>(StringComparer.Ordinal));"
  - "private static bool CheckSubtype(DFAState stateA, DFAState stateB, ProgramDFA dfa, HashSet<string> visited): pairKey = $\"{stateA.Name}:{stateB.Name}\"; if (visited.Contains(pairKey)) return true; visited.Add(pairKey); if (stateA.Equals(stateB)) return true; Debug.Assert(!stateA.IsDual && !stateB.IsDual); if (stateB.IsWildcard || stateB.IsAnonymousFinal) return true; if (stateA.IsWildcard || stateA.IsAnonymousFinal) return false; if (stateA.IsPrimitiveType || stateB.IsPrimitiveType) return CheckPrimitiveSubtype(stateA, stateB); var automA = dfa.GetAutomaton(stateA.Name); var automB = dfa.GetAutomaton(stateB.Name); foreach (var kvp in automA.Transitions) { var (fromState, label) = kvp.Key; if (!fromState.Equals(stateA)) continue; var targetA = kvp.Value; var targetB = automB.Transition(stateB, label); if (targetB is null) return false; if (targetA.Equals(targetB)) continue; if (!CheckTargetSubtype(targetA, targetB, dfa, visited)) return false; } return true;"
  - "private static bool CheckTargetSubtype(DFAState targetA, DFAState targetB, ProgramDFA dfa, HashSet<string> visited): if (!targetA.IsDual && !targetB.IsDual) return CheckSubtype(targetA, targetB, dfa, visited); if (targetA.IsDual && targetB.IsDual) { var innerA = dfa.GetState(targetA.BaseName); var innerB = dfa.GetState(targetB.BaseName); return CheckSubtype(innerB, innerA, dfa, visited); } return false;"
  - "private static bool CheckPrimitiveSubtype(DFAState stateA, DFAState stateB): if (stateB.IsWildcard) return true; if (stateA.IsWildcard) return false; if (stateA.IsIntegerType && stateB.IsNumberType) return true; if (stateA.IsRealType && stateB.IsNumberType) return true; return string.Equals(stateA.BaseName, stateB.BaseName, StringComparison.Ordinal);"
  - "XML-doc /// summary blocks ported from each Dart /// doc-comment verbatim — header file comment with spec citation (docs/type system/subtyping.md) + Paper Reference (Section 4.6, Definition 4.7) preserved on the public IsSubtype method; per-helper section references (spec 4.1 / 4.2 / 4.3 / 4.4 / 4.5) preserved verbatim."
  - "using System.Collections.Generic; using System.Diagnostics; (the latter for Debug.Assert)"
escalations: []
```

## Rationale & Research Provenance

This file implements the coinductive subtyping algorithm for GLP output
types (Paper Reference: Definition 4.7, spec `docs/type system/
subtyping.md`). The conversion reuses heavily from the cached idioms
already recorded by `program_dfa.dart`, `clause_validation.dart`,
`prelude.dart`, and `type_ast.dart` (FR-024 cache, no fresh research
for cached idioms per FR-012 / SC-007). Three first-seen idioms are
defined in this artifact:

### dart-toplevel-fn-to-csharp-static-method  (cached idiom)

**Deep analysis.** One public predicate (`isSubtype`) + three private
helpers (`_isSubtype`, `_checkTargetSubtype`, `_checkPrimitiveSubtype`)
sit at the library top level. The public entry point is a 1-statement
delegate that seeds the visited set; the three helpers are
file-internal recursion machinery.

**Research (cached, FR-024 — no fresh call).** Reuses
`rf-csharp-static-class-no-toplevel-members` first recorded by
`prelude.dart`'s spec: Microsoft Learn — "A class declared at namespace
scope is a top-level type; methods can only be declared inside a type."
Idiom `dart-toplevel-fn-to-csharp-static-method` is `active`; per FR-012
/ SC-007, REUSE verbatim — do not re-research.

**Conclusion.** Host class `public static class Subtyping` in namespace
`Glp.Analysis.TypeChecker`; the public predicate becomes
`public static bool IsSubtype(...)`; the three helpers become
`private static bool CheckSubtype/CheckTargetSubtype/CheckPrimitiveSubtype`
on the same host class.

### dart-coinductive-recursive-predicate-with-shared-visited-set-to-csharp  (FIRST-SEEN)

**Deep analysis.** `_isSubtype` is the engine: a recursive predicate
over a DFA state pair that uses a mutable `Set<String>` (`visited`) as
the coinductive *assumption set* for cycle detection (spec section 4.5:
"if we've already assumed this pair, succeed"). The ordering of the
six pre-iteration checks is load-bearing (memo → reflexivity → mode-
shape assert → wildcard top/bottom → primitive lattice → transition
iteration). The shared `visited` set threads downward through every
recursive call AND through `_checkTargetSubtype` (which re-enters
`_isSubtype` with the same set), accumulating assumptions across the
entire descent.

**Research (FRESH — first-seen idiom, FR-024 official-docs
authoritative).** Microsoft Learn — `System.Diagnostics.Debug.Assert`:
"Checks for a condition; if the condition is false, displays a message
box that shows the call stack. … This method is ignored unless the
DEBUG conditional compilation symbol is defined." This matches Dart's
`assert` semantics (stripped in production / `--release` builds). The
conditional-compilation pairing is the load-bearing match; `Trace.Assert`
(unconditional) would change the runtime cost profile and behaviour.
Microsoft Learn `HashSet<T>` constructors: "Initializes a new instance
of the `HashSet<T>` class that uses the specified equality comparer
for the set type" — passing `StringComparer.Ordinal` explicitly to
match the codebase-wide ordinal discipline. Microsoft Learn ValueTuple
deconstruction: `var (fromState, label) = kvp.Key;` is the canonical
pattern for dictionary iteration over tuple keys; matches the Dart 3
record-pattern destructure `final (fromState, label) = entry.key;`
1:1.

**Conclusion.** The new idiom
`dart-coinductive-recursive-predicate-with-shared-visited-set-to-csharp`
records the three load-bearing nuances: (a) `==` on `DFAState` MUST be
`.Equals` (partial value-equality via `IEquatable<DFAState>`, cached
from `program_dfa.dart`); (b) `assert` MUST be `Debug.Assert`; (c) the
shared mutable `visited` set is constructed once at the public entry
and threaded downward by reference, never re-allocated per call.

### dart-mode-shape-three-case-dispatch-to-csharp-if-chain  (FIRST-SEEN)

**Deep analysis.** `_checkTargetSubtype` discriminates on the
mode-shape pair `(targetA.isDual, targetB.isDual)` — three cases:
(output, output) → covariant recursion; (dual, dual) → contravariant
recursion with arguments REVERSED; otherwise → false. The
contravariance pivot is the *single most-load-bearing semantic in this
helper* (paper Section 4.6, Definition 4.7).

**Research (FRESH — first-seen, FR-024).** Microsoft Learn pattern
matching — discrimination on a *boolean tuple* (`(bool, bool)`) is
syntactically possible via switch expressions
`switch ((targetA.IsDual, targetB.IsDual)) { (false, false) => ...,
(true, true) => ..., _ => false }` but reduces source-form
parallelism vs. the Dart code. The Dart source uses three sequential
`if` statements; the C# port preserves that shape so the contravariant-
REVERSED comment lines up with the recursive call exactly. Microsoft
Learn `IEquatable<T>.Equals`: the value-equality contract requires
explicit `.Equals` invocation when reference-type identity is
insufficient — this anchors the use of `.Equals` over `==` consistently
with `program_dfa.dart`'s partial-equality idiom.

**Conclusion.** The new idiom
`dart-mode-shape-three-case-dispatch-to-csharp-if-chain` records the
three-`if` structure, the contravariant-REVERSED pivot (with the
preserved `// REVERSED` comment), and the explicit `// Case 3: Mixed
→ incompatible mode structure` no-op-via-default-`false` arm.
Reference-vs-value note: `dfa.GetState(...)` returns the canonical
shared `DFAState` instance from `ProgramDFA._states` — no allocation,
no copy; the recursive call sees the same reference.

### dart-primitive-lattice-cascading-classifier-to-csharp-if-chain  (FIRST-SEEN)

**Deep analysis.** `_checkPrimitiveSubtype` encodes a tiny primitive-
type Hasse diagram (Integer ≤ Number, Real ≤ Number; no shared join
between Integer and Real except Number; otherwise `baseName`
equality). The function is TOTAL and defensively duplicates the
wildcard top/bottom check from its coinductive caller.

**Research (cached, FR-024).** Reuses
`rf-csharp-string-equality-ordinal-by-default` from
`program_dfa.dart` / `clause_validation.dart`: Microsoft Learn — "C#
`string ==` is ordinal by default." Explicit `StringComparison.Ordinal`
is the codebase convention for reviewability. Microsoft Learn boolean
short-circuit operators (`&&`) — identical semantics in Dart and C#,
mechanical port.

**Conclusion.** The new idiom
`dart-primitive-lattice-cascading-classifier-to-csharp-if-chain`
records the cascading `if … return` shape, the defensive
caller-duplication of the wildcard checks (do NOT delete as "dead
code" — the helper is TOTAL by contract), the asymmetric `stateB.
IsWildcard` (top) vs `stateA.IsWildcard` (not bottom) treatment, and
the explicit `StringComparison.Ordinal` discipline.

### Trivial / non-construct elements

- File header `// lib/analysis/type_checker/subtyping.dart` and the
  spec-citation comment block (`// Specification: docs/type system/
  subtyping.md`, `// Paper Reference: Section 4.6, Definition 4.7
  (Subtyping)`) map to C# `//` comments mechanically — no research.
- `/// XML doc-comments` (on `isSubtype`, `_isSubtype`,
  `_checkTargetSubtype`, `_checkPrimitiveSubtype`) map 1-for-1 to C#
  `///` summary blocks — Dart triple-slash and C# triple-slash
  semantics are identical (XML doc).
- `import 'program_dfa.dart';` is subsumed by a `using
  Glp.Analysis.TypeChecker;` directive emitted by the codegen stage
  per the project's namespace layout — not specced per construct
  (trivial, cross-file concern; all four types — `DFAState`,
  `ProgramDFA`, `Automaton`, `TransitionLabel` — live in the same
  namespace per `program_dfa.dart`'s convspec).
