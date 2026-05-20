> Conversion-spec artifact for test/circular_term_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/circular_term_test.dart
source_sha256: 13325b134ab40b28f0b298af90405dcdd2f608c084cecd20446828be4f7b8db2
target_code_unit: test/CircularTermTest.cs
constructs:
  - construct_key: dart.file_level_doc_comment.multi_paragraph_top_of_file_no_library_directive
    source_form: >-
      "/// Tests for circular term handling in GLP runtime.
       ///
       /// Circular terms can form through cross-goal communication when two goals
       /// share variables and bind them in ways that create cycles. These tests
       /// verify that the runtime handles such terms gracefully:
       /// - ground/1 guard terminates and correctly identifies ground circular terms
       /// - =?= equality terminates and correctly compares circular terms
       /// - copy_term/2 preserves cyclic structure in copies"
    target_decision: >-
      No `library;` directive present; the file-level `///` doc-comment
      block at the top maps to the XML `<summary>` doc-comment on the
      enclosing xUnit test class `CircularTermTests`. Each `///` line
      preserved verbatim; the bullet list (three dashes) reflowed as
      `<list type="bullet"><item>...</item></list>` so Visual Studio /
      Rider render it identically to dartdoc.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Same nuance as every `package:test` file in this inventory: Dart
      library scope = the file; C# scope = `namespace`. No `library`
      name to carry; the file-level doc-comment is the ONLY surviving
      piece of file-level Dart metadata and lands as the test-class
      XML doc — so the human-readable rationale ("Tests for circular
      term handling in GLP runtime", the three bullet points naming
      ground/1, =?=, copy_term/2) survives the conversion verbatim.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. Project-wide pinning is
      xUnit (precedent: test/heap/binding_pointer_test.dart.md,
      test/multiagent/boot_loader_test.dart.md, test/multiagent/
      mad_error_handling_test.dart.md, test/smoke_test.dart.md).
      Codegen MUST also add `using System;` (needed only if a future
      revision of this file maps a `throwsStateError` to
      `InvalidOperationException`; none in current file) — for the
      current shape only `using Xunit;` plus the runtime `using` are
      strictly required.
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a PROJECT-WIDE policy nuance, NOT a
      file-local choice: every `package:test` file in this inventory
      MUST map to the SAME .NET framework (xUnit) so test discovery,
      runner config, and attribute vocabulary stay consistent.
      Reused verbatim — NO re-research (FR-024 cache hit).
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/system_predicates.dart';
       import 'package:glp_runtime/runtime/system_predicates_impl.dart';"
    target_decision: >-
      Four `package:glp_runtime/runtime/...` imports collapse to ONE
      `using <RootNs>.Runtime;` — the converted runtime sub-namespace
      decided by the four SUT specs lib/runtime/runtime.dart.md,
      lib/runtime/terms.dart.md, lib/runtime/system_predicates.dart.md,
      lib/runtime/system_predicates_impl.dart.md. C# `using` is per-
      namespace, not per-file — multiple Dart imports into the SAME
      C# namespace collapse. Static-class import nuance: per
      system_predicates_impl.dart.md, the predicate functions are
      `public static` methods on a containing class (e.g.
      `SystemPredicatesImpl`), so the bare callsite
      `copyTermPredicate(rt, call)` becomes
      `SystemPredicatesImpl.CopyTermPredicate(rt, call)` UNLESS
      codegen adds a `using static <RootNs>.Runtime.
      SystemPredicatesImpl;` which permits the unqualified callsite.
      EITHER is acceptable; the SUT spec pinned the static class but
      did not pin the test-side `using static` choice — left to the
      file-scope codegen pass. This artifact records the dependency
      shape; the namespace string is owned by the four SUT specs.
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: in Dart each `package:` URI is a
      separate import; in C# all four converted files share the
      runtime namespace, so a SINGLE `using` suffices. Top-level Dart
      function `copyTermPredicate` was lifted into a static class in
      C# per system_predicates_impl.dart.md construct
      `dart.toplevel_function.predicate_handler_two_arg_returning_
      systemresult` (idiom rf-dart-toplevel-function-to-csharp-static-
      method-on-class). Test code MUST either fully-qualify the
      callsite or emit `using static <RootNs>.Runtime.
      SystemPredicatesImpl;` so the bare `CopyTermPredicate(...)`
      callsite compiles. Reused verbatim from prior `package:glp_
      runtime/runtime/...` test specs — no re-research.
  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
         group('Circular Term Handling', () {
           late GlpRuntime rt;
           setUp(() { rt = GlpRuntime(); });
           group('Ground Guard with Circular Terms', () { test(...); test(...); });
           group('Equality (=?=) with Circular Terms', () { test(...); test(...); });
           group('Deep Copy with Circular Terms', () { test(...); test(...); });
           group('Term Formatter with Circular Terms', () { test(...); });
         });
       }"
    target_decision: >-
      Eliminate `main` entirely; xUnit discovers `[Fact]` methods by
      reflection — there is NO per-file entrypoint to emit. The
      single outer `group('Circular Term Handling', ...)` body
      collapses into the enclosing test class `CircularTermTests`;
      the four inner groups become `[Trait("Group", "<inner label>")]`
      partitions on the test methods (see `dart.package_test.group_
      block_nested_with_setUp` below).
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook — only per-class
      (constructor + `IDisposable.Dispose`) and per-collection
      fixtures. THIS file's `main` body is one outer group containing
      a `late` field + `setUp` + four inner groups; the omission of
      `main` is lossless because every statement inside collapses
      into the class shape below. Reused verbatim from
      binding_pointer_test convspec — no re-research.
  - construct_key: dart.package_test.group_block_nested_with_setUp
    source_form: >-
      "group('Circular Term Handling', () {
         late GlpRuntime rt;
         setUp(() { rt = GlpRuntime(); });
         group('Ground Guard with Circular Terms', () { test(...); test(...); });
         group('Equality (=?=) with Circular Terms', () { test(...); test(...); });
         group('Deep Copy with Circular Terms', () { test(...); test(...); });
         group('Term Formatter with Circular Terms', () { test(...); });
       });"
    target_decision: >-
      ONE outer group containing a `late` field + `setUp` + FOUR
      sibling inner groups (no nesting deeper than two levels). Map
      to a SINGLE xUnit test class `CircularTermTests` (named after
      the outer group label, sanitised PascalCase + `Tests` suffix).
      The `late GlpRuntime rt;` field is hoisted to a private
      instance field; the outer `setUp(() { rt = GlpRuntime(); })`
      closure becomes the class constructor body. Each inner group's
      label survives as `[Trait("Group", "<inner label>")]` on every
      test method belonging to that group. Per-test method names are
      group-prefixed PascalCased identifier-safe forms of the test
      label so collisions across groups are impossible. The original
      Dart test label MUST be preserved verbatim via
      `[Fact(DisplayName = "<original label>")]`. This is the SAME
      "flatten with traits" pattern pinned by boot_loader_test.dart.md
      and binding_pointer_test.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-with-setUp nuance (explicitly addressed — matches the
      boot_loader_test shape, not the binding_pointer_test shape):
      this file's structure is 1 outer + 4 inner siblings + a single
      shared `late GlpRuntime rt` driven by `setUp`. Codegen MUST
      hoist `rt` to a constructor-assigned field (NOT a method-local
      `var`) because all seven tests across all four inner groups
      depend on it. Label-mangling nuance: inner-group labels contain
      operator-like substrings ("=?=", "(", ")") and ordinary spaces;
      `DisplayName` preserves verbatim, method names sanitise to
      identifier-safe PascalCase (e.g. `EqualityWithCircularTerms_
      IdenticalCircularTermsAreEqual`). Reused verbatim from
      boot_loader_test convspec — no re-research.
  - construct_key: dart.late_field.glpruntime_per_test_runtime_arena
    source_form: "late GlpRuntime rt;"
    target_decision: >-
      Dart `late GlpRuntime rt;` declared inside the outer group's
      callback (lexically closed over by `setUp` + every test) maps
      to a `private GlpRuntime _rt = null!;` instance field on
      `CircularTermTests`. The `null!` initialiser asserts to the
      compiler that the constructor (the setUp mapping — see next
      construct) has already assigned a non-null value before any
      test method runs. PascalCase to camelCase nuance: Dart field
      `rt` becomes private C# field `_rt` (underscore-prefixed,
      lowerCamelCase per .NET Framework Design Guidelines for private
      fields).
    idiom_id: null
    research_finding_id: rf-dart-late-field-to-csharp-null-bang-field
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart `late T x;` is
      a runtime-checked "promise to assign before first read"; C#
      NRT `T x = null!;` is a static SUPPRESSION of the null-warning
      without runtime check. The behaviour is equivalent ONLY when
      the constructor unconditionally assigns — which it does (the
      `setUp` mapping below is the constructor body, and assignment
      is unconditional). Recorded as a known divergence — if a future
      revision moves `setUp` to conditional assignment, codegen MUST
      switch to nullable `private GlpRuntime? _rt;` + null-forgiving
      reads at every access site, NOT the `null!` initialiser.
      Reused verbatim from boot_loader_test convspec — no re-
      research.
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { rt = GlpRuntime(); });"
    target_decision: >-
      Dart `setUp` registered inside the outer group maps to the xUnit
      class CONSTRUCTOR. xUnit instantiates the test class ONCE PER
      TEST METHOD (`https://xunit.net/docs/shared-context#constructor`)
      so a constructor body is semantically equivalent to per-test
      `setUp`. The closure body `rt = GlpRuntime();` becomes
      `_rt = new GlpRuntime();` in the constructor.
    idiom_id: null
    research_finding_id: rf-dart-package-test-setUp-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (explicitly addressed): `package:test`'s
      `setUp` runs BEFORE EACH test; xUnit's per-test instantiation
      runs the constructor BEFORE EACH `[Fact]`. The mappings are
      observationally identical — fresh `_rt` per test, no cross-test
      state leak. Async-setUp nuance NOT exercised here (the `setUp`
      callback is synchronous and the body is one statement).
      Default-ctor-call nuance: `GlpRuntime()` invokes the named-
      optional constructor with ALL parameters defaulted — per
      lib/runtime/runtime.dart.md construct `dart.constructor.named_
      optional_with_null_coalesce_defaults_static_factory_seed`, the
      C# constructor is `public GlpRuntime(HeapFCP? heap = null,
      GoalQueue? gq = null, SystemPredicateRegistry? systemPredicates
      = null, BodyKernelRegistry? bodyKernels = null)`, so the
      Dart callsite `GlpRuntime()` maps to C# `new GlpRuntime()`
      with ALL defaults — the runtime then seeds heap/gq/registries
      internally per the SUT spec.
  - construct_key: dart.package_test.test_call_simple
    source_form: >-
      "test('<label>', () { /* arrange (heap.allocateVariable,
        StructTerm/ConstTerm/VarRef construction), act (heap.bindVariable,
        copyTermPredicate, value.toString), assert (expect ...) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance
      method on `CircularTermTests`, decorated with
      `[Fact(DisplayName = "<original label>")]` plus
      `[Trait("Group", "<enclosing inner-group label>")]`. The method
      name is the inner-group-prefixed PascalCased label (see
      group_block_nested_with_setUp). The closure body converts
      statement-for-statement into the method body (arrange =
      record-deconstruction of `_rt.Heap.AllocateVariable()`, act =
      heap mutation + predicate invocation, assert = `expect(...)`
      translations below). All 7 `test` calls in this file are
      synchronous (no `async`/`Future`) so no target method is
      `async Task`. NO per-method `var heap = new HeapFcp();` arena
      — the test class shares `_rt` and reaches the heap via `_rt.
      Heap`.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Shared-state nuance (explicitly addressed, matches boot_loader_
      test shape NOT binding_pointer_test shape): unlike
      binding_pointer_test where every test allocated its OWN `final
      heap = HeapFCP()`, this file shares a single `late GlpRuntime
      rt` field. Codegen MUST NOT emit `var rt = new GlpRuntime();`
      inside method bodies; the runtime arena is the constructor-
      assigned `_rt` field. Async nuance recorded but not exercised
      in this file (zero `async`/`Future` in the closures).
  - construct_key: dart.local_var.final_constructor_instance
    source_form: >-
      "final circularStruct = StructTerm('f', [VarRef(varReader)]);
       final value = rt.heap.getValue(varWriter);
       final struct = value as StructTerm;
       final xValue = rt.heap.getValue(xWriter);
       final yValue = rt.heap.getValue(yWriter);
       final call = SystemCall('copy_term', [...]);
       final result = copyTermPredicate(rt, call);
       final copyValue = rt.heap.getValue(copyWriter);
       final copyStruct = copyValue as StructTerm;
       final struct = StructTerm('f', [ConstTerm('a'), ConstTerm('b')]);"
    target_decision: >-
      Dart `final <T> x = <expr>;` with type inferred from the RHS
      maps to C# `var x = <expr>;` for method-local lifetimes.
      `final` is assignment-once; `var` produces a non-readonly local
      but the single-assignment shape is preserved by the converted
      body (no reassignment anywhere in this file). Per-method
      local; never an instance field.
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Reassignment nuance: Dart `final` is enforced; C# `var` is not.
      Codegen MUST NOT introduce reassignment, but does NOT need to
      emit `readonly` (illegal on locals in C#). For every
      single-assignment local in this file the mapping is uniform.
      Reused verbatim from binding_pointer_test convspec — no re-
      research.
  - construct_key: dart.local_var.record_destructuring_two_ints_or_ignored
    source_form: >-
      "final (varWriter, varReader) = rt.heap.allocateVariable();
       final (xWriter, xReader) = rt.heap.allocateVariable();
       final (yWriter, yReader) = rt.heap.allocateVariable();
       final (copyWriter, _) = rt.heap.allocateVariable();"
    target_decision: >-
      Dart record-positional-destructuring of `(int, int)` returned
      by `_rt.Heap.AllocateVariable()` maps to C# tuple-
      deconstruction: `var (varWriter, varReader) = _rt.Heap.
      AllocateVariable();`, `var (xWriter, xReader) = ...;`,
      `var (yWriter, yReader) = ...;`, `var (copyWriter, _) = ...;`.
      C# 7+ supports `_` as a discard in deconstruction. The SUT
      method's return type per heap_fcp.dart.md construct `dart.
      tuple_return.record_two_int_addresses_allocate_variable` is
      `(long writerAddr, long readerAddr)`, so every deconstructed
      local in this file is `long` — NOT `int`.
    idiom_id: null
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      Discard-vs-bind nuance (explicitly addressed): three sites
      `(varWriter, varReader)`, `(xWriter, xReader)`, `(yWriter,
      yReader)`, plus one discard `(copyWriter, _)` — symmetric to
      binding_pointer_test. Int-width nuance: Dart `int` maps to C#
      `long` to preserve address-arithmetic width (per cells.dart.md
      construct `dart.int.fixed_width_identity_field`, idiom rf-dart-
      int-to-csharp-long-width). Codegen MUST keep both deconstructed
      names typed as `long`, not `int`. Reused verbatim from
      binding_pointer_test convspec — no re-research.
  - construct_key: dart.constructor_call.const_term_with_value
    source_form: "ConstTerm('a'), ConstTerm('b')"
    target_decision: >-
      Dart positional `ConstTerm(<lit>)` maps to C# `new
      ConstTerm(<lit>)`. The SUT type per terms.dart.md construct
      `dart.sum_type_leaf.value_carrying_no_eq_override_reference_
      identity` is a `sealed class ConstTerm : Term` with a single
      nullable `object? Value` field; string literals box
      transparently into `object?`. Two uses in this file (both
      `ConstTerm('a')` and `ConstTerm('b')` inside the "copy of
      acyclic term creates independent copy" test and one
      `ConstTerm('a')` inside the "copy of circular term preserves
      structure" test).
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Literal-typing nuance: Dart `'a'`/`'b'` are `String`; box to
      C# `object?`. Reference-identity nuance for the wrapper class
      itself comes from rf-dart-sumleaf-no-eq-to-csharp-class-no-
      record (no `record` synthesis — preserve reference identity).
      Reused verbatim from binding_pointer_test convspec — no re-
      research.
  - construct_key: dart.constructor_call.struct_term_with_functor_and_args_list
    source_form: >-
      "StructTerm('f', [VarRef(varReader)]);
       StructTerm('f', [VarRef(yReader), VarRef(xReader)]);
       StructTerm('f', [VarRef(xReader)]);
       StructTerm('g', [VarRef(yReader)]);
       StructTerm('f', [ConstTerm('a'), VarRef(xReader)]);
       StructTerm('f', [ConstTerm('a'), ConstTerm('b')]);"
    target_decision: >-
      Dart `StructTerm(<functor>, <list-of-Term>)` maps to C# `new
      StructTerm(<functor>, new List<Term> { ... })`. Per terms.dart.md
      construct `dart.sum_type_leaf.functor_args_list_reference_
      identity`, the SUT type is `sealed class StructTerm : Term` with
      `string Functor` and `IReadOnlyList<Term> Args`. Codegen passes
      `new List<Term> { ... }` (assignable to `IReadOnlyList<Term>`).
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      List-literal nuance (explicitly addressed): Dart `[a, b]` of
      type `List<Term>` maps to C# `new List<Term> { a, b }`. Single-
      element lists `[VarRef(...)]` map to `new List<Term> { new
      VarRef(...) }`. Cyclic-reference nuance (explicitly addressed
      — load-bearing for THIS file): every `StructTerm('f', [VarRef(
      <reader>)])` is part of a CYCLE created later by `_rt.Heap.
      BindVariable(<writer>, <struct>)`. C# `List<Term>` and `VarRef`
      are BOTH reference types, so the `Args` list of the bound
      struct will (after binding) hold a `VarRef` whose `Addr`
      points back to the same writer's reader address — a heap-level
      cycle, NOT an object-graph cycle (no Term holds a direct
      reference to itself; the cycle goes through the heap's cell
      table). This means C# garbage collection is NOT at risk: the
      cycle is in DATA, not in object references. Reused verbatim
      from binding_pointer_test convspec — no re-research.
  - construct_key: dart.constructor_call.var_ref_single_int_addr
    source_form: >-
      "VarRef(varReader), VarRef(yReader), VarRef(xReader),
       VarRef(yReader), VarRef(xReader), VarRef(copyWriter)"
    target_decision: >-
      `new VarRef(<reader_or_writer_addr>)`. SUT type per terms.dart.md
      construct `dart.sum_type_leaf.variable_ref_int_address_value_
      equality` is a `sealed class VarRef : Term, IEquatable<VarRef>`
      with a single `long Addr` field. Each argument is a `long`
      deconstructed earlier; passes directly.
    idiom_id: null
    research_finding_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    nuance: >-
      Reader-vs-writer-address nuance (explicitly addressed): six of
      the seven `VarRef(...)` callsites in this file pass a READER
      address (e.g. `varReader`, `xReader`, `yReader`), but the
      `SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)])`
      construction passes a WRITER address (`copyWriter`) in the
      second slot — this is intentional per the `copy_term` system-
      predicate contract (first arg = source READER, second arg =
      destination WRITER). The C# `VarRef` type is address-agnostic
      (single `long Addr`); the distinction is enforced by the heap,
      not by the type. Reused verbatim from binding_pointer_test
      convspec — no re-research.
  - construct_key: dart.constructor_call.system_call_name_and_args_list
    source_form: >-
      "SystemCall('copy_term', [
         VarRef(xReader),  // Original (reader)
         VarRef(copyWriter),  // Copy (writer)
       ]);
       SystemCall('copy_term', [
         VarRef(xReader),  // Reader
         VarRef(copyWriter),  // Writer
       ]);"
    target_decision: >-
      Dart `SystemCall(<name>, <args-list>)` maps to C# `new
      SystemCall(<name>, new List<object?> { ... })`. Per
      system_predicates.dart.md construct
      `dart.mutable_callcontext_class.final_string_final_list_object_
      questionmark_final_set_inline_init_positional_ctor` (idiom rf-
      dart-mutable-callcontext-class-to-csharp-reference-class), the
      SUT type is `public sealed class SystemCall` with positional
      constructor `public SystemCall(string name, IReadOnlyList<
      object?> args)`. The arg-list element type `Term` (in this
      file, all elements are `VarRef`) widens to `object?` at the
      callsite — C# array/list-initialiser covariance handles this
      automatically (`new List<object?> { new VarRef(xReader), new
      VarRef(copyWriter) }` compiles because `VarRef` is a reference
      type assignable to `object?`).
    idiom_id: null
    research_finding_id: rf-dart-mutable-callcontext-class-to-csharp-reference-class
    nuance: >-
      Reference-type nuance (explicitly addressed): `SystemCall` MUST
      be a reference type (not `record`, not `struct`) per
      system_predicates.dart.md — the predicate mutates
      `SuspendedReaders` on the SAME instance the caller constructed.
      For THIS file the mutation surface is unused (`copyTermPredicate`
      doesn't suspend); still, the type contract is unchanged.
      List-element-type nuance: the Dart args list `[VarRef(...),
      VarRef(...)]` has STATIC type `List<VarRef>` but is passed to
      `List<Object?> args` — Dart implicit upcast through the
      constructor; the C# equivalent is `new List<object?> { new
      VarRef(...), new VarRef(...) }` (List<T> is invariant in C#,
      so `new List<VarRef>` does NOT implicitly convert to
      `IReadOnlyList<object?>` — codegen MUST construct the
      `List<object?>` directly). Reused decision from system_
      predicates.dart.md.
  - construct_key: dart.method_call.heap_allocateVariable
    source_form: "rt.heap.allocateVariable()"
    target_decision: >-
      `_rt.Heap.AllocateVariable()` returning `(long writerAddr, long
      readerAddr)` per heap_fcp.dart.md construct `dart.tuple_return.
      record_two_int_addresses_allocate_variable` (mapped to
      `ValueTuple<long,long>` with named elements). Field-access
      chain `rt.heap` -> `_rt.Heap` (PascalCase). Used four times.
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Field-access-chain nuance (explicitly addressed): Dart
      `rt.heap.allocateVariable()` is a two-step access — read the
      `heap` field on the `GlpRuntime` instance, then call the
      `allocateVariable` method. In C# this becomes `_rt.Heap.
      AllocateVariable()` — the `Heap` PROPERTY (per runtime.dart.md
      construct `dart.mutable_state_class.identity_equality.runtime_
      facade_aggregate`, where `final HeapFCP heap` maps to a
      get-only auto-property `public HeapFCP Heap { get; }`). No
      method extraction; the chain is a property read + method call.
      Reused verbatim — no re-research.
  - construct_key: dart.method_call.heap_bindVariable_mutator
    source_form: >-
      "rt.heap.bindVariable(varWriter, circularStruct);
       rt.heap.bindVariable(xWriter, circularStruct);
       rt.heap.bindVariable(xWriter, circularX);
       rt.heap.bindVariable(yWriter, circularY);
       rt.heap.bindVariable(xWriter, circularStruct);
       rt.heap.bindVariable(xWriter, circularStruct);"
    target_decision: >-
      Dart instance method call `rt.heap.bindVariable(<writerAddr>,
      <term>)` maps to PascalCase C# instance method call
      `_rt.Heap.BindVariable(<writerAddr>, <term>)` on the converted
      `HeapFcp` type. Return value (List<SuspensionRecord> per
      heap_fcp.dart.md construct `dart.bind_writer_family.callback_
      control_with_in_place_mutation_returning_activation_list`) is
      DISCARDED at every callsite in this file — the tests don't
      capture or assert on activations. Codegen MAY emit a `_ =
      _rt.Heap.BindVariable(...);` discard, or leave the call as an
      expression statement (C# allows discarding a method return
      value implicitly).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Method-name-divergence nuance (explicitly addressed — load-
      bearing for this file): this file calls `bindVariable` (the
      legacy name) at every site, NOT `bindWriter` (the modern name
      pinned by heap_fcp.dart.md). The SUT spec heap_fcp.dart.md
      construct `dart.method.bind_writer_explicit_set_or_throws_if_
      bound` decided the C# name `BindWriter`. Two valid resolutions:
      (a) the C# heap exposes BOTH `BindVariable` and `BindWriter`
      as a public surface (likely if `bindVariable` is a legacy
      alias still present on the Dart side); (b) `bindVariable` is
      renamed to `BindWriter` in the test conversion to match the
      SUT spec. Resolution: codegen MUST emit `BindVariable` to
      match the EXACT Dart method name used in the source, and the
      SUT spec heap_fcp.dart.md MUST be checked to confirm
      `BindVariable` is still on the public surface (it IS, per
      that spec's exposed-method list — `bindVariable` is the public
      legacy entry point that internally calls `bindWriter`).
      No conflict; both names coexist as part of the public surface.
      Reused verbatim from binding_pointer_test convspec — no re-
      research.
  - construct_key: dart.method_call.heap_isWriterBound_query
    source_form: "rt.heap.isWriterBound(yWriter)"
    target_decision: >-
      `_rt.Heap.IsWriterBound(yWriter)` returning `bool`. SUT method
      per heap_fcp.dart.md construct `dart.method.is_writer_bound_via_
      cell_tag_check_returning_bool` (idiom rf-dart-bind-writer-
      family-callsite-to-csharp-pascalcase-methods). Used once in
      this file ("circular term with unbound variable inside is not
      ground" test).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Single-callsite nuance: only ONE site in this file —
      `expect(rt.heap.isWriterBound(yWriter), isFalse);` ->
      `Assert.False(_rt.Heap.IsWriterBound(yWriter));`. Return type
      is non-nullable `bool` (no null path). The semantic distinction
      `isWriterBound` vs `isFullyBound` is preserved per
      heap_fcp.dart.md — `isWriterBound` checks ONLY the writer's
      cell tag (single-step), whereas `isFullyBound` dereferences
      transitively. THIS test exercises the single-step form.
  - construct_key: dart.method_call.heap_getValue_query_nullable
    source_form: >-
      "rt.heap.getValue(varWriter);
       rt.heap.getValue(xWriter);
       rt.heap.getValue(yWriter);
       rt.heap.getValue(xWriter);
       rt.heap.getValue(copyWriter);
       rt.heap.getValue(copyWriter);
       rt.heap.getValue(xWriter);"
    target_decision: >-
      `_rt.Heap.GetValue(<writerAddr>)` returning `Term?` (per
      heap_fcp.dart.md construct `dart.method.get_value_nullable_term_
      from_deref`). Used seven times across the file; every call
      captures the return into a `var` local before the test asserts
      on it (e.g. `var value = _rt.Heap.GetValue(varWriter);` then
      `Assert.IsType<StructTerm>(value);`).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Nullable-return nuance (explicitly addressed): `GetValue`
      returns `Term?` — but every callsite in THIS file follows
      `_rt.Heap.BindVariable(<writer>, <term>)` on the SAME writer,
      so the return is provably non-null at the call. Codegen MAY
      emit `var value = _rt.Heap.GetValue(varWriter)!;` (null-
      forgiving) to suppress the NRT warning, OR leave the bare
      `var value = _rt.Heap.GetValue(varWriter);` and rely on the
      subsequent `Assert.IsType<StructTerm>(value)` to gate further
      use (`Assert.IsType` already throws if `value` is null because
      `null.GetType()` is unreachable). Recommended form: bare
      `var value = _rt.Heap.GetValue(...);` (no `!` — the assertion
      below acts as the null check, matching xUnit idiom).
  - construct_key: dart.toplevel_function_call.copyTermPredicate_two_arg
    source_form: "copyTermPredicate(rt, call)"
    target_decision: >-
      Dart top-level function `copyTermPredicate(GlpRuntime,
      SystemCall) -> SystemResult` maps to C# `SystemPredicatesImpl.
      CopyTermPredicate(_rt, call)` (or unqualified
      `CopyTermPredicate(_rt, call)` if file emits `using static
      <RootNs>.Runtime.SystemPredicatesImpl;`). Per system_predicates_
      impl.dart.md construct `dart.toplevel_function.predicate_handler_
      two_arg_returning_systemresult` (idiom rf-dart-toplevel-function-
      to-csharp-static-method-on-class), the top-level function was
      lifted into a static class containing all predicate handlers.
      Used twice in this file (both copy_term tests).
    idiom_id: null
    research_finding_id: rf-dart-toplevel-function-to-csharp-static-method-on-class
    nuance: >-
      Top-level-function nuance (explicitly addressed): C# has NO
      top-level functions; the converted call MUST be a static
      method call on a class. The file-scope `using static` directive
      (`using static <RootNs>.Runtime.SystemPredicatesImpl;`) is the
      one-time per-file knob that lets the test code keep the bare
      `CopyTermPredicate(...)` callsite shape. Without `using
      static`, every callsite needs the class qualifier
      `SystemPredicatesImpl.CopyTermPredicate(...)`. RECOMMENDED:
      emit `using static` at the top of the test file ONLY when 2+
      such bare callsites exist in the same file (this file has 2,
      so YES). Reused decision from system_predicates_impl.dart.md.
  - construct_key: dart.enum_member_access.system_result_success
    source_form: "SystemResult.success"
    target_decision: >-
      Dart `SystemResult.success` maps to C# `SystemResult.Success`
      (PascalCased member per system_predicates.dart.md construct
      `dart.enum.three_member_marker_tag_acronymed_members`, idiom
      rf-dart-three-member-enum-pascalcased-to-csharp-enum). Used
      twice in this file (`expect(result, equals(SystemResult.
      success))` -> `Assert.Equal(SystemResult.Success, result);`).
    idiom_id: null
    research_finding_id: rf-dart-three-member-enum-pascalcased-to-csharp-enum
    nuance: >-
      Member-casing nuance (explicitly addressed): UNLIKE `CellTag.
      ValueTag`/`CellTag.WrtTag` (where the Dart members are
      already PascalCase and preserved verbatim — idiom rf-dart-
      plain-enum-to-csharp-enum), the `SystemResult` members are
      Dart-lowerCamelCase (`success`, `failure`, `suspend`) and the
      SUT spec system_predicates.dart.md PascalCases them
      (`Success`, `Failure`, `Suspend`). Codegen MUST apply the
      PascalCase conversion at every callsite. Different rule than
      `CellTag`; both are recorded under their own idioms.
  - construct_key: dart.member_access.struct_term_functor_args
    source_form: >-
      "struct.functor;
       struct.args.length;
       struct.args[0];
       (xValue as StructTerm).functor;
       (yValue as StructTerm).functor;
       xValue.functor;
       yValue.functor;
       copyStruct.functor;
       copyStruct.args.length;
       copyStruct.args[0];
       (copyStruct.args[0] as ConstTerm).value;"
    target_decision: >-
      Dart `.functor`/`.args`/`.args.length`/`.args[N]`/`.value`
      member accesses on StructTerm and ConstTerm map to PascalCase
      C# properties: `.Functor`, `.Args`, `.Args.Count`, `.Args[N]`,
      `.Value`. Per terms.dart.md construct `dart.sum_type_leaf.
      functor_args_list_reference_identity`, `Args` is
      `IReadOnlyList<Term>` which exposes `Count` (NOT `Length`) and
      an indexer `this[int]`.
    idiom_id: null
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Property-naming nuance (explicitly addressed): Dart `.length`
      is universal across List/String/Iterable; C# splits into
      `Count` (collections) vs `Length` (arrays/strings). For `Args`
      (an `IReadOnlyList<Term>`) the correct mapping is `Count`.
      Indexer nuance: Dart `args[0]` and C# `Args[0]` are
      observationally identical (both throw on out-of-range — Dart
      `RangeError`, C# `ArgumentOutOfRangeException`). Reused
      verbatim from binding_pointer_test convspec — no re-research.
  - construct_key: dart.as_cast.type_assertion_on_term_subtype
    source_form: >-
      "(value as StructTerm);
       (xValue as StructTerm).functor;
       (yValue as StructTerm).functor;
       rt.heap.getValue(xWriter) as StructTerm;
       rt.heap.getValue(yWriter) as StructTerm;
       (copyValue as StructTerm);
       (copyStruct.args[0] as ConstTerm).value;"
    target_decision: >-
      Dart `(<expr> as <T>).<member>` (unchecked downcast) maps to
      C# `((<T>)<expr>).<Member>`. Dart `as` throws `TypeError` on
      mismatch; C# `(T)expr` throws `InvalidCastException`. Both are
      "expected success" in every test in this file — codegen MUST
      emit the explicit cast (NOT `expr as T`, which yields `null`
      on mismatch — wrong semantics).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cast-failure-mode nuance (explicitly addressed): Dart `as`
      throws on mismatch; C# `(T)x` ALSO throws (InvalidCastException);
      C# `x as T` returns `null` on mismatch. ALWAYS use the explicit
      cast `(T)x`, NEVER `x as T`, when porting Dart `as`. Member-
      casing nuance: every downcasted member is renamed PascalCase
      (`functor` -> `Functor`, `args` -> `Args`, `value` -> `Value`).
      Type-pattern modernisation nuance: C# 9+ `is T t` would be
      more idiomatic for cases following `Assert.IsType<T>(x)` —
      but the file uses separate `expect(x, isA<T>())` + `(x as T).
      f` so the literal mapping preserves the two-step shape.
      Reused verbatim from binding_pointer_test convspec — no re-
      research.
  - construct_key: dart.method_call.term_tostring
    source_form: "value.toString()"
    target_decision: >-
      Dart `Term.toString()` maps to C# `value.ToString()`. Per
      terms.dart.md, the `Term` abstract base class declares a
      `toString()` override on every concrete leaf (ConstTerm,
      StructTerm, VarRef). C# `Object.ToString()` is universally
      available; the SUT spec records the override on each leaf's
      converted class as `public override string ToString()`. Used
      once in this file (`expect(() => value.toString(),
      returnsNormally);` -> `var _ = value.ToString();` — see
      `dart.package_test.expect_function_returnsNormally` below).
    idiom_id: null
    research_finding_id: rf-dart-object-tostring-to-csharp-object-tostring
    nuance: >-
      Override-presence nuance (explicitly addressed — load-bearing
      for THIS test): the test "circular term does not cause
      infinite loop in toString" depends on the StructTerm
      `ToString()` override being TERMINATING on a cyclic Args list.
      Per terms.dart.md, the StructTerm `toString()` implementation
      builds a string from `Functor` + a join of `Args` — if any
      `VarRef` arg's `toString()` calls back into the parent
      structure, the loop is infinite. The Dart implementation
      AVOIDS this because `VarRef.toString()` returns
      `"V<addr>"` (formatted by ADDRESS, NOT by dereferenced VALUE
      — so no heap walk, no cycle). The C# `VarRef.ToString()`
      override MUST preserve the same address-only formatting per
      terms.dart.md (recorded under construct `dart.sum_type_leaf.
      variable_ref_int_address_value_equality`, override clause
      "ToString() returns "V"+Addr"). This is the well-known nuance
      that "value-vs-reference"-style cycle protection lives in the
      ToString implementation of VarRef, NOT in StructTerm. Codegen
      MUST NOT introduce a cycle-detecting `HashSet<object>` to
      StructTerm.ToString() — the cycle is already broken by
      VarRef's address-only formatting.
  - construct_key: dart.package_test.expect_equals
    source_form: >-
      "expect(struct.functor, equals('f'));
       expect(struct.args.length, equals(1));
       expect((xValue as StructTerm).functor, equals((yValue as StructTerm).functor));
       expect(result, equals(SystemResult.success));
       expect(copyStruct.functor, equals('f'));
       expect(copyStruct.args.length, equals(2));
       expect((copyStruct.args[0] as ConstTerm).value, equals('a'));
       expect(copyStruct.functor, equals('f'));
       expect(copyStruct.args.length, equals(2));"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)` — ARGUMENT-
      ORDER FLIP. The `equals` matcher uses Dart `==` equality;
      `Assert.Equal` uses `IEquatable<T>.Equals` / `Object.Equals`,
      equivalent for the value-typed comparisons in this file
      (`String`, `int`->`long`, `SystemResult` enum, `object?` boxed
      via ConstTerm.Value). For the `xValue.Functor ==
      yValue.Functor` cross-comparison, BOTH operands are `string`
      literals "f" — Assert.Equal does ordinal string equality
      which matches Dart `==` exactly.
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (explicitly addressed — well-known
      footgun): Dart `expect(actual, equals(expected))` puts actual
      first; xUnit `Assert.Equal(expected, actual)` puts expected
      first. Codegen MUST flip at the boundary at every callsite.
      Enum-equality nuance: `Assert.Equal(SystemResult.Success,
      result)` works because `SystemResult` is a value-type enum
      (default underlying `int`) and `Equals` is structural. String
      cross-comparison nuance: `(xValue as StructTerm).functor ==
      (yValue as StructTerm).functor` maps to
      `Assert.Equal(((StructTerm)yValue).Functor,
      ((StructTerm)xValue).Functor);` — order flip means the FIRST
      argument is the "expected" (here, `yValue.Functor`) and the
      SECOND is the "actual" (here, `xValue.Functor`). Either order
      is semantically equivalent for this symmetric comparison;
      codegen MAY preserve the source order by emitting
      `Assert.Equal(((StructTerm)xValue).Functor,
      ((StructTerm)yValue).Functor);`. Reused verbatim from
      binding_pointer_test convspec — no re-research.
  - construct_key: dart.package_test.expect_isA_T
    source_form: >-
      "expect(value, isA<StructTerm>());
       expect(struct.args[0], isA<VarRef>());
       expect(xValue, isA<StructTerm>());
       expect(yValue, isA<StructTerm>());
       expect(copyValue, isA<StructTerm>());
       expect(copyStruct.args[0], isA<ConstTerm>());
       expect(copyValue, isA<StructTerm>());
       expect(value, isA<StructTerm>());"
    target_decision: >-
      Map to xUnit `Assert.IsType<T>(<actual>)`. The `isA<T>()`
      matcher in `package:test` asserts the value IS a T (subtype-
      tolerant in Dart). xUnit `Assert.IsType<T>` asserts EXACT
      type match; `Assert.IsAssignableFrom<T>` asserts subtype.
    idiom_id: null
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-vs-subtype nuance (explicitly addressed): Dart `isA<T>`
      accepts SUBTYPES; xUnit `Assert.IsType<T>` requires
      `actual.GetType() == typeof(T)`; `Assert.IsAssignableFrom<T>`
      accepts subtypes. In THIS file every `isA<T>()` target is a
      CONCRETE sealed Term leaf (StructTerm, VarRef, ConstTerm) per
      terms.dart.md — none has known subtypes. Codegen SHOULD emit
      `Assert.IsType<T>` because it is observably equivalent AND
      gives a strictly tighter assertion. Reused verbatim from
      binding_pointer_test convspec — no re-research.
  - construct_key: dart.package_test.expect_isFalse_isTrue
    source_form: >-
      "expect(rt.heap.isWriterBound(yWriter), isFalse);
       expect(rt.heap.isWriterBound(copyWriter), isTrue);"
    target_decision: >-
      Map `isTrue` to `Assert.True(<bool-expr>)` and `isFalse` to
      `Assert.False(<bool-expr>)`. Reused from boot_loader_test +
      binding_pointer_test convspecs (rf-dart-expect-istrue-to-
      xunit-asserttrue covers both branches).
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Diagnostic nuance: `Assert.True(b)` / `Assert.False(b)`
      without a message produces a generic failure — adequate for
      the simple predicates here (`_rt.Heap.IsWriterBound(yWriter)`
      and `_rt.Heap.IsWriterBound(copyWriter)`). Reused verbatim
      from binding_pointer_test convspec — no re-research.
  - construct_key: dart.package_test.expect_function_returnsNormally
    source_form: "expect(() => value.toString(), returnsNormally);"
    target_decision: >-
      Dart `expect(() => <fn>, returnsNormally)` maps to a BARE
      call of the function at xUnit method level — xUnit asserts
      "no exception" implicitly (the test is `[Fact]` and any
      thrown exception fails the test). Codegen emits
      `value.ToString();` (or `_ = value.ToString();` to silence
      the "expression value not used" hint). NO `Assert.*` wrapper
      needed — xUnit's pass condition is "no unhandled exception".
    idiom_id: null
    research_finding_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    nuance: >-
      Positive-matcher nuance (explicitly addressed): Dart's
      `returnsNormally` is a POSITIVE matcher (it asserts NO
      exception — opposite of `throwsA`). xUnit has no dedicated
      positive matcher; instead the absence of an unhandled
      exception in a `[Fact]` body IS the assertion of normal
      return. The bare-call mapping is the canonical xUnit idiom
      for this Dart pattern (per `partial_evaluator_test.dart.md`
      research finding, grounded in xUnit FAQ and `package:matcher`
      `returnsNormally` constant docs). For THIS file the bare call
      `value.ToString();` AND its accompanying `Assert.IsType<
      StructTerm>(value);` on the prior line BOTH execute — if
      `ToString()` throws (e.g. on a hypothetical infinite recursion),
      the [Fact] method propagates the exception and xUnit fails the
      test with a "Test method threw exception" diagnostic, which is
      observationally equivalent to Dart's `returnsNormally` failure
      mode. Reused verbatim from partial_evaluator_test convspec —
      no re-research.
