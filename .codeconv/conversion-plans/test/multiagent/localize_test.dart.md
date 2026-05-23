---
path: test/multiagent/localize_test.dart
cycle_group_id: 147
scc_siblings: []
generated_at: 2026-05-21T15:24:56Z
source_sha256: 18f65f67b39c84df4e9b09b357301bb42bfb69b6b2660592ef2a251ee8976ec7
schema_version: 1
---

# Conversion Plan: test/multiagent/localize_test.dart

## 1. Source Analysis

The Dart source `glp_runtime_net/test/multiagent/localize_test.dart` is a
synchronous `package:test` test file (148 lines) for the `Localize`
operation derived from `madGLP-spec.md` Section 5.2. The file contains:

- File-level doc comment (lines 1-7) describing the operation under test
  with reference to madGLP-spec Section 5.2.
- Three `import` directives (lines 9-11):
  - `import 'package:test/test.dart';` — the test framework
    (third-party package).
  - `import 'package:glp_runtime/multiagent/global_writers_table.dart';`
    — in-repo SUT import.
  - `import 'package:glp_runtime/multiagent/mad_helpers.dart';` — in-repo
    SUT import.
- `void main()` entrypoint (line 13) containing exactly one `group(...)`
  call labelled `'Localize'`.
- Three `test(...)` callbacks inside the group (no `skip:` argument on
  any):
  1. `'_w(p,i): spawns global_send, returns writer'` (lines 15-57).
  2. `'_r(p,i): creates entry with remote index, returns reader'`
     (lines 59-101).
  3. `'mixed global names: correct handling'` (lines 103-145).
- Per-test arrange-act-assert structure with embedded Given/When/Then
  comments and `Spec Section 5.2`/`Spec Section 5.3` inline references.
- Constructs observed at source inspection (each row corresponds to a
  construct in §2 below):
  - `final table = GlobalWritersTable('q');` — Dart `final` local with
    constructor invocation (no `new` in Dart).
  - `final globalNames = [GlobalName.writer('p', 5)];` etc. — list
    literal of `GlobalName` static-factory calls.
  - `var nextAddr = 100;` (and `200`, `300`) — mutable numeric local.
  - `(int, int) allocateAddr() { ... }` — local function declaration
    returning a Dart 3 positional record `(int, int)`, capturing the
    enclosing mutable `nextAddr` and post-incrementing it (`nextAddr++`).
  - `final result = localize(globalNames: ..., localAgent: 'q', table:
    ..., freshAddrAllocator: allocateAddr);` — named-argument invocation
    of a top-level Dart function whose `freshAddrAllocator` parameter
    type is Dart `(int, int) Function()`.
  - `result.freshPairs[0].writerAddr` etc. — list-indexing + member
    property access.
  - `result.freshPairs.length` and `result.spawns.length` — `List<T>`
    `.length` getter.
  - `expect(actual, value)` (≈18 sites) — implicit-equals matcher.
  - `expect(result.spawns, isEmpty);` (1 site, line 100) — `isEmpty`
    matcher.
  - `expect(entry, isNotNull);` (2 sites, lines 89 and 141) — `isNotNull`
    matcher.
  - `entry!.writerAddr` (2 sites, lines 90 and 142) — null-assertion
    bang operator.
- No `async`/`await`/`Future`/`Stream`/`Completer`/`Isolate` surface.
- No `setUp`/`tearDown`/`setUpAll`/`tearDownAll`/`skip:`/`tags:`/nested
  `group(...)` surface.
- No mutation of state across tests (each test constructs its own
  `GlobalWritersTable`, `nextAddr`, `allocateAddr`).

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the RATIFIED convspec at
`.codeconv/conversion-specs/test/multiagent/localize_test.dart.md` (FR-011
artefact, schema_version 1, source_sha256 matches). Each Dart construct
maps to the C# emission described in the convspec; references below cite
the convspec's `constructs:` entries.

1. **`dart.package_test.import_directive`** —
   `import 'package:test/test.dart';` → drop the Dart directive; emit
   `using Xunit;` at file scope (FR-012 / SC-007 batch-wide framework
   pin). Codegen also emits `using System;` at file scope for
   project-uniform shape. No new framework surface introduced by this
   file (no setUp/tearDown, no setUpAll, no skip, no tags, no async).
