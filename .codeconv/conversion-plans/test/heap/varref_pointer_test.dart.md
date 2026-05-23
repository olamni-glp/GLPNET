---
path: test/heap/varref_pointer_test.dart
cycle_group_id: 130
scc_siblings: []
generated_at: 2026-05-21T16:01:33Z
source_sha256: 574fc311c885281ba54ebf6d357e14982f727a622c7707e0a5e5a67aa61b1ed7
schema_version: 1
---

# Conversion Plan: test/heap/varref_pointer_test.dart

## 1. Source Analysis

The Dart source file is a 180-line `package:test` test suite exercising
the `VarRef` term type and the `HeapFCP` dereferencing surface under the
Pointer Architecture (per `docs/heap-pointer-architecture-spec.md v3.0`).

Top-of-file:
- `library;` (bare, un-named library directive — Dart 2.12+ marker only;
  carries no name, no `part`, no `part of`).
- Three imports:
  - `package:test/test.dart` (test framework).
  - `package:glp_runtime/runtime/terms.dart` (SUT — `Term`, `ConstTerm`,
    `StructTerm`, `VarRef`).
  - `package:glp_runtime/runtime/heap_fcp.dart` (SUT — `HeapFCP`).
- `void main()` entrypoint containing exactly three top-level sibling
  `group(...)` calls, no other statements.

Group topology (flat — three sibling top-level groups, NO `setUp`,
NO `tearDown`, NO `late` field, NO nested groups, NO shared cross-test
state):

1. `'VarRef Structure - Pointer Architecture'` — 6 tests:
   - `'VarRef has only addr field'`
   - `'VarRef equality based on addr only'`
   - `'VarRef hashCode consistent with equality'`
   - `'Determine reader/writer by checking heap cell'`
   - `'VarRef can be used as struct argument'`
   - `'VarRef in nested structures'`
2. `'VarRef Dereferencing'` — 6 tests:
   - `'Dereference VarRef to writer returns VarRef when unbound'`
   - `'Dereference VarRef to reader returns VarRef to writer when unbound'`
   - `'Dereference VarRef returns value when bound'`
   - `'dereference method on heap works with VarRef term'`
   - `'dereference non-VarRef term returns itself'`
   - `'dereference StructTerm returns itself (no deep deref)'`
3. `'VarRef in Collections'` — 2 tests:
   - `'VarRef can be used in Set'`
   - `'VarRef can be used as Map key'`

Total: 14 `test(...)` calls — all synchronous closures, no `async`, no
`Future`, no `Stream`, no `Completer`, no `Timer`, no `skip:`, no
`timeout:`, no `retry:`.

Per-test surface inventory:
- ~30 `final <name> = <expr>;` local declarations (one+ per test).
- ~25 constructor-call sites without `new` (Dart 2 style):
  `VarRef(n)`, `HeapFCP()`, `ConstTerm(v)`, `StructTerm('f', [...])`.
- Four record-pattern destructure sites for
  `heap.allocateVariable()`'s `(int, int)` return:
  `final (writerAddr, readerAddr) = ...`, `final (_, readerAddr) = ...`,
  `final (writerAddr, _) = ...`, `final (_, r1) = ...`.
- Member property access: `ref.addr`, `struct.args[1]`,
  `(result as VarRef).addr`, `(result1 as ConstTerm).value`,
  `(struct.args[1] as VarRef).addr`, `ref1.hashCode`.
- Method calls on `heap`: `allocateVariable()`, `isWriter(addr)`,
  `isReader(addr)`, `derefAddr(addr)`, `bindWriter(addr, term)`,
  `dereference(term)`.
- Set/Map operations: `<VarRef>{ref1, ref2, ref3}`, `set.contains(...)`,
  `set.length`; `<VarRef, String>{}`, `map[VarRef(10)] = 'first'`,
  `map.length`, `map[VarRef(10)]`.
- Matchers exercised: `equals(int)`, `equals(string)`,
  `equals(VarRef)`, `isNot(equals(...))`, `isA<VarRef>()`,
  `isA<ConstTerm>()`, `isA<StructTerm>()`, `isTrue`, `isFalse`,
  `same(term)`, `same(struct)`.
