---
path: test/circular_term_test.dart
cycle_group_id: 115
scc_siblings: []
generated_at: 2026-05-21T16:30:04Z
source_sha256: 13325b134ab40b28f0b298af90405dcdd2f608c084cecd20446828be4f7b8db2
schema_version: 1
---

# Conversion Plan: test/circular_term_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/circular_term_test.dart` (198 lines, sha256 `13325b134ab40b28f0b298af90405dcdd2f608c084cecd20446828be4f7b8db2`):

- **File-level dartdoc** (lines 1–8): three-paragraph `///` block stating the purpose — circular terms can form through cross-goal communication when two goals share variables and bind them in ways that create cycles; tests verify ground/1 guard termination, =?= equality termination, and copy_term/2 cyclic-structure preservation. NO `library;` directive.
- **Imports** (lines 10–14): four `import` directives — `package:test/test.dart`, `package:glp_runtime/runtime/runtime.dart`, `package:glp_runtime/runtime/terms.dart`, `package:glp_runtime/runtime/system_predicates.dart`, `package:glp_runtime/runtime/system_predicates_impl.dart`.
- **`main()` entrypoint** (line 16): synchronous `void main()` containing a single outer `group('Circular Term Handling', ...)`.
- **Outer group structure** (lines 17–197): `late GlpRuntime rt;` field (line 18) + `setUp(() { rt = GlpRuntime(); });` (lines 20–22) + FOUR sibling inner groups:
  - `group('Ground Guard with Circular Terms', ...)` — 2 tests (lines 25–47, 49–66).
  - `group('Equality (=?=) with Circular Terms', ...)` — 2 tests (lines 70–92, 94–110).
  - `group('Deep Copy with Circular Terms', ...)` — 2 tests (lines 114–149, 151–178).
  - `group('Term Formatter with Circular Terms', ...)` — 1 test (lines 182–195).
- **Per-test arrange/act/assert**:
  - 4 record-destructuring sites on `rt.heap.allocateVariable()` (lines 28, 51, 52, 72, 73, 96, 97, 116, 124, 153, 158, 184) — three bind both slots `(W, R)`, one discards with `(W, _)`.
  - 6 `StructTerm(<functor>, <args-list>)` constructions across tests (lines 31, 55, 75, 76, 99, 100, 117–120, 154, 185).
  - 2 `ConstTerm('a')` and 1 `ConstTerm('b')` constructions (lines 118, 148, 154).
  - 7 `VarRef(<addr>)` constructions covering reader and writer addresses (lines 31, 56, 57, 75, 76, 99, 100, 119, 129, 162, 163, 185).
  - 6 `rt.heap.bindVariable(<writer>, <struct>)` mutator calls (lines 34, 61, 78, 79, 102, 103, 121, 155, 186).
  - 1 `rt.heap.isWriterBound(yWriter)` query (line 65) + 1 `rt.heap.isWriterBound(copyWriter)` query (line 139).
  - 7 `rt.heap.getValue(<writer>)` queries (lines 38, 84, 85, 105, 106, 142, 171, 189).
  - 2 `SystemCall('copy_term', [...])` constructions (lines 127–130, 161–164).
  - 2 `copyTermPredicate(rt, call)` top-level function calls (lines 133, 167).
  - 2 `SystemResult.success` enum member accesses (lines 136, 168).
  - Multiple `as StructTerm` / `as ConstTerm` downcasts (lines 43, 91, 105, 106, 144, 175, 148).
  - 1 `value.toString()` invocation wrapped in `expect(() => …, returnsNormally)` (line 194).
- **Matcher inventory**: 9 `equals(...)`, 8 `isA<T>()`, 1 `isFalse`, 1 `isTrue`, 1 `returnsNormally`, multiple `isNot(equals(...))`.
- **Async surface**: zero — every test closure is synchronous.

## 2. Dart → C#/.NET Conversion Plan

