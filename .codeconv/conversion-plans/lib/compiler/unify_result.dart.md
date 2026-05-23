---
path: lib/compiler/unify_result.dart
cycle_group_id: 37
scc_siblings: []
generated_at: 2026-05-21T15:19:52Z
source_sha256: 34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52
schema_version: 1
---

# Conversion Plan: lib/compiler/unify_result.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/lib/compiler/unify_result.dart` (20 lines, sha256 `34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52`):

- **Line 1** — `import 'ast.dart' show Term;` — a single relative import of the sibling `ast.dart` library, narrowed via Dart `show` to the type `Term`. `Term` is referenced exactly once, as the value-type parameter of `Map<String, Term>` on `UnifySuccess` (line 7).
- **Line 3** — Triple-slash doc comment `/// Result of compile-time GLP unification for partial evaluation.` immediately above the base class declaration.
- **Line 4** — `sealed class UnifyResult {}` — Dart-3 `sealed` marker base class with empty body. No methods, no fields, no `==`/`hashCode`/`toString` override.
- **Lines 6-9** — `class UnifySuccess extends UnifyResult { final Map<String, Term> substitution; UnifySuccess(this.substitution); }` — success arm; one `final` `Map<String, Term>` payload (`substitution`); positional constructor with `this.substitution` initialising formal.
- **Lines 11-14** — `class UnifyFail extends UnifyResult { final String reason; UnifyFail(this.reason); }` — failure arm; one `final` non-nullable `String` payload (`reason`); positional constructor.
- **Lines 16-19** — `class UnifySuspend extends UnifyResult { final Set<String> unboundReaders; UnifySuspend(this.unboundReaders); }` — suspend arm; one `final` `Set<String>` payload (`unboundReaders`); positional constructor.