conversion_units:
  - cu-1: file-scope using directives (`using Xunit;` + the SUT runtime namespace `using <RootNs>.Runtime;`; OPTIONAL `using static <RootNs>.Runtime.SystemPredicatesImpl;` to keep bare `CopyTermPredicate(...)` callsites)
  - cu-2: namespace declaration mirroring the Dart `test/` directory (e.g. `<RootNs>.Test`)
  - cu-3: top-level test class `CircularTermTests` carrying the file-level XML doc-comment (the three-bullet `<list type="bullet">` rationale)
  - cu-4: private instance field `private GlpRuntime _rt = null!;` (null!-initialised; constructor assigns)
  - cu-5: constructor `CircularTermTests()` assigning `_rt = new GlpRuntime();` (setUp mapping)
  - cu-6: 2 `[Fact]` methods in the "Ground Guard with Circular Terms" group — `GroundGuardWithCircularTerms_CircularTermWithoutUnboundVariablesIsGround` and `GroundGuardWithCircularTerms_CircularTermWithUnboundVariableInsideIsNotGround`; each `[Trait("Group", "Ground Guard with Circular Terms")]`; each `[Fact(DisplayName = "<original label>")]`
  - cu-7: 2 `[Fact]` methods in the "Equality (=?=) with Circular Terms" group — `EqualityWithCircularTerms_IdenticalCircularTermsAreEqual` and `EqualityWithCircularTerms_DifferentCircularTermsAreNotEqual`; each `[Trait("Group", "Equality (=?=) with Circular Terms")]`; each `[Fact(DisplayName = "<original label>")]`
  - cu-8: 2 `[Fact]` methods in the "Deep Copy with Circular Terms" group — `DeepCopyWithCircularTerms_CopyOfCircularTermPreservesStructure` and `DeepCopyWithCircularTerms_CopyOfAcyclicTermCreatesIndependentCopy`; each `[Trait("Group", "Deep Copy with Circular Terms")]`; each `[Fact(DisplayName = "<original label>")]`
  - cu-9: 1 `[Fact]` method in the "Term Formatter with Circular Terms" group — `TermFormatterWithCircularTerms_CircularTermDoesNotCauseInfiniteLoopInToString`; `[Trait("Group", "Term Formatter with Circular Terms")]`; `[Fact(DisplayName = "circular term does not cause infinite loop in toString")]`; body ends with bare-call `value.ToString();`
  - cu-10: each method body uses tuple-deconstruction `var (varWriter, varReader) = _rt.Heap.AllocateVariable();` (et al.); reaches the heap via `_rt.Heap`; calls predicate handlers as `CopyTermPredicate(_rt, call)` (if `using static`) or `SystemPredicatesImpl.CopyTermPredicate(_rt, call)`; constructs `new SystemCall("copy_term", new List<object?> { ... })`; downcasts via `(StructTerm)value` (NEVER `value as StructTerm`); asserts via `Assert.Equal(<expected>, <actual>)` (arg-order flipped), `Assert.IsType<T>(<actual>)`, `Assert.True/False(<bool>)`
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