### 2.1 File-level doc-comment → xUnit class XML `<summary>`

`dart.file_level_doc_comment.multi_paragraph_top_of_file_no_library_directive` (convspec): the `///` block lifts to the XML `<summary>` doc-comment on the enclosing xUnit class `CircularTermTests`. Each `///` line preserved verbatim; the three-dash bullet list reflowed as `<list type="bullet"><item>…</item></list>`. Research finding `rf-dart-library-directive-to-csharp-namespace-elision`.

### 2.2 `import 'package:test/test.dart';` → `using Xunit;`

`dart.package_test.import_directive`. Project-wide xUnit pinning (matches `binding_pointer_test`, `boot_loader_test`, `mad_error_handling_test`, `smoke_test`). Research finding `rf-dart-package-test-import-to-xunit-using` (FR-024 cache hit).

### 2.3 Four `package:glp_runtime/runtime/...` imports → single `using <RootNs>.Runtime;`

`dart.package_under_test.import_directive`. C# `using` is per-namespace, not per-file — the four runtime sub-imports collapse to one directive. OPTIONAL file-scope `using static <RootNs>.Runtime.SystemPredicatesImpl;` so the bare callsite `CopyTermPredicate(_rt, call)` compiles (2+ callsites in this file → recommended). Research finding `rf-dart-internal-package-import-to-csharp-using`.

### 2.4 `void main()` → eliminated

`dart.package_test.main_entrypoint`. xUnit discovers `[Fact]` methods by reflection; no per-file entrypoint. Research finding `rf-dart-package-test-main-omit-in-xunit`.

### 2.5 Outer + 4-inner group block → ONE xUnit class with 4 traits

`dart.package_test.group_block_nested_with_setUp`. Map to class `CircularTermTests` (PascalCase + `Tests` suffix). Inner-group labels become `[Trait("Group", "<inner label>")]` on each `[Fact]`. Original Dart test labels preserved via `[Fact(DisplayName = "<original label>")]`. Boot-loader-test-shape (NOT binding-pointer-test-shape). Research finding `rf-dart-package-test-group-to-xunit-class`.

### 2.6 `late GlpRuntime rt;` → `private GlpRuntime _rt = null!;`

`dart.late_field.glpruntime_per_test_runtime_arena`. Underscore-prefixed lowerCamelCase per .NET Framework Design Guidelines. `null!` valid because the constructor unconditionally assigns. Research finding `rf-dart-late-field-to-csharp-null-bang-field`.

### 2.7 `setUp(() { rt = GlpRuntime(); });` → constructor body

`dart.package_test.setUp_block`. xUnit instantiates the test class ONCE PER TEST METHOD → constructor body is semantically equivalent to per-test `setUp`. `_rt = new GlpRuntime();` in the constructor (all named-optional params defaulted per runtime.dart.md). Research finding `rf-dart-package-test-setUp-to-xunit-constructor`.

### 2.8 Each `test(label, () { ... })` → `[Fact]` instance method

`dart.package_test.test_call_simple`. `public void` instance methods decorated with `[Fact(DisplayName = "<original label>")]` + `[Trait("Group", "<enclosing inner-group label>")]`. Method name = inner-group-prefixed PascalCased label. All 7 tests synchronous → no `async Task`. Research finding `rf-dart-test-callback-to-xunit-method-body`.

### 2.9 `final <T> x = <expr>;` → `var x = <expr>;`

`dart.local_var.final_constructor_instance`. Single-assignment shape preserved by the converted body. Research finding `rf-dart-final-local-to-csharp-var-local`.

### 2.10 Record-destructuring `(W, R) = rt.heap.allocateVariable()` → C# tuple-deconstruction

`dart.local_var.record_destructuring_two_ints_or_ignored`. `var (varWriter, varReader) = _rt.Heap.AllocateVariable();` (et al.); `var (copyWriter, _) = _rt.Heap.AllocateVariable();` for the discard. Element type `long` (NOT `int`) per cells.dart.md address-width nuance. Research finding `rf-dart-record-return-to-csharp-valuetuple`.

