> Conversion-spec artifact for test/module/module_compiler_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/module/module_compiler_test.dart
source_sha256: 112ccd7b1688a462b205b63e4ad4082a0088432a2921539cee0597b8c8f7c2dd
target_code_unit: test/module/ModuleCompilerTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned project-wide as the
      modern .NET default — same idiom as the precedent files
      test/module/module_parser_test.dart.md,
      test/module/module_typecheck_test.dart.md,
      test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md, recorded under
      `rf-dart-package-test-import-to-xunit-using`). Codegen MUST also add
      `using System.Linq;` because the file uses Dart `Iterable.whereType<T>()`
      twice (lines 90, 107, 132, 144, 163, 164) — see
      `dart.iterable.whereType_filter` below; the C# equivalent is
      `IEnumerable<T>.OfType<TResult>().ToList()` which lives in
      `System.Linq`.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom — reused verbatim, no re-research. xUnit selection is
      project-wide policy (NOT a file-local choice); reversing it would
      invalidate every prior test-file convspec. The `System.Linq` addition
      is mechanical per the `whereType` construct row below.
  - construct_key: dart.package_under_test.import_directive
    source_form: |-
      "import 'package:glp_runtime/compiler/lexer.dart';
       import 'package:glp_runtime/compiler/parser.dart';
       import 'package:glp_runtime/compiler/analyzer.dart';
       import 'package:glp_runtime/compiler/codegen.dart';
       import 'package:glp_runtime/compiler/ast.dart';
       import 'package:glp_runtime/bytecode/opcodes.dart';"
    target_decision: >-
      Each of the six `package:glp_runtime/...` imports maps to a `using`
      directive that names the C# namespace produced by converting the
      corresponding SUT file. The five `compiler/*.dart` imports
      (lexer, parser, analyzer, codegen, ast) collapse to ONE `using` line
      if all five target files share the C# namespace `<RootNs>.Compiler`
      (the conventional outcome — see .codeconv/conversion-specs/
      lib/compiler/{lexer,parser,analyzer,codegen,ast}.dart.md). The
      `bytecode/opcodes.dart` import maps to the C# namespace produced
      from `lib/bytecode/opcodes.dart` (conventionally `<RootNs>.Bytecode`
      — see .codeconv/conversion-specs/lib/bytecode/opcodes.dart.md). The
      exact namespace strings are decided when those SUT files convert;
      this spec records only the SHAPE of the cross-file dependency.
      `Distribute` and `Transmit` (referenced unqualified in the body —
      `ops.whereType<Distribute>()`, `Distribute(1, 'factorial', 2)`,
      `Transmit(5, 'foo', 3)`) live in the bytecode/opcodes namespace per
      `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md` — codegen
      consults that spec for the final type names; the `using` collapses
      across the Bytecode namespace.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedent: boot_loader_test.dart.md,
      module_parser_test.dart.md). Multiple Dart package-imports collapsing
      to a single C# `using` when target files share a namespace is a
      documented C# convention (one `using` per namespace, not per file).
      No `as`-alias / show / hide directives appear in this file, so simple
      `using <Ns>;` suffices for each distinct target namespace. The
      enum-style class-member access `Distribute(...)` constructor / `op.
      importIndex` / `op.functor` / `op.arity` translation is governed by
      the opcodes.dart.md spec.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('ImportTable', () { ... }); group('RPC Transformation - Static Module', () { ... }); group('RPC Transformation - Dynamic Module', () { ... }); group('RPC Transformation - Mixed', () { ... }); group('Distribute and Transmit opcodes', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint; xUnit
      discovers `[Fact]` methods by reflection — there is NO per-file
      entrypoint to emit. Eliminate `main` entirely; its body (five
      top-level `group(...)` calls) becomes five enclosing test classes
      (see `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedent: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md, module_parser_test.dart.md). Lifecycle
      nuance: Dart `main` is invoked once per test-file process; xUnit has
      no per-file hook. THIS file's `main` body is exactly five sibling
      `group()` calls with no file-level setUp / no shared state, so the
      omission is lossless — no migration into `IClassFixture<>` needed.
  - construct_key: dart.package_test.group_block
    source_form: |-
      "group('ImportTable', () { test(...); ...×6 tests });
       group('RPC Transformation - Static Module', () { test(...); ...×2 tests });
       group('RPC Transformation - Dynamic Module', () { test(...); ...×2 tests });
       group('RPC Transformation - Mixed', () { test(...); ...×1 test });
       group('Distribute and Transmit opcodes', () { test(...); ...×2 tests });"
    target_decision: >-
      Five sibling top-level `group(...)` calls (NOT nested). Map each to
      its own PascalCase xUnit test class within the same `.cs` file. The
      labels become class names with non-identifier characters stripped
      and dashes converted to camel-joins: `ImportTableTests`,
      `RpcTransformationStaticModuleTests`,
      `RpcTransformationDynamicModuleTests`,
      `RpcTransformationMixedTests`,
      `DistributeAndTransmitOpcodesTests`. The original label MUST be
      preserved verbatim via `[Fact(DisplayName = "<original label>")]` on
      every test method so reporter output keeps the Dart sentence form.
      SIBLING (not nested) groups in this file do NOT share state (no
      `late` field, no `setUp`), so each group becomes a FULLY INDEPENDENT
      class — no shared base class, no `IClassFixture<>`. The top-level
      `compile(String source)` helper (lines 10–20) is file-scoped and
      shared by EIGHT test methods across THREE groups (Static / Dynamic
      / Mixed) — see `dart.toplevel.helper_function` below for the C#
      placement rule.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md, module_parser_test.dart.md). Topology
      nuance EXPLICITLY addressed: this file's groups are SIBLINGS (five
      top-level `group(...)` calls in `main`), not NESTED. Because no
      outer group exists, no shared `late` field forces a single-class
      FLATTEN; each sibling group becomes its own class. The shared
      file-scope `compile` helper is NOT a per-group concern — it must be
      reachable by EVERY group (Static, Dynamic, Mixed) so it lives at
      file/namespace scope, not class scope (see the helper construct row).
      Name-mangling nuance: the labels `'RPC Transformation - Static Module'`
      etc. contain spaces, hyphens, and the bare-acronym `RPC`; mangling
      MUST strip non-identifier characters and PascalCase the rest. Initial
      `RPC` is preserved as `Rpc` (PascalCase convention — three-letter
      acronyms cased as words per Microsoft naming guidelines).
  - construct_key: dart.toplevel.helper_function
    source_form: |-
      "/// Helper to compile GLP source to bytecode and return the ops list
       List<dynamic> compile(String source) {
         final lexer = Lexer(source);
         final tokens = lexer.tokenize();
         final parser = Parser(tokens);
         final program = parser.parse();
         final analyzer = Analyzer();
         final annotated = analyzer.analyze(program);
         final generator = CodeGenerator();
         final bytecode = generator.generate(annotated);
         return bytecode.ops;
       }"
    target_decision: >-
      A FILE-SCOPE Dart helper function used by 8 of the 13 tests across 3
      sibling groups. Because xUnit forbids true top-level methods at the
      file level (everything must be in a class) and the helper is shared
      by methods in MULTIPLE per-group classes, emit a static `internal
      static class ModuleCompilerTestHelpers` (sibling to the per-group
      test classes within the same namespace) containing a `public static
      IReadOnlyList<object> Compile(string source) { ... }` method. The
      method body translates statement-for-statement: each `final <n> =
      <Ctor>(<args>);` becomes `var <n> = new <Ctor>(<args>);` (per
      `rf-dart-constructor-call-no-new-to-csharp-new-keyword` cached
      idiom — Dart 2.x dropped optional `new`; C# requires it); the
      terminal `return bytecode.ops;` becomes `return bytecode.Ops;` (the
      `ops` member's casing is decided by codegen.dart.md /
      opcodes.dart.md; this spec records the body shape, not the property
      name). All test sites then call `ModuleCompilerTestHelpers.Compile(
      "...")` to obtain the heterogeneous opcode list.
    idiom_id: null
    research_finding_id: rf-dart-toplevel-function-to-csharp-static-helper-class
    nuance: >-
      FIRST-SEEN idiom row. Top-level-function nuance (EXPLICITLY
      addressed): Dart allows true file-scope functions; C# does NOT (every
      method must be a member of a type). The canonical C# translation
      wraps file-scope helpers in a `static class` (per Microsoft naming
      guidelines: "Use a static class that contains a set of static methods"
      — https://learn.microsoft.com/dotnet/standard/design-guidelines/static-class).
      The `internal` access modifier limits visibility to the test
      assembly (matching the Dart implicit-library-scope of an
      unprefixed top-level function); the helper class is sibling — NOT
      base — to the per-group test classes, so it composes naturally with
      the sibling-groups-as-separate-classes idiom established above.
      Doc-comment nuance: Dart `///` triple-slash doc comments map to C#
      `///` XML doc comments (identical syntax for the slash prefix);
      payload becomes `<summary>...</summary>` per the C# doc-comment
      convention (https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags).
  - construct_key: dart.return_type.list_of_dynamic
    source_form: "List<dynamic> compile(String source) { ... return bytecode.ops; }"
    target_decision: >-
      Dart `List<dynamic>` (a list whose element static-type is `dynamic`
      — Dart's escape-hatch type that defers all member-access to runtime)
      maps to C# `IReadOnlyList<object>` for THIS file's usage shape
      (immutable downstream read-only consumption: `.whereType<T>().toList()`
      and indexed `.length` checks only). The actual underlying list is
      heterogeneous (mix of `Distribute`, `Transmit`, and other opcode
      classes per opcodes.dart.md); `object` is the C# heterogeneous-list
      root. `dynamic` in C# is a SEPARATE TYPE (DLR-binding, late-binding
      member dispatch) — it MUST NOT be used here because the downstream
      `OfType<Distribute>()` / `OfType<Transmit>()` filters perform
      runtime-type filtering on the strongly-typed elements (not late-
      bound member access), so `object` (not `dynamic`) is correct.
    idiom_id: null
    research_finding_id: rf-dart-list-of-dynamic-to-csharp-ireadonlylist-of-object
    nuance: >-
      FIRST-SEEN idiom row. Dart-`dynamic` vs C#-`dynamic` nuance
      (EXPLICITLY addressed — well-known footgun): both languages have a
      `dynamic` keyword, but they MEAN DIFFERENT THINGS. Dart `dynamic`
      (https://dart.dev/language/built-in-types#the-dynamic-type) is the
      type-erasure escape hatch — a static-checking opt-out, semantically
      equivalent to "untyped object reference with runtime member lookup";
      the storage is still a regular reference. C# `dynamic`
      (https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-dynamic-type)
      activates the DLR for runtime-bound member dispatch — a heavier
      mechanism with measurable overhead and IDE / refactoring opacity.
      For the use site here (storing opcode INSTANCES and filtering by
      RUNTIME TYPE), the appropriate C# type is `object` — see also
      Microsoft Learn "When to use dynamic"
      (https://learn.microsoft.com/dotnet/csharp/programming-guide/types/using-type-dynamic).
      Mutability nuance: Dart `List<T>` is mutable by default; the helper
      RETURNS the list directly without copy. `IReadOnlyList<object>`
      signals that consumers SHOULD NOT mutate; the actual returned
      instance is still a `List<object>` (the SUT-side `bytecode.ops`
      list — its mutability is decided in opcodes.dart.md /
      codegen.dart.md). All test consumers use only `OfType<>` /
      `Count` / indexed read, so read-only is correct.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange, act, assert */ });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body and
      no `skip:` argument becomes a `public void` instance method on the
      enclosing xUnit class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The method name is the
      label PascalCased with non-identifier characters stripped — examples
      from this file:
      `'assigns 1-based indices to imports'` → `Assigns1BasedIndicesToImports`;
      `'returns same index for duplicate imports'` → `ReturnsSameIndexForDuplicateImports`;
      `'getIndex returns null for unknown modules'` → `GetIndexReturnsNullForUnknownModules`;
      `'size returns number of unique imports'` → `SizeReturnsNumberOfUniqueImports`;
      `'orderedImports returns imports in index order'` → `OrderedImportsReturnsImportsInIndexOrder`;
      `'contains checks for module presence'` → `ContainsChecksForModulePresence`;
      `'compiles static RPC to Distribute opcode'` → `CompilesStaticRpcToDistributeOpcode`;
      `'assigns correct indices to multiple static RPCs'` → `AssignsCorrectIndicesToMultipleStaticRpcs`;
      `'compiles dynamic RPC to Transmit opcode'` → `CompilesDynamicRpcToTransmitOpcode`;
      `'compiles dynamic RPC with multiple args'` → `CompilesDynamicRpcWithMultipleArgs`;
      `'handles mix of static and dynamic RPCs'` → `HandlesMixOfStaticAndDynamicRpcs`;
      `'Distribute toString formats correctly'` → `DistributeToStringFormatsCorrectly`;
      `'Transmit toString formats correctly'` → `TransmitToStringFormatsCorrectly`.
      All 13 callbacks in this file are synchronous (no `async`/`Future`),
      so NO target method is `async Task`. The arrange/act/assert closure
      body translates statement-for-statement: final-local declarations →
      `var` declarations; `expect(...)` calls → `Assert.*` calls; `compile(...)`
      helper-calls → `ModuleCompilerTestHelpers.Compile(...)`.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md, multiple
      others). Async nuance (explicitly addressed even though absent in
      this file): a Dart `test('...', () async { ... })` would target
      `public async Task <Name>()`. None of this file's callbacks are
      async. Closure-capture nuance: callbacks here capture NOTHING from
      `main()` scope (no `setUp` variables, no `late` field); they DO call
      the FILE-SCOPE `compile` helper, which becomes a static-class call
      (see `dart.toplevel.helper_function`).
  - construct_key: dart.local.final_var_declaration
    source_form: |-
      "final table = ImportTable();
       final ops = compile('''...''');
       final distributeOps = ops.whereType<Distribute>().toList();
       final transmitOps = ops.whereType<Transmit>().toList();
       final op = Distribute(1, 'factorial', 2);
       final op = Transmit(5, 'foo', 3);
       final lexer = Lexer(source); final tokens = lexer.tokenize();
       final parser = Parser(tokens); final program = parser.parse();
       final analyzer = Analyzer(); final annotated = analyzer.analyze(program);
       final generator = CodeGenerator(); final bytecode = generator.generate(annotated);"
    target_decision: >-
      Every `final <name> = <expr>;` local in this file maps to
      `var <name> = <expr>;` in C#. `final` in Dart on a LOCAL is the
      "single-assignment, type-inferred" idiom; C# `var` is identical
      except `var` is NOT single-assignment (it's just type-inferred and
      mutable). For test method locals AND helper-body locals — none of
      which are reassigned anywhere in this file — the looser `var` is
      observably equivalent. Stricter equivalents (`readonly` for fields,
      `const` for compile-time constants, `in` for parameter-direction)
      DO NOT apply to method/helper-body locals. Dart 2.x constructor
      calls without `new` (`ImportTable()`, `Distribute(1, 'factorial',
      2)`, `Transmit(5, 'foo', 3)`, `Lexer(source)`, `Parser(tokens)`,
      `Analyzer()`, `CodeGenerator()`) map to `new ImportTable()`,
      `new Distribute(1, "factorial", 2)`, `new Transmit(5, "foo", 3)`,
      `new Lexer(source)`, `new Parser(tokens)`, `new Analyzer()`,
      `new CodeGenerator()` in C# (per
      `rf-dart-constructor-call-no-new-to-csharp-new-keyword` cached
      idiom from varref_pointer_test.dart.md / suspension_pointer_test.dart.md).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      global_send_test.dart.md, binding_pointer_test.dart.md,
      varref_pointer_test.dart.md, module_parser_test.dart.md).
      Single-assignment nuance (explicitly addressed): Dart `final`
      enforces no-reassignment at the language level; C# `var` does NOT.
      For these test/helper bodies the distinction is invisible — no local
      is ever reassigned in this file. If a future test reassigned a
      `final`-target local, codegen would need to flag it; but `final`
      PROHIBITS reassignment in Dart, so that case cannot arise from
      valid source. The `new`-keyword nuance is well-known (Dart 2 made
      `new` optional; C# requires it) and is governed by the cached
      `rf-dart-constructor-call-no-new-to-csharp-new-keyword`.
  - construct_key: dart.iterable.whereType_filter
    source_form: |-
      "final distributeOps = ops.whereType<Distribute>().toList();
       final transmitOps = ops.whereType<Transmit>().toList();"
    target_decision: >-
      Dart `Iterable<dynamic>.whereType<T>()` filters by RUNTIME TYPE and
      yields an `Iterable<T>` containing only the elements assignable to
      `T`. The C# equivalent is `IEnumerable<object>.OfType<T>()` from
      `System.Linq` — same semantic (runtime type-filter, skip non-T
      elements; result is `IEnumerable<T>`). The terminal `.toList()`
      becomes `.ToList()` (terminal that materialises the deferred
      sequence into `List<T>`). Concretely:
      `ops.whereType<Distribute>().toList()` →
      `ops.OfType<Distribute>().ToList()`. The four call sites (lines 90,
      107, 132, 144, 163, 164 — six total invocations across the Static /
      Dynamic / Mixed groups, plus implicit single-element materialisation
      in the Mixed group) all follow this rule.
    idiom_id: rf-dart-iterable-where-to-linq
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Cached idiom (precedent: lib/lint/linter.dart.md and
      lib/analysis/analysis_phase.dart.md). Type-filter semantics
      EXPLICITLY addressed: Dart `whereType<T>`
      (https://api.dart.dev/stable/dart-core/Iterable/whereType.html)
      and C# `OfType<TResult>`
      (https://learn.microsoft.com/dotnet/api/system.linq.enumerable.oftype)
      are documented EQUIVALENTS — both skip null and non-T elements,
      both return a lazy/deferred sequence, both materialise on a terminal
      operator. The `.toList()` → `.ToList()` mapping is the canonical
      LINQ terminal. Alternative idioms (`ops.Cast<Distribute>().ToList()`,
      `ops.Where(o => o is Distribute).Cast<Distribute>().ToList()`) are
      observably weaker (`Cast` THROWS on non-T elements, `Where` requires
      a separate cast) — `OfType<T>().ToList()` is the literal translation
      preferred for review traceability.
  - construct_key: dart.package_test.expect_equals_implicit
    source_form: |-
      "expect(table.addImport('math'), 1);
       expect(table.addImport('io'), 2);
       expect(table.getIndex('math'), 1);
       expect(table.size, 0); expect(table.size, 1); expect(table.size, 2);
       expect(distributeOps.length, 2);
       expect(distributeOps[0].importIndex, 1);
       expect(distributeOps[0].functor, 'factorial');
       expect(distributeOps[0].arity, 2);
       expect(transmitOps[0].functor, 'foo');
       expect(transmitOps[0].arity, 1);
       expect(op.toString(), 'Distribute([1] factorial/2)');
       expect(op.toString(), 'Transmit(X5, foo/3)');
       ... (~25 implicit-equals calls in total)"
    target_decision: >-
      Dart `expect(<actual>, <literal-value>)` (implicit-equals, no
      `equals(...)` wrapper) is the matcher-library implicit-equality
      shorthand and maps to xUnit `Assert.Equal(<expected>, <actual>)`
      with the ARGUMENT-ORDER FLIP (Dart puts actual first; xUnit puts
      expected first). All ~25 `expect(actual, literal)` calls in this
      file use this shorthand on `int` (`.length`, `.arity`, `.importIndex`,
      `.size`, `1`, `2`, `3`, `4`), `String` (`'factorial'`, `'foo'`,
      `'process'`, `'bar'`, `'gcd'`, `'print'`, the two
      `Distribute.toString()` / `Transmit.toString()` payloads), and `bool`
      (no bool literals appear in this file's implicit-equals form — the
      one `true` and one `false` use `expect(x, true)` / `expect(x,
      false)` which are still implicit-equals on `bool`). All three value
      types have value-equality semantics identical between Dart `==` and
      C# `Equals`.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      mad_error_handling_test.dart.md prose, moded_head_test.dart.md,
      boot_loader_test.dart.md). Argument-order flip is the well-known
      footgun. Implicit-equals nuance (explicitly addressed):
      `package:test` treats a literal second argument to `expect` as an
      implicit `equals(literal)` matcher; the C# target collapses to the
      SAME `Assert.Equal` as the explicit `expect(x, equals(y))` form, so
      the implicit/explicit Dart distinction is NOT preserved in the
      target (lossless because both use value-equality). For the
      `expect(table.contains('math'), true)` / `expect(table.contains('io'),
      false)` calls (line 74 / 75), the literal C# translation is
      `Assert.Equal(true, table.Contains("math"))` / `Assert.Equal(false,
      table.Contains("io"))` — observably equivalent to the more idiomatic
      `Assert.True(table.Contains("math"))` / `Assert.False(table.Contains
      ("io"))`. Codegen MAY prefer the literal `Assert.Equal` shape for
      mechanical fidelity, OR the more idiomatic `Assert.True` /
      `Assert.False` shape — both are documented xUnit usage; the
      precedent file boot_loader_test.dart.md uses the `Assert.Equal`
      literal shape and this spec inherits it.
  - construct_key: dart.package_test.expect_isNull_matcher
    source_form: "expect(table.getIndex('unknown'), isNull);"
    target_decision: >-
      Dart `expect(x, isNull)` maps to xUnit `Assert.Null(x);` per the
      cached idiom. One use in this file (line 47 — `table.getIndex(
      'unknown')` in `getIndex returns null for unknown modules`).
      `Assert.Null` semantics: pass iff `x is null` (reference equality
      with `null`), identical to Dart `isNull`.
    idiom_id: rf-dart-expect-isNull-to-xunit-assert-null
    research_finding_id: rf-dart-expect-isNull-to-xunit-assert-null
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      global_send_test.dart.md, binding_pointer_test.dart.md). Argument-
      order: `Assert.Null` is the unary form (one arg, no flip needed) —
      distinct from the binary `Assert.Equal(null, x)` which would also
      pass but reads less idiomatically. xUnit
      (https://xunit.net/docs/comparisons#assertions) recommends
      `Assert.Null` for null-checks specifically.
  - construct_key: dart.package_test.expect_list_equals_implicit
    source_form: "expect(table.orderedImports, ['io', 'math', 'utils']);"
    target_decision: >-
      Dart `expect(<List<T>>, <list-literal>)` (implicit-equals on a list
      operand) is the matcher-library implicit-equality shorthand on a
      list — the matcher resolves to `equals(literal)` which for a list
      means ordered, element-wise equality. The C# equivalent is xUnit
      `Assert.Equal(<IEnumerable<T>>, <IEnumerable<T>>)` (binary form on
      enumerables) — documented at xUnit Asserts
      (https://xunit.net/docs/comparisons#assertions): for IEnumerable
      operands, xUnit walks both sequences in lockstep using the default
      equality comparer. Concrete translation:
      `Assert.Equal(new[] { "io", "math", "utils" },
      table.OrderedImports);` (Dart list literal → C# array initialiser
      `new[] { ... }`, which implicit-converts to `IEnumerable<string>`).
      Argument-order FLIP applies (Dart actual-first, xUnit
      expected-first).
    idiom_id: null
    research_finding_id: rf-dart-expect-list-equals-to-xunit-assertequal-enumerable
    nuance: >-
      FIRST-SEEN idiom row. Ordered-vs-unordered nuance (EXPLICITLY
      addressed): Dart `package:matcher` `equals` on a List performs
      ORDERED element-wise comparison
      (https://pub.dev/documentation/matcher/latest/matcher/equals.html);
      xUnit `Assert.Equal(IEnumerable<T>, IEnumerable<T>)` ALSO performs
      ORDERED element-wise comparison
      (https://learn.microsoft.com/dotnet/api/xunit.assert.equal#xunit-assert-equal-1).
      Both sides are ordered — the translation is lossless. List-literal
      shape nuance: Dart `['io', 'math', 'utils']` is a `List<String>`
      literal with type-inference; C# `new[] { "io", "math", "utils" }`
      is a `string[]` array initialiser with type-inference. Alternative:
      `new List<string> { "io", "math", "utils" }` — observably equivalent
      for the xUnit assertion. Distinct from the SET-literal idiom
      `rf-dart-set-literal-typed-to-csharp-hashset-initializer` (which
      compares MEMBERSHIP, not ORDER, and uses `HashSet<T>`).
  - construct_key: dart.string.triple_quoted_raw_literal
    source_form: |-
      "final ops = compile('''
       boot :- otherwise |
           math # factorial(5, R),
           io # print(R?).
       ''');
       (used in 5 of the 8 `compile(...)` call sites — every GLP source fixture in the Static / Dynamic / Mixed groups)"
    target_decision: >-
      Dart triple-single-quoted multi-line string literals (used to embed
      every `.glp` source fixture in this file) map to C# 11 raw string
      literals (`""" ... """`) — same as the cached idiom from
      boot_loader_test.dart.md / module_parser_test.dart.md. The literal
      payload is byte-identical across the boundary; codegen MUST emit
      the closing `"""` at the appropriate column to preserve indentation.
      Fallback to C# verbatim strings (`@"..."`) for pre-C#11 targets is
      equivalent here because no fixture in this file contains a `"`.
    idiom_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      Cached idiom (precedent: boot_loader_test.dart.md,
      module_parser_test.dart.md). Whitespace nuance: Dart triple-quoted
      strings preserve leading whitespace exactly; C# raw strings strip
      a common indent matched to the closing `"""` column. Newline-
      encoding nuance: Dart `'''...'''` uses `\n` line endings (LF in
      source); C# 11 raw strings preserve source-file line endings — on
      Windows-edited source files this could be `\r\n`. Codegen MUST
      normalise to `\n` (LF) line endings inside the raw string literal
      so the byte-identity invariant holds against the Dart fixture.
  - construct_key: dart.string.single_quoted_literal
    source_form: "'math', 'io', 'utils', 'unknown', 'factorial', 'gcd', 'foo', 'bar', 'process', 'print', 'Distribute([1] factorial/2)', 'Transmit(X5, foo/3)', etc."
    target_decision: >-
      Dart single-quoted single-line string literals (every label /
      identifier / `toString` expected-payload fragment in this file's
      `expect` calls and `compile`-helper arguments) map to C#
      double-quoted string literals (`"math"`, `"io"`, etc.).
      Escape-sequence nuance is trivial here because no literal in this
      file uses Dart-specific escapes (`\$`, `\u{...}`) — all content is
      ASCII printable.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md). Quote-character
      nuance (explicitly addressed): Dart accepts BOTH `'...'` and `"..."`
      for single-line literals (no semantic difference); C# accepts ONLY
      `"..."`. No literal in this file requires escape transformations.
      The two `toString()` expected payloads `'Distribute([1] factorial/
      2)'` and `'Transmit(X5, foo/3)'` contain `[`, `]`, `(`, `)`, `/` —
      all legal inside C# double-quoted strings without escape.
  - construct_key: dart.list.indexer_access
    source_form: |-
      "distributeOps[0].importIndex; distributeOps[0].functor; distributeOps[0].arity;
       distributeOps[1].importIndex; distributeOps[2].importIndex;
       distributeOps[2].functor; distributeOps[3].importIndex;
       transmitOps[0].functor; transmitOps[0].arity;"
    target_decision: >-
      Dart list / `List<T>` zero-indexed `[i]` access maps DIRECTLY to C#
      `List<T>` / `IList<T>` zero-indexed `[i]` access (identical syntax).
      Both throw on out-of-range. No mapping logic needed; this row
      exists to anchor the cached idiom for code review.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md). Exception-type
      nuance: Dart `RangeError` vs C# `ArgumentOutOfRangeException` are
      distinct exception types but the test bodies never exercise the OOB
      case — every indexer in this file targets a known-good position
      established by the preceding `distributeOps.length == N` /
      `transmitOps.length == N` assertion.
  - construct_key: dart.property_access.identity
    source_form: "table.size; distributeOps.length; distributeOps[0].importIndex; distributeOps[0].functor; distributeOps[0].arity; transmitOps[0].functor; transmitOps[0].arity; op.toString()"
    target_decision: >-
      Dart property/getter access `<expr>.<name>` maps to C# property
      access `<expr>.<PascalName>` (identical syntax modulo identifier
      casing convention). Codegen MUST consult the SUT convspecs for the
      target casing of each member — `ImportTable.size` →
      `ImportTable.Size` (per
      .codeconv/conversion-specs/lib/compiler/codegen.dart.md); the
      `Distribute.importIndex` / `Distribute.functor` / `Distribute.arity`
      → `Distribute.ImportIndex` / `Functor` / `Arity` (per
      .codeconv/conversion-specs/lib/bytecode/opcodes.dart.md);
      `List.length` → `List<T>.Count` (the well-known Dart→.NET rename).
      `op.toString()` → `op.ToString()` (PascalCase override on
      `System.Object`). This row exists so codegen has ONE place to look
      up the property-access transformation rule across the whole file.
    idiom_id: null
    research_finding_id: rf-dart-property-access-to-csharp-property-access
    nuance: >-
      FIRST-SEEN idiom row. Length-vs-Count nuance (EXPLICITLY addressed
      — well-known footgun): Dart `List<T>.length`
      (https://api.dart.dev/stable/dart-core/List/length.html) → C#
      `List<T>.Count`
      (https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.count)
      — different names, identical semantics (O(1) element count). Dart
      `String.length` similarly maps to C# `string.Length` (Pascal-cased
      property — the rare C# case where `Length` is the canonical name,
      NOT `Count`; `string`, `Array`, `Span<T>`, `ReadOnlySpan<T>` use
      `Length`; collection types use `Count`). No `String.length` access
      appears in this file, but the rule is recorded for downstream
      reuse. `toString()` → `ToString()` is the trivial PascalCase rename
      governed by C# inheritance from `System.Object.ToString`
      (https://learn.microsoft.com/dotnet/api/system.object.tostring).
  - construct_key: dart.doc_comment.triple_slash
    source_form: "/// Helper to compile GLP source to bytecode and return the ops list"
    target_decision: >-
      Dart triple-slash doc comments `/// <text>` map to C# triple-slash
      XML doc comments `/// <summary><text></summary>`. The C# documenter
      requires the `<summary>` tag for the doc-line to be picked up by
      Visual Studio IntelliSense and the doc-XML build output (project-
      level `<GenerateDocumentationFile>true</GenerateDocumentationFile>`).
      The single doc-line in this file becomes one
      `/// <summary>Helper to compile GLP source to bytecode and return
      the ops list</summary>` line above the `Compile` static method.
    idiom_id: null
    research_finding_id: rf-dart-doc-comment-to-csharp-xml-doc-comment
    nuance: >-
      FIRST-SEEN idiom row. Doc-comment shape nuance (EXPLICITLY
      addressed): Dart `///` doc comments accept markdown-style payload
      with bare text + paragraph breaks; C# `///` doc comments require
      XML markup (`<summary>`, `<param>`, `<returns>`, `<remarks>`,
      `<exception>`). Authoritative Dart: dart.dev language reference —
      "Doc comments" (https://dart.dev/effective-dart/documentation).
      Authoritative .NET: Microsoft Learn — "Recommended XML tags for
      C# documentation comments"
      (https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags).
      For a single-line summary, the `<summary>` wrap is the minimal
      idiomatic form. Codegen MAY split the comment into `<summary>` +
      `<returns>` if the Dart doc-comment includes a "Returns ..."
      sentence; this file's single doc-line conflates both, and the
      minimal wrap is sufficient.
  - construct_key: dart.comment.line
    source_form: |-
      "// Same
       // Still 1
       // Duplicate
       // factorial + print
       // math = index 1
       // io = index 2
       // math # factorial
       // io # print
       // math # gcd (same index as first math call)
       // io # print (same index)
       // Valid SRSW: R is bound in RPC, R? is used in print
       // Simplest valid SRSW pattern: M writer in head, M? reader in body
       // Args X passed as reader from head to body
       // M is module, X and Y are args passed through
       // M is writer (head), M? reader (body)
       // X is writer (head), X? reader (body)
       // F is writer (factorial output), F? reader (process input)
       // Find Distribute instruction"
    target_decision: >-
      Dart `//` single-line comments map DIRECTLY to C# `//` single-line
      comments (identical syntax). Every comment in this file is preserved
      verbatim by codegen — they document SRSW (Single-Reader / Single-
      Writer) invariants of the embedded GLP fixtures and are not
      translated because (a) they pertain to the EMBEDDED Dart-source-
      level fixtures (GLP language), NOT the Dart→C# transformation, and
      (b) preserving the source author's reasoning aids review.
    idiom_id: null
    research_finding_id: rf-dart-line-comment-to-csharp-line-comment
    nuance: >-
      FIRST-SEEN idiom row (trivial — recorded for KB completeness).
      Comment-payload nuance: the comments reference GLP-specific terms
      (SRSW, `M?`, `R?`, `# `) that are NOT Dart language constructs; they
      describe the EMBEDDED GLP-source fixtures inside the raw-string
      payloads. Codegen MUST preserve them as opaque commentary; no
      translation of `M?` to a C# equivalent is needed (they are GLP
      reader-syntax, not Dart). Authoritative: Dart language tour
      "Comments" (https://dart.dev/language/comments); C# language
      reference "Comments"
      (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/comments).
conversion_units:
  - cu-1: file-scope using directives (Xunit + System.Linq + System.Collections.Generic [for IReadOnlyList<object>] + SUT namespaces from glp_runtime/compiler/{lexer,parser,analyzer,codegen,ast}.dart and glp_runtime/bytecode/opcodes.dart)
  - cu-2: namespace declaration mirroring the test/module path (e.g. <RootNs>.Test.Module)
  - cu-3: internal static class ModuleCompilerTestHelpers with the public static Compile(string source) helper method (from the file-scope `compile(...)` helper)
  - cu-4: class ImportTableTests with 6 `[Fact(DisplayName="...")]` methods (Assigns1BasedIndicesToImports, ReturnsSameIndexForDuplicateImports, GetIndexReturnsNullForUnknownModules, SizeReturnsNumberOfUniqueImports, OrderedImportsReturnsImportsInIndexOrder, ContainsChecksForModulePresence) — uses Assert.Null, Assert.Equal(IEnumerable, IEnumerable)
  - cu-5: class RpcTransformationStaticModuleTests with 2 `[Fact]` methods (CompilesStaticRpcToDistributeOpcode, AssignsCorrectIndicesToMultipleStaticRpcs) — calls ModuleCompilerTestHelpers.Compile + OfType<Distribute>().ToList()
  - cu-6: class RpcTransformationDynamicModuleTests with 2 `[Fact]` methods (CompilesDynamicRpcToTransmitOpcode, CompilesDynamicRpcWithMultipleArgs) — calls ModuleCompilerTestHelpers.Compile + OfType<Transmit>().ToList()
  - cu-7: class RpcTransformationMixedTests with 1 `[Fact]` method (HandlesMixOfStaticAndDynamicRpcs) — uses both OfType<Distribute> + OfType<Transmit>
  - cu-8: class DistributeAndTransmitOpcodesTests with 2 `[Fact]` methods (DistributeToStringFormatsCorrectly, TransmitToStringFormatsCorrectly) — uses Assert.Equal on op.ToString() payloads
  - cu-9: raw-string-literal payloads (`"""..."""`) for every embedded `.glp` source fixture across the 5 `compile(...)` calls in the Static/Dynamic/Mixed groups, with LF line endings and column-0 closing delimiter so the literal payload is byte-identical to the Dart fixture
escalations: []
```

## Rationale + research provenance

### Cached-idiom reuse profile (SC-007 / FR-012)

12 of the 18 constructs in this file resolve via a CACHED idiom_id from
prior file convspecs:

- `rf-dart-package-test-import-to-xunit-using` (precedent:
  module_parser_test.dart.md, mad_error_handling_test.dart.md)
- `rf-dart-internal-package-import-to-csharp-using` (precedent:
  module_parser_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-package-test-main-omit-in-xunit` (precedent:
  module_parser_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-package-test-group-to-xunit-class` (precedent:
  module_parser_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-test-callback-to-xunit-method-body` (multiple precedents)
- `rf-dart-final-local-to-csharp-var-local` (precedents:
  module_parser_test.dart.md, boot_loader_test.dart.md,
  varref_pointer_test.dart.md)
- `rf-dart-constructor-call-no-new-to-csharp-new-keyword` (precedent:
  varref_pointer_test.dart.md, suspension_pointer_test.dart.md)
- `rf-dart-iterable-where-to-linq` (precedent: lib/lint/linter.dart.md,
  lib/analysis/analysis_phase.dart.md)
- `rf-dart-expect-equals-to-xunit-assertequal` (precedent:
  module_parser_test.dart.md, mad_error_handling_test.dart.md prose,
  moded_head_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-expect-isNull-to-xunit-assert-null` (precedent:
  module_parser_test.dart.md, global_send_test.dart.md,
  binding_pointer_test.dart.md)
- `rf-dart-triple-quoted-string-to-csharp-raw-string` (precedent:
  module_parser_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
  (precedent: module_parser_test.dart.md)
- `rf-dart-list-indexer-to-csharp-list-indexer` (precedent:
  module_parser_test.dart.md)

Reusing these cached idioms verbatim (no re-research, no re-derivation)
satisfies the FR-012 / SC-007 consistency guarantee. The KB-lookup
decision-order from `convspec_idiom_schema.md` was applied per
construct: KB lookup hit → REUSE.

### Six FIRST-SEEN idiom rows (research-justified, NO escalation)

1. **`rf-dart-toplevel-function-to-csharp-static-helper-class`** —
   the file-scope `List<dynamic> compile(String source)` helper used by
   eight test methods across three sibling groups. C# forbids top-level
   methods at file scope, so the canonical translation wraps shared
   helpers in an `internal static class` containing `public static`
   methods. Authoritative bases: Microsoft Learn "Static classes and
   static class members"
   (https://learn.microsoft.com/dotnet/standard/design-guidelines/static-class)
   and "Methods" (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/methods).
   Dart top-level function semantics: dart.dev language tour "Functions"
   (https://dart.dev/language/functions).

2. **`rf-dart-list-of-dynamic-to-csharp-ireadonlylist-of-object`** —
   `List<dynamic>` as a function return type, when the consumers use
   only runtime-type-filter (`OfType<T>`) + indexed read. The Dart
   `dynamic` keyword and C# `dynamic` keyword have CRITICALLY DIFFERENT
   semantics (Dart = static-type-erasure escape hatch; C# = DLR-bound
   late-binding member dispatch). The correct C# type for a
   heterogeneous-element list consumed read-only is
   `IReadOnlyList<object>`, NOT `IReadOnlyList<dynamic>`. Authoritative
   bases: Dart language reference "The dynamic type"
   (https://dart.dev/language/built-in-types#the-dynamic-type);
   Microsoft Learn "dynamic type" reference
   (https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-dynamic-type);
   Microsoft Learn "When to use dynamic"
   (https://learn.microsoft.com/dotnet/csharp/programming-guide/types/using-type-dynamic).
   Authoritative both sides — no escalation.

3. **`rf-dart-expect-list-equals-to-xunit-assertequal-enumerable`** —
   `expect(<List<T>>, <list-literal>)` (implicit-equals on ordered list
   operands). Distinct from the cached SET-literal idiom
   `rf-dart-set-literal-typed-to-csharp-hashset-initializer` (which is
   MEMBERSHIP, not ORDER). Both sides perform ORDERED element-wise
   equality. Authoritative bases: Dart package:matcher `equals`
   constant
   (https://pub.dev/documentation/matcher/latest/matcher/equals.html);
   xUnit `Assert.Equal(IEnumerable<T>, IEnumerable<T>)`
   (https://learn.microsoft.com/dotnet/api/xunit.assert.equal).

4. **`rf-dart-property-access-to-csharp-property-access`** —
   anchors the `.length → .Count` rule (List<T>.length →
   List<T>.Count, the well-known Dart→C# rename), the `.toString() →
   .ToString()` PascalCase rename, and the general "Dart `<expr>.<lcc>`
   → C# `<expr>.<PascalName>` consult-SUT-convspec-for-target-casing"
   rule. Authoritative bases: Dart core `List.length`
   (https://api.dart.dev/stable/dart-core/List/length.html); .NET
   `List<T>.Count`
   (https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.count);
   `System.Object.ToString`
   (https://learn.microsoft.com/dotnet/api/system.object.tostring).

5. **`rf-dart-doc-comment-to-csharp-xml-doc-comment`** — the single
   `///` doc comment above the `compile` helper. Dart and C# share the
   `///` prefix but diverge in payload shape (Dart markdown-style; C#
   XML-tagged). Authoritative bases: dart.dev "Effective Dart —
   Documentation" (https://dart.dev/effective-dart/documentation);
   Microsoft Learn "Recommended XML tags for C# documentation comments"
   (https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags).

6. **`rf-dart-line-comment-to-csharp-line-comment`** — trivial.
   `// <text>` is identical both sides; recorded for KB completeness so
   downstream files do not re-research. Authoritative bases: Dart
   language tour "Comments"
   (https://dart.dev/language/comments); C# language reference
   "Comments"
   (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/comments).

### Sibling-group topology (five independent classes)

This file has FIVE SIBLING top-level groups — same topology as
`module_parser_test.dart.md` (which uses six sibling groups). No outer
group exists; no `setUp` appears anywhere in `main`; the per-group test
classes share no state. The cleanest target shape is one C# `public
class` per Dart `group`, all in the same `.cs` file. Multi-class-per-file
is fully supported by xUnit's reflection-based test discovery
(https://xunit.net/docs/getting-started/v3/getting-started). The
file-scope `compile` helper does NOT change this — it lives in a SIXTH
`internal static class ModuleCompilerTestHelpers` sibling to the five
test classes.

### Shared file-scope helper — the only non-trivial topology decision

The Dart top-level function `compile(String source)` is called by 8 of
the 13 tests across 3 sibling groups (Static, Dynamic, Mixed). Three
options were considered:

(a) **Inline the helper body into every test method** — rejected
because it duplicates 9 lines of arrangement code 8 times and obscures
the test's intent.

(b) **Promote to a per-group instance method** — rejected because the
helper is GROUP-AGNOSTIC (used identically by Static / Dynamic / Mixed
groups); duplicating it across three classes is worse than (a).

(c) **Sibling `internal static class ModuleCompilerTestHelpers`** —
SELECTED. Single point of truth; trivially reusable across groups; the
canonical C# idiom for shared file-scope test helpers per Microsoft
naming guidelines. The `internal` modifier limits visibility to the
test assembly (matching the Dart implicit-library-scope of an
unprefixed top-level function).

### `List<dynamic>` → `IReadOnlyList<object>` — the well-known footgun

The MOST important nuance in this file is the Dart-`dynamic` /
C#-`dynamic` distinction. The two keywords look identical and would
silently typecheck — but `C# dynamic` activates the DLR (Dynamic
Language Runtime) for runtime-bound member dispatch, which:

- has measurable per-access overhead (DLR cache lookup),
- breaks IDE refactoring tools (member references are invisible to
  Find Usages),
- defers type errors that the static type-checker would catch.

For the helper's use site — storing heterogeneous opcode instances and
filtering by RUNTIME TYPE via `OfType<T>()` — the C# type that captures
"heterogeneous list of reference values" is `object`, NOT `dynamic`.
`object` is the C# heterogeneous-list root and supports the
`OfType<T>()` LINQ extension naturally (which itself filters via
runtime type-check on `object` references). This idiom is recorded as
FIRST-SEEN row #2 above because the Dart-`dynamic` / C#-`dynamic`
distinction is a project-wide concern that future convspecs will
revisit (e.g. Dart `dynamic` parameters in interpreter dispatch code).

### `whereType<T>()` → `OfType<T>()` — LINQ equivalence

Dart `Iterable<T>.whereType<R>()` and C# LINQ `IEnumerable<T>.OfType<TResult>()`
are documented EQUIVALENTS — both filter by runtime type, skip non-T
elements, return a deferred lazy sequence, and materialise on a
terminal operator (`.toList()` ↔ `.ToList()`). The cached idiom
`rf-dart-iterable-where-to-linq` from `lib/lint/linter.dart.md` and
`lib/analysis/analysis_phase.dart.md` is applied verbatim. The
alternative `Cast<T>().ToList()` is observably weaker (`Cast` THROWS on
non-T elements rather than skipping); `OfType<T>().ToList()` is the
literal, semantics-preserving translation.

### Implicit-equals on `bool` operands — the two `expect(contains, true/false)` calls

Lines 74–75 use the implicit-equals shorthand with a `bool` literal:
`expect(table.contains('math'), true);` and `expect(table.contains(
'io'), false);`. Both translate literally to `Assert.Equal(true,
table.Contains("math"))` / `Assert.Equal(false, table.Contains("io"))`
under the cached `rf-dart-expect-equals-to-xunit-assertequal` idiom.
The more idiomatic xUnit shapes `Assert.True(...)` / `Assert.False(...)`
are observably equivalent; precedent boot_loader_test.dart.md uses the
literal `Assert.Equal` shape and this spec inherits it for mechanical
fidelity and review traceability. Either form satisfies the contract.

### Why no escalations (FR-013)

Every construct has a clear, single-decision target shape grounded in
official Dart and .NET documentation. The "soft" decisions
(one-class-per-group vs FLATTEN; file-scope helper placement;
`Assert.Equal(bool, bool)` vs `Assert.True/False`; `OfType<T>` vs
`Where(o => o is T)`) are documented project-wide policy
(corroborating alternatives recorded in the relevant research findings),
not unresolved choices. The Dart-`dynamic` / C#-`dynamic` distinction is
a well-known Dart→C# nuance with an authoritative resolution. No
construct involves an idiom-vs-research conflict or an idiom-vs-idiom
conflict, and nothing is undecidable. `escalations: []` is therefore
intentional, not a placeholder.