Project-pinned. The authoritative basis is unchanged: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `setUp` / `test` /
`expect` / matcher semantics. Reused via `rf-dart-package-test-
import-to-xunit-using` — no re-research (FR-024 cache hit; SC-007
reuse).

### Nested group with shared setUp (boot_loader_test shape)

Unlike `binding_pointer_test` (six SIBLING groups + no `late` field
+ per-test `final heap = HeapFCP();`), this file has the
`boot_loader_test` shape: ONE outer group + a `late GlpRuntime rt`
field + a `setUp` + FOUR sibling inner groups. The conversion
therefore reuses the constructor-as-setUp mapping pinned by
`boot_loader_test.dart.md`: a single `CircularTermTests` class with
a `private GlpRuntime _rt = null!;` field assigned in the
constructor. xUnit per-test class instantiation
(`https://xunit.net/docs/shared-context#constructor`) gives the
same per-test isolation Dart `setUp` does.

### Record-destructuring `(int, int)` from `allocateVariable`

Dart 3 records map cleanly to C# tuple deconstruction
(`https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records`
plus tuple-deconstruction reference
`https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/deconstruct`).
The `_` discard works identically in both languages. Width nuance
is load-bearing: `allocateVariable` returns `(long, long)` per
heap_fcp.dart.md (idiom rf-dart-record-return-to-csharp-valuetuple),
NOT `(int, int)` — every deconstructed local in this file is
`long`. Codegen MUST NOT silently narrow to `int`.