- List literals: `[ConstTerm('a'), VarRef(readerAddr), ConstTerm('b')]`,
  `[VarRef(r1)]`, `[inner, VarRef(r2)]`, `[VarRef(readerAddr)]`.
- String literals (all single-quoted): `'f'`, `'a'`, `'b'`, `'inner'`,
  `'outer'`, `'hello'`, `'first'`, `'second'`, `'updated'`, plus the
  group/test label strings.
- No type declarations, no `late`, no `mixin`, no `extension`, no
  null-safety nuance, no value-vs-reference surprises (every SUT type is
  a sealed reference class per terms.dart.md / heap_fcp.dart.md).

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim
(`.codeconv/conversion-specs/test/heap/varref_pointer_test.dart.md`).

- **`library;` (bare library directive)** → Drop entirely. Emit no C#
  counterpart. C# has no per-file library declaration; the file
  participates in compilation by being in the project. Idiom:
  `rf-dart-library-directive-to-csharp-namespace-elision` (carry-forward
  from lib/runtime/terms.dart.md, KB cache hit per FR-012 / SC-007).

- **`import 'package:test/test.dart';`** → `using Xunit;` at file scope.
  xUnit is the batch-wide pinned framework (carry-forward from
  smoke_test.dart.md, boot_loader_test.dart.md, glp_runtime_test.dart.md,
  test/multiagent/*.dart.md). Idiom:
  `rf-dart-package-test-import-to-xunit-using`.

- **`import 'package:glp_runtime/runtime/terms.dart';` +
  `import 'package:glp_runtime/runtime/heap_fcp.dart';`** → collapse to
  ONE `using <RootNs>.Runtime;` directive (both SUT files lift their
  leaves into the same `<RootNs>.Runtime` namespace per their respective
  convspecs; codegen de-duplicates to avoid CS0105). Idiom:
  `rf-dart-internal-package-import-to-csharp-using` (carry-forward from
  terms.dart.md, heap_fcp.dart.md).

- **`void main() { group(...); group(...); group(...); }`** → eliminate
  entirely. xUnit discovery is attribute-driven (`[Fact]`); no per-file
  entrypoint. The three top-level `group(...)` calls promote to three
  test classes. Idiom: `rf-dart-package-test-main-omit-in-xunit`
  (carry-forward from boot_loader_test.dart.md).

- **Three flat sibling top-level `group(...)` blocks (no shared
  setUp/tearDown)** → emit ONE C# test class per group, three classes
  total in the same file under file-scoped
  `namespace <RootNs>.Test.Heap;`:
  - `public class VarRefStructurePointerArchitectureTests`
  - `public class VarRefDereferencingTests`
  - `public class VarRefInCollectionsTests`
  No constructor, no `IDisposable.Dispose`, no `IClassFixture<T>` on any
  class (no shared state in source). Idiom:
  `rf-dart-package-test-group-to-xunit-class` (carry-forward from
  boot_loader_test.dart.md, specialised to the "flat siblings, no shared
  state" sub-case).

- **Each `test('<name>', () { <body> })` call** → one
  `[Fact(DisplayName = "<original Dart test label>")] public void
  <PascalCasedIdentifier>() { <body> }` method per test on the enclosing
  class. All 14 methods return `void` (every closure is synchronous; no
  async/Future surface). Identifier PascalCasing strips whitespace,
  hyphens, parentheses, and apostrophes. Idiom:
  `rf-dart-test-callback-to-xunit-method-body` (carry-forward from
  smoke_test.dart.md).

- **`final <name> = <expr>;` (~30 sites)** → `var <name> = <expr>;`
  Trade-off accepted: `var` loses `final`'s rebind-prevention guarantee;
  mitigated by single-method-scope visibility and code review (precedent:
  global_writers_table_test.dart.md, external_io.dart.md). Idiom:
  `rf-dart-final-local-to-csharp-var-local`.

- **Constructor calls without `new` (~25 sites: `VarRef(42)`,
  `HeapFCP()`, `ConstTerm('a')`, `ConstTerm(42)`,
  `StructTerm('f', [...])`)** → emit `new <Ctor>(args)` at every site.
  C# requires `new` for reference-type construction; target-typed
  `new()` is incompatible with the `var` local style used here. Idiom:
  `rf-dart-constructor-call-no-new-to-csharp-new-keyword` (FIRST-SEEN —
  defines a new active idiom in this convspec).

- **Member property access (`ref.addr`, `struct.args[1]`,
  `(result as VarRef).addr`, `(result1 as ConstTerm).value`,
  `(struct.args[1] as VarRef).addr`)** → `obj.Property` (C# auto-
  property; PascalCase names per terms.dart.md / heap_fcp.dart.md
  precedent: `Addr`, `Functor`, `Args`, `Value`). Index access on `Args`
  works because `IReadOnlyList<Term>` exposes `this[int]`. The Dart `as`
  cast becomes a C# explicit cast `((VarRef)x).Addr` (NOT `x as VarRef`,
  which returns `null` in C# — different semantic). Idiom:
  `rf-dart-list-indexer-to-csharp-list-indexer` (carry-forward);
  explicit-cast convention from
  `rf-dart-as-cast-to-csharp-explicit-cast` (heap_fcp.dart.md).

- **`ref1.hashCode` property access** → `ref1.GetHashCode()` method
  call. Dart `hashCode` is a property; C# `GetHashCode()` is a method
  inherited from `System.Object`. Carry-forward implicit in
  terms.dart.md's `IEquatable<VarRef>` + `GetHashCode()` decision.

- **Four record-destructure sites (`final (a, b) = heap.allocateVariable();`
  with two `_` discards)** → `var (writerAddr, readerAddr) =
  heap.AllocateVariable();`, `var (_, readerAddr) = ...`,
  `var (writerAddr, _) = ...`, `var (_, r1) = ...`. C# discard `_` is
  identical syntax to Dart. `AllocateVariable()` returns
  `(int, int)` value-tuple per heap_fcp.dart.md's
  `dart.tuple_return.record_two_int_addresses_allocate_variable`. Idiom:
  `rf-dart-record-destructure-to-csharp-valuetuple-deconstruction`
  (carry-forward from external_io.dart.md).

- **`expect(x, isA<T>())` (~10 sites with `VarRef`, `ConstTerm`,
  `StructTerm`)** → `Assert.IsType<T>(actual)`. All target leaves are
  `sealed` per terms.dart.md, so the EXACT-type assertion is
  semantically tight (would FAIL loudly if a leaf ever gained a subclass).
  Idiom: `rf-dart-expect-isA-to-xunit-assert-istype` (FIRST-SEEN in this
  convspec).

- **`expect(actual, equals(int|string|VarRef))` (multiple sites)** →
  `Assert.Equal(expected, actual)` — ARGUMENT-ORDER SWAP (xUnit puts
  expected FIRST; Dart puts actual FIRST). Codegen MUST swap at every
  site. For `VarRef` comparand, dispatches to `IEquatable<VarRef>.Equals`
  via `EqualityComparer<VarRef>.Default` (terms.dart.md's pinned
  contract). Idiom:
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder`
  (carry-forward from smoke_test.dart.md; footgun explicitly recorded).

- **`expect(ref1, isNot(equals(ref3)))`** →
  `Assert.NotEqual(ref3, ref1)` (same argument-order swap). Idiom:
  `rf-dart-expect-isNot-equals-to-xunit-assert-notequal` (FIRST-SEEN).
  Routing-table family with boot_loader_test.dart.md's
  `isNot(contains(X))` → `Assert.DoesNotContain(X, actual)`.

- **`expect(actual, isTrue)` (4 sites)** → `Assert.True(actual)`. Both
  operands are strict `bool` (no truthy coercion). Idiom:
  `rf-dart-expect-isTrue-to-xunit-assert-true` (carry-forward).

- **`expect(actual, isFalse)` (2 sites)** → `Assert.False(actual)`.
  First exercise of the `isFalse` matcher in the convspec corpus
  (promoted from smoke_test.dart.md's routing-table nuance to a
  first-class idiom). Idiom:
  `rf-dart-expect-isFalse-to-xunit-assert-false` (FIRST-SEEN, promoted).

- **`expect(result, same(term))` and `expect(result, same(struct))`** →
  `Assert.Same(term, result)` / `Assert.Same(struct, result)` — REFERENCE-
  IDENTITY assertion (uses `Object.ReferenceEquals`). Same argument-
  order swap (expected first). LOAD-BEARING: validates the no-copy
  contract of `HeapFCP.Dereference` from heap_fcp.dart.md (
  `dart.method.dereference_term_with_varref_chase`). Distinct from
  `Assert.Equal` (which would dispatch to `IEquatable`); must use
  `Assert.Same` to assert exact reference identity. Idiom:
  `rf-dart-expect-same-to-xunit-assert-same` (FIRST-SEEN, LOAD-BEARING).

- **List literals (`[ConstTerm('a'), VarRef(readerAddr), ConstTerm('b')]`,
  `[VarRef(r1)]`, `[inner, VarRef(r2)]`, `[VarRef(readerAddr)]`)** →
  `new List<Term> { ... }` collection-initialiser at each site. C# 12
  collection-expression `[...]` is an alternative; spec preference is
  the explicit `new List<Term> { ... }` form (carry-forward from
  terms.dart.md). Idiom: `rf-dart-list-literal-to-csharp-list-initializer`.

- **`<VarRef>{ref1, ref2, ref3}` (typed set literal)** →
  `new HashSet<VarRef> { ref1, ref2, ref3 }`. `HashSet<VarRef>` uses
  `EqualityComparer<VarRef>.Default` → `IEquatable<VarRef>.Equals` +
  `GetHashCode()` (terms.dart.md's pinned equality contract), so
  `new VarRef(10)` and `new VarRef(10)` de-duplicate. Final
  `set.Count == 2`. Ordering caveat recorded in nuance:
  `LinkedHashSet` (Dart) is insertion-ordered; `HashSet<T>` (C#) is
  unordered — irrelevant here (only `Count` and `Contains` asserted).
  Idiom: `rf-dart-set-literal-typed-to-csharp-hashset-initializer`
  (FIRST-SEEN).

- **`<VarRef, String>{}` + indexer-set writes** →
  `var map = new Dictionary<VarRef, string>();` then
  `map[new VarRef(10)] = "first";`, etc. C# `Dictionary<TKey,TValue>`
  indexer-set is "put-or-update" (matches Dart `Map`); DO NOT use
  `Dictionary.Add` (throws on duplicate — different semantic). The third
  write at key `VarRef(10)` UPDATES rather than ADDS because
  `EqualityComparer<VarRef>.Default` dispatches to
  `IEquatable<VarRef>.Equals` (terms.dart.md's contract). `map.length` →
  `map.Count`. Idiom:
  `rf-dart-map-literal-typed-to-csharp-dictionary` (FIRST-SEEN).

- **String literals (`'f'`, `'a'`, `'b'`, `'inner'`, `'outer'`,
  `'hello'`, `'first'`, `'second'`, `'updated'`, plus group/test
  labels)** → C# double-quoted string literals (`"f"`, `"a"`, etc.). C#
  `'x'` is `char`, not `string` — MUST use double quotes. No
  interpolation, no raw strings, no triple-quoted strings in this file.
  Idiom: `rf-dart-string-literal-to-csharp-string-literal` (FIRST-SEEN,
  promoted).

Output file shape (per `conversion_units` in convspec):
1. File-scoped `namespace <RootNs>.Test.Heap;`
2. `using Xunit;`
3. `using <RootNs>.Runtime;` (single, de-duplicated)
4. `public class VarRefStructurePointerArchitectureTests` with 6
   `[Fact]` methods: `VarRefHasOnlyAddrField`,
   `VarRefEqualityBasedOnAddrOnly`,
   `VarRefHashCodeConsistentWithEquality`,
   `DetermineReaderWriterByCheckingHeapCell`,
   `VarRefCanBeUsedAsStructArgument`, `VarRefInNestedStructures`.
5. `public class VarRefDereferencingTests` with 6 `[Fact]` methods:
   `DereferenceVarRefToWriterReturnsVarRefWhenUnbound`,
   `DereferenceVarRefToReaderReturnsVarRefToWriterWhenUnbound`,
   `DereferenceVarRefReturnsValueWhenBound`,
   `DereferenceMethodOnHeapWorksWithVarRefTerm`,
   `DereferenceNonVarRefTermReturnsItself`,
   `DereferenceStructTermReturnsItselfNoDeepDeref`.
6. `public class VarRefInCollectionsTests` with 2 `[Fact]` methods:
   `VarRefCanBeUsedInSet`, `VarRefCanBeUsedAsMapKey`.
7. All 14 methods carry `[Fact(DisplayName = "<original Dart label>")]`.
8. No constructor, no `IDisposable`, no `void main()`, no `library;`.

## 3. Decomposed Task Units

- T1: emit file-scoped `namespace <RootNs>.Test.Heap;` header — done.
- T2: emit `using Xunit;` — done.
- T3: emit single `using <RootNs>.Runtime;` collapsing the two Dart SUT
  imports — done.
- T4: drop `void main()` and the `library;` directive (no C# emission) —
  done.
- T5: emit `public class VarRefStructurePointerArchitectureTests` shell
  (no ctor, no IDisposable) — done.
- T6: emit 6 `[Fact(DisplayName=...)]` methods inside T5's class,
  PascalCase identifiers, `void` return, body translated 1-to-1
  (`var` locals, `new <Ctor>(...)` calls, `var (a, b) =
  heap.AllocateVariable()` destructures, `Assert.True/False/Equal/
  NotEqual/IsType<T>` per matcher) — done.
- T7: emit `public class VarRefDereferencingTests` shell — done.
- T8: emit 6 `[Fact(DisplayName=...)]` methods inside T7's class
  exercising `heap.DerefAddr`, `heap.BindWriter`, `heap.Dereference`,
  using `Assert.IsType<T>`, `Assert.Equal(expected, actual)`,
  `Assert.Same(expected, actual)` per matcher — done.
- T9: emit `public class VarRefInCollectionsTests` shell — done.
- T10: emit 2 `[Fact(DisplayName=...)]` methods inside T9's class
  building `new HashSet<VarRef> { ... }` and
  `new Dictionary<VarRef, string>()`, exercising indexer-set
  put-or-update and `Assert.Equal(int, set.Count)` /
  `Assert.True(set.Contains(...))` /
  `Assert.Equal("updated", map[new VarRef(10)])` — done.
- T11: ensure every `expect(actual, equals(X))` /
  `expect(actual, isNot(equals(X)))` / `expect(actual, same(X))` site
  applies the expected-first argument-order swap relative to xUnit —
  done.
- T12: ensure every Dart single-quoted string literal emits as a C#
  double-quoted string literal — done.

## 4. Research Findings

None required. All idioms invoked by this conversion plan are either
carry-forward KB cache hits per FR-012 / SC-007 (verbatim reuse from
terms.dart.md, heap_fcp.dart.md, smoke_test.dart.md,
boot_loader_test.dart.md, glp_runtime_test.dart.md,
external_io.dart.md, global_writers_table_test.dart.md) or FIRST-SEEN
idioms already registered with authoritative both-sides citations in the
ratified convspec's "Rationale and research provenance" section
(`rf-dart-constructor-call-no-new-to-csharp-new-keyword`,
`rf-dart-expect-isA-to-xunit-assert-istype`,
`rf-dart-expect-isNot-equals-to-xunit-assert-notequal`,
`rf-dart-expect-isFalse-to-xunit-assert-false`,
`rf-dart-expect-same-to-xunit-assert-same`,
`rf-dart-set-literal-typed-to-csharp-hashset-initializer`,
`rf-dart-map-literal-typed-to-csharp-dictionary`,
`rf-dart-string-literal-to-csharp-string-literal`). The convspec
records zero escalations.

## 5. Consistency Pass

fixed — derived from
`.codeconv/conversion-specs/test/heap/varref_pointer_test.dart.md`
(ratified mirror; schema_version 1; source_sha256 matches this plan's
header byte-for-byte:
`574fc311c885281ba54ebf6d357e14982f727a622c7707e0a5e5a67aa61b1ed7`).
All §2 construct decisions mirror the convspec's `constructs:` entries
verbatim. Cross-file dependencies on terms.dart.md and heap_fcp.dart.md
are cited where they pin types/namespaces/equality contracts the test
file relies on. The convspec's own `escalations: []` is reproduced in §6.

## 6. Escalations

None.
