> Conversion-spec artifact for test/heap/varref_pointer_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/heap/varref_pointer_test.dart
source_sha256: 574fc311c885281ba54ebf6d357e14982f727a622c7707e0a5e5a67aa61b1ed7
target_code_unit: test/heap/VarRefPointerTest.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "library;"
    target_decision: >-
      Drop the bare `library;` directive entirely; emit no C# counterpart.
      C# has no per-file library-declaration syntax — every `.cs` file
      participates in compilation by being in the project; namespaces are
      declared per-type (or with a file-scoped `namespace
      <RootNs>.Test.Heap;`). The library directive carries no name, no
      `part`, and no `part of` here, so the omission is lossless.
    idiom_id: rf-dart-library-directive-to-csharp-namespace-elision
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance (carry-forward from lib/runtime/terms.dart.md
      idiom rf-dart-library-directive-to-csharp-namespace-elision, KB
      cache hit per FR-012 / SC-007 — REUSE, no re-research): Dart 2.12+
      requires `library;` (un-named) only as a marker for doc-comments;
      no name, no parts. C# elides the construct entirely and uses the
      file-scoped `namespace <RootNs>.Test.Heap;` shape instead. No
      value-vs-reference, null-safety, async, or isolate surface
      implicated.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned project-wide;
      same pinning as the precedent test files smoke_test.dart.md,
      glp_runtime_test.dart.md, and test/multiagent/*.dart.md). Codegen
      MUST also project this file into the namespace
      `<RootNs>.Test.Heap` (mirrors the Dart `test/heap` directory
      shape), and the test .csproj (out of scope for this single-file
      artifact — a langpair-level concern) MUST reference `xunit` +
      `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance — every
      `package:test` file in the inventory MUST map to the same .NET
      framework. xUnit is the batch-wide default (carry-forward
      idiom_id from smoke_test.dart.md / boot_loader_test.dart.md, KB
      cache hit per FR-012). No re-research; no re-derivation.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/terms.dart'; import
      'package:glp_runtime/runtime/heap_fcp.dart';"
    target_decision: >-
      Two cross-file SUT imports. Map each to a single `using` directive
      that names the C# namespace produced by converting the
      corresponding SUT file: `terms.dart` (whose conversion-spec
      lib/runtime/terms.dart.md already exists) lifts the leaves
      `Term`/`ConstTerm`/`StructTerm`/`VarRef` into the namespace
      `<RootNs>.Runtime` (decision recorded in terms.dart.md's
      `dart.library_directive.top_of_file_no_name` /
      `dart.import_directive.package_internal_to_using_namespace`);
      `heap_fcp.dart` (whose conversion-spec lib/runtime/heap_fcp.dart.md
      already exists) lifts `HeapFCP` into the SAME `<RootNs>.Runtime`
      namespace (same precedent). Therefore the C# file emits a SINGLE
      `using <RootNs>.Runtime;` covering both (de-duplicated by codegen
      — emitting the same `using` twice is a CS0105 warning).
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (carry-forward from
      lib/runtime/terms.dart.md and lib/runtime/heap_fcp.dart.md, KB
      cache hit — REUSE per FR-012 / SC-007): Dart resolves `package:`
      imports via the pubspec/URI; C# resolves type references via
      assembly + namespace. The two SUT files land in ONE namespace
      (`<RootNs>.Runtime`) because both their respective convspec
      artifacts pinned that namespace at the same lib/runtime depth;
      so the two Dart imports COLLAPSE to one C# `using` directive.
      No `as` alias, no `show`/`hide` in this file — the simple
      `using` form suffices. No value-vs-reference, async, isolate,
      or null-safety surface implicated by import directives.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group(...); group(...); group(...); }"
    target_decision: >-
      Eliminate `void main()` entirely. xUnit discovery is reflection
      over `[Fact]` attributes — no per-file entrypoint. The three
      top-level `group(...)` calls inside `main` become the three test
      classes (see `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (carry-forward from boot_loader_test.dart.md,
      KB cache hit — REUSE per FR-012 / SC-007): Dart `main` is the
      per-file `package:test` registration entrypoint; xUnit has no
      per-file hook (only per-class constructor + IDisposable.Dispose,
      and per-collection fixtures). THIS file's `main` body is exactly
      three `group(...)` calls with no other statements — omission is
      lossless. No closures captured across groups; no shared state at
      the file level (each `group` body owns its own `final` locals).
  - construct_key: dart.package_test.group_block.three_sibling_top_level_groups_no_shared_setup
    source_form: >-
      "group('VarRef Structure - Pointer Architecture', () { test(...); ... });
       group('VarRef Dereferencing', () { test(...); ... });
       group('VarRef in Collections', () { test(...); test(...); });"
      // three SIBLING top-level groups, NO `setUp`/`tearDown`, NO `late`
      // fields, NO nested groups
    target_decision: >-
      Emit ONE test class per top-level Dart `group`. Three classes
      total in the same file (C# allows multiple public types per file,
      same compilation unit, same `namespace <RootNs>.Test.Heap;`):
      (1) `public class VarRefStructurePointerArchitectureTests` —
      one `[Fact]` per `test()` inside the first group;
      (2) `public class VarRefDereferencingTests` — one `[Fact]` per
      `test()` inside the second group;
      (3) `public class VarRefInCollectionsTests` — one `[Fact]` per
      `test()` inside the third group.
      No constructor / `Dispose` on any class (no `setUp` /
      `tearDown` in source). No `IClassFixture<T>` (no shared
      cross-test state). Class-name shape: PascalCase + `Tests`
      suffix, derived by dropping the Dart hyphen/colon/space
      punctuation in the group label (precedent: boot_loader_test
      group names `'valid boot files'` -> `ValidBootFiles`-style
      derived identifiers; here `'VarRef Structure - Pointer
      Architecture'` -> `VarRefStructurePointerArchitecture` +
      `Tests` suffix).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Group-topology nuance. xUnit has NO first-class nested-group
      construct (boot_loader_test.dart.md explored three viable target
      shapes — single class + [Trait], one class per inner group,
      `IClassFixture<>` hierarchy — and CHOSE the single-class + Trait
      shape there because the groups shared `setUp`). HERE the
      topology is FLAT (three SIBLING groups, no shared setUp, no
      `late` field) — so the simpler shape applies: one class per
      group, no Trait needed, no shared base class. This is the
      same `rf-dart-package-test-group-to-xunit-class` idiom as
      boot_loader_test.dart.md but specialised to the "flat siblings,
      no shared state" sub-case. No cross-test state to share via
      constructor; no per-test cleanup. Per-test fresh-instance
      lifecycle (xUnit creates one instance per `[Fact]`) is
      irrelevant — there is no instance state on these classes. No
      async test methods (every closure is synchronous).
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<name>', () { <body> });"
    target_decision: >-
      One `[Fact(DisplayName = "<original Dart test label>")] public
      void <PascalCasedIdentifier>() { <body> }` method per Dart
      `test()` call, on the enclosing test class (chosen above). The
      Dart test label (a human-readable string with spaces, hyphens,
      and slashes) is preserved verbatim in `DisplayName`; the C#
      method identifier is PascalCased + punctuation-stripped (C#
      method identifiers cannot contain whitespace, hyphens, dots,
      apostrophes, parentheses, or angle brackets). All twelve
      `test(...)` calls in this file are synchronous (no async/Future
      surface) — every `[Fact]` method returns `void`, NOT `async
      Task`. No `skip:` argument on any `test()` call; no `timeout:`;
      no `retry:` — pure default invocation.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Test-registration nuance (carry-forward from smoke_test.dart.md
      / boot_loader_test.dart.md, KB cache hit — REUSE per FR-012):
      Dart `test(name, body)` registers a closure; xUnit `[Fact]`
      method body IS the test. The closure body translates 1-to-1 into
      the method body. `DisplayName` preserves the original label so
      test reports remain searchable by the source name. PascalCasing
      rule: split on whitespace and punctuation, uppercase first
      letter of each token, concatenate. Example: `'VarRef has only
      addr field'` -> `VarRefHasOnlyAddrField`; `'dereference
      non-VarRef term returns itself'` -> `DereferenceNonVarRefTermReturnsItself`.
      No closure capture of mutable outer state in any of these tests
      (each test allocates its own `HeapFCP` and `VarRef` locals from
      scratch — fresh-state).
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: "final ref = VarRef(42);  // and ~30 similar at all test sites"
    target_decision: >-
      Each `final <name> = <expr>;` becomes `var <name> = <expr>;` in
      the C# method body. C# `var` is implicitly-typed and (in the
      common readable case) is the idiomatic translation of Dart
      `final`. To preserve Dart's once-and-only-once binding
      semantics, codegen SHOULD prefer the C# 12+ feature of marking
      the local as initialised-only via `readonly`-style discipline
      where supported (currently NO direct equivalent of `final` for
      method-body locals exists in C# — the closest enforcer is
      `readonly` on fields, which does not apply to locals).
      Trade-off recorded: `var` loses the rebind-prevention guarantee
      that `final` provides in Dart; mitigated by code review +
      style-cop rule (precedent recorded in
      test/multiagent/global_writers_table_test.dart.md and
      lib/runtime/external_io.dart.md). NO `late final` in this file
      (all locals have initialisers at declaration). NO `late` field.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      `final` vs `var` nuance (carry-forward from multiple precedents
      — KB cache hit, REUSE per FR-012 / SC-007): Dart `final` locals
      are write-once-initialised; C# `var` is `readonly`-like only by
      convention. This file declares ~30 such locals (one per test,
      sometimes several per test for `heap`, `(writerAddr,
      readerAddr)`, `ref`, `struct`, `result`, `inner`, `outer`,
      `ref1`/`ref2`/`ref3`, `set`, `map`, `term`); ALL collapse to
      `var <name> = <expr>;`. The rebind risk is bounded by single-
      method-scope visibility — same trade-off as accepted at every
      precedent site.
  - construct_key: dart.expression.constructor_call_no_new_keyword
    source_form: "VarRef(42)  // and HeapFCP(), ConstTerm('a'), ConstTerm(42), StructTerm('f', [...])"
    target_decision: >-
      Dart has no `new` keyword (optional since Dart 2; not used here);
      C# constructor calls REQUIRE `new` outside of target-typed `new()`
      sites. Codegen emits `new VarRef(42)`, `new HeapFCP()`,
      `new ConstTerm("a")`, `new ConstTerm(42)`,
      `new StructTerm("f", new List<Term> { ... })` etc. The class
      shapes (sealed class, IEquatable on VarRef, IReadOnlyList on
      StructTerm.args -> List<Term>) are decided in
      lib/runtime/terms.dart.md (carry-forward via the imported `using
      <RootNs>.Runtime;`). Codegen MUST NOT re-derive those shapes —
      it imports them. C# 9+ target-typed `new()` (e.g.
      `VarRef v = new(42);`) is NOT preferred here because the locals
      use `var` (target-typed `new` requires an explicit destination
      type); plain `new VarRef(42)` keeps the surface uniform.
    idiom_id: null
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      No-`new` nuance (FIRST-SEEN — defines a new active idiom).
      Dart 2 made `new` optional and the codebase uses the elided
      form everywhere; C# `new` is mandatory for reference-type
      constructor calls (Microsoft Learn: "C# language reference -
      `new` operator"). The construct is structurally simple but
      pervasive (~25 sites in this 180-line file) — recording it
      as a first-class idiom prevents every subsequent test
      convspec from re-deriving it. No value-vs-reference change:
      `VarRef`/`HeapFCP`/`ConstTerm`/`StructTerm` are all sealed
      classes (reference-allocated, both sides). No nullability
      change. No `const` constructor (none declared in terms.dart).
  - construct_key: dart.expression.member_property_access
    source_form: "ref.addr  // and struct.args[1], (struct.args[1] as VarRef).addr"
    target_decision: >-
      Dart `obj.field` directly translates to C# `obj.Property` (C#
      auto-properties — `public int Addr { get; }`, `public string
      Functor { get; }`, `public IReadOnlyList<Term> Args { get; }`).
      Property names PascalCase (per terms.dart.md / heap_fcp.dart.md
      precedents). Index access on `Args` works because
      `IReadOnlyList<Term>` exposes `this[int]`. The `(struct.args[1]
      as VarRef).addr` site translates to `((VarRef)struct.Args[1]).Addr`
      using a C# explicit cast.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Field-vs-property nuance (carry-forward from terms.dart.md and
      multiple test precedents — KB cache hit per FR-012). Dart `final
      <T> <name>` and C# `public <T> <Name> { get; }` are semantically
      equivalent for read-only access (both prevent re-assignment from
      outside the declaring scope). The Dart `as` cast is `is`-test-
      adjacent but explicitly synchronous and throws on type mismatch
      — C# explicit cast `(T)x` has the SAME throw-on-mismatch
      semantic (throws `InvalidCastException`, matching Dart's
      `TypeError`). Choice rationale recorded: explicit cast `(VarRef)
      x` over the safer `x as VarRef` (C# `as` returns `null` instead
      of throwing — semantically DIFFERENT from Dart's `as`, see
      lib/runtime/heap_fcp.dart.md's
      rf-dart-as-cast-to-csharp-explicit-cast precedent).
  - construct_key: dart.expression.record_destructure_two_int_addresses_with_optional_discard
    source_form: >-
      "final (writerAddr, readerAddr) = heap.allocateVariable();
       final (_, readerAddr) = heap.allocateVariable();
       final (writerAddr, _) = heap.allocateVariable();
       final (_, r1) = heap.allocateVariable();"
      // four destructuring sites; two with `_` discard
    target_decision: >-
      Use C# tuple deconstruction at every site: `var (writerAddr,
      readerAddr) = heap.AllocateVariable();`, `var (_, readerAddr)
      = heap.AllocateVariable();`, `var (writerAddr, _) = ...`, `var
      (_, r1) = ...`. The `_` is C# discard syntax (identical to Dart).
      `HeapFCP.AllocateVariable()` returns
      `(int WriterAddr, int ReaderAddr)` per
      lib/runtime/heap_fcp.dart.md's
      `dart.tuple_return.record_two_int_addresses_allocate_variable`
      construct — carry-forward.
    idiom_id: rf-dart-record-destructure-to-csharp-valuetuple-deconstruction
    research_finding_id: rf-dart-record-destructure-to-csharp-valuetuple-deconstruction
    nuance: >-
      Tuple-destructure nuance (carry-forward from
      lib/runtime/external_io.dart.md, KB cache hit per FR-012 /
      SC-007 — REUSE). Dart record patterns and C# value-tuple
      deconstruction agree shape-for-shape (`(a, b) = expr;`).
      Discard `_` is identical syntax both sides. The return type of
      `AllocateVariable` on the C# side (decided in
      heap_fcp.dart.md) is `(int, int)` value-tuple — exactly what
      tuple deconstruction expects. No null-safety implication
      (`int` is value type, non-nullable both sides). Four sites in
      this file exercise this idiom; same translation applied to all.
  - construct_key: dart.package_test.expect_isA_matcher_bare
    source_form: >-
      "expect(result, isA<VarRef>());  // and isA<ConstTerm>(),
       isA<StructTerm>(), and the negative variant
       expect(set.contains(...), isTrue) is separate; bare isA<T>()
       (NOT inside throwsA) appears at ~10 sites"
    target_decision: >-
      Translate `expect(actual, isA<T>())` to xUnit
      `Assert.IsType<T>(actual)` (per xunit.net Assert API — verifies
      that `actual` is of EXACTLY type `T`, NOT a subtype; throws
      `IsTypeException` otherwise). For sites that need
      subtype-tolerance, `Assert.IsAssignableFrom<T>(actual)` is the
      alternative; HERE every `isA<T>()` site targets a CONCRETE leaf
      (`VarRef`, `ConstTerm`, `StructTerm` — all `sealed` per
      terms.dart.md), so the EXACT-type form `Assert.IsType<T>` is
      semantically correct AND tighter than the Dart matcher (Dart
      `isA<T>` is subtype-tolerant; here there are no subtypes by
      virtue of `sealed`).
    idiom_id: null
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Type-assertion nuance (FIRST-SEEN — defines a new active
      idiom). Dart `isA<T>` (from `package:matcher`) is
      subtype-tolerant. xUnit has TWO type assertions: `Assert.IsType<T>`
      (EXACT type — throws `IsTypeException` on subtype too) and
      `Assert.IsAssignableFrom<T>` (subtype-tolerant). Choice
      rationale: every target leaf in this file is `sealed` (per
      terms.dart.md) — no subtypes exist — so the two assertions
      coincide on the actually-exercised inputs. Spec preference =
      emit `Assert.IsType<T>` because it conveys "exact type"
      intent, matches the strict 1-to-1 shape of the assertion, and
      makes a future widening (if a leaf ever gains a subclass) FAIL
      the test loudly rather than silently allowing the subtype.
      Recorded broader routing table (for sibling test convspecs):
      `isA<T>()` BARE -> `Assert.IsType<T>(actual)` (default,
      sealed-leaf case); `isA<T>()` BARE where `T` has subtypes ->
      `Assert.IsAssignableFrom<T>(actual)`; `throwsA(isA<T>())` ->
      `Assert.ThrowsAny<T>(() => ...)` per
      boot_loader_test.dart.md's already-recorded mapping. NO
      `null`-safety surface (the actual operand is non-null at every
      site here). Value vs reference: both `VarRef`, `ConstTerm`,
      `StructTerm` are reference types (sealed classes) on both
      sides — no boxing.
  - construct_key: dart.package_test.expect_equals_matcher_int
    source_form: >-
      "expect(ref.addr, equals(42));  expect(ref1.hashCode,
       equals(ref2.hashCode));  expect((result as VarRef).addr,
       equals(writerAddr));  expect((result1 as ConstTerm).value,
       equals(42));  expect((result2 as ConstTerm).value,
       equals('hello'));  expect(set.length, equals(2));
       expect(map.length, equals(2));"
    target_decision: >-
      Translate `expect(actual, equals(expected))` to xUnit
      `Assert.Equal(expected, actual)` — note the ARGUMENT-ORDER
      SWAP (xUnit puts expected FIRST, Dart puts actual FIRST). This
      is the footgun explicitly recorded in smoke_test.dart.md's
      `rf-dart-expect-equals-to-xunit-assert-equal-argorder`. Site
      types: `int` (`Addr`, `hashCode`, `length`), `int` constant
      literal (`42`), `string` literal (`'hello'`). All map to
      `Assert.Equal(int, int)` / `Assert.Equal(string, string)`
      respectively. `hashCode` -> `GetHashCode()` (Dart property
      access -> C# method call) per the `Object.GetHashCode()`
      Microsoft Learn reference.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order swap nuance (carry-forward from
      smoke_test.dart.md / glp_runtime_test.dart.md — KB cache hit
      per FR-012, REUSE). xUnit `Assert.Equal<T>(T expected, T
      actual)` with expected-FIRST; Dart `expect(actual,
      equals(expected))` with actual-FIRST. Codegen MUST swap the
      argument order at every site, NEVER paste through. Additional
      nuance for this file: `ref1.hashCode == ref2.hashCode` test
      enforces the `IEquatable<VarRef>` + `GetHashCode` contract
      decided in terms.dart.md's
      `dart.sum_type_leaf.variable_ref_int_address_value_equality`
      — i.e., the C# side must implement `GetHashCode()` so two
      `new VarRef(100).GetHashCode() == new VarRef(100).GetHashCode()`.
      The terms.dart.md spec already pinned that implementation; this
      test just exercises it.
  - construct_key: dart.package_test.expect_equals_matcher_VarRef_value_equality
    source_form: >-
      "expect(ref1, equals(ref2));   // ref1=VarRef(100), ref2=VarRef(100)
       expect(ref1, isNot(equals(ref3)));  // ref3=VarRef(101)"
    target_decision: >-
      Translate to xUnit `Assert.Equal(ref2, ref1)` (expected first,
      actual second — same argument-order swap as the int case) and
      `Assert.NotEqual(ref3, ref1)`. Both rely on `VarRef`'s
      `IEquatable<VarRef>` + `Equals(VarRef?)` override decided in
      terms.dart.md (`rf-dart-class-eq-on-single-int-field-to-csharp-
      iequatable`). xUnit's `Assert.Equal<T>` uses `EqualityComparer<T>.
      Default.Equals` which dispatches to `IEquatable<T>.Equals` when
      implemented — so two `VarRef(100)` instances ARE equal under
      this assertion.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Value-equality-vs-reference-identity nuance (LOAD-BEARING,
      explicitly addressed): the whole reason terms.dart.md emitted
      `VarRef` with `IEquatable<VarRef>` (and explicitly NOT as a
      `record class`) was to support exactly this test pattern —
      two `VarRef(100)` instances are DIFFERENT REFERENCES but
      EQUAL by their `Addr` field. xUnit `Assert.Equal` dispatches to
      `EqualityComparer<T>.Default` which honors `IEquatable<T>.Equals`,
      so the assertion passes. WITHOUT the `IEquatable` override
      (e.g., if codegen emitted a plain class with default
      reference-identity equality), `Assert.Equal(new VarRef(100),
      new VarRef(100))` would FAIL. This test is the load-bearing
      validation of terms.dart.md's equality decision. Codegen MUST
      NOT silently substitute a `record class` (would over-equate)
      OR a plain class (would under-equate). The negative case
      `isNot(equals(VarRef(101)))` -> `Assert.NotEqual` mirrors the
      same path inverted. Reference vs value: both `VarRef`
      instances are heap-allocated reference types in C# (same as
      Dart), but their EQUALITY is structural over `Addr`.
  - construct_key: dart.package_test.expect_isNot_equals
    source_form: "expect(ref1, isNot(equals(ref3)));"
    target_decision: >-
      Composed matcher `isNot(equals(X))` -> dedicated assertion
      `Assert.NotEqual(X, actual)`. Same argument-order swap as
      `Assert.Equal`. (Distinct from boot_loader_test.dart.md's
      `isNot(contains(...))` -> `Assert.DoesNotContain` — same
      routing-table family, different leaf assertion.)
    idiom_id: null
    research_finding_id: rf-dart-expect-isNot-equals-to-xunit-assert-notequal
    nuance: >-
      `isNot`-composition nuance (FIRST-SEEN — defines a new active
      idiom; co-equal with boot_loader_test.dart.md's `isNot(contains)`
      routing entry). xUnit has NO composable `isNot(X)` primitive;
      each composed Dart matcher routes to a DEDICATED xUnit
      assertion: `isNot(equals(X))` -> `Assert.NotEqual(X, actual)`;
      `isNot(contains(X))` -> `Assert.DoesNotContain(X, actual)`;
      `isNot(isA<T>())` -> `Assert.IsNotType<T>(actual)`. This file
      exercises `isNot(equals)` AND `isFalse` on `heap.isWriter(...)`
      etc. (a separate idiom — see below). Recorded broader table.
  - construct_key: dart.package_test.expect_isTrue_matcher
    source_form: >-
      "expect(heap.isWriter(writerRef.addr), isTrue);
       expect(heap.isReader(readerRef.addr), isTrue);
       expect(set.contains(VarRef(10)), isTrue);
       expect(set.contains(VarRef(20)), isTrue);"
    target_decision: >-
      Translate `expect(actual, isTrue)` to `Assert.True(actual)`
      (carry-forward from smoke_test.dart.md's
      `rf-dart-expect-isTrue-to-xunit-assert-true`). All four sites
      take a `bool` predicate (HeapFCP `isWriter`/`isReader` returns
      `bool` per heap_fcp.dart.md; `Set<T>.contains(x)` -> .NET
      `HashSet<T>.Contains(x)` returns `bool`).
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      `isTrue` carry-forward (KB cache hit per FR-012 / SC-007 —
      REUSE). Dart `bool` and C# `bool` are both strict (no truthy
      coercion). `Assert.True(bool)` mirrors `expect(actual, isTrue)`
      exactly.
  - construct_key: dart.package_test.expect_isFalse_matcher
    source_form: >-
      "expect(heap.isReader(writerRef.addr), isFalse);
       expect(heap.isWriter(readerRef.addr), isFalse);"
    target_decision: >-
      Translate `expect(actual, isFalse)` to `Assert.False(actual)`.
      Symmetric to `isTrue`; xunit.net `Assert.False(bool condition)`
      verifies the condition is `false`, throws `FalseException` on
      mismatch.
    idiom_id: rf-dart-expect-isFalse-to-xunit-assert-false
    research_finding_id: rf-dart-expect-isFalse-to-xunit-assert-false
    nuance: >-
      `isFalse` carry-forward from smoke_test.dart.md's recorded
      routing table (the table embedded in
      `rf-dart-expect-isTrue-to-xunit-assert-true`'s nuance listed
      `isFalse` -> `Assert.False` even though smoke_test itself
      didn't exercise `isFalse`). This file IS the first to
      exercise it — promoted to its own first-class idiom entry,
      definition stable.
  - construct_key: dart.package_test.expect_same_matcher_reference_identity
    source_form: >-
      "expect(result, same(term));    // term = ConstTerm(123)
       expect(result, same(struct));  // struct = StructTerm('f', [...])"
    target_decision: >-
      Translate `expect(actual, same(expected))` to xUnit
      `Assert.Same(expected, actual)` — REFERENCE-IDENTITY assertion
      (Dart `same` is reference-identity; xunit.net `Assert.Same`
      verifies that two object references point to the same object,
      via `Object.ReferenceEquals`). Same argument-order swap as
      `Assert.Equal` (expected first). This is DISTINCT from
      `Assert.Equal` — even with `IEquatable<VarRef>` value-equality,
      `Assert.Same` STILL requires the same heap object. The test
      `expect(result, same(term))` is asserting that
      `heap.dereference(term)` returns the EXACT same `ConstTerm`
      instance, not a structurally-equal copy.
    idiom_id: null
    research_finding_id: rf-dart-expect-same-to-xunit-assert-same
    nuance: >-
      Reference-identity nuance (FIRST-SEEN, LOAD-BEARING — defines
      a new active idiom). Dart `same` (from `package:matcher`):
      "Returns a matcher that matches any object that is equal to
      the given `expected` using `identical`" (
      pub.dev/documentation/matcher/latest/matcher/same.html
      ). xunit.net `Assert.Same(object expected, object actual)`:
      "Verifies that two objects are the same instance" (
      xunit.net Assert reference ). Both use reference-identity
      (Dart `identical()` / .NET `Object.ReferenceEquals`). Critical
      distinction from `Assert.Equal`: `Assert.Equal` honors
      `IEquatable` and structural equality; `Assert.Same` does NOT
      — it ONLY checks reference identity. For the two
      `dereference` test sites here, BOTH are correct: the test
      asserts `heap.dereference(term) RETURNS term ITSELF` (no copy)
      and `heap.dereference(struct) RETURNS struct ITSELF` (shallow
      deref — no deep copy). The `dereference` method on the C#
      side (decided in heap_fcp.dart.md as
      `dart.method.dereference_term_with_varref_chase`) returns the
      passed-in `Term` reference unchanged when input is not a
      `VarRef` (or when the deref chain ends at a `VariableEntry`
      — "return the ORIGINAL term"). So `Assert.Same` is THE
      correct assertion to verify the no-copy contract.
      Value-vs-reference nuance explicitly addressed: even though
      `ConstTerm` has no `==` override (terms.dart.md decided
      reference-identity equality), `Assert.Equal` would happen to
      pass here too — BUT `Assert.Same` is the strictly-correct
      semantic because the contract being tested IS reference
      identity, not equality.
  - construct_key: dart.expression.list_literal_of_objects
    source_form: >-
      "StructTerm('f', [ ConstTerm('a'), VarRef(readerAddr),
       ConstTerm('b'), ])  // and similar nested list literal sites:
       [VarRef(r1)], [inner, VarRef(r2)], [VarRef(readerAddr)]"
    target_decision: >-
      Each Dart `[ ... ]` list literal of `Term` elements becomes a
      C# `new List<Term> { ... }` collection initialiser (the
      `StructTerm(string, List<Term>)` constructor in terms.dart.md
      takes `List<Term>` for its `args` parameter; C# auto-
      conversion from `List<T>` to `IReadOnlyList<T>` is implicit).
      C# 12 collection-expression syntax `[ ... ]` is the modern
      alternative; spec preference = explicit `new List<Term> { ... }`
      for clarity and compatibility with the existing
      `StructTerm.Args` shape (carry-forward from terms.dart.md's
      `dart.sum_type_leaf.functor_args_list_reference_identity`).
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      List-literal carry-forward (KB cache hit per FR-012 — REUSE).
      Dart `List<Term>` is the parameter type expected by
      `StructTerm`'s C# constructor (per terms.dart.md). No `const`
      list, no spread, no `if`/`for` element. Reference vs value:
      `List<T>` is a reference type in both languages.
  - construct_key: dart.collections.set_literal_typed_with_VarRef_elements
    source_form: "final set = <VarRef>{ref1, ref2, ref3};"
    target_decision: >-
      Translate to `var set = new HashSet<VarRef> { ref1, ref2, ref3 };`
      using C# collection-initialiser syntax for `HashSet<T>`. The
      key correctness requirement is that `HashSet<VarRef>` uses
      `EqualityComparer<VarRef>.Default`, which honors the
      `IEquatable<VarRef>` + `GetHashCode()` overrides decided in
      terms.dart.md — so `ref1 = new VarRef(10)` and `ref2 = new
      VarRef(10)` are de-duplicated; final set has 2 elements
      (matching the Dart assertion `expect(set.length, equals(2))`).
    idiom_id: null
    research_finding_id: rf-dart-set-literal-typed-to-csharp-hashset-initializer
    nuance: >-
      Set-literal nuance (FIRST-SEEN — defines a new active idiom).
      Dart's `Set<T>` literal `<T>{a, b, c}` is a `LinkedHashSet`
      under the hood (insertion-ordered, hash-based dedup using
      `==`/`hashCode`). C#'s `HashSet<T>` is UNORDERED (no
      insertion-order guarantee), but the only ordering-sensitive
      assertion in this file is `set.length` (count) which both
      collections agree on. If a future test asserts iteration
      order, codegen MUST upgrade to `System.Collections.Specialized.
      OrderedDictionary`-style or a custom insertion-ordered set
      (not present in BCL). `HashSet<T>` matches the dedup-by-
      `IEquatable` contract — load-bearing here. Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.hashset-1` documents `HashSet<T>.Add` returning
      `false` on duplicate (de-dup), and `Count` reflecting
      dedup-aware size. Authoritative both sides; no escalation.
      Element ordering of `set.contains(VarRef(10))` ->
      `set.Contains(new VarRef(10))` is by definition unaffected by
      ordering (membership test).
  - construct_key: dart.collections.map_literal_empty_then_indexer_writes_VarRef_key
    source_form: >-
      "final map = <VarRef, String>{};
       map[VarRef(10)] = 'first';
       map[VarRef(20)] = 'second';
       map[VarRef(10)] = 'updated';   // updates, doesn't add
       expect(map.length, equals(2));
       expect(map[VarRef(10)], equals('updated'));
       expect(map[VarRef(20)], equals('second'));"
    target_decision: >-
      Translate the typed empty literal `<VarRef, String>{}` to
      `var map = new Dictionary<VarRef, string>();`. Translate
      indexer-writes `map[VarRef(10)] = 'first'` to
      `map[new VarRef(10)] = "first";`. C# `Dictionary<TKey, TValue>`
      indexer-set has the SAME semantic as Dart's `Map` indexer:
      insert-if-absent, overwrite-if-present (no `KeyExistsException`
      on second write, in contrast to `Dictionary.Add`). The
      assertions `map.length` -> `map.Count`, `map[VarRef(10)]` ->
      `map[new VarRef(10)]`. Critically, dictionary key equality
      uses `EqualityComparer<TKey>.Default` which dispatches to
      `VarRef`'s `IEquatable<VarRef>.Equals` + `GetHashCode()` (per
      terms.dart.md) — so the third write at key `VarRef(10)`
      UPDATES the existing entry rather than adding a new one (final
      count = 2, matching the Dart assertion).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-typed-to-csharp-dictionary
    nuance: >-
      Map-literal + indexer-set semantics nuance (FIRST-SEEN —
      defines a new active idiom). Dart `Map<K, V>` indexer-set is
      "put-or-update"; C# `Dictionary<K, V>` indexer-set is also
      "put-or-update" (Microsoft Learn `https://learn.microsoft.com/
      dotnet/api/system.collections.generic.dictionary-2.item`:
      "Setting the value of an existing key OVERWRITES the old
      value"). DO NOT use `Dictionary.Add` (throws on duplicate
      key — DIFFERENT semantic). Key-equality nuance: the load-
      bearing requirement is that `new VarRef(10)` (key on second
      write) equals `new VarRef(10)` (key on first write) by their
      `Addr` field — this is the `IEquatable<VarRef>` contract
      from terms.dart.md, exercised here. Without that contract,
      the dictionary would treat the two `VarRef(10)` instances as
      different keys and end up with 3 entries instead of 2 —
      breaking the test. Value-vs-reference: `string` is a
      reference type in C# but with value-semantics for `==`
      (interned string literals; structural equality by default).
      `Assert.Equal("updated", map[new VarRef(10)])` uses string
      equality, which matches the Dart `equals('updated')` matcher.
      Authoritative both sides (Microsoft Learn for Dictionary,
      pub.dev for Dart `Map`); no escalation.
  - construct_key: dart.string_literal.single_or_double_quoted
    source_form: >-
      "'f', 'a', 'b', 'inner', 'outer', 'hello', 'first', 'second',
       'updated'  // and group/test name labels"
    target_decision: >-
      All Dart string literals `'...'` map to C# string literals
      `"..."`. Dart accepts both `'...'` and `"..."` (interchangeable);
      C# accepts only `"..."` for regular strings (`'...'` is `char`).
      No interpolation in this file's string literals. No raw strings
      (`r'...'`). No triple-quoted strings.
    idiom_id: null
    research_finding_id: rf-dart-string-literal-to-csharp-string-literal
    nuance: >-
      String-literal nuance (FIRST-SEEN — promoted to a recorded
      idiom because every test file exercises it, and pinning the
      simple-case routing now prevents every subsequent test
      convspec from re-deriving it). Dart `'x'` is a single-character
      string; C# `'x'` is a `char` (different type — would not
      compile as `string`). The conversion MUST emit double quotes.
      Microsoft Learn (`https://learn.microsoft.com/dotnet/csharp/
      language-reference/builtin-types/reference-types#the-string-
      type`): "A string literal is enclosed in double quotation
      marks." No null-safety nuance (string literals are non-null).
conversion_units:
  - "namespace <RootNs>.Test.Heap; (file-scoped namespace mirroring the test/heap directory)"
  - "using Xunit; (file-level)"
  - "using <RootNs>.Runtime; (single using covering both terms.dart and heap_fcp.dart SUT imports — deduplicated)"
  - "public class VarRefStructurePointerArchitectureTests { ... } — 6 [Fact] methods (VarRefHasOnlyAddrField, VarRefEqualityBasedOnAddrOnly, VarRefHashCodeConsistentWithEquality, DetermineReaderWriterByCheckingHeapCell, VarRefCanBeUsedAsStructArgument, VarRefInNestedStructures)"
  - "public class VarRefDereferencingTests { ... } — 6 [Fact] methods (DereferenceVarRefToWriterReturnsVarRefWhenUnbound, DereferenceVarRefToReaderReturnsVarRefToWriterWhenUnbound, DereferenceVarRefReturnsValueWhenBound, DereferenceMethodOnHeapWorksWithVarRefTerm, DereferenceNonVarRefTermReturnsItself, DereferenceStructTermReturnsItselfNoDeepDeref)"
  - "public class VarRefInCollectionsTests { ... } — 2 [Fact] methods (VarRefCanBeUsedInSet, VarRefCanBeUsedAsMapKey)"
  - "All 14 [Fact] methods carry [Fact(DisplayName = \"<original Dart test label>\")] preserving the human-readable name"
  - "Per-test body shape: var <name> = new <Ctor>(args); var (a, b) = heap.AllocateVariable(); Assert.True/False/Equal/NotEqual/Same/IsType<T>(...) calls — 1-to-1 translation of each Dart test body"
  - "NO constructor / NO IDisposable on any class (no setUp / tearDown in source)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely"
  - "NO equivalent of Dart's library; directive — file-scoped namespace replaces it"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-constructor-call-no-new-to-csharp-new-keyword — Dart's `new`-less constructor call -> C# `new` (FIRST-SEEN)

- **Deep analysis**: this file calls constructors at ~25 sites without
  the `new` keyword (Dart 2 style, mandatory in this codebase per its
  uniform style). C# requires `new` for every reference-type
  constructor call (target-typed `new()` is a 9+ shorthand that
  requires an explicit destination type — incompatible with the `var`
  local style chosen here).
- **Authoritative Dart**: `https://dart.dev/language/classes#using-constructors`
  documents the `new` keyword as optional since Dart 2 ("In Dart 2 and
  later, the `new` keyword is optional"). All call sites in this file
  use the no-`new` form.
- **Authoritative .NET**: Microsoft Learn
  `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`
  — "Use the `new` operator to create instances of types." Target-typed
  `new()` syntax is documented at the same page but requires an
  explicit destination type (`VarRef v = new(42);`); incompatible with
  `var v = new(42);`.
- **Conclusion**: emit `new <Ctor>(args)` at every site. Authoritative
  both sides; no escalation. First-seen idiom registered (active);
  every subsequent test convspec MUST reuse via the KB rather than
  re-derive.

### rf-dart-expect-isA-to-xunit-assert-istype — bare `isA<T>()` matcher -> `Assert.IsType<T>` (FIRST-SEEN)

- **Deep analysis**: this file uses `expect(x, isA<T>())` at ~10 sites
  (every `derefAddr` / `dereference` result type check). The target
  types (`VarRef`, `ConstTerm`, `StructTerm`) are all `sealed` per
  terms.dart.md — no subtypes. Therefore the EXACT-type assertion is
  semantically equivalent to the assignable-from variant for the
  inputs actually exercised, AND the EXACT-type form is the stricter
  contract.
- **Authoritative Dart**: `https://pub.dev/documentation/matcher/latest/matcher/isA.html`
  — "A matcher that matches objects with type `T`." Subtype-tolerant.
- **Authoritative .NET**: xunit.net `Assert.IsType<T>` (
  `https://xunit.net/docs/comparisons` and the API reference) —
  "Verifies that an object is of the given EXACT type." Throws
  `IsTypeException` on subtype as well. Sibling assertion
  `Assert.IsAssignableFrom<T>` is the subtype-tolerant variant.
- **Conclusion**: `bare isA<T>() -> Assert.IsType<T>` when `T` is
  sealed (this file's case). Routing-table extension recorded in the
  nuance for sibling test files: `bare isA<T>() WITH subtypes ->
  Assert.IsAssignableFrom<T>`; `throwsA(isA<T>()) -> Assert.ThrowsAny<T>`
  (already pinned in boot_loader_test.dart.md). Authoritative both
  sides; no escalation. NEW idiom registered (active).

### rf-dart-expect-isNot-equals-to-xunit-assert-notequal — `isNot(equals(X))` -> `Assert.NotEqual` (FIRST-SEEN)

- **Deep analysis**: xUnit has no composable `isNot` primitive; each
  composed Dart matcher routes to a dedicated assertion. This file
  exercises only `isNot(equals(X))`. boot_loader_test.dart.md
  exercised `isNot(contains(X))` -> `Assert.DoesNotContain` — same
  family.
- **Authoritative Dart**: `package:matcher` `isNot` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isNot.html`)
  — "A matcher that inverts another matcher's logic."
- **Authoritative .NET**: xunit.net `Assert.NotEqual<T>(T expected, T
  actual)` — "Verifies that two objects are not equal." Same
  argument-order swap as `Assert.Equal`.
- **Conclusion**: `isNot(equals(X)) -> Assert.NotEqual(X, actual)`.
  Authoritative both sides; no escalation. NEW idiom registered (active).

### rf-dart-expect-isFalse-to-xunit-assert-false — `isFalse` -> `Assert.False` (FIRST-SEEN, promoted from routing table)

- **Deep analysis**: smoke_test.dart.md recorded `isFalse` -> `Assert.False`
  in its broader routing table nuance but did not exercise it. This
  file is the first to exercise it (`expect(heap.isReader(writerRef.addr),
  isFalse)`). Promotion to a first-class idiom recorded.
- **Authoritative Dart**: `package:matcher` `isFalse`
  (`https://pub.dev/documentation/matcher/latest/matcher/isFalse-constant.html`)
  — "A matcher that matches if the value is `false`." Strict boolean.
- **Authoritative .NET**: xunit.net `Assert.False(bool condition)` —
  "Verifies that an expression is false." Strict boolean; throws
  `FalseException` on mismatch.
- **Conclusion**: 1-to-1, no caveat. NEW idiom registered (active).

### rf-dart-expect-same-to-xunit-assert-same — `same(X)` -> `Assert.Same(X, actual)` (FIRST-SEEN, LOAD-BEARING)

- **Deep analysis**: load-bearing because the `dereference` contract
  (decided in heap_fcp.dart.md as
  `dart.method.dereference_term_with_varref_chase`) returns the
  INPUT TERM REFERENCE unchanged when the input is not a `VarRef`
  (and when the deref chain ends at a `VariableEntry`). The test
  `expect(result, same(term))` is verifying that exact reference-
  identity contract — not structural equality.
- **Authoritative Dart**: `package:matcher` `same`
  (`https://pub.dev/documentation/matcher/latest/matcher/same.html`)
  — "Returns a matcher that matches any object that is `identical` to
  the given expected value." `identical()` is Dart's reference-identity
  primitive.
- **Authoritative .NET**: xunit.net `Assert.Same(object expected,
  object actual)` (`https://xunit.net/docs/comparisons`) —
  "Verifies that two objects are the same instance." Uses
  `Object.ReferenceEquals` internally. The sibling assertion
  `Assert.NotSame` is the negation.
- **Conclusion**: `same(X) -> Assert.Same(X, actual)`. Critical
  distinction from `Assert.Equal` recorded in nuance — `Assert.Same`
  bypasses `IEquatable` and tests reference identity only.
  Authoritative both sides; no escalation. NEW idiom registered (active).

### rf-dart-set-literal-typed-to-csharp-hashset-initializer — `<T>{...}` -> `new HashSet<T> { ... }` (FIRST-SEEN)

- **Deep analysis**: this file's `<VarRef>{ref1, ref2, ref3}` is the
  first set-literal site in the convspec corpus. Dart's set-literal
  is hash-based with `==`/`hashCode` dedup; .NET's `HashSet<T>` is
  the matching primitive (uses `IEqualityComparer<T>` / falls back to
  `EqualityComparer<T>.Default` -> `IEquatable<T>.Equals` +
  `GetHashCode()`).
- **Authoritative Dart**: `https://dart.dev/language/collections#sets`
  — "A `Set` in Dart is an unordered collection of unique items."
  The typed literal `<T>{...}` constructs a `LinkedHashSet<T>`
  (insertion-ordered).
- **Authoritative .NET**: Microsoft Learn
  `https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1`
  — "Represents a set of values" with `Add` returning `false` on
  duplicate. Microsoft Learn collection-initialiser syntax:
  `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#collection-initializers`
  — "`new HashSet<int> { 1, 2, 3 }`."
- **Conclusion**: `<T>{a, b, c} -> new HashSet<T> { a, b, c }`.
  Ordering caveat recorded in nuance (`LinkedHashSet` is insertion-
  ordered; `HashSet<T>` is unordered). Not load-bearing for this
  file's assertions (only `Count` and `Contains`). Authoritative
  both sides; no escalation. NEW idiom registered (active).

### rf-dart-map-literal-typed-to-csharp-dictionary — `<K,V>{}` + indexer-set -> `Dictionary<K,V>` (FIRST-SEEN)

- **Deep analysis**: empty typed map literal + indexer writes
  (including overwrite-existing-key) is exercised here for the first
  time in the convspec corpus. The critical correctness pivot is
  that the third write at key `VarRef(10)` UPDATES, not ADDS — which
  requires both Dart and C# semantics to match on "indexer-set is
  put-or-update", AND `VarRef`'s `IEquatable` contract to fire.
- **Authoritative Dart**: `https://dart.dev/language/collections#maps`
  — "Maps in Dart pair keys with values." Indexer-set: "If the key
  already exists, its value is replaced."
- **Authoritative .NET**: Microsoft Learn
  `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.item`
  — verbatim: "Setting the value of an existing key OVERWRITES the
  old value." DO NOT use `.Add` (throws on duplicate). Dictionary
  key equality: `EqualityComparer<TKey>.Default` honors
  `IEquatable<TKey>.Equals` + `GetHashCode()`.
- **Conclusion**: `<K,V>{} -> new Dictionary<K, V>();` then
  `map[k] = v;` (identical syntax both sides). `map.length ->
  map.Count`. Load-bearing nuance: the `IEquatable<VarRef>` contract
  from terms.dart.md is what makes the test pass; without it the
  dictionary would have 3 entries. Authoritative both sides; no
  escalation. NEW idiom registered (active).

### rf-dart-string-literal-to-csharp-string-literal — `'x'` -> `"x"` (FIRST-SEEN, promoted)

- **Deep analysis**: trivial and pervasive. Promoted from "implicit"
  to a first-class idiom recorded in the KB so it doesn't bloat
  every future convspec's rationale.
- **Authoritative Dart**: `https://dart.dev/language/built-in-types#strings`
  — Dart accepts `'...'` and `"..."` interchangeably for string
  literals.
- **Authoritative .NET**: Microsoft Learn
  `https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-string-type`
  — "A string literal is enclosed in double quotation marks." C#
  `'x'` is a `char`, NOT a `string`.
- **Conclusion**: emit `"..."` at every site. Authoritative both
  sides; no escalation. NEW idiom registered (active).

### Carry-forward idioms (KB reuse only — FR-012 / SC-007)

The following idioms are REUSED VERBATIM from prior convspecs (KB
cache hit; NO research, NO re-derivation per FR-012):

- `rf-dart-library-directive-to-csharp-namespace-elision`
  (terms.dart.md): `library;` -> file-scoped `namespace …;`.
- `rf-dart-package-test-import-to-xunit-using`
  (smoke_test.dart.md, boot_loader_test.dart.md): `package:test` ->
  `using Xunit;` with xUnit pinned project-wide.
- `rf-dart-internal-package-import-to-csharp-using`
  (terms.dart.md, boot_loader_test.dart.md): `package:glp_runtime/...`
  -> `using <RootNs>.Runtime;` (two Dart imports COLLAPSE to one C#
  `using` because both target the same lib/runtime namespace).
- `rf-dart-package-test-main-omit-in-xunit`
  (boot_loader_test.dart.md): drop `void main()` entirely.
- `rf-dart-package-test-group-to-xunit-class`
  (boot_loader_test.dart.md): one xUnit class per Dart `group`;
  flat-siblings sub-case applies here (no shared setUp).
- `rf-dart-test-callback-to-xunit-method-body`
  (smoke_test.dart.md): one `[Fact]` method per `test()` with
  `[Fact(DisplayName=...)]` preserving the original label.
- `rf-dart-final-local-to-csharp-var-local` (multiple precedents):
  `final <name> = <expr>` -> `var <name> = <expr>`.
- `rf-dart-list-indexer-to-csharp-list-indexer` (multiple
  precedents): `list[i]` -> `list[i]` (works on
  `IReadOnlyList<T>`).
- `rf-dart-record-destructure-to-csharp-valuetuple-deconstruction`
  (external_io.dart.md): `final (a, b) = expr` -> `var (a, b) = expr`;
  `_` discard identical syntax.
- `rf-dart-expect-equals-to-xunit-assert-equal-argorder`
  (smoke_test.dart.md): `expect(actual, equals(X))` ->
  `Assert.Equal(X, actual)` with argument-order swap.
- `rf-dart-expect-isTrue-to-xunit-assert-true` (smoke_test.dart.md):
  `expect(actual, isTrue)` -> `Assert.True(actual)`.
- `rf-dart-list-literal-to-csharp-list-initializer` (multiple
  precedents): `[a, b, c]` -> `new List<T> { a, b, c }`.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — every test method is synchronous, so every
  `[Fact]` returns `void` (not `async Task`).
- No `late`, `mixin`, `extension`, generics-with-bounds, sealed/abstract
  declarations (the SUT types `VarRef`/`ConstTerm`/`StructTerm`/`HeapFCP`
  ARE sealed per terms.dart.md / heap_fcp.dart.md, but this test file
  declares no types of its own), bitwise/shift, or null-safety nuance.
- The test file's `hashCode` assertion (`expect(ref1.hashCode,
  equals(ref2.hashCode))`) is the load-bearing test that validates
  terms.dart.md's `IEquatable<VarRef>` + `GetHashCode()` contract.
  Codegen MUST NOT omit `GetHashCode()` on the C# `VarRef` class.
- The `same(...)` assertions are the load-bearing tests that validate
  heap_fcp.dart.md's `dereference` no-copy contract (the C# method
  must return the input reference unchanged for non-`VarRef` inputs
  and at chain-terminal `VariableEntry` cases).
- The `<VarRef>{ref1, ref2, ref3}` set + `<VarRef, String>{}` map
  tests are the load-bearing tests that validate terms.dart.md's
  decision to make `VarRef` value-equal-on-`Addr` (not record-class,
  not plain class). Without that decision, both collection tests
  would fail.
- The Dart-pubspec-aware `import 'package:glp_runtime/...'`
  resolution has no C# counterpart — the test project's `.csproj`
  (langpair-level, OUT OF SCOPE for this single-file artifact) MUST
  reference the SUT assembly that contains `<RootNs>.Runtime`.
- Zero escalations: every construct in this file is authoritative-
  supported on both sides; every nuance (value-vs-reference, null-
  safety absence, reference-identity-vs-structural-equality,
  set/dict key-equality dispatch) is explicitly addressed per
  FR-009 / FR-010 / SC-006 / US2-AS4. Six first-seen idioms are
  registered (active); twelve carry-forward idioms are reused
  verbatim from the KB per FR-012 / SC-007.
