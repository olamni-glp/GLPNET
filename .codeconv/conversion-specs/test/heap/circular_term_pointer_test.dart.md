> Conversion-spec artifact for test/heap/circular_term_pointer_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion; contains
> NO compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/heap/circular_term_pointer_test.dart
source_sha256: b239cd5fda24cb63efa4f4406ee8b94ad54679a79761f06f188268f7cbd60dff
target_code_unit: test/heap/CircularTermPointerTest.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "library;"
    target_decision: >-
      Elide entirely; C# files have no `library` directive. The
      doc-comment block ABOVE `library;` (description of circular-term
      handling, provenance `Adapted from: test/circular_term_test.dart`
      and `For spec: docs/heap-pointer-architecture-spec.md v3.0`) is
      preserved as the test class XML doc-comment on
      `CircularTermPointerTests`.
    idiom_id: rf-dart-library-directive-to-csharp-namespace-elision
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Same nuance as every `library;` in the heap test family
      (binding_pointer_test, varref_pointer_test,
      suspension_pointer_test): Dart library scope = the file; C# scope
      is `namespace`. No name to carry; the per-file doc-comment
      becomes the test-class XML doc-comment so the spec-citation
      provenance (`docs/heap-pointer-architecture-spec.md v3.0`) and
      the "Circular terms can form through cross-goal communication..."
      explanation survive the conversion.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. Project-wide pinning: xUnit
      is the chosen .NET test framework (precedent: every prior
      `package:test` spec including binding_pointer_test.dart.md,
      varref_pointer_test.dart.md, suspension_pointer_test.dart.md,
      partial_evaluator_test.dart.md). Codegen MUST also add
      `using System;` (for `Action` lambdas in returnsNormally) and
      project to a single namespace mirroring the Dart `test/heap`
      directory (e.g. `<RootNs>.Test.Heap`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance, NOT a
      file-local choice: every `package:test` file in the inventory MUST
      map to the SAME .NET framework (xUnit). Reused verbatim from
      binding_pointer_test convspec — NO re-research (FR-024 cached).
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/heap_fcp.dart';
       import 'package:glp_runtime/runtime/system_predicates.dart';
       import 'package:glp_runtime/runtime/system_predicates_impl.dart';"
    target_decision: >-
      Five `package:glp_runtime/runtime/...` imports collapse to ONE
      `using <RootNs>.Runtime;` (the converted runtime sub-namespace
      decided collectively by lib/runtime/runtime.dart.md,
      lib/runtime/terms.dart.md, lib/runtime/heap_fcp.dart.md,
      lib/runtime/system_predicates.dart.md,
      lib/runtime/system_predicates_impl.dart.md). C# `using` is
      per-namespace, not per-file — multiple Dart imports into the SAME
      C# namespace collapse. The test assembly must reference the SUT
      assembly via the project file (project-system idiom; out of scope
      for THIS artifact).
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: in Dart each `package:` URI is a
      separate import; in C# all five converted files share the runtime
      namespace, so a single `using` suffices. The `copyTermPredicate`
      free function (Dart top-level) is hosted by
      system_predicates_impl.dart.md as a `public static` method on a
      `SystemPredicates` host class — see the `copy_term system call`
      construct below.
  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
         group('Circular Term Handling - Pointer Architecture', () { ... });
       }"
    target_decision: >-
      Eliminate `main` entirely; xUnit discovers `[Fact]` methods by
      reflection — there is NO per-file entrypoint to emit. The
      single outer `group(...)` call inside `main`'s body becomes the
      enclosing test class with nested-group `[Trait]`-grouped methods
      (see `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook — only per-class
      (constructor + `IDisposable.Dispose`) and per-collection
      fixtures. THIS file's `main` body is one outer `group(...)` with
      no other statements, so the omission is lossless.
  - construct_key: dart.package_test.group_block.one_outer_five_inner_with_late_runtime_setup
    source_form: >-
      "group('Circular Term Handling - Pointer Architecture', () {
         late GlpRuntime rt;
         setUp(() { rt = GlpRuntime(); });
         group('Ground Guard with Circular Terms', () { test(...); test(...); });
         group('Equality (=?=) with Circular Terms', () { test(...); test(...); });
         group('Deep Copy with Circular Terms', () { test(...); test(...); });
         group('Term Formatter with Circular Terms', () { test(...); });
         group('Dereferencing Circular Terms', () { test(...); test(...); });
       });"
    target_decision: >-
      One outer group + FIVE inner groups + `late GlpRuntime rt` with
      `setUp` → map to a SINGLE PascalCase xUnit test class
      `CircularTermPointerTests` containing ALL test methods. The outer
      group label "Circular Term Handling - Pointer Architecture" is
      preserved as the test class XML doc-comment (alongside the
      file-level doc-comment). Each inner group's label is preserved
      verbatim as `[Trait("Group", "<label>")]` on every test method
      belonging to that group. The `setUp(() { rt = GlpRuntime(); })`
      maps to an xUnit per-test class constructor that initialises a
      `GlpRuntime _rt;` field (xUnit creates a fresh test-class
      instance per test, so a field-and-constructor is observably
      equivalent to Dart's per-test `setUp`). Per-test method names
      are inner-group-prefixed PascalCased identifier-safe forms of
      the label so collisions across groups are impossible (e.g.
      `GroundGuardWithCircularTerms_CircularTermWithoutUnboundVariablesIsGround`,
      `EqualityWithCircularTerms_IdenticalCircularTermsAreEqual`,
      `DeepCopyWithCircularTerms_CopyOfCircularTermPreservesStructure`,
      `TermFormatterWithCircularTerms_CircularTermDoesNotCauseInfiniteLoopInToString`,
      `DereferencingCircularTerms_NestedCircularReferencesWorkCorrectly`).
      The original test label MUST be preserved verbatim via
      `[Fact(DisplayName = "<original label>")]`.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-group nuance (explicitly addressed, differs from
      binding_pointer_test which had SIX SIBLING TOP-LEVEL groups +
      NO `late` field): this file has ONE OUTER + FIVE INNER + a
      `late GlpRuntime rt` hoisted via `setUp`. Two encoding choices
      were considered: (A) FLATTEN to a single test class with inner
      `[Trait("Group", ...)]` (chosen — matches boot_loader_test
      precedent and binding_pointer_test); (B) nested partial classes
      / collections (rejected — over-translation; xUnit `[Trait]`
      partitioning already groups in VS Test Explorer / Rider /
      `dotnet test --logger trx`). Codegen MUST emit per-test class
      instantiation behaviour: a `_rt` field initialised in the
      constructor produces the same fresh-runtime-per-test contract
      as Dart's `setUp`. Async nuance not exercised (all tests are
      synchronous).
  - construct_key: dart.package_test.test_call_simple
    source_form: >-
      "test('<label>', () { /* arrange (heap.allocateVariable / VarRef /
       StructTerm / ConstTerm), act (heap.bindWriter /
       heap.derefAddr / heap.getValue / heap.isFullyBound /
       copyTermPredicate), assert (expect ...) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance
      method on `CircularTermPointerTests`, decorated with
      `[Fact(DisplayName = "<original label>")]` plus
      `[Trait("Group", "<enclosing inner group label>")]`. The method
      name is the group-prefixed PascalCased label (see group_block).
      The closure body converts statement-for-statement into the
      method body. All NINE `test` calls in this file are synchronous
      (no `async`/`Future`) so no target method is `async Task`.
      Tests access the shared `_rt` field from the constructor; they
      do NOT re-allocate a runtime per test.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Shared-state nuance (explicitly addressed, differs from
      binding_pointer_test): binding_pointer_test had per-test
      `final heap = HeapFCP();` inline (no shared field); THIS file
      uses `late GlpRuntime rt` initialised in `setUp`. xUnit's
      per-test class instantiation gives the SAME per-test isolation
      as Dart `setUp` (each `[Fact]` runs against a fresh
      `CircularTermPointerTests` instance with a fresh `_rt`).
      Codegen MUST NOT promote `_rt` to `static` — that would break
      isolation.
  - construct_key: dart.local_var.late_runtime_field_initialised_in_setup
    source_form: "late GlpRuntime rt; setUp(() { rt = GlpRuntime(); });"
    target_decision: >-
      Dart `late GlpRuntime rt;` declared at group scope, assigned in
      `setUp`, becomes a private field `private readonly GlpRuntime
      _rt;` on the xUnit test class. The constructor assigns it:
      `public CircularTermPointerTests() { _rt = new GlpRuntime(); }`.
      Per-test class instantiation (xUnit's default) preserves the
      Dart `setUp`-per-test contract.
    idiom_id: null
    research_finding_id: rf-dart-late-field-with-setup-to-csharp-readonly-field-ctor
    nuance: >-
      Late-init nuance (explicitly addressed): Dart `late` defers
      initialisation; C# `readonly` field initialised in the
      constructor is the canonical equivalent (xUnit docs:
      `https://xunit.net/docs/comparisons#setup`). `readonly` is
      chosen because no test reassigns `rt` (the field is read-only
      after construction — symmetric to Dart `final` + `late`). The
      backing field is `_rt` (Microsoft naming convention) but the
      DisplayName-preserved test bodies access it via `_rt.Heap...`.
      No `setUp` parameter — xUnit constructor takes no arguments
      (an `ITestOutputHelper` would be optional but unused here).
  - construct_key: dart.local_var.final_constructor_instance_or_record_destructure
    source_form: >-
      "final (writerAddr, readerAddr) = rt.heap.allocateVariable();
       final (xWriter, xReader) = rt.heap.allocateVariable();
       final (yWriter, yReader) = rt.heap.allocateVariable();
       final (copyWriter, _) = rt.heap.allocateVariable();
       final circularStruct = StructTerm('f', [VarRef(readerAddr)]);
       final circularX = StructTerm('f', [VarRef(xReader)]);
       final value = rt.heap.getValue(writerAddr);
       final struct = value as StructTerm;
       final call = SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)]);
       final result = copyTermPredicate(rt, call);
       final copyValue = rt.heap.getValue(copyWriter);
       final copyStruct = copyValue as StructTerm;
       final resultX = rt.heap.derefAddr(xWriter);
       final resultY = rt.heap.derefAddr(yWriter);"
    target_decision: >-
      Dart `final <T> x = <expr>;` and `final (a, b) = <expr>;` map
      uniformly to C# `var x = <expr>;` and `var (a, b) = <expr>;`
      for method-local lifetimes. `final` is assignment-once; `var`
      produces a non-readonly local but the single-assignment shape is
      preserved by the converted body (no reassignment of any of
      these locals occurs). C# 7+ tuple-deconstruction supports `_`
      as a discard in the `(copyWriter, _)` position. The SUT method
      `allocateVariable` returns `(long, long)` per heap_fcp.dart.md
      (idiom rf-dart-record-return-to-csharp-valuetuple) — every
      deconstructed local in this file is `long`.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Width nuance: Dart `int` (address slot) MUST map to C# `long`
      to preserve address-arithmetic width (per cells.dart.md
      construct `dart.int.fixed_width_identity_field`, idiom
      rf-dart-int-to-csharp-long-width). Codegen MUST keep
      deconstructed names typed as `long`, not `int`. Single-versus-
      destructured nuance: `final value = rt.heap.getValue(...);` →
      `var value = _rt.Heap.GetValue(...);`. Cast-by-final nuance:
      `final struct = value as StructTerm;` introduces a name shadow
      (the Dart local `struct` is also a C# keyword) — codegen MUST
      rename to `structTerm` or `castStruct` to avoid the keyword
      collision (`@struct` is legal but discouraged; the precedent
      from binding_pointer_test and varref_pointer_test renames
      where collisions would arise).
  - construct_key: dart.constructor_call.const_term_with_value
    source_form: "ConstTerm('a'), ConstTerm('b')"
    target_decision: >-
      Dart positional `ConstTerm(<lit>)` maps to C# `new
      ConstTerm(<lit>)`. SUT type per terms.dart.md construct
      `dart.sum_type_leaf.value_carrying_no_eq_override_reference_identity`
      is a `sealed class ConstTerm : Term` with a single nullable
      `object? Value` field. Both uses in this file pass a single
      string literal (`'a'`, `'b'`) which boxes transparently into
      `object?`.
    idiom_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Reuse nuance: ConstTerm conversion is fully pinned by
      terms.dart.md and reused verbatim from binding_pointer_test +
      varref_pointer_test + suspension_pointer_test. NO re-research
      (FR-024 cache hit; SC-007 reuse).
  - construct_key: dart.constructor_call.struct_term_with_functor_and_args_list_circular
    source_form: >-
      "StructTerm('f', [VarRef(readerAddr)]);
       StructTerm('f', [VarRef(yReader), VarRef(xReader)]);
       StructTerm('f', [VarRef(xReader)]);
       StructTerm('g', [VarRef(yReader)]);
       StructTerm('f', [ConstTerm('a'), VarRef(xReader)]);
       StructTerm('f', [ConstTerm('a'), ConstTerm('b')]);
       StructTerm('f', [VarRef(yReader)]);
       StructTerm('g', [VarRef(xReader)]);"
    target_decision: >-
      Dart `StructTerm(<functor>, <list-of-Term>)` maps to C# `new
      StructTerm(<functor>, new List<Term> { ... })`. SUT type per
      terms.dart.md construct
      `dart.sum_type_leaf.functor_args_list_reference_identity` (idiom
      rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist):
      `sealed class StructTerm : Term` with `string Functor` and
      `IReadOnlyList<Term> Args`. Codegen passes `new List<Term> { ... }`
      (assignable to `IReadOnlyList<Term>`).
    idiom_id: rf-dart-list-literal-to-csharp-list-of-T
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      Cycle-construction nuance (explicitly addressed, file-specific):
      This file constructs circular terms by EMBEDDING a `VarRef` to
      a reader address inside a StructTerm, then `bindWriter`-ing the
      paired writer to that StructTerm. The cycle lives in the HEAP
      cell graph, NOT in the C# object graph: `StructTerm.Args[0]`
      is a `VarRef` (a plain reference-identity wrapper around a
      `long Addr`) — there is NO direct object-reference cycle. The
      C# mapping therefore needs NO special cycle handling at the
      object-construction layer; cycle-safety is a property of
      `HeapFcp.DerefAddr` (visited-set), `HeapFcp.GetValue`,
      `Term.ToString`, and `SystemPredicates.CopyTermPredicate` (deep-
      copy with visited map) — all pinned in lib/runtime SUT specs.
      Nested-Term-in-args nuance: `StructTerm('f', [ConstTerm('a'),
      VarRef(xReader)])` — a `ConstTerm` and a `VarRef` together —
      `List<Term>` is covariantly composable; no cast needed.
  - construct_key: dart.constructor_call.var_ref_single_int_addr
    source_form: "VarRef(readerAddr), VarRef(yReader), VarRef(xReader), VarRef(copyWriter)"
    target_decision: >-
      `new VarRef(<addr>)`. SUT type per terms.dart.md construct
      `dart.sum_type_leaf.variable_ref_int_address_value_equality`
      (idiom rf-dart-class-eq-on-single-int-field-to-csharp-iequatable)
      is a `sealed class VarRef : Term, IEquatable<VarRef>` with a
      single `long Addr` field. Each address argument is a `long`
      deconstructed earlier; passes directly.
    idiom_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    research_finding_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    nuance: >-
      Cycle-pointer nuance (explicitly addressed, file-specific):
      `VarRef(xReader)` inside `StructTerm('f', [...])` then bound to
      `xWriter` is the literal encoding of `X = f(X?)`. The C#
      mapping preserves the SAME shape: `new VarRef(xReader)` inside
      `new List<Term> { ... }` then bound via `_rt.Heap.BindWriter(
      xWriter, structF);`. Reference-identity is preserved (no
      `record` synthesis) per terms.dart.md.
  - construct_key: dart.constructor_call.system_call_name_and_args_list
    source_form: >-
      "SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)]);
       SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)]);"
    target_decision: >-
      Dart `SystemCall(<name>, <args>)` maps to C# `new
      SystemCall(<name>, <args>)`. SUT type per
      system_predicates.dart.md: `class SystemCall { final string Name;
      final List<object?> Args; ... }` mapped to a reference C# class
      (NOT a record) with positional ctor `public SystemCall(string
      name, IReadOnlyList<object?> args)`. The args list is
      `List<object?>` because `SystemCall.Args` is type-erased over
      mixed Term subtypes — `VarRef` (a `Term`) boxes into `object?`.
    idiom_id: null
    research_finding_id: rf-dart-system-call-construction-to-csharp-new
    nuance: >-
      Type-erasure nuance (explicitly addressed): Dart `List<Object?>`
      (the type of `SystemCall.args` per system_predicates.dart.md)
      maps to C# `IReadOnlyList<object?>` (or `List<object?>` at the
      construction site). The Dart list literal `[VarRef(xReader),
      VarRef(copyWriter)]` is inferred as `List<Term>` in source —
      assignable to `List<Object?>` via Dart's bottom-up inference.
      C# requires explicit boxing: `new List<object?> { new
      VarRef(xReader), new VarRef(copyWriter) }`. Codegen MUST emit
      the `object?` element type at the list literal to match the
      `SystemCall` ctor signature. Reference-identity nuance:
      `SystemCall` is a reference class (per system_predicates.dart.md)
      — `Args` mutation surface (`suspendedReaders` set) is preserved.
  - construct_key: dart.free_function_call.copy_term_predicate
    source_form: "copyTermPredicate(rt, call)"
    target_decision: >-
      Dart top-level free function `copyTermPredicate(rt, call)` maps
      to C# static method call. Per system_predicates_impl.dart.md
      (see line referencing `CopyTermPredicate` in the SUT spec), the
      target is `SystemPredicates.CopyTermPredicate(_rt, call)` —
      hosted on a static container class `SystemPredicates` because
      C# has no top-level free functions outside top-level statements.
      Return type is `SystemResult` (the enum from
      system_predicates.dart.md, mapped to `public enum SystemResult
      { Success, Failure, Suspend }`).
    idiom_id: null
    research_finding_id: rf-dart-top-level-function-to-csharp-static-method-host-class
    nuance: >-
      Top-level-function nuance (explicitly addressed): Dart allows
      free top-level functions; C# requires a hosting class. Codegen
      MUST emit `SystemPredicates.CopyTermPredicate(...)` (PascalCase
      method name on the host class pinned by
      system_predicates_impl.dart.md). The call-site arguments
      `(rt, call)` map to `(_rt, call)` (using the field name from the
      `late`-replacement field decision above). Authoritative source:
      C# language spec on top-level statements
      (`https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements`)
      — top-level free functions are NOT supported outside the entry
      file; library code uses static methods.
  - construct_key: dart.method_call.heap_allocate_variable_returning_record
    source_form: "rt.heap.allocateVariable()"
    target_decision: >-
      Maps to PascalCase: `_rt.Heap.AllocateVariable()` returning
      `(long writerAddr, long readerAddr)` ValueTuple. Per
      heap_fcp.dart.md construct
      `dart.tuple_return.record_two_int_addresses_allocate_variable`
      (idiom rf-dart-record-return-to-csharp-valuetuple).
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Property-vs-method nuance: Dart `rt.heap` is a public field on
      `GlpRuntime` (per runtime.dart.md construct
      `dart.mutable_state_class.identity_equality.runtime_facade_aggregate`);
      C# mapping is a public property `Heap { get; }` (per
      runtime.dart.md). Codegen MUST emit `_rt.Heap.AllocateVariable()`,
      NOT `_rt.heap.allocateVariable()`.
  - construct_key: dart.method_call.heap_mutator_bind_writer
    source_form: >-
      "rt.heap.bindWriter(writerAddr, circularStruct);
       rt.heap.bindWriter(xWriter, circularStruct);
       rt.heap.bindWriter(yWriter, structY);
       rt.heap.bindWriter(xWriter, circularX);
       rt.heap.bindWriter(yWriter, circularY);
       rt.heap.bindWriter(xWriter, struct);
       rt.heap.bindWriter(yWriter, structY);"
    target_decision: >-
      Dart instance method call maps to PascalCase C# instance method
      call on the converted `HeapFcp` type per heap_fcp.dart.md:
      `_rt.Heap.BindWriter(writerAddr, circularStruct);`. Return-type
      nuance: `bindWriter` returns `List<SuspensionRecord>
      activations` per heap_fcp.dart.md construct
      `dart.bind_writer_family.callback_control_with_in_place_mutation_returning_activation_list`
      — but EVERY call in THIS file IGNORES the return value
      (statement-expression context); codegen MUST NOT introduce a
      capture variable.
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Discard-return-value nuance (explicitly addressed, differs from
      binding_pointer_test which captured `var activations = ...`):
      THIS file uses `BindWriter` purely for side effect; no test
      asserts on activations. Codegen MUST emit
      `_rt.Heap.BindWriter(...);` as a statement (no `_ = ...`
      assignment unless a linter rule like CA1806 mandates explicit
      discard). The `List<SuspensionRecord>` return is created and
      garbage-collected.
  - construct_key: dart.method_call.heap_query_get_value_deref_isfullybound
    source_form: >-
      "rt.heap.getValue(writerAddr);
       rt.heap.getValue(xWriter);
       rt.heap.getValue(yWriter);
       rt.heap.getValue(copyWriter);
       rt.heap.derefAddr(writerAddr);
       rt.heap.derefAddr(readerAddr);
       rt.heap.derefAddr(xWriter);
       rt.heap.derefAddr(yWriter);
       rt.heap.isFullyBound(yWriter);
       rt.heap.isFullyBound(copyWriter);"
    target_decision: >-
      Map to PascalCase methods on `HeapFcp` per heap_fcp.dart.md:
      `_rt.Heap.GetValue(addr)` returns `Term?` (per construct
      `dart.method.get_value_nullable_term_from_deref`),
      `_rt.Heap.DerefAddr(addr)` returns `Term` (per construct
      `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_violation_and_three_tag_cases`),
      `_rt.Heap.IsFullyBound(addr)` returns `bool` (per construct
      `dart.method.is_fully_bound_via_deref_returning_bool`).
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Cycle-safety nuance (explicitly addressed, load-bearing for
      THIS file): `DerefAddr` and `GetValue` are exercised against
      CIRCULAR heap states (`X = f(X?)`, `X = f(Y?), Y = g(X?)`).
      The visited-set cycle detection in
      `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_violation_and_three_tag_cases`
      is load-bearing — codegen MUST preserve the `HashSet<long>
      visited` parameter and termination semantics from
      heap_fcp.dart.md verbatim. The tests
      "dereference through circular structure terminates" and
      "nested circular references work correctly" are regression
      tests for that cycle-detection logic.
  - construct_key: dart.as_cast.type_assertion_on_term_subtype
    source_form: >-
      "value as StructTerm;
       xValue as StructTerm;
       yValue as StructTerm;
       copyValue as StructTerm;
       copyStruct.args[0] as ConstTerm;
       resultX as StructTerm;
       resultY as StructTerm;"
    target_decision: >-
      Dart `(<expr> as <T>).<member>` (unchecked downcast) maps to C#
      `((<T>)<expr>).<Member>`. Dart `as` throws `TypeError` on
      mismatch; C# `(T)expr` throws `InvalidCastException`. Both are
      "expected success" in every test in this file — codegen MUST
      emit the explicit cast (NOT `expr as T`, which yields `null` on
      mismatch — wrong semantics).
    idiom_id: rf-dart-as-cast-to-csharp-explicit-cast
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Member-casing nuance (explicitly addressed): every downcasted
      member is renamed PascalCase (`functor` -> `Functor`, `args`
      -> `Args`, `value` -> `Value`, `length` -> `Count`). The Dart
      idiom `final struct = value as StructTerm;` followed by
      `struct.functor` becomes `var structTerm = (StructTerm)value;`
      followed by `structTerm.Functor;` — see the keyword-collision
      rename in the local_var construct above.
  - construct_key: dart.member_access.list_length_property
    source_form: "struct.args.length, copyStruct.args.length"
    target_decision: >-
      Dart `List.length` -> C# `IReadOnlyList<T>.Count`. Per
      terms.dart.md construct
      `dart.sum_type_leaf.functor_args_list_reference_identity`,
      `Args` is `IReadOnlyList<Term>`. Two uses in this file
      (`struct.args.length` once, `copyStruct.args.length` twice).
    idiom_id: rf-dart-list-length-to-csharp-list-count
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Property-naming nuance: Dart `.length` is universal across
      List/String/Iterable; C# splits into `Count` (collections) vs
      `Length` (arrays/strings). For `Args` (a list) the correct
      mapping is `Count`.
  - construct_key: dart.member_access.struct_term_args_indexer
    source_form: "struct.args[0], copyStruct.args[0]"
    target_decision: >-
      Dart `IReadOnlyList<Term>` indexer access -> C# `Args[0]`
      (identical syntax). Per terms.dart.md, `Args` is
      `IReadOnlyList<Term>` which supports `[int]` indexing
      (read-only) in C#.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Width nuance: Dart `[0]` is `int`; C# `IReadOnlyList<T>` indexer
      is `int`-typed (NOT `long`) — index width is `int` in C#
      regardless of element type. No conversion needed; the literal
      `0` is `int` in both languages.
  - construct_key: dart.method_call.object_to_string
    source_form: "value.toString()"
    target_decision: >-
      Dart `<expr>.toString()` maps to C# `<expr>.ToString()`. Used
      ONCE in this file inside the `returnsNormally` lambda
      (`() => value.toString()`). The SUT type `Term` (and its leaves
      ConstTerm/StructTerm/VarRef/Pointer) MUST override `ToString()`
      per terms.dart.md (the cycle-safe formatter with visited-set
      protection is load-bearing — see the test
      "circular term does not cause infinite loop in toString").
    idiom_id: null
    research_finding_id: rf-dart-tostring-to-csharp-tostring
    nuance: >-
      Cycle-safety nuance (explicitly addressed, load-bearing): Dart
      `toString()` and C# `ToString()` are both reflection-defaultable
      methods; the cycle-safe override MUST be inherited from
      `terms.dart.md`'s `Term` base class (per construct
      `dart.term_base_class.tostring_with_visited_set` if such a
      construct exists in that spec — otherwise codegen MUST emit an
      override on each Term leaf that follows VarRef-targets via the
      heap to detect cycles). Without cycle-safety, calling
      `ToString()` on `f(f(f(...)))` would stack-overflow — exactly
      what this test guards against.
  - construct_key: dart.package_test.expect_isA_T
    source_form: >-
      "expect(value, isA<StructTerm>());
       expect(struct.args[0], isA<VarRef>());
       expect(xValue, isA<StructTerm>());
       expect(yValue, isA<StructTerm>());
       expect(copyValue, isA<StructTerm>());
       expect(copyStruct.args[0], isA<ConstTerm>());
       expect(result, isA<StructTerm>());
       expect(resultReader, isA<StructTerm>());
       expect(resultX, isA<StructTerm>());
       expect(resultY, isA<StructTerm>());"
    target_decision: >-
      Map to xUnit `Assert.IsType<T>(<actual>)`. The `isA<T>()`
      matcher in `package:test` asserts the value IS a T
      (subtype-tolerant in Dart). xUnit `Assert.IsType<T>` asserts
      EXACT type match; `Assert.IsAssignableFrom<T>` asserts subtype.
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-vs-subtype nuance (explicitly addressed, reused
      verbatim from binding_pointer_test): every `isA<T>()` target is
      a SEALED CONCRETE Term leaf (StructTerm, ConstTerm, VarRef) per
      terms.dart.md — none have known subtypes. `Assert.IsType<T>`
      is observably equivalent and strictly tighter.
  - construct_key: dart.package_test.expect_equals
    source_form: >-
      "expect(struct.functor, equals('f'));
       expect(struct.args.length, equals(1));
       expect((xValue as StructTerm).functor, equals((yValue as StructTerm).functor));
       expect(copyStruct.functor, equals('f'));
       expect(copyStruct.args.length, equals(2));
       expect((copyStruct.args[0] as ConstTerm).value, equals('a'));
       expect(result, equals(SystemResult.success));
       expect((resultX as StructTerm).functor, equals('f'));
       expect((resultY as StructTerm).functor, equals('g'));"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)` — ARGUMENT-
      ORDER FLIP. The `equals` matcher uses Dart `==` equality;
      `Assert.Equal` uses `IEquatable<T>.Equals` / `Object.Equals`,
      equivalent for the value-typed comparisons in this file
      (`string` functor, `int` length, `object?` payload, `SystemResult`
      enum).
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order nuance (explicitly addressed — well-known
      footgun): Dart `expect(actual, equals(expected))` puts actual
      first; xUnit `Assert.Equal(expected, actual)` puts expected
      first. Enum-equality nuance: `expect(result, equals(SystemResult
      .success))` -> `Assert.Equal(SystemResult.Success, result)`.
      Per system_predicates.dart.md the enum member casing maps
      lowercase→PascalCase (`success` -> `Success`). Cross-cast
      equality nuance: `equals((yValue as StructTerm).functor)`
      becomes `Assert.Equal(((StructTerm)yValue).Functor,
      ((StructTerm)xValue).Functor);` — the expected argument is
      itself a downcast expression.
  - construct_key: dart.package_test.expect_isNot_equals
    source_form: "expect(xValue.functor, isNot(equals(yValue.functor)));"
    target_decision: >-
      Map to xUnit `Assert.NotEqual(<expected>, <actual>)`. The
      `isNot(equals(...))` composite is the negated equality matcher.
      Used ONCE in this file ("different circular terms are not
      equal").
    idiom_id: null
    research_finding_id: rf-dart-expect-isNot-equals-to-xunit-assert-notequal
    nuance: >-
      Argument-order nuance (explicitly addressed): same flip as
      `Assert.Equal`. `xValue` and `yValue` are typed `StructTerm` by
      a prior `as`-cast assignment, so `.functor` is a plain string;
      mapping is `Assert.NotEqual(yValue.Functor, xValue.Functor);`.
      Reused finding-id from varref_pointer_test.dart.md.
  - construct_key: dart.package_test.expect_isFalse_isTrue
    source_form: >-
      "expect(rt.heap.isFullyBound(yWriter), isFalse);
       expect(rt.heap.isFullyBound(copyWriter), isTrue);"
    target_decision: >-
      Map `isTrue` to `Assert.True(<bool-expr>)` and `isFalse` to
      `Assert.False(<bool-expr>)`. Per
      `rf-dart-expect-istrue-to-xunit-asserttrue` precedent.
    idiom_id: rf-dart-expect-istrue-to-xunit-asserttrue
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Diagnostic nuance: bare `Assert.True/False` without a message
      produces a generic failure — adequate for the predicates here.
  - construct_key: dart.package_test.expect_function_returnsNormally
    source_form: "expect(() => value.toString(), returnsNormally);"
    target_decision: >-
      Dart `expect(<fn>, returnsNormally)` (a positive matcher
      asserting the function completes without throwing) maps to a
      BARE CALL in xUnit: `value.ToString();` as a statement (any
      uncaught exception fails the test). Per
      `partial_evaluator_test.dart.md` precedent (idiom
      rf-dart-expect-returns-normally-to-xunit-bare-call).
    idiom_id: null
    research_finding_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    nuance: >-
      Positive-matcher nuance (explicitly addressed, reused from
      partial_evaluator_test): `returnsNormally` is a constant matcher
      from `package:matcher`
      (`https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`)
      that returns success iff the function completes without
      throwing. xUnit has NO direct equivalent because any uncaught
      exception in a `[Fact]` method already fails the test — emit
      the bare call. Codegen MUST emit `value.ToString();` (statement
      form, NOT `_ = value.ToString();`) — the side-effect
      semantics (does-not-throw) is the assertion.
      Result-discard alternative: `_ = value.ToString();` would
      explicitly discard the return; not required because
      `ToString()` return is implicitly discarded in statement
      position.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System + the SUT runtime namespace)
  - cu-2: namespace declaration mirroring the test/heap path (e.g. `<RootNs>.Test.Heap`)
  - cu-3: top-level test class `CircularTermPointerTests` with file-level + outer-group XML doc-comments preserved
  - cu-4: constructor `public CircularTermPointerTests() { _rt = new GlpRuntime(); }` and `private readonly GlpRuntime _rt;` field (replaces Dart `late GlpRuntime rt; setUp(() { rt = GlpRuntime(); });`)
  - cu-5: 2 `[Fact]` methods in the "Ground Guard with Circular Terms" group (`CircularTermWithoutUnboundVariablesIsGround`, `CircularTermWithUnboundVariableInsideIsNotGround`), each `[Trait("Group", "Ground Guard with Circular Terms")]`, each with `[Fact(DisplayName = "<original label>")]`
  - cu-6: 2 `[Fact]` methods in the "Equality (=?=) with Circular Terms" group (`IdenticalCircularTermsAreEqual`, `DifferentCircularTermsAreNotEqual`), each `[Trait("Group", "Equality (=?=) with Circular Terms")]`
  - cu-7: 2 `[Fact]` methods in the "Deep Copy with Circular Terms" group (`CopyOfCircularTermPreservesStructure`, `CopyOfAcyclicTermCreatesIndependentCopy`), each `[Trait("Group", "Deep Copy with Circular Terms")]`, each emitting `var call = new SystemCall("copy_term", new List<object?> { new VarRef(...), new VarRef(...) });` followed by `var result = SystemPredicates.CopyTermPredicate(_rt, call);`
  - cu-8: 1 `[Fact]` method in the "Term Formatter with Circular Terms" group (`CircularTermDoesNotCauseInfiniteLoopInToString`), `[Trait("Group", "Term Formatter with Circular Terms")]`, body uses the BARE-CALL shape for `returnsNormally`
  - cu-9: 2 `[Fact]` methods in the "Dereferencing Circular Terms" group (`DereferenceThroughCircularStructureTerminates`, `NestedCircularReferencesWorkCorrectly`), each `[Trait("Group", "Dereferencing Circular Terms")]`
  - cu-10: in every method, the keyword-collision rename `struct -> structTerm` is applied where the Dart local `final struct = value as StructTerm;` appears (one site in this file)
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

Fourth heap-test `package:test` file specced; xUnit is project-pinned.
The authoritative basis is unchanged: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `test` / `expect` /
matcher semantics. Reused via `rf-dart-package-test-import-to-xunit-using`
— no re-research (FR-024 cache hit; SC-007 reuse). This file's nested-
group + `setUp` decision continues the precedent set in
boot_loader_test.dart.md and suspension_pointer_test.dart.md.

### Nested groups + `late`+`setUp` ⇒ flat class + constructor field

This file's structure (1 outer + 5 inner groups + `late GlpRuntime rt`
hoisted via `setUp`) is the OPPOSITE shape of binding_pointer_test
(6 sibling groups + per-test inline `final heap = HeapFCP()`). The
conversion strategy is the same FLATTEN-with-`[Trait]` mapping plus a
constructor-and-field for the `setUp` per-test runtime. xUnit's per-test
class instantiation is documented at
`https://xunit.net/docs/comparisons#setup` — the constructor runs ONCE
per `[Fact]`, exactly matching Dart's `setUp` contract. `readonly` is
chosen for `_rt` because no test reassigns it.

