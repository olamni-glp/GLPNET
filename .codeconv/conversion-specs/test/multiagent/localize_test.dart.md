> Conversion-spec artifact for test/multiagent/localize_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/localize_test.dart
source_sha256: 18f65f67b39c84df4e9b09b357301bb42bfb69b6b2660592ef2a251ee8976ec7
target_code_unit: test/multiagent/LocalizeTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the batch-wide test
      framework already pinned by the sibling test-file specs
      (test/smoke_test.dart.md, test/multiagent/global_writers_table_test.dart.md,
      test/multiagent/mad_error_handling_test.dart.md, and
      test/multiagent/boot_loader_test.dart.md); THIS file MUST reuse that
      idiom verbatim (FR-012 / SC-007) — no re-research. The .NET test
      project (.csproj, langpair-level concern, OUT OF SCOPE here) provides
      the `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`
      NuGet refs. Codegen also adds `using System;` at file scope (no
      exception-typed asserts in THIS file, but the namespace is referenced
      by future maintenance edits) and projects to a single namespace
      mirroring the Dart `test/multiagent` directory (e.g.
      `<RootNs>.Test.Multiagent`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit and MSTest are recorded alternatives in
      the research-finding row but are NOT used here. The full
      `package:test`-to-xUnit shape mapping (import drop + class-with-Facts
      + matcher routing table) is detailed in the sibling test-file specs
      and reused verbatim — this file introduces NO new framework-level
      surface (no setUp/tearDown, no setUpAll/tearDownAll, no skip, no
      tags, no async). Module/namespace nuance: Dart's `package:test`
      exposes top-level functions (`group`, `test`, `expect`, `isEmpty`)
      re-exported via the one import; xUnit has NO top-level test
      functions — tests are public instance methods on a public class
      discovered via `[Fact]` reflection. No async/Future/Stream/isolate
      surface in this file.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: >-
      "import 'package:glp_runtime/multiagent/global_writers_table.dart';"
      and
      "import 'package:glp_runtime/multiagent/mad_helpers.dart';"
    target_decision: >-
      Both imports are SUT (system-under-test) references — the Dart
      `package:glp_runtime/...` URI resolves to the converted C# namespace
      for the same source units. Replace each with a C# `using` directive
      that names the namespace the converted Dart libraries emit into,
      e.g. `using <RootNs>.Multiagent;` (BOTH `global_writers_table.dart`
      and `mad_helpers.dart` live under the `lib/multiagent/` Dart
      subtree, so they share one converted namespace under the
      cross-cutting Dart-directory-to-C#-namespace idiom). The exact
      namespace string is fixed by the SUT files' own conversion-specs
      (`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`
      and `.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`,
      produced separately); THIS test-file spec records the DEPENDENCY
      relationship — codegen MUST emit a `using` that resolves the
      symbols `GlobalWritersTable` (class), `GlobalName` (class with
      static factory methods `Writer`/`Reader`), `LocalizeResult`
      (class with `FreshPairs`/`Spawns`/`UseReader`), `FreshPair`
      (class with `WriterAddr`/`ReaderAddr`), `SpawnInfo` (class with
      `ReaderAddr`/`GlobalName`/`DestAgent`), and the top-level function
      `localize(...)` — since the test body calls
      `GlobalWritersTable('q')`, `GlobalName.writer(...)`,
      `GlobalName.reader(...)`, `localize(globalNames: ..., localAgent:
      ..., table: ..., freshAddrAllocator: ...)`,
      `table.localizeEntryCount`, and `table.findByRemote(...)`.
      Per-file working-directory convention from feature 016/017
      (`<file>__/`) means the SUT files and the test file live in
      sibling working dirs; the `using` resolves through the test
      .csproj's `<ProjectReference>` to the runtime .csproj
      (langpair-level concern, OUT OF SCOPE here — recorded for codegen
      cross-file wiring).
    idiom_id: null
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed): a `package:`
      import that resolves to an in-repo Dart library (NOT to a pub.dev
      third-party package) maps to a C# `using <Namespace>;` that
      targets the OUTPUT namespace of the converted Dart library — NOT
      a separate NuGet reference. This contrasts with `package:test`,
      which IS a third-party dependency and maps to a NuGet reference +
      `using Xunit;`. The conversion MUST distinguish the two cases by
      inspecting the `package:` URI: `package:glp_runtime/...` is the
      in-repo Dart library (Dart `pubspec.yaml` `name: glp_runtime`);
      any other `package:foo/...` would be a third-party dep needing
      its own NuGet decision. Multi-SUT-import nuance (NEW for this
      file vs. global_writers_table_test.dart which has ONE SUT import):
      TWO `package:glp_runtime/multiagent/...` imports map to ONE
      `using <RootNs>.Multiagent;` because both Dart files emit into
      the same namespace — codegen MUST de-duplicate `using` directives
      after namespace mapping rather than emitting one `using` per Dart
      import. Project-file wiring (a `<ProjectReference>` from the test
      .csproj to the runtime .csproj) is langpair/project-skeleton
      level, not per-file — recorded so codegen knows a `using` alone
      is insufficient without the project reference.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Localize', () { test(...); test(...); test(...); }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]`
      methods on `public` classes by reflection; there is no per-file
      entrypoint to emit. The single `group(...)` call inside `main`
      becomes the enclosing test class (next construct).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` runs once
      per test-file process and registers tests; xUnit has no per-file
      hook — only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly one
      `group()` call with no other statements, so omitting `main` is
      lossless. If future maintenance adds top-of-main setup, that
      setup MUST migrate into the enclosing class's constructor or an
      `IClassFixture<>` — same rule as the sibling specs.
  - construct_key: dart.package_test.group_block
    source_form: "group('Localize', () { test(...); test(...); test(...); });"
    target_decision: >-
      The Dart `group('Localize', body)` maps to a `public class
      LocalizeTests` whose name encodes the group label in PascalCase
      with the conventional `Tests` suffix. The original label MAY be
      preserved via `[Trait("Group", "Localize")]` on the class for
      reporter parity. No nested `group(...)`, no `setUp`/`tearDown`
      inside the group — each test constructs its own
      `GlobalWritersTable` instance and `nextAddr`/`allocateAddr`
      closure locally (the Given/When/Then-prologue pattern), so
      xUnit's per-test fresh-instance lifecycle ("xUnit.net creates a
      new instance of the test class for every test that is run")
      maps cleanly with NO shared state and NO constructor-side
      fixture needed.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (explicitly addressed): the Dart group label
      `'Localize'` is already a valid C# identifier, so the mangle is
      trivial (append `Tests` → `LocalizeTests`). Per-test labels
      contain spaces and punctuation (e.g.
      `'_w(p,i): spawns global_send, returns writer'`); the per-test
      method-name mangling strips non-identifier chars and PascalCases
      (see test_call_executable construct below). Lifecycle nuance:
      no `setUp`/`tearDown` in this file's group — but the IDIOM
      record MUST capture the mapping (Dart group `setUp` → xUnit
      constructor; group `tearDown` → `IDisposable.Dispose`) since it
      will fire on any sibling test file that uses them. Nested-group
      nuance: not used here.
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('<label>', () { /* Given/When/Then with executable
      arrange-act-assert */ });" — applied to all 3 test cases in this
      file (none use `skip:`). Labels:
      `'_w(p,i): spawns global_send, returns writer'`,
      `'_r(p,i): creates entry with remote index, returns reader'`,
      `'mixed global names: correct handling'`.
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument) becomes a
      `public void` method on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]` so runner output keeps
      the sentence-form name (NOT `[Fact(Skip=...)]` — this file's
      tests are executable, contrast with mad_error_handling_test.dart
      where all 5 are `[Fact(Skip="Not yet implemented")]`). Method
      name = label PascalCased with non-identifier chars stripped:
      `'_w(p,i): spawns global_send, returns writer'` →
      `WriterPISpawnsGlobalSendReturnsWriter` (or any deterministic
      mangling that the codegen idiom pins — the IMPORTANT property is
      idempotence + identifier-validity, not exact spelling);
      `'_r(p,i): creates entry with remote index, returns reader'` →
      `ReaderPICreatesEntryWithRemoteIndexReturnsReader`;
      `'mixed global names: correct handling'` →
      `MixedGlobalNamesCorrectHandling`. Method body translates the
      Dart arrange-act-assert verbatim, with `expect(actual, matcher)`
      calls routed to xUnit `Assert.*` per the matcher-routing idiom
      (see constructs below). The Given/When/Then comments and the
      embedded `Spec Section 5.2`/`Spec Section 5.3` references MUST
      be carried into the target as a `/// <summary>` doc-comment
      block per method so spec traceability survives the conversion.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every
      `test` callback in THIS file is synchronous (no
      `async`/`Future`/`await`); target method returns `void` (xUnit
      also supports `async Task` for async tests — not applicable
      here). Closure-capture nuance: no `setUp` variables — every
      `final table = GlobalWritersTable('q');` is local to the test
      body, mapping 1-to-1 to a local
      `var table = new GlobalWritersTable("q");` in the C# method
      (see the `final` ⇒ `var` construct below). Identifier-mangling
      starting with `_` nuance: Dart leading-underscore in a TEST
      LABEL is just a character in the string literal (NOT library
      privacy), so the method name strips it during mangling
      (`'_w(p,i)...'` → `WriterPI...`, NOT `_WriterPI...`); C# leading
      underscore on a public method is discouraged style. NO `Future`
      await, NO `Stream`, NO `Completer`, NO `Isolate`.
      Skip-semantics nuance (NOT firing here, but contrasting with
      mad_error_handling_test.dart): no `skip:` argument anywhere, so
      NO `Skip=` property on `[Fact]`.
  - construct_key: dart.statement.local_function_declaration_returning_record
    source_form: >-
      "(int, int) allocateAddr() { final w = nextAddr++; final r =
      nextAddr++; return (w, r); }" — local function declared INSIDE the
      test body, returning a Dart record-type `(int, int)`, capturing
      and mutating the surrounding `var nextAddr = <base>;` via
      closure. Repeated in all 3 tests with bases 100 / 200 / 300.
    target_decision: >-
      Dart local functions declared inside a method body (NOT
      top-level) map to C# LOCAL FUNCTIONS — a C# 7+ feature whose
      shape is `static? <ReturnType> <Name>(<params>) { ... }`
      declared inside another method's body. Because `allocateAddr`
      CAPTURES `nextAddr` from the enclosing scope (closure over a
      mutable local), it CANNOT be marked `static` in C#; codegen MUST
      emit it as a non-static local function so the C# compiler emits
      the same display-class-style closure capture Dart's anonymous
      function would. Translate the Dart record-type return
      `(int, int)` to a C# tuple type — `(long, long)` per the
      project-wide width idiom rf-dart-int-to-csharp-long-width
      (pinned by lib/bytecode/opcodes_v2.dart.md: Dart `int` ⇒ C#
      `long` for generic numeric semantics). Element access:
      Dart `result.$1` / `result.$2` ⇒ C# `tuple.Item1` /
      `tuple.Item2` (NOT used in THIS file — the tuple is consumed
      inside the SUT, not the test). Return-statement form:
      Dart `return (w, r);` ⇒ C# `return (w, r);` — both languages
      use parenthesised comma-separated literal-tuple syntax. The
      enclosing `var nextAddr = 100;` (mutable local int) ⇒
      `long nextAddr = 100L;` (explicit `long` because the SUT
      consumer `freshAddrAllocator` parameter type will be
      `Func<(long, long)>` under the same width idiom — `var` would
      pick `int` from the literal `100` and force a later
      narrowing-conversion warning). The post-increment expressions
      `nextAddr++` are equivalent in C# (returns old value, then
      increments).
    idiom_id: null
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Record-type-to-ValueTuple nuance (load-bearing, explicitly
      addressed): Dart 3 introduced positional records
      `(int, int)` — a value-type 2-tuple with structural equality,
      `==` by element, `hashCode` derived from elements. C# has TWO
      tuple types: the legacy reference-type `System.Tuple<T1,T2>`
      (immutable, distinct identity) and the modern value-type
      `System.ValueTuple<T1,T2>` (the `(T1, T2)` syntax sugar, with
      structural equality since C# 7.0). Dart record `(int, int)` ⇒
      C# `(long, long)` (ValueTuple) — NOT `Tuple<long, long>` —
      because (a) the value semantics match Dart records, (b)
      `Microsoft Learn` documents ValueTuple as the canonical modern
      choice
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples`),
      and (c) the legacy `Tuple<>` would force `.Item1`/`.Item2` AND
      reference semantics that Dart records do not share.
      Closure-capture nuance: Dart anonymous functions and local
      functions both capture by REFERENCE (mutating `nextAddr++`
      inside is visible to the outer scope's `var nextAddr`); C#
      local functions also capture by reference (same semantics).
      Function-typed-parameter nuance: the SUT's `localize(...,
      required (int, int) Function() freshAddrAllocator)` parameter
      (Dart `(int, int) Function()`) maps to C# `Func<(long, long)>`
      — the test passes the local function `allocateAddr` directly,
      which the C# compiler auto-converts to `Func<(long, long)>` via
      method-group conversion. Local-function-vs-lambda nuance: Dart
      `(int, int) allocateAddr() { ... }` is a LOCAL FUNCTION (named
      declaration with body, hoistable, supports recursion); the
      equivalent C# shape is also a LOCAL FUNCTION
      (`(long, long) allocateAddr() { ... }`), NOT a lambda — codegen
      MUST NOT collapse it to `Func<(long, long)> allocateAddr = () =>
      { ... };` because (a) the Dart source is unambiguously a local
      function (named, statement-form, NOT an assignment to a
      function-typed variable), and (b) C# local functions and
      `Func<>`-lambdas differ in capture-allocation costs and
      recursion ergonomics (local functions support self-reference
      directly; lambdas require an explicit `Func<>` variable
      declared first).
  - construct_key: dart.expression.var_local_variable_with_initializer
    source_form: "var nextAddr = 100;  // and `var nextAddr = 200;`, `var nextAddr = 300;`"
    target_decision: >-
      Translate Dart `var <name> = <expr>;` (mutable local) to C#
      `<Type> <name> = <expr>;` with EXPLICIT type, NOT C# `var`,
      because (a) C# `var` infers `int` from the literal `100`, but
      the SUT consumer requires `long` (under the project-wide
      Dart-`int`→C#-`long` width idiom), and (b) emitting
      `long nextAddr = 100L;` documents intent and avoids
      narrowing-conversion warnings at the function-typed parameter
      pass-site. Specifically: `var nextAddr = 100;` ⇒
      `long nextAddr = 100L;` (and `200` → `200L`, `300` → `300L`).
      Contrast with `final table = GlobalWritersTable('q');` which
      maps to `var table = new GlobalWritersTable("q");` (next
      construct — `final` reference-type binding uses `var` because
      type inference picks the correct reference type).
    idiom_id: null
    research_finding_id: rf-dart-var-local-to-csharp-explicit-type
    nuance: >-
      Mutability-semantics nuance (explicitly addressed): Dart `var
      <local>` declares a mutable local (can be rebound); C# `<Type>
      <name>` (without `readonly`) is also a mutable local. Same
      semantics. Width-suffix nuance: the literal `100` in Dart is an
      `int` (64-bit on native; arbitrary precision in semantics) —
      mapping to C# `long` requires the `L` suffix on the literal
      (`100L`) when the variable type is explicitly `long`, OR an
      implicit `int`→`long` conversion at the assignment (C# permits
      this widening implicitly). Codegen MAY emit either form;
      preference is the explicit `L` suffix for readability.
      Type-inference contrast with `final` ⇒ `var`: this construct
      DELIBERATELY breaks the otherwise-uniform `var → var` mapping
      to pin the width on a numeric local; the rule is "Dart numeric
      `var` with an `int`-literal initializer that flows into a
      `long`-typed sink ⇒ explicit `long` in C#".
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final table = GlobalWritersTable('q');  and `final globalNames =
      [GlobalName.writer('p', 5)];`, `final result = localize(...);`,
      `final entry = table.findByRemote('p', 3);`"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in
      C# where the initializer is a constructor invocation, a
      static-factory call, a top-level function call, or a method
      call that returns a non-null reference, AND translate to
      `<Type> <name> = <expr>;` with the explicit type ONLY where C#
      type inference would otherwise lose information (not applicable
      in this file — every `final` here binds a reference whose
      static type is inferable from the initializer). Specifically:
      `final table = GlobalWritersTable('q')` ⇒
      `var table = new GlobalWritersTable("q");` (note C# mandatory
      `new`); `final globalNames = [GlobalName.writer('p', 5)]` ⇒
      `var globalNames = new List<GlobalName> { GlobalName.Writer("p", 5L) };`
      (list-literal → `new List<T> { ... }` per the Dart-list-literal
      idiom, see next construct; static factory `GlobalName.writer`
      → `GlobalName.Writer`); `final result = localize(...)` ⇒
      `var result = Localize(...);` (top-level Dart function ⇒
      static method on a static class per the SUT's mad_helpers
      conversion-spec); `final entry = table.findByRemote('p', 3)` ⇒
      `var entry = table.FindByRemote("p", 3);` (the return is
      nullable — see null-aware constructs below).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (explicitly addressed): Dart
      `final <local>` prevents REBINDING the local after init but
      does NOT prevent mutation of the referenced object's state —
      exactly the same semantics as C# `var` (which is
      `readonly`-style only when declared `readonly` at field scope;
      LOCAL `var` is freely rebindable). The semantic-tightest C#
      equivalent of Dart's local `final` is actually no direct
      equivalent — C# has no `readonly` modifier for locals. The
      conversion ACCEPTS this minor semantic loss because Dart
      `final`'s no-rebind constraint is enforced at the same point
      in time C# would detect a rebind anyway (in the same method
      body, by code review / linting). Constructor-syntax nuance:
      Dart allows `Foo(...)` without `new`; C# requires
      `new Foo(...)`. String literals: Dart `'q'` and `"q"` are
      equivalent (both string literals); C# uses ONLY `"..."`
      (single quotes are `char`). Codegen MUST emit
      `new GlobalWritersTable("q")`, NOT
      `new GlobalWritersTable('q')` (the latter is a `char`-arg
      constructor that does not exist on the SUT). Sibling-spec
      reuse: identical idiom row already pinned by
      global_writers_table_test.dart.md — this file reuses it
      verbatim.
  - construct_key: dart.expression.list_literal_of_objects
    source_form: >-
      "[GlobalName.writer('p', 5)]  and  [GlobalName.reader('p', 3)]
      and  [GlobalName.writer('p', 1), GlobalName.reader('p', 2)]"
    target_decision: >-
      Dart list literal `[a, b, ...]` of a reference-type element
      maps to C# `new List<T> { a, b, ... }` (collection-initializer
      syntax over `List<T>`) where `T` is the converted element type.
      Specifically: `[GlobalName.writer('p', 5)]` ⇒
      `new List<GlobalName> { GlobalName.Writer("p", 5L) }`. The
      receiving SUT parameter `globalNames` is Dart `List<GlobalName>`
      ⇒ C# `List<GlobalName>` (NOT `IList<>` / `IEnumerable<>` /
      `IReadOnlyList<>` — the SUT's mad_helpers conversion-spec pins
      `List<T>` for Dart-`List`-typed public surfaces). For
      higher-kinded read-only/covariance preferences, the SUT spec
      may upgrade to `IReadOnlyList<T>` separately; this test spec
      tracks the SUT spec's choice and does not override it.
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      Collection-type nuance (explicitly addressed): Dart `List<T>`
      is a mutable, ordered, indexable collection; the closest C#
      counterpart is `System.Collections.Generic.List<T>` with
      collection-initializer syntax. Dart `const []` literal would
      map to a read-only equivalent (`Array.Empty<T>()` or `new
      List<T>().AsReadOnly()`) — NOT applicable here (no `const`
      list literals). Generic-inference nuance: C# collection
      initializers infer `T` from the elements; if all elements are
      `GlobalName.Writer(...)` (returning `GlobalName`), `new List<>
      { ... }` does NOT compile (missing type argument) — codegen
      MUST emit the explicit `<GlobalName>` type argument. Width
      nuance on the int literal `5`/`3`/`1`/`2`: the SUT factory
      signature `GlobalName.writer(String agent, int index)` ⇒
      `GlobalName.Writer(string agent, long index)` under the
      width idiom — codegen SHOULD emit `5L` etc., though implicit
      `int`→`long` conversion at call-site also compiles.
  - construct_key: dart.expression.static_factory_method_call
    source_form: "GlobalName.writer('p', 5)  and  GlobalName.reader('p', 3)"
    target_decision: >-
      Dart `Class.factoryName(args)` (a NAMED CONSTRUCTOR or a STATIC
      FACTORY METHOD — the test uses `GlobalName.writer` /
      `GlobalName.reader` which the SUT may declare as either) maps
      to C# `Class.FactoryName(args)` (a static method on the class)
      with PascalCased name. The exact SUT shape (named constructor
      vs. static factory method vs. private constructor + public
      static `Create*` method) is fixed by the SUT's own
      conversion-spec
      (`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`,
      produced separately). Translate `GlobalName.writer('p', 5)` ⇒
      `GlobalName.Writer("p", 5L)`; `GlobalName.reader('p', 3)` ⇒
      `GlobalName.Reader("p", 3L)`. Comparison with `==` (used in
      `expect(result.spawns[0].globalName, GlobalName.writer('p', 5))`)
      relies on the SUT's `GlobalName` overriding `==` / `Equals` /
      `GetHashCode` — see the implicit-equals matcher construct below.
    idiom_id: null
    research_finding_id: rf-dart-named-constructor-or-static-factory-to-csharp-static-method
    nuance: >-
      Named-constructor-vs-static-factory nuance (explicitly
      addressed): Dart syntax `Class.name(args)` is ambiguous between
      a NAMED CONSTRUCTOR (declared as `Class.name(args) : ...`) and
      a STATIC FACTORY METHOD (declared as `static Class name(args)
      => ...`); both call-sites look identical. C# has no
      named-constructor syntax — both map to a STATIC METHOD on the
      class returning an instance. Codegen MUST defer to the SUT
      conversion-spec for the exact emitted shape; this test spec
      records the CALL-SITE only (`GlobalName.Writer(...)` /
      `GlobalName.Reader(...)`). Member-name PascalCase: Dart's
      `writer` / `reader` lowercase factory names ⇒ C# `Writer` /
      `Reader` PascalCase (member-naming idiom already pinned by
      sibling specs). Equality nuance: the SUT's converted `GlobalName`
      MUST implement structural equality (Dart records do this by
      default; the SUT likely uses a `class` with `==` override or a
      C# `record` / `record struct`) so the implicit-equals matcher
      in `expect(result.spawns[0].globalName, GlobalName.writer('p',
      5))` works on the C# side — recorded as a precondition on the
      SUT spec.
  - construct_key: dart.expression.named_argument_invocation
    source_form: >-
      "localize(globalNames: globalNames, localAgent: 'q', table: table,
      freshAddrAllocator: allocateAddr);"
    target_decision: >-
      Dart named arguments (`name: value`) map DIRECTLY to C# named
      arguments (`name: value`) — same syntax, same semantics. The
      target call becomes
      `Localize(globalNames: globalNames, localAgent: "q",
      table: table, freshAddrAllocator: allocateAddr);` where the
      method name is PascalCased (`localize` → `Localize`) and the
      argument NAMES are camelCased (C# convention for parameter
      names matches Dart's lowerCamelCase — no rename needed for
      `globalNames`/`localAgent`/`table`/`freshAddrAllocator`). The
      parameter ORDER in the SUT signature may differ from Dart's,
      but named-argument call sites are order-independent on both
      sides, so codegen MUST NOT re-order. Top-level function
      `localize` ⇒ static method on the SUT's static-helpers class
      (likely `MadHelpers.Localize` or similar — exact class name
      pinned by the SUT spec).
    idiom_id: null
    research_finding_id: rf-dart-named-arguments-to-csharp-named-arguments
    nuance: >-
      Named-vs-positional nuance (explicitly addressed): Dart's
      `localize` declares all four arguments as `required`
      named-only parameters (Dart syntax `{required X x, required Y
      y}`); C# parameters are positional by default but accept named
      arguments at call sites — there is no `required-named-only`
      modifier on C# parameters (C# 11's `required` is for object
      initializers, NOT for method parameters). The converted SUT
      signature is therefore positional in declaration but the
      tests preserve the readability by using named-argument
      call-site syntax. Optional-argument nuance: none of these
      arguments are optional/defaultable, so no `= default` shape
      is needed. Function-typed-parameter nuance: the
      `freshAddrAllocator` argument is a function value
      (`Func<(long, long)>`); passing the local function
      `allocateAddr` is a C# method-group conversion (implicit, no
      ceremony needed at the call site).
  - construct_key: dart.expression.list_index_access
    source_form: >-
      "result.freshPairs[0]  and  result.useReader[0]  and
      result.useReader[1]  and  result.spawns[0]"
    target_decision: >-
      Dart `list[i]` (operator `[]` on `List<T>`) maps to C# `list[i]`
      (indexer on `List<T>`) — direct 1-to-1 syntax. `List<T>` in
      both Dart and C# is 0-indexed. Property/field access on the
      indexed result is then standard member access. Specifically:
      `result.freshPairs[0].writerAddr` ⇒
      `result.FreshPairs[0].WriterAddr`;
      `result.useReader[0]` ⇒ `result.UseReader[0]` (where
      `useReader` is a `List<bool>` per the SUT spec);
      `result.spawns[0].globalName` ⇒
      `result.Spawns[0].GlobalName`. Out-of-bounds nuance: both Dart
      `List` and C# `List<T>` throw on out-of-bounds access (Dart
      throws `RangeError`; C# throws `ArgumentOutOfRangeException`).
      Semantic parity holds for valid indices.
    idiom_id: null
    research_finding_id: rf-dart-list-indexing-to-csharp-list-indexer
    nuance: >-
      Indexer nuance (explicitly addressed): both `List<T>.operator
      []` (Dart) and `List<T>.this[int]` (C#) are 0-indexed,
      O(1)-amortised, and throw on out-of-bounds. No semantic
      difference. Member-naming on the indexed element: Dart camelCase
      properties (`writerAddr`, `readerAddr`, `globalName`,
      `destAgent`) ⇒ C# PascalCase (`WriterAddr`, `ReaderAddr`,
      `GlobalName`, `DestAgent`) per the member-naming idiom.
  - construct_key: dart.expression.member_property_access
    source_form: >-
      "result.freshPairs.length  and  result.spawns.length  and
      result.freshPairs[0].writerAddr  and  table.localizeEntryCount
      and  entry.remoteAgent  and  entry.remoteIndex"
    target_decision: >-
      Dart property-style member access `x.member` maps to C#
      `x.Member` with PascalCased member name (member-naming idiom
      already pinned by sibling specs). Dart `List<T>.length` ⇒ C#
      `List<T>.Count` — semantic-equivalent property names DIFFER
      between platforms (`length` vs `Count`) and codegen MUST
      rename, NOT just PascalCase. This is a load-bearing renaming.
      Specifically: `result.freshPairs.length` ⇒
      `result.FreshPairs.Count`; `result.spawns.length` ⇒
      `result.Spawns.Count`; `result.freshPairs[0].writerAddr` ⇒
      `result.FreshPairs[0].WriterAddr`;
      `table.localizeEntryCount` ⇒ `table.LocalizeEntryCount` (Dart
      camelCase getter ⇒ C# PascalCase property — no rename, just
      case); `entry.remoteAgent` ⇒ `entry.RemoteAgent`;
      `entry.remoteIndex` ⇒ `entry.RemoteIndex`.
    idiom_id: null
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Renaming nuance (explicitly addressed, NOT glossed): the Dart
      `.length` getter on `List<T>`/`String`/`Iterable<T>` is one of
      the SHORT LIST of Dart-vs-C# property RENAMES (not just case)
      that codegen MUST handle. Counterparts: `.length` ⇒ `.Count`
      (on `List<T>` / `IReadOnlyCollection<T>`) or `.Length` (on
      `string` / `Array`). For `List<T>` specifically, C# uses
      `.Count` (a `System.Collections.Generic.ICollection<T>.Count`
      member); for `string` and `T[]`, C# uses `.Length`. Codegen
      MUST inspect the receiver's converted static type to choose:
      receiver is `List<T>` here, so `.length` ⇒ `.Count`. Sibling
      tests use the same rule. Read-only-vs-mutable nuance: both
      `List<T>.length` (Dart) and `List<T>.Count` (C#) are
      read-only counts of CURRENT elements (Dart's
      `List<T>.length=` setter that grows/shrinks the list does
      NOT have a C# counterpart and is NOT used in this file).
  - construct_key: dart.package_test.expect_equals_implicit_matcher
    source_form: >-
      "expect(result.freshPairs.length, 1);  and 9 other uses, including
      `expect(result.freshPairs[0].writerAddr, 100);`,
      `expect(result.useReader[0], false);`,
      `expect(result.spawns[0].readerAddr, 100);`,
      `expect(result.spawns[0].globalName, GlobalName.writer('p', 5));`,
      `expect(result.spawns[0].destAgent, 'p');`,
      `expect(table.localizeEntryCount, 0);`,
      `expect(entry!.writerAddr, 200);`,
      `expect(entry.remoteAgent, 'p');`,
      `expect(entry.remoteIndex, 3);`,
      `expect(result.useReader[1], true);`,
      `expect(entry!.writerAddr, 302);`"
    target_decision: >-
      Dart `expect(actual, value)` (where the second argument is a
      non-matcher value rather than a `Matcher`) is sugar for
      `expect(actual, equals(value))` per the `package:test` /
      `package:matcher` rule: the matcher second-argument auto-wraps
      bare values in `equals(...)`. Translate to
      `Assert.Equal(expected, actual);` with the EXPECTED value FIRST
      and the ACTUAL second — this is the xUnit argument order, which
      is the INVERSE of Dart's `expect(actual, equals(expected))`.
      Codegen MUST swap the argument order. Used ≈18× across the
      three tests. Examples:
      `expect(result.freshPairs.length, 1)` ⇒
      `Assert.Equal(1, result.FreshPairs.Count);`;
      `expect(result.freshPairs[0].writerAddr, 100)` ⇒
      `Assert.Equal(100L, result.FreshPairs[0].WriterAddr);`;
      `expect(result.useReader[0], false)` ⇒
      `Assert.Equal(false, result.UseReader[0]);` (codegen MAY prefer
      `Assert.False(result.UseReader[0]);` per the
      isFalse-matcher idiom — but Dart literal `false` here is a
      VALUE not a matcher, so the implicit-equals routing fires;
      either form is semantically equivalent; codegen SHOULD pick the
      `Assert.False` form for readability when the expected value is
      the boolean literal `false`, and `Assert.True` for `true`,
      per the matcher-routing idiom row);
      `expect(result.spawns[0].globalName, GlobalName.writer('p', 5))`
      ⇒
      `Assert.Equal(GlobalName.Writer("p", 5L), result.Spawns[0].GlobalName);`
      (relies on `GlobalName`'s structural equality — see nuance);
      `expect(entry.remoteAgent, 'p')` ⇒
      `Assert.Equal("p", entry.RemoteAgent);`;
      `expect(table.localizeEntryCount, 0)` ⇒
      `Assert.Equal(0, table.LocalizeEntryCount);`.
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order footgun (explicitly addressed): Dart
      `expect(actual, equals(expected))` has actual-first; xUnit
      `Assert.Equal<T>(T expected, T actual)` has expected-first.
      This is the EASY-TO-INVERT inversion that smoke_test.dart's
      spec pre-flagged for sibling reuse. Codegen MUST swap.
      Value-vs-reference-equality nuance: this file's expected
      values include `int` literals (0, 1, 2, 3, 100, 101, 200, 201,
      300, 302), `String` literals ('p', 'q'), `bool` literals
      (`true`, `false`), AND a `GlobalName` instance
      (`GlobalName.writer('p', 5)`, `GlobalName.writer('p', 1)`).
      For `int`/`string`/`bool`, C# `Assert.Equal` does the right
      thing via `IEquatable<T>`. For `GlobalName`, the comparison
      MUST be structural — the SUT's converted `GlobalName` MUST
      override `Equals(object?)` + `GetHashCode()` (Dart records do
      this by default; the SUT likely uses a C# `record class` or
      `record struct` to mirror Dart record semantics). This
      precondition is recorded on the SUT spec; THIS test spec
      records the DEPENDENCY (Assert.Equal on `GlobalName` requires
      structural equality on the SUT side). Width nuance: per the
      cross-cutting idiom rf-dart-int-to-csharp-long-width (pinned
      by lib/bytecode/opcodes_v2.dart.md), Dart `int` ⇒ C# `long`
      for generic numeric semantics. THIS file's literal values
      (≤302) are well within `int` range, but the SUT's
      `FreshPair.WriterAddr`/`ReaderAddr` and
      `SpawnInfo.ReaderAddr` types are `long` under the pinned
      idiom — therefore codegen SHOULD emit `100L`, `101L`, etc.
      (the `L` suffix), though implicit `int`→`long` conversion at
      the `Assert.Equal<long>` call-site also compiles.
      Boolean-comparison nuance: codegen SHOULD route
      `expect(x, false)` ⇒ `Assert.False(x);` and
      `expect(x, true)` ⇒ `Assert.True(x);` (per matcher-routing
      idiom) rather than the literal `Assert.Equal(false, x)` —
      this is a readability preference, both forms are
      semantically equivalent.
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(result.spawns, isEmpty);"
    target_decision: >-
      Dart `expect(x, isEmpty)` (from `package:matcher`) maps to
      xUnit `Assert.Empty(x);` (where `x` is `IEnumerable`). Used 1×
      in this file (line 100, second test). Translate
      `expect(result.spawns, isEmpty)` ⇒
      `Assert.Empty(result.Spawns);`. The C# signature is
      `Assert.Empty(IEnumerable collection)` (or
      `Assert.Empty<T>(IEnumerable<T>)` in newer xUnit) — both
      enumerate at most once and throw if the collection has any
      elements.
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Empty-collection nuance (explicitly addressed): Dart
      `isEmpty` matcher accepts ANY collection-like with an
      `isEmpty` getter (Iterable, String, Map, etc.); xUnit
      `Assert.Empty` accepts `IEnumerable` and enumerates to verify
      zero elements. For `List<T>` (the receiver here), both are
      O(1) (Dart short-circuits via the `isEmpty` getter; xUnit
      short-circuits on the first MoveNext()). Strict-emptiness
      semantics identical on both sides. NEW idiom row recorded
      here (the four sibling test-file specs use isTrue/isNotNull/
      isNull/implicit-equals but NOT isEmpty — first appearance).
  - construct_key: dart.expression.null_assertion_bang_operator
    source_form: "entry!.writerAddr  // used 2× — lines 90 (entry from test 2) and 142 (entry from test 3)"
    target_decision: >-
      Dart's null-assertion operator `entry!` (asserts non-null at
      runtime, throws `TypeError` if null) maps to C#'s
      null-forgiving operator `entry!` (compile-time annotation only
      — does NOT throw, just silences the NRT warning). The semantic
      difference is load-bearing and MUST be addressed: in C#, after
      `Assert.NotNull(entry)` on the preceding line, the runtime
      guarantee is already in place (xUnit threw if null); the `!`
      then silences the NRT warning without adding a runtime check.
      Translate `entry!.writerAddr` ⇒ `entry!.WriterAddr`
      (PascalCased property name). In THIS file every `!` usage IS
      preceded by `expect(entry, isNotNull)` on the immediately
      previous statement, so no extra assert is needed.
    idiom_id: null
    research_finding_id: rf-dart-bang-operator-to-csharp-null-forgiving
    nuance: >-
      Runtime-vs-compile-time nuance (explicitly addressed, NOT
      glossed): Dart `!` is a RUNTIME null-check that throws
      `TypeError` if the operand is null; C# `!` is a COMPILE-TIME
      NRT annotation that emits no runtime code (it only suppresses
      the warning). The semantic gap is closed in this file because
      every `!` follows an `Assert.NotNull` (xUnit throws on null,
      so the program never reaches the `!` with a null operand).
      Codegen MUST audit each `!` translation against this
      precondition: if the preceding statement is NOT an
      `Assert.NotNull` of the same expression, codegen MUST insert
      one (or use `entry ?? throw new InvalidOperationException()`
      as the runtime-throw equivalent). This is a CONVERSION
      INVARIANT that any future Dart-`!`→C#-`!` mapping MUST
      preserve. Sibling-spec reuse: identical idiom row already
      pinned by global_writers_table_test.dart.md — this file
      reuses it verbatim.
  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: "expect(entry, isNotNull);  // used 2× — lines 89 and 141"
    target_decision: >-
      `expect(x, isNotNull)` ⇒ `Assert.NotNull(x);` per the
      matcher-routing table pinned by smoke_test.dart and
      global_writers_table_test.dart. xUnit
      `Assert.NotNull(object?)` throws `NotNullException` on null,
      otherwise passes — strict null-vs-not-null semantics
      identical to Dart `isNotNull`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart's
      `package:test` `isNotNull` matches any non-null value
      (including `false`, `0`, empty string — Dart has no
      truthiness coercion); xUnit `Assert.NotNull(object?)` is
      identically strict. The xUnit signature is
      `Assert.NotNull(object? @object)` — the parameter is a
      nullable `object?`, so the argument is implicitly upcast.
      Nullable-reference-types (C# NRT) nuance: in
      `#nullable enable` mode, after `Assert.NotNull(entry)` the
      C# flow-analyzer narrows `entry`'s static type to non-nullable
      iff xUnit ≥2.5's `[NotNull]` post-condition annotation is
      present. Codegen SHOULD prefer the `Assert.NotNull(actual)`
      form; downstream uses of `entry.WriterAddr` rely on either
      xUnit's `[NotNull]` annotation OR an explicit null-forgiving
      operator `entry!.WriterAddr` (the latter matches the Dart
      source's `entry!` operator — see preceding construct).
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System + <RootNs>.Multiagent for SUT; two Dart SUT imports DEDUPED to one using)"
  - "cu-2: namespace declaration mirroring test/multiagent path (<RootNs>.Test.Multiagent)"
  - "cu-3: top-level class LocalizeTests (from group label 'Localize') with optional [Trait(\"Group\", \"Localize\")]"
  - "cu-4: 3 [Fact(DisplayName=\"<original label>\")] public void methods, one per Dart test() call, all executable (NO Skip), all with /// <summary> carrying the original Given/When/Then comments + Spec Section 5.2 and 5.3 references"
  - "cu-5: per-method body: arrange-act-assert translation with `var table = new GlobalWritersTable(\"q\")`-style local declarations, `long nextAddr = <N>L;` numeric local with explicit width, LOCAL FUNCTION `(long, long) allocateAddr() { ... }` (NOT a Func<>-lambda), `var globalNames = new List<GlobalName> { GlobalName.Writer(\"p\", <i>L) };`, `var result = Localize(globalNames: ..., localAgent: \"q\", table: ..., freshAddrAllocator: allocateAddr);`, expect() ⇒ Assert.* per matcher-routing idiom (equals-implicit/isNotNull/isEmpty + Assert.True/False for boolean literals), `!`/`?.` operators preserved 1-to-1 with the Dart runtime-vs-compile-time-semantics caveat documented under construct dart.expression.null_assertion_bang_operator"
  - "cu-6: explicit literal-width suffix `L` on integer literals where the consumed type is `long` (writerAddr/readerAddr/index args) — optional but preferred for readability"
escalations: []
```

## Rationale + research provenance

### Why all 3 tests are `[Fact]` (NOT `[Fact(Skip=...)]`)

Every `test(...)` call in this file has executable arrange-act-assert in
the body (no `skip:` argument anywhere). Contrast with the sibling
mad_error_handling_test.dart, where all 5 tests are `skip: 'Not yet
implemented'` and map to `[Fact(Skip="Not yet implemented")]`. The same
test-framework-mapping idiom (xUnit, `[Fact]` per Dart `test()`) applies
to both files; the only difference is the absence of the `Skip=`
argument. THIS file therefore reuses the framework idiom and the
test-callback idiom verbatim from the sibling specs and adds NO new
skip-related surface.

### Reuse from sibling test-file specs (FR-012 / SC-007)

Idiom KB reuse (no re-research) per FR-012:

- `rf-dart-package-test-to-dotnet-xunit` — framework choice pinned by
  smoke_test.dart.md and the sibling multiagent specs. xUnit selected
  as batch-wide default. Authoritative source: Microsoft Learn
  `unit-testing-csharp-with-xunit` + xunit.net docs.
- `rf-dart-test-main-to-xunit-class-with-facts` — drop `main`, lift
  registered tests to `[Fact]` methods on a class. Authoritative
  source: pub.dev `test` API + Microsoft Learn + xunit.net "Shared
  Context between Tests".
- `rf-dart-package-test-group-to-xunit-class` — `group(label, body)` ⇒
  `public class <Label>Tests`. Authoritative source: pub.dev `test`
  group API + Microsoft Learn xUnit test-class discovery.
- `rf-dart-test-callback-to-xunit-method-body` — Dart test callback
  closure becomes the method body of the `[Fact]` method. Sibling
  specs cover this verbatim.
- `rf-dart-package-sut-import-to-csharp-using` — in-repo
  `package:glp_runtime/...` import ⇒ `using <ConvertedNamespace>;`.
  Pinned by global_writers_table_test.dart.md. THIS file adds the
  multi-SUT-import DEDUP rule (two Dart imports under the same
  Dart-directory ⇒ one C# `using`).
- `rf-dart-final-local-to-csharp-var-local` — Dart `final` local ⇒ C#
  `var` local. Pinned by sibling spec. Authoritative source:
  dart.dev language tour + Microsoft Learn declarations reference.
- `rf-dart-bang-operator-to-csharp-null-forgiving` — Dart `!` ⇒ C#
  `!` with the load-bearing runtime-vs-compile-time caveat. Pinned
  by sibling spec. Authoritative source: dart.dev null-safety doc +
  Microsoft Learn null-forgiving operator doc.
- `rf-dart-expect-isNotNull-to-xunit-assert-notnull`,
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder` — pinned
  by global_writers_table_test.dart.md's matcher-routing table.

### New idiom rows recorded by this file (FR-024 official-docs)

- `rf-dart-record-type-to-csharp-valuetuple`: Dart 3 record syntax
  `(T1, T2)` ⇒ C# `ValueTuple<T1, T2>` syntax sugar `(T1, T2)`.
  Authoritative Dart source: dart.dev "Records"
  (`https://dart.dev/language/records`). Authoritative .NET source:
  Microsoft Learn "Value tuples"
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples`).
  Both sides authoritative; value-semantics + structural-equality
  match between Dart records and C# `ValueTuple<>`. Codegen MUST
  use `(T1, T2)` syntax (ValueTuple), NOT
  `System.Tuple<T1, T2>` (the legacy reference-type tuple, which
  has distinct identity semantics).
- `rf-dart-var-local-to-csharp-explicit-type`: Dart mutable `var
  <local>` with an `int`-literal initializer that flows into a
  `long`-typed sink ⇒ explicit `long <local>` in C# (NOT C# `var`)
  to avoid narrowing-conversion ambiguity. Authoritative sources:
  dart.dev "Variables"
  (`https://dart.dev/language/variables`) + Microsoft Learn
  "Implicitly typed local variables"
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/implicitly-typed-local-variables`).
  This is a project-wide subordinate idiom under the master width
  idiom rf-dart-int-to-csharp-long-width.
- `rf-dart-list-literal-to-csharp-list-of-T`: Dart list literal
  `[a, b]` ⇒ C# collection initializer `new List<T> { a, b }`.
  Authoritative Dart source: dart.dev "Collections"
  (`https://dart.dev/language/collections#lists`). Authoritative
  .NET source: Microsoft Learn `List<T>` + "Object and Collection
  Initializers"
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers`).
  Generic type argument MUST be explicit when codegen cannot infer
  from elements alone. Both sides authoritative.
- `rf-dart-named-constructor-or-static-factory-to-csharp-static-method`:
  Dart `Class.name(args)` (either a named constructor or a static
  factory) ⇒ C# `Class.Name(args)` (a static method). Authoritative
  Dart source: dart.dev "Constructors"
  (`https://dart.dev/language/constructors#named-constructors`) +
  dart.dev "Factory constructors"
  (`https://dart.dev/language/constructors#factory-constructors`).
  Authoritative .NET source: Microsoft Learn "Static classes and
  static class members"
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`).
  C# has no syntactic distinction between "factory method" and
  "static method"; both Dart shapes collapse to the same C# form.
  The exact emitted shape (named constructor preserved via
  `internal` ctor + public static `Writer`/`Reader` methods, or a
  C# `record` with primary constructor + factory) is the SUT spec's
  responsibility.
- `rf-dart-named-arguments-to-csharp-named-arguments`: Dart named
  arguments (`name: value`) ⇒ C# named arguments (`name: value`).
  Authoritative Dart source: dart.dev "Functions" — Named
  parameters
  (`https://dart.dev/language/functions#named-parameters`).
  Authoritative .NET source: Microsoft Learn "Named and optional
  arguments"
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`).
  Direct 1-to-1 syntax; same semantics. Dart's `required` modifier
  on named parameters has no C# parameter-level equivalent (C# 11's
  `required` is for object initializers, not method parameters);
  enforcement falls to the SUT's converted positional declaration
  + call-site discipline.
- `rf-dart-list-indexing-to-csharp-list-indexer`: Dart `list[i]` ⇒
  C# `list[i]` on `List<T>`. Authoritative Dart source: dart.dev
  `List` API
  (`https://api.dart.dev/stable/dart-core/List/operator_subscript.html`).
  Authoritative .NET source: Microsoft Learn `List<T>.Item[Int32]`
  property
  (`https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.item`).
  Both 0-indexed, both throw on out-of-bounds.
- `rf-dart-list-length-to-csharp-list-count`: Dart `List<T>.length`
  ⇒ C# `List<T>.Count`. Load-bearing RENAME (not just case).
  Authoritative Dart source: dart.dev `List.length`
  (`https://api.dart.dev/stable/dart-core/List/length.html`).
  Authoritative .NET source: Microsoft Learn `List<T>.Count`
  property
  (`https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.count`).
  Codegen MUST inspect receiver type: `List<T>` → `.Count`;
  `string`/`T[]` → `.Length`.
- `rf-dart-expect-isEmpty-to-xunit-assert-empty`: Dart `isEmpty`
  matcher ⇒ xUnit `Assert.Empty`. Authoritative Dart source:
  pub.dev `package:matcher` `isEmpty` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isEmpty-constant.html`).
  Authoritative .NET source: xunit.net `Assert.Empty` API
  reference. Both strict zero-element checks; semantic parity.

### Why no escalations

Every construct in this file is authoritative-supported on both sides.
Most matcher routing rows (`isNotNull`, implicit-equals, `!`, `?.`,
SUT-import, `final`→`var`) are already pinned by sibling test-file
specs and reused verbatim per FR-012/SC-007. The new rows
(`record-type→ValueTuple`, `var`→explicit-long, `list literal`→
`List<T>` collection initializer, named constructor/static factory →
C# static method, named arguments, list indexing, `.length`→`.Count`,
`isEmpty`→`Assert.Empty`) each cite official Dart and .NET
documentation. The Dart-record-to-ValueTuple mapping has a clear
authoritative basis on BOTH sides with value-semantics parity, and the
`(int, int)` return type's only consumer in this file is the SUT's
`freshAddrAllocator` parameter (whose type the SUT spec pins as
`Func<(long, long)>`) — no ambiguity, no escalation. The local-function
declaration construct has a 1-to-1 C# 7+ local-function shape with
identical closure-capture semantics. NO idiom-vs-research conflict, NO
idiom-vs-idiom conflict, NOTHING undecidable. The `escalations: []` is
intentional, not a placeholder.

### Cross-file dependency note

The two SUT conversion-specs
(`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`
and `.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`,
produced by separate convspec runs) are the source of truth for:

- the emitted namespace + class signatures + member names of
  `GlobalWritersTable`, `GlobalName`, `LocalizeResult`, `FreshPair`,
  `SpawnInfo`;
- the exact emitted shape of `GlobalName.Writer` /
  `GlobalName.Reader` (named-constructor-preserved-as-internal-ctor
  vs. C# `record` with public static factory);
- the exact emitted shape of `Localize(...)` (top-level Dart function
  ⇒ static method on `MadHelpers` or similar — class name pinned by
  mad_helpers.dart.md);
- the `Func<(long, long)>` type of the `freshAddrAllocator`
  parameter (Dart `(int, int) Function()`);
- structural-equality override on `GlobalName` (required for
  `Assert.Equal(GlobalName.Writer(...), result.Spawns[0].GlobalName)`
  to compare by value, not reference).

THIS test spec records the DEPENDENCY (`using <RootNs>.Multiagent;` +
`<ProjectReference>` + structural-equality precondition on
`GlobalName`) but does NOT pin the SUT's namespace string or the
factory-shape choice — those pinnings are the SUT specs'
responsibility. Codegen wiring must join the three specs at the
project-skeleton level (langpair / 016-init scope, OUT OF this
single-file artifact).

### Spec-section traceability preserved

The Dart source documents Spec Section 5.2 (Localize) and Spec
Section 5.3 (Globalize-Localize Correspondence) via inline comments
across all 3 tests. Each MUST be carried into the corresponding C#
method's `/// <summary>` XML-doc block — this is the spec-only-no-
guessing discipline (FR-013/023) at the doc-comment level: the
conversion preserves the invariant-tracing the test file documents,
even though the doc-comment block is non-executable. NOT a separate
construct row because it is uniform across all 3 tests and falls
under the test-callback idiom's already-recorded `/// <summary>`
carry-over requirement (sibling specs).