2. **`dart.package_test.import_sut_relative_package`** — both
   `package:glp_runtime/multiagent/global_writers_table.dart` and
   `package:glp_runtime/multiagent/mad_helpers.dart` resolve to in-repo
   Dart libraries → emit a SINGLE `using <RootNs>.Multiagent;` (de-dup
   after Dart-directory→C#-namespace mapping). The SUT namespace string
   is pinned by the SUT specs (`global_writers_table.dart.md` +
   `mad_helpers.dart.md`); this file records the dependency, not the
   resolved string. Project-file wiring (`<ProjectReference>` from the
   test .csproj to the runtime .csproj) is langpair-skeleton-level
   (OUT OF SCOPE here, per convspec construct row).
3. **`dart.package_test.main_entrypoint`** — drop Dart `void main()`
   entirely; xUnit discovers `[Fact]` methods on `public` classes by
   reflection. The `main` body here is exactly one `group(...)` call
   with no other statements → omission is lossless.
4. **`dart.package_test.group_block`** — `group('Localize', body)` →
   `public class LocalizeTests` (Pascal-cased group label + `Tests`
   suffix). Optional `[Trait("Group", "Localize")]` on the class for
   reporter parity. No nested groups, no group-level setUp/tearDown →
   xUnit per-test fresh-instance lifecycle applies with NO constructor
   fixture needed.
5. **`dart.package_test.test_call_executable`** — each Dart `test(label,
   body)` (no `skip:`) → `public void` method decorated with
   `[Fact(DisplayName = "<original label>")]` (NOT `[Fact(Skip=...)]`,
   contrast with mad_error_handling_test.dart). Method name = label
   PascalCased with non-identifier chars stripped:
   - `'_w(p,i): spawns global_send, returns writer'` →
     `WriterPISpawnsGlobalSendReturnsWriter`.
   - `'_r(p,i): creates entry with remote index, returns reader'` →
     `ReaderPICreatesEntryWithRemoteIndexReturnsReader`.
   - `'mixed global names: correct handling'` →
     `MixedGlobalNamesCorrectHandling`.
   Given/When/Then comments + `Spec Section 5.2`/`Spec Section 5.3`
   inline references carried into the target as `/// <summary>` XML-doc
   blocks per method.
6. **`dart.statement.local_function_declaration_returning_record`** —
   `(int, int) allocateAddr() { final w = nextAddr++; final r =
   nextAddr++; return (w, r); }` → C# LOCAL FUNCTION (C# 7+):
   `(long, long) allocateAddr() { var w = nextAddr++; var r =
   nextAddr++; return (w, r); }` declared inside the same method body.
   - NOT marked `static` (closure over mutable `nextAddr`).
   - NOT collapsed to a `Func<(long, long)>` lambda (Dart source is a
     named local function; C# local functions and `Func<>`-lambdas
     differ in capture-allocation and recursion ergonomics).
   - Dart record `(int, int)` → C# `ValueTuple<long, long>` (the
     `(long, long)` syntax sugar, NOT legacy `System.Tuple<long,
     long>`) under research finding
     `rf-dart-record-type-to-csharp-valuetuple`.
   - Width: `int` → `long` per pinned project-wide width idiom
     (rf-dart-int-to-csharp-long-width).