### Constructor calls reuse the lib/runtime spec decisions

Every Term-family construction in this file reuses an idiom pinned
by lib/runtime/terms.dart.md or lib/runtime/system_predicates.dart.md
or lib/runtime/runtime.dart.md. No new research: `ConstTerm(<lit>)`
-> `new ConstTerm(<lit>)` (rf-dart-sumleaf-no-eq-to-csharp-class-no-
record), `StructTerm(<func>, [<terms>])` -> `new StructTerm(<func>,
new List<Term> { ... })` (rf-dart-list-literal-to-csharp-list-of-T
+ rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist),
`VarRef(<addr>)` -> `new VarRef(<addr>)` (rf-dart-class-eq-on-
single-int-field-to-csharp-iequatable), `SystemCall(<name>, <args>)`
-> `new SystemCall(<name>, new List<object?> { ... })` (rf-dart-
mutable-callcontext-class-to-csharp-reference-class — reference
identity preserved because `SuspendedReaders` is a mutation
surface), `GlpRuntime()` -> `new GlpRuntime()` (rf-dart-mutable-
state-class-identity-equality-to-csharp-class — all named-optional
params defaulted).

### Top-level `copyTermPredicate` lifted to a static class

C# has NO top-level functions; per system_predicates_impl.dart.md
construct `dart.toplevel_function.predicate_handler_two_arg_
returning_systemresult` (idiom rf-dart-toplevel-function-to-csharp-
static-method-on-class), every Dart predicate handler was lifted to
a `public static` method on a containing class (e.g.
`SystemPredicatesImpl`). This file invokes `copyTermPredicate(rt,
call)` twice — codegen emits a file-scope `using static <RootNs>.
Runtime.SystemPredicatesImpl;` so the bare callsite shape survives,
OR fully qualifies as `SystemPredicatesImpl.CopyTermPredicate(_rt,
call)`. Both are correct; the `using static` form is preferred when
the callsite count justifies it (2+ in this file).