### Circular-term construction at the heap layer, not the object layer

The Dart idiom `X = f(X?)` is encoded as `StructTerm('f',
[VarRef(xReader)])` then `heap.bindWriter(xWriter, struct)`. The cycle
lives in the HEAP CELL GRAPH (writer cell → struct → VarRef →
reader cell → writer cell), NOT in the C# object graph (StructTerm
holds a VarRef which holds a `long Addr`, not a back-pointer). The C#
mapping therefore needs NO special cycle handling at the object-
construction layer; ALL cycle-safety is a property of the SUT methods
`HeapFcp.DerefAddr` (visited-set parameter per heap_fcp.dart.md
construct `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_violation_and_three_tag_cases`),
`HeapFcp.GetValue`, `Term.ToString`, and
`SystemPredicates.CopyTermPredicate` (deep-copy with visited map per
system_predicates_impl.dart.md). The convspec records the test's
dependency on these SUT cycle-safety guarantees; if any SUT spec ever
drops the visited-set, this test must escalate via the SUT spec.

### `SystemCall` construction with type-erased args

`SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)])` maps to
`new SystemCall("copy_term", new List<object?> { new VarRef(xReader),
new VarRef(copyWriter) });`. Per system_predicates.dart.md, `SystemCall
.args` is `List<Object?>` (type-erased) because the args carry mixed
Term subtypes plus possibly non-Term metadata. C# requires explicit
`object?` element typing on the list literal (Dart's bottom-up
inference from `List<Term>` to `List<Object?>` does not auto-apply in
C#). Reference-identity is preserved (SystemCall is a plain class, NOT
a record) per system_predicates.dart.md.