7. **`dart.expression.var_local_variable_with_initializer`** —
   `var nextAddr = 100;` (and `200`, `300`) → `long nextAddr = 100L;`
   (explicit `long`, NOT C# `var`) because C# `var` would infer `int`
   and force a narrowing-conversion ambiguity at the
   `Func<(long, long)>` consumer site. Literal suffix `L`
   preferred (implicit `int`→`long` widening also compiles). Three
   occurrences: `100L`, `200L`, `300L`.
8. **`dart.expression.final_local_variable_with_initializer`** —
   `final <name> = <expr>;` → `var <name> = <expr>;` where the
   initializer's static type is inferable:
   - `final table = GlobalWritersTable('q')` → `var table = new
     GlobalWritersTable("q");` (C# mandatory `new`; `'q'` → `"q"`
     because C# single quotes are `char`).
   - `final globalNames = [GlobalName.writer('p', 5)]` → `var
     globalNames = new List<GlobalName> { GlobalName.Writer("p", 5L) };`
     (list literal idiom + factory-name PascalCase + width suffix).
   - `final result = localize(...)` → `var result = Localize(...);`
     (top-level Dart function → static method on SUT static-helpers
     class).
   - `final entry = table.findByRemote('p', 3)` → `var entry =
     table.FindByRemote("p", 3);` (return is nullable; see null-aware
     constructs below).
9. **`dart.expression.list_literal_of_objects`** — Dart `[a, b, ...]`
   of reference-type elements → C# `new List<T> { a, b, ... }`
   (collection-initializer over `List<T>`). Explicit `<GlobalName>`
   type argument MANDATORY (C# cannot infer the generic argument from
   the collection-initializer alone). SUT param `List<GlobalName>` →
   C# `List<GlobalName>` (NOT `IList<>`/`IEnumerable<>`/
   `IReadOnlyList<>`; SUT spec pins `List<T>` for Dart-`List`-typed
   public surfaces).
10. **`dart.expression.static_factory_method_call`** —
    `GlobalName.writer('p', 5)` → `GlobalName.Writer("p", 5L)` and
    `GlobalName.reader('p', 3)` → `GlobalName.Reader("p", 3L)`. The
    SUT-side shape (named constructor preserved as internal ctor +
    public static methods, OR C# `record` with primary constructor +
    static factory) is pinned by `mad_helpers.dart.md`. THIS spec
    records the CALL-SITE only. Structural equality on `GlobalName` is
    a precondition on the SUT spec (required for the implicit-equals
    matcher routing below).
11. **`dart.expression.named_argument_invocation`** — `localize(
    globalNames: globalNames, localAgent: 'q', table: table,
    freshAddrAllocator: allocateAddr)` → `Localize(globalNames:
    globalNames, localAgent: "q", table: table, freshAddrAllocator:
    allocateAddr);`:
    - Method PascalCased (`localize` → `Localize`).
    - Argument names UNCHANGED (Dart lowerCamelCase matches C#
      parameter-name convention; no rename).
    - Call-site order preserved verbatim; named-argument call sites
      are order-independent on both sides — codegen MUST NOT reorder.
    - `allocateAddr` argument auto-converts to `Func<(long, long)>` via
      C# method-group conversion (implicit, no ceremony at call site).
    - Dart `required` named-only modifier has NO C# parameter-level
      equivalent (C# 11's `required` is for object initializers only);
      enforcement is purely call-site discipline.
12. **`dart.expression.list_index_access`** —
    `result.freshPairs[0]`, `result.useReader[0]`, `result.useReader[1]`,
    `result.spawns[0]` → `result.FreshPairs[0]`, `result.UseReader[0]`,
    `result.UseReader[1]`, `result.Spawns[0]` (1-to-1 indexer syntax;
    both Dart and C# `List<T>` are 0-indexed, both throw on
    out-of-bounds — `RangeError` vs `ArgumentOutOfRangeException`).
13. **`dart.expression.member_property_access`** — member-access
    PascalCasing + the LOAD-BEARING `.length`→`.Count` rename for
    `List<T>` receivers:
    - `result.freshPairs.length` → `result.FreshPairs.Count`.
    - `result.spawns.length` → `result.Spawns.Count`.
    - `result.freshPairs[0].writerAddr` →
      `result.FreshPairs[0].WriterAddr`.
    - `table.localizeEntryCount` → `table.LocalizeEntryCount` (Dart
      camelCase getter → C# PascalCase property; no rename).
    - `entry.remoteAgent` → `entry.RemoteAgent`.
    - `entry.remoteIndex` → `entry.RemoteIndex`.
    Codegen MUST inspect receiver static type: `List<T>` → `.Count`;
    `string`/`T[]` → `.Length`.
14. **`dart.package_test.expect_equals_implicit_matcher`** — Dart
    `expect(actual, value)` (bare value second arg auto-wrapped in
    `equals(...)`) → xUnit `Assert.Equal(expected, actual)` with
    ARGUMENT-ORDER SWAPPED (xUnit expected-first, Dart actual-first).
    ≈18 sites in this file. Examples:
    - `expect(result.freshPairs.length, 1)` → `Assert.Equal(1,
      result.FreshPairs.Count);`.
    - `expect(result.freshPairs[0].writerAddr, 100)` →
      `Assert.Equal(100L, result.FreshPairs[0].WriterAddr);`.
    - `expect(result.spawns[0].globalName, GlobalName.writer('p', 5))`
      → `Assert.Equal(GlobalName.Writer("p", 5L),
      result.Spawns[0].GlobalName);` (relies on SUT-side structural
      equality precondition on `GlobalName`).
    - `expect(entry.remoteAgent, 'p')` → `Assert.Equal("p",
      entry.RemoteAgent);`.
    - `expect(table.localizeEntryCount, 0)` → `Assert.Equal(0,
      table.LocalizeEntryCount);`.
    Boolean-literal sites (`expect(result.useReader[0], false)`,
    `expect(result.useReader[1], true)`, etc.) SHOULD route to
    `Assert.False(...)` / `Assert.True(...)` (readability preference;
    semantically equivalent to `Assert.Equal(false, ...)`).
15. **`dart.package_test.expect_isEmpty_matcher`** —
    `expect(result.spawns, isEmpty)` (1 site, line 100) →
    `Assert.Empty(result.Spawns);`. NEW idiom row vs sibling test-file
    specs (first appearance in this batch).
16. **`dart.expression.null_assertion_bang_operator`** — Dart `entry!`
    (runtime null-assertion, throws `TypeError`) → C# `entry!`
    (compile-time NRT annotation, no runtime check). Used 2× (lines 90,
    142). Semantic-gap closure: every `!` IS preceded by
    `expect(entry, isNotNull)` on the immediately previous statement,
    which becomes `Assert.NotNull(entry)` — xUnit throws on null, so
    the program never reaches `!` with a null operand. CONVERSION
    INVARIANT: codegen MUST audit each `!` translation; if the
    preceding statement is NOT an `Assert.NotNull` of the same
    expression, codegen MUST insert one (or use the
    `entry ?? throw new InvalidOperationException()` runtime-throw
    form).
17. **`dart.package_test.expect_isNotNull_matcher`** — `expect(entry,
    isNotNull)` (2 sites, lines 89, 141) → `Assert.NotNull(entry);`.
    The xUnit ≥2.5 `[NotNull]` post-condition annotation narrows
    `entry`'s static type to non-nullable under `#nullable enable`;
    pairs naturally with the `entry!` operator usage above.

Conversion-units summary (per convspec):

- **cu-1**: file-scope `using` directives — `using Xunit;`, `using
  System;`, and ONE `using <RootNs>.Multiagent;` (two Dart SUT
  imports deduped).
- **cu-2**: namespace declaration mirroring the Dart `test/multiagent`
  path (e.g. `<RootNs>.Test.Multiagent`).
- **cu-3**: top-level `public class LocalizeTests` (from `group`
  label) with optional `[Trait("Group", "Localize")]`.
- **cu-4**: three `[Fact(DisplayName = "<label>")]` `public void`
  methods (one per Dart `test()` call), all executable (NO `Skip=`),
  each with a `/// <summary>` XML-doc block carrying the Given/When/
  Then comments + Spec Section 5.2/5.3 references.
- **cu-5**: per-method body — arrange-act-assert translation:
  - `var table = new GlobalWritersTable("q");`.
  - `long nextAddr = <N>L;`.
  - LOCAL FUNCTION `(long, long) allocateAddr() { ... }` (NOT a
    `Func<>`-lambda).
  - `var globalNames = new List<GlobalName> { GlobalName.Writer("p",
    <i>L) };` (and reader / mixed variants).
  - `var result = Localize(globalNames: ..., localAgent: "q", table:
    ..., freshAddrAllocator: allocateAddr);`.
  - `expect(...)` → `Assert.*` per matcher-routing idiom (implicit-
    equals → `Assert.Equal` (arg-order swapped), `isNotNull` →
    `Assert.NotNull`, `isEmpty` → `Assert.Empty`, boolean literals →
    `Assert.True` / `Assert.False`).
  - `!` operator preserved 1-to-1 with the runtime-vs-compile-time
    documentation invariant.
- **cu-6**: explicit literal-width suffix `L` on integer literals
  flowing into `long`-typed consumers (writerAddr / readerAddr / index
  args) — preferred for readability; implicit widening also compiles.

## 3. Decomposed Task Units

- T1: emit cu-1 file-scope `using` directives (Xunit + System +
  deduped SUT `using <RootNs>.Multiagent;`). Done.
- T2: emit cu-2 namespace declaration mirroring `test/multiagent`. Done.
- T3: emit cu-3 `public class LocalizeTests` with optional
  `[Trait("Group", "Localize")]`. Done.
- T4: emit cu-4 three `[Fact(DisplayName=...)]` `public void` methods
  with `/// <summary>` XML-doc blocks carrying Given/When/Then + Spec
  Section 5.2/5.3 references; method names per construct #5 mangling.
  Done.
- T5: emit cu-5 method body for test 1 (`_w`): local declarations
  (`var table`, `long nextAddr = 100L`, local function `allocateAddr`,
  `var globalNames` with single `GlobalName.Writer("p", 5L)`, `var
  result = Localize(...)`); five `Assert.*` calls per construct #14/#15
  (Count==1, WriterAddr==100L, ReaderAddr==101L,
  `Assert.False(UseReader[0])`, Spawns.Count==1,
  `Spawns[0].ReaderAddr==100L`,
  `Spawns[0].GlobalName==GlobalName.Writer("p", 5L)`,
  `Spawns[0].DestAgent=="p"`, `LocalizeEntryCount==0`). Done.
- T6: emit cu-5 method body for test 2 (`_r`): same shape with `200L`
  base, `GlobalName.Reader("p", 3L)`, including `Assert.NotNull(entry)`
  before the `entry!` accesses (`WriterAddr==200L`,
  `RemoteAgent=="p"`, `RemoteIndex==3`), `Assert.True(UseReader[0])`,
  `Assert.Empty(result.Spawns)`. Done.
- T7: emit cu-5 method body for test 3 (mixed): `300L` base, list of
  two `GlobalName` factory calls, `Assert.False(UseReader[0])` +
  `Assert.True(UseReader[1])`, `Spawns.Count==1`, `Spawns[0]` checks,
  `LocalizeEntryCount==1`, `Assert.NotNull(entry)` + `entry!.WriterAddr
  == 302L`. Done.
- T8: emit cu-6 `L`-suffixed integer literals at every `long`-typed
  sink (writerAddr / readerAddr / index args / nextAddr literals).
  Done.
- T9: confirm `.length` → `.Count` rename applied to every `List<T>`
  receiver (FreshPairs, Spawns); confirm NO `.Length` mis-emission.
  Done.
- T10: confirm Dart `expect(actual, expected)` → xUnit
  `Assert.Equal(expected, actual)` argument-order swap applied
  uniformly across all ≈18 call sites. Done.
- T11: confirm every `entry!` is preceded by `Assert.NotNull(entry)`
  (conversion invariant); no synthetic `Assert.NotNull` insertion
  needed in this file (both occurrences already paired in source).
  Done.

## 4. Research Findings

none required — the convspec ratifies every construct via official-
Dart-doc + Microsoft-Learn citations and via verbatim reuse from
sibling test-file specs (smoke_test.dart.md,
global_writers_table_test.dart.md, mad_error_handling_test.dart.md,
boot_loader_test.dart.md). New idiom rows recorded by THIS file
(record→ValueTuple, `var`→explicit-long, list-literal→`List<T>`,
named-constructor/static-factory→static-method, named-args→named-args,
list-indexing→`list[i]`, `.length`→`.Count`, `isEmpty`→`Assert.Empty`)
each cite both Dart-side and .NET-side authoritative documentation per
the convspec's "Rationale + research provenance" section. No
WebSearch/WebFetch/Agent calls required.

## 5. Consistency Pass

fixed — derived from
`.codeconv/conversion-specs/test/multiagent/localize_test.dart.md`
(RATIFIED convspec, schema_version 1, source_sha256 matches the file's
current sha256 `18f65f67b39c84df4e9b09b357301bb42bfb69b6b2660592ef2a251ee8976ec7`).
Every construct row in §2 mirrors a `constructs:` entry in the convspec;
every conversion-unit (cu-1 .. cu-6) is preserved; every cross-file
dependency (SUT `using` resolution + `<ProjectReference>` + structural-
equality precondition on `GlobalName`) is recorded as a dependency on
the SUT specs (`global_writers_table.dart.md` + `mad_helpers.dart.md`)
rather than re-pinned here. No idiom-vs-research conflict; no idiom-
vs-idiom conflict; no construct undecidable from the convspec alone.
Cross-cutting width idiom (`rf-dart-int-to-csharp-long-width`) carried
through to all integer-literal sites (`100L`, `101L`, `200L`, `201L`,
`300L`, `301L`, `302L`, `1L`, `2L`, `3L`, `5L`) AND to the
`(int, int)` record return type (→ `(long, long)`) AND to the
`Func<(long, long)>` consumer type of `freshAddrAllocator`. The
implicit-equals→`Assert.Equal` argument-order swap (xUnit expected-
first vs Dart actual-first) is the only easy-to-invert footgun and is
called out explicitly in cu-5 + T10. The Dart-`!`-vs-C#-`!` runtime-vs-
compile-time semantic gap is closed by the `Assert.NotNull` precondition
audited in T11.

## 6. Escalations

None.
