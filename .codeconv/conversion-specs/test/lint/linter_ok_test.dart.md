> Conversion-spec artifact for test/lint/linter_ok_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/lint/linter_ok_test.dart
source_sha256: 75029d51648451ea8ae4049fe8a1f3e64fc07635122432564fdc4c3c45ce99da
target_code_unit: test/lint/LinterOkTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and emit
      `using Xunit;` at file scope. REUSE the batch-wide test-framework
      idiom recorded in every prior `package:test` convspec in this batch
      (smoke_test.dart.md, glp_runtime_test.dart.md, test/conformance/*,
      test/heap/* siblings). Per FR-012 / SC-007 this construct is NOT
      re-researched; the `rf-dart-package-test-to-dotnet-xunit` finding
      carries forward verbatim. The .NET test project's `.csproj`
      (referencing `xunit`, `xunit.runner.visualstudio`,
      `Microsoft.NET.Test.Sdk`) is OUT OF SCOPE for this per-file artifact
      (langpair-level emission concern).
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      the framework decision (xUnit vs MSTest vs NUnit) was settled in
      the first test-file spec of this batch (`smoke_test.dart`) and
      every subsequent test file reuses it via the KB (FR-012). Module /
      discovery / lifecycle nuances (top-level `test()` => `[Fact]`
      instance methods, fresh test-class instance per `[Fact]` per
      xunit.net "Shared Context between Tests", no top-level function
      surface in xUnit) carry forward verbatim from the siblings. No
      async / Future / Stream / isolate surface in this file, so the
      synchronous `void`-returning `[Fact]` shape (not `async Task`)
      applies. Strict-bool / strict-equality semantics are unaffected
      by the import directive itself.
  - construct_key: dart.internal_package_import.same_package
    source_form: >-
      "import 'package:glp_runtime/bytecode/asm.dart';
       import 'package:glp_runtime/lint/linter.dart';"
    target_decision: >-
      Drop both Dart `import 'package:glp_runtime/...';` directives and
      emit TWO C# `using` directives — `using <RootNs>.Bytecode;` (for
      `BC` and the assembled program type) and `using <RootNs>.Lint;`
      (for `Linter` and `LintResult`). They do NOT collapse because the
      SUT specs at `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`
      and `.codeconv/conversion-specs/lib/lint/linter.dart.md` place
      them in DIFFERENT converted namespaces (`Bytecode` vs `Lint`) — one
      `using` per namespace. The Dart static-helper `BC` (recorded in
      `asm.dart.md` as `dart.class.namespace_of_static_helpers`) is
      brought into scope by `using <RootNs>.Bytecode;` and accessed as
      `BC.L(...)`, `BC.R(...)`, `BC.U(...)`, `BC.TRY()`, `BC.SUSP()`,
      `BC.prog(...)` verbatim (asm.dart.md's recorded UPPERCASE-alias
      decision preserves the Dart short forms in C# verbatim — same
      identifier-casing surface). The test assembly's `.csproj` must
      reference the converted-SUT assembly — that project-system wiring
      is OUT OF SCOPE for this per-file artifact (langpair-level
      concern; same as every other test convspec in the batch).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, reused from
      the test/heap/ + test/conformance/ siblings): in Dart each
      `package:` URI is a separate import; in C# imports into the SAME
      converted namespace collapse to ONE `using`, but imports into
      DIFFERENT namespaces do NOT — and the two Dart imports here
      target `Bytecode` and `Lint` separately. No `using static` is
      needed for `BC` because the source already names members through
      the type (`BC.L(...)`); a `using static <RootNs>.Bytecode.BC;`
      would be an ALTERNATIVE that would let the test write `L(...)`
      bare — REJECTED here because the Dart source writes `BC.L(...)`
      explicitly and the spec preserves source shape one-for-one (same
      decision recorded in the asm.dart.md notes). Visibility: `BC`,
      `Linter`, `LintResult` are all library-public on the Dart side
      (no leading underscore) => `public` on the C# side per the SUT
      specs.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class LinterOkTest`
      (mirroring the file name `linter_ok_test.dart` =>
      `LinterOkTest.cs`). The Dart test name
      `'Valid shape: head/guards only pre-commit; single SuspendEnd
      after clauses'` becomes the method identifier
      `ValidShape_HeadGuardsOnlyPreCommit_SingleSuspendEndAfterClauses`
      (PascalCased, with non-identifier characters — `:`, `/`, `;`,
      spaces — stripped or replaced with `_`), with
      `[Fact(DisplayName = "Valid shape: head/guards only pre-commit;
      single SuspendEnd after clauses")]` to preserve the original
      human-readable reporting name. REUSE the idiom recorded in the
      sibling smoke_test.dart, glp_runtime_test.dart, test/conformance/*,
      and test/heap/* specs — same structural lift; no re-research
      (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests"). The Dart
      `main()` registration pass has no xUnit equivalent and is
      dropped. Label-mangling nuance: the Dart test name contains
      colons, a forward slash, a semicolon, and spaces — all stripped
      or replaced with `_` to form an identifier-safe method name; the
      original is preserved verbatim via `[Fact(DisplayName = ...)]`
      so the test runner's report shows the source label unchanged.
      Single-test-file nuance (file-specific): exactly ONE `test(...)`
      call in this file, no `group(...)`, no shared state — therefore
      no `[Trait]` partitioning and no constructor/Dispose pair. The
      `[Fact]` method body is synchronous `void` (no `async Task`); no
      Future/Stream/isolate surface.
  - construct_key: dart.local_var.final_typed_static_factory_invocation
    source_form: "final prog = BC.prog([ BC.L('C1'), BC.TRY(), BC.R(1), BC.U('C2'), BC.L('C2'), BC.TRY(), BC.R(2), BC.U('END'), BC.L('END'), BC.SUSP(), ]);"
    target_decision: >-
      Emit `var prog = BC.prog(new List<Op> { BC.L("C1"), BC.TRY(),
      BC.R(1), BC.U("C2"), BC.L("C2"), BC.TRY(), BC.R(2), BC.U("END"),
      BC.L("END"), BC.SUSP(), });` in the C# `[Fact]` method body. The
      Dart `final` on a never-reassigned local maps to C# `var` (NOT
      `readonly` — illegal on locals — and NOT `const` — the RHS is a
      runtime construction, not a compile-time constant). The Dart
      list-literal `[ ... ]` of `Op` instances maps to C# `new
      List<Op> { ... }` (collection-initialiser syntax targeting the
      converted `List<Op>` parameter of `BC.Prog` per asm.dart.md). The
      `BC.prog` factory itself is recorded in asm.dart.md as a static
      factory returning a `BytecodeProgram` — the converted call is
      `BC.Prog(...)` OR `BC.prog(...)` depending on asm.dart.md's
      casing decision (the asm spec's UPPERCASE-alias decision
      preserves the `BC.prog` short form verbatim AND emits an
      uppercase alias `BC.PROG`/`BC.Prog`; this test convspec records
      the call as `BC.prog(...)` faithful to the source, leaving the
      specific casing routing to the asm SUT spec). The element factory
      calls — `BC.L`, `BC.TRY`, `BC.R`, `BC.U`, `BC.SUSP` — all map
      verbatim per asm.dart.md (UPPERCASE aliases preserved). The
      integer literals `1` and `2` (arguments to `BC.R`) bind to the
      converted `BC.R` parameter type recorded in asm.dart.md (`long`
      per `rf-dart-int-to-csharp-long-width`; emit as `1L`/`2L` for
      unambiguous binding if asm.dart.md's recorded shape is `long`,
      else as `1`/`2` if it kept `int`). The string literals
      `'C1'`/`'C2'`/`'END'` map verbatim to `"C1"`/`"C2"`/`"END"`
      (Dart single quotes => C# double quotes; identical content).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-variable mutability nuance (explicitly addressed, REUSED
      from siblings): Dart `final <name> = expr;` is single-assignment
      with RHS-inferred type. C# `var` is the idiomatic equivalent for
      method locals (Microsoft Learn C# reference for implicitly typed
      local variables). The single-assignment INTENT is lost at the
      language level — a later edit could reassign `prog` — but the
      converted body does not reassign and is faithful to the source.
      `readonly` is WRONG (fields only). `const` is WRONG (the RHS
      `BC.prog(...)` is a runtime construction, not a compile-time
      constant). List-literal nuance (reused from test/heap/* siblings'
      `rf-dart-list-literal-to-csharp-list-of-T`): Dart `[a, b, c, ...]`
      of element type `Op` => C# `new List<Op> { a, b, c, ... }`,
      assignable to the SUT's `IReadOnlyList<Op>`/`List<Op>` parameter
      per asm.dart.md. Trailing-comma nuance: Dart permits a trailing
      comma in list literals; C# collection-initialisers also permit a
      trailing comma — preserves the formatting one-for-one. Reference-
      identity: every `BC.L(...)`/`BC.TRY()`/`BC.R(...)`/`BC.U(...)`/
      `BC.SUSP()` call returns a fresh `Op`-subtype instance in both
      languages (per asm.dart.md's static-factory recording) — the list
      holds those references in insertion order.
  - construct_key: dart.local_var.final_method_call_result
    source_form: "final res = Linter().lint(prog);"
    target_decision: >-
      Emit `var res = new Linter().Lint(prog);` in the C# `[Fact]`
      method body. Dart `Linter()` (no-arg constructor call, with
      Dart-2-optional `new`) maps to C# `new Linter()` (C# requires the
      `new` operator). The Dart instance method `lint` maps to C#
      `Lint` per linter.dart.md's recorded camelCase->PascalCase
      surface (`Linter.lint(BytecodeProgram) -> LintResult` =>
      `Linter.Lint(BytecodeProgram) -> LintResult`). The `prog`
      argument flows in as the same local declared above (type
      `BytecodeProgram` per asm.dart.md's `BC.prog` return type). The
      Dart `final res` maps to C# `var res` (same single-assignment
      handling as the previous construct).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Combined `new`-elision + method-naming nuance (explicitly
      addressed): Dart 2+ made `new` optional and idiomatic code omits
      it (the source writes `Linter()` not `new Linter()`); C# requires
      `new` (Microsoft Learn C# language reference for the `new`
      operator). Method naming: Dart instance methods are
      `lowerCamelCase`; C# instance methods are `PascalCase` per
      Microsoft's C# Coding Conventions, and linter.dart.md records
      this mapping (`lint` => `Lint`). Reference-vs-value: `Linter` is
      a reference type in both languages (Dart classes are reference
      types; linter.dart.md pins the converted `Linter` as a `class`
      not a `struct`) so `res` (and `new Linter()` itself) holds a
      reference in both. The construction is stateless from the
      caller's perspective (no fields read after `Lint` returns —
      `Linter` is service-shaped per linter.dart.md), so the
      `new Linter()` could equally be a single-line chain
      `new Linter().Lint(prog)` — the spec preserves the two-step shape
      faithful to the source.
  - construct_key: dart.package_test.expect_value_boolean_matcher_with_reason_computed
    source_form: "expect(res.ok, isTrue, reason: res.issues.join('\\n'));"
    target_decision: >-
      Translate `expect(actual, isTrue, reason: <expr>)` to xUnit
      `Assert.True(actual, <expr>)` — xUnit's `Assert.True` has an
      overload `public static void True(bool? condition, string?
      userMessage)` (per xunit.net Assert API reference) that mirrors
      Dart's `expect(actual, isTrue, reason: msg)` exactly: the
      `userMessage` surfaces in the failure output the same way Dart's
      `reason:` does. The matcher routing follows the table recorded
      in the smoke_test.dart spec's nuance — `isTrue` => `Assert.True`,
      `isFalse` => `Assert.False` — and the `reason:`-with-overload
      mapping follows the test/conformance/fairness_26_test.dart
      precedent (`rf-dart-expect-isFalse-with-reason-to-xunit-assert-
      false`, symmetric `isTrue` row). The `actual` argument
      `res.ok` maps to `res.Ok` per linter.dart.md's recorded
      camelCase->PascalCase surface (the computed get-only property
      `bool Ok => Issues.Count == 0`). The reason expression
      `res.issues.join('\\n')` is a COMPUTED string (not a literal) —
      see the next construct.
    idiom_id: null
    research_finding_id: rf-dart-expect-isTrue-with-reason-to-xunit-assert-true
    nuance: >-
      Reason-parameter mapping (load-bearing, explicitly addressed,
      symmetric to the `isFalse`-with-reason precedent in
      `fairness_26_test.dart.md`): Dart `expect`'s named `reason:`
      parameter (pub.dev test_api `expect` doc — "An optional reason
      for the matcher to use") surfaces in the failure-message header.
      xUnit `Assert.True` accepts an optional `userMessage` string
      argument (xunit.net Assert API reference;
      `Assert.True(bool condition, string userMessage)`). The
      conversion preserves the `reason:` text as the `userMessage`
      positional argument — and crucially the COMPUTED reason
      expression `res.issues.join('\\n')` is evaluated EAGERLY in
      both Dart and C# (positional argument, not a deferred lambda),
      so the join runs whether or not the assertion fails. That is
      identical to the Dart behaviour (`reason:` is a positional
      string, not a `() => String`) — no laziness divergence. Strict-
      boolean nuance: Dart `isTrue` asserts strict `true`; xUnit
      `Assert.True(bool condition, ...)` asserts strict `true` —
      semantically identical. Argument-order nuance: `Assert.True`
      takes ACTUAL-FIRST then optional message — same positional order
      as Dart's `expect(actual, isTrue, reason: msg)` (modulo the
      `reason:` named-vs-positional difference). Exception-on-failure:
      Dart throws `TestFailure`; xUnit throws `Xunit.Sdk.TrueException`
      (subclass of `Xunit.Sdk.XunitException`) — runner-caught,
      equivalent.
  - construct_key: dart.iterable_join.list_of_string_to_string_with_separator
    source_form: "res.issues.join('\\n')"
    target_decision: >-
      Dart `Iterable<E>.join([String separator = ''])` joins the
      string representations of the elements with `separator`. The .NET
      counterpart is `string.Join(string separator, IEnumerable<T>
      values)` (Microsoft Learn `String.Join` reference at
      `learn.microsoft.com/en-us/dotnet/api/system.string.join`),
      which calls `Object.ToString()` on each element. Emit
      `string.Join("\\n", res.Issues)`. The element type is
      `LintIssue` per linter.dart.md's `LintResult.Issues` typed
      `List<LintIssue>` (mapped to C# get-only `List<LintIssue>` per
      linter.dart.md's recorded shape), and `LintIssue.ToString()` is
      pinned by linter.dart.md as an override emitting
      `$"[{Code}] @op#{Index}: {Message}"` — so the joined string in
      C# is observably equivalent to Dart's joined output.
    idiom_id: null
    research_finding_id: rf-dart-iterable-join-to-csharp-string-join
    nuance: >-
      Separator-and-toString nuance (explicitly addressed): Dart's
      `Iterable.join(sep)` calls `Object.toString()` on each non-null
      element (and emits the literal string `"null"` for null
      elements); .NET `String.Join(sep, IEnumerable<T>)` calls
      `Object.ToString()` on each element and emits the empty string
      for `null` elements. The two diverge ONLY on the null-element
      treatment ("null" vs ""). For THIS file the elements are
      `LintIssue` instances added by the linter's internal logic
      (linter.dart.md: `issues.add(LintIssue(...))` — no null ever
      added), so the null-divergence does not fire. The newline
      separator `'\\n'` maps verbatim to C# `"\\n"` (both languages
      use the same escape sequence for LF). Cross-platform line-ending
      nuance: neither side emits `\\r\\n` here — `\\n` is faithful to
      the source. `LintIssue.ToString()` override (linter.dart.md
      construct `dart.data_class.immutable_final_fields_positional_
      ctor_with_optional_positional`) ensures each joined line is
      `[<code>] @op#<index>: <message>` in both languages. Argument-
      order nuance: Dart receiver-method form (`xs.join(sep)`); C#
      static-method form (`string.Join(sep, xs)`) — the conversion
      flips receiver-vs-static and separator-vs-collection order; this
      is a well-known footgun and load-bearing for any future test
      that joins a different collection. LINQ alternative considered:
      `res.Issues.Aggregate("", (a, b) => a + "\\n" + b)` — REJECTED
      (O(n^2) string concatenation, semantically equivalent but
      degenerate); `string.Join` is the canonical mapping.
  - construct_key: dart.member_access.property_chain_through_result
    source_form: "res.ok ... res.issues"
    target_decision: >-
      Dart `res.ok` (a computed `bool get`) maps to C# `res.Ok` (a
      computed get-only `bool` property) per linter.dart.md's
      `dart.data_class.list_field_with_isempty_getter_idiomatic`
      construct decision: `bool get ok => issues.isEmpty` =>
      `public bool Ok => Issues.Count == 0`. Dart `res.issues` (a
      `final List<LintIssue>` field exposed read-only by the absence
      of a setter, mutable list contents) maps to C# `res.Issues` (a
      get-only `List<LintIssue>` property, mutable list contents
      preserved by the same idiom). Both are member-access only; no
      method invocation surface here.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Naming-convention nuance (explicitly addressed): Dart fields and
      property-getters are `lowerCamelCase`; C# auto-properties /
      computed properties are `PascalCase` per Microsoft's C# Coding
      Conventions (and linter.dart.md records this carry-forward). The
      computed-getter mapping (`get ok` => `Ok =>`) preserves the
      O(1) `isEmpty` => `Count == 0` semantics (Dart `List.isEmpty`
      and .NET `List<T>.Count == 0` are both O(1); LINQ `Any()` would
      also work but `Count == 0` is the direct counterpart already
      pinned by linter.dart.md). Reference-identity nuance: `res.Issues`
      returns the SAME list reference the linter constructed (no
      defensive copy on either side, per linter.dart.md's recorded
      shape) — but this test only READS through it (`string.Join`,
      `Ok` already-computed), never mutates, so the no-defensive-copy
      decision is invisible here.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <RootNs>.Bytecode; (file-level using directive replacing `import 'package:glp_runtime/bytecode/asm.dart';` — namespace owned by the SUT spec at .codeconv/conversion-specs/lib/bytecode/asm.dart.md)"
  - "using <RootNs>.Lint; (file-level using directive replacing `import 'package:glp_runtime/lint/linter.dart';` — namespace owned by the SUT spec at .codeconv/conversion-specs/lib/lint/linter.dart.md)"
  - "namespace <RootNs>.Test.Lint { ... } (namespace mirroring the Dart test/lint path; bracket-form chosen for consistency with sibling test convspecs that nest a single class per file)"
  - "public class LinterOkTest { ... } (single public test class, name mirrors the .dart file name linter_ok_test.dart => LinterOkTest, no base class needed, no constructor / Dispose because no shared per-test state)"
  - "[Fact(DisplayName = \"Valid shape: head/guards only pre-commit; single SuspendEnd after clauses\")] public void ValidShape_HeadGuardsOnlyPreCommit_SingleSuspendEndAfterClauses() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original human-readable test name; identifier-safe form replaces colons / slash / semicolon / spaces with `_`)"
  - "method body line 1-3: var prog = BC.prog(new List<Op> { BC.L(\"C1\"), BC.TRY(), BC.R(1), BC.U(\"C2\"), BC.L(\"C2\"), BC.TRY(), BC.R(2), BC.U(\"END\"), BC.L(\"END\"), BC.SUSP(), }); (Dart `final prog = BC.prog([...])` => C# `var` + collection-initialiser; BC short-form factory names preserved verbatim per asm.dart.md; element types are converted Op subtypes; integer args 1/2 bind to BC.R's parameter type recorded in asm.dart.md)"
  - "method body line 4: var res = new Linter().Lint(prog); (Dart `final res = Linter().lint(prog);` => C# `var` + explicit `new` + PascalCase Lint method name per linter.dart.md)"
  - "method body line 5: Assert.True(res.Ok, string.Join(\"\\n\", res.Issues)); (isTrue with reason: => Assert.True with userMessage overload; reason: text is a COMPUTED string via string.Join, evaluated eagerly as a positional argument; res.Ok and res.Issues PascalCased per linter.dart.md)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` => xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the first test-file spec
  of this batch (`smoke_test.dart`); every subsequent test convspec in
  the batch reuses it. Authoritative sources cited verbatim in the
  originating spec: Microsoft Learn `unit-testing-csharp-with-xunit`,
  xunit.net, pub.dev/package:test,
  pub.dev/documentation/test/latest/test/test.html.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. The `.csproj`-level NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is out of scope
  for this per-file artifact (langpair-level emission). Zero
  escalation.

### rf-dart-internal-package-import-to-csharp-using — `package:glp_runtime/{bytecode,lint}/*.dart` => two separate `using` directives

- **KB reuse (FR-012 / SC-007)**: recorded in the `test/heap/` and
  `test/conformance/` siblings where multiple `package:` imports
  collapsed (when into the same namespace) or stayed separate (when
  into different namespaces). Same rule applies here for the two
  glp_runtime imports — they target DIFFERENT converted namespaces
  (`Bytecode` and `Lint`) so they do NOT collapse.
- **Authoritative Dart**: Dart's official language tour at
  `https://dart.dev/tools/pub/dependencies` documents `package:`
  imports as per-file path-based imports.
- **Authoritative .NET**: Microsoft Learn's C# `using directive`
  reference at `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/keywords/using-directive` documents the `using
  <namespace>;` shape — per-namespace, not per-file. Multiple Dart
  imports into DIFFERENT converted namespaces require one C# `using`
  each.
- **Conclusion**: emit `using <RootNs>.Bytecode;` and
  `using <RootNs>.Lint;`. Zero escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` => `class { [Fact] }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the smoke_test.dart and
  glp_runtime_test.dart siblings and every `test/conformance/*` /
  `test/heap/*` sibling. Same structural lift: drop `main()`, lift
  the one `test()` registration into a `[Fact]` method on a class
  whose name mirrors the .dart file name.
- **File-specific application**: `linter_ok_test.dart` =>
  `LinterOkTest.cs` => `public class LinterOkTest`; the test name
  `'Valid shape: head/guards only pre-commit; single SuspendEnd
  after clauses'` => method identifier
  `ValidShape_HeadGuardsOnlyPreCommit_SingleSuspendEndAfterClauses`
  (PascalCased; colons, slash, semicolon, and spaces stripped or
  replaced with `_` per the C# language specification's lexical-
  structure rules at `learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/language-specification/lexical-structure`),
  with `[Fact(DisplayName = "Valid shape: head/guards only
  pre-commit; single SuspendEnd after clauses")]` preserving the
  original human-readable reporting name. Zero escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` => `var <local> = <expr>;` (REUSED, twice)

- **KB reuse (FR-012 / SC-007)**: recorded in fairness_26_test.dart
  and every `test/heap/*` sibling. Used twice in this file (`final
  prog = ...` and `final res = ...`); both map to C# `var`.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/variables#final-and-const`.
- **Authoritative .NET**: Microsoft Learn C# reference for local
  variable declarations at `https://learn.microsoft.com/en-us/dotnet/
  csharp/language-reference/statements/declarations`. `readonly` is
  field-only; `const` requires a compile-time constant initialiser
  (not satisfied by `BC.prog(...)` or `new Linter().Lint(prog)`).
- **Conclusion**: `var prog = BC.prog(new List<Op> { ... });` and
  `var res = new Linter().Lint(prog);`. Zero escalation.

### rf-dart-expect-isTrue-with-reason-to-xunit-assert-true — `expect(actual, isTrue, reason: msg)` => `Assert.True(actual, msg)`

- **Deep analysis**: Dart `expect`'s `reason:` named parameter
  surfaces in the failure-message header. xUnit's `Assert.True` has
  an overload accepting `userMessage` that surfaces the same way.
  The `reason:` expression here is COMPUTED (`res.issues.join('\\n')`)
  rather than a literal — Dart evaluates the positional argument
  eagerly; C# evaluates the positional argument eagerly; no laziness
  divergence.
- **Authoritative Dart**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  — `expect(actual, matcher, {String? reason, ...})`. Pub.dev
  `https://pub.dev/documentation/matcher/latest/matcher/isTrue-constant.html`
  — `isTrue` matches strict `true`.
- **Authoritative .NET**: xunit.net Assert API reference for
  `Assert.True(bool? condition, string? userMessage)` — verbatim
  overload with a user message. Symmetric to the `Assert.False`
  precedent recorded in fairness_26_test.dart.md (same xunit.net
  Assert API page).
- **Conclusion**: `Assert.True(res.Ok, string.Join("\\n",
  res.Issues));`. Strict-boolean semantics match. Zero escalation.

### rf-dart-iterable-join-to-csharp-string-join — `xs.join(sep)` => `string.Join(sep, xs)`

- **Deep analysis**: Dart's `Iterable.join(separator)` joins the
  string representations of the elements with `separator`, calling
  `Object.toString()` per element. The .NET counterpart is
  `String.Join(separator, IEnumerable<T>)`, which calls
  `Object.ToString()` per element. The argument-order is FLIPPED:
  Dart is receiver-method (`xs.join(sep)`), C# is static-method
  (`string.Join(sep, xs)`). The element-type ToString contract is
  pinned by linter.dart.md: `LintIssue.ToString()` is overridden to
  emit `[<code>] @op#<index>: <message>` — observably identical
  string in Dart and C#.
- **Authoritative Dart**: dart.dev API reference for `Iterable.join`
  at `https://api.dart.dev/stable/dart-core/Iterable/join.html` —
  "Converts each element to a String and concatenates the strings
  ... separator strings are placed between the converted strings."
- **Authoritative .NET**: Microsoft Learn `String.Join` reference at
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.join`
  — `public static string Join<T>(string? separator, IEnumerable<T>
  values)` calls `ToString()` on each element.
- **Null-element nuance**: Dart `join` emits the literal string
  `"null"` for null elements; .NET `String.Join` emits the empty
  string. Not exercised here (linter.dart.md's `issues.add(...)`
  never adds null), but recorded for any future test that joins a
  collection that may contain nulls.
- **Conclusion**: `string.Join("\\n", res.Issues)`. Argument-order
  flip is the load-bearing footgun and is recorded. Zero escalation.

### rf-dart-final-field-class-to-csharp-getonly-class — `res.ok` / `res.issues` member access (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in linter.dart.md
  (construct `dart.data_class.list_field_with_isempty_getter_idiomatic`)
  and inherited from token.dart.md / opcodes.dart.md. The
  `lowerCamelCase` => `PascalCase` mapping (`res.ok` => `res.Ok`,
  `res.issues` => `res.Issues`) is part of the same idiom row.
- **Authoritative Dart**: dart.dev style guide
  `https://dart.dev/effective-dart/style#do-name-types-using-
  uppercamelcase` — fields/getters are `lowerCamelCase` in Dart.
- **Authoritative .NET**: Microsoft Learn C# Coding Conventions at
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/
  coding-style/coding-conventions` — public properties are
  `PascalCase`.
- **Conclusion**: `res.Ok` and `res.Issues`. Get-only computed
  property + get-only list property exactly as linter.dart.md
  records. Zero escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the `[Fact]` method is `void` (not `async
  Task`). The well-known async-Dart-vs-.NET-async nuance is
  deliberately not asserted here (does not apply to this file's
  source surface).
- No `late`, `mixin`, `extension`, generics-at-callsite,
  sealed/abstract, bitwise/shift, isolate, or null-safety nuance —
  all absent.
- No `group(...)` / `setUp` / `tearDown` / `skip:` surface — single
  `[Fact]` method on a stateless test class, no constructor / no
  Dispose / no `[Trait]` partitioning needed.
- The file exercises the linter's "happy path" — a hand-assembled
  valid bytecode program (`label / try / reader / unify_clause`
  pairs followed by a single `SuspendEnd`) is asserted to lint
  cleanly (`res.Ok` true). The SUT-side conversion shape (BC static
  helpers, Op class hierarchy, BytecodeProgram, Linter / LintIssue /
  LintResult) is owned by the SUT specs at
  `.codeconv/conversion-specs/lib/bytecode/asm.dart.md` and
  `.codeconv/conversion-specs/lib/lint/linter.dart.md`; this test
  convspec references their decisions but does not duplicate them.
- The `reason:` argument is a COMPUTED string
  (`res.issues.join('\\n')`), not a literal — a finer-grained nuance
  than fairness_26_test's literal reason strings. The eager-vs-lazy
  evaluation is identical on both sides (positional argument, not a
  deferred lambda), so the join runs once whether or not the
  assertion fails. Recorded as a reusable consideration for any
  future test that uses a computed `reason:` expression.
- The `.join('\\n')` => `string.Join("\\n", ...)` receiver-vs-static
  argument-order flip is load-bearing and is recorded as a reusable
  consideration for any future test that joins a collection.
- Zero escalations: every construct in this file is
  authoritative-supported on both sides, and every non-trivial
  construct REUSES an idiom / finding recorded by sibling specs
  (smoke_test.dart, fairness_26_test.dart, glp_runtime_test.dart,
  test/heap/* siblings) per FR-012 / SC-007 KB-reuse decision order.
  The single new-to-batch construct row (`Iterable.join` =>
  `string.Join`) is grounded in official Dart and .NET docs (FR-024)
  and recorded under `rf-dart-iterable-join-to-csharp-string-join`
  for reuse by any future test that calls `.join(sep)`.