### `copyTermPredicate` ⇒ static method on `SystemPredicates` host class

Dart top-level free function `copyTermPredicate(rt, call)` has no
direct C# equivalent — C# requires a hosting class. Per
system_predicates_impl.dart.md (which spells the target
`SystemPredicates.CopyTermPredicate(GlpRuntime rt, SystemCall call)`),
codegen emits `SystemPredicates.CopyTermPredicate(_rt, call)`.
Authoritative source: C# language spec on top-level statements
(`https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements`)
— top-level free functions are NOT supported outside the entry file;
library code uses static methods on container classes.

### `returnsNormally` ⇒ bare call

Reused verbatim from partial_evaluator_test.dart.md (idiom
rf-dart-expect-returns-normally-to-xunit-bare-call). Dart matcher docs:
`https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`.
xUnit has no direct equivalent because uncaught exceptions in `[Fact]`
methods automatically fail the test — a bare call is the canonical
positive-matcher mapping. The single use here
(`expect(() => value.toString(), returnsNormally);` ⇒
`value.ToString();`) regression-tests cycle-safe `ToString` on
`f(f(f(...)))`.

### `struct` keyword collision

The Dart local `final struct = value as StructTerm;` collides with the
C# `struct` keyword. Two options: (A) verbatim `@struct` (legal but
discouraged); (B) rename to `structTerm` / `castStruct` (chosen —
matches the C# convention of avoiding `@`-prefixed identifiers where a
descriptive rename is available). Recorded as a file-local rename in
cu-10. Authoritative source: C# language reference on identifiers and
contextual keywords
(`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim`).

