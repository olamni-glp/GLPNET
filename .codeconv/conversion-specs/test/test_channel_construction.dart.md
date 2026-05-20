> Conversion-spec artifact for test/test_channel_construction.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/test_channel_construction.dart
source_sha256: d74fde5cacb1398422070b6ca4a11ad7325c200fef17c0d9b2f7d76a96fc8b90
target_code_unit: test/TestChannelConstruction.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit is the project-wide
      target framework pinned by the precedent artifacts
      `.codeconv/conversion-specs/test/multiagent/mad_error_handling_test.dart.md`
      and `.../boot_loader_test.dart.md` (idiom
      rf-dart-package-test-import-to-xunit-using; KB cache hit per
      FR-012 / SC-007 — REUSE verbatim, no re-research). Codegen MUST
      also add `using System;` (idiomatic baseline) and project this
      file's class to a single namespace mirroring the Dart `test/`
      directory (e.g. `<RootNs>.Test`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance, NOT a
      file-local choice: every `package:test` file in the inventory
      MUST map to the SAME .NET framework so test discovery, runner
      config, and attribute vocabulary stay consistent. Pinned to
      xUnit by the precedent files cited above (FLATTENed groups +
      constructor-per-test isolation + `[Fact]` 1:1 with `test(...)`).
      NUnit and MSTest remain corroborating alternatives recorded
      once at the import-idiom level — not re-derived per file.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/heap_fcp.dart';
       import 'package:glp_runtime/runtime/external_io.dart';
       import 'package:glp_runtime/runtime/terms.dart';"
    target_decision: >-
      Map each `package:glp_runtime/...` import to a `using` directive
      naming the C# namespace produced when the referenced SUT file is
      converted. The SUT files have their own dedicated convspec
      artifacts (`.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`,
      `external_io.dart.md`, `terms.dart.md`); the precise namespace
      string is decided when those files are converted, so this spec
      records only the SHAPE of the cross-file dependency (e.g.
      `using <RootNs>.Runtime;` — three separate `using` lines, one
      per imported Dart library; if the three SUT files end up in the
      same C# namespace, codegen collapses to a single `using`).
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (KB cache hit per FR-012 / SC-007 —
      REUSE from the precedent file
      test/multiagent/boot_loader_test.dart.md): in Dart `package:
      glp_runtime/...` is an explicit pubspec-anchored URI; in C#
      there is no per-file URI — only assembly + namespace. The
      conversion must therefore (a) ensure the converted SUT lives
      in a deterministic namespace derived from its relative path,
      and (b) ensure the test assembly references the SUT assembly
      via the project file (out of scope for THIS artifact — a
      project-system idiom). No `as` alias / partial import is used
      in this file, so the simple `using <Ns>;` form suffices.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { test('buildChannelTerm creates ch(Reader, Writer)', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint.
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; its
      single statement (one top-level `test(...)` call, NO enclosing
      `group(...)`) becomes a single test method on the enclosing
      class (see test_call_simple below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (explicitly addressed, KB cache hit — REUSE):
      Dart `main` is invoked once per test-file process; xUnit has no
      per-file hook. THIS file's `main` body is exactly one
      `test(...)` call with no other statements (no `group`, no
      `setUp`, no `tearDown`), so the omission is lossless.
  - construct_key: dart.package_test.test_file_no_group
    source_form: "void main() { test('<label>', () { ... }); }   // no group(...) wrapper"
    target_decision: >-
      Because there is NO `group(...)` in this file, the FLATTEN-with-
      [Trait] mapping from `dart.package_test.group_block` (precedent
      boot_loader_test.dart.md) reduces to: emit ONE PascalCase test
      class whose name is derived from the source file basename
      (`test_channel_construction.dart` -> `TestChannelConstructionTests`).
      The class contains exactly one `[Fact]` method (see
      test_call_simple). No `[Trait]` attribute is emitted (no
      group label exists). The Dart `test()` label survives via
      `[Fact(DisplayName = "buildChannelTerm creates ch(Reader, Writer)")]`.
    idiom_id: null
    research_finding_id: rf-dart-package-test-no-group-to-xunit-class-per-file
    nuance: >-
      Topology nuance (explicitly addressed): the FLATTEN rule from
      the group_block idiom is the GENERAL form — a single class
      collecting every `test(...)` in the file with optional `[Trait]`
      tags per inner group. When the file has NO `group(...)`, the
      rule collapses to "one class, no traits, one method per test".
      This is a first-class extension of the group_block idiom
      capturing the no-group degenerate case explicitly so codegen
      does not need to special-case it. Class-name derivation: take
      the Dart source basename without the `.dart` suffix, PascalCase
      it (`test_channel_construction` -> `TestChannelConstruction`),
      and append `Tests` for the conventional xUnit suffix. The
      `Test` prefix retained in the basename is intentional (it is
      the filename's name, not a fixture-class convention) and
      matches the Dart filename for trace-back parity.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('buildChannelTerm creates ch(Reader, Writer)', () { /* arrange, act, assert */ });"
    target_decision: >-
      Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance
      method on the enclosing class, decorated with
      `[Fact(DisplayName = "buildChannelTerm creates ch(Reader,
      Writer)")]`. The method name is the PascalCased,
      identifier-safe form of the label:
      `BuildChannelTermCreatesChReaderWriter`. The closure body
      converts statement-for-statement into the method body
      (`final heap = HeapFCP();` arrange; `final userChannel =
      createExternalChannel(heap, 'user');` act-1; `final term =
      buildChannelTerm(userChannel);` act-2; the eight `expect(...)`
      calls + nine `print(...)` calls translate per the constructs
      below). The closure is synchronous (no `async`/`Future`) so
      the target method is `public void`, NOT `async Task`.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      KB cache hit per FR-012 / SC-007 — REUSE from the precedent
      file test/multiagent/boot_loader_test.dart.md. Async nuance
      (carry-forward, absent in this file): an `async` closure
      would target `public async Task <Name>()`. Closure-capture
      nuance: this file's callback captures nothing from outer
      scope (no `group`/`setUp` fields) — all locals (`heap`,
      `userChannel`, `term`, `st`, `arg0`, `arg1`) are method-
      scoped, an exact match for xUnit's per-method instance.
  - construct_key: dart.final_local
    source_form: >-
      "final heap = HeapFCP();
       final userChannel = createExternalChannel(heap, 'user');
       final term = buildChannelTerm(userChannel);
       final st = term as StructTerm;
       final arg0 = st.args[0] as VarRef;
       final arg1 = st.args[1] as VarRef;"
    target_decision: >-
      Each `final <name> = <expr>;` local maps to `var <name> =
      <expr>;` in C#. Dart `final` on a local makes it single-
      assignment; C# has no per-local `readonly` keyword for method-
      locals, but `var` + xUnit's per-method instance lifetime and
      the absence of any re-assignment in the method body gives
      observably equivalent behaviour (no analyser warning required
      because the locals are not re-assigned). Recorded precedents:
      test/heap/varref_pointer_test.dart.md, test/multiagent/
      localize_test.dart.md, test/multiagent/global_send_test.dart.md
      (KB cache hit per FR-012 / SC-007 — REUSE).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Single-assignment nuance (carry-forward): Dart `final` blocks
      re-assignment at compile time; C# `var` does not. Codegen
      MUST verify no later statement in the method re-assigns the
      local (verifiable by simple AST inspection); this file has
      no re-assignment, so `var` is correct. For class-level
      fields the mapping is different (`final` field -> C# `readonly`
      field) but that case does not occur in this file.
  - construct_key: dart.constructor_invocation_no_args
    source_form: "HeapFCP()"
    target_decision: >-
      Dart implicit-`new` constructor invocation `HeapFCP()` maps to
      C# `new HeapFCP()`. SUT type per precedent
      `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md` (top-
      level type `HeapFCP` → C# `class HeapFCP` — the convspec for
      `heap_fcp.dart` preserves the PascalCase `HeapFCP` identifier;
      codegen MUST consult that artifact for the exact final class
      name including any `Fcp`/`FCP` casing decision and apply it
      verbatim here for cross-file consistency).
    idiom_id: rf-dart-constructor-invocation-implicit-new-to-csharp-new
    research_finding_id: rf-dart-constructor-invocation-implicit-new-to-csharp-new
    nuance: >-
      Implicit-`new` nuance: since Dart 2 the `new` keyword is
      optional; this file uses the no-`new` form. C# (pre-9) REQUIRES
      `new T()`; C# 9+ allows `T x = new()` as a target-typed form
      but the explicit `new HeapFCP()` is the safer choice on the
      right-hand side of `var` (target type is inferred, not given).
      No constructor arguments → no value/reference passing nuance
      applies.
  - construct_key: dart.function_call_top_level
    source_form: >-
      "createExternalChannel(heap, 'user');
       buildChannelTerm(userChannel);"
    target_decision: >-
      Dart top-level functions map to C# `public static` methods on a
      designated utility class (per the SUT convspec
      `.codeconv/conversion-specs/lib/runtime/external_io.dart.md`,
      `createExternalChannel` and `buildChannelTerm` become PascalCase
      static methods, e.g. `ExternalIo.CreateExternalChannel(heap,
      "user")` and `ExternalIo.BuildChannelTerm(userChannel)` — the
      precise host class name is whatever external_io.dart.md
      decides; codegen MUST consult that artifact and apply it
      verbatim here for cross-file consistency).
    idiom_id: rf-dart-top-level-function-callsite-to-csharp-static-method
    research_finding_id: rf-dart-top-level-function-callsite-to-csharp-static-method
    nuance: >-
      Top-level-function nuance (explicitly addressed): Dart permits
      file-level (top-level) functions; C# does NOT — every method
      must live on a type. The SUT convspec
      external_io.dart.md decides the host class once for all
      top-level functions in that library; every callsite (this
      file's two callsites included) MUST reference the SAME host.
      Naming nuance: Dart `createExternalChannel` (camelCase) →
      C# `CreateExternalChannel` (PascalCase). String-literal nuance:
      Dart single-quoted `'user'` is identical in semantics to a
      C# double-quoted `"user"` for ASCII-only payloads (no escape
      processing differences) — the literal contains no escapes.
  - construct_key: dart.string.single_quoted_literal
    source_form: "'user'"
    target_decision: >-
      Dart single-quoted string literals (no interpolation, no
      escapes here) map to C# double-quoted string literals: `"user"`.
      No raw-string / verbatim-string treatment needed (no embedded
      newlines or quotes in any literal in this file).
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Quote-style nuance: Dart accepts both `'...'` and `"..."`
      with identical semantics; C# only accepts `"..."` for plain
      strings (and `@"..."` or `"""..."""` for verbatim/raw). This
      file's lone literal `'user'` contains no escapes or `"` so
      the mapping is mechanical. Encoding nuance: both Dart and
      C# strings are UTF-16; no transcoding required.
  - construct_key: dart.core.print
    source_form: >-
      "print('User channel: $userChannel');
       print('  inputWriterAddr: ${userChannel.inputWriterAddr}');
       print('  inputReaderAddr: ${userChannel.inputReaderAddr}');
       print('  outputWriterAddr: ${userChannel.outputWriterAddr}');
       print('  outputReaderAddr: ${userChannel.outputReaderAddr}');
       print('\\nChannel term: $term');
       print('  arg[0]: VarRef(${arg0.addr}) isReader=${heap.isReader(arg0.addr)}');
       print('  arg[1]: VarRef(${arg1.addr}) isReader=${heap.isReader(arg1.addr)}');
       print('\\n✓ Channel term is ch(Reader, Writer) as expected');"
    target_decision: >-
      Dart top-level `print(String)` writes to stdout with a trailing
      newline. The exact xUnit-idiomatic target is `ITestOutputHelper.
      WriteLine(...)` — xUnit captures per-test stdout for the test
      reporter via a constructor-injected `ITestOutputHelper`. The
      target class therefore takes `ITestOutputHelper output` in its
      constructor, stores it in a `_output` field, and every `print
      (...)` callsite becomes `_output.WriteLine(...)`. This preserves
      the per-test diagnostic-trace semantics of the Dart `print`
      calls (which are pure observers, not load-bearing assertions —
      every assertion in this file is a separate `expect(...)`).
      `Console.WriteLine` is a viable but inferior fallback (it is
      NOT captured by xUnit's test runner and pollutes the global
      console); the research finding records BOTH targets with
      `ITestOutputHelper` chosen.
    idiom_id: null
    research_finding_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    nuance: >-
      Diagnostic-output nuance (explicitly addressed, well-known
      xUnit footgun): xUnit deliberately does NOT capture `Console.
      Out` — using `Console.WriteLine` from a test silently writes
      to the build agent's stdout but does NOT appear under the
      test in the reporter (VS Test Explorer, `dotnet test --logger
      trx`, Rider). The documented xUnit mechanism is `ITestOutput
      Helper` injected through the test class constructor (xUnit
      docs `https://xunit.net/docs/capturing-output`). Per-test
      isolation: the helper is unique per test instance, so output
      from one `[Fact]` cannot bleed into another's report.
      Escape-character nuance: the two `print('\\n...')` calls use
      Dart `\\n` which is processed as a newline. C# string
      literals also process `\\n`; the literal payload is
      byte-identical after escape processing. The UTF-8 checkmark
      character `✓` in the final `print` survives unchanged
      because both Dart and C# string literals accept the literal
      `✓` glyph encoded directly (UTF-16 code unit 0x2713).
  - construct_key: dart.string.interpolation
    source_form: >-
      "'User channel: $userChannel'
       '  inputWriterAddr: ${userChannel.inputWriterAddr}'
       '\\nChannel term: $term'
       '  arg[0]: VarRef(${arg0.addr}) isReader=${heap.isReader(arg0.addr)}'"
    target_decision: >-
      Dart string interpolation (`$name` / `${expr}`) maps to C#
      interpolated strings (`$"...{name}...{expr}..."`). All nine
      interpolated literals in this file become
      `$"User channel: {userChannel}"`,
      `$"  inputWriterAddr: {userChannel.InputWriterAddr}"`,
      `$"\\nChannel term: {term}"`,
      `$"  arg[0]: VarRef({arg0.Addr}) isReader={heap.IsReader(arg0.Addr)}"`,
      etc. The implicit `.ToString()` invocation that Dart performs
      on each interpolated expression is matched by C#'s
      `IFormattable`/`Object.ToString()` invocation inside `$"..."`.
      Per precedent `.codeconv/conversion-specs/lib/runtime/
      terms.dart.md` (idiom rf-dart-string-interpolation-join-to-
      csharp-interpolation-string-join), simple field-reference
      interpolations (no `.join(...)` involved) reuse THIS first-
      class entry; the `-join-` variant from terms.dart.md is the
      richer form for `Iterable.join` round-trips and is NOT used
      here.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Field-name-casing nuance (explicitly addressed): Dart
      `userChannel.inputWriterAddr` (camelCase property/field) MUST
      translate to C# `userChannel.InputWriterAddr` (PascalCase per
      external_io.dart.md's `ExternalChannel` class definition).
      The interpolation expression must therefore be RE-EMITTED
      (not copied verbatim) using the PascalCased property name;
      this is a per-construct duty of codegen, not a per-string
      one. ToString nuance: `$userChannel` invokes
      `ExternalChannel.toString()` in Dart (overridden to a custom
      format per external_io.dart.md construct
      `dart.class.tostring_interpolation_override`); the C# target
      is `ExternalChannel.ToString()` (overridden identically per
      that same convspec). The interpolation faithfully forwards
      that decision.
  - construct_key: dart.downcast.as_expression
    source_form: >-
      "term as StructTerm;
       st.args[0] as VarRef;
       st.args[1] as VarRef;"
    target_decision: >-
      Dart `<expr> as T` is a checked downcast (throws `TypeError` on
      mismatch). The idiomatic C# equivalent is the explicit cast
      `(T)<expr>` (throws `InvalidCastException` on mismatch) — a
      direct semantic match. Three callsites in this file:
      `var st = (StructTerm)term;`,
      `var arg0 = (VarRef)st.Args[0];`,
      `var arg1 = (VarRef)st.Args[1];`.
      Two precedent specs already recorded this idiom:
      test/heap/binding_pointer_test.dart.md and
      test/heap/varref_pointer_test.dart.md (KB cache hit per
      FR-012 / SC-007 — REUSE verbatim).
    idiom_id: rf-dart-as-cast-to-csharp-explicit-cast
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cast-failure-mode nuance (carry-forward): both `as` and the
      C# explicit cast throw on mismatch; the C# `as` keyword
      (which returns `null` on mismatch) is NOT the right target
      because the Dart `as` does not. Fold-into-IsType nuance: each
      `as`-cast in this file is PRECEDED by an `expect(<expr>,
      isA<T>())` assertion on the SAME `<expr>` — the
      `dart.package_test.expect_isA_T` precedent (binding_pointer_
      test.dart.md) records the fold pattern `expect(r, isA<T>());
      var x = r as T;` -> `var x = Assert.IsType<T>(r);` which
      eliminates the redundant explicit cast. Codegen for this
      file SHOULD apply that fold for all THREE pairs (term/st,
      st.args[0]/arg0, st.args[1]/arg1) — see the
      `expect_isA_T_with_as_fold` construct below for the
      authoritative emission shape.
  - construct_key: dart.package_test.expect_isA_T
    source_form: >-
      "expect(term, isA<StructTerm>());
       expect(st.args[0], isA<VarRef>());
       expect(st.args[1], isA<VarRef>());"
    target_decision: >-
      Map each `expect(<expr>, isA<T>())` to xUnit `Assert.IsType<T>
      (<expr>)`. Per precedent test/heap/binding_pointer_test.dart.md
      (idiom rf-dart-expect-isA-to-xunit-assert-istype; KB cache hit
      per FR-012 / SC-007 — REUSE). Each of the three `isA<T>()`
      targets here (`StructTerm`, `VarRef`, `VarRef`) is a CONCRETE
      sealed `Term` leaf per the SUT convspec
      `.codeconv/conversion-specs/lib/runtime/terms.dart.md` — no
      known subtypes exist, so `Assert.IsType<T>` (exact-type) is
      observably equivalent to Dart `isA<T>` (subtype-tolerant)
      and gives a strictly tighter assertion.
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-vs-subtype nuance (carry-forward from
      binding_pointer_test.dart.md): Dart `isA<T>` accepts SUBTYPES;
      xUnit `Assert.IsType<T>` does NOT (requires `actual.GetType()
      == typeof(T)`); `Assert.IsAssignableFrom<T>` does. In THIS file
      every `isA<T>()` target is a CONCRETE sealed-Term leaf
      (`StructTerm`, `VarRef`) per terms.dart.md — both are sealed
      with no known subtypes. Codegen SHOULD emit `Assert.IsType<T>`
      because it is observably equivalent and gives a strictly
      tighter assertion. If a future test exercises a subtype, the
      mapping promotes to `Assert.IsAssignableFrom<T>` (recorded
      in the rf).
  - construct_key: dart.package_test.expect_isA_T_with_as_fold
    source_form: >-
      "expect(term, isA<StructTerm>()); final st = term as StructTerm;
       expect(st.args[0], isA<VarRef>()); final arg0 = st.args[0] as VarRef;
       expect(st.args[1], isA<VarRef>()); final arg1 = st.args[1] as VarRef;"
    target_decision: >-
      The recurring pair `expect(<expr>, isA<T>());  var <name> =
      <expr> as T;` folds into a single xUnit call:
      `var <name> = Assert.IsType<T>(<expr>);` — `Assert.IsType<T>`
      both asserts and returns the value cast to `T` (xUnit API
      contract). This SUPERSEDES the separate
      `dart.package_test.expect_isA_T` + `dart.downcast.as_expression`
      emissions for these three matched pairs. The three folded
      emissions are:
      `var st = Assert.IsType<StructTerm>(term);`
      `var arg0 = Assert.IsType<VarRef>(st.Args[0]);`
      `var arg1 = Assert.IsType<VarRef>(st.Args[1]);`
      Per precedent
      test/analysis/type_checker/moded_head_test.dart.md cu-7:
      "fold each `expect(r, isA<T>()); var x = r as T;` pair into
      `var x = Assert.IsType<T>(r);`" (KB cache hit per FR-012 /
      SC-007 — REUSE).
    idiom_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    research_finding_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    nuance: >-
      Fold-correctness nuance (explicitly addressed): the fold is
      semantically safe IFF the `<expr>` is referentially-transparent
      (no side-effects between the two callsites). All three
      occurrences in this file use trivially-pure expressions
      (`term`, `st.args[0]`, `st.args[1]` — pure field/indexer
      reads on already-bound locals). For any `<expr>` containing a
      method call with side-effects, the fold is UNSAFE and codegen
      must emit the un-folded shape (recorded in the rf). Cast-
      failure-mode nuance: `Assert.IsType<T>` throws
      `Xunit.Sdk.IsTypeException` on mismatch (NOT
      `InvalidCastException` like an explicit cast); both are
      observable test failures, but `IsTypeException` carries the
      richer xUnit diagnostic.
  - construct_key: dart.package_test.expect_equals
    source_form: >-
      "expect(st.functor, equals('ch'));
       expect(st.args.length, equals(2));
       expect(arg0.addr, equals(userChannel.inputReaderAddr));
       expect(arg1.addr, equals(userChannel.outputWriterAddr));"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)` with the
      ARGUMENT-ORDER FLIP applied (Dart puts actual first; xUnit
      puts expected first). The four emissions are:
      `Assert.Equal("ch", st.Functor);`
      `Assert.Equal(2, st.Args.Count);`
      `Assert.Equal(userChannel.InputReaderAddr, arg0.Addr);`
      `Assert.Equal(userChannel.OutputWriterAddr, arg1.Addr);`
      Per precedent test/multiagent/boot_loader_test.dart.md (idiom
      rf-dart-expect-equals-to-xunit-assertequal; KB cache hit per
      FR-012 / SC-007 — REUSE).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (carry-forward — well-known footgun):
      reversing actual/expected silently produces correct-looking
      but misleading failure messages. Codegen MUST emit
      `Assert.Equal(expected, actual)`. Length-vs-count nuance:
      Dart `List.length` (`st.args.length`) maps to C# `List<T>.
      Count` (`st.Args.Count`) — recorded under
      rf-dart-list-length-to-csharp-list-count. Value-vs-reference
      nuance: the four `equals` callsites compare `String`
      (`functor`), `int` (`length`), and `int` (`addr`) — all
      value-typed with default `Equals` semantics matching Dart's
      `==`.
  - construct_key: dart.list.length
    source_form: "st.args.length"
    target_decision: >-
      Dart `List<T>.length` (getter) maps to C# `List<T>.Count`
      (property). Recorded once at the construct level so every
      future `<list>.length` callsite reuses this mapping verbatim.
    idiom_id: rf-dart-list-length-to-csharp-list-count
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Naming nuance: Dart standardises on `.length` for both
      `List` (collection size) and `String` (UTF-16 code unit
      count); C# standardises on `.Count` for `List<T>`/`IList<T>`/
      `ICollection<T>` and `.Length` for `string`/arrays/`Span<T>`.
      Codegen MUST disambiguate by the receiver's static type: a
      `List<Term>` member `.length` → `.Count`; a `String` member
      `.length` → `.Length`. THIS file's lone `.length` use is on
      `st.args` whose static type is `List<Term>` per
      terms.dart.md's `StructTerm` definition, so `.Count` is
      correct.
  - construct_key: dart.list.indexer
    source_form: >-
      "st.args[0]
       st.args[1]"
    target_decision: >-
      Dart `List<T>` integer indexer `list[i]` maps to C# `List<T>`
      indexer `list[i]` — direct syntactic correspondence. Per
      precedent test/heap/binding_pointer_test.dart.md (idiom
      rf-dart-list-indexing-to-csharp-list-indexer; KB cache hit
      per FR-012 / SC-007 — REUSE).
    idiom_id: rf-dart-list-indexing-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexing-to-csharp-list-indexer
    nuance: >-
      Out-of-range nuance (carry-forward): both Dart and C# throw
      on out-of-range index (`RangeError` vs
      `ArgumentOutOfRangeException`); neither performs silent
      clamping. Mutability nuance: Dart `List<T>` is mutable by
      default; C# `List<T>` is mutable; both `[i]` reads are
      side-effect-free.
  - construct_key: dart.method_invocation.bool_return
    source_form: >-
      "heap.isReader(arg0.addr)
       heap.isReader(arg1.addr)"
    target_decision: >-
      Dart instance-method call `heap.isReader(addr)` maps to C#
      `heap.IsReader(addr)` (PascalCase rename per the SUT convspec
      `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`,
      which decides the final method name). The return type is
      `bool` in both languages — no nullability or three-valued-
      logic nuance. Used in the two `expect(<bool-expr>, is{True,
      False})` constructs below and in two `print(...)` interpolations.
    idiom_id: rf-dart-instance-method-call-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-call-camelcase-to-csharp-pascalcase
    nuance: >-
      Naming-convention nuance (explicitly addressed): Dart instance
      methods use camelCase by convention; C# instance methods use
      PascalCase. Every method-call site must be transliterated.
      Codegen MUST consult the SUT convspec (here heap_fcp.dart.md)
      for the canonical PascalCase name — DO NOT mechanically
      upper-case the first letter; some SUT convspecs may decide
      a different rename (e.g. abbreviation expansion). For this
      file, `isReader` → `IsReader` is the documented choice.
  - construct_key: dart.package_test.expect_isTrue_with_reason
    source_form: "expect(heap.isReader(arg0.addr), isTrue, reason: 'First arg should be reader');"
    target_decision: >-
      Map to xUnit `Assert.True(heap.IsReader(arg0.Addr), "First arg
      should be reader");` — xUnit `Assert.True` has an overload
      `Assert.True(bool condition, string userMessage)` which is the
      direct target for Dart's `reason:` named argument. The
      `userMessage` parameter is emitted ONLY when the Dart call
      passes `reason:` (this file's single `isTrue` callsite does).
      Per precedent test/heap/varref_pointer_test.dart.md (idiom
      rf-dart-expect-isTrue-to-xunit-assert-true; KB cache hit per
      FR-012 / SC-007 — REUSE) + the diagnostic-nuance addendum
      from boot_loader_test.dart.md noting the optional
      `userMessage` overload.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Reason/userMessage nuance (explicitly addressed): Dart
      `expect(..., reason: 'msg')` annotates the failure with a
      human-readable explanation; xUnit `Assert.True(bool, string)`
      passes the explanation as `userMessage` which the runner
      embeds in the failure report. Mapping the Dart `reason:` to
      `userMessage` is faithful and lossless. Default value: if
      the Dart call omits `reason:`, the C# emission MUST also
      omit the second argument (do NOT emit an empty string —
      `Assert.True(b, "")` produces an empty-message line in the
      report, slightly noisier than `Assert.True(b)`).
  - construct_key: dart.package_test.expect_isFalse_with_reason
    source_form: "expect(heap.isReader(arg1.addr), isFalse, reason: 'Second arg should be writer');"
    target_decision: >-
      Map to xUnit `Assert.False(heap.IsReader(arg1.Addr), "Second
      arg should be writer");` — symmetric to the `isTrue` mapping
      above. xUnit `Assert.False` has an overload
      `Assert.False(bool condition, string userMessage)`. Per
      precedent test/heap/varref_pointer_test.dart.md (idiom
      rf-dart-expect-isFalse-to-xunit-assert-false; KB cache hit
      per FR-012 / SC-007 — REUSE) + the reason/userMessage
      addendum recorded above for the `isTrue` counterpart.
    idiom_id: rf-dart-expect-isFalse-to-xunit-assert-false
    research_finding_id: rf-dart-expect-isFalse-to-xunit-assert-false
    nuance: >-
      Symmetry nuance (carry-forward): every property recorded
      for `isTrue`/`Assert.True` (reason mapping, userMessage
      overload semantics, omission rule) applies identically to
      `isFalse`/`Assert.False`. THIS file is the first to use
      BOTH `isTrue` and `isFalse` with `reason:` in a single
      method — the symmetric mapping is mechanical.
  - construct_key: dart.member_access.field
    source_form: >-
      "userChannel.inputWriterAddr
       userChannel.inputReaderAddr
       userChannel.outputWriterAddr
       userChannel.outputReaderAddr
       st.functor
       st.args
       arg0.addr
       arg1.addr"
    target_decision: >-
      Dart camelCase field/property access maps to C# PascalCase
      property access per the SUT convspecs for `ExternalChannel`
      (external_io.dart.md), `StructTerm` (terms.dart.md), and
      `VarRef` (terms.dart.md). The eight callsites become
      `userChannel.InputWriterAddr`, `userChannel.InputReaderAddr`,
      `userChannel.OutputWriterAddr`, `userChannel.OutputReaderAddr`,
      `st.Functor`, `st.Args`, `arg0.Addr`, `arg1.Addr`.
    idiom_id: rf-dart-camelcase-field-to-csharp-pascalcase-property
    research_finding_id: rf-dart-camelcase-field-to-csharp-pascalcase-property
    nuance: >-
      Field-vs-property nuance (explicitly addressed): in Dart,
      class members declared `final int inputWriterAddr;` are
      fields with implicit getters; in C# the idiomatic
      translation is an auto-property `public int InputWriterAddr
      { get; }` (NOT a public field — C# style forbids public
      fields on data-bearing types). The READ-side syntax
      (`obj.X`) is identical, so this idiom records ONLY the
      naming rename; the field-vs-property choice is the SUT
      convspec's responsibility. Per-property authority: codegen
      MUST consult each owning SUT convspec for the canonical
      target identifier (do NOT mechanically PascalCase if the
      SUT convspec has decided a non-mechanical rename).
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System + SUT namespaces from glp_runtime/runtime/heap_fcp.dart, external_io.dart, terms.dart)"
  - "cu-2: namespace declaration mirroring the test/ path (no group nesting in this file)"
  - "cu-3: top-level test class TestChannelConstructionTests with constructor injecting ITestOutputHelper output and storing it in a private _output field"
  - 'cu-4: ONE `[Fact(DisplayName = "buildChannelTerm creates ch(Reader, Writer)")]` public-void method BuildChannelTermCreatesChReaderWriter'
  - 'cu-5: method body line-for-line — arrange (var heap = new HeapFCP();), act-1 (var userChannel = ExternalIo.CreateExternalChannel(heap, "user");), 5 `_output.WriteLine($"...{userChannel.<Pascal>Addr}...")` diagnostics, act-2 (var term = ExternalIo.BuildChannelTerm(userChannel);), 1 `_output.WriteLine($"\nChannel term: {term}")` diagnostic'
  - "cu-6: folded `var st = Assert.IsType<StructTerm>(term);` (collapses expect_isA_T + as-cast pair)"
  - 'cu-7: `Assert.Equal("ch", st.Functor);` and `Assert.Equal(2, st.Args.Count);`'
  - 'cu-8: folded `var arg0 = Assert.IsType<VarRef>(st.Args[0]);` (collapses expect_isA_T + as-cast pair), one `_output.WriteLine` diagnostic with interpolated `arg0.Addr` and `heap.IsReader(arg0.Addr)`, `Assert.True(heap.IsReader(arg0.Addr), "First arg should be reader");`, `Assert.Equal(userChannel.InputReaderAddr, arg0.Addr);`'
  - 'cu-9: folded `var arg1 = Assert.IsType<VarRef>(st.Args[1]);` (collapses expect_isA_T + as-cast pair), one `_output.WriteLine` diagnostic, `Assert.False(heap.IsReader(arg1.Addr), "Second arg should be writer");`, `Assert.Equal(userChannel.OutputWriterAddr, arg1.Addr);`'
  - 'cu-10: final `_output.WriteLine($"\n✓ Channel term is ch(Reader, Writer) as expected");` diagnostic'
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative, KB cache hit)

This is the Nth `package:test` file specced for this inventory. xUnit
was pinned as the project-wide target by the FIRST file specced
(`test/multiagent/mad_error_handling_test.dart.md`) and reused
verbatim by every subsequent file (boot_loader_test, global_send_test,
binding_pointer_test, varref_pointer_test, etc.). Maintaining the pin
satisfies SC-007 (≥95% of recurring constructs resolved via a recorded
idiom). The authoritative basis remains the xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / constructor-as-setUp / `Assert.*` semantics, and the Dart
`package:test` README on `pub.dev` (`https://pub.dev/packages/test`)
for `test()` / `expect()` / matcher semantics. No re-research per
FR-024.

### Why one test class, no `[Trait]` (no-group degenerate case)

This file has NO `group(...)` wrapper — `main()` contains exactly one
`test(...)` call. The FLATTEN-with-`[Trait]` rule from
boot_loader_test.dart.md's `dart.package_test.group_block` reduces to
"one class, no traits, one method". A new idiom
`rf-dart-package-test-no-group-to-xunit-class-per-file` is recorded
to make this degenerate case first-class so codegen has an explicit
pattern to look up rather than reasoning from the general rule.

### Why `ITestOutputHelper.WriteLine`, not `Console.WriteLine`

The Dart `print(...)` calls in this file are pure diagnostic
observers — they describe the channel structure but every assertion
is a separate `expect(...)`. The xUnit-documented mechanism for
per-test diagnostic output is `ITestOutputHelper`, injected through
the test-class constructor (`https://xunit.net/docs/capturing-output`).
`Console.WriteLine` is NOT captured by xUnit's runner; it would
silently leak to the build agent's stdout and break the reporter
view, even though it would still "run". This is the canonical
xUnit footgun and is recorded explicitly in the new idiom
`rf-dart-print-to-xunit-itestoutputhelper-writeline` so subsequent
test-file convspecs reuse it without re-derivation.

### Fold `expect(r, isA<T>()); var x = r as T;` into `Assert.IsType<T>(r)`

Three places in this file use the exact `expect(r, isA<T>()); final
x = r as T;` shape (term/StructTerm, st.args[0]/VarRef,
st.args[1]/VarRef). The xUnit `Assert.IsType<T>(actual)` overload
both asserts the type AND returns the value cast to T
(`https://xunit.net/docs/comparisons#exceptions` — note `IsType`
return value). The fold collapses two statements into one, retains
both the assertion semantics (subtype-strict, matching the
sealed-leaf reality of `StructTerm`/`VarRef`) AND the typed
downcast. Recorded once at the construct level
(`rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`) and
applied uniformly to all three matching pairs. Safety check: the
`<expr>` must be referentially transparent — all three here are
pure field/indexer reads on bound locals, fold is safe.

### `reason:` → `userMessage` overload

Two `expect(...)` calls in this file pass `reason: '<msg>'`. xUnit
`Assert.True(bool, string userMessage)` and `Assert.False(bool,
string userMessage)` overloads accept the message as the second
argument
(`https://learn.microsoft.com/dotnet/api/xunit.assert.true`). The
mapping is mechanical: every `reason:` passes through to
`userMessage`; omission of `reason:` MUST also omit the second C#
argument (do not emit empty strings). Recorded as a nuance on the
two existing precedent idioms
(`rf-dart-expect-isTrue-to-xunit-assert-true`,
`rf-dart-expect-isFalse-to-xunit-assert-false`), not as a new
idiom — the base mapping is unchanged, only an addendum about the
overload.

### Argument-order flip on `Assert.Equal` (carry-forward)

Every `equals(...)` call MUST be flipped at the boundary per the
documented xUnit convention. Four occurrences in this file:
`Assert.Equal("ch", st.Functor)`, `Assert.Equal(2, st.Args.Count)`,
`Assert.Equal(userChannel.InputReaderAddr, arg0.Addr)`,
`Assert.Equal(userChannel.OutputWriterAddr, arg1.Addr)`. This is the
most common silent-bug source when porting test code and is the
single most-cited nuance across this inventory's test convspecs.

### Cross-file dependencies on SUT convspecs (FR-009 / FR-010)

This file's behaviour depends on three SUT types whose conversion
shape is decided elsewhere:

- `HeapFCP` (constructor + `IsReader(int)` method) — decided by
  `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`.
- `ExternalChannel` (5 fields/properties + `ToString()`),
  `createExternalChannel(HeapFCP, String)` top-level function,
  `buildChannelTerm(ExternalChannel)` top-level function — all
  decided by
  `.codeconv/conversion-specs/lib/runtime/external_io.dart.md`.
- `StructTerm` (`Functor`, `Args`), `VarRef` (`Addr`) — decided by
  `.codeconv/conversion-specs/lib/runtime/terms.dart.md`.

Codegen for THIS test file MUST consult each of those SUT convspec
artifacts for the exact final identifier choices (class name,
property name, host-class for top-level functions). The test
convspec records only the SHAPE of the cross-file references; it
does NOT duplicate or override the SUT decisions. This split keeps
the SUT convspecs as the single source of truth and prevents
drift if the SUT identifiers are renamed downstream.

### Why no escalations

Every construct has a clear, single-decision target shape grounded
in official documentation for both Dart `package:test` / Dart core
(`print`, string interpolation, `final` locals, `as` casts) and
xUnit / .NET (`Assert.IsType` return, `[Fact(DisplayName)]`,
`ITestOutputHelper`, `Assert.True`/`False` userMessage overload).
The four prior decisions reused via KB cache hit
(`rf-dart-package-test-import-to-xunit-using`,
`rf-dart-package-test-main-omit-in-xunit`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-as-cast-to-csharp-explicit-cast`,
`rf-dart-expect-isA-to-xunit-assert-istype`,
`rf-dart-expect-equals-to-xunit-assertequal`,
`rf-dart-list-indexing-to-csharp-list-indexer`,
`rf-dart-expect-isTrue-to-xunit-assert-true`,
`rf-dart-expect-isFalse-to-xunit-assert-false`) are stable
project-wide pins, not unresolved choices. The three new idioms
introduced by this file
(`rf-dart-package-test-no-group-to-xunit-class-per-file`,
`rf-dart-print-to-xunit-itestoutputhelper-writeline`,
`rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`,
`rf-dart-constructor-invocation-implicit-new-to-csharp-new`,
`rf-dart-top-level-function-callsite-to-csharp-static-method`,
`rf-dart-single-quoted-string-to-csharp-double-quoted-string`,
`rf-dart-instance-method-call-camelcase-to-csharp-pascalcase`,
`rf-dart-list-length-to-csharp-list-count`,
`rf-dart-camelcase-field-to-csharp-pascalcase-property`) each
have a single authoritative target shape from official docs and
will become KB cache hits for subsequent test files.
`escalations: []` is therefore intentional, not a placeholder.
