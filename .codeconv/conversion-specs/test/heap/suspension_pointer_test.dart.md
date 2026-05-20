> Conversion-spec artifact for test/heap/suspension_pointer_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/heap/suspension_pointer_test.dart
source_sha256: 8f0020be2a925a63f498abf316c3e5ea71c60b9a6e5ef23bbffbc1615d4b2a95
target_code_unit: test/heap/SuspensionPointerTest.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "library;"
    target_decision: >-
      Drop the bare `library;` directive entirely. C# has no per-file
      library-declaration syntax — every `.cs` file participates in
      compilation by being in the project; namespaces are declared per-
      file or per-type. The doc-comment block ABOVE `library;` (the
      "Tests for suspension and reactivation with Pointer Architecture
      Heap" lead, the `Adapted from: …` provenance, the `For spec:
      docs/heap-pointer-architecture-spec.md v3.0` citation, and the
      three-bullet "Key changes from original" list) is preserved as the
      XML doc-comment on the FIRST emitted test class
      (`SuspensionAndReactivationPointerArchitectureTests`) since that
      class corresponds to the dominant first group; the other two
      class XML doc-comments inherit a one-line reference back to the
      file lead. File is projected into the file-scoped namespace
      `<RootNs>.Test.Heap;` mirroring the Dart `test/heap` directory
      shape (precedent: binding_pointer_test.dart.md /
      varref_pointer_test.dart.md).
    idiom_id: rf-dart-library-directive-to-csharp-namespace-elision
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance (carry-forward KB cache hit per FR-012 /
      SC-007 — REUSE, NO re-research): Dart 2.12+ requires `library;`
      (un-named) only as a marker for file-level doc-comments; no name,
      no `part`, no `part of`. C# elides the construct entirely and uses
      the file-scoped `namespace …;` shape instead. No value-vs-
      reference, async, isolate, or null-safety surface implicated. The
      provenance comment block IS load-bearing (it cites the heap-
      pointer-architecture spec v3.0 — a precondition for understanding
      the suspendOnWriter vs suspendOnReader API) and MUST survive the
      conversion as the test-class XML doc-comment.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit is pinned project-wide
      (precedent: binding_pointer_test.dart.md, varref_pointer_test.dart.md,
      boot_loader_test.dart.md, smoke_test.dart.md). Codegen MUST also
      add `using System.Collections.Generic;` for the `Dictionary<int,
      Term>` literal used in the CommitOps group, and `using
      System.Linq;` for the `Select(...).ToHashSet()` call.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance (carry-
      forward KB cache hit per FR-012 — REUSE). Every `package:test`
      file in the inventory MUST map to the SAME .NET framework so test
      discovery, runner config, and attribute vocabulary stay consistent.
      No re-research; no re-derivation.
  - construct_key: dart.package_under_test.import_directive_runtime_facade_plus_runtime_siblings_with_show_clause
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/suspension.dart';
       import 'package:glp_runtime/runtime/heap_fcp.dart' show HeapFCP, Pointer, SuspensionListNode, WriterContent;
       import 'package:glp_runtime/runtime/terms.dart';"
    target_decision: >-
      Five `package:glp_runtime/runtime/...` imports — four bare plus one
      with a `show` clause — collapse to ONE `using <RootNs>.Runtime;`
      directive. Per runtime.dart.md / heap_fcp.dart.md / machine_state.
      dart.md / suspension.dart.md / terms.dart.md, all five SUT files
      land in the same `<RootNs>.Runtime` namespace, so a single `using`
      suffices. The `show HeapFCP, Pointer, SuspensionListNode,
      WriterContent` clause has NO C# counterpart (precedent: runtime.
      dart.md construct `dart.import_directive.package_with_show_clause_
      bytecode_runner_and_glpchannelhandle`, idiom rf-dart-import-show-
      clause-no-csharp-counterpart) — C# `using` brings in the whole
      namespace and the compiler discards unused names at link time;
      explicit narrowing is not modelled. The four names are visible by
      virtue of being in `<RootNs>.Runtime`; codegen MUST NOT attempt a
      using-alias workaround.
    idiom_id: rf-dart-import-relative-to-csharp-using-namespace
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Cross-file dependency + show-clause nuance (carry-forward KB cache
      hit per FR-012 — REUSE). In Dart each `package:` URI is a separate
      import; the `show` clause narrows the imported surface to a
      whitelist of names. In C# `using <ns>;` brings the whole namespace
      in scope and there is no `show`-like narrower (the partial
      `using <Alias> = <Type>;` is per-name aliasing, not surface
      narrowing — wrong shape). The Dart show-clause restriction is
      observably equivalent at COMPILE-TIME (unused imported names are
      silently allowed); the test assembly references the SUT assembly
      via the .csproj (project-system idiom; out of scope for THIS
      artifact). Five Dart imports collapse to one C# `using`.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Suspension and Reactivation - Pointer Architecture', () { ... }); group('Fairness Budget - Pointer Architecture', () { ... }); group('CommitOps Equivalent - Pointer Architecture', () { ... }); }"
    target_decision: >-
      Eliminate `void main()` entirely; xUnit discovers `[Fact]` methods
      by reflection — there is NO per-file entrypoint. The three top-
      level `group(...)` calls inside `main`'s body become three top-
      level test classes (see `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (carry-forward KB cache hit per FR-012 — REUSE).
      Dart `main` is invoked once per test-file process; xUnit has no
      per-file hook — only per-class (constructor + `IDisposable.
      Dispose`) and per-collection fixtures. THIS file's `main` body is
      three `group(...)` calls with no other statements, so the omission
      is lossless. No closures captured across groups; no file-level
      shared state.
  - construct_key: dart.package_test.group_block.three_sibling_top_level_groups_no_shared_setup
    source_form: >-
      "group('Suspension and Reactivation - Pointer Architecture', () { test(...); ... 7 tests ... });
       group('Fairness Budget - Pointer Architecture', () { test(...);  // 1 test });
       group('CommitOps Equivalent - Pointer Architecture', () { test(...); // 1 test });"
      // three SIBLING top-level groups, NO setUp/tearDown, NO `late`
      // fields, NO nested groups; every test allocates its own
      // `final rt = GlpRuntime();` and `final heap = rt.heap as HeapFCP;`
    target_decision: >-
      Apply the flat-siblings sub-case of rf-dart-package-test-group-to-
      xunit-class (precedent: varref_pointer_test.dart.md — three sibling
      groups, no shared state -> three test classes). Three classes total
      in the same file (C# allows multiple public types per file, same
      compilation unit, same `namespace <RootNs>.Test.Heap;`):
      (1) `public class SuspensionAndReactivationPointerArchitectureTests` —
      7 `[Fact]` methods (one per `test()` inside the first group);
      (2) `public class FairnessBudgetPointerArchitectureTests` —
      1 `[Fact]` method;
      (3) `public class CommitOpsEquivalentPointerArchitectureTests` —
      1 `[Fact]` method.
      No constructor / `Dispose` on any class (no `setUp` / `tearDown`
      in source). No `IClassFixture<T>` (no shared cross-test state).
      Class-name shape: PascalCase + `Tests` suffix, derived by dropping
      hyphens / spaces from the group label (same convention as
      varref_pointer_test.dart.md). Alternative single-class +
      `[Trait("Group", "<label>")]` (the binding_pointer_test.dart.md
      shape) was REJECTED because there is no overarching theme that
      ties suspension + fairness + commit-ops together — they are three
      structurally distinct concerns; one class per group is the cleaner
      partition.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Group-topology nuance (carry-forward KB cache hit per FR-012 —
      REUSE the same flat-siblings sub-case decided in varref_pointer_
      test.dart.md). xUnit has NO first-class nested-group construct;
      boot_loader_test.dart.md explored single-class-plus-Trait, one-
      class-per-inner-group, and IClassFixture<> hierarchies and picked
      single-class-plus-Trait because groups SHARED setUp. HERE the
      topology is FLAT, no shared setUp, no `late` field — the per-
      group class shape is strictly simpler and aligns with the three-
      groups-three-classes precedent in varref_pointer_test. xUnit
      lifecycle per-test fresh-instance is irrelevant — no instance
      state. No async test methods (every closure is synchronous).
  - construct_key: dart.package_test.test_call_simple
    source_form: >-
      "test('On wake, activation pc equals kappa (restart at clause 1)', () { ... });
       test('Multiple suspensions on same variable all activate', () { ... });
       test('Disarmed suspensions do not activate', () { ... });
       test('Suspension forwarding when binding to another variable', () { ... });
       test('Chain of variable bindings forwards suspensions correctly', () { ... });
       test('suspendOnWriter adds directly to writer cell', () { ... });
       test('suspendOnReader follows pointer to find writer', () { ... });
       test('26-step tail recursion budget yields and resets', () { ... });
       test('applySigmaHat binds multiple variables and collects activations', () { ... });"
    target_decision: >-
      One `[Fact(DisplayName = "<original Dart test label>")] public
      void <PascalCasedIdentifier>() { <body> }` method per Dart
      `test()` call, on the enclosing test class. The Dart test label
      (human-readable string with spaces, hyphens, parentheses, commas)
      is preserved verbatim in `DisplayName`; the C# method identifier
      is PascalCased + punctuation-stripped (C# method identifiers
      cannot contain whitespace, hyphens, parentheses, commas, periods).
      All 9 `test(...)` calls in this file are synchronous (no async /
      Future surface) — every `[Fact]` method returns `void`, NOT
      `async Task`. No `skip:` / `timeout:` / `retry:` argument on any
      `test()` call. Example identifier mangling: `'On wake, activation
      pc equals kappa (restart at clause 1)'` -> `OnWakeActivationPcEqu
      alsKappaRestartAtClause1`; `'26-step tail recursion budget
      yields and resets'` -> `TwentySixStepTailRecursionBudgetYieldsA
      ndResets` (leading-digit reshape — C# identifiers cannot start
      with a digit, so prefix with the spelled-out cardinal).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Test-registration + identifier-shape nuance (carry-forward KB
      cache hit per FR-012 — REUSE). `DisplayName` preserves the
      searchable label; the PascalCased identifier is whatever C# accepts.
      Leading-digit case (`'26-step ...'`) is genuinely file-local —
      C# identifiers MUST NOT start with a digit (Microsoft Learn,
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-
      style/identifier-names`) — codegen MUST prefix the spelled-out
      cardinal (or any unambiguous letter prefix). `DisplayName` keeps
      the literal "26-step …" so reports stay accurate. No closure
      capture of mutable outer state in any test — each starts with
      `final rt = GlpRuntime();` and `final heap = rt.heap as HeapFCP;`
      from scratch.
  - construct_key: dart.local_var.final_constructor_instance_or_cast_or_record_destructure
    source_form: >-
      "final rt = GlpRuntime();
       final heap = rt.heap as HeapFCP;
       final (writerAddr, readerAddr) = heap.allocateVariable();
       final (writerAddr, _) = heap.allocateVariable();
       final (w1, r1) = heap.allocateVariable();
       final (w2, r2) = heap.allocateVariable();
       final (w3, r3) = heap.allocateVariable();
       final record = SuspensionRecord(g, kappa);
       final r1 = SuspensionRecord(10, 100);
       final r2 = SuspensionRecord(20, 200);
       final r3 = SuspensionRecord(30, 300);
       final activations = heap.bindWriter(writerAddr, ConstTerm('ground'));
       final acts1 = heap.bindWriterToReader(w1, r2);
       final acts2 = heap.bindWriter(w2, ConstTerm('final'));
       final goalIds = activations.map((a) => a.id).toSet();
       final wc = heap.cells[writerAddr].content as WriterContent;
       final sigmaHat = <int, Term>{ w1: ConstTerm('a'), w2: ConstTerm('b'), w3: ConstTerm('c') };
       final allActivations = <GoalRef>[];
       final acts = heap.bindWriter(entry.key, entry.value);
       final y = rt.tailReduce(g);
       final y26 = rt.tailReduce(g);
       final y1 = rt.tailReduce(g);"
    target_decision: >-
      Three sub-shapes, all collapse to `var <name> = <expr>;` at the
      C# call-site: (a) bare `final <name> = <Ctor>(...)` -> `var
      <name> = new <Ctor>(...)`; (b) `final <name> = <expr> as <T>` ->
      `var <name> = (T)<expr>` (explicit cast — Dart `as` throws on
      mismatch and C# `(T)x` throws `InvalidCastException`, matching
      Dart's `TypeError`; do NOT use C# `x as T` which yields `null`
      on mismatch — wrong semantics); (c) `final (a, b) = <expr>` ->
      `var (a, b) = <expr>` (tuple deconstruction, identical syntax;
      `_` discard identical both sides). All instance-method return
      captures (`heap.bindWriter(...)`, `rt.tailReduce(g)`, `heap.
      cells[…].content as WriterContent`) use plain `var`. Note: the
      Dart source has a NAME-SHADOWING site in the "Multiple
      suspensions" test (`final r1 = SuspensionRecord(10, 100);`
      shadows the `(_, r1) = ...` discard? No — in that test the
      allocateVariable destructure is `(writerAddr, readerAddr)` so
      `r1`/`r2`/`r3` are SUSPENSION RECORDS, not readers; the
      "Disarmed suspensions" test similarly uses `r1`/`r2` as
      suspension-record names and there is NO collision because that
      test's destructure is also `(writerAddr, readerAddr)`. Codegen
      MUST preserve the variable identity (suspension-record vs reader
      addr) — they are deliberately disjoint per-test.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      `final` -> `var` carry-forward (KB cache hit per FR-012 — REUSE).
      Dart `final` is enforced single-assignment; C# `var` is not
      (locals don't accept `readonly`). The discipline is preserved by
      not reassigning in the converted body. The `final (a, b) =` form
      reuses rf-dart-record-destructure-to-csharp-valuetuple-
      deconstruction (carry-forward, KB cache hit). The `as`-cast in
      `final heap = rt.heap as HeapFCP;` reuses rf-dart-as-cast-to-
      csharp-explicit-cast (KB cache hit from binding_pointer_test.
      dart.md). Width nuance: `allocateVariable()` returns `(int, int)`
      per heap_fcp.dart.md (mapped to a C# value-tuple of the same int
      typedef; precedent in varref_pointer_test.dart.md kept the SUT-
      side int).
  - construct_key: dart.local_var.const_typed_goalid_pc_int_alias
    source_form: >-
      "const GoalId g = 77;
       const Pc kappa = 1;
       const GoalId g = 123;"
    target_decision: >-
      `GoalId` and `Pc` are Dart `typedef`s of `int` per machine_state.
      dart.md construct `typedef opaque-int-identifier GoalId Pc
      ReaderId WriterId` (idiom rf-dart-typedef-int-to-csharp-global-
      using-alias). In C# the typedef is materialised as a file-scoped
      or global `using GoalId = System.Int32; using Pc = System.Int32;`
      (a per-file using-alias-directive — Microsoft Learn:
      `https://learn.microsoft.com/dotnet/csharp/language-reference/
      keywords/using-directive#the-using-alias-directive`). Therefore
      `const GoalId g = 77;` maps to `const GoalId g = 77;` verbatim;
      `const Pc kappa = 1;` maps to `const Pc kappa = 1;` verbatim. C#
      `const` on a local is legal for compile-time constants of
      primitive types — `int` qualifies (Microsoft Learn:
      `https://learn.microsoft.com/dotnet/csharp/language-reference/
      keywords/const`). Codegen MUST emit the typedef-alias name (NOT
      collapse to plain `int`) so the test still documents the
      "opaque-int identifier kind" contract that the typedef declares.
    idiom_id: rf-dart-typedef-int-to-csharp-global-using-alias
    research_finding_id: rf-dart-typedef-int-to-csharp-global-using-alias
    nuance: >-
      Typedef-name-preservation nuance (carry-forward KB cache hit per
      FR-012 — REUSE the machine_state.dart.md decision). Dart `typedef
      GoalId = int;` is an opaque-int marker: `GoalId` and `Pc` are
      nominally distinct but share the underlying width. C# `using
      GoalId = System.Int32;` reproduces the alias at the file/global
      level (.NET 6+ supports global using-alias; per-file using-alias
      is fully equivalent for this test file's needs). `const` on a
      local: Dart `const` and C# `const` agree on compile-time-
      constant semantics for `int` literals; both forbid `const` on
      reference-type locals (not exercised here). Width nuance: int-
      vs-long carry-forward — machine_state.dart.md picked the typedef
      target as `int` (32-bit) which differs from the `long`-width
      decision in heap address types (cells.dart.md). For THIS test,
      `g = 77`, `kappa = 1`, `g = 123` all fit in `int` and the
      typedef chain stays consistent with machine_state.dart.md.
  - construct_key: dart.expression.member_property_access_runtime_heap_field
    source_form: "rt.heap"
    target_decision: >-
      `rt.heap` (Dart property of `GlpRuntime`) maps to `rt.Heap` (C#
      PascalCase property of the `GlpRuntime` class per runtime.dart.md
      construct `dart.mutable_state_class.identity_equality.runtime_
      facade_aggregate`, idiom rf-dart-mutable-state-class-identity-
      equality-to-csharp-class). The cast `rt.heap as HeapFCP` reuses
      the explicit-cast idiom; the C# shape is `(HeapFCP)rt.Heap`.
    idiom_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      PascalCase property nuance (carry-forward KB cache hit per
      FR-012 — REUSE). Dart instance fields/properties are lower-
      camelCase; C# properties are PascalCase. The cast pattern
      `final heap = rt.heap as HeapFCP;` documents that `Runtime.Heap`
      is declared as the base `Heap` interface type with the FCP
      implementation injected; the cast accesses HeapFCP-specific
      methods (`allocateVariable`, `suspendOnReader`, `cells`, etc.)
      not on the base interface. C# `(HeapFCP)rt.Heap` throws
      `InvalidCastException` on mismatch — same fail-fast semantics
      as Dart `as`.
  - construct_key: dart.constructor_call.glp_runtime_no_args
    source_form: "GlpRuntime()"
    target_decision: >-
      `new GlpRuntime()`. SUT type per runtime.dart.md construct
      `dart.mutable_state_class.identity_equality.runtime_facade_
      aggregate` (idiom rf-dart-mutable-state-class-identity-equality-
      to-csharp-class) is a `public class GlpRuntime` with a parameter-
      less constructor seeded with default-initialised state.
    idiom_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      Carry-forward KB cache hit per FR-012 — REUSE. C# `new` mandatory
      (rf-dart-constructor-call-no-new-to-csharp-new-keyword from
      varref_pointer_test.dart.md). No constructor args; no target-
      typed-new shorthand (incompatible with `var` locals).
  - construct_key: dart.constructor_call.suspension_record_two_ints_goalid_pc
    source_form: >-
      "SuspensionRecord(g, kappa);
       SuspensionRecord(10, 100);
       SuspensionRecord(20, 200);
       SuspensionRecord(30, 300);
       SuspensionRecord(42, 500);
       SuspensionRecord(99, 999);
       SuspensionRecord(55, 550);
       SuspensionRecord(66, 660);
       SuspensionRecord(1, 10);
       SuspensionRecord(2, 20);
       SuspensionRecord(3, 30);"
    target_decision: >-
      `new SuspensionRecord(g, kappa)` etc. SUT type per suspension.
      dart.md construct `class SuspensionRecord shared-state-record-
      nullable-int-goalid-final-int-resumepc disarm-method armed-getter
      tostring-override` (idiom rf-dart-shared-mutable-record-by-
      reference-to-csharp-class): plain reference-type class with
      positional ctor `(int? goalId, int resumePC)`, mutable `bool
      Armed { get; private set; }` (or `int? GoalId` plus a `bool
      Armed => GoalId != null;` derived getter per the SUT decision),
      and a `Disarm()` method that sets `GoalId = null`. Codegen MUST
      preserve REFERENCE identity (no `record class` synthesis, no
      `record struct`) because `r1.disarm()` mutates state observed
      through the heap's suspension list aliasing.
    idiom_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      Reference-identity nuance (carry-forward KB cache hit per FR-012
      — REUSE from binding_pointer_test.dart.md and suspension.dart.md).
      SuspensionRecord instances are passed by reference into the
      heap's suspension list; `r1.disarm()` later mutates the SAME
      object the heap holds. C# `record class` / `record struct` would
      synthesise structural equality and `with`-cloning — both
      forbidden here. The mapping is a plain `class` (already pinned by
      suspension.dart.md). Constructor-call no-`new` -> C# `new` carry-
      forward.
  - construct_key: dart.constructor_call.const_term_with_string_literal
    source_form: >-
      "ConstTerm('ground'), ConstTerm('value'), ConstTerm('final'),
       ConstTerm('end'), ConstTerm('a'), ConstTerm('b'), ConstTerm('c')"
    target_decision: >-
      `new ConstTerm("ground")` etc. SUT type per terms.dart.md (idiom
      rf-dart-sumleaf-no-eq-to-csharp-class-no-record) is `sealed class
      ConstTerm : Term` with a single nullable `object? Value` field;
      C# string literal boxes transparently into `object?`.
    idiom_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      String-literal nuance (carry-forward KB cache hit per FR-012 —
      REUSE rf-dart-string-literal-to-csharp-string-literal from
      varref_pointer_test.dart.md). Dart `'…'` maps to C# `"…"`; Dart
      single-char `'x'` is a string, C# `'x'` is a `char` — codegen
      MUST always emit double quotes. Reference-identity nuance (carry-
      forward from binding_pointer_test.dart.md): ConstTerm is a plain
      reference-type class — Assert.Equal would compare object-
      identity by default; tests in THIS file only USE ConstTerm
      payloads, never compare them, so identity-vs-equality is not
      exercised here.
  - construct_key: dart.method_call.heap_mutator_suspend_on_reader
    source_form: >-
      "heap.suspendOnReader(readerAddr, record);
       heap.suspendOnReader(readerAddr, r1);
       heap.suspendOnReader(readerAddr, r2);
       heap.suspendOnReader(readerAddr, r3);
       heap.suspendOnReader(r1, record);
       heap.suspendOnReader(r1, SuspensionRecord(1, 10));
       heap.suspendOnReader(r2, SuspensionRecord(2, 20));
       heap.suspendOnReader(r3, SuspensionRecord(3, 30));"
    target_decision: >-
      `heap.SuspendOnReader(readerAddr, record);` (PascalCase per
      heap_fcp.dart.md surface, idiom rf-dart-bind-writer-family-
      callsite-to-csharp-pascalcase-methods, carry-forward from
      binding_pointer_test.dart.md). Per heap_fcp.dart.md, the method
      walks the reader's `Pointer` to the writer cell and appends the
      suspension to the writer's `WriterContent.Suspensions` linked
      list (per the spec citation, "suspensions live on WRITER cells").
      Return type is `void` (the family member that does NOT trigger
      activations — activation happens only on `BindWriter` /
      `BindImportedReader`).
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Naming-convention + WRITER-side-effect nuance (carry-forward KB
      cache hit per FR-012 — REUSE from binding_pointer_test.dart.md).
      Dart instance methods are `lowerCamelCase`; C# instance methods
      are `PascalCase`. Critical semantic nuance (file lead doc-comment
      "Suspensions now live on WRITER cells (not reader cells)"):
      `SuspendOnReader` follows the reader's `Pointer` to find the
      writer; the suspension is APPENDED to the WriterCell, NOT to the
      Reader. Tests in this file ASSERT this (e.g. "suspendOnReader
      follows pointer to find writer" asserts `heap.cells[writerAddr]
      .content is WriterContent` AFTER `suspendOnReader(readerAddr,
      …)`). Codegen MUST preserve this semantic — it is pinned in
      heap_fcp.dart.md.
  - construct_key: dart.method_call.heap_mutator_suspend_on_writer
    source_form: "heap.suspendOnWriter(writerAddr, record);"
    target_decision: >-
      `heap.SuspendOnWriter(writerAddr, record);`. Per heap_fcp.dart.md,
      this method directly appends to the writer cell's
      `WriterContent.Suspensions` without traversing a Pointer. Return
      type is `void`.
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Direct-writer-side-effect nuance (carry-forward KB cache hit per
      FR-012 — REUSE). The "suspendOnWriter adds directly to writer
      cell" test asserts (after `heap.suspendOnWriter(writerAddr,
      record)`) that `heap.cells[writerAddr].content is WriterContent`
      and `wc.suspensions!.record.goalId == 55`. Codegen MUST honour
      the direct (no-pointer-traverse) shape of `SuspendOnWriter` — it
      is the writer-side counterpart of `SuspendOnReader`.
  - construct_key: dart.method_call.heap_mutator_bind_writer_returning_list_goalref
    source_form: >-
      "final activations = heap.bindWriter(writerAddr, ConstTerm('ground'));
       final activations = heap.bindWriter(writerAddr, ConstTerm('value'));
       final acts2 = heap.bindWriter(w2, ConstTerm('final'));
       final activations = heap.bindWriter(w3, ConstTerm('end'));
       final acts = heap.bindWriter(entry.key, entry.value);"
    target_decision: >-
      `var activations = heap.BindWriter(writerAddr, new ConstTerm(
      "ground"));` etc. Return type is `List<GoalRef>` per heap_fcp.
      dart.md construct `dart.bind_writer_family.callback_control_with_
      in_place_mutation_returning_activation_list` (carry-forward from
      binding_pointer_test.dart.md). `GoalRef` is a `readonly record
      struct` per machine_state.dart.md (idiom rf-dart-value-class-
      equality-override-to-csharp-readonly-record-struct) — value-typed,
      copied-by-value into the list.
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Return-list-of-record-struct nuance (carry-forward KB cache hit
      per FR-012 — REUSE). `BindWriter` returns `List<GoalRef>` (NOT
      `IReadOnlyList<GoalRef>`) because heap_fcp.dart.md's source
      returns a freshly built mutable list AND test code calls
      `activations.first.id`, `activations.map(...)`, `activations.
      addAll(acts)` — all of which work on `List<T>`. `GoalRef` is
      value-typed (`readonly record struct`) — no allocation per
      activation; equality is structural over `(Id, Pc)`. The "CommitOps
      Equivalent" test uses `allActivations.addAll(acts)` -> C# `List<
      GoalRef>.AddRange(acts)` (NOT `List.Add(acts)` which would add
      the LIST as a single element).
  - construct_key: dart.method_call.heap_mutator_bind_writer_to_reader_forwarding_returns_list_goalref
    source_form: >-
      "final acts1 = heap.bindWriterToReader(w1, r2);
       heap.bindWriterToReader(w1, r2);
       heap.bindWriterToReader(w2, r3);"
    target_decision: >-
      `var acts1 = heap.BindWriterToReader(w1, r2);`. Per heap_fcp.dart.
      md construct (idiom rf-dart-bind-writer-to-reader-pointer-
      forwarding-suspensions, captured under the bind-writer-family
      idiom), this method (a) sets `cells[w1].Content = new Pointer(
      r2)` keeping `Tag = WrtTag`, (b) FORWARDS any suspensions from
      w1's previous WriterContent to the target writer (the cell at
      `deref(r2)` chain end), and (c) returns the EMPTY activation
      list (suspension forwarding does NOT activate — only `BindWriter`
      with a ground value does). The "Suspension forwarding when
      binding to another variable" test asserts `acts1.isEmpty` AFTER
      this call, then later `BindWriter(w2, …)` returns the previously-
      forwarded activation.
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Suspension-forwarding semantics nuance (LOAD-BEARING — explicitly
      addressed): this is the contract being tested. `BindWriterToReader
      (w1, r2)` MUST NOT activate; suspensions previously parked on w1
      MUST end up on `deref(r2)`'s terminal writer cell. The "Chain of
      variable bindings forwards suspensions correctly" test exercises
      a 3-hop chain (suspension on w1 -> w2 -> w3) and asserts the
      suspension is on w3 AFTER the chain. Codegen MUST preserve the
      heap_fcp.dart.md semantic exactly — any short-cut that activates
      eagerly OR drops suspensions on intermediate writers BREAKS this
      test. Carry-forward from binding_pointer_test.dart.md (same
      idiom, same return type `List<GoalRef>`).
  - construct_key: dart.method_call.runtime_tail_reduce_returning_bool
    source_form: >-
      "final y = rt.tailReduce(g);
       final y26 = rt.tailReduce(g);
       final y1 = rt.tailReduce(g);"
    target_decision: >-
      `var y = rt.TailReduce(g);` (PascalCase per runtime.dart.md
      construct `dart.method.tail_reduce.budget_state_with_fairness_
      helpers`, idiom rf-dart-map-index-null-coalesce-default-to-
      csharp-tryget-ternary). Return type is `bool` (true iff the
      goal's tail-recursion budget was exhausted and reset on this
      call; false otherwise). The "26-step tail recursion budget yields
      and resets" test exercises the contract: 25 calls return false,
      the 26th returns true, the 27th returns false again with the
      budget reset to 25.
    idiom_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      Budget-state-machine nuance (carry-forward KB cache hit per
      FR-012 — REUSE from runtime.dart.md). `TailReduce(g)` consumes
      one budget unit; reset happens when the budget hits 0; the
      method returns true ONLY on the reset-triggering step. This
      file's test calls it 27 times in a `for` loop + 2 trailing
      direct calls, asserting return values per step. Codegen MUST
      preserve the bool-return semantics; the `for (var i = 0; i <
      25; i++)` -> C# `for (int i = 0; i < 25; i++)` carries the
      assertion-on-each-step pattern with `Assert.False(y, …)` /
      `Assert.True(y26, …)`.
  - construct_key: dart.method_call.runtime_budget_of_returning_int
    source_form: "rt.budgetOf(g)"
    target_decision: >-
      `rt.BudgetOf(g)`. Per runtime.dart.md construct `dart.method.
      simple_getter_indexer_pair.budget_of` (idiom rf-dart-map-index-
      null-coalesce-default-to-csharp-tryget-ternary). Return type is
      `int` (the remaining tail-recursion budget for goal `g`,
      defaulting to `TailRecursionBudgetInit` if `g` has no entry).
    idiom_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      Carry-forward KB cache hit per FR-012 — REUSE. The "26-step …
      budget yields and resets" test asserts `expect(rt.budgetOf(g),
      26)` after reset (the budget IS the init constant immediately
      after reset; per machine_state.dart.md `tailRecursionBudgetInit
      = 26`), then `expect(rt.budgetOf(g), 25)` after one further
      `tailReduce`. Codegen MUST preserve the `int` return.
  - construct_key: dart.instance_method_call.suspension_disarm
    source_form: "r1.disarm();"
    target_decision: >-
      `r1.Disarm();`. SUT method per suspension.dart.md (idiom rf-dart-
      shared-mutable-record-by-reference-to-csharp-class) — mutates
      `Armed` to false (or sets `GoalId = null` per the SUT
      implementation choice). The "Disarmed suspensions do not activate"
      test then `BindWriter`s and asserts that r1 does NOT appear in
      the activation list — i.e. the walker SKIPS disarmed entries.
    idiom_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      Mutation-side-effect aliasing nuance (carry-forward KB cache hit
      per FR-012 — REUSE from binding_pointer_test.dart.md). The
      mutation is observable through the heap's suspension list (because
      the heap stores a REFERENCE to the same `SuspensionRecord`
      object); the "disarmed suspensions not activated" test depends
      on this aliasing. Codegen MUST keep `SuspensionRecord` as a
      reference-type class — NOT record struct, NOT plain record class
      (the former would copy-on-aliasing and the latter would
      synthesise structural equality and break identity).
  - construct_key: dart.field_indexer.cells_at_addr_with_member_access_writercontent_pointer
    source_form: >-
      "heap.cells[writerAddr].content;
       heap.cells[readerAddr].content;
       heap.cells[w2].content;
       heap.cells[w3].content;
       heap.cells[writerAddr].content as WriterContent;"
    target_decision: >-
      `heap.Cells[writerAddr].Content` (and the explicit-cast variant
      `((WriterContent)heap.Cells[writerAddr].Content)`). Per heap_fcp.
      dart.md construct `dart.heap_class.master_runtime_state_list_of_
      cells_mutable_hp_callback_map`, the SUT field `Cells` is a
      `List<Cell>` of reference-type cells with a settable `Content`
      property. C# `List<T>` indexer returns the reference for
      reference types, so member access mutations would propagate (not
      exercised in THIS file — all accesses here are reads).
    idiom_id: rf-dart-list-indexing-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexing-to-csharp-list-indexer
    nuance: >-
      Indexer + Cell-as-reference-type nuance (carry-forward KB cache
      hit per FR-012 — REUSE from binding_pointer_test.dart.md /
      cells.dart.md). Cell is `class` (NOT struct) so the indexer
      returns a reference — load-bearing for any assert that reads
      `heap.cells[x].content` and downcasts. If Cell were ever
      converted as a struct, the downcast would still work but a
      hypothetical mutation through the indexer would silently mutate
      a COPY. THIS file performs only reads, so the indexer-returns-
      copy-vs-reference distinction is irrelevant — but the cells.
      dart.md decision (Cell = class) is preserved.
  - construct_key: dart.as_cast.type_assertion_on_cell_content_or_runtime_heap
    source_form: >-
      "rt.heap as HeapFCP;
       heap.cells[writerAddr].content as WriterContent;
       heap.cells[w2].content as WriterContent  // implicit in cast pattern"
    target_decision: >-
      Dart `<expr> as <T>` -> C# `(T)<expr>` (explicit cast — throws
      `InvalidCastException` on mismatch, matching Dart's `TypeError`).
      ALWAYS use the explicit cast `(T)x`, NEVER `x as T` (the latter
      returns null on mismatch in C# — wrong semantics for porting
      Dart `as`). Used for `rt.Heap` -> `HeapFCP` downcast (file lead
      pattern) and for `heap.Cells[writerAddr].Content` -> `WriterContent`
      downcast in the "suspendOnWriter adds directly to writer cell"
      test (to access `.Suspensions.Record.GoalId`).
    idiom_id: rf-dart-as-cast-to-csharp-explicit-cast
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cast-failure-mode nuance (carry-forward KB cache hit per FR-012
      — REUSE from binding_pointer_test.dart.md). Dart `as` throws on
      mismatch; C# `(T)x` throws (InvalidCastException); C# `x as T`
      returns null on mismatch — DIFFERENT semantics. Codegen MUST
      emit `(T)x`. The chained access `(heap.cells[writerAddr].content
      as WriterContent).suspensions!.record.goalId` maps to `((Writer
      Content)heap.Cells[writerAddr].Content).Suspensions!.Record.
      GoalId` with PascalCased members per suspension.dart.md.
  - construct_key: dart.expression.null_assertion_operator_bang_then_member_chain
    source_form: "wc.suspensions!.record.goalId"
    target_decision: >-
      Dart null-assertion operator `!` (postfix bang) maps to C# null-
      forgiving operator `!` (same syntactic position) — both assert
      to the compiler / type-checker that the expression is non-null
      at this point; Dart throws `TypeError` at runtime if it IS null;
      C# null-forgiving is COMPILE-TIME ONLY and does NOT throw at
      runtime (it suppresses CS8602 / CS8629). For PARITY with Dart's
      runtime-throw behaviour, codegen options are: (a) emit `wc.
      Suspensions!.Record.GoalId` (compile-time-only suppression — the
      subsequent member access throws `NullReferenceException` if
      `Suspensions` is null, observably similar to Dart's throw); OR
      (b) emit `wc.Suspensions ?? throw new InvalidOperationException(
      "Suspensions was null")).Record.GoalId` (explicit throw). The
      SPEC PREFERENCE is (a) — the C# member-access NRE is
      observably-equivalent FAIL-FAST behaviour for the test's
      contract (the test ASSERTS that suspensions is non-null
      immediately above the access via `expect(wc.suspensions,
      isNotNull)`), so the `!` post-fix is purely a compile-time
      suppression that mirrors Dart's compile-time `!`.
    idiom_id: null
    research_finding_id: rf-dart-null-assertion-bang-to-csharp-null-forgiving-bang
    nuance: >-
      Null-assertion semantics nuance (FIRST-SEEN — defines a new
      active idiom). Dart `expr!` (Dart null-safety, since 2.12):
      "Dart's null-assertion operator (!) — if the expression to its
      left is null, throws an error" (Dart language tour,
      `https://dart.dev/null-safety/understanding-null-safety#null-
      assertion-operator`). C# `expr!` (null-forgiving operator):
      "Tells the compiler to treat the expression as non-null. Does
      NOT have a runtime effect" (Microsoft Learn,
      `https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/null-forgiving`). Semantic asymmetry explicitly
      addressed: Dart's `!` is BOTH a compile-time hint AND a runtime
      check; C#'s `!` is ONLY a compile-time hint. For the test's
      single use (`wc.suspensions!.record.goalId`), the subsequent
      `.record.goalId` access throws `NullReferenceException` if
      `Suspensions` is null at runtime — observably equivalent FAIL-
      FAST (just a different exception class). The test passes only
      when `Suspensions` is non-null, and the `isNotNull` pre-check
      (varref_pointer_test.dart.md style) is NOT present here —
      instead the test ASSERTS the chain ends in a value comparison
      `equals(55)`, which would fail if any intermediate were null.
      Authoritative both sides (Dart language tour, Microsoft Learn);
      no escalation. NEW idiom registered (active). NOTE: for tests
      that REQUIRE the explicit runtime throw, codegen MUST use the
      `?? throw` form — recorded in the rf.
  - construct_key: dart.member_access.suspension_record_goalid
    source_form: "wc.suspensions!.record.goalId"
    target_decision: >-
      Dart `wc.suspensions!.record.goalId` maps to `wc.Suspensions!.
      Record.GoalId`. Per suspension.dart.md, `SuspensionListNode.
      Record` is `SuspensionRecord` (a reference-type property), and
      `SuspensionRecord.GoalId` is `int?` (nullable — set to null by
      `Disarm()`). The test's `expect(…goalId, equals(55))` -> `Assert.
      Equal(55, …GoalId)` works through the nullable-int automatic
      unwrapping (`Assert.Equal<int?>(55, wc.Suspensions!.Record.GoalId)`
      compares the boxed-nullable values).
    idiom_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      Nullable-int comparison nuance (carry-forward KB cache hit per
      FR-012 — REUSE from suspension.dart.md). `GoalId` is `int?` to
      represent the disarmed-state sentinel; xUnit `Assert.Equal(int,
      int?)` lifts the int to int? for comparison (default equality);
      `Assert.Equal(55, x)` succeeds when `x == 55`. Codegen MUST NOT
      strip the nullable annotation — it is the disarm sentinel.
  - construct_key: dart.expression.list_literal_typed_empty_goalref
    source_form: "<GoalRef>[];"
    target_decision: >-
      Dart typed empty list literal `<GoalRef>[]` maps to C# `new List<
      GoalRef>()`. The `<T>[]` Dart syntax names the element type
      explicitly; C# achieves the same via the constructor's generic
      type argument. GoalRef is `readonly record struct` (value-typed)
      per machine_state.dart.md — `List<GoalRef>` is a list of value-
      typed entries; `List.AddRange` copies the value structs into the
      list.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Typed-empty-list nuance (carry-forward KB cache hit per FR-012
      — REUSE from varref_pointer_test.dart.md). Dart `<T>[]` ↔ C#
      `new List<T>()`. Used once in the "applySigmaHat" test as the
      accumulator (`final allActivations = <GoalRef>[];`); the test
      then `addAll`s into it (see next construct). No `const` list;
      no spread.
  - construct_key: dart.expression.map_literal_typed_int_to_term
    source_form: >-
      "final sigmaHat = <int, Term>{
         w1: ConstTerm('a'),
         w2: ConstTerm('b'),
         w3: ConstTerm('c'),
       };"
    target_decision: >-
      Dart typed map literal `<int, Term>{ … }` with three entries maps
      to C# `new Dictionary<int, Term> { { w1, new ConstTerm("a") },
      { w2, new ConstTerm("b") }, { w3, new ConstTerm("c") } };`
      (collection-initialiser syntax for `Dictionary<TKey, TValue>`
      — Microsoft Learn:
      `https://learn.microsoft.com/dotnet/csharp/programming-guide/
      classes-and-structs/object-and-collection-initializers#dictionary
      -collection-initializers`). The map-literal idiom carry-forwards
      from varref_pointer_test.dart.md (rf-dart-map-literal-typed-to-
      csharp-dictionary). Element type is `Term` (the abstract base of
      `ConstTerm`/`VarRef`/`StructTerm` per terms.dart.md) — the C#
      `Dictionary<int, Term>` accepts `ConstTerm` instances via
      covariant assignment (Term is the declared dictionary value
      type, ConstTerm : Term).
    idiom_id: rf-dart-map-literal-typed-to-csharp-dictionary
    research_finding_id: rf-dart-map-literal-typed-to-csharp-dictionary
    nuance: >-
      Map-literal-with-Term-value carry-forward nuance (KB cache hit
      per FR-012 — REUSE from varref_pointer_test.dart.md). Note the
      subtlety: varref_pointer_test used `<VarRef, String>` (custom
      key with IEquatable); HERE the key is plain `int` so
      `EqualityComparer<int>.Default` is the trivial primitive comparer
      — no `IEquatable` contract dependency. Value-vs-reference: Term
      is a reference-type base; the dictionary holds references to
      ConstTerm instances. Indexer-set `sigmaHat[k] = v` semantics are
      put-or-update (rf nuance carries over).
  - construct_key: dart.expression.foreach_entry_iteration_over_map_entries
    source_form: >-
      "for (final entry in sigmaHat.entries) {
         final acts = heap.bindWriter(entry.key, entry.value);
         allActivations.addAll(acts);
       }"
    target_decision: >-
      Dart `for (final entry in sigmaHat.entries)` maps to C#
      `foreach (var entry in sigmaHat) { … }` — note: C# `Dictionary<
      K, V>` is itself enumerable as `IEnumerable<KeyValuePair<K, V>>`,
      so iterating the dictionary directly yields `KeyValuePair<K, V>`
      with `.Key` and `.Value` properties (exactly mirroring Dart's
      `MapEntry.key` / `MapEntry.value`). The `entry.key` /
      `entry.value` member accesses map to `entry.Key` / `entry.Value`
      (PascalCased). The loop body's `heap.bindWriter(entry.key,
      entry.value)` maps to `heap.BindWriter(entry.Key, entry.Value)`
      and the `allActivations.addAll(acts)` maps to `allActivations.
      AddRange(acts)`.
    idiom_id: null
    research_finding_id: rf-dart-for-in-map-entries-to-csharp-foreach-kvp
    nuance: >-
      Map-iteration nuance (FIRST-SEEN — defines a new active idiom).
      Dart `Map<K, V>.entries` returns `Iterable<MapEntry<K, V>>` (Dart
      language tour, `https://api.dart.dev/stable/dart-core/Map/
      entries.html`). C# `Dictionary<K, V>` implements `IEnumerable<
      KeyValuePair<K, V>>` directly (Microsoft Learn,
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.dictionary-2`); iterating the dictionary yields KVPs
      without an `.Entries` accessor. The `MapEntry.key/value` -> KVP
      `Key/Value` mapping is canonical and authoritative. ORDERING
      nuance explicitly addressed: Dart's default map (LinkedHashMap)
      iterates in insertion order; C# `Dictionary<K, V>` does NOT
      guarantee enumeration order (Microsoft Learn explicitly:
      "Order of items …  is not defined"). For THIS test, the assertion
      `expect(allActivations.length, equals(3))` and `expect(goalIds,
      equals({1, 2, 3}))` are both ORDER-INSENSITIVE — they count
      and set-compare — so the ordering nuance does NOT break the
      test. Codegen MUST NOT depend on Dictionary enumeration order
      in any future port. AddRange call uses `List<T>.AddRange(IEnumer
      able<T>)` (Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.list-1.addrange`) — NOT `Add` (which would add the
      enumerable as a single element). NEW idiom registered (active).
  - construct_key: dart.instance_method_call.list_addall
    source_form: "allActivations.addAll(acts);"
    target_decision: >-
      Dart `List.addAll(Iterable)` maps to C# `List<T>.AddRange(IEnum
      erable<T>)`. NOT `List<T>.Add(T)` (which would add the whole
      list as a single element — `IEnumerable<T>` IS `T` for some
      generic args via implicit conversion to base types — actively
      wrong). Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.list-1.addrange`.
    idiom_id: null
    research_finding_id: rf-dart-list-addall-to-csharp-list-addrange
    nuance: >-
      List-addall nuance (FIRST-SEEN — defines a new active idiom).
      Dart `List<T>.addAll(Iterable<T>)` (dart:core,
      `https://api.dart.dev/stable/dart-core/List/addAll.html`):
      "Appends all objects of [iterable] to the end of this list."
      C# `List<T>.AddRange(IEnumerable<T>)`: "Adds the elements of the
      specified collection to the end of the List<T>." Semantically
      identical. NEW idiom registered (active). Promotion rationale:
      this is the FIRST corpus site to exercise it and the construct
      is structurally simple but pervasive enough across runtime tests
      to warrant KB registration so subsequent files reuse.
  - construct_key: dart.iterable.map_with_arrow_to_set_of_int
    source_form: >-
      "activations.map((a) => a.id).toSet();
       allActivations.map((a) => a.id).toSet();"
    target_decision: >-
      Dart `Iterable<T>.map((x) => f(x)).toSet()` maps to C# LINQ
      `.Select(a => a.Id).ToHashSet()`. Two uses in this file; both
      produce a `Set<int>` / `HashSet<int>` of goal IDs for set-
      equality assertion.
    idiom_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    research_finding_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    nuance: >-
      LINQ Select/ToHashSet carry-forward (KB cache hit per FR-012 —
      REUSE from binding_pointer_test.dart.md). The element type is
      `int` (or the `GoalId` typedef alias) because `GoalRef.Id` is
      `int` per machine_state.dart.md. Requires `using System.Linq;`
      (already in the file-level using directives). Set-equality
      assertion choice: `containsAll([10, 20, 30])` and `equals({1,
      2, 3})` matchers — see expect_containsAll and expect_equals_set
      constructs below.
  - construct_key: dart.constructor_call.suspension_record_in_map_value_position
    source_form: >-
      "heap.suspendOnReader(r1, SuspensionRecord(1, 10));
       heap.suspendOnReader(r2, SuspensionRecord(2, 20));
       heap.suspendOnReader(r3, SuspensionRecord(3, 30));"
    target_decision: >-
      Same as `dart.constructor_call.suspension_record_two_ints_goalid
      _pc` above — `new SuspensionRecord(1, 10)` etc. Inline-construction
      site (no local-variable binding), threaded through method
      argument. C# `new` mandatory; rest is identical.
    idiom_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      Reference-identity nuance (carry-forward, same as above). Inline
      construction creates a fresh reference each call — the heap holds
      the ONLY ref; the test never calls `Disarm` on these particular
      records, so the reference-identity contract is unobserved here
      but still correct.
  - construct_key: dart.expression.for_loop_classical_with_int_counter
    source_form: >-
      "for (var i = 0; i < 25; i++) {
         final y = rt.tailReduce(g);
         expect(y, isFalse, reason: 'should not yield on step ${i + 1}');
       }"
    target_decision: >-
      Dart classical `for (var i = 0; i < 25; i++)` maps to C# `for
      (int i = 0; i < 25; i++)` (or `var i` — both legal). Loop body
      converts statement-for-statement: `final y = rt.tailReduce(g);`
      -> `var y = rt.TailReduce(g);`. The `expect(y, isFalse, reason:
      'should not yield on step ${i + 1}')` -> `Assert.False(y,
      $"should not yield on step {i + 1}")` (string interpolation
      mapping documented below).
    idiom_id: null
    research_finding_id: rf-dart-classical-for-loop-to-csharp-for-loop
    nuance: >-
      Classical for-loop nuance (FIRST-SEEN — defines a new active
      idiom; promoted from "implicit" because every runtime test will
      eventually exercise it). Dart `for (var i = 0; …)` and C# `for
      (int i = 0; …)` are syntactically nearly identical (Microsoft
      Learn: `https://learn.microsoft.com/dotnet/csharp/language-
      reference/statements/iteration-statements#the-for-statement`).
      Increment `i++` identical. The loop body's `final y` -> `var y`
      reuses rf-dart-final-local-to-csharp-var-local. NEW idiom
      registered (active).
  - construct_key: dart.expression.string_interpolation_curly_dollar
    source_form: "'should not yield on step ${i + 1}'"
    target_decision: >-
      Dart `'… ${expr}'` (string interpolation with curly-braced
      expression) maps to C# `$"… {expr}"` (verbatim interpolated
      string — Microsoft Learn:
      `https://learn.microsoft.com/dotnet/csharp/language-reference/
      tokens/interpolated`). The expression syntax inside braces is
      identical (`i + 1` works both sides). C# requires the leading
      `$` sigil; Dart does not (string itself opts in via `${}`).
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      String-interpolation nuance (FIRST-SEEN — defines a new active
      idiom; promoted from implicit). Dart `'… ${expr} …'` and C#
      `$"… {expr} …"` have identical semantics for expression
      interpolation. Dart's `'$identifier'` (bare-identifier form
      without braces) also maps to C# `$"{identifier}"` (C# always
      requires braces). No raw strings in this file; no triple-quoted.
      Authoritative both sides; no escalation. NEW idiom registered
      (active).
  - construct_key: dart.package_test.expect_hasLength
    source_form: >-
      "expect(activations, hasLength(1));
       expect(activations, hasLength(3));
       expect(allActivations, hasLength(3));"
    target_decision: >-
      Dart `expect(<iterable>, hasLength(<n>))` maps to xUnit
      `Assert.Equal(<n>, <iterable>.Count())`. The `Count()` LINQ
      extension (System.Linq.Enumerable, Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.
      count`) works on any `IEnumerable<T>`. For `List<T>` the more
      direct `<list>.Count` (property, not LINQ) is also valid and
      slightly faster; spec preference = `Assert.Equal(n, list.Count)`
      for the concrete `List<GoalRef>` actual values in this file
      (all three uses target `List<GoalRef>` returned by `BindWriter`
      or the test-local accumulator).
    idiom_id: null
    research_finding_id: rf-dart-expect-hasLength-to-xunit-assert-equal-count
    nuance: >-
      Count-vs-Length nuance (FIRST-SEEN — defines a new active idiom).
      Dart `package:matcher` `hasLength(n)`
      (`https://pub.dev/documentation/matcher/latest/matcher/hasLength.
      html`): "Returns a matcher that matches if the match argument has
      a length of the given value." xUnit has no first-class `hasLength`
      assertion; the canonical translation is `Assert.Equal(n, x.Count)`
      for `ICollection<T>` (Microsoft Learn: ICollection.Count) or
      `Assert.Equal(n, x.Length)` for arrays / strings. The argument-
      order swap (n FIRST in xUnit) carry-forwards from rf-dart-expect-
      equals-to-xunit-assert-equal-argorder. For the three uses in this
      file, all targets are `List<GoalRef>` -> `.Count` (NOT `.Length`,
      not LINQ `.Count()`). Authoritative both sides; no escalation.
      NEW idiom registered (active).
  - construct_key: dart.package_test.expect_isA_T
    source_form: >-
      "expect(heap.cells[writerAddr].content, isA<WriterContent>());
       expect(heap.cells[readerAddr].content, isA<Pointer>());
       expect(heap.cells[w2].content, isA<WriterContent>());
       expect(heap.cells[w3].content, isA<WriterContent>());
       expect(heap.cells[writerAddr].content, isA<WriterContent>());
       expect((heap.cells[writerAddr].content as WriterContent).suspensions, isNotNull);
       expect(heap.cells[readerAddr].content, isA<Pointer>());"
    target_decision: >-
      `expect(actual, isA<T>())` -> `Assert.IsType<T>(actual)`. Carry-
      forward KB hit per rf-dart-expect-isA-to-xunit-assert-istype
      (binding_pointer_test.dart.md, varref_pointer_test.dart.md).
      Every target type here (`WriterContent`, `Pointer`,
      `SuspensionListNode`) is a sealed leaf per cells.dart.md /
      suspension.dart.md — `Assert.IsType<T>` (exact match) is
      observably equivalent to `Assert.IsAssignableFrom<T>` AND
      strictly tighter.
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Sealed-leaf nuance (carry-forward KB cache hit per FR-012 —
      REUSE). Same rationale as binding_pointer_test.dart.md: every
      `isA<T>` target is sealed; exact-type assertion is the strictly
      tighter contract that matches Dart's structural intent. Subtype-
      tolerance fallback to `Assert.IsAssignableFrom<T>` recorded in
      rf for the case when a target leaf gains a subtype.
  - construct_key: dart.package_test.expect_isNotNull
    source_form: "expect((heap.cells[writerAddr].content as WriterContent).suspensions, isNotNull);"
    target_decision: >-
      `expect(actual, isNotNull)` -> `Assert.NotNull(actual)`. Used
      once in the "suspendOnReader follows pointer to find writer" test
      to assert the writer's suspension-list head is non-null after
      `SuspendOnReader`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      isNotNull nuance (FIRST-SEEN — defines a new active idiom;
      symmetric counterpart to rf-dart-expect-isNull-to-xunit-assert-
      null from binding_pointer_test.dart.md). Dart `package:matcher`
      `isNotNull` (`https://pub.dev/documentation/matcher/latest/
      matcher/isNotNull.html`): "A matcher that matches any non-null
      value." xUnit `Assert.NotNull(object? actual)`
      (`https://learn.microsoft.com/dotnet/api/xunit.assert.notnull`):
      "Verifies that an object reference is not null." 1-to-1 mapping.
      NEW idiom registered (active).
  - construct_key: dart.package_test.expect_equals_with_goalref_id_or_pc_int
    source_form: >-
      "expect(activations.first.id, g);              // g is const GoalId = 77
       expect(activations.first.pc, kappa);          // kappa is const Pc = 1
       expect(activations.first.id, equals(20));
       expect(acts2.first.id, equals(42));
       expect(acts2.first.pc, equals(500));
       expect(activations.first.id, equals(99));
       expect(rt.budgetOf(g), 26, reason: 'budget resets after yielding');
       expect(rt.budgetOf(g), 25);"
    target_decision: >-
      Map `expect(actual, <expected>)` (no matcher wrapper — Dart
      `expect` shorthand for `expect(a, equals(b))`) AND `expect(actual,
      equals(expected))` (explicit matcher) both to `Assert.Equal(
      expected, actual)` with the argument-order swap. The `reason:`
      named argument maps to the xUnit assertion `userMessage` overload
      (`Assert.Equal<T>(T expected, T actual)` does NOT take a message;
      `Assert.True/False` do; for `Assert.Equal` the test framework
      uses the default diagnostic — codegen MAY drop the reason text
      OR emit `Assert.True(actual == expected, "<reason>")` as a
      readable substitute for primitives). For int / GoalId / Pc
      primitives in this file, the simple `Assert.Equal(expected,
      actual)` is correct.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order swap + reason-arg nuance (carry-forward KB cache
      hit per FR-012 — REUSE). xUnit `Assert.Equal<T>` does NOT accept
      a per-assertion message string (unlike `Assert.True/False`).
      Codegen MUST DROP the Dart `reason:` argument when mapping
      `expect(..., equals(...), reason: ...)` -> `Assert.Equal(...)`,
      OR rewrite to `Assert.True(expected == actual, reason)` if the
      reason is load-bearing for diagnostic clarity. For THIS file's
      two `reason:` uses (`'budget resets after yielding'`, `'should
      not yield on step …'`), both are tail-recursion-loop diagnostic
      hints — non-critical for test correctness; codegen MAY drop
      them OR preserve them via `Assert.True(actual == expected,
      reason)`. Spec preference = preserve via Assert.True for
      diagnostic clarity (the for-loop `Assert.False(y, $"should not
      yield on step {i + 1}")` is the natural fit).
  - construct_key: dart.package_test.expect_isFalse_with_reason
    source_form: "expect(y, isFalse, reason: 'should not yield on step ${i + 1}');"
    target_decision: >-
      `Assert.False(y, $"should not yield on step {i + 1}")`. xUnit
      `Assert.False(bool condition, string userMessage)` (Microsoft
      Learn `xunit.net Assert.False`) accepts a per-assertion message —
      the reason argument maps directly.
    idiom_id: rf-dart-expect-isFalse-to-xunit-assert-false
    research_finding_id: rf-dart-expect-isFalse-to-xunit-assert-false
    nuance: >-
      Reason-arg + interpolation nuance (carry-forward KB cache hit
      per FR-012 — REUSE rf-dart-expect-isFalse-to-xunit-assert-false
      from varref_pointer_test.dart.md). xUnit `Assert.False(bool,
      string)` overload preserves the diagnostic. Interpolated string
      uses C# `$"…{expr}…"` per rf-dart-string-interpolation-to-csharp-
      interpolated-string above. Same shape applies to the symmetric
      `expect(y26, isTrue, reason: 'should yield on step 26')` ->
      `Assert.True(y26, "should yield on step 26")`.
  - construct_key: dart.package_test.expect_isTrue_with_reason
    source_form: "expect(y26, isTrue, reason: 'should yield on step 26');"
    target_decision: >-
      `Assert.True(y26, "should yield on step 26")`. Symmetric to the
      isFalse-with-reason mapping above.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Carry-forward KB cache hit per FR-012 — REUSE rf-dart-expect-
      isTrue-to-xunit-assert-true from binding_pointer_test.dart.md.
      Bare string literal (no interpolation needed) keeps the form
      simple.
  - construct_key: dart.package_test.expect_isEmpty
    source_form: "expect(acts1, isEmpty);"
    target_decision: >-
      `Assert.Empty(acts1)`. Used once in the "Suspension forwarding
      when binding to another variable" test to assert that
      `BindWriterToReader` returns an EMPTY activation list (suspension
      forwarding does not activate).
    idiom_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Carry-forward KB cache hit per FR-012 — REUSE rf-dart-expect-
      isEmpty-to-xunit-assert-empty from binding_pointer_test.dart.md.
      `acts1` is `List<GoalRef>` — both `Assert.Empty(acts1)` and
      `Assert.Equal(0, acts1.Count)` are valid; the dedicated
      assertion gives clearer diagnostic.
  - construct_key: dart.package_test.expect_containsAll_of_int_iterable
    source_form: "expect(goalIds, containsAll([10, 20, 30]));"
    target_decision: >-
      Dart `expect(<set>, containsAll(<iterable>))` (matcher `containsAll`
      asserts the actual contains EVERY element of the expected
      iterable). xUnit has no direct `containsAll` assertion; the
      canonical translation for a `HashSet<T>` actual is `Assert.True(
      goalIds.IsSupersetOf(new[] { 10, 20, 30 }))` (System.Collections.
      Generic.HashSet<T>.IsSupersetOf, Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.hashset-1.issupersetof`). For an `IEnumerable<T>` actual
      (not a HashSet), the LINQ equivalent is `Assert.True(expected.
      All(e => actual.Contains(e)))`. Spec preference: emit
      `Assert.Superset(new HashSet<int> { 10, 20, 30 }, goalIds)` —
      the xUnit-native equivalent (
      `https://xunit.net/docs/comparisons#assertions`, the `Assert.
      Superset(expected, actual)` API verifies that `actual` is a
      superset of `expected`).
    idiom_id: null
    research_finding_id: rf-dart-expect-containsAll-to-xunit-assert-superset
    nuance: >-
      containsAll/superset nuance (FIRST-SEEN — defines a new active
      idiom). Dart `package:matcher` `containsAll(Iterable)`
      (`https://pub.dev/documentation/matcher/latest/matcher/
      containsAll.html`): "Matches any iterable that contains all the
      elements of the given iterable." xUnit `Assert.Superset(ISet
      expected, ISet actual)` (xunit.net Assertions reference):
      "Verifies that the actual set is a superset of the expected
      set." Argument order: xUnit puts the EXPECTED-subset first and
      the ACTUAL-superset second (the OPPOSITE of `Assert.Equal`'s
      order — which is documented at the xunit.net reference). Codegen
      MUST emit `Assert.Superset(new HashSet<int>{ 10, 20, 30 },
      goalIds);` (NOT `Assert.Superset(goalIds, …)` — wrong order).
      Authoritative both sides; no escalation. NEW idiom registered
      (active). Set-equality-vs-superset nuance: `containsAll` does
      NOT enforce size — the actual MAY be strictly larger; `Assert.
      Superset` matches the same semantic.
  - construct_key: dart.package_test.expect_equals_set_literal_of_int
    source_form: "expect(goalIds, equals({1, 2, 3}));"
    target_decision: >-
      `Assert.Equal(new HashSet<int> { 1, 2, 3 }, goalIds)` is the
      LITERAL Assert.Equal translation but it does NOT enforce SET
      equality (xUnit's `Assert.Equal` on two `HashSet<T>` falls back
      to IEnumerable element-wise comparison, which IS order-sensitive
      — wrong for sets). Spec preference (carry-forward from binding_
      pointer_test.dart.md's rf-dart-iterable-map-toset-to-csharp-linq-
      select-tohashset nuance): emit `Assert.True(goalIds.SetEquals(
      new[] { 1, 2, 3 }))` — `HashSet<T>.SetEquals` is the order-
      insensitive set-equality assertion (Microsoft Learn:
      `https://learn.microsoft.com/dotnet/api/system.collections.
      generic.hashset-1.setequals`).
    idiom_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    research_finding_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    nuance: >-
      Set-equality semantics nuance (carry-forward KB cache hit per
      FR-012 — REUSE the binding_pointer_test.dart.md decision):
      `goalIds.SetEquals(new[] { 1, 2, 3 })` is the SET-semantic
      assertion. xUnit `Assert.Equal` on collections is sequence
      semantics — observably correct ONLY if the iteration order
      happens to match. For THIS test, the source is `allActivations
      .map((a) => a.id).toSet()` after iterating a Dart Map whose
      `.entries` order is insertion-order (w1, w2, w3 -> ids 1, 2, 3);
      the C# Dictionary iteration order is UNDEFINED (see for-in-map-
      entries nuance above). Therefore `Assert.Equal` would be
      potentially flaky in C# — `SetEquals` is the correctness
      preserving form. Spec MANDATES `SetEquals`.
conversion_units:
  - cu-1: file-scope `namespace <RootNs>.Test.Heap;` (file-scoped namespace mirroring the test/heap path)
  - cu-2: file-scope using directives — `using Xunit;`, `using System;`, `using System.Collections.Generic;`, `using System.Linq;`, `using <RootNs>.Runtime;` (single using covers all five SUT imports — `runtime.dart`, `machine_state.dart`, `suspension.dart`, `heap_fcp.dart`, `terms.dart` all land in `<RootNs>.Runtime`; the `show` clause is dropped)
  - cu-3: file-scope using-alias directives mirroring the GoalId/Pc typedefs — `using GoalId = System.Int32; using Pc = System.Int32;` (per machine_state.dart.md idiom rf-dart-typedef-int-to-csharp-global-using-alias)
  - cu-4: NO equivalent of Dart `void main()` (dropped per rf-dart-package-test-main-omit-in-xunit) — xUnit attribute-driven discovery replaces it
  - cu-5: top-level test class `SuspensionAndReactivationPointerArchitectureTests` — 7 `[Fact(DisplayName = "<original Dart test label>")]` methods (`OnWakeActivationPcEqualsKappaRestartAtClause1` / `MultipleSuspensionsOnSameVariableAllActivate` / `DisarmedSuspensionsDoNotActivate` / `SuspensionForwardingWhenBindingToAnotherVariable` / `ChainOfVariableBindingsForwardsSuspensionsCorrectly` / `SuspendOnWriterAddsDirectlyToWriterCell` / `SuspendOnReaderFollowsPointerToFindWriter`), each starting with `var rt = new GlpRuntime(); var heap = (HeapFCP)rt.Heap;`
  - cu-6: top-level test class `FairnessBudgetPointerArchitectureTests` — 1 `[Fact]` method (`TwentySixStepTailRecursionBudgetYieldsAndResets`, `DisplayName = "26-step tail recursion budget yields and resets"`) — leading-digit identifier reshape; body has a `for (int i = 0; i < 25; i++)` loop + 2 trailing direct calls; uses `Assert.False(y, $"should not yield on step {i + 1}")` and `Assert.True(y26, "should yield on step 26")` for diagnostic preservation
  - cu-7: top-level test class `CommitOpsEquivalentPointerArchitectureTests` — 1 `[Fact]` method (`ApplySigmaHatBindsMultipleVariablesAndCollectsActivations`); body sets up 3 writers + 3 suspensions + a `Dictionary<int, Term>` literal (collection initialiser), iterates via `foreach (var entry in sigmaHat)`, `AddRange`s the activations into a `List<GoalRef>` accumulator, and asserts `Count == 3` plus `SetEquals({1, 2, 3})`
  - cu-8: omit constructor and `IDisposable` on all three classes (no `setUp` / `tearDown` in source); per-test fresh-state via inline `var rt = new GlpRuntime();` is intentional
  - cu-9: each test method body translates statement-for-statement — `final` -> `var`, `<expr> as <T>` -> `(T)<expr>` (explicit cast, NOT `x as T`), Dart `expr!` -> C# `expr!` (compile-time-only; runtime fail-fast comes from the subsequent member-access NRE — semantically equivalent for the single use here), Dart `'${expr}'` -> C# `$"{expr}"`
  - "cu-10: first-seen idiom registrations (idiom_id null on these construct entries; rf-* row defines each) — rf-dart-null-assertion-bang-to-csharp-null-forgiving-bang, rf-dart-for-in-map-entries-to-csharp-foreach-kvp, rf-dart-list-addall-to-csharp-list-addrange, rf-dart-classical-for-loop-to-csharp-for-loop, rf-dart-string-interpolation-to-csharp-interpolated-string, rf-dart-expect-hasLength-to-xunit-assert-equal-count, rf-dart-expect-isNotNull-to-xunit-assert-notnull, rf-dart-expect-containsAll-to-xunit-assert-superset — subsequent test convspecs MUST reuse via the KB rather than re-derive"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### Carry-forward idioms (KB reuse only — FR-012 / SC-007)

The following idioms are REUSED VERBATIM from prior convspecs (KB
cache hit; NO research, NO re-derivation per FR-012):

- `rf-dart-library-directive-to-csharp-namespace-elision`
  (terms.dart.md): `library;` -> file-scoped `namespace …;`.
- `rf-dart-package-test-import-to-xunit-using`
  (smoke_test.dart.md, boot_loader_test.dart.md, binding_pointer_test.
  dart.md, varref_pointer_test.dart.md): `package:test` -> `using Xunit;`.
- `rf-dart-import-relative-to-csharp-using-namespace`
  (runtime.dart.md): `package:glp_runtime/runtime/...` -> `using <RootNs>
  .Runtime;`. Five Dart imports collapse to one. `show` clause has no
  C# counterpart (idiom rf-dart-import-show-clause-no-csharp-counterpart
  from runtime.dart.md — the `show HeapFCP, Pointer, SuspensionListNode,
  WriterContent` clause is dropped).
- `rf-dart-package-test-main-omit-in-xunit` (boot_loader_test.dart.md):
  drop `void main()` entirely; xUnit is attribute-driven.
- `rf-dart-package-test-group-to-xunit-class`
  (varref_pointer_test.dart.md flat-siblings sub-case): one xUnit class
  per Dart group, three classes total for the three SIBLING groups
  here, no shared `setUp` / `late` field.
- `rf-dart-test-callback-to-xunit-method-body` (smoke_test.dart.md):
  one `[Fact(DisplayName="<label>")]` method per `test()`.
- `rf-dart-final-local-to-csharp-var-local` (multiple precedents):
  `final <name> = <expr>` -> `var <name> = <expr>`.
- `rf-dart-as-cast-to-csharp-explicit-cast` (binding_pointer_test.dart.md):
  `<expr> as <T>` -> `(T)<expr>` (explicit cast — NOT C# `x as T`).
- `rf-dart-record-destructure-to-csharp-valuetuple-deconstruction`
  (external_io.dart.md): `final (a, b) = expr` -> `var (a, b) = expr`.
- `rf-dart-typedef-int-to-csharp-global-using-alias`
  (machine_state.dart.md): `typedef GoalId = int;` -> `using GoalId =
  System.Int32;`. `const GoalId g = 77;` -> `const GoalId g = 77;`
  verbatim (typedef alias name preserved).
- `rf-dart-mutable-state-class-identity-equality-to-csharp-class`
  (runtime.dart.md): `GlpRuntime`/`rt.heap`/`rt.tailReduce`/
  `rt.budgetOf` -> PascalCase C# instance API on `GlpRuntime` reference-
  type class.
- `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`
  (binding_pointer_test.dart.md): `heap.suspendOnReader` /
  `heap.suspendOnWriter` / `heap.bindWriter` / `heap.bindWriterToReader`
  -> PascalCase C# methods; return-list shape `List<GoalRef>`
  preserved.
- `rf-dart-shared-mutable-record-by-reference-to-csharp-class`
  (suspension.dart.md): `SuspensionRecord` reference-type class with
  mutable Armed/GoalId state; `Disarm()` mutates aliased state observed
  through heap suspension list.
- `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`
  (terms.dart.md): `ConstTerm('x')` -> `new ConstTerm("x")`; reference-
  identity preserved (no record class).
- `rf-dart-list-indexing-to-csharp-list-indexer`
  (binding_pointer_test.dart.md): `heap.cells[i]` -> `heap.Cells[i]`.
- `rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary`
  (runtime.dart.md): `rt.tailReduce(g)` / `rt.budgetOf(g)` use the
  internal budget map; the bool / int return is the visible contract.
- `rf-dart-list-literal-to-csharp-list-initializer`
  (varref_pointer_test.dart.md): `<GoalRef>[]` -> `new List<GoalRef>()`.
- `rf-dart-map-literal-typed-to-csharp-dictionary`
  (varref_pointer_test.dart.md): `<int, Term>{ … }` -> `new Dictionary
  <int, Term> { … }` with collection-initialiser entries.
- `rf-dart-expect-equals-to-xunit-assert-equal-argorder`
  (smoke_test.dart.md): `expect(actual, equals(X))` /
  `expect(actual, X)` shorthand -> `Assert.Equal(X, actual)` with
  argument-order swap.
- `rf-dart-expect-isA-to-xunit-assert-istype` (binding_pointer_test.
  dart.md, varref_pointer_test.dart.md): `isA<T>()` (sealed leaf) ->
  `Assert.IsType<T>(actual)`.
- `rf-dart-expect-isTrue-to-xunit-assert-true` (smoke_test.dart.md):
  `isTrue` -> `Assert.True`.
- `rf-dart-expect-isFalse-to-xunit-assert-false` (varref_pointer_test.
  dart.md): `isFalse` -> `Assert.False`.
- `rf-dart-expect-isEmpty-to-xunit-assert-empty` (binding_pointer_test.
  dart.md): `isEmpty` -> `Assert.Empty`.
- `rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset`
  (binding_pointer_test.dart.md): `.map((a) => a.id).toSet()` ->
  `.Select(a => a.Id).ToHashSet()`; `equals({…})` matcher ->
  `Assert.True(actual.SetEquals(…))` for SET-equality semantics
  (NOT `Assert.Equal` which is sequence-semantics on HashSets).

### NEW first-seen idioms registered in THIS artifact (FR-012 / FR-024)

The following are FIRST-SEEN in the test-suite convspec corpus.
Each is grounded in BOTH official Dart and official .NET documentation
(FR-024 authoritative-both-sides); none requires escalation.

#### rf-dart-null-assertion-bang-to-csharp-null-forgiving-bang — `expr!` (Dart null-assertion) -> `expr!` (C# null-forgiving)

- **Deep analysis**: this file uses `wc.suspensions!.record.goalId`
  at one site to extract a goal ID from a nullable
  `SuspensionListNode?` field. Dart's `!` is BOTH a compile-time
  hint AND a runtime throw-on-null; C#'s `!` is COMPILE-TIME ONLY.
  The runtime fail-fast in C# comes from the subsequent member-access
  `NullReferenceException` — observably equivalent for the test's
  contract.
- **Authoritative Dart**: `https://dart.dev/null-safety/understanding-
  null-safety#null-assertion-operator` — "If the expression is null,
  null assertion throws a runtime exception."
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/csharp/
  language-reference/operators/null-forgiving` — "The null-forgiving
  operator has no effect at runtime."
- **Conclusion**: emit `expr!` in C# for the compile-time hint; rely
  on the subsequent member-access NRE for the runtime fail-fast. For
  tests that REQUIRE the explicit runtime throw at the `!` site
  itself, codegen MUST use `expr ?? throw new InvalidOperationException
  ("…")` — recorded in the rf. Authoritative both sides; no
  escalation. NEW idiom registered (active).

#### rf-dart-for-in-map-entries-to-csharp-foreach-kvp — `for (final e in map.entries)` -> `foreach (var e in dict)`

- **Deep analysis**: this file iterates a `Map<int, Term>` via
  `for (final entry in sigmaHat.entries) { … }` to drive a batch
  `bindWriter` operation. Dart `Map<K,V>.entries` returns
  `Iterable<MapEntry<K, V>>`; C# `Dictionary<K, V>` directly
  implements `IEnumerable<KeyValuePair<K, V>>` so `foreach (var e in
  dict)` yields KVPs without an `.Entries` accessor.
- **Authoritative Dart**: `https://api.dart.dev/stable/dart-core/Map/
  entries.html` — `Iterable<MapEntry<K, V>> get entries;`.
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/api/
  system.collections.generic.dictionary-2` — "Dictionary implements
  IEnumerable<KeyValuePair<TKey, TValue>>." Microsoft Learn note on
  iteration order: "The order in which the items are returned is
  undefined."
- **Conclusion**: `foreach (var entry in dict) { … entry.Key …
  entry.Value … }`. Ordering nuance documented (Dart insertion-order
  vs C# undefined) — not load-bearing for this test (assertions are
  order-insensitive Count and SetEquals). NEW idiom registered (active).

#### rf-dart-list-addall-to-csharp-list-addrange — `List.addAll(Iterable)` -> `List.AddRange(IEnumerable)`

- **Deep analysis**: this file uses `allActivations.addAll(acts)` to
  accumulate per-iteration activations into a flat list. Direct .NET
  counterpart: `List<T>.AddRange`.
- **Authoritative Dart**: `https://api.dart.dev/stable/dart-core/List/
  addAll.html` — "Appends all objects of iterable to the end of this
  list."
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/api/
  system.collections.generic.list-1.addrange` — "Adds the elements
  of the specified collection to the end of the List<T>."
- **Conclusion**: 1-to-1 mapping; DO NOT use `Add(IEnumerable)` (would
  add the whole enumerable as a single element if generic args
  permitted, OR not compile). NEW idiom registered (active).

#### rf-dart-classical-for-loop-to-csharp-for-loop — `for (var i = 0; …; i++)` -> `for (int i = 0; …; i++)`

- **Deep analysis**: the "26-step tail recursion budget …" test runs
  a classical 25-iteration counted loop. Dart and C# share nearly
  identical syntax (Dart `var i` vs C# `int i` / `var i` — both
  legal).
- **Authoritative Dart**: Dart language tour, iteration statements
  (`https://dart.dev/language/loops`).
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/csharp/
  language-reference/statements/iteration-statements#the-for-statement`.
- **Conclusion**: emit C# `for (int i = 0; i < N; i++)` (or `var i`).
  Identical body translation. NEW idiom registered (active).
  Promotion rationale: structural-simple but pervasive; pinning
  saves re-derivation effort across the test corpus.

#### rf-dart-string-interpolation-to-csharp-interpolated-string — `'${expr}'` -> `$"{expr}"`

- **Deep analysis**: the "26-step …" test uses `'should not yield on
  step ${i + 1}'`. Dart string interpolation accepts both
  `'$identifier'` (bare) and `'${expression}'` (braced); C# uses the
  leading `$` sigil and always-braced `{expr}` form.
- **Authoritative Dart**: Dart language tour, strings
  (`https://dart.dev/language/built-in-types#strings`).
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/csharp/
  language-reference/tokens/interpolated`.
- **Conclusion**: emit `$"… {expr} …"`. NEW idiom registered (active).
  Promotion rationale: structural-simple but pervasive.

#### rf-dart-expect-hasLength-to-xunit-assert-equal-count — `hasLength(N)` -> `Assert.Equal(N, x.Count)`

- **Deep analysis**: three uses in this file, all targeting
  `List<GoalRef>` actuals. xUnit has no native `hasLength` assertion;
  the canonical translation is `Assert.Equal(N, x.Count)` (or `.Length`
  for arrays/strings).
- **Authoritative Dart**: `https://pub.dev/documentation/matcher/
  latest/matcher/hasLength.html` — "Returns a matcher that matches
  if the match argument has a length of the given value."
- **Authoritative .NET**: `https://xunit.net/docs/comparisons` (xUnit
  Assert API). `List<T>.Count` property
  (`https://learn.microsoft.com/dotnet/api/system.collections.generic.
  list-1.count`).
- **Conclusion**: `Assert.Equal(N, list.Count)` with argument-order
  swap (N first, per rf-dart-expect-equals-to-xunit-assert-equal-
  argorder). NEW idiom registered (active).

#### rf-dart-expect-isNotNull-to-xunit-assert-notnull — `isNotNull` -> `Assert.NotNull`

- **Deep analysis**: symmetric counterpart to rf-dart-expect-isNull-
  to-xunit-assert-null. Used once in the "suspendOnReader follows
  pointer to find writer" test to assert the writer's `Suspensions`
  field is non-null after `SuspendOnReader`.
- **Authoritative Dart**: `https://pub.dev/documentation/matcher/
  latest/matcher/isNotNull.html`.
- **Authoritative .NET**: `https://learn.microsoft.com/dotnet/api/
  xunit.assert.notnull`.
- **Conclusion**: 1-to-1 mapping. NEW idiom registered (active).

#### rf-dart-expect-containsAll-to-xunit-assert-superset — `containsAll([…])` -> `Assert.Superset(new HashSet<T>{…}, actual)`

- **Deep analysis**: the "Multiple suspensions on same variable all
  activate" test uses `expect(goalIds, containsAll([10, 20, 30]))`
  to assert that the activation set is a superset of the expected
  goal-id list (independent of set size). xUnit has a dedicated
  `Assert.Superset(expected, actual)` for exactly this case.
- **Authoritative Dart**: `https://pub.dev/documentation/matcher/
  latest/matcher/containsAll.html` — "Matches any iterable that
  contains all the elements of the given iterable."
- **Authoritative .NET**: xunit.net Assertions reference
  (`https://xunit.net/docs/comparisons#assertions`) documents
  `Assert.Superset(ISet expected, ISet actual)`. Also Microsoft
  Learn `HashSet<T>.IsSupersetOf`
  (`https://learn.microsoft.com/dotnet/api/system.collections.generic.
  hashset-1.issupersetof`) as the underlying primitive.
- **Conclusion**: `Assert.Superset(new HashSet<int>{ 10, 20, 30 },
  goalIds);` — note argument order (expected-subset FIRST). Authoritative
  both sides; no escalation. NEW idiom registered (active).

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — every test method is synchronous, so every
  `[Fact]` returns `void` (not `async Task`).
- No `late`, `mixin`, `extension`, generics-with-bounds, sealed/abstract
  declarations (the SUT types referenced — `GlpRuntime`, `HeapFCP`,
  `WriterContent`, `Pointer`, `SuspensionListNode`, `SuspensionRecord`,
  `ConstTerm`, `GoalRef`, etc. — ARE sealed / record-struct / class
  per their respective lib/runtime convspecs, but this test file
  declares no types of its own).
- The file lead doc-comment is LOAD-BEARING (cites heap-pointer-
  architecture-spec v3.0 and documents the "suspensions live on
  WRITER cells (not reader cells)" semantic that the test ASSERTS).
  Codegen MUST preserve it as the first test class's XML doc-comment.
- The `(GoalId, Pc)` typedef-alias pair from machine_state.dart.md
  is preserved at the file using-alias level so `const GoalId g =
  77;` and `const Pc kappa = 1;` translate verbatim — without the
  alias, the test's documentation of "opaque-int identifier kind"
  is lost.
- The `r1.disarm()` aliasing nuance is the load-bearing test for
  suspension.dart.md's reference-type decision: without reference-
  identity, the disarm would mutate a copy and the heap's suspension
  list would still see armed = true, breaking the "Disarmed
  suspensions do not activate" assertion.
- The `bindWriterToReader` suspension-forwarding nuance is the load-
  bearing test for heap_fcp.dart.md's pointer-following + suspension-
  forwarding contract. The "Chain of variable bindings" test
  exercises a 3-hop chain; codegen MUST honour the no-eager-activation
  rule.
- The `tailReduce` budget-state-machine is the load-bearing test for
  runtime.dart.md's `_budgets` map + null-coalesce-default machinery.
  Codegen MUST keep the budget map keyed by `GoalId` (typedef alias
  for int) with `tailRecursionBudgetInit = 26` per machine_state.
  dart.md.
- The `<int, Term>{ w1: …, w2: …, w3: … }` map literal in the
  CommitOps test exercises the dictionary-of-Term-values shape; the
  `foreach (var entry in sigmaHat)` iteration is the FIRST-SEEN
  map-entry-iteration pattern in the test convspec corpus and
  registers the rf-dart-for-in-map-entries-to-csharp-foreach-kvp
  idiom for reuse.
- Zero escalations: every construct in this file is authoritative-
  supported on both sides; every nuance (value-vs-reference,
  reference-aliasing, null-assertion runtime-vs-compile-time semantic,
  set-equality vs sequence-equality, map-iteration ordering, return-
  list-of-record-struct) is explicitly addressed per FR-009 /
  FR-010 / SC-006 / US2-AS4. Eight first-seen idioms are registered
  (active); twenty-one carry-forward idioms are reused verbatim from
  the KB per FR-012 / SC-007.