### `as`-cast and member-casing

Standard mapping (rf-dart-as-cast-to-csharp-explicit-cast): Dart `as`
throws on mismatch and C# `(T)x` ALSO throws (InvalidCastException);
Dart `x as T?` returning null on mismatch is NOT the same as C#
`x as T` — codegen MUST use the explicit cast `(T)x`. Every downcasted
member is renamed PascalCase per the runtime SUT specs.

### `Assert.Equal` argument-order flip

~9 `equals(...)` calls in this file — every one MUST be flipped at the
boundary. xUnit docs (`https://xunit.net/docs/comparisons#assertions`)
document the order explicitly. The enum-equality call
`equals(SystemResult.success)` ⇒ `Assert.Equal(SystemResult.Success,
result)` is the canonical pattern (note PascalCase enum member per
system_predicates.dart.md).

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official Dart / .NET documentation, and EVERY non-trivial construct
reuses an idiom_id-equivalent rf-* already recorded in a precedent
spec (`heap_fcp.dart.md`, `cells.dart.md`, `terms.dart.md`,
`suspension.dart.md`, `runtime.dart.md`, `system_predicates.dart.md`,
`system_predicates_impl.dart.md`, `binding_pointer_test.dart.md`,
`varref_pointer_test.dart.md`, `suspension_pointer_test.dart.md`,
`boot_loader_test.dart.md`, `partial_evaluator_test.dart.md`). The
file-local choices (keyword-rename `struct`→`structTerm`, bare-call
mapping for `returnsNormally`, type-erased `object?` list literal for
`SystemCall.args`, FLATTEN nested groups to `[Trait]`-tagged methods,
`readonly` field initialised in constructor for `late`+`setUp`) are
deliberate, in-file-justified choices with authoritative documentation
references. `escalations: []` is therefore intentional.