### 2.11 `ConstTerm('a')` / `ConstTerm('b')` → `new ConstTerm("a")` / `new ConstTerm("b")`

`dart.constructor_call.const_term_with_value`. Sealed class with single nullable `object? Value`; literals box transparently. Research finding `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`.

### 2.12 `StructTerm(<functor>, <list>)` → `new StructTerm("<functor>", new List<Term> { ... })`

`dart.constructor_call.struct_term_with_functor_and_args_list`. `Args` is `IReadOnlyList<Term>`; `List<Term>` is assignable. Cyclic-reference nuance: cycles live in the HEAP CELL TABLE (via `VarRef.Addr`), NOT in the C# object graph — no GC risk. Research finding `rf-dart-list-literal-to-csharp-list-of-T`.

### 2.13 `VarRef(<addr>)` → `new VarRef(<addr>)`

`dart.constructor_call.var_ref_single_int_addr`. Sealed class with single `long Addr`, `IEquatable<VarRef>`. Address-agnostic (reader-vs-writer enforced by the heap, not the type). Research finding `rf-dart-class-eq-on-single-int-field-to-csharp-iequatable`.

### 2.14 `SystemCall('copy_term', [...])` → `new SystemCall("copy_term", new List<object?> { ... })`

`dart.constructor_call.system_call_name_and_args_list`. Reference type (NOT record / struct) — mutation surface `SuspendedReaders` requires reference identity. List element type widens from `VarRef` to `object?` at construction (C# `List<T>` is invariant — codegen MUST construct `List<object?>` directly). Research finding `rf-dart-mutable-callcontext-class-to-csharp-reference-class`.

### 2.15 `rt.heap.allocateVariable()` → `_rt.Heap.AllocateVariable()`

`dart.method_call.heap_allocateVariable`. `Heap` is a get-only auto-property on `GlpRuntime`; method returns `ValueTuple<long,long>`. Research finding `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`.

### 2.16 `rt.heap.bindVariable(<W>, <term>)` → `_rt.Heap.BindVariable(<W>, <term>)`

`dart.method_call.heap_bindVariable_mutator`. PascalCase method on the converted `HeapFcp` type. Return value (`List<SuspensionRecord>`) DISCARDED at every callsite. `BindVariable` (legacy public surface) IS retained per heap_fcp.dart.md (alongside `BindWriter`). Research finding `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`.

### 2.17 `rt.heap.isWriterBound(<W>)` → `_rt.Heap.IsWriterBound(<W>)`

`dart.method_call.heap_isWriterBound_query`. Returns non-nullable `bool` (single-step cell-tag check; distinct from transitive `IsFullyBound`). Research finding `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`.

### 2.18 `rt.heap.getValue(<W>)` → `_rt.Heap.GetValue(<W>)`

`dart.method_call.heap_getValue_query_nullable`. Returns `Term?`. Every callsite in this file is preceded by `BindVariable` on the same writer → non-null at the call. Recommended: bare `var value = _rt.Heap.GetValue(...);` (no `!`), with the subsequent `Assert.IsType<StructTerm>(value)` acting as the null check. Research finding `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`.

### 2.19 `copyTermPredicate(rt, call)` → `CopyTermPredicate(_rt, call)` (or `SystemPredicatesImpl.CopyTermPredicate(...)`)

`dart.toplevel_function_call.copyTermPredicate_two_arg`. C# has no top-level functions — Dart top-level function lifted to a `public static` method on `SystemPredicatesImpl`. File-scope `using static <RootNs>.Runtime.SystemPredicatesImpl;` (2 callsites → emit it). Research finding `rf-dart-toplevel-function-to-csharp-static-method-on-class`.

### 2.20 `SystemResult.success` → `SystemResult.Success`

`dart.enum_member_access.system_result_success`. PascalCased member per system_predicates.dart.md (`success`/`failure`/`suspend` → `Success`/`Failure`/`Suspend`). Distinct from CellTag (which preserves PascalCase verbatim). Research finding `rf-dart-three-member-enum-pascalcased-to-csharp-enum`.

### 2.21 `.functor` / `.args` / `.args.length` / `.args[N]` / `.value` → PascalCase properties + `.Count`

`dart.member_access.struct_term_functor_args`. `Args` is `IReadOnlyList<Term>` → `Count` (NOT `Length`). Indexer `Args[N]`. Research finding `rf-dart-list-length-to-csharp-list-count`.

### 2.22 `(<expr> as <T>).<member>` → `((<T>)<expr>).<Member>`

`dart.as_cast.type_assertion_on_term_subtype`. Explicit cast (NOT `expr as T` — that returns `null` on mismatch, wrong semantics). Both Dart `as` and C# `(T)x` throw on mismatch (Dart `TypeError`, C# `InvalidCastException`). Member casing PascalCased. Research finding `rf-dart-as-cast-to-csharp-explicit-cast`.

### 2.23 `value.toString()` → `value.ToString()`

`dart.method_call.term_tostring`. `Object.ToString()` override on every Term leaf. Load-bearing for the cycle-termination test: `VarRef.ToString()` formats by ADDRESS (returns `"V" + Addr`), NOT by dereferenced value — this single rule breaks any heap-level cycle at the formatting layer. Codegen MUST NOT add cycle-detecting `HashSet<object>` to StructTerm.ToString. Research finding `rf-dart-object-tostring-to-csharp-object-tostring`.

### 2.24 `expect(<actual>, equals(<expected>))` → `Assert.Equal(<expected>, <actual>)` (ARG-ORDER FLIP)

`dart.package_test.expect_equals`. 9 callsites in this file — every one MUST be flipped at the boundary. Research finding `rf-dart-expect-equals-to-xunit-assertequal`.

### 2.25 `expect(<actual>, isA<T>())` → `Assert.IsType<T>(<actual>)`

`dart.package_test.expect_isA_T`. Exact-type match (sealed Term leaves have no known subtypes per terms.dart.md, so `IsType<T>` is observably equivalent and strictly tighter than `IsAssignableFrom<T>`). 8 callsites. Research finding `rf-dart-expect-isA-to-xunit-assert-istype`.

### 2.26 `expect(<bool>, isFalse|isTrue)` → `Assert.False(<bool>)` / `Assert.True(<bool>)`

`dart.package_test.expect_isFalse_isTrue`. 2 callsites (line 65 `isFalse`, line 139 `isTrue`). Research finding `rf-dart-expect-istrue-to-xunit-asserttrue`.

### 2.27 `expect(() => <fn>, returnsNormally)` → bare `<Fn>;` call

`dart.package_test.expect_function_returnsNormally`. xUnit's pass condition is "no unhandled exception" — bare-call is the canonical idiom for positive-no-throw matchers. Codegen emits `value.ToString();` (line 194); the `[Fact]` body fails if it throws. Research finding `rf-dart-expect-returns-normally-to-xunit-bare-call`.

## 3. Decomposed Task Units

- T1: emit file-scope `using` directives — `using Xunit;` + the runtime namespace `using <RootNs>.Runtime;` + (recommended) `using static <RootNs>.Runtime.SystemPredicatesImpl;`. — done
- T2: emit `namespace <RootNs>.Test` mirroring the Dart `test/` directory. — done
- T3: emit class `CircularTermTests` carrying the file-level XML `<summary>` doc-comment (three-bullet `<list type="bullet">` rationale). — done
- T4: emit `private GlpRuntime _rt = null!;` instance field. — done
- T5: emit constructor `CircularTermTests()` with body `_rt = new GlpRuntime();`. — done
- T6: emit `[Fact]` `GroundGuardWithCircularTerms_CircularTermWithoutUnboundVariablesIsGround` — `[Trait("Group", "Ground Guard with Circular Terms")]` + `[Fact(DisplayName = "circular term without unbound variables is ground")]`; body = tuple-deconstruction `(varWriter, varReader)`, `new StructTerm("f", new List<Term> { new VarRef(varReader) })`, `BindVariable`, `GetValue`, `Assert.IsType<StructTerm>(value)`, cast + `Assert.Equal("f", struct.Functor)`, `Assert.Equal(1, struct.Args.Count)`, `Assert.IsType<VarRef>(struct.Args[0])`. — done
- T7: emit `[Fact]` `GroundGuardWithCircularTerms_CircularTermWithUnboundVariableInsideIsNotGround` — `(xWriter, xReader)` + `(yWriter, yReader)`, build `new StructTerm("f", new List<Term> { new VarRef(yReader), new VarRef(xReader) })`, `BindVariable(xWriter, …)`, `Assert.False(_rt.Heap.IsWriterBound(yWriter))`. — done
- T8: emit `[Fact]` `EqualityWithCircularTerms_IdenticalCircularTermsAreEqual` — two writer/reader pairs, two `StructTerm("f", …)` constructions, two `BindVariable`s, two `GetValue` captures, `Assert.IsType<StructTerm>` on each, `Assert.Equal(((StructTerm)xValue).Functor, ((StructTerm)yValue).Functor)`. `[Trait("Group", "Equality (=?=) with Circular Terms")]`. — done
- T9: emit `[Fact]` `EqualityWithCircularTerms_DifferentCircularTermsAreNotEqual` — two pairs, `StructTerm("f", …)` and `StructTerm("g", …)`, two binds, two `GetValue`+cast, `Assert.NotEqual(((StructTerm)xValue).Functor, ((StructTerm)yValue).Functor)` (or equivalent — convspec records `isNot(equals(...))` mapping; see §5 derivation from `dart.package_test.expect_equals` + Dart `isNot` semantics → `Assert.NotEqual` with arg-order flip). — done
- T10: emit `[Fact]` `DeepCopyWithCircularTerms_CopyOfCircularTermPreservesStructure` — `(xWriter, xReader)`, `new StructTerm("f", new List<Term> { new ConstTerm("a"), new VarRef(xReader) })`, `BindVariable`, `(copyWriter, _)`, `new SystemCall("copy_term", new List<object?> { new VarRef(xReader), new VarRef(copyWriter) })`, `var result = CopyTermPredicate(_rt, call);`, `Assert.Equal(SystemResult.Success, result)`, `Assert.True(_rt.Heap.IsWriterBound(copyWriter))`, `var copyValue = _rt.Heap.GetValue(copyWriter);`, `Assert.IsType<StructTerm>(copyValue)`, cast → `Assert.Equal("f", copyStruct.Functor)`, `Assert.Equal(2, copyStruct.Args.Count)`, `Assert.IsType<ConstTerm>(copyStruct.Args[0])`, `Assert.Equal("a", ((ConstTerm)copyStruct.Args[0]).Value)`. `[Trait("Group", "Deep Copy with Circular Terms")]`. — done
- T11: emit `[Fact]` `DeepCopyWithCircularTerms_CopyOfAcyclicTermCreatesIndependentCopy` — `(xWriter, xReader)`, `new StructTerm("f", new List<Term> { new ConstTerm("a"), new ConstTerm("b") })`, `BindVariable`, `(copyWriter, _)`, `new SystemCall("copy_term", new List<object?> { new VarRef(xReader), new VarRef(copyWriter) })`, `CopyTermPredicate(_rt, call)`, `Assert.Equal(SystemResult.Success, result)`, `GetValue`, `Assert.IsType<StructTerm>`, cast, `Assert.Equal("f", copyStruct.Functor)`, `Assert.Equal(2, copyStruct.Args.Count)`. — done
- T12: emit `[Fact]` `TermFormatterWithCircularTerms_CircularTermDoesNotCauseInfiniteLoopInToString` — `(xWriter, xReader)`, `new StructTerm("f", new List<Term> { new VarRef(xReader) })`, `BindVariable`, `var value = _rt.Heap.GetValue(xWriter);`, `Assert.IsType<StructTerm>(value)`, bare `value.ToString();` (no Assert wrapper — xUnit's no-throw is the assertion). `[Trait("Group", "Term Formatter with Circular Terms")]` + `[Fact(DisplayName = "circular term does not cause infinite loop in toString")]`. — done

## 4. Research Findings

none required — every construct reuses an `rf-*` research finding already pinned by a precedent convspec (terms.dart.md, heap_fcp.dart.md, cells.dart.md, runtime.dart.md, system_predicates.dart.md, system_predicates_impl.dart.md, binding_pointer_test.dart.md, boot_loader_test.dart.md, partial_evaluator_test.dart.md, smoke_test.dart.md). Convspec §"Rationale + research provenance" enumerates them; this plan inherits the cache hits verbatim under FR-024.

## 5. Consistency Pass

- §2.1 file-doc → class XML `<summary>` — fixed — derived from convspec construct `dart.file_level_doc_comment.multi_paragraph_top_of_file_no_library_directive` (rf-dart-library-directive-to-csharp-namespace-elision).
- §2.2 `package:test` → `using Xunit;` — fixed — derived from convspec construct `dart.package_test.import_directive` (rf-dart-package-test-import-to-xunit-using).
- §2.3 four runtime imports → single `using <RootNs>.Runtime;` + optional `using static SystemPredicatesImpl;` — fixed — derived from convspec construct `dart.package_under_test.import_directive` (rf-dart-internal-package-import-to-csharp-using).
- §2.4 `main()` elided — fixed — derived from convspec construct `dart.package_test.main_entrypoint` (rf-dart-package-test-main-omit-in-xunit).
- §2.5 outer + 4-inner group → one class with traits — fixed — derived from convspec construct `dart.package_test.group_block_nested_with_setUp` (rf-dart-package-test-group-to-xunit-class).
- §2.6 `late GlpRuntime rt;` → `private GlpRuntime _rt = null!;` — fixed — derived from convspec construct `dart.late_field.glpruntime_per_test_runtime_arena` (rf-dart-late-field-to-csharp-null-bang-field).
- §2.7 `setUp` → constructor — fixed — derived from convspec construct `dart.package_test.setUp_block` (rf-dart-package-test-setUp-to-xunit-constructor).
- §2.8 each `test(...)` → `[Fact]` method — fixed — derived from convspec construct `dart.package_test.test_call_simple` (rf-dart-test-callback-to-xunit-method-body).
- §2.9 `final` locals → `var` — fixed — derived from convspec construct `dart.local_var.final_constructor_instance` (rf-dart-final-local-to-csharp-var-local).
- §2.10 record-destructuring → tuple-deconstruction (long, NOT int) — fixed — derived from convspec construct `dart.local_var.record_destructuring_two_ints_or_ignored` (rf-dart-record-return-to-csharp-valuetuple) + cells.dart.md width nuance.
- §2.11 `ConstTerm(<lit>)` → `new ConstTerm(<lit>)` — fixed — derived from convspec construct `dart.constructor_call.const_term_with_value` (rf-dart-sumleaf-no-eq-to-csharp-class-no-record).
- §2.12 `StructTerm(<f>, [<args>])` → `new StructTerm(<f>, new List<Term> { ... })` — fixed — derived from convspec construct `dart.constructor_call.struct_term_with_functor_and_args_list` (rf-dart-list-literal-to-csharp-list-of-T).
- §2.13 `VarRef(<addr>)` → `new VarRef(<addr>)` — fixed — derived from convspec construct `dart.constructor_call.var_ref_single_int_addr` (rf-dart-class-eq-on-single-int-field-to-csharp-iequatable).
- §2.14 `SystemCall(<n>, [...])` → `new SystemCall(<n>, new List<object?> { ... })` — fixed — derived from convspec construct `dart.constructor_call.system_call_name_and_args_list` (rf-dart-mutable-callcontext-class-to-csharp-reference-class).
- §2.15 `rt.heap.allocateVariable()` → `_rt.Heap.AllocateVariable()` — fixed — derived from convspec construct `dart.method_call.heap_allocateVariable` (rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods).
- §2.16 `rt.heap.bindVariable(...)` → `_rt.Heap.BindVariable(...)` — fixed — derived from convspec construct `dart.method_call.heap_bindVariable_mutator` (rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods).
- §2.17 `rt.heap.isWriterBound(...)` → `_rt.Heap.IsWriterBound(...)` — fixed — derived from convspec construct `dart.method_call.heap_isWriterBound_query` (rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods).
- §2.18 `rt.heap.getValue(...)` → `_rt.Heap.GetValue(...)` (nullable Term?) — fixed — derived from convspec construct `dart.method_call.heap_getValue_query_nullable` (rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods).
- §2.19 `copyTermPredicate(...)` → `CopyTermPredicate(_rt, call)` (with `using static`) — fixed — derived from convspec construct `dart.toplevel_function_call.copyTermPredicate_two_arg` (rf-dart-toplevel-function-to-csharp-static-method-on-class).
- §2.20 `SystemResult.success` → `SystemResult.Success` — fixed — derived from convspec construct `dart.enum_member_access.system_result_success` (rf-dart-three-member-enum-pascalcased-to-csharp-enum).
- §2.21 `.functor`/`.args.length`/`.args[N]`/`.value` → `.Functor`/`.Args.Count`/`.Args[N]`/`.Value` — fixed — derived from convspec construct `dart.member_access.struct_term_functor_args` (rf-dart-list-length-to-csharp-list-count).
- §2.22 `(x as T).m` → `((T)x).M` (explicit cast, NOT `as`) — fixed — derived from convspec construct `dart.as_cast.type_assertion_on_term_subtype` (rf-dart-as-cast-to-csharp-explicit-cast).
- §2.23 `value.toString()` → `value.ToString()` (cycle terminated by VarRef.ToString's address-only format) — fixed — derived from convspec construct `dart.method_call.term_tostring` (rf-dart-object-tostring-to-csharp-object-tostring) + terms.dart.md VarRef ToString override.
- §2.24 `expect(actual, equals(expected))` → `Assert.Equal(expected, actual)` (arg-order flip) — fixed — derived from convspec construct `dart.package_test.expect_equals` (rf-dart-expect-equals-to-xunit-assertequal).
- §2.25 `expect(actual, isA<T>())` → `Assert.IsType<T>(actual)` — fixed — derived from convspec construct `dart.package_test.expect_isA_T` (rf-dart-expect-isA-to-xunit-assert-istype).
- §2.26 `expect(b, isFalse|isTrue)` → `Assert.False(b)` / `Assert.True(b)` — fixed — derived from convspec construct `dart.package_test.expect_isFalse_isTrue` (rf-dart-expect-istrue-to-xunit-asserttrue).
- §2.27 `expect(() => fn, returnsNormally)` → bare `Fn();` — fixed — derived from convspec construct `dart.package_test.expect_function_returnsNormally` (rf-dart-expect-returns-normally-to-xunit-bare-call).
- T9 `expect(xValue.functor, isNot(equals(yValue.functor)))` → `Assert.NotEqual(<expected>, <actual>)` — fixed — derived from convspec construct `dart.package_test.expect_equals` (rf-dart-expect-equals-to-xunit-assertequal) composed with Dart `isNot(matcher)` negation matching xUnit's `Assert.NotEqual` (the canonical inverse of `Assert.Equal`; arg-order flip applies identically).

## 6. Escalations

None.