File-level invariants observed:
- No `async`/`await`, `Future`, `Stream`, `Completer`, `Isolate`, `late`, `mixin`, `extension`, `record`, operator overloading, factory constructors, const constructors, named constructors, generics with bounds, FFI, web-only types, top-level variables, top-level functions, or static members.
- No `==`/`hashCode`/`toString` overrides on any arm — default `Object` reference identity equality inherited.
- File is a pure declaration: this file never iterates, mutates, or reads the payload collections; consumption happens in `analyzer.dart` and `partial_evaluator.dart` (which now import this file rather than each redeclaring the ADT — the lift-to-shared resolution of escalation #3).
- Three payload field types: `Map<String, Term>`, `String`, `Set<String>`. Non-nullable under enabled Dart NRT.
- Closure semantics: Dart 3 `sealed` library-local closure with three exhaustive arms.

## 2. Dart → C#/.NET Conversion Plan

Mirror of convspec `.codeconv/conversion-specs/lib/compiler/unify_result.dart.md` (sha256 of source matches; ratified). The canonical cached idiom instance is `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves` (canonical site `compiler/ast.dart.md`; this file is now the canonical home for the `UnifyResult` ADT specifically).

**C1 — `import 'ast.dart' show Term;`** → emit `using <root>.Compiler;` (the namespace hosting the converted `Ast.cs`). Cached idiom `rf-dart-relative-import-to-csharp-using-or-same-namespace` (sibling: `compiler/parser.dart.md`). The Dart `show Term` per-symbol filter is dropped per `rf-dart-import-show-clause-no-csharp-counterpart` (originated `runtime/heap_fcp.dart.md`) — C# has no per-symbol allow-list at the `using` directive level; `using static <Type>;` imports type *members*, not type *references*, so it is not a counterpart. If the converted `UnifyResult.cs` lives in the same C# namespace as `Ast.cs` (both under `<root>.Compiler`), codegen MAY elide the directive as a same-namespace no-op; the default form for review parity is to emit the redundant `using` line.

**C2 — `/// Result of compile-time GLP unification for partial evaluation.`** → emit C# XML-doc comment `/// <summary>Result of compile-time GLP unification for partial evaluation.</summary>` immediately above the abstract class declaration. Trivial mechanical mapping (cached convention across compiler/* and runtime/* specs).

**C3 — `sealed class UnifyResult {}` + three subclass arms** → cached idiom `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves` (the canonical instance home for `UnifyResult`). Emit a CLOSED hierarchy:
- `public abstract class UnifyResult { protected UnifyResult() { } }` — `abstract` prevents direct instantiation; the protected constructor pins instantiation to derived classes; reference type with default `Object`-identity equality (no `Equals`/`GetHashCode`/`ToString` override).
- Closure of the hierarchy is encoded by sealing every LEAF (Microsoft Learn: "It's an error to use the abstract modifier with a sealed class" — the base cannot be both, so closure is split between `abstract` on the base and `sealed` on each leaf).
- The Dart-3 static exhaustiveness check is RUNTIME-MIRRORED at every consumer site by a throwing default arm `_ => throw new InvalidOperationException(...)` — C# 11+ pattern-match switches do NOT verify exhaustiveness across user-declared class hierarchies (only across enums / value types / nullable references). Consumer specs (`analyzer.dart.md`, `partial_evaluator.dart.md`) already record this consumer-side obligation; this declaration spec records the source-side root and forwards.
- `record` (positional or otherwise) REJECTED: synthesises by-value structural equality + auto-generated `ToString`/`GetHashCode`/`Deconstruct` surface that the Dart source does not have; default equality contract is reference identity, must be preserved. `record struct` doubly rejected: heap-bound reference payloads + boxing every time a leaf value flows through the abstract base.

**C4 — `class UnifySuccess extends UnifyResult { final Map<String, Term> substitution; UnifySuccess(this.substitution); }`** →
- `public sealed class UnifySuccess : UnifyResult` (sealed leaf, encodes closure).
- Property: `public IReadOnlyDictionary<string, Term> Substitution { get; }` — get-only auto-property; payload-immutability projection of the sealed-ADT cached idiom (the substitution Map is captured once and never mutated by the ADT). Cached idiom `rf-dart-map-to-csharp-dictionary` (root: `runtime/machine_state.dart.md`) governs the concrete-impl side: callers supply any concrete dictionary (most commonly `Dictionary<string, Term>`). Read-only interface (`IReadOnlyDictionary<TKey,TValue>`, `System.Collections.Generic`, Microsoft Learn) exposes `Count`/`ContainsKey`/`TryGetValue`/`this[key]`/`GetEnumerator`/`Keys`/`Values` but NOT `Add`/`Remove`/`Clear`/`this[key] = …`.
- Constructor: `public UnifySuccess(IReadOnlyDictionary<string, Term> substitution) { Substitution = substitution; }` — positional, assigns the get-only auto-property in the body. Non-nullable under enabled NRT (Dart `Map<String, Term>`, not `Map<String?, Term?>`).
- Iteration-order delta (`LinkedHashMap` insertion-ordered vs `Dictionary` undefined) is LATENT in this declaration file (no iteration here); consumer specs already record the cross-file delta. Missing-key-lookup divergence (Dart `Map[k]` returns null vs C# `Dictionary[k]` throws `KeyNotFoundException`) also latent here.

**C5 — `class UnifyFail extends UnifyResult { final String reason; UnifyFail(this.reason); }`** →
- `public sealed class UnifyFail : UnifyResult` (sealed leaf).
- Property: `public string Reason { get; }` — get-only auto-property; non-nullable under enabled NRT (Dart source types the field `String`, not `String?`). Cached idiom `rf-dart-string-to-csharp-string` (root: `compiler/error.dart.md`'s exception-message field family).
- Constructor: `public UnifyFail(string reason) { Reason = reason; }` — positional, non-nullable `string` parameter. Calling with `null` requires explicit `null!` override at the caller (matches Dart compile error for `null` to a non-nullable formal).
- No interpolation/formatting occurs in this declaration file; the message is opaque to the ADT. Interpolation at the caller site (where the failure message is built) is handled by the caller's spec (`rf-dart-tostring-interp-to-csharp-tostring-interp` cached idiom).
- Encoding: Dart `String` and C# `string` are both UTF-16 code-unit sequences (api.dart.dev / Microsoft Learn) — byte-identical character storage.

**C6 — `class UnifySuspend extends UnifyResult { final Set<String> unboundReaders; UnifySuspend(this.unboundReaders); }`** →
- `public sealed class UnifySuspend : UnifyResult` (sealed leaf).
- Property: `public IReadOnlySet<string> UnboundReaders { get; }` — get-only auto-property; payload-immutability projection (the unbound-readers set is captured once and never mutated by the ADT). Cached idiom `rf-dart-set-to-csharp-hashset` (root: `runtime/machine_state.dart.md` suspension-tracker family) governs the concrete-impl side: callers supply any concrete set (most commonly `HashSet<string>`). Read-only interface `IReadOnlySet<T>` was introduced in .NET 5 (Microsoft Learn) — target framework `net6.0` or newer is presumed (sibling-spec convention).
- Constructor: `public UnifySuspend(IReadOnlySet<string> unboundReaders) { UnboundReaders = unboundReaders; }` — positional, non-nullable parameter (Dart `Set<String>`, not nullable element type).
- Iteration-order delta (`LinkedHashSet` insertion-ordered vs `HashSet` undefined) is latent here; consumer specs record the cross-file delta.

Target code-unit per convspec: `lib/compiler/UnifyResult.cs`.

## 3. Decomposed Task Units

- T1: Emit single `using <root>.Compiler;` directive (or elide if same-namespace) — C1; cached `rf-dart-relative-import-to-csharp-using-or-same-namespace` + `rf-dart-import-show-clause-no-csharp-counterpart`. done.
- T2: Emit XML-doc `/// <summary>Result of compile-time GLP unification for partial evaluation.</summary>` above abstract base — C2; trivial. done.
- T3: Emit `public abstract class UnifyResult { protected UnifyResult() { } }` — C3 base; cached `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`. done.
- T4: Emit `public sealed class UnifySuccess : UnifyResult` with get-only `IReadOnlyDictionary<string, Term> Substitution` and positional ctor `UnifySuccess(IReadOnlyDictionary<string, Term> substitution)` — C4; cached `rf-dart-map-to-csharp-dictionary` + read-only-interface payload-immutability projection. done.
- T5: Emit `public sealed class UnifyFail : UnifyResult` with get-only non-nullable `string Reason` and positional ctor `UnifyFail(string reason)` — C5; cached `rf-dart-string-to-csharp-string`. done.
- T6: Emit `public sealed class UnifySuspend : UnifyResult` with get-only `IReadOnlySet<string> UnboundReaders` and positional ctor `UnifySuspend(IReadOnlySet<string> unboundReaders)` — C6; cached `rf-dart-set-to-csharp-hashset` + read-only-interface payload-immutability projection. done.

## 4. Research Findings

none required. Every construct resolves from cached idioms with authoritative Dart and .NET official-documentation citations carried forward in the convspec (FR-024 cache hit at every construct):

- `rf-dart-relative-import-to-csharp-using-or-same-namespace` — Dart language tour "import directives" + Microsoft Learn `using` directive (sibling: `compiler/parser.dart.md`).
- `rf-dart-import-show-clause-no-csharp-counterpart` — Dart `show` narrows imported surface; C# `using` has no per-symbol filter; `using static` imports members not types (root: `runtime/heap_fcp.dart.md`).
- `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves` — Dart 3 `sealed` (Dart language tour "Class modifiers": "sealed gives the compiler enough information to enforce exhaustive switching") + Microsoft Learn `abstract`/`sealed` modifiers ("It's an error to use the abstract modifier with a sealed class") + Microsoft Learn `IReadOnlyDictionary<TKey,TValue>` + Microsoft Learn `IReadOnlySet<T>` ("introduced in .NET 5") (canonical site: `compiler/ast.dart.md`; this spec is the canonical instance home for `UnifyResult`).
- `rf-dart-map-to-csharp-dictionary` — Dart `Map<K,V>` (`dart:core`, default `LinkedHashMap` insertion-ordered) → C# `Dictionary<TKey,TValue>` (`System.Collections.Generic`, Microsoft Learn: enumeration order "undefined") with `IReadOnlyDictionary` at the read-only property surface (root: `runtime/machine_state.dart.md`).
- `rf-dart-set-to-csharp-hashset` — Dart `Set<E>` (`dart:core`, default `LinkedHashSet` insertion-ordered) → C# `HashSet<T>` (Microsoft Learn: enumeration order "undefined") with `IReadOnlySet<T>` at the read-only property surface (root: `runtime/machine_state.dart.md` suspension-tracker family).
- `rf-dart-string-to-csharp-string` — Dart api.dart.dev `String` (UTF-16 code units, reference type with by-value equality) → C# Microsoft Learn `System.String` (UTF-16 code units, reference type with by-value equality; non-nullable under enabled NRT) (root: `compiler/error.dart.md`).

Cross-file deltas latent in this declaration file (recorded by reference in consumer specs `analyzer.dart.md` and `partial_evaluator.dart.md`): (a) `LinkedHashMap`/`LinkedHashSet` insertion-ordered vs `Dictionary`/`HashSet` undefined-order iteration; (b) `Map.operator[] → V?` returns null vs `Dictionary[k]` throws `KeyNotFoundException`; (c) static exhaustiveness verification gap for `switch` over the sealed ADT — consumers must emit a throwing default arm.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/lib/compiler/unify_result.dart.md` (convspec ratified, source_sha256 match `34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52`). Every construct in the source file (1 import directive, 1 doc comment, 1 sealed base class, 3 subclass arms — totalling 6 constructs) is enumerated in §2 and §3 and mapped to a cached idiom in §4. Convspec `escalations: []` and the convspec "Notes" section confirms zero escalations at convspec time; this plan reproduces that disposition. The Dart-3 closure-preservation argument (lift-to-shared collapses two library-local `sealed UnifyResult` declarations to one assembly-global `<root>.Compiler.UnifyResult` with the union closed-leaf set identical to each Dart library's closed-leaf set) is faithfully reproduced from convspec nuance (3). The record/record-struct rejection rationale (reference-identity contract preservation; heap-bound payload boxing avoidance) is faithfully reproduced from convspec nuance (1) and §"Why NOT `record`". The `IReadOnly*` payload-immutability projection (substitution / unbound-readers) is faithfully reproduced from convspec constructs C4 and C6 and the §"Why `IReadOnlyDictionary` at the property surface" / §"Why `IReadOnlySet` at the property surface" rationales. The runtime throwing-default-arm obligation at consumer sites is recorded by reference (consumer specs own the obligation; this declaration site forwards). No deviation from convspec; nothing invented.

## 6. Escalations

None.
