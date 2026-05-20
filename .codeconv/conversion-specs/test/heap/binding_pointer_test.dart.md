> Conversion-spec artifact for test/heap/binding_pointer_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/heap/binding_pointer_test.dart
source_sha256: 60cea5fbe3415839b21caf214b4f3ca09470e8fb038192fc05274f68924360e7
target_code_unit: test/heap/BindingPointerTest.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "library;"
    target_decision: >-
      Elide entirely; C# files have no `library` directive. The doc-comment
      block ABOVE the `library;` keyword (spec citation, bullet list of
      scenarios) is preserved as the test class XML doc-comment on
      `BindingPointerTests`.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Same nuance as every `library;`: Dart library scope = the file;
      C# scope is `namespace`. No name to carry; the per-file doc-comment
      becomes the test-class doc-comment so the human-readable
      "Tests for binding operations with Pointer Architecture Heap" lead
      and the `docs/heap-pointer-architecture-spec.md v3.0` provenance
      survive the conversion.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. Project-wide pinning: xUnit is
      the chosen .NET test framework (precedent: test/multiagent/
      mad_error_handling_test.dart.md, test/multiagent/boot_loader_test.
      dart.md). Codegen MUST also add `using System;` (for
      `InvalidOperationException`/`StateError` mapping in `throwsStateError`
      construct below) and project to a single namespace mirroring the
      Dart `test/heap` directory (e.g. `<RootNs>.Test.Heap`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance, NOT a
      file-local choice: every `package:test` file in the inventory MUST
      map to the SAME .NET framework (xUnit) so test discovery, runner
      config, and attribute vocabulary stay consistent. Reused verbatim
      from boot_loader_test convspec — NO re-research (FR-024 cached).
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/heap_fcp.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/suspension.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';"
    target_decision: >-
      Four `package:glp_runtime/runtime/...` imports collapse to ONE
      `using <RootNs>.Runtime;` (the converted runtime sub-namespace
      decided by the four SUT specs: lib/runtime/heap_fcp.dart.md,
      lib/runtime/terms.dart.md, lib/runtime/suspension.dart.md,
      lib/runtime/machine_state.dart.md). C# `using` is per-namespace,
      not per-file — multiple Dart imports into the SAME C# namespace
      collapse. This spec records only the shape of the cross-file
      dependency; the namespace string is owned by the SUT specs.
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: in Dart each `package:` URI is a
      separate import; in C# all four converted files share the runtime
      namespace, so a single `using` suffices. Test assembly must
      reference the SUT assembly via the project file (project-system
      idiom; out of scope for THIS artifact).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('bindWriter - Ground Values', () { ... }); group('bindWriterToReader - Variable Chains', () { ... }); group('WxW Violation Detection', () { ... }); group('Binding with Suspensions', () { ... }); group('Binding State Transitions', () { ... }); group('isFullyBound and getValue', () { ... }); }"
    target_decision: >-
      Eliminate `main` entirely; xUnit discovers `[Fact]` methods by
      reflection — there is NO per-file entrypoint to emit. The six
      top-level `group(...)` calls inside `main`'s body become the
      enclosing test class with `[Trait]`-grouped methods (see
      `dart.package_test.group_block` below).
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook — only per-class (constructor +
      `IDisposable.Dispose`) and per-collection fixtures. THIS file's
      `main` body is six `group(...)` calls with no other statements, so
      the omission is lossless.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('bindWriter - Ground Values', () { test(...); ... });
       group('bindWriterToReader - Variable Chains', () { test(...); ... });
       group('WxW Violation Detection', () { test(...); ... });
       group('Binding with Suspensions', () { test(...); ... });
       group('Binding State Transitions', () { test(...); ... });
       group('isFullyBound and getValue', () { test(...); ... });"
    target_decision: >-
      Six sibling top-level groups (NO nesting in this file, unlike
      boot_loader_test). Map to a SINGLE PascalCase xUnit test class
      `BindingPointerTests` containing ALL test methods, with each
      group's label preserved as `[Trait("Group", "<label>")]` on every
      test method belonging to that group. Per-test method names are
      group-prefixed PascalCased, identifier-safe forms of the label so
      collisions across groups are impossible (e.g.
      `BindWriterGroundValues_BindToConstTermInteger`,
      `BindWriterToReaderVariableChains_BasicBindingCreatesPointer`,
      `WxwViolationDetection_BindWriterToWriterThrowsStateError`,
      `BindingWithSuspensions_BindingGroundValueActivatesAllSuspensions`,
      `BindingStateTransitions_UnboundWriterHasPointerToReader`,
      `IsFullyBoundAndGetValue_GetValueFollowsChain`). The original test
      label MUST be preserved verbatim via `[Fact(DisplayName = "<original
      label>")]`.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Flat-group nuance (explicitly addressed, differs from boot_loader_
      test which had nested groups): six sibling groups + 27 tests with
      NO shared per-test state across groups — every test allocates its
      own `final heap = HeapFCP()` inside its own callback. FLATTEN is
      strictly simpler than the boot_loader case because there is no
      `late` field to hoist to a constructor — see next construct. The
      label-mangling nuance applies: spaces, hyphens, and special chars
      ("WxW", "isFullyBound and getValue") are PascalCased and stripped
      to identifier-safe form; the `DisplayName` preserves the original.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange (HeapFCP+allocateVariable), act (bindWriter/bindWriterToReader/...), assert (expect ...) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance method
      on `BindingPointerTests`, decorated with `[Fact(DisplayName =
      "<original label>")]` plus `[Trait("Group", "<enclosing group
      label>")]`. The method name is the group-prefixed PascalCased label
      (see group_block). The closure body converts statement-for-
      statement into the method body (arrange = local `var heap = new
      HeapFcp();` + record-deconstruction; act = the heap mutation
      methods; assert = `expect(...)` translations below). All 27 `test`
      calls in this file are synchronous (no `async`/`Future`) so no
      target method is `async Task`. Each test starts with its OWN
      `var heap = new HeapFcp();` (no shared `_heap` field) — codegen
      MUST NOT hoist this to a constructor field because per-test isolation
      is already provided by xUnit's per-test class instantiation AND
      the explicit per-test `final heap = HeapFCP()` makes the lack of
      sharing intentional.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      No-shared-state nuance (explicitly addressed, differs from
      boot_loader_test): boot_loader_test had a `late BootLoader loader`
      hoisted to a constructor field; THIS file deliberately does NOT
      share `heap` across tests — every test (and even one test's
      `final heap2 = HeapFCP();` second arena, see test "unbound chain
      returns final writer VarRef") instantiates its own. Codegen MUST
      preserve that locality: emit `var heap = new HeapFcp();` (and
      where present `var heap2 = new HeapFcp();`) as a method-local
      `var`, NOT as an instance field. Async nuance recorded but not
      exercised in this file.
  - construct_key: dart.local_var.final_constructor_instance
    source_form: "final heap = HeapFCP();"
    target_decision: >-
      Dart `final <T> x = <expr>;` with type inferred from the RHS maps
      to C# `var x = <expr>;` for method-local lifetimes. `final` is
      assignment-once; `var` produces a non-readonly local but the
      single-assignment pattern is preserved by the converted body
      (there is no reassignment of `heap` anywhere in this file). Per-
      method local; never an instance field (see test_call_simple
      nuance).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Reassignment nuance: Dart `final` is enforced; C# `var` is not.
      Codegen MUST NOT introduce a reassignment, but does NOT need to
      emit `readonly` (illegal on locals in C#). If a future test
      reassigned the local, the conversion would need to drop `final`
      anyway. For `heap`/`heap2`/`struct`/`inner`/`outer`/`value`/`r1`/
      `r2`/`acts1`/`acts2`/`activations`/`ids`/`result`/`wc` — all
      single-assignment locals in this file — the mapping is uniform.
  - construct_key: dart.local_var.record_destructuring_two_ints_or_ignored
    source_form: >-
      "final (writerAddr, _) = heap.allocateVariable();
       final (_, r2) = heap.allocateVariable();
       final (w1, r1) = heap.allocateVariable();"
    target_decision: >-
      Dart record-positional-destructuring of a `(int, int)` returned by
      `heap.allocateVariable()` maps to C# tuple-deconstruction:
      `var (writerAddr, _) = heap.AllocateVariable();`,
      `var (_, r2)        = heap.AllocateVariable();`,
      `var (w1, r1)       = heap.AllocateVariable();`. C# 7+ supports
      `_` as a discard in deconstruction. The SUT method's return type
      is `(long writerAddr, long readerAddr)` per heap_fcp.dart.md
      construct `dart.tuple_return.record_two_int_addresses_allocate_
      variable` (mapped to `ValueTuple<long,long>` with named elements).
    idiom_id: null
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      Discard-vs-bind nuance (explicitly addressed): Dart `(writerAddr,
      _)` discards the reader; `(_, r2)` discards the writer; `(w1,
      r1)` binds both. C# `var (a, _)` deconstruction supports the
      identical pattern. The int-width nuance — Dart `int` (64-bit
      arbitrary on web, 64-bit on VM) MUST map to C# `long` to preserve
      address-arithmetic width (per cells.dart.md construct
      `dart.int.fixed_width_identity_field`, idiom rf-dart-int-to-
      csharp-long-width). Codegen MUST keep both deconstructed names
      typed as `long`, not `int`.
  - construct_key: dart.constructor_call.const_term_with_value
    source_form: "ConstTerm(42), ConstTerm('hello'), ConstTerm(3.14159), ConstTerm(null), ConstTerm(true), ConstTerm('value'), ConstTerm('x'), ConstTerm('end'), ConstTerm('final'), ConstTerm('chain_end'), ConstTerm('y')"
    target_decision: >-
      Dart positional `ConstTerm(<lit>)` maps to C# `new ConstTerm(<lit>)`.
      The SUT type per terms.dart.md construct `dart.sum_type_leaf.
      value_carrying_no_eq_override_reference_identity` (idiom rf-dart-
      sumleaf-no-eq-to-csharp-class-no-record) is a `sealed class
      ConstTerm : Term` with a single nullable `object? Value` field
      (Dart `dynamic` -> C# `object?`). Numeric/string/null/bool literals
      box transparently into `object?`.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Literal-typing nuance (explicitly addressed): Dart `42` is `int`,
      `3.14159` is `double`, `'hello'` is `String`, `null` is `Null`,
      `true` is `bool`. All box to C# `object?`. C# `new ConstTerm(42)`
      boxes `int` (not `long`!) — this is a deliberate divergence from
      the address-width mapping: heap address widths are `long` for
      pointer arithmetic, but `ConstTerm`'s payload is a user-supplied
      value object whose runtime type is preserved via `object?` boxing.
      Equality nuance: Dart `(value as ConstTerm).value` equality uses
      `==`; C# `((ConstTerm)value).Value` equality uses `Equals` — for
      the boxed primitives in this file (`int`/`string`/`double`/`bool`/
      `null`) the two are observably equivalent. Reference-identity
      nuance for the wrapper class itself comes from rf-dart-sumleaf-no-
      eq-to-csharp-class-no-record (no `record` synthesis — preserve
      reference identity).
  - construct_key: dart.constructor_call.struct_term_with_functor_and_args_list
    source_form: >-
      "StructTerm('point', [ConstTerm(10), ConstTerm(20)]);
       StructTerm('inner', [ConstTerm('x')]);
       StructTerm('outer', [inner, ConstTerm('y')]);
       StructTerm('f', [VarRef(r2)]);"
    target_decision: >-
      Dart `StructTerm(<functor>, <list-of-Term>)` maps to C# `new
      StructTerm(<functor>, new List<Term> { ... })`. Per terms.dart.md
      construct `dart.sum_type_leaf.functor_args_list_reference_identity`
      (idiom rf-dart-sumleaf-with-list-no-eq-to-csharp-class-
      ireadonlylist), the SUT type is `sealed class StructTerm : Term`
      with `string Functor` and `IReadOnlyList<Term> Args`. Codegen
      passes `new List<Term> { ... }` (which is assignable to
      `IReadOnlyList<Term>`) or `new Term[] { ... }`.
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      List-literal nuance (explicitly addressed): Dart `[a, b]` of type
      `List<Term>` maps to C# `new List<Term> { a, b }` (mutable
      List<T>; preferred over `new Term[]` because the SUT field type is
      `IReadOnlyList<Term>` which `List<T>` implements covariantly).
      Nested-struct nuance (present in this file, "bind to nested
      StructTerm"): `StructTerm('outer', [inner, ConstTerm('y')])` —
      `inner` is a previously-bound local; codegen preserves the local
      reference, NO clone (the test asserts identity-by-content via
      `expect(value.args[0], isA<StructTerm>())` which is a TYPE test,
      not a reference comparison).
  - construct_key: dart.constructor_call.var_ref_single_int_addr
    source_form: "VarRef(r2)"
    target_decision: >-
      `new VarRef(r2)`. SUT type per terms.dart.md construct
      `dart.sum_type_leaf.variable_ref_int_address_value_equality` (idiom
      rf-dart-class-eq-on-single-int-field-to-csharp-iequatable) is a
      `sealed class VarRef : Term, IEquatable<VarRef>` with a single
      `long Addr` field. `r2` is a `long` deconstructed earlier; passes
      directly.
    idiom_id: null
    research_finding_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    nuance: >-
      Value-equality nuance (explicitly addressed): `VarRef` is the ONE
      Term subtype in terms.dart that overrides `==` to compare by
      `addr`. The C# mapping uses `IEquatable<VarRef>` and overrides
      `Equals(object?)` + `GetHashCode()` so `Assert.Equal(varRefA,
      varRefB)` compares by address (preserving the file's assertion
      `expect((result as VarRef).addr, equals(wb))` semantics). For
      `Assert.Same` reference-identity, codegen MUST emit
      `Assert.NotSame` or `Assert.Equal` deliberately — this file only
      exercises the `.addr` field comparison via `Assert.Equal(wb,
      ((VarRef)result).Addr)`, NOT the whole-object equality.
  - construct_key: dart.constructor_call.suspension_record_two_ints
    source_form: "SuspensionRecord(1, 100), SuspensionRecord(2, 200), SuspensionRecord(3, 300), SuspensionRecord(10, 1000)"
    target_decision: >-
      `new SuspensionRecord(1, 100)` etc. SUT type per suspension.dart.md
      construct (idiom rf-dart-shared-mutable-record-by-reference-to-
      csharp-class): `class SuspensionRecord` with positional ctor
      `(long? goalId, long resumePc)` and a mutable `bool Armed { get;
      private set; } = true;` plus a `Disarm()` method. Codegen MUST
      preserve REFERENCE identity (no `record struct` synthesis) because
      `disarm()` mutation is observed across heap state.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      Reference-identity nuance (explicitly addressed): SuspensionRecord
      instances are passed by reference into the heap's suspension list;
      `r1.disarm()` later mutates the SAME object the heap holds. C#
      `record class` (or `record struct`) would synthesise structural
      equality and `with`-cloning — both forbidden here. The mapping is
      a plain `class` (already pinned by suspension.dart.md).
  - construct_key: dart.constructor_call.pointer_single_int_addr
    source_form: "Pointer(w2)"
    target_decision: >-
      `new Pointer(w2)`. Per cells.dart.md construct `dart.pointer_class.
      single_final_int_address_reference_identity_tostring_only` (idiom
      rf-dart-sumleaf-no-eq-to-csharp-class-no-record): plain reference-
      identity wrapper around a single `long TargetAddr` field.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Direct-mutation nuance (explicitly addressed for the WxW test
      "indirect WxW through deref detected"): the test BYPASSES the
      safe `bindWriterToReader` and directly assigns `heap.cells[w1].
      content = Pointer(w2);` to simulate a corrupt heap. The C# field
      `Content` MUST be a writable property (`get; set;` or `public`
      field) — already pinned by cells.dart.md construct
      `dart.heap_cell_class.dynamic_content_mutable_tag_reference_
      identity` (idiom rf-dart-shared-mutable-record-by-reference-to-
      csharp-class). No setter-encapsulation refactor here — the test
      DEPENDS on direct content assignment, which is the documented
      shape.
  - construct_key: dart.method_call.heap_mutator_void_or_returning_activations
    source_form: >-
      "heap.bindWriter(writerAddr, ConstTerm(42));
       heap.bindWriterToReader(w1, r2);
       heap.bindWriterToWriter(w1, w2);   // throws
       heap.suspendOnWriter(writerAddr, SuspensionRecord(1, 100));
       final activations = heap.bindWriter(writerAddr, ConstTerm('value'));
       final acts1 = heap.bindWriterToReader(w1, r2);
       final acts2 = heap.bindWriter(w2, ConstTerm('done'));"
    target_decision: >-
      Dart instance method calls map to PascalCase C# instance method
      calls on the converted `HeapFcp` type per heap_fcp.dart.md:
      `heap.BindWriter(writerAddr, new ConstTerm(42));`
      `heap.BindWriterToReader(w1, r2);`
      `heap.BindWriterToWriter(w1, w2);`
      `heap.SuspendOnWriter(writerAddr, new SuspensionRecord(1L, 100L));`
      Return-type nuance: `bindWriter` and `bindWriterToReader` return
      `List<SuspensionRecord> activations` per heap_fcp.dart.md construct
      `dart.bind_writer_family.callback_control_with_in_place_mutation_
      returning_activation_list` — codegen MUST preserve the return value
      capture (`var activations = ...`, `var acts1 = ...`, `var acts2 =
      ...`).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Naming-convention nuance (explicitly addressed): Dart instance
      methods are `lowerCamelCase`; C# instance methods are `PascalCase`.
      Codegen MUST apply the convention consistently across every
      callsite in the test, matching the public surface decided in
      heap_fcp.dart.md (which itself names the methods `BindWriter`,
      `BindWriterToReader`, `BindWriterToWriter`, `SuspendOnWriter`,
      `DerefAddr`, `IsFullyBound`, `GetValue`, `AllocateVariable`).
      Return-type nuance: `bindWriter` returning `List<SuspensionRecord>`
      maps to C# `List<SuspensionRecord>` (NOT `IReadOnlyList<>` because
      heap_fcp.dart.md's source returns a freshly built mutable list and
      the test code calls `.length` and `.first.id` — both work on
      `List<T>`).
  - construct_key: dart.method_call.heap_query_deref_isfullybound_getvalue
    source_form: >-
      "heap.derefAddr(r1);
       heap.isFullyBound(writerAddr);
       heap.getValue(writerAddr);"
    target_decision: >-
      Map to PascalCase methods per heap_fcp.dart.md:
      `heap.DerefAddr(r1)` returns `Term` (per construct
      `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_
      violation_and_three_tag_cases`),
      `heap.IsFullyBound(writerAddr)` returns `bool` (per construct
      `dart.method.is_fully_bound_via_deref_returning_bool`),
      `heap.GetValue(writerAddr)` returns `Term?` (per construct
      `dart.method.get_value_nullable_term_from_deref`).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Nullable-return nuance (explicitly addressed): `GetValue` returns
      `Term?` — the test `expect(heap.getValue(writerAddr), isNull)`
      exercises the null path; codegen MUST emit `Assert.Null(heap.
      GetValue(writerAddr))` which works on any nullable reference.
      `DerefAddr` does NOT return null; it returns a `Term` (possibly a
      `VarRef` for unbound chains, as the "unbound chain returns final
      writer VarRef" test exercises). The exception path (`throwsStateError`
      from `DerefAddr` on WxW corruption) is a documented behaviour of
      construct `dart.deref_addr.large_switch_...`.
  - construct_key: dart.instance_method_call.suspension_disarm
    source_form: "r1.disarm();"
    target_decision: >-
      `r1.Disarm();`. SUT method per suspension.dart.md (idiom rf-dart-
      shared-mutable-record-by-reference-to-csharp-class).
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Mutation-side-effect nuance: `Disarm()` mutates `Armed` to false;
      the assertion `expect(r1.armed, isFalse)` then `Assert.False(r1.
      Armed)`. The mutation is observable through the heap's suspension
      list (the "disarmed suspensions not activated" test depends on
      this aliasing).
  - construct_key: dart.field_indexer.cells_at_addr_with_member_access
    source_form: >-
      "heap.cells[writerAddr].tag;
       heap.cells[writerAddr].content;
       heap.cells[w1].content = Pointer(w2);"
    target_decision: >-
      Dart `heap.cells[i]` (list indexing) + member access maps to C#
      `heap.Cells[i].Tag` / `heap.Cells[i].Content`. Per heap_fcp.dart.md
      construct `dart.heap_class.master_runtime_state_list_of_cells_
      mutable_hp_callback_map`, the SUT field `Cells` is `List<Cell>`
      (mutable list) and `Cell.Tag`/`Cell.Content` are settable
      properties (idiom rf-dart-shared-mutable-record-by-reference-to-
      csharp-class). The "indirect WxW through deref detected" test
      DIRECTLY assigns `heap.cells[w1].content = Pointer(w2);` —
      codegen MUST emit `heap.Cells[w1].Content = new Pointer(w2);`
      which compiles because `Content` is a writable property.
    idiom_id: null
    research_finding_id: rf-dart-list-indexing-to-csharp-list-indexer
    nuance: >-
      Indexer nuance: Dart `List<T>` index returns by reference for
      reference types (each `Cell` is a reference type per cells.dart.md
      — pinned to a `class`, not a `struct`/`record struct`). C# `List<T>`
      indexer also returns the reference for reference types — the
      mutation `heap.Cells[w1].Content = ...` therefore mutates the
      heap-resident cell, identical to Dart. If `Cell` were ever
      converted as a `struct`, this assignment would silently mutate a
      COPY — pinning Cell as `class` (already done) is load-bearing
      here. Address-width nuance: indices are `long` (matches deconstructed
      `w1`/`writerAddr`).
  - construct_key: dart.enum_member_access.cell_tag_value_or_wrttag
    source_form: "CellTag.ValueTag, CellTag.WrtTag"
    target_decision: >-
      Dart `CellTag.ValueTag` maps to C# `CellTag.ValueTag` (identical
      identifier shape because cells.dart.md construct `dart.enum.plain_
      two_member_marker_tag` idiom rf-dart-plain-enum-to-csharp-enum
      preserves the Dart enum-member casing as-is rather than converting
      to PascalCase). All five uses in this file (`CellTag.ValueTag` ×
      2, `CellTag.WrtTag` × 3) collapse to the same C# enum literal.
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Casing-preservation nuance: the cells.dart precedent pinned
      ValueTag/WrtTag verbatim (PascalCase already by Dart convention).
      Codegen MUST NOT down-case (`Valuetag`) or re-case (`VALUE_TAG`);
      preserve the literal.
  - construct_key: dart.as_cast.type_assertion_on_term_subtype
    source_form: >-
      "(heap.cells[writerAddr].content as ConstTerm).value;
       (value as ConstTerm).value;
       (value as StructTerm).functor;
       (heap.getValue(w1) as StructTerm);
       (value.args[0] as VarRef).addr;
       (heap.cells[writerAddr].content as Pointer).targetAddr;
       (heap.cells[w2].content as WriterContent);
       (result as ConstTerm).value;
       (result as VarRef).addr;
       (heap.derefAddr(r1) as ConstTerm).value;
       (acts1).first.id   // not an as-cast — see throwsStateError construct
       activations.first.id"
    target_decision: >-
      Dart `(<expr> as <T>).<member>` (unchecked downcast) maps to C#
      `((<T>)<expr>).<Member>`. The Dart `as` throws `TypeError` on
      mismatch; C# `(T)expr` throws `InvalidCastException`. Both are
      "expected success" in every test in this file — codegen MUST emit
      the explicit cast (not `expr as T`, which yields `null` on
      mismatch — wrong semantics).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cast-failure-mode nuance (explicitly addressed): Dart `as`
      throws on mismatch; C# `(T)x` ALSO throws (InvalidCastException);
      C# `x as T` returns `null` on mismatch. ALWAYS use the explicit
      cast `(T)x`, NEVER `x as T`, when porting Dart `as`. Member-
      casing nuance: every downcasted member is renamed PascalCase
      (`value` -> `Value`, `functor` -> `Functor`, `args` -> `Args`,
      `addr` -> `Addr`, `targetAddr` -> `TargetAddr`, `length` -> `Count`
      [see list-length below]). Type-pattern modernisation nuance: C# 9+
      `is T t` would be more idiomatic for the cases that follow with
      `Assert.IsType<T>(x)` immediately above — but the file uses
      separate `expect(x, isA<T>())` + `(x as T).f` so the literal
      mapping preserves that two-step shape. Codegen MAY optimise
      adjacent `Assert.IsType<T>(x); ((T)x).F` into `var t =
      Assert.IsType<T>(x); t.F` but is not required.
  - construct_key: dart.member_access.list_length_property
    source_form: "value.args.length"
    target_decision: >-
      Dart `List.length` -> C# `IReadOnlyList<T>.Count`. Per terms.dart.md
      construct `dart.sum_type_leaf.functor_args_list_reference_identity`,
      `Args` is `IReadOnlyList<Term>`. Used once in this file
      (`expect(value.args.length, equals(2));` -> `Assert.Equal(2,
      value.Args.Count);`).
    idiom_id: null
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Property-naming nuance: Dart `.length` is universal across List/
      String/Iterable; C# splits into `Count` (collections) vs `Length`
      (arrays/strings). For `Args` (a list) the correct mapping is
      `Count`. If a future test asserted on a string `.length`, it would
      map to `Length` — the rf records both branches.
  - construct_key: dart.member_access.iterable_first_with_property
    source_form: "activations.first.id, acts2.first.id"
    target_decision: >-
      Dart `Iterable.first` -> C# LINQ `Enumerable.First()`. Used twice:
      `expect(acts2.first.id, equals(10))` -> `Assert.Equal(10,
      acts2.First().Id);`. Requires `using System.Linq;` in addition to
      `using Xunit;` and the runtime namespace.
    idiom_id: null
    research_finding_id: rf-dart-iterable-first-to-csharp-linq-first
    nuance: >-
      Empty-collection nuance: Dart `Iterable.first` throws `StateError`
      on empty; LINQ `First()` throws `InvalidOperationException` —
      observably equivalent for this file (every test that reads
      `.first.id` has just asserted `activations.length == N` with N>=1
      on the same line above). Dart `firstOrNull` would map to LINQ
      `FirstOrDefault()` — not used here.
  - construct_key: dart.iterable.map_with_arrow_to_set
    source_form: "activations.map((a) => a.id).toSet()"
    target_decision: >-
      Dart `Iterable.map((x) => f(x)).toSet()` maps to C# LINQ
      `.Select(a => a.Id).ToHashSet()`. Used once
      (`final ids = activations.map((a) => a.id).toSet();` ->
      `var ids = activations.Select(a => a.Id).ToHashSet();`).
    idiom_id: null
    research_finding_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    nuance: >-
      Set-semantics nuance (explicitly addressed): Dart `Set<T>` is
      hash-based and unordered; C# `HashSet<T>` matches. Equality of
      sets in xUnit `Assert.Equal(setA, setB)` works because
      `HashSet<T>` implements `IEnumerable<T>` and xUnit falls back to
      `SetEquals`-equivalent element-wise comparison (and actually
      `Assert.Equal(IEnumerable, IEnumerable)` does NOT use set
      semantics by default — it compares by sequence). For the file's
      ONE use (`expect(ids, equals({1, 2, 3}));`), the codegen MUST
      emit `Assert.Equal(new HashSet<long> { 1L, 2L, 3L }, ids);` (also
      a HashSet, so element-wise iteration may be order-dependent in
      principle — for sets of integers from the same activation order
      the iteration is implementation-defined). PREFERRED:
      `Assert.True(ids.SetEquals(new[] { 1L, 2L, 3L }));` for order-
      insensitive comparison; recorded as the chosen form in the
      research finding. The Dart `equals(Set)` matcher uses
      `Set.containsAll` + size check (set equality) — `SetEquals`
      preserves those semantics exactly.
  - construct_key: dart.expr.set_literal_of_ints
    source_form: "{1, 2, 3}"
    target_decision: >-
      Dart `{1, 2, 3}` (set literal — context-disambiguated from map
      because elements are bare expressions, not key:value pairs) maps
      to C# `new HashSet<long> { 1L, 2L, 3L }` (long because the IDs
      flow from `SuspensionRecord(1, 100)` etc., and `SuspensionRecord.
      GoalId` is `long?` per suspension.dart.md mapping).
    idiom_id: null
    research_finding_id: rf-dart-set-literal-to-csharp-hashset-collection-init
    nuance: >-
      Map-vs-set disambiguation nuance (explicitly addressed): Dart
      `{}` is ambiguous; `{1,2,3}` is unambiguously a `Set<int>`. C#
      `new HashSet<long> { 1L, 2L, 3L }` uses collection-initialiser
      syntax. An empty literal `{}` in Dart is a `Map<dynamic,dynamic>`
      — not used here.
  - construct_key: dart.package_test.expect_equals
    source_form: >-
      "expect(<actual>, equals(<expected>));   // ~24 calls in this file
       expect(value.functor, equals('point'));
       expect(value.args.length, equals(2));
       expect((value as ConstTerm).value, equals(42));
       ... etc"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)` — ARGUMENT-ORDER
      FLIP. The `equals` matcher uses Dart `==` equality;
      `Assert.Equal` uses `IEquatable<T>.Equals` / `Object.Equals`,
      equivalent for the value-typed comparisons in this file (`int`,
      `double`, `string`, `bool`, `long`-address).
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (explicitly addressed — well-known footgun):
      Dart `expect(actual, equals(expected))` puts actual first; xUnit
      `Assert.Equal(expected, actual)` puts expected first. Codegen MUST
      emit `Assert.Equal(expected, actual)` and the spec records the
      rule. Value-vs-reference nuance: `equals` is applied to `int`
      (e.g. `value.args.length`), `long` (e.g. `(content as Pointer).
      targetAddr` against an address local), `String` (e.g. `functor`,
      `value`), `double` (e.g. `3.14159`), and `bool` (e.g. `true`). For
      `double` 3.14159, exact equality is fine in this file because the
      same literal is on both sides — but in general `Assert.Equal(double,
      double, precision)` is the safe form. The file's exact-literal-
      against-stored-literal pattern stays correct.
  - construct_key: dart.package_test.expect_isA_T
    source_form: >-
      "expect(heap.cells[writerAddr].content, isA<ConstTerm>());
       expect(value, isA<ConstTerm>());
       expect(value, isA<StructTerm>());
       expect(heap.cells[w1].content, isA<Pointer>());
       expect(value.args[0], isA<StructTerm>());
       expect(value.args[0], isA<VarRef>());
       expect(result, isA<ConstTerm>());
       expect(result, isA<VarRef>());
       expect(heap.derefAddr(r1) as ConstTerm, ...);
       expect(heap.cells[w2].content, isA<WriterContent>());
       expect(wc.suspensions, isA<SuspensionListNode>());
       expect(heap.cells[writerAddr].content, isA<Pointer>());
       expect(heap.cells[writerAddr].content, isA<WriterContent>());
       expect(heap.cells[w1].content, isA<Pointer>());
       expect(heap.cells[writerAddr].content, isA<ConstTerm>());"
    target_decision: >-
      Map to xUnit `Assert.IsType<T>(<actual>)`. The `isA<T>()` matcher
      in `package:test` asserts the value IS a T (subtype-tolerant in
      Dart — see exception-test construct below for the analogous
      `isA<T>` in `throwsA`). xUnit `Assert.IsType<T>` asserts EXACT
      type match; `Assert.IsAssignableFrom<T>` asserts subtype.
    idiom_id: null
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-vs-subtype nuance (explicitly addressed): Dart `isA<T>`
      accepts SUBTYPES; xUnit `Assert.IsType<T>` does NOT (it requires
      `actual.GetType() == typeof(T)`); `Assert.IsAssignableFrom<T>`
      does. In THIS file every `isA<T>()` target is a CONCRETE type
      (ConstTerm, StructTerm, VarRef, Pointer, WriterContent,
      SuspensionListNode) — none of which have known subtypes in
      cells.dart.md/terms.dart.md/suspension.dart.md (Pointer is a
      sealed sumtype leaf; ConstTerm/StructTerm/VarRef are sealed Term
      leaves; WriterContent is a plain class; SuspensionListNode is a
      plain wrapper). Codegen SHOULD emit `Assert.IsType<T>` because it
      is observably equivalent and gives a strictly tighter assertion.
      If a future test exercises a subtype, the spec mandates
      `Assert.IsAssignableFrom<T>` — recorded in the rf.
  - construct_key: dart.package_test.expect_isNull
    source_form: >-
      "expect((value as ConstTerm).value, isNull);
       expect(heap.getValue(writerAddr), isNull);"
    target_decision: >-
      Map to xUnit `Assert.Null(<actual>)`. Used twice. Both cases assert
      that a nullable value is null.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNull-to-xunit-assert-null
    nuance: >-
      Nullable-target nuance: Dart `isNull` works on `Object?`; C#
      `Assert.Null` accepts any nullable. Both the `ConstTerm.value`
      (mapped to `object? Value`) and `GetValue` return (`Term?`) are
      nullable in the target, so the assertion is well-typed.
  - construct_key: dart.package_test.expect_isFalse_isTrue
    source_form: >-
      "expect(r1.armed, isFalse);
       expect(heap.isFullyBound(writerAddr), isFalse);
       expect(heap.isFullyBound(writerAddr), isTrue);
       expect(heap.isFullyBound(w1), isFalse);
       expect(heap.isFullyBound(w1), isTrue);"
    target_decision: >-
      Map `isTrue` to `Assert.True(<bool-expr>)` and `isFalse` to
      `Assert.False(<bool-expr>)`. Per construct
      `dart.package_test.expect_isTrue` in boot_loader_test (rf-dart-
      expect-istrue-to-xunit-asserttrue); `isFalse` is the symmetric
      mapping recorded in the same research finding.
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Diagnostic nuance: `Assert.True(b)` / `Assert.False(b)` without
      a message produces a generic failure — adequate for the simple
      predicates here (`r1.Armed`, `heap.IsFullyBound(x)`).
  - construct_key: dart.package_test.expect_isEmpty
    source_form: "expect(acts1, isEmpty);"
    target_decision: >-
      Map to xUnit `Assert.Empty(<actual>)`. Used once: the activation
      list returned by `bindWriterToReader` must be empty
      (suspensions forward without activation).
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Iterable-emptiness nuance: Dart `isEmpty` matches any
      `Iterable`/`String`/`Map` with `.isEmpty == true`; C# `Assert.
      Empty` accepts any `IEnumerable`. `acts1` is `List<SuspensionRecord>`
      — both `Assert.Empty(acts1)` and `Assert.Equal(0, acts1.Count)`
      work; the dedicated assertion gives a clearer diagnostic.
  - construct_key: dart.package_test.expect_throwsStateError
    source_form: >-
      "expect(() => heap.bindWriterToWriter(w1, w2), throwsStateError);
       expect(() => heap.derefAddr(w1), throwsStateError);"
    target_decision: >-
      Dart `throwsStateError` is a constant matcher that asserts the
      thrown exception is a `StateError`. The C# equivalent per
      heap_fcp.dart.md construct `dart.method.bind_writer_to_writer_
      explicit_wxw_violation_throw` and construct
      `dart.deref_addr.large_switch_...` (idiom rf-dart-staterror-to-
      csharp-invalidoperationexception) is `InvalidOperationException`.
      Map to xUnit `Assert.Throws<InvalidOperationException>(() =>
      heap.BindWriterToWriter(w1, w2));` (no follow-on Message assertion
      because the test does not introspect the message text).
    idiom_id: null
    research_finding_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    nuance: >-
      Exception-mapping nuance (explicitly addressed): Dart `StateError`
      is the documented exception for "operation invalid given current
      state" (see Dart core lib `StateError` doc); the corresponding
      .NET type is `System.InvalidOperationException` (BCL doc:
      "thrown when a method call is invalid for the object's current
      state"). The mapping is canonical and reused from heap_fcp.dart.md.
      Exact-vs-subtype nuance: `throwsStateError` matches `StateError`
      AND its subtypes; `Assert.Throws<InvalidOperationException>`
      matches only the exact type; `Assert.ThrowsAny<InvalidOperationException>`
      matches subtypes. `InvalidOperationException` has stdlib
      subtypes (e.g. `ObjectDisposedException`) but the heap throws
      `InvalidOperationException` directly per heap_fcp.dart.md; therefore
      `Assert.Throws<T>` is observably equivalent. Lambda nuance: Dart
      `() => heap.bindWriterToWriter(w1, w2)` maps to C# `() => heap.
      BindWriterToWriter(w1, w2)` (identical arrow syntax). The
      xUnit assertion synchronously invokes the lambda and catches the
      throw.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System + System.Linq + the SUT runtime namespace)
  - cu-2: namespace declaration mirroring the test/heap path (e.g. `<RootNs>.Test.Heap`)
  - cu-3: top-level test class `BindingPointerTests` (NO inner classes — six flat groups become `[Trait]` partitions on methods)
  - cu-4: NO constructor (no `late` / no `setUp` — every test allocates its own `var heap = new HeapFcp();`)
  - cu-5: 7 `[Fact]` methods in the "bindWriter - Ground Values" group (BindWriterGroundValues_BindToConstTermInteger / String / Double / Null / Boolean / StructTerm / NestedStructTerm / StructTermContainingVarRef), each `[Trait("Group", "bindWriter - Ground Values")]`, each with `[Fact(DisplayName = "<original label>")]`
  - cu-6: 4 `[Fact]` methods in the "bindWriterToReader - Variable Chains" group (basic-binding-creates-pointer / chain-of-bindings / long-chain-dereferences-correctly / unbound-chain-returns-final-writer-VarRef), each `[Trait("Group", "bindWriterToReader - Variable Chains")]`, including the second-heap (`var heap2 = new HeapFcp();`) in the last test
  - cu-7: 2 `[Fact]` methods in the "WxW Violation Detection" group (bindWriterToWriter-throws-StateError / indirect-WxW-through-deref-detected), each `[Trait("Group", "WxW Violation Detection")]`, each using `Assert.Throws<InvalidOperationException>`
  - cu-8: 3 `[Fact]` methods in the "Binding with Suspensions" group (binding-ground-value-activates-all-suspensions / binding-to-variable-forwards-suspensions-without-activation / disarmed-suspensions-not-activated), each `[Trait("Group", "Binding with Suspensions")]`
  - cu-9: 4 `[Fact]` methods in the "Binding State Transitions" group (unbound-writer-has-Pointer-to-reader / writer-with-suspension-has-WriterContent / writer-bound-to-variable-has-Pointer-content / writer-bound-to-ground-has-ValueTag-and-Term-content), each `[Trait("Group", "Binding State Transitions")]`
  - cu-10: 7 `[Fact]` methods in the "isFullyBound and getValue" group (isFullyBound-false-for-unbound / isFullyBound-true-for-bound-to-ground / isFullyBound-follows-chain / getValue-returns-null-for-unbound / getValue-returns-term-for-bound / getValue-follows-chain), each `[Trait("Group", "isFullyBound and getValue")]`
  - cu-11: in every method the per-test arena `var heap = new HeapFcp();` (plus `var heap2 = new HeapFcp();` where present) and the deconstruction locals `var (writerAddr, _) = heap.AllocateVariable();` (or symmetric variants) preserved verbatim
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

Third `package:test` file specced; xUnit is project-pinned. The
authoritative basis is unchanged: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `test` / `expect` /
matcher semantics. This file's `[Trait]` decision continues the
flatten-with-traits pattern established in boot_loader_test.dart.md.
Reused via `rf-dart-package-test-import-to-xunit-using` — no
re-research (FR-024 cache hit; SC-007 reuse).

### Flat groups, no nesting, no shared setUp

Unlike boot_loader_test (1 outer + 3 inner + `late BootLoader loader`),
this file has 6 SIBLING groups and NO `late` field — every test does
`final heap = HeapFCP()` inline. The conversion is therefore SIMPLER
than boot_loader's: a single `BindingPointerTests` class, NO constructor,
NO `_loader = null!`-style field, and method-local `var heap = new
HeapFcp();`. `[Trait("Group", "...")]` per method preserves the six-
way partition for VS Test Explorer / `dotnet test --logger trx` /
Rider grouping (xUnit docs:
`https://xunit.net/docs/comparisons#categories`). The labels contain
spaces, hyphens, and the unusual "WxW" acronym — all preserved
verbatim in `DisplayName`, sanitised to PascalCase in method names.

### Record-destructuring `final (writerAddr, _) = heap.allocateVariable();`

Dart 3 records map cleanly to C# tuple deconstruction
(`https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records`
plus tuple-deconstruction reference
`https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/deconstruct`).
The `_` discard works identically in both languages. Width nuance is
load-bearing: `allocateVariable` returns `(long, long)` per
heap_fcp.dart.md (idiom rf-dart-record-return-to-csharp-valuetuple),
NOT `(int, int)` — every deconstructed local in this file is `long`.
Codegen MUST NOT silently narrow to `int`; address arithmetic
elsewhere in `HeapFcp` depends on the 64-bit width (cells.dart.md
construct `dart.int.fixed_width_identity_field`, idiom rf-dart-int-
to-csharp-long-width).

### Constructor calls reuse the lib/runtime spec decisions

Every constructor call in this file targets a Term-family type whose
shape was pinned by lib/runtime/terms.dart.md or lib/runtime/cells.
dart.md or lib/runtime/suspension.dart.md. No new research:
`ConstTerm(<lit>)` -> `new ConstTerm(<lit>)` (rf-dart-sumleaf-no-eq-
to-csharp-class-no-record), `StructTerm(<func>, [<terms>])` -> `new
StructTerm(<func>, new List<Term> { ... })` (rf-dart-list-literal-to-
csharp-list-of-T and rf-dart-sumleaf-with-list-no-eq-to-csharp-class-
ireadonlylist), `VarRef(<addr>)` -> `new VarRef(<addr>)` (rf-dart-
class-eq-on-single-int-field-to-csharp-iequatable), `Pointer(<addr>)`
-> `new Pointer(<addr>)` (rf-dart-sumleaf-no-eq-to-csharp-class-no-
record), `SuspensionRecord(<goalId>, <resumePc>)` -> `new
SuspensionRecord(<goalId>, <resumePc>)` (rf-dart-shared-mutable-record-
by-reference-to-csharp-class — reference identity preserved because
`Disarm()` mutates observable state).

### `Assert.Equal` argument-order flip

Dart `expect(actual, equals(expected))` puts actual first; xUnit
`Assert.Equal(expected, actual)` puts expected first. This file has
~24 `equals(...)` calls — every one MUST be flipped at the boundary.
xUnit docs (`https://xunit.net/docs/comparisons#assertions`) document
the order explicitly. Reused verbatim from boot_loader_test convspec.

### `isA<T>` -> `Assert.IsType<T>` (exact match)

Dart `isA<T>` matcher accepts subtypes; xUnit `Assert.IsType<T>` requires
EXACT type. xUnit docs
(`https://xunit.net/docs/comparisons#assertions`,
`https://learn.microsoft.com/dotnet/api/xunit.assert.istype`) document
`Assert.IsAssignableFrom<T>` as the subtype-tolerant variant. In THIS
file every `isA<T>` target is a sealed leaf with no subtypes
(ConstTerm, StructTerm, VarRef, Pointer, WriterContent,
SuspensionListNode), so `Assert.IsType<T>` is observably equivalent
AND strictly tighter. The exception case (`throwsStateError`) chooses
`Assert.Throws<T>` for the same reason: heap_fcp.dart throws
`StateError` directly, no subtype involvement.

### `throwsStateError` -> `Assert.Throws<InvalidOperationException>`

Dart core lib `StateError` doc
(`https://api.dart.dev/stable/dart-core/StateError-class.html`) and
.NET `InvalidOperationException` doc
(`https://learn.microsoft.com/dotnet/api/system.invalidoperationexception`)
describe semantically equivalent "method invalid given current state"
contracts. Mapping is canonical and was pinned by heap_fcp.dart.md
(construct `dart.method.bind_writer_to_writer_explicit_wxw_violation_
throw`, idiom rf-dart-staterror-to-csharp-invalidoperationexception)
and `dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_
violation_and_three_tag_cases` (same idiom). No re-derivation.

### `.map((a) => a.id).toSet()` + `equals({1,2,3})` -> LINQ + `SetEquals`

Dart `Iterable.map(...).toSet()` + `equals(Set)` matcher is the
order-insensitive set-equality assertion (Dart `Set.containsAll` +
size check). The xUnit equivalent that PRESERVES set semantics is
`Assert.True(ids.SetEquals(new[] { 1L, 2L, 3L }));` — `Assert.Equal`
on two `HashSet<T>` actually does sequence-equality (NOT set
equality) because xUnit treats `IEnumerable` element-wise. Reference:
`System.Linq.Enumerable` docs
(`https://learn.microsoft.com/dotnet/api/system.linq.enumerable`) for
`Select` / `ToHashSet`, `HashSet<T>.SetEquals` docs
(`https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1.setequals`)
for the set-equality assertion shape. Recorded under
`rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset` for
reuse in any later file that does the same `map.toSet()` + set-
equality pattern.

### Direct cell-content mutation (WxW corruption test)

The "indirect WxW through deref detected" test does
`heap.cells[w1].content = Pointer(w2);` — DIRECTLY writes a field on
the heap-resident cell to simulate a corrupt heap state. This works
because cells.dart.md pinned `Cell` as a CLASS (reference type) with
a writable `Content` property (idiom rf-dart-shared-mutable-record-
by-reference-to-csharp-class). Codegen MUST NOT refactor this to a
setter method or to a read-only property — the test's behaviour
depends on the documented `Content` writability. If `Cell` were
silently converted to a `record struct`, the assignment would mutate
a COPY and the WxW corruption would never reach the deref — pinning
`Cell` as `class` is load-bearing. This convspec records the test's
dependency on the precedent decision; if cells.dart.md ever changes
to make `Content` read-only, this test must be rewritten via a
test-only mutator or marked as a regression in cells.dart.md's spec
(escalation surface preserved).

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official Dart / .NET documentation, and EVERY non-trivial construct
reuses an idiom_id-equivalent rf-* already recorded in a precedent
spec (`heap_fcp.dart.md`, `cells.dart.md`, `terms.dart.md`,
`suspension.dart.md`, `boot_loader_test.dart.md`, `smoke_test.dart.md`,
`localize_test.dart.md`). The two file-local nuance calls — `Assert.
IsType<T>` over `Assert.IsAssignableFrom<T>` (justified by sealed
leaves), and `SetEquals` over `Assert.Equal` on two HashSets (justified
by Dart `Set` equality semantics) — are deliberate, in-file-justified
choices with corroborating alternatives recorded in their research
findings, not undecidable points. `escalations: []` is therefore
intentional.