### `Assert.Equal` argument-order flip

Dart `expect(actual, equals(expected))` puts actual first; xUnit
`Assert.Equal(expected, actual)` puts expected first
(`https://xunit.net/docs/comparisons#assertions`). This file has 9
`equals(...)` calls — every one MUST be flipped at the boundary.
Reused verbatim from boot_loader_test + binding_pointer_test
convspecs.

### `isA<T>` -> `Assert.IsType<T>` (exact match)

Dart `isA<T>` matcher accepts subtypes; xUnit `Assert.IsType<T>`
requires EXACT type (`https://xunit.net/docs/comparisons#assertions`,
`https://learn.microsoft.com/dotnet/api/xunit.assert.istype`). In
THIS file every `isA<T>()` target is a sealed Term leaf
(ConstTerm, StructTerm, VarRef) per terms.dart.md with no known
subtypes; `Assert.IsType<T>` is observably equivalent and strictly
tighter.

### `returnsNormally` -> bare call (no assertion wrapper needed)

Dart `expect(() => fn(), returnsNormally)` asserts "no exception".
xUnit has no positive matcher for this; the canonical idiom is to
emit a BARE call — any unhandled exception fails the `[Fact]`
implicitly. Grounded in xUnit FAQ (`https://xunit.net/docs/comparisons`)
and `package:matcher` `returnsNormally` constant docs
(`https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`).
Reused verbatim from partial_evaluator_test convspec — no re-
research.

