---
path: lib/analysis/type_checker/subtyping.dart
cycle_group_id: 15
scc_siblings: []
generated_at: 2026-05-21T15:10:00Z
source_sha256: 57b232507c21c6081aca3fcc70d7d7c850b562a16fe52abdef790d058a330224
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/subtyping.dart

## 1. Source Analysis

Actual inspection of `glp_runtime_net/lib/analysis/type_checker/subtyping.dart`
(113 lines, sha256 `57b2325…30224`):

- **File header (lines 1–8):** `//` comments naming the file, summarising
  the algorithm ("A <: B iff every simple prefix of A is accepted by B,
  and at mode inversion points the direction reverses (contravariance)"),
  and citing `docs/type system/subtyping.md` + paper Definition 4.7.
- **Single import (line 10):** `import 'program_dfa.dart';` — pulls in
  `DFAState`, `ProgramDFA`, `Automaton`, `TransitionLabel`.
- **Public predicate (lines 12–20):** `bool isSubtype(DFAState stateA,
  DFAState stateB, ProgramDFA dfa)` — three lines: triple-slash doc-
  comment + one-line body delegating to `_isSubtype(stateA, stateB, dfa,
  <String>{})`. The `<String>{}` literal allocates a mutable empty
  `Set<String>` (the coinductive *assumption set*).
- **Private engine (lines 22–72):** `bool _isSubtype(DFAState stateA,
  DFAState stateB, ProgramDFA dfa, Set<String> visited)`. Six load-
  bearing steps in declaration order:
    1. `pairKey` = `'${stateA.name}:${stateB.name}'`; memo-check + add
       to `visited` (coinductive base case, spec 4.5).
    2. Reflexivity short-circuit: `if (stateA == stateB) return true;`.
    3. Mode-shape invariant: `assert(!stateA.isDual && !stateB.isDual);`.
    4. Wildcard top/bottom (spec 4.4): `stateB.isWildcard ||
       stateB.isAnonymousFinal` → true; symmetric A-side → false.
    5. Primitive-lattice short-circuit (spec 4.3): if either operand is
       primitive, delegate to `_checkPrimitiveSubtype`.
    6. Transition iteration (spec 4.1): pull `automA = dfa.getAutomaton(
       stateA.name)`, `automB = dfa.getAutomaton(stateB.name)`; for each
       `(fromState, label) → targetA` entry in `automA.transitions`
       where `fromState == stateA`, look up `targetB =
       automB.transition(stateB, label)`; if `targetB == null` →
       `false`; if `targetA == targetB` → `continue`; else recurse via
       `_checkTargetSubtype(targetA, targetB, dfa, visited)`. Final
       fall-through returns `true`.
- **Target-dispatch helper (lines 74–93):** `bool _checkTargetSubtype(...)`
  — three-case dispatch on `(targetA.isDual, targetB.isDual)`:
  (false, false) → covariant `_isSubtype(targetA, targetB, ...)`;
  (true, true) → contravariant `_isSubtype(innerB, innerA, ...)` with
  arguments REVERSED (the source carries an inline `// REVERSED`
  marker on line 88); mixed → `false`. The contravariant arm
  resolves `innerA = dfa.getState(targetA.baseName)` (and similarly
  for `B`) — `baseName` strips the dual `?` suffix.
- **Primitive-lattice helper (lines 95–112):** `bool
  _checkPrimitiveSubtype(DFAState stateA, DFAState stateB)` — five
  sequential checks: defensive `stateB.isWildcard` → true, defensive
  `stateA.isWildcard` → false, `Integer <: Number` → true, `Real <:
  Number` → true, fallback `stateA.baseName == stateB.baseName`. The
  defensive wildcard duplication is documented inline (`// _ is top
  for output types (handled in caller, but be safe)`).
- **No fields, no classes, no async, no exceptions thrown directly
  here.** All exception-throwing surfaces are inside the called
  primitives (`dfa.getAutomaton`, `dfa.getState`) and live in
  `program_dfa.dart`'s ported surface.

## 2. Dart → C#/.NET Conversion Plan

Per construct (mirrors convspec verbatim — convspec is RATIFIED):

- **File header + spec-citation comments (lines 1–8):** Port verbatim
  as C# `//` comments at the top of `subtyping.cs`. The block is
  documentation, not code; mechanical port.
- **`import 'program_dfa.dart';` (line 10):** Resolved at code-gen
  stage. Both files emit into the same `Glp.Analysis.TypeChecker`
  namespace per `program_dfa.dart`'s convspec, so no explicit
  `using` directive is required for the four imported symbols
  (`DFAState`, `ProgramDFA`, `Automaton`, `TransitionLabel`). The
  generated `.cs` will still emit `using System.Collections.Generic;`
  (for `HashSet<string>`) and `using System.Diagnostics;` (for
  `Debug.Assert`).
- **`bool isSubtype(...)` (lines 18–20)** →
  `public static bool IsSubtype(DFAState stateA, DFAState stateB,
  ProgramDFA dfa)` on `public static class Subtyping` in namespace
  `Glp.Analysis.TypeChecker`. One-statement body:
  `return CheckSubtype(stateA, stateB, dfa, new HashSet<string>(
  StringComparer.Ordinal));`. Cached idiom
  `dart-toplevel-fn-to-csharp-static-method` +
  `dart-string-keyed-map-to-csharp-ordinal-dictionary` (the latter
  applied to `HashSet<string>` for codebase-wide ordinal discipline,
  per FR-024 cache from `prelude.dart` / `program_dfa.dart`).
- **`bool _isSubtype(...)` (lines 25–72)** →
  `private static bool CheckSubtype(DFAState stateA, DFAState stateB,
  ProgramDFA dfa, HashSet<string> visited)` on the same host class.
  Step-by-step:
    1. `string pairKey = $"{stateA.Name}:{stateB.Name}";` — string
       interpolation idiom (cached from `program_dfa.dart`).
    2. `if (visited.Contains(pairKey)) return true; visited.Add(
       pairKey);` — direct port; the shared mutable `visited` set is
       threaded downward, mutations visible to every recursive frame.
    3. `if (stateA.Equals(stateB)) return true;` — **MUST** use
       `.Equals` rather than `==` to invoke `DFAState`'s
       `IEquatable<DFAState>` value-equality (per
       `program_dfa.dart`'s
       `dart-value-class-partial-equality-to-csharp-iequatable`
       idiom). Raw `==` would default to reference identity for the
       reference type and silently change the coinductive base case.
       This is the single most load-bearing nuance in the file.
    4. `Debug.Assert(!stateA.IsDual && !stateB.IsDual);` —
       development-only invariant; matches Dart's release-stripped
       `assert` cost profile. NOT `Trace.Assert`, NOT a thrown
       exception (cached
       `dart-assert-to-csharp-debug-assert-conditional-on-debug-symbol`
       semantics, first researched here).
    5. `if (stateB.IsWildcard || stateB.IsAnonymousFinal) return true;`
       and symmetric A-side `return false`. Direct port; boolean
       classifier properties already cached as expression-bodied
       per `program_dfa.dart`.
    6. `if (stateA.IsPrimitiveType || stateB.IsPrimitiveType) return
       CheckPrimitiveSubtype(stateA, stateB);` — port verbatim, do
       NOT "optimise" to a both-primitive precondition (would
       change behaviour for mixed primitive-vs-non-primitive cases).
    7. `Automaton automA = dfa.GetAutomaton(stateA.Name);` and
       symmetric `automB`. `GetAutomaton` throws
       `UnknownTypeException` on miss (cached
       `dart-error-class-recoverable-signal-to-csharp-exception`).
    8. Transition iteration:
       ```
       foreach (var kvp in automA.Transitions) {
           var (fromState, label) = kvp.Key;
           if (!fromState.Equals(stateA)) continue;
           DFAState targetA = kvp.Value;
           DFAState? targetB = automB.Transition(stateB, label);
           if (targetB is null) return false;
           if (targetA.Equals(targetB)) continue;
           if (!CheckTargetSubtype(targetA, targetB, dfa, visited))
               return false;
       }
       ```
       `Automaton.Transitions` is the
       `IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState>`
       defined by `program_dfa.dart`'s convspec; the tuple-key
       deconstruction `var (fromState, label) = kvp.Key;` matches
       Dart 3 record patterns 1:1. `Transition(stateB, label)` returns
       `DFAState?` (per
       `dart-map-nullable-indexer-to-csharp-trygetvalue` cached
       idiom); the `is null` check narrows the nullable on the
       success path so no `!` forgiveness operator is needed.
    9. `return true;` — port verbatim.
  All inline source comments preserved: `// Coinductive: if we've
  already assumed this pair, succeed (spec 4.5)`, `// Reflexivity`,
  `// Both must be output types (not dual)`, `// Wildcard/final
  handling (spec 4.4)`, `// _FINAL_ is treated as equivalent to _
  for subtyping purposes`, `// Primitive type lattice (spec 4.3)`,
  `// User-defined types: check transitions (spec 4.1)`,
  `// Every transition from A must have a matching transition in B`,
  `// Only check transitions from the start state of A`,
  `// A has a transition B lacks → not a subtype`,
  `// Skip trivially equal targets`, `// Check target compatibility
  (spec 4.2)`.
- **`bool _checkTargetSubtype(...)` (lines 77–93)** →
  `private static bool CheckTargetSubtype(DFAState targetA, DFAState
  targetB, ProgramDFA dfa, HashSet<string> visited)`. Three
  sequential `if` statements (NOT a tuple `switch` — three ifs
  mirror the Dart source 1:1 and preserve the inline `// REVERSED`
  anchor near the contravariant arm):
    - Case 1 (output × output): `if (!targetA.IsDual &&
      !targetB.IsDual) return CheckSubtype(targetA, targetB, dfa,
      visited);`.
    - Case 2 (dual × dual, contravariant pivot):
      ```
      if (targetA.IsDual && targetB.IsDual) {
          DFAState innerA = dfa.GetState(targetA.BaseName); // output type A'
          DFAState innerB = dfa.GetState(targetB.BaseName); // output type B'
          return CheckSubtype(innerB, innerA, dfa, visited); // REVERSED
      }
      ```
      `dfa.GetState(string)` returns the canonical
      `DFAState` instance from `ProgramDFA._states` (per
      `program_dfa.dart`; throws
      `InvalidOperationException` on miss). `BaseName` strips the
      `?` dual marker — intentional, the contravariant recursion
      descends into the underlying output type.
    - Case 3: `return false;` preceded by `// Case 3: Mixed →
      incompatible mode structure` (the `→` is U+2192).
  Comments preserved verbatim: `// Case 1: Both output types →
  covariant recursion`, `// Case 2: Both dual types →
  contravariant recursion (reversed)`, `// REVERSED`, and the
  Case 3 comment above.
- **`bool _checkPrimitiveSubtype(...)` (lines 99–112)** →
  `private static bool CheckPrimitiveSubtype(DFAState stateA,
  DFAState stateB)`. Five sequential statements:
    ```
    if (stateB.IsWildcard) return true;
    if (stateA.IsWildcard) return false;
    if (stateA.IsIntegerType && stateB.IsNumberType) return true;
    if (stateA.IsRealType    && stateB.IsNumberType) return true;
    return string.Equals(stateA.BaseName, stateB.BaseName,
                         StringComparison.Ordinal);
    ```
  Explicit `StringComparison.Ordinal` (codebase-wide reviewability
  discipline, cached from `program_dfa.dart` /
  `clause_validation.dart`). Defensive wildcard duplication is
  PRESERVED — the helper is TOTAL by contract, do NOT delete as
  dead code. Comments preserved: `// _ is top for output types
  (handled in caller, but be safe)`, `// Integer <: Number`,
  `// Real <: Number`, `// Otherwise must be identical`.
- **Triple-slash XML doc-comments** on `isSubtype`, `_isSubtype`,
  `_checkTargetSubtype`, `_checkPrimitiveSubtype` map 1:1 to C#
  `/// <summary>` blocks (Dart and C# triple-slash semantics are
  identical XML doc).
- **Required `using` directives:** `using System.Collections.Generic;`
  (HashSet), `using System.Diagnostics;` (Debug.Assert).

## 3. Decomposed Task Units

- **T1:** Emit `using System.Collections.Generic;` and
  `using System.Diagnostics;` plus `namespace Glp.Analysis.TypeChecker
  { public static class Subtyping { … } }` skeleton with the file-
  header `//` comment block and spec/paper citations preserved
  verbatim. — done one-line.
- **T2:** Emit `public static bool IsSubtype(DFAState stateA, DFAState
  stateB, ProgramDFA dfa)` as a single-statement delegate seeding
  `new HashSet<string>(StringComparer.Ordinal)` into `CheckSubtype`,
  with the `/// <summary>` block ported from the Dart doc-comment
  (Definition 4.7 reference preserved). — done one-line.
- **T3:** Emit `private static bool CheckSubtype(...)` with all six
  load-bearing checks in declaration order: pair-key memo,
  reflexivity via `.Equals`, `Debug.Assert` mode-shape invariant,
  wildcard top/bottom (B then A), primitive-lattice delegation,
  automaton-transition iteration with tuple-key deconstruction +
  `is null` narrowing + `.Equals` reference filter. Inline comments
  preserved verbatim including `// (spec 4.X)` callouts. — done
  one-line.
- **T4:** Emit `private static bool CheckTargetSubtype(...)` as
  three sequential `if`s (covariant / contravariant / mixed→false)
  with the `// REVERSED` anchor inline on the contravariant
  recursive call, the `→` (U+2192) preserved in the
  Case 1/Case 2/Case 3 comments. — done one-line.
- **T5:** Emit `private static bool CheckPrimitiveSubtype(...)` as
  five sequential statements (defensive wildcard top/bottom,
  Integer ≤ Number, Real ≤ Number, explicit-ordinal `BaseName`
  equality) with all four inline comments preserved. — done
  one-line.
- **T6:** Emit `/// <summary>` blocks on all four methods,
  capturing the Dart doc-comments (spec sections + paper
  Definition 4.7) verbatim. — done one-line.

## 4. Research Findings

none required — all idioms either cached (FR-024) from prior specs
(`prelude.dart`, `program_dfa.dart`, `clause_validation.dart`,
`type_ast.dart`) or established in this file's own ratified
convspec (three first-seen idioms with full Microsoft Learn
provenance documented in the convspec's Rationale & Research
Provenance section). No fresh WebSearch/WebFetch needed.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/lib/analysis/type_checker/subtyping.dart.md`
(ratified convspec, source_sha256 match
`57b232507c21c6081aca3fcc70d7d7c850b562a16fe52abdef790d058a330224`)
and its cross-references to `program_dfa.dart`'s convspec for
`DFAState` / `Automaton` / `ProgramDFA` surface semantics
(IEquatable partial equality, nullable `Transition`, `Name`/`BaseName`
properties, `IReadOnlyDictionary<(DFAState, TransitionLabel),
DFAState>` for `Transitions`). The single cross-file decision in
the convspec — explicit `StringComparison.Ordinal` on the
fallback `BaseName` equality — is consistent with `type_ast.dart`,
`clause_validation.dart`, and `program_dfa.dart`'s ordinal
discipline. The `TypeEnvironment.getType(String)` /
`object.GetType` shadowing concern flagged in the planner
preamble does NOT apply here: this file does not call
`TypeEnvironment.getType` and does not touch `TypeEnvironment` at
all (its only collaborator is `ProgramDFA` via `getAutomaton` and
`getState`).

## 6. Escalations

None.
