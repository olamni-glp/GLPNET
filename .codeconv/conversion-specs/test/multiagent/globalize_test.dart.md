> Conversion-spec artifact for test/multiagent/globalize_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/globalize_test.dart
source_sha256: 835b084ec2a497797993bffd3264943b83bceec139165e4852f959bda15fb3be
target_code_unit: test/multiagent/GlobalizeTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the project-wide test
      framework already pinned by the prior test-file specs
      (test/smoke_test.dart.md, test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md,
      test/multiagent/global_writers_table_test.dart.md). THIS file MUST
      reuse that idiom verbatim (FR-012 / SC-007) — no re-research. The
      .NET test project (.csproj — out of this single-file artifact's
      scope) provides `xunit` + `xunit.runner.visualstudio` +
      `Microsoft.NET.Test.Sdk` NuGet references. Codegen also adds
      `using System.Collections.Generic;` at file scope because the test
      body materialises `List<TermVar>` literals (see
      dart.expression.list_literal_typed below), and projects to a
      namespace mirroring the Dart `test/multiagent` directory
      (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest are recorded alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `expect`, `isEmpty`) re-exported via the one import; xUnit has NO
      top-level test functions — tests are public instance methods on a
      public class discovered via `[Fact]` reflection. No async / Future /
      Stream / isolate surface in this file. This file introduces no new
      framework-level surface beyond what the four sibling test specs
      already pin.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: >-
      "import 'package:glp_runtime/multiagent/global_writers_table.dart';
       import 'package:glp_runtime/multiagent/mad_helpers.dart';"
    target_decision: >-
      Both imports are SUT (system-under-test) references — Dart
      `package:glp_runtime/...` URIs that resolve to the converted C#
      namespace for the same source units. Replace each with a C# `using`
      directive that names the namespace the converted
      `global_writers_table.dart` and `mad_helpers.dart` will emit into,
      e.g. `using <RootNs>.Multiagent;` (both SUT files emit into the same
      sub-namespace under the multiagent directory — one `using` covers
      both). The exact namespace string is determined by each SUT file's
      own conversion-spec (siblings
      `.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`
      and `.../lib/multiagent/mad_helpers.dart.md`, produced separately);
      this test-file spec records the DEPENDENCY relationship. Codegen
      MUST emit `using`s that resolve the symbols this test references:
      `GlobalWritersTable` (the class), `GlobalName`,
      `GlobalName.writer`/`GlobalName.reader` (named constructors —
      mapped per dart.class.named_constructor_factory below), `TermVar`,
      `TermVar.writer`/`TermVar.reader` (named constructors), and the
      free function `globalize(...)` plus its return shape `GlobalizeResult`
      (with members `globalNames`, `spawns`). Per-file working-directory
      convention from feature 016/017 (`<file>__/`) means the SUT and
      test live in sibling working dirs; the `using` resolves through
      the test .csproj's project-reference to the runtime .csproj
      (langpair-level concern, OUT OF SCOPE here — recorded for codegen
      cross-file wiring).
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed): a `package:`
      import that resolves to an in-repo Dart library (NOT to a pub.dev
      third-party package) maps to a C# `using <Namespace>;` that
      targets the OUTPUT namespace of the converted Dart library — NOT a
      separate NuGet reference. Distinguish by inspecting the `package:`
      URI prefix against the host repo's `pubspec.yaml` `name:` (here,
      `glp_runtime`). Project-file wiring (`<ProjectReference>` from the
      test .csproj to the runtime .csproj) is langpair/project-skeleton
      level, recorded so codegen knows the `using` alone is insufficient
      without the project reference. Free-function nuance (load-bearing,
      NEW for this file vs the sibling test specs which only imported
      classes): Dart's top-level `globalize(...)` function — defined at
      library scope in `mad_helpers.dart` — has no direct C# equivalent
      (C# has no top-level functions outside of top-level-statement
      Program.cs). Per the cross-cutting top-level-function idiom
      rf-dart-toplevel-function-to-csharp-static-method (recorded by
      `lib/multiagent/mad_helpers.dart.md`, sibling SUT spec) the
      function will land as a public static method on a static class —
      likely `MadHelpers.Globalize(...)` — and the `using` here gives
      access via the unqualified call `Globalize(...)` IF codegen emits
      `using static <RootNs>.Multiagent.MadHelpers;`, OR via
      `MadHelpers.Globalize(...)` otherwise. Pinning that choice is the
      SUT spec's responsibility; this test spec records the dependency.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Globalize', () { test(...); ... }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The single `group(...)` call inside `main` becomes the
      enclosing test class (next construct).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` runs once per
      test-file process and registers tests; xUnit has no per-file hook —
      only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly one
      `group()` call with no other statements, so omitting `main` is
      lossless. No `setUp` / `setUpAll` / `tearDown` / `tearDownAll`
      anywhere in this file, so no constructor or `IDisposable.Dispose`
      content is needed — identical to global_writers_table_test.dart.
  - construct_key: dart.package_test.group_block
    source_form: "group('Globalize', () { test(...); test(...); test(...); test(...); test(...); });"
    target_decision: >-
      The Dart `group('Globalize', body)` maps to a `public class
      GlobalizeTests` whose name encodes the group label in PascalCase
      with the conventional `Tests` suffix. The original label MAY be
      preserved via `[Trait("Group", "Globalize")]` on the class for
      reporter parity. No nested `group(...)` and no `setUp`/`tearDown`
      inside the group — each test constructs its own
      `GlobalWritersTable` instance and its own `variables` list locally,
      so xUnit's per-test fresh-instance lifecycle ("xUnit.net creates a
      new instance of the test class for every test that is run") maps
      cleanly with NO shared state and NO constructor-side fixture
      needed.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance: the Dart group label `'Globalize'` is already
      a valid C# identifier; mangle is trivial (append `Tests`). Per-test
      labels in this file ('writer variable: creates entry, no spawn',
      'reader variable: spawns global_send info, no entry',
      'mixed term: correct handling of both', 'nested structure:
      recursive globalization', 'index allocation is sequential') contain
      spaces, colons, and one underscore-bearing identifier
      (`global_send`); the per-test method-name mangling strips
      non-identifier characters and PascalCases — see next construct.
      Lifecycle nuance: nothing in this file's group needs the
      `setUp`-to-constructor mapping; the idiom record still applies
      project-wide.
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('<label>', () { /* Given/When/Then with executable
      arrange-act-assert */ });" — applied to all 5 test cases in this
      file (none use `skip:`).
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument) becomes a
      `public void` method on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. Method name = label
      PascalCased with non-identifier chars stripped:
      `'writer variable: creates entry, no spawn'` ->
      `WriterVariableCreatesEntryNoSpawn`;
      `'reader variable: spawns global_send info, no entry'` ->
      `ReaderVariableSpawnsGlobalSendInfoNoEntry`;
      `'mixed term: correct handling of both'` ->
      `MixedTermCorrectHandlingOfBoth`;
      `'nested structure: recursive globalization'` ->
      `NestedStructureRecursiveGlobalization`;
      `'index allocation is sequential'` ->
      `IndexAllocationIsSequential`. Method body translates the Dart
      arrange-act-assert verbatim, with `expect(actual, matcher)` calls
      routed to xUnit `Assert.*` per the matcher-routing idioms below.
      The Given/When/Then comments (which carry "Spec Section 5.1",
      "Spec Section 5.3", "Spec Section 3.2" references) MUST be carried
      into the target as a `/// <summary>` doc-comment block per method
      so spec traceability survives the conversion (FR-024 doc-level).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every
      `test` callback in this file is synchronous (no `async`/`Future`/
      `await`); target method returns `void` (xUnit also supports
      `async Task` for async tests — not applicable here). Closure-
      capture nuance: no `setUp` variables — every `final table = ...`
      and `final variables = [...]` is local to the test body, mapping
      1-to-1 to local `var table = ...` / `var variables = ...` in the
      C# method (see dart.expression.final_local_variable_with_initializer
      and dart.expression.list_literal_typed). Skip-semantics nuance
      (NOT firing here): no `skip:` argument anywhere, so NO `Skip=`
      property on `[Fact]` — contrast with mad_error_handling_test.dart.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final table = GlobalWritersTable('p');
       final variables = [TermVar.writer(100, readerAddr: 101)];
       final result = globalize(variables: ..., localAgent: 'p', ...);
       final entry = table.lookupByIndex(1);"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a constructor invocation, a method call,
      or a list literal whose static type is inferable. Specifically:
      `final table = GlobalWritersTable('p')` ->
      `var table = new GlobalWritersTable("p");` (note the mandatory C#
      `new` keyword — Dart's optional-`new` constructor call requires
      C#'s explicit `new`; Dart single-quote string `'p'` -> C# double-
      quote `"p"`); `final variables = [...]` -> `var variables = new
      List<TermVar> { ... };` (see dart.expression.list_literal_typed);
      `final result = globalize(...)` ->
      `var result = Globalize(...)` (assuming `using static` form per
      the SUT-import nuance above; otherwise `MadHelpers.Globalize(...)`);
      `final entry = table.lookupByIndex(1)` ->
      `var entry = table.LookupByIndex(1);` (the return is nullable —
      see dart.expression.null_assertion_bang_operator).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (explicitly addressed): Dart
      `final <local>` prevents REBINDING the local after init but does
      NOT prevent mutation of the referenced object's state — exactly
      the same semantics as C# `var`. C# 7+ has no `readonly` modifier
      for locals; conversion accepts this minor semantic loss (sibling
      global_writers_table_test.dart.md records the same trade-off).
      Constructor-syntax nuance: Dart allows `Foo(...)` without `new`;
      C# requires `new Foo(...)`. String-literal nuance: Dart `'p'` and
      `"p"` are equivalent; C# uses ONLY `"..."` (single quotes are
      `char`). Codegen MUST emit `new GlobalWritersTable("p")` —
      `new GlobalWritersTable('p')` would select a non-existent
      `char`-arg constructor.
  - construct_key: dart.class.named_constructor_factory
    source_form: >-
      "TermVar.writer(100, readerAddr: 101)
       TermVar.reader(201, writerAddr: 200)
       GlobalName.writer('p', 1)
       GlobalName.reader('p', 2)"
    target_decision: >-
      Dart's NAMED CONSTRUCTORS (`ClassName.namedCtor(...)`) — used
      heavily in this file's arrange/assert lines for both `TermVar` and
      `GlobalName` — have NO direct C# equivalent (C# only supports
      multiple positional constructors disambiguated by parameter
      signature, plus optional STATIC FACTORY methods). The pinned
      mapping (cross-cutting idiom — first-seen here, recorded for
      reuse) is: Dart `Foo.bar(args)` -> C# `Foo.Bar(args)` STATIC
      FACTORY METHOD on the converted class. So `TermVar.writer(100,
      readerAddr: 101)` -> `TermVar.Writer(100, readerAddr: 101)`
      (NB: also a named argument — see next construct);
      `TermVar.reader(201, writerAddr: 200)` ->
      `TermVar.Reader(201, writerAddr: 200)`; `GlobalName.writer('p', 1)`
      -> `GlobalName.Writer("p", 1)`; `GlobalName.reader('p', 2)` ->
      `GlobalName.Reader("p", 2)`. The factory method name is the
      named-constructor identifier PascalCased per the cross-cutting
      C# member-naming idiom. The SUT spec
      (`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`)
      is the source of truth for the exact static-factory signature
      emitted; THIS test spec records the call-site shape and pins the
      mapping.
    idiom_id: null
    research_finding_id: rf-dart-named-constructor-to-csharp-static-factory
    nuance: >-
      Constructor-semantics nuance (explicitly addressed, NOT glossed):
      Dart named constructors are still CONSTRUCTORS — they go through
      the same object-allocation+initialization pipeline as the primary
      constructor and can chain via `: super(...)` or `: this(...)`. C#
      static factories are METHOD CALLS that internally `return new
      Foo(...)`. The semantic gap that matters here is around
      sub-classing (a named ctor can be inherited / overridden in
      certain shapes; a static method cannot) — for `TermVar` and
      `GlobalName` in this file, both are SEALED data classes with no
      subclasses in the Dart source, so the gap is benign. The
      ALTERNATIVE C# encoding — multiple constructor overloads
      disambiguated by parameter type — was rejected because
      `TermVar.writer(int, {int readerAddr})` and `TermVar.reader(int,
      {int writerAddr})` differ ONLY by the named-parameter LABEL, not
      the type signature, so two `(int, int)` constructors would
      conflict. Static factories on the same type sidestep the
      ambiguity. Pinned mapping: named-ctor -> PascalCase static
      method on the same class returning `new ClassName(...)`.
      Initializer-list nuance: any `: field = expr` initializer-list
      content the Dart named ctor uses internally is the SUT spec's
      problem to encode (likely as private field assignments in the
      factory body); the test-call-site shape is unaffected.
  - construct_key: dart.expression.named_argument_in_invocation
    source_form: >-
      "TermVar.writer(100, readerAddr: 101)
       TermVar.reader(201, writerAddr: 200)
       globalize(variables: variables, localAgent: 'p', remoteAgent: 'q',
                 table: table)"
    target_decision: >-
      Dart NAMED ARGUMENTS (`name: value` at call site, with the
      callee's parameter declared either `{required Type name}` or
      `{Type name = default}`) map to C# NAMED ARGUMENTS (`name:
      value` at call site, with the callee's parameter declared as an
      ordinary parameter — optionally with a default value). The C#
      naming convention requires the parameter name to be camelCase
      (e.g. `readerAddr`, `writerAddr`, `variables`, `localAgent`,
      `remoteAgent`, `table`) — IDENTICAL to the Dart spelling. So
      `TermVar.writer(100, readerAddr: 101)` ->
      `TermVar.Writer(100, readerAddr: 101)`;
      `globalize(variables: variables, localAgent: 'p', remoteAgent:
      'q', table: table)` ->
      `Globalize(variables: variables, localAgent: "p", remoteAgent:
      "q", table: table)`. The C# parameter is declared on the static
      factory / static method (see SUT spec) as e.g.
      `public static TermVar Writer(int addr, int readerAddr)` —
      positional `int addr` first, then `int readerAddr` (no `:`
      keyword on the declaration; the colon is ONLY at the call site).
    idiom_id: null
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Required-vs-optional nuance (explicitly addressed): Dart
      `{required Type name}` is COMPILE-TIME-MANDATORY at every call
      site; C# named arguments are by default OPTIONAL at the call
      site (the caller may pass positionally OR by name). To preserve
      the "must be supplied" guarantee that Dart `required` provides,
      the C# parameter must NOT have a default value — leaving it as
      `int readerAddr` (no `= default`) forces the caller to supply
      a value, which is the same end-state (compile-time required).
      Order-independence nuance: Dart named arguments may appear in
      ANY order at the call site, independently of the declared
      parameter order; C# named arguments may also appear in any
      order. This file relies on the order independence implicitly
      (positional `int addr` first, then named `readerAddr:` / 
      `writerAddr:`) — codegen preserves the call-site order exactly
      as written in the Dart source. Naming-convention nuance: Dart
      parameter names are camelCase (e.g. `readerAddr`); C#
      convention for PARAMETER names is also camelCase (NOT
      PascalCase — PascalCase is for public members like methods /
      properties / types). So the name `readerAddr` carries over
      VERBATIM; this is a non-obvious carve-out from the general
      Dart-member-name-to-C#-PascalCase rule that the test-file
      specs have pinned for getters/methods.
  - construct_key: dart.expression.list_literal_typed
    source_form: >-
      "final variables = [TermVar.writer(100, readerAddr: 101)];
       final variables = [TermVar.writer(100, readerAddr: 101),
                          TermVar.reader(201, writerAddr: 200)];
       final variables = [TermVar.writer(100, readerAddr: 101),
                          TermVar.writer(200, readerAddr: 201),
                          TermVar.reader(301, writerAddr: 300)];"
    target_decision: >-
      Dart list literals `[a, b, c]` whose static element type is
      inferred (here, `List<TermVar>` because every element is a
      `TermVar` factory call) map to C# `new List<TermVar> { a, b, c }`
      (collection-initializer syntax on `System.Collections.Generic.
      List<T>`). The `using System.Collections.Generic;` at file
      scope (see dart.package_test.import_directive nuance) makes
      `List<TermVar>` resolvable. Element calls are themselves
      converted per dart.class.named_constructor_factory above. The
      ALTERNATIVE `new[] { ... }` (a C# array literal of type
      `TermVar[]`) is REJECTED because the `globalize` function's
      `variables` parameter is `List<TermVar>` (NOT `IEnumerable<TermVar>`
      or `TermVar[]`); the converted SUT signature
      `public static GlobalizeResult Globalize(List<TermVar> variables,
      ...)` requires the concrete `List<TermVar>` instance, not an
      array. (If the SUT spec instead converts the param to
      `IReadOnlyList<TermVar>` or `IEnumerable<TermVar>`, either array
      or list literal would work — but the conservative `List<TermVar>`
      mirror of Dart's `List` carries over literally per the
      `rf-dart-list-to-csharp-list-generic` cross-cutting idiom
      pinned by the SUT-side specs.)
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Collection-type nuance (explicitly addressed): Dart `List<T>` is
      growable by default (Dart `<T>[]` is a `GrowableList<T>`), so
      mapping to C# `List<T>` (which is also growable) preserves the
      runtime characteristic. If the Dart source had used `const [...]`
      (compile-time-constant immutable list — NOT the case here), the
      C# equivalent would be `ImmutableList<T>.Create(...)` from
      `System.Collections.Immutable`; THIS file uses plain runtime
      list literals so the `new List<T> { ... }` form is correct.
      Element-equality nuance: not asserted as a whole-list compare in
      this file (no `expect(variables, equals([...]))`); only individual
      `globalNames[i]` accesses are compared (see
      dart.expression.index_access below), so the collection-equality
      idiom from boot_loader_test.dart.md
      (`rf-dart-list-equality-to-xunit-assertequal-collection`) is NOT
      exercised here.
  - construct_key: dart.expression.index_access
    source_form: >-
      "result.globalNames[0]
       result.globalNames[1]
       result.globalNames[2]
       result.spawns[0]"
    target_decision: >-
      Dart indexer access `<list>[i]` on a `List<T>` maps DIRECTLY to
      C# indexer access `<list>[i]` on a `List<T>` (same syntax, same
      0-based semantics, same `IndexOutOfRangeException` /
      `ArgumentOutOfRangeException` behaviour on out-of-bounds). The
      member-naming idiom renames `globalNames` -> `GlobalNames` and
      `spawns` -> `Spawns` (Dart lowerCamelCase public-field/getter
      -> C# PascalCase public property / read-only field), per the
      cross-cutting `rf-dart-getter-to-csharp-property` /
      `rf-dart-public-field-to-csharp-property` idiom pinned by
      sibling SUT specs. So `result.globalNames[0]` ->
      `result.GlobalNames[0]`; `result.spawns[0]` -> `result.Spawns[0]`.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Bounds-check nuance (explicitly addressed): Dart `<list>[i]`
      throws `RangeError` on out-of-bounds (runtime); C# `List<T>[i]`
      throws `ArgumentOutOfRangeException` (runtime). Both fail at
      runtime — preserved exactly. No `?[i]` (null-aware index) used
      in this file. Member-rename nuance: this construct ALSO carries
      the Dart-public-field/getter -> C# property renaming, but the
      cross-cutting member-naming idiom is the authority for that
      mapping; this row only records the indexer-access portion.
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(result.spawns, isEmpty);"
    target_decision: >-
      Dart `expect(<collection>, isEmpty)` (using the
      `package:matcher` `isEmpty` constant) maps to xUnit
      `Assert.Empty(<collection>)`. Used once in this file (line 44).
      `Assert.Empty(IEnumerable)` throws `EmptyException` if the
      enumerable yields any element. Translate
      `expect(result.spawns, isEmpty)` ->
      `Assert.Empty(result.Spawns);` (the `Spawns` property is
      `List<SpawnInfo>` per the SUT spec; `List<T>` implements
      `IEnumerable<T>`).
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Emptiness-semantics nuance (explicitly addressed): Dart
      `isEmpty` matches any object with an `isEmpty` getter that
      returns `true` (works for `Iterable`, `Map`, `String`); xUnit
      `Assert.Empty` accepts `IEnumerable` and `string` (via
      separate overloads). For `List<T>` -> `IEnumerable<T>` the
      semantics are identical: both check "no elements". Diagnostic
      message: xUnit's `Assert.Empty` produces "Assert.Empty()
      Failure: Collection was not empty" on failure; Dart's matcher
      produces "Expected: empty / Actual: [...]" — minor
      diagnostic-quality difference, accepted (same trade-off
      smoke_test.dart and global_writers_table_test.dart accepted
      for other matcher rows).
  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: "expect(entry, isNotNull);"
    target_decision: >-
      `expect(x, isNotNull)` -> `Assert.NotNull(x);` per the
      matcher-routing table pinned by smoke_test.dart and
      global_writers_table_test.dart. Used 2x in this file (lines 38
      and 102). xUnit `Assert.NotNull(object?)` throws
      `NotNullException` on null, otherwise passes — strict
      null-vs-not-null semantics identical to Dart `isNotNull`.
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart `isNotNull`
      matches any non-null value (including `false`, `0`, empty
      string — Dart has no truthiness coercion); xUnit
      `Assert.NotNull(object?)` is identically strict. NRT-flow nuance:
      after `Assert.NotNull(entry)`, the C# flow-analyzer narrows
      `entry` to non-nullable ONLY if xUnit's `Assert.NotNull` is
      annotated with `[NotNull]` (xUnit >= 2.5 does this). For older
      xUnit, the converted code must use the null-forgiving operator
      `entry!.WriterAddr` at the subsequent dereference — which is
      what the Dart source already does on line 38 -> line 39 (
      `expect(entry, isNotNull); expect(entry!.writerAddr, 100);`),
      so the conversion mirrors the source bang verbatim.
  - construct_key: dart.package_test.expect_equals_implicit_matcher
    source_form: >-
      "expect(result.globalNames.length, 1);
       expect(result.globalNames[0], GlobalName.writer('p', 1));
       expect(entry!.writerAddr, 100);
       expect(entry.remoteAgent, 'q');
       expect(table.globalizeEntryCount, 1);
       expect(table.globalizeEntryCount, 0);
       expect(table.nextIndex, 2);
       expect(table.nextIndex, 4);
       expect(result.spawns.length, 1);
       expect(result.spawns[0].readerAddr, 200);
       expect(result.spawns[0].globalName, GlobalName.reader('p', 1));
       expect(result.spawns[0].destAgent, 'q');
       (and the analogous lines for the mixed-term, nested-structure,
        and index-allocation tests)"
    target_decision: >-
      Dart `expect(actual, value)` where the second argument is a
      non-matcher value (bare value rather than a `Matcher`) is sugar
      for `expect(actual, equals(value))` — the
      `package:test`/`package:matcher` rule auto-wraps bare values in
      `equals(...)`. Translate to `Assert.Equal(expected, actual);`
      with the EXPECTED value FIRST and the ACTUAL second — the
      argument order is the INVERSE of Dart's `expect(actual,
      equals(expected))`. Codegen MUST swap. Used ~24x in this file.
      Examples:
      `expect(result.globalNames.length, 1)` ->
      `Assert.Equal(1, result.GlobalNames.Count);`
      (NB: Dart's `List.length` -> C# `List<T>.Count` per the
      cross-cutting `rf-dart-list-length-to-csharp-list-count` idiom);
      `expect(result.globalNames[0], GlobalName.writer('p', 1))` ->
      `Assert.Equal(GlobalName.Writer("p", 1), result.GlobalNames[0]);`
      (the expected value is itself a `GlobalName` factory call —
      value-equality semantics depend on `GlobalName.Equals` /
      `GetHashCode` being correctly converted by the SUT spec; see
      nuance);
      `expect(entry.remoteAgent, 'q')` ->
      `Assert.Equal("q", entry.RemoteAgent);`;
      `expect(table.nextIndex, 2)` ->
      `Assert.Equal(2, table.NextIndex);`.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order footgun (explicitly addressed — well-known): Dart
      `expect(actual, equals(expected))` is actual-first; xUnit
      `Assert.Equal<T>(T expected, T actual)` is expected-first.
      Codegen MUST swap; sibling specs pre-flagged this for batch reuse.
      Value-vs-reference nuance (LOAD-BEARING for this file, explicitly
      addressed): the implicit-equals matcher is applied to (a) `int`
      literals (1, 2, 3, 4, 100, 200, 300, 201, 101, 0), (b) `String`
      literals ('p', 'q'), AND (c) `GlobalName` instances (constructed
      via `GlobalName.writer('p', 1)` etc.). The first two map via
      C# value semantics with no extra work. The third — comparing
      `GlobalName` references — REQUIRES the SUT's `GlobalName` class
      to override `Object.Equals(object?)` and `Object.GetHashCode()`
      (or implement `IEquatable<GlobalName>`) so that
      `Assert.Equal(GlobalName.Writer("p", 1), result.GlobalNames[0])`
      performs STRUCTURAL equality (same `type` + `agent` + `index`),
      NOT REFERENCE equality. The Dart source's `GlobalName` ALREADY
      overrides `==` and `hashCode` (lines 47-52 of
      `lib/multiagent/mad_helpers.dart`); the SUT spec MUST carry
      that override into C# as `IEquatable<GlobalName>` +
      `Object.Equals`/`GetHashCode` overrides — recorded here as a
      CROSS-FILE INVARIANT that this test relies on. Codegen ALTERNATIVE
      `Assert.Equal(...)` could be a C# `record class` for `GlobalName`
      (records auto-generate structural equality) — that choice is
      the SUT spec's call; this test spec just records that the SUT
      MUST produce structural equality however it encodes the type.
      Width nuance: per the cross-cutting `rf-dart-int-to-csharp-long-
      width` idiom, Dart `int` -> C# `long` would force `Count` and
      `nextIndex` to `long`; xUnit `Assert.Equal<long>(long, long)`
      handles the int-literal -> long widening implicitly. THIS file's
      literal values (max 4) are well within both ranges. List-length
      idiom: Dart `<list>.length` -> C# `<list>.Count` (PROPERTY rename
      from `length` -> `Count`, NOT the more general getter-to-property
      idiom — the name change is specific to `IList<T>` / `List<T>` /
      arrays). This rename is the SUT-side
      `rf-dart-list-length-to-csharp-list-count` idiom; reused
      verbatim, not re-derived.
  - construct_key: dart.expression.null_assertion_bang_operator
    source_form: "entry!.writerAddr (line 38, line 102 of the source)"
    target_decision: >-
      Dart's null-assertion operator `entry!` (asserts non-null at
      runtime, throws `TypeError` if null) maps to C#'s null-forgiving
      operator `entry!` (compile-time annotation only — does NOT
      throw, just silences the NRT warning). The semantic difference
      is load-bearing and MUST be addressed: in C#, after
      `Assert.NotNull(entry)` on the immediately preceding line, the
      runtime guarantee is already in place (xUnit threw if null);
      the `!` then silences the NRT warning without adding a runtime
      check. Translate `entry!.writerAddr` -> `entry!.WriterAddr`
      (PascalCased property name per member-naming idiom). If
      `Assert.NotNull` were absent before the dereference, codegen
      would emit `entry!.WriterAddr` AND insert an explicit
      `Assert.NotNull(entry);` line to preserve the Dart runtime-throw
      semantics — but in THIS file every `!` usage IS preceded by
      `expect(entry, isNotNull)` on the immediately previous line
      (lines 37+38, 101+102), so no extra assert is needed.
    idiom_id: rf-dart-bang-operator-to-csharp-null-forgiving
    research_finding_id: rf-dart-bang-operator-to-csharp-null-forgiving
    nuance: >-
      Runtime-vs-compile-time nuance (explicitly addressed, NOT
      glossed): Dart `!` is a RUNTIME null-check that throws
      `TypeError` if the operand is null; C# `!` is a COMPILE-TIME
      NRT annotation that emits no runtime code. The semantic gap is
      closed in this file because every `!` follows an
      `Assert.NotNull` (xUnit throws on null). Codegen MUST audit
      each `!` translation against this precondition: if the
      preceding statement is NOT an `Assert.NotNull` of the same
      expression, codegen MUST insert one (or use
      `entry ?? throw new InvalidOperationException()` as the
      runtime-throw equivalent). CONVERSION INVARIANT carried over
      from global_writers_table_test.dart.md verbatim.
  - construct_key: dart.package_test.expect_boolean_getter_implicit
    source_form: >-
      "expect(result.globalNames[0].isWriter, true);
       expect(result.globalNames[1].isReader, true);"
    target_decision: >-
      Dart `expect(<bool>, true)` (bare-value second argument auto-
      wrapped to `equals(true)`) maps to xUnit
      `Assert.True(<bool>)` — NOT `Assert.Equal(true, <bool>)`.
      Rationale: although the implicit-equals matcher idiom would
      technically translate this to `Assert.Equal(true, x)`,
      xUnit's `Assert.True(bool)` is the idiomatic form for boolean
      assertions (better diagnostic message). The implicit-equals
      idiom's nuance row already records `bool` as a special case.
      Translate `expect(result.globalNames[0].isWriter, true)` ->
      `Assert.True(result.GlobalNames[0].IsWriter);` (NB: `isWriter`
      -> `IsWriter` property — per `rf-dart-getter-to-csharp-property`
      idiom, lowerCamelCase getter -> PascalCase property; this
      file's SUT has `bool get isWriter` and `bool get isReader`
      defined on `GlobalName`).
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Boolean-literal coercion nuance (explicitly addressed): Dart
      `expect(x, true)` is INDISTINGUISHABLE from `expect(x, isTrue)`
      at the matcher layer (both end up as `equals(true)`); xUnit's
      `Assert.True(b)` is the canonical idiom for both. Diagnostic
      quality is BETTER with `Assert.True` ("Assert.True() Failure")
      than with `Assert.Equal(true, b)` ("Assert.Equal() Failure:
      Expected: True / Actual: False") — so codegen MUST prefer
      `Assert.True` for bool-typed expressions even when the Dart
      source uses the bare-`true` second argument rather than the
      `isTrue` matcher constant. Getter-to-property nuance: Dart
      `bool get isWriter` is a GETTER (defined as `bool get
      isWriter => type == GlobalNameType.writer;` in the SUT) — the
      C# equivalent is an EXPRESSION-BODIED PROPERTY (`public bool
      IsWriter => Type == GlobalNameType.Writer;`). The SUT spec
      pins the property-vs-method choice; this test spec relies on
      the property form (zero-arg, no parentheses at call site)
      because the Dart source accesses it without parens
      (`result.globalNames[0].isWriter`).
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System.Collections.Generic + <RootNs>.Multiagent for SUT; optionally `using static <RootNs>.Multiagent.MadHelpers;` to allow unqualified `Globalize(...)`)"
  - "cu-2: namespace declaration mirroring test/multiagent path (<RootNs>.Test.Multiagent)"
  - "cu-3: top-level class GlobalizeTests (from group label 'Globalize') with optional [Trait(\"Group\", \"Globalize\")]"
  - "cu-4: 5 [Fact(DisplayName=\"<original label>\")] public void methods, one per Dart test() call, all executable (NO Skip), all with /// <summary> carrying the original Given/When/Then comments + spec-section references (5.1, 5.3, 3.2)"
  - "cu-5: per-method body: arrange-act-assert translation with `var table = new GlobalWritersTable(\"p\")`, `var variables = new List<TermVar> { TermVar.Writer(..., readerAddr: ...), ... }`, `var result = Globalize(variables: variables, localAgent: \"p\", remoteAgent: \"q\", table: table)`, expect() routed to Assert.* per matcher-routing idioms (isEmpty -> Empty, isNotNull -> NotNull, isTrue / boolean-literal -> True, implicit-equals -> Equal-with-arg-swap)"
  - "cu-6: `!` and member-access translations preserved 1-to-1 with the Dart runtime-vs-C#-compile-time-semantics caveat (each `!` precedes an Assert.NotNull on the same expression on the immediately prior line — invariant audited per construct dart.expression.null_assertion_bang_operator)"
  - "cu-7: cross-file dependency on the SUT's structural-equality implementation: `GlobalName` MUST be emitted with IEquatable<GlobalName> + Object.Equals/GetHashCode (or as a `record class`) so Assert.Equal(GlobalName.Writer(\"p\", 1), result.GlobalNames[0]) performs value-equality not reference-equality — recorded as a hard invariant; the SUT spec for lib/multiagent/mad_helpers.dart.md owns the implementation choice"
escalations: []
```

## Rationale + research provenance

### Why all 5 tests are `[Fact]` (NOT `[Fact(Skip=...)]`)

Every `test(...)` call in this file has executable arrange-act-assert
in the body (no `skip:` argument anywhere). Contrast with the sibling
mad_error_handling_test.dart, where all 5 tests are `skip: 'Not yet
implemented'` and map to `[Fact(Skip="Not yet implemented")]`. The
same test-framework-mapping idiom (xUnit, `[Fact]` per Dart
`test()`) applies to both files; the only difference is the absence
of the `Skip=` argument. THIS file therefore reuses the framework
idiom and the test-callback idiom verbatim from the sibling specs
and adds NO new skip-related surface.

### Reuse from sibling test-file specs (FR-012 / SC-007)

Idiom KB reuse (no re-research) per FR-012:

- `rf-dart-package-test-to-dotnet-xunit` — framework choice pinned by
  smoke_test.dart.md, mad_error_handling_test.dart.md,
  boot_loader_test.dart.md, and global_writers_table_test.dart.md.
  xUnit selected as batch-wide default. Authoritative source:
  Microsoft Learn `unit-testing-csharp-with-xunit` + xunit.net docs.
- `rf-dart-test-main-to-xunit-class-with-facts` — drop `main`, lift
  registered tests to `[Fact]` methods on a class.
- `rf-dart-package-test-group-to-xunit-class` — `group(label, body)`
  -> `public class <Label>Tests`.
- `rf-dart-test-callback-to-xunit-method-body` — Dart test callback
  closure becomes the method body of the `[Fact]` method.
- `rf-dart-expect-isTrue-to-xunit-assert-true`,
  `rf-dart-expect-isNotNull-to-xunit-assert-notnull`,
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder` —
  matcher-routing rows pinned by smoke_test.dart and
  global_writers_table_test.dart.
- `rf-dart-package-sut-import-to-csharp-using` — in-repo `package:` ->
  `using <Namespace>;`.
- `rf-dart-final-local-to-csharp-var-local` — `final <local>` ->
  `var <local>`.
- `rf-dart-bang-operator-to-csharp-null-forgiving` — `entry!.x` ->
  `entry!.X` with the runtime-vs-compile-time invariant.

All reused verbatim. SC-007 (≥95% recurring constructs via recorded
idiom) is satisfied.

### New idioms first-recorded by this file (NEW research findings)

This file introduces FOUR new construct kinds not covered by the
sibling test specs:

- `rf-dart-named-constructor-to-csharp-static-factory`: Dart's
  `ClassName.namedCtor(args)` syntax (used for `TermVar.writer`,
  `TermVar.reader`, `GlobalName.writer`, `GlobalName.reader`)
  has no direct C# equivalent. Authoritative Dart side:
  dart.dev language tour `constructors`
  (`https://dart.dev/language/constructors#named-constructors`).
  Authoritative .NET side: Microsoft Learn `static-methods` /
  `Factory pattern` (no single doc — the mapping is conventional;
  the static-factory shape is the idiomatic equivalent across the
  .NET ecosystem, e.g. `DateTime.Parse`, `Tuple.Create`,
  `ImmutableList.Create`). Conventional within .NET; the recording
  pins the choice for batch reuse.
- `rf-dart-named-argument-to-csharp-named-argument`: Dart's
  `name: value` call-site form (with `{required Type name}` or
  `{Type name = default}` declaration) maps 1-to-1 to C# named
  arguments (`name: value` at the call site, with the parameter
  declared as an ordinary parameter). Authoritative Dart side:
  dart.dev language tour `named parameters`
  (`https://dart.dev/language/functions#named-parameters`).
  Authoritative .NET side: Microsoft Learn `Named and Optional
  Arguments`
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`).
  Both sides authoritative. The "Dart `required` -> C# no-default"
  rule is the load-bearing nuance.
- `rf-dart-list-literal-to-csharp-list-initializer`: Dart list
  literals `[a, b, c]` -> C# `new List<T> { a, b, c }`.
  Authoritative Dart side: dart.dev language tour
  `collection literals`
  (`https://dart.dev/language/collections#lists`).
  Authoritative .NET side: Microsoft Learn `Object and Collection
  Initializers`
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers`).
  Both sides authoritative. The `const [...]` -> `ImmutableList<T>`
  carve-out (not used in this file) is recorded for batch reuse.
- `rf-dart-list-indexer-to-csharp-list-indexer`: Dart `<list>[i]`
  -> C# `<list>[i]`. Authoritative Dart side: dart.dev
  `Lists` (`https://api.dart.dev/dart-core/List/operator_get.html`).
  Authoritative .NET side: Microsoft Learn `List<T>.Item`
  (`https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.item`).
  Direct 1-to-1 syntactic + semantic mapping. Both sides
  authoritative.
- `rf-dart-expect-isEmpty-to-xunit-assert-empty`: Dart `isEmpty`
  matcher -> xUnit `Assert.Empty`. Authoritative Dart side:
  pub.dev `package:matcher` `isEmpty` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isEmpty-constant.html`).
  Authoritative .NET side: xunit.net `Assert.Empty` API
  reference. Both sides authoritative. Strict-emptiness semantics
  identical.

### Cross-file invariant: structural equality on `GlobalName`

The implicit-equals matcher row applies `Assert.Equal` to
`GlobalName` instances constructed via the `GlobalName.Writer` /
`GlobalName.Reader` static factories. For
`Assert.Equal(GlobalName.Writer("p", 1), result.GlobalNames[0])` to
perform STRUCTURAL equality (same `Type` + `Agent` + `Index`) rather
than REFERENCE equality, the SUT's `GlobalName` class MUST override
`Object.Equals(object?)` + `Object.GetHashCode()` OR implement
`IEquatable<GlobalName>` OR be emitted as a `record class`. The Dart
source already overrides `==` and `hashCode` (lines 47-52 of
`lib/multiagent/mad_helpers.dart`); the SUT spec
(`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`,
produced separately) owns the implementation-choice pinning. THIS
test spec records the dependency as a hard invariant — without it
every implicit-equals assertion over `GlobalName` in this file
would silently degrade to reference comparison and ALL 5 tests
would fail in the converted C#. Recorded under conversion-unit cu-7.

### Why no escalations

Every construct in this file is authoritative-supported on both
sides. The matcher routing table is mostly already pinned by sibling
test specs; the one new matcher row (`isEmpty`) cites official Dart
matcher documentation and xUnit API documentation. The new
construct rows (`named-constructor` -> static factory,
`named-argument` -> named argument, `list-literal` -> list
initializer, `list-indexer` -> indexer) each have authoritative
docs on both sides. The two null-aware operators (`!`, member
access) reuse already-pinned idioms. The SUT-import mapping is
straightforward in-repo cross-file wiring. The structural-equality
cross-file invariant is recorded as a dependency on the SUT spec,
not an unresolved decision. NO idiom-vs-research conflict, NO
idiom-vs-idiom conflict, NOTHING undecidable. The `escalations: []`
is intentional, not a placeholder.

### Cross-file dependency note

The SUT spec
(`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`,
produced separately) is the source of truth for: the exact namespace,
the emitted shape of `TermVar` and `GlobalName` (static-factory vs
record-class; structural-equality implementation), the
`globalize(...)` -> `Globalize(...)` static-method placement, and
the `GlobalizeResult` / `SpawnInfo` value shapes (member visibility,
property naming). THIS test spec records the call-site dependencies
without pinning the SUT's internal choices. Codegen wiring joins
the two specs at the project-skeleton level (langpair / 016-init
scope, OUT OF this single-file artifact).

### Spec-section traceability preserved

The Dart source documents 9 spec-section references in inline
comments (Spec Sections 3.2, 5.1, 5.3). Each must be carried into
the corresponding C# method's `/// <summary>` XML-doc block — this
is the spec-only-no-guessing discipline (FR-013/023) at the
doc-comment level: the conversion preserves the invariant-tracing
the test file documents, even though the doc-comment block is
non-executable. NOT a separate construct row because it is uniform
across all 5 tests and falls under the test-callback idiom's
already-recorded `/// <summary>` carry-over requirement.