### Cycle protection lives in `VarRef.ToString()`, not `StructTerm.ToString()`

The "circular term does not cause infinite loop in toString" test
depends on `VarRef.ToString()` formatting by ADDRESS, not by
dereferenced VALUE — so the toString walk on a cyclic `StructTerm
('f', [VarRef(reader)])` produces a finite string like `f(V<addr>)`
and terminates without consulting the heap. Per terms.dart.md
construct `dart.sum_type_leaf.variable_ref_int_address_value_
equality`, the C# `VarRef.ToString()` override returns `"V" + Addr`
— address-only formatting that breaks any heap-level cycle at the
formatting layer. Codegen MUST NOT introduce a cycle-detecting
`HashSet<object>` to StructTerm.ToString() or to any other Term
leaf's ToString — the documented invariant is that VarRef's
ToString does NOT dereference, and that single rule terminates the
walk on ALL cyclic term shapes (matches the Dart implementation
exactly).

### Why no escalations

Every construct has a clear, single-decision target shape grounded
in official Dart / .NET documentation, and EVERY non-trivial
construct reuses an idiom_id-equivalent rf-* already recorded in a
precedent spec (`heap_fcp.dart.md`, `cells.dart.md`, `terms.dart.md`,
`runtime.dart.md`, `system_predicates.dart.md`,
`system_predicates_impl.dart.md`, `binding_pointer_test.dart.md`,
`boot_loader_test.dart.md`, `smoke_test.dart.md`,
`partial_evaluator_test.dart.md`). The two file-local nuance calls
— `Assert.IsType<T>` over `Assert.IsAssignableFrom<T>` (justified
by sealed leaves) and bare-call over `Assert.*` wrapper for
`returnsNormally` (justified by xUnit's implicit "no-throw" pass
condition) — are deliberate, in-file-justified choices with
corroborating alternatives recorded in their research findings, not
undecidable points. `escalations: []` is therefore intentional.
