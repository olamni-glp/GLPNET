> Conversion-spec artifact for test/conformance/restart_clause1_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/conformance/restart_clause1_test.dart
source_sha256: 15baa98d14a52a37cc739a1867d3ad3a7c68c3c6c5c10ee75ad9ce59b23ac517
target_code_unit: test/conformance/RestartClause1Test.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and emit
      `using Xunit;` at file scope. REUSE the batch-wide test-framework
      idiom recorded in the sibling specs
      `.codeconv/conversion-specs/test/smoke_test.dart.md`,
      `.codeconv/conversion-specs/test/glp_runtime_test.dart.md`, and the
      sibling conformance spec
      `.codeconv/conversion-specs/test/conformance/fairness_26_test.dart.md`
      (and every prior `package:test` file in the `test/heap/`,
      `test/multiagent/`, `test/analysis/`, `test/module/` siblings — all
      resolved to xUnit). Per FR-012 / SC-007 this construct is NOT
      re-researched here; the `rf-dart-package-test-to-dotnet-xunit`
      finding carries forward verbatim. The .NET test project's `.csproj`
      (referencing `xunit`, `xunit.runner.visualstudio`,
      `Microsoft.NET.Test.Sdk`) is OUT OF SCOPE for this per-file
      artifact — same langpair-level emission concern recorded in the
      siblings.
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      the framework decision (xUnit vs MSTest vs NUnit) was settled in
      the first test-file spec of this batch (`smoke_test.dart`) and
      every subsequent test file reuses it via the KB (FR-012). The
      module / discovery / lifecycle nuances (top-level `test()` ⇒
      `[Fact]` instance methods, fresh test-class instance per `[Fact]`
      per xunit.net "Shared Context between Tests", no top-level
      function surface in xUnit) carry forward verbatim from the
      siblings. No async / Future / Stream / isolate surface in this
      file, so the synchronous `void`-returning `[Fact]` shape (not
      `async Task`) still applies. Strict-bool / strict-equality
      semantics are unaffected by the import directive itself.
  - construct_key: dart.internal_package_import.same_package_multi
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/suspend_ops.dart';
       import 'package:glp_runtime/runtime/heap_fcp.dart';
       import 'package:glp_runtime/runtime/commit.dart';
       import 'package:glp_runtime/runtime/terms.dart';"
    target_decision: >-
      Drop all six Dart `import 'package:glp_runtime/runtime/...';`
      directives and collapse them into a SINGLE C# `using
      <RootNs>.Runtime;` directive — the converted runtime sub-namespace
      decided by the six SUT specs at
      `.codeconv/conversion-specs/lib/runtime/runtime.dart.md`,
      `lib/runtime/machine_state.dart.md`, `lib/runtime/suspend_ops.dart.md`,
      `lib/runtime/heap_fcp.dart.md`, `lib/runtime/commit.dart.md`, and
      `lib/runtime/terms.dart.md`. All six Dart libraries lift into the
      same C# `Runtime` sub-namespace per their SUT specs, so one `using`
      brings `GlpRuntime`, the `GoalId` and `Pc` type-alias shapes,
      `SuspendOps`, `HeapFCP`/`HeapFcp`, `CommitOps`, and `ConstTerm`
      into scope. The test assembly's `.csproj` must reference the
      converted-SUT assembly — out of scope for this per-file artifact
      (langpair-level concern; same as every other test convspec in the
      batch).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, REUSED from
      the test/heap/ siblings and from `fairness_26_test.dart.md`): in
      Dart each `package:` URI is a separate import; in C# all sub-paths
      under the same converted namespace collapse into ONE `using`
      directive (C# `using` is per-namespace, not per-file). The six
      Dart imports here all target the `Runtime` sub-namespace per
      their SUT specs, so they collapse. No `using static` is needed —
      the test body names `GlpRuntime` (class), `GoalId` / `Pc` (type
      aliases owned by `machine_state.dart.md`), `SuspendOps` / `CommitOps`
      (static-op classes owned by their SUT specs), `HeapFCP` (class —
      C#-side identifier per `heap_fcp.dart.md`), and `ConstTerm`
      (class owned by `terms.dart.md`), all reachable through the
      namespace-level `using`. Visibility: every imported identifier
      is library-public on the Dart side (no leading underscore) ⇒
      `public` on the C# side per the SUT specs. No cross-package,
      cross-isolate, or transitive-export semantics apply.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class RestartClause1Test`
      (mirroring the file name `restart_clause1_test.dart` ⇒
      `RestartClause1Test.cs`). The Dart test name
      `'On wake, activation pc equals kappa (restart at clause 1)'`
      becomes the method identifier
      `OnWakeActivationPcEqualsKappaRestartAtClause1` (PascalCased, with
      commas, spaces, and parentheses stripped to identifier-safe form),
      with `[Fact(DisplayName = "On wake, activation pc equals kappa
      (restart at clause 1)")]` to preserve the original human-readable
      reporting name. REUSE the idiom recorded in the sibling
      `smoke_test.dart`, `glp_runtime_test.dart`, and the sibling
      conformance spec `fairness_26_test.dart` — same structural lift;
      no re-research (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests"). The Dart
      `main()` registration pass has no xUnit equivalent and is dropped.
      The Dart test name contains punctuation (commas, parentheses)
      that is not valid in C# identifiers — these are stripped from the
      method identifier; the original human-readable name is preserved
      verbatim via `[Fact(DisplayName = ...)]` so the test runner's
      report shows the original sentence. No setUp / tearDown / group /
      async — synchronous `void` `[Fact]`, no constructor /
      `IDisposable.Dispose` / `IAsyncLifetime` surface. Per-test
      fresh-instance lifecycle nuance recorded but does not fire here
      (the `GlpRuntime`/`HeapFCP` references are method-scoped locals,
      not field-scoped).
  - construct_key: dart.local_var.final_typed_constructor_invocation
    source_form: "final rt = GlpRuntime();"
    target_decision: >-
      Emit `var rt = new GlpRuntime();` in the C# `[Fact]` method body
      (type inferred via C# `var`, matching Dart's `final` +
      RHS-typed inference). `final` on a Dart local that is never
      reassigned maps idiomatically to C# `var` (not `readonly` —
      `readonly` applies to fields, not locals; C# has no method-local
      `readonly` keyword). The Dart `GlpRuntime()` invocation maps to
      C# `new GlpRuntime()` (C# requires the `new` operator for
      constructor calls; Dart made `new` optional in Dart 2 and the
      source omits it). The converted `GlpRuntime` class lives in the
      `Runtime` sub-namespace already brought into scope by the
      file-level `using` (see the internal-package-import construct
      above). REUSE — same idiom emitted in the sibling
      `fairness_26_test.dart.md` for `final rt = GlpRuntime();`.
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-variable mutability nuance (explicitly addressed, carry-forward
      from `fairness_26_test.dart.md`): Dart `final <name> = expr;`
      declares a single-assignment local with RHS-inferred type (Dart
      language tour `https://dart.dev/language/variables#final-and-const`).
      C# has no method-local single-assignment modifier; the idiomatic
      equivalent is `var` (Microsoft Learn C# reference for implicitly
      typed local variables at `learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/statements/declarations`). The single-assignment
      INTENT is lost at the language level — a later edit could reassign
      `rt` — but the converted code does not reassign and the generated
      body is faithful to the source. `readonly` (field-only) and
      `const` (Dart `const` ⇒ compile-time constant, not `final`'s
      runtime single-assignment) are BOTH wrong mappings here.
      Reference-vs-value: `GlpRuntime` is a reference type in both Dart
      and C# (Dart classes are reference types; the converted C# class
      is a `class` not a `struct` per its SUT spec), so `rt` holds a
      reference in both. Constructor syntax: Dart 2+ `new` is optional
      and omitted in idiomatic code; C# requires `new` (Microsoft Learn
      C# language reference for the `new` operator at
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      operators/new-operator`).
  - construct_key: dart.local_var.final_downcast_via_as
    source_form: "final heap = rt.heap as HeapFCP;"
    target_decision: >-
      Emit `var heap = (HeapFCP)rt.Heap;` in the C# `[Fact]` method
      body. Dart's `<expr> as T` is a runtime cast that throws
      `TypeError` on failure (Dart language tour
      `https://dart.dev/language/operators#type-test-operators`). The
      C# equivalent for a reference-type runtime cast that throws on
      failure is the explicit cast expression `(T)<expr>` (Microsoft
      Learn "Cast expression" at
      `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      operators/type-testing-and-cast#cast-expression`), which throws
      `InvalidCastException` on mismatch — the closest semantic
      equivalent to Dart's `TypeError`. The Dart property access
      `rt.heap` PascalCases to C# `rt.Heap` per the SUT
      `runtime.dart.md`'s property-naming carry-forward (`camelCase` ⇒
      `PascalCase` for public members per Microsoft's C# Coding
      Conventions). The HeapFCP identifier shape (case — `HeapFCP` vs
      `HeapFcp`) is owned by `heap_fcp.dart.md`; this spec records only
      the cast shape.
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cast-semantics nuance (load-bearing, explicitly addressed): Dart
      `as T` THROWS on mismatch (per the Dart language tour cited
      above) — it is NOT the safe-cast `is T ? ... : null` form. C# has
      two cast shapes: the explicit cast `(T)x` (throws
      `InvalidCastException` on mismatch — semantic match for Dart
      `as`) and the safe cast `x as T` (returns `null` on mismatch —
      semantic match for Dart's `<expr> is T ? <expr> as T : null`
      pattern). Spec emits `(HeapFCP)rt.Heap` (NOT `rt.Heap as HeapFCP`)
      because the C# `as` operator returns `null` on failure, which
      would silently null-propagate where Dart `as` would throw. The
      surface looks similar but the semantics differ — this is a
      classic Dart↔C# `as` footgun documented at
      `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      operators/type-testing-and-cast#the-as-operator`. Reference-type
      vs nullable-value-type: `HeapFCP` is a reference type, so both
      C# cast shapes are legal here; the choice is purely about
      failure-mode semantics. The `final` modifier on the local maps
      to `var` per the `dart.local_var.final_typed_constructor_invocation`
      construct above (single-assignment intent lost at the language
      level, faithful in the generated body).
  - construct_key: dart.const_local.typed_int_literal
    source_form: "const GoalId g = 77;"
    target_decision: >-
      Emit `const GoalId g = 77;` in the C# `[Fact]` method body
      (C# does support `const` on method locals). The `GoalId` type
      identifier is reachable via the file-level `using <RootNs>.Runtime;`.
      The `GoalId` SUT spec at
      `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`
      records the converted shape of `typedef GoalId = int;` — if the
      SUT spec converts the Dart typedef to a C# `using GoalId =
      System.Int32;` alias (or to a plain `int`/`long`), this file's
      local declaration uses that same shape unchanged. NOTE: C#
      `const` requires a compile-time constant initializer, and integer
      literals satisfy that — the source literal `77` is a compile-time
      constant in both Dart and C#, so the C# `const` works directly.
      If the converted `GoalId` is a `long` (not `int`), the literal
      becomes `77L` for unambiguous binding (mirrors the lib spec's
      `rf-dart-int-to-csharp-long-width` mapping carried forward from
      the SUT specs). REUSE — same idiom emitted in the sibling
      `fairness_26_test.dart.md` for `const GoalId g = 123;`.
    idiom_id: null
    research_finding_id: rf-dart-const-local-typed-int-to-csharp-const
    nuance: >-
      `const` semantics nuance (explicitly addressed, carry-forward
      from `fairness_26_test.dart.md`): Dart `const` on a local creates
      a compile-time canonicalised constant (Dart language tour
      `https://dart.dev/language/variables#const`). C# `const` on a
      local also creates a compile-time constant (Microsoft Learn C#
      reference for the `const` keyword at
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      keywords/const`) — semantically the closest match. The conversion
      `Dart const local ⇒ C# const local` IS authoritative-supported on
      both sides for primitive integer literals; this is NOT a case
      that requires `readonly` or `static readonly` (those are for
      fields and require runtime initialization). Integer-width nuance:
      Dart `int` ⇒ C# `long` (or `int`) per the SUT spec's
      `rf-dart-int-to-csharp-long-width` carry-forward — the literal
      `77` is within both ranges so no truncation hazard, but the
      converted code emits the literal in the chosen width for type
      cleanliness. Identifier-via-typedef: `GoalId` itself is the SUT
      machine_state spec's responsibility; this convspec records only
      that the local declaration uses the SUT-decided shape verbatim.
  - construct_key: dart.const_local.typed_pc_int_literal
    source_form: "const Pc kappa = 1;"
    target_decision: >-
      Emit `const Pc kappa = 1;` (or `const Pc kappa = 1L;` if the
      `Pc` SUT spec records a `long` width) in the C# `[Fact]` method
      body. Identical shape to `const GoalId g = 77;` above — the only
      differences are the type alias (`Pc` instead of `GoalId`) and
      the literal value (`1` instead of `77`). The `Pc` type-alias
      shape is owned by the SUT spec at
      `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`
      (Dart `typedef Pc = int;` ⇒ C# `using Pc = System.Int32;` alias
      or equivalent). This local declaration reuses the SUT-decided
      shape verbatim. Same authoritative basis as the GoalId construct
      above; no re-research (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-const-local-typed-int-to-csharp-const
    nuance: >-
      Same `const`-on-local semantics nuance as the GoalId construct.
      Recording this as a separate construct row (not collapsing with
      GoalId) because the `Pc` type alias is distinct in the SUT
      machine_state spec — they MAY decode to different C# widths (e.g.
      `Pc` to `int` vs `GoalId` to `long`) — so emission MUST track
      each alias independently. Both are compile-time constants on
      both sides of the conversion; no truncation hazard for the small
      integer literals `1` and `77`.
  - construct_key: dart.record_destructure.pattern_two_addresses
    source_form: "final (writerAddr, readerAddr) = heap.allocateVariable();"
    target_decision: >-
      Emit C# tuple-deconstruction: `var (writerAddr, readerAddr) =
      heap.AllocateVariable();`. Dart record-positional-destructuring
      of a `(int, int)` returned by `heap.allocateVariable()` maps to
      C# tuple-deconstruction with `var` — semantically and lexically
      a direct 1-to-1 translation (Microsoft Learn "Deconstructing
      tuples and other types" at
      `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/
      functional/deconstruct`). The SUT method's return type is
      `(long writerAddr, long readerAddr)` per `heap_fcp.dart.md`
      construct `dart.tuple_return.record_two_int_addresses_allocate_
      variable` (mapped to `ValueTuple<long,long>` with named
      elements). Dart method name `allocateVariable` PascalCases to C#
      `AllocateVariable` per the SUT spec's general method-naming
      carry-forward. REUSE — same destructuring idiom emitted by the
      test/heap/ sibling `binding_pointer_test.dart.md`'s
      `dart.local_var.record_destructuring_two_ints_or_ignored`
      construct.
    idiom_id: null
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      Discard-vs-bind nuance (carry-forward from the heap sibling,
      not exercised here): this file binds BOTH positional elements
      (no `_` discard) — same shape as the `(w1, r1)` line in
      `binding_pointer_test.dart`. The int-width nuance — Dart `int`
      (64-bit on the VM) MUST map to C# `long` to preserve address-
      arithmetic width (per `cells.dart.md` construct
      `dart.int.fixed_width_identity_field`, idiom
      `rf-dart-int-to-csharp-long-width`). Codegen MUST keep both
      deconstructed names typed as `long`, not `int`. Tuple element
      naming: Dart record-positional names are local to the
      destructuring, not part of the type — equivalent to C#'s
      positional tuple-element access. The `final` modifier on the
      destructuring maps to `var` per the local-mutability nuance
      already recorded.
  - construct_key: dart.set_literal.single_element_named_arg
    source_form: "readerVarIds: {readerAddr}"
    target_decision: >-
      Emit a C# collection-expression set with the converted
      identifier name: `readerVarIds: new HashSet<long> { readerAddr }`
      (or, on C# 12+, the collection-expression form `readerVarIds:
      [readerAddr]` IF the parameter type is `IReadOnlySet<long>` /
      `ISet<long>` / `HashSet<long>` and the conversion target
      framework is .NET 8+). Dart `{readerAddr}` inside a named-arg
      position is parsed as a `Set<int>` literal (Dart language tour
      `https://dart.dev/language/collections#sets`), NOT a `Map`
      literal — single-element brace literals at expression position
      with non-pair contents disambiguate to `Set`. The C# equivalent
      depends on the `SuspendOps.suspendGoalFCP` SUT parameter type
      (owned by `.codeconv/conversion-specs/lib/runtime/
      suspend_ops.dart.md`): if that SUT spec types
      `readerVarIds` as `ISet<long>` / `HashSet<long>`, this site
      emits `new HashSet<long> { readerAddr }`; if it types it as
      `IReadOnlySet<long>` plus the .NET 8+ collection-expression
      conversion is allowed, the more concise `[readerAddr]` form is
      preferred.
    idiom_id: null
    research_finding_id: rf-dart-set-literal-to-csharp-hashset-or-collection-expr
    nuance: >-
      Set-vs-map literal disambiguation nuance (load-bearing, explicitly
      addressed): Dart `{}` is an empty Map; `{e}` (one expression, no
      `:`) is a Set; `{k: v}` is a Map. The source's `{readerAddr}` is
      unambiguously a Set literal (one expression, no colon). C# has
      no brace-only set literal — codegen MUST emit either the
      explicit constructor + initializer (`new HashSet<long> { ... }`)
      or the collection-expression form (`[...]`, C# 12+ /
      .NET 8+). The constructor form is universally supported and is
      the safe default; the collection-expression form depends on the
      target collection type having a supported collection-builder
      pattern (per the Microsoft Learn "Collection expressions"
      reference at `https://learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/operators/collection-expressions`). Mutability
      nuance: Dart `Set` literal is mutable by default (unless prefixed
      with `const`); C# `HashSet<T>` is mutable — semantic match. The
      element-type `long` carry-forward (from the deconstructed
      `readerAddr`) preserves the int-width decision from
      `cells.dart.md`. Named-argument syntax: C# named arguments use
      `<name>: <expr>` — same surface as Dart — so the named-arg
      delivery is direct.
  - construct_key: dart.static_method_call.named_args_only
    source_form: >-
      "SuspendOps.suspendGoalFCP(heap: heap, goalId: g, kappa: kappa,
      readerVarIds: {readerAddr});"
    target_decision: >-
      Emit `SuspendOps.SuspendGoalFcp(heap: heap, goalId: g, kappa:
      kappa, readerVarIds: new HashSet<long> { readerAddr });` (or the
      collection-expression form per the set-literal construct above).
      The class identifier `SuspendOps` carries forward unchanged from
      its SUT spec (`.codeconv/conversion-specs/lib/runtime/
      suspend_ops.dart.md`). The method name `suspendGoalFCP`
      PascalCases to `SuspendGoalFcp` per the SUT spec's recorded
      shape — note the `FCP` → `Fcp` casing follows .NET Framework
      Design Guidelines for 3-letter-and-longer acronyms (only 2-letter
      acronyms remain all-caps, e.g. `IO` stays `IO`; `FCP` is a
      3-letter pseudo-acronym so it Pascals to `Fcp`); the SUT spec is
      authoritative — the exact casing follows whatever the SUT spec
      records. C# named-argument syntax (`<name>: <expr>`) is a direct
      match for Dart's named-argument syntax (Microsoft Learn
      "Named and optional arguments" at
      `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/
      classes-and-structs/named-and-optional-arguments`). Return type
      `void` in both languages — emitted as an expression-statement.
    idiom_id: null
    research_finding_id: rf-dart-named-args-call-to-csharp-named-args
    nuance: >-
      Named-argument parity nuance (explicitly addressed): Dart
      `f(name: value)` and C# `f(name: value)` have identical surface
      and identical positional-vs-named-argument resolution — direct
      1-to-1 mapping (Microsoft Learn "Named and optional arguments"
      cited above; Dart language tour
      `https://dart.dev/language/functions#named-parameters`). The
      ordering of named arguments at the call site is NOT
      significant in either language (both languages resolve named
      arguments by name, not position) — codegen MAY preserve the
      source order for readability. Required-vs-optional: Dart's
      `required` keyword on named parameters (Dart 2.12+) is owned by
      the SUT `suspend_ops.dart.md` spec; this spec assumes the SUT
      records the same required/optional status on the C# side
      (typically required-named-args have no default; optional-named-
      args have a `= default` in the C# signature). Acronym-casing
      nuance: the `FCP` → `Fcp` (or `FCP` preserved) Pascal-casing is
      owned by the SUT spec; this convspec defers to the SUT-recorded
      identifier verbatim.
  - construct_key: dart.map_literal.single_pair_typed_inference
    source_form: "final sigmaHat = {writerAddr: ConstTerm('ground')};"
    target_decision: >-
      Emit `var sigmaHat = new Dictionary<long, Term> { { writerAddr,
      new ConstTerm("ground") } };` in the C# `[Fact]` method body
      (or, on C# 12+/.NET 8+, the collection-expression form `var
      sigmaHat = new Dictionary<long, Term> { [writerAddr] = new
      ConstTerm("ground") };` — equivalent semantics). Dart's brace
      literal with a `key: value` pair is a `Map<K, V>` (Dart language
      tour `https://dart.dev/language/collections#maps`); C#'s closest
      equivalent is `Dictionary<TKey, TValue>` with a collection
      initializer (Microsoft Learn "Object and Collection Initializers"
      at `https://learn.microsoft.com/en-us/dotnet/csharp/programming-
      guide/classes-and-structs/object-and-collection-initializers`).
      The key type `long` follows from `writerAddr` (per the
      destructuring construct above); the value type `Term` (the
      common supertype of `ConstTerm` and friends) is owned by the
      `terms.dart.md` SUT spec. The Dart `ConstTerm('ground')`
      constructor call maps to C# `new ConstTerm("ground")` — Dart
      single-quoted string literals map to C# double-quoted string
      literals (C# has no single-quoted string form; single quotes are
      for `char`).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary
    nuance: >-
      Map literal vs Set literal disambiguation (cross-reference to
      the set-literal construct above): the presence of `key: value`
      pairs makes this a `Map`, not a `Set`. C# `Dictionary<K,V>` is
      mutable by default — semantic match for Dart's mutable-by-default
      `Map`. Type-inference nuance: Dart infers the generic types of
      the literal from the contents (`int` key from `writerAddr`,
      `ConstTerm` value from `ConstTerm('ground')` — widened to the
      `Term` supertype if the variable is then passed to a parameter
      typed `Map<int, Term>`). C# requires the dictionary type to be
      named explicitly at the constructor (C# 12 collection
      expressions can't fully infer for `Dictionary<K,V>` without a
      target-typed context). The widening to `Term` is owned by the
      `CommitOps.applySigmaHatFCP` SUT signature in `commit.dart.md` —
      if that signature takes `Map<long, Term>`, this site emits
      `Dictionary<long, Term>`; if it takes a more specific
      `Map<long, ConstTerm>`, this site emits the matching
      `Dictionary<long, ConstTerm>`. String-quote nuance: Dart `'...'`
      ⇒ C# `"..."`; C# single quotes are for `char` only (Microsoft
      Learn C# string-literal reference at
      `https://learn.microsoft.com/en-us/dotnet/csharp/language-
      reference/builtin-types/reference-types#the-string-type`).
  - construct_key: dart.constructor_call.const_term_with_string
    source_form: "ConstTerm('ground')"
    target_decision: >-
      Emit `new ConstTerm("ground")` in the C# `[Fact]` method body.
      Dart's optional `new` keyword (Dart 2+) is omitted in the source;
      C# requires the `new` operator for constructor invocation
      (Microsoft Learn C# `new` operator at
      `https://learn.microsoft.com/en-us/dotnet/csharp/language-
      reference/operators/new-operator`). The Dart single-quoted
      string literal `'ground'` maps to the C# double-quoted string
      literal `"ground"`. The `ConstTerm` class identifier and its
      constructor signature shape are owned by the SUT spec at
      `.codeconv/conversion-specs/lib/runtime/terms.dart.md`. REUSE —
      same pattern appears across `binding_pointer_test.dart.md`'s
      `dart.constructor_call.const_term_with_value` construct (which
      enumerates `ConstTerm(42)`, `ConstTerm('hello')`,
      `ConstTerm(3.14159)`, etc.); the same idiom carries forward
      verbatim.
    idiom_id: null
    research_finding_id: rf-dart-const-term-constructor-call-to-csharp-new
    nuance: >-
      String-literal-quote nuance (explicitly addressed, carry-forward
      from the heap sibling): Dart accepts single OR double quotes for
      string literals; C# accepts ONLY double quotes for strings
      (single quotes are reserved for `char` literals). Conversion
      always emits double quotes. Value-vs-reference: `ConstTerm` is a
      reference type in both languages per the SUT `terms.dart.md`
      spec (Dart classes are reference types; the converted C# is a
      `class` not a `struct`). Constructor-arg type: the source
      argument `'ground'` is a `String` in Dart ⇒ `string` in C# —
      direct 1-to-1 type mapping (Microsoft Learn "Built-in reference
      types" at the reference above). No interpolation, no escape
      sequences, no multi-line — the simplest possible string-literal
      conversion.
  - construct_key: dart.static_method_call.named_args_return_collection
    source_form: >-
      "final acts = CommitOps.applySigmaHatFCP(heap: heap, sigmaHat:
      sigmaHat);"
    target_decision: >-
      Emit `var acts = CommitOps.ApplySigmaHatFcp(heap: heap, sigmaHat:
      sigmaHat);` in the C# `[Fact]` method body. The class `CommitOps`
      and its method `applySigmaHatFCP` (PascalCased to
      `ApplySigmaHatFcp` per the SUT spec) live in the `Runtime`
      sub-namespace already brought into scope. Named-argument
      delivery is identical surface in Dart and C# (see the
      `dart.static_method_call.named_args_only` construct above for
      the authoritative basis). The return type is a collection of
      activations (`List<GoalActivation>` per the SUT
      `.codeconv/conversion-specs/lib/runtime/commit.dart.md` —
      converted to `IReadOnlyList<GoalActivation>` or the SUT-decided
      shape). The `final` local maps to `var` per the local-mutability
      nuance already recorded. The acronym `FCP` ⇒ `Fcp` Pascal-casing
      follows the SUT-recorded convention (.NET Framework Design
      Guidelines for 3+-letter pseudo-acronyms); the exact casing is
      owned by the SUT spec.
    idiom_id: null
    research_finding_id: rf-dart-named-args-call-to-csharp-named-args
    nuance: >-
      Same named-argument-parity nuance as the
      `SuspendOps.suspendGoalFCP` construct above. The
      return-type-handling nuance: Dart `final` on a local + `=
      <static-method-returning-List>` collapses to C# `var = ...` —
      the deduced static type on the C# side matches whatever the
      SUT spec records for the return type of `ApplySigmaHatFcp`
      (whether `List<>`, `IReadOnlyList<>`, or `IEnumerable<>`). This
      spec defers to the SUT-recorded return type and does not
      duplicate the decision. Reference-vs-value: the returned
      collection is a reference in both languages.
  - construct_key: dart.package_test.expect_value_length_matcher
    source_form: "expect(acts, hasLength(1));"
    target_decision: >-
      Translate `expect(actual, hasLength(N))` to xUnit
      `Assert.Single(acts);` for the `N = 1` special case, OR to
      `Assert.Equal(N, acts.Count);` for the general case. For this
      file's `hasLength(1)`, spec emits `Assert.Single(acts);` — the
      most specific and most diagnostic xUnit assertion (per xunit.net
      Assert API reference, `Assert.Single<T>(IEnumerable<T>
      collection)` verifies the collection contains exactly one
      element AND can return that element for chained assertion).
      Alternative `Assert.Equal(1, acts.Count);` is correct but less
      idiomatic and gives a value-diff failure rather than the
      semantic "single" failure message. Dart's
      `hasLength(int)` matcher (from `package:matcher`, pub.dev
      `https://pub.dev/documentation/matcher/latest/matcher/
      hasLength.html`) asserts the actual collection's `.length`
      equals N. xUnit `Assert.Single(IEnumerable<T>)` asserts the
      same condition for N=1 (and crucially returns the single
      element — but the source does NOT chain off the matcher result
      so this returned-element capability is unused here).
    idiom_id: null
    research_finding_id: rf-dart-expect-hasLength-to-xunit-assert-single-or-count
    nuance: >-
      Matcher-routing nuance (explicitly addressed): `hasLength(N)`
      has two idiomatic xUnit emissions — `Assert.Single` for `N==1`
      (most specific, best failure diagnostic) and `Assert.Equal(N,
      coll.Count)` for `N > 1`. Spec default selects the most
      specific available xUnit assertion (carry-forward decision rule
      from the smoke_test.dart spec's matcher-table nuance). The
      `acts.Count` property requires the collection to be
      `IReadOnlyCollection<T>` / `ICollection<T>` (which
      `List<T>` / `IReadOnlyList<T>` both satisfy); for arbitrary
      `IEnumerable<T>` it would be the LINQ extension
      `.Count()` (different method). Since the SUT spec
      `commit.dart.md` records `ApplySigmaHatFcp` returning a
      list-typed result, both `Assert.Single(acts)` and
      `acts.Count` are legal. No `reason:` parameter is supplied in
      the source, so no `userMessage` mapping is needed. Argument
      order: `Assert.Single` takes the collection only (one arg) —
      direct 1-to-1 with the Dart `expect(acts, hasLength(1))`
      argument intent.
  - construct_key: dart.package_test.expect_property_equals_matcher
    source_form: "expect(acts.first.id, g);"
    target_decision: >-
      Translate the bare-value `expect(actual, value)` form to xUnit
      `Assert.Equal(g, acts.First().Id);` — EXPECTED-FIRST per the
      argument-swap recorded in the smoke_test.dart and
      `fairness_26_test.dart.md` siblings. The Dart `Iterable.first`
      getter (pub.dev / dart.dev `https://api.dart.dev/stable/dart-
      core/Iterable/first.html`) maps to the LINQ `.First()`
      extension method (Microsoft Learn LINQ `First` at
      `https://learn.microsoft.com/en-us/dotnet/api/system.linq.
      enumerable.first`) — both throw on empty (Dart throws
      `StateError`; C# LINQ throws `InvalidOperationException`). For
      a known-non-empty collection (this assertion runs AFTER
      `Assert.Single(acts)` has succeeded), `.First()` is safe. The
      `.id` Dart property access PascalCases to `.Id` on the C# side
      per the SUT spec for the activation type (owned by
      `.codeconv/conversion-specs/lib/runtime/commit.dart.md` or
      `glp_activation.dart.md`). The `GoalId` type carries forward —
      if `GoalId` decodes to `long` on the C# side, the literal `g`
      (also `GoalId`) is the same type, so `Assert.Equal<long>` is
      inferred from the operands; no explicit generic argument
      needed. xUnit's `Assert.Equal<T>` has NO `userMessage`
      overload — no `reason:` was supplied in the source, so this is
      not an issue here.
    idiom_id: null
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      Same EXPECTED-FIRST argument-order footgun as in
      `fairness_26_test.dart.md` (explicitly addressed,
      carry-forward): Dart `expect(actual, value)` is ACTUAL-FIRST;
      xUnit `Assert.Equal<T>(expected, actual)` is EXPECTED-FIRST.
      Conversion swaps argument order. `.first` ⇒ `.First()` nuance
      (load-bearing): Dart `.first` is a getter; LINQ `.First()` is
      an extension method (parentheses required). Both throw on
      empty; both return the first element of the iteration; lazy-vs-
      eager semantics differ for `IEnumerable<T>` but irrelevant for
      this `List`-typed collection. The integer-width nuance (Dart
      `int` ⇒ C# `long` per `cells.dart.md`'s
      `rf-dart-int-to-csharp-long-width`) carries forward via
      `GoalId`'s SUT-decided shape. Method-name PascalCasing: Dart
      `id` ⇒ C# `Id` per Microsoft's C# Coding Conventions and the
      SUT spec.
  - construct_key: dart.package_test.expect_property_equals_matcher_second
    source_form: "expect(acts.first.pc, kappa);"
    target_decision: >-
      Emit `Assert.Equal(kappa, acts.First().Pc);` — same EXPECTED-
      FIRST shape and same `.first` ⇒ `.First()` translation as the
      previous construct. The Dart `.pc` property PascalCases to
      `.Pc` per the SUT spec for the activation type. `kappa` is
      typed `Pc` (a typedef) which decodes to `int`/`long` per
      `machine_state.dart.md`'s SUT decision; `.Pc` returns the same
      typedef-decoded type, so `Assert.Equal` infers `T` from the
      operands.
    idiom_id: null
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      Same matcher-routing-table row as the previous construct, with
      a different property name (`.Pc` instead of `.Id`). No
      `reason:` ⇒ no inline-comment emission. The argument-order-swap
      and integer-width nuances carry forward verbatim. Recording
      this as a separate construct row (not collapsing with the
      `.id` row) because the property-name PascalCasing is recorded
      property-by-property in the SUT spec — each emission references
      a distinct SUT-recorded identifier.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <RootNs>.Runtime; (single file-level using directive collapsing all six `import 'package:glp_runtime/runtime/...';` lines — the exact namespace identifier is owned by the SUT specs at .codeconv/conversion-specs/lib/runtime/{runtime,machine_state,suspend_ops,heap_fcp,commit,terms}.dart.md)"
  - "public class RestartClause1Test { ... } (single public test class, name mirrors the .dart file name restart_clause1_test.dart ⇒ RestartClause1Test, no base class needed)"
  - "[Fact(DisplayName = \"On wake, activation pc equals kappa (restart at clause 1)\")] public void OnWakeActivationPcEqualsKappaRestartAtClause1() { ... } (one Fact-attributed method for the file's single test() call; DisplayName preserves the original sentence verbatim including commas and parentheses; method identifier strips punctuation to identifier-safe PascalCase form)"
  - "method body line 1: var rt = new GlpRuntime(); (Dart `final rt = GlpRuntime();` ⇒ C# `var` with explicit `new`)"
  - "method body line 2: var heap = (HeapFCP)rt.Heap; (Dart `final heap = rt.heap as HeapFCP;` ⇒ C# explicit-cast — NOT the `as` operator because C# `as` returns null on mismatch where Dart `as` throws; the explicit `(T)x` cast throws `InvalidCastException` matching Dart's `TypeError` semantics; rt.heap PascalCases to rt.Heap)"
  - "method body line 3: const GoalId g = 77; (Dart `const GoalId g = 77;` ⇒ C# `const` on a method local; GoalId type-alias shape owned by machine_state SUT spec)"
  - "method body line 4: const Pc kappa = 1; (Dart `const Pc kappa = 1;` ⇒ C# `const` on a method local; Pc type-alias shape owned by machine_state SUT spec)"
  - "method body line 5: var (writerAddr, readerAddr) = heap.AllocateVariable(); (Dart record-destructuring ⇒ C# tuple-deconstruction with `var`; both names typed `long` per cells.dart.md int-width idiom; allocateVariable ⇒ AllocateVariable PascalCase per SUT spec)"
  - "method body line 6: SuspendOps.SuspendGoalFcp(heap: heap, goalId: g, kappa: kappa, readerVarIds: new HashSet<long> { readerAddr }); (Dart static-method call with all-named-args ⇒ C# named-args; `{readerAddr}` Dart Set literal ⇒ C# HashSet<long> constructor + collection initializer; suspendGoalFCP PascalCased per SUT spec, acronym FCP ⇒ Fcp per .NET Framework Design Guidelines for 3+-letter acronyms)"
  - "method body line 7: var sigmaHat = new Dictionary<long, Term> { { writerAddr, new ConstTerm(\"ground\") } }; (Dart Map literal with one `key: value` pair ⇒ C# Dictionary<long, Term> with collection initializer; Term value type is the SUT-decided common supertype owned by terms.dart.md; single-quoted Dart string ⇒ double-quoted C# string)"
  - "method body line 8: var acts = CommitOps.ApplySigmaHatFcp(heap: heap, sigmaHat: sigmaHat); (Dart static-method call ⇒ C# named-args; return type owned by commit.dart.md SUT spec; applySigmaHatFCP ⇒ ApplySigmaHatFcp per SUT spec)"
  - "method body line 9: Assert.Single(acts); (Dart `expect(acts, hasLength(1));` ⇒ xUnit `Assert.Single` for the N==1 special case — most specific and most diagnostic available xUnit assertion)"
  - "method body line 10: Assert.Equal(g, acts.First().Id); (Dart `expect(acts.first.id, g);` ⇒ xUnit `Assert.Equal` EXPECTED-FIRST; .first ⇒ .First() LINQ extension; .id ⇒ .Id PascalCase per SUT spec)"
  - "method body line 11: Assert.Equal(kappa, acts.First().Pc); (same shape as the previous line; .pc ⇒ .Pc PascalCase per SUT spec)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice (REUSED)

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

### rf-dart-internal-package-import-to-csharp-using — six `package:glp_runtime/runtime/*` imports ⇒ one collapsed `using <RootNs>.Runtime;` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the `test/heap/`
  siblings (where four runtime `package:` imports collapsed into one
  `using`) and in `fairness_26_test.dart.md` (two imports collapsed).
  Same rule scales to the six imports here.
- **Authoritative Dart**: Dart's official language tour at
  `https://dart.dev/tools/pub/dependencies` and
  `https://dart.dev/guides/libraries/create-packages` documents
  `package:` imports as per-file path-based imports.
- **Authoritative .NET**: Microsoft Learn's C# `using directive`
  reference at `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/keywords/using-directive` documents the `using
  <namespace>;` shape — per-namespace, not per-file. Multiple Dart
  imports into the same converted namespace collapse to one C# `using`.
- **Conclusion**: emit a single `using <RootNs>.Runtime;`. Zero
  escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` ⇒ `class { [Fact] }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the smoke_test.dart,
  glp_runtime_test.dart, and `fairness_26_test.dart.md` siblings.
  Same structural lift: drop `main()`, lift the one `test()`
  registration into a `[Fact]` method on a class whose name mirrors
  the .dart file name. Authoritative sources cited in the siblings:
  Microsoft Learn xUnit tutorial, xunit.net "Shared Context between
  Tests", pub.dev `test` API reference, Dart language tour
  `#hello-world`.
- **File-specific application**: `restart_clause1_test.dart` ⇒
  `RestartClause1Test.cs` ⇒ `public class RestartClause1Test`; the
  test name `'On wake, activation pc equals kappa (restart at clause
  1)'` ⇒ method identifier
  `OnWakeActivationPcEqualsKappaRestartAtClause1` (PascalCased with
  punctuation stripped to identifier-safe form), with
  `[Fact(DisplayName = "On wake, activation pc equals kappa (restart
  at clause 1)")]` preserving the original sentence verbatim
  including commas and parentheses. Zero escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` ⇒ `var <local> = <expr>;` (REUSED)

- **KB reuse (FR-012 / SC-007)**: same construct recorded in
  `fairness_26_test.dart.md` for `final rt = GlpRuntime();`. Reused
  verbatim for this file's `final rt = GlpRuntime();` and (mediated
  by the `as`-cast construct) for `final heap = rt.heap as HeapFCP;`.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/variables#final-and-const` — "Use
  `final` ... for a variable that's set only once."
- **Authoritative .NET**: Microsoft Learn C# reference for local
  variable declarations at `https://learn.microsoft.com/en-us/dotnet/
  csharp/language-reference/statements/declarations` — `var` is the
  implicitly typed local variable form. C# has no method-local
  `readonly` (field-only per
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  keywords/readonly`), and `const` requires a compile-time constant
  initializer (not satisfied by `new GlpRuntime()` or a runtime cast
  expression).
- **Conclusion**: `var rt = new GlpRuntime();` and the cast variant
  `var heap = (HeapFCP)rt.Heap;`. The single-assignment intent is
  lost at the language level but the generated body does not reassign.
  Zero escalation.

### rf-dart-as-cast-to-csharp-explicit-cast — Dart `<expr> as T` ⇒ C# `(T)<expr>` (NOT C# `as`)

- **Deep analysis**: this is the FILE-NEW load-bearing construct for
  this artifact — it is NOT covered by `fairness_26_test.dart.md` or
  the heap siblings. Dart `as T` throws on failure; C# has two cast
  shapes with DIFFERENT failure semantics; the conversion MUST pick
  the throwing form to preserve Dart's semantics.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/operators#type-test-operators` —
  "`as` Typecast (also used to specify library prefixes)" — the
  `<expr> as T` form throws `TypeError` if the cast fails.
- **Authoritative .NET**: Microsoft Learn "Type-testing operators and
  cast expression" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  operators/type-testing-and-cast` — explicit cast `(T)x` throws
  `InvalidCastException` on mismatch; the C# `as` operator
  (`x as T`) returns `null` on mismatch. The semantic match for
  Dart's throwing `as` is the C# EXPLICIT CAST `(T)x`, NOT C# `as`.
- **Conclusion**: emit `(HeapFCP)rt.Heap` — explicit cast. Using
  `rt.Heap as HeapFCP` would silently null-propagate where Dart
  would throw. Zero escalation.

### rf-dart-const-local-typed-int-to-csharp-const — `const <Typedef> <name> = <int-literal>;` ⇒ verbatim (REUSED, applied twice)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_26_test.dart.md` for `const GoalId g = 123;`. This file
  applies it TWICE: once for `const GoalId g = 77;` and once for
  `const Pc kappa = 1;`.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/variables#const` — "Use `const` for
  variables that you want to be compile-time constants."
- **Authoritative .NET**: Microsoft Learn C# reference for `const` at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  keywords/const` — "You use the `const` keyword to declare a constant
  field or a constant local. ... The expression that's used to
  initialize a constant ... must be a constant expression."
- **Conclusion**: emit `const GoalId g = 77;` and `const Pc kappa =
  1;` verbatim. The `GoalId`/`Pc` type-alias shapes are owned by the
  machine_state SUT spec. Zero escalation.

### rf-dart-record-return-to-csharp-valuetuple — `final (a, b) = f();` ⇒ `var (a, b) = f();` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `binding_pointer_test.dart.md`'s
  `dart.local_var.record_destructuring_two_ints_or_ignored` construct
  (which enumerates the discard variants `(writerAddr, _)`, `(_, r2)`,
  `(w1, r1)`). This file's `final (writerAddr, readerAddr) =
  heap.allocateVariable();` is the bind-both variant — same idiom.
- **Authoritative Dart**: Dart language tour "Records" at
  `https://dart.dev/language/records` — records support positional
  destructuring with `final (a, b) = record;`.
- **Authoritative .NET**: Microsoft Learn "Deconstructing tuples and
  other types" at `https://learn.microsoft.com/en-us/dotnet/csharp/
  fundamentals/functional/deconstruct` — `var (a, b) = tuple;` is
  the canonical C# 7+ deconstruction form.
- **Conclusion**: emit `var (writerAddr, readerAddr) =
  heap.AllocateVariable();` with both names typed `long` per the
  cells.dart.md int-width idiom. Zero escalation.

### rf-dart-set-literal-to-csharp-hashset-or-collection-expr — `{e}` Set literal ⇒ `new HashSet<T> { e }` (or `[e]` on C# 12+)

- **Deep analysis**: FILE-NEW load-bearing construct for this artifact
  (not covered by the sibling test specs). The Dart source has
  `readerVarIds: {readerAddr}` — a single-element Set literal at a
  named-argument position. Dart's `{}` is ambiguous between Map and
  Set; one-expression-no-colon disambiguates to Set.
- **Authoritative Dart**: Dart language tour "Sets" at
  `https://dart.dev/language/collections#sets` — "Dart provides set
  literal support: ... `var halogens = {'fluorine', 'chlorine'};`".
- **Authoritative .NET**: Microsoft Learn "HashSet<T>" at
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.
  generic.hashset-1` — `HashSet<T>` is the canonical C# unordered
  unique-element collection; supports collection-initializer syntax.
  Microsoft Learn "Collection expressions" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  operators/collection-expressions` — C# 12 collection expressions
  `[e1, e2]` target-type to any supported collection including
  `HashSet<T>` (.NET 8+).
- **Conclusion**: emit `new HashSet<long> { readerAddr }` (universally
  supported), with the C# 12+ `[readerAddr]` form as an optional
  conciseness alternative when the target framework is .NET 8+. The
  parameter type is owned by the SUT `suspend_ops.dart.md` spec.
  Zero escalation.

### rf-dart-named-args-call-to-csharp-named-args — `f(name: value)` ⇒ `f(name: value)` (verbatim surface)

- **Deep analysis**: Dart and C# have identical named-argument
  syntax (`<name>: <expr>`) and identical name-based resolution
  semantics — direct 1-to-1 mapping. The file has two
  named-args-only static-method calls: `SuspendOps.suspendGoalFCP`
  (four named args) and `CommitOps.applySigmaHatFCP` (two named args).
- **Authoritative Dart**: Dart language tour "Functions: named
  parameters" at `https://dart.dev/language/functions#named-
  parameters` — "Use `paramName: value` to specify a named argument".
- **Authoritative .NET**: Microsoft Learn "Named and optional
  arguments (C# Programming Guide)" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/
  classes-and-structs/named-and-optional-arguments` — C# named
  arguments use `<name>: <expr>`.
- **Conclusion**: emit the same named-argument surface verbatim. The
  method-name PascalCasing (`suspendGoalFCP` ⇒ `SuspendGoalFcp`,
  `applySigmaHatFCP` ⇒ `ApplySigmaHatFcp`) is owned by the SUT
  specs. Zero escalation.

### rf-dart-map-literal-to-csharp-dictionary — `{k: v}` Map literal ⇒ `new Dictionary<TK, TV> { { k, v } }`

- **Deep analysis**: FILE-NEW load-bearing construct for this artifact
  (not covered by the sibling test specs). The source has `{writerAddr:
  ConstTerm('ground')}` — a single-pair Map literal whose generic
  parameters are inferred from contents (key type `long` from
  `writerAddr`, value type widened to `Term` based on usage at the
  `CommitOps.applySigmaHatFCP` call site).
- **Authoritative Dart**: Dart language tour "Maps" at
  `https://dart.dev/language/collections#maps` — "Dart's
  Map literal allows you to write maps with strings as keys: ...
  `var gifts = {'first': 'partridge', ...};`".
- **Authoritative .NET**: Microsoft Learn "Dictionary<TKey,TValue>" at
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.
  generic.dictionary-2` — `Dictionary<TKey, TValue>` is the canonical
  C# associative collection. Microsoft Learn "Object and Collection
  Initializers" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-
  guide/classes-and-structs/object-and-collection-initializers#
  collection-initializers` — supports `new Dictionary<K,V> { { k1,
  v1 }, ... }` form.
- **Conclusion**: emit `new Dictionary<long, Term> { { writerAddr,
  new ConstTerm("ground") } }`. The value type is determined by the
  `CommitOps.applySigmaHatFCP` parameter type in the SUT spec
  (commit.dart.md). Zero escalation.

### rf-dart-const-term-constructor-call-to-csharp-new — `ConstTerm(<arg>)` ⇒ `new ConstTerm(<arg>)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `binding_pointer_test.dart.md`'s
  `dart.constructor_call.const_term_with_value` construct (which
  enumerates `ConstTerm(42)`, `ConstTerm('hello')`,
  `ConstTerm(3.14159)`, `ConstTerm(null)`, `ConstTerm(true)`, etc.).
  This file's `ConstTerm('ground')` is the string-arg variant of the
  same idiom.
- **Authoritative Dart**: Dart 2+ optional-`new` (Dart language tour
  `#hello-world` and the language specification).
- **Authoritative .NET**: Microsoft Learn C# `new` operator at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-
  reference/operators/new-operator` — C# requires the `new` operator
  for constructor invocation.
- **Conclusion**: emit `new ConstTerm("ground")` — single quotes ⇒
  double quotes (C# has no single-quoted string form). Zero
  escalation.

### rf-dart-expect-hasLength-to-xunit-assert-single-or-count — `hasLength(N)` matcher

- **Deep analysis**: FILE-NEW load-bearing construct for this artifact
  (not covered by the sibling test specs). The source has
  `expect(acts, hasLength(1));` — a `hasLength` matcher with
  argument `1`.
- **Authoritative Dart**: pub.dev `matcher` package's `hasLength`
  matcher at
  `https://pub.dev/documentation/matcher/latest/matcher/hasLength.html`
  — asserts that the actual value's `.length` equals the given.
- **Authoritative .NET**: xunit.net Assert API reference for
  `Assert.Single<T>(IEnumerable<T> collection)` — verifies that the
  collection contains exactly one element AND returns that element.
  Microsoft Learn xUnit testing guide and xunit.net `Assert.Equal`
  reference for the general-N case.
- **Conclusion**: for `hasLength(1)` emit `Assert.Single(acts);` —
  the most specific and most diagnostic xUnit form. For
  `hasLength(N>1)` the spec would emit `Assert.Equal(N, acts.Count);`
  (general-N fallback). Spec records both routings as a single
  matcher-routing rule. Zero escalation.

### rf-dart-expect-bare-value-int-to-xunit-assert-equal — `expect(actual, value)` (with or without `reason:`) ⇒ `Assert.Equal(value, actual)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded verbatim in
  `fairness_26_test.dart.md`'s
  `dart.package_test.expect_value_equals_matcher_with_reason` and
  `_bare_with_reason` constructs. This file applies it twice
  (`expect(acts.first.id, g);` and `expect(acts.first.pc, kappa);`)
  with `.first` chained property access and no `reason:` argument.
- **Authoritative Dart (`expect` bare-value)**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  — "If [matcher] is not a [Matcher], it will be implicitly wrapped
  in [equals]."
- **Authoritative Dart (`Iterable.first`)**: api.dart.dev
  `https://api.dart.dev/stable/dart-core/Iterable/first.html` — "The
  first element. Throws a StateError if `this` is empty."
- **Authoritative .NET (`Assert.Equal`)**: xunit.net Assert API for
  `Equal<T>(T expected, T actual)` — verbatim EXPECTED-FIRST. NOTE:
  no `userMessage` overload exists for `Assert.Equal<T>` (this is a
  deliberate xUnit design choice — the value diff IS the
  diagnostic). Not exercised here since no `reason:` is supplied.
- **Authoritative .NET (`Enumerable.First`)**: Microsoft Learn
  `https://learn.microsoft.com/en-us/dotnet/api/system.linq.
  enumerable.first` — "Returns the first element of a sequence. ...
  Throws InvalidOperationException [if] The source sequence is
  empty."
- **Conclusion**: emit `Assert.Equal(g, acts.First().Id);` and
  `Assert.Equal(kappa, acts.First().Pc);` — EXPECTED-FIRST argument
  order, `.first` ⇒ `.First()` LINQ extension, `.id`/`.pc` ⇒
  `.Id`/`.Pc` PascalCase per SUT specs. Zero escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer` /
  `async`-`await` surface in this file — the `[Fact]` method is
  `void` (not `async Task`). The well-known async-Dart-vs-.NET-async
  nuance is deliberately not asserted here (does not apply to this
  file's source surface).
- No `late`, `mixin`, `extension`, generics-at-declaration-site,
  sealed/abstract, bitwise/shift, isolate, or null-safety nuance —
  all absent.
- The file exercises the runtime's wake-on-binding semantics
  (`SuspendOps.suspendGoalFCP` and `CommitOps.applySigmaHatFCP`) on
  a `HeapFCP`-backed `GlpRuntime`. The SUT-side conversion shape
  (class names, method names, return types, `GoalId`/`Pc` type-alias
  shapes, `HeapFCP` casing) is owned by the SUT specs at
  `.codeconv/conversion-specs/lib/runtime/{runtime,machine_state,
  suspend_ops,heap_fcp,commit,terms}.dart.md`; this test convspec
  references their decisions but does not duplicate them.
- File-specific FILE-NEW (not-yet-in-batch) nuances recorded as
  reusable considerations: (a) the `as`-cast semantic-divergence
  footgun (Dart `as` throws vs C# `as` returns null) — load-bearing
  for any future test that uses `expr as T`; (b) the Dart Set
  literal `{e}` ⇒ C# `HashSet<T>` (or C# 12+ collection
  expression) idiom; (c) the Dart Map literal `{k: v}` ⇒ C#
  `Dictionary<TK, TV>` idiom with a one-pair example; (d) the
  `hasLength(N)` matcher-routing-table row with the `N==1` ⇒
  `Assert.Single` special case.
- Identifier-PascalCasing of method names (`allocateVariable` ⇒
  `AllocateVariable`, `suspendGoalFCP` ⇒ `SuspendGoalFcp`,
  `applySigmaHatFCP` ⇒ `ApplySigmaHatFcp`) and property names (`.heap`
  ⇒ `.Heap`, `.id` ⇒ `.Id`, `.pc` ⇒ `.Pc`) is owned by the SUT specs.
  The 3+-letter-acronym Pascal-casing rule (`FCP` ⇒ `Fcp` rather
  than preserved `FCP`) follows .NET Framework Design Guidelines;
  the exact choice is whatever the SUT specs record.
- Zero escalations: every construct in this file is
  authoritative-supported on both sides; the majority REUSE idioms /
  findings recorded by sibling specs (smoke_test.dart,
  glp_runtime_test.dart, fairness_26_test.dart, test/heap/* siblings)
  per FR-012 / SC-007 KB-reuse decision order; the file-NEW
  constructs (`as`-cast, Set-literal, Map-literal, `hasLength`) are
  recorded as reusable considerations for future test conversions
  in this batch.
