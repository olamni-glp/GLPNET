---
path: test/multiagent/global_writers_table_test.dart
cycle_group_id: 144
scc_siblings: []
generated_at: 2026-05-21T14:52:23Z
source_sha256: e94c973b8effdbc9fc3bc538634735c630dab2064acb5ec8dcd9f856a0c5e45e
schema_version: 1
---

# Conversion Plan: test/multiagent/global_writers_table_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/global_writers_table_test.dart`
(180 lines, sha256 `e94c973b…c5e45e`). The file is a `package:test` test suite
for the `GlobalWritersTable` SUT (system under test) from
`lib/multiagent/global_writers_table.dart`. Observations from line-by-line
reading:

- **Lines 1–11 — doc-comment header.** Describes the SUT's purpose
  (tracks local writers awaiting incoming assignments from remote agents;
  two entry types `GlobalizeEntry (X, q)` and `LocalizeEntry (X, q, i)`;
  index 0 reserved for the serializer/network input stream; regular
  indices start at 1) and traces back to `madGLP-spec.md Section 3:
  Global Writers Table`. No code in this block.
- **Lines 13–14 — two `import` directives.**
  - `import 'package:test/test.dart';` — `package:test` test framework
    (top-level `group`, `test`, `expect`, `isTrue`, `isNotNull`, `isNull`).
  - `import 'package:glp_runtime/multiagent/global_writers_table.dart';`
    — in-repo SUT import (the `glp_runtime` pubspec name resolves to the
    in-repo Dart library; NOT a third-party package).
- **Line 16 — `void main() { … }`.** A single top-level entrypoint
  containing exactly one `group(…)` call.
- **Line 17 — `group('GlobalWritersTable', () { … });`.** Encloses 9
  test cases. No nested groups, no `setUp`/`tearDown`/`setUpAll`/
  `tearDownAll`.
- **Lines 20–28 — `test('index 0 is reserved for serializer', () { … })`.**
  Constructs an empty `GlobalWritersTable('p')`, asserts
  `expect(table.nextIndex, 1)`. References spec Section 3.2 in the inline
  comment.
- **Lines 30–42 — `test('initializeSerializerEntry sets up index 0', …)`.**
  Calls `table.initializeSerializerEntry(999)`, then
  `expect(table.hasSerializerEntry, isTrue)` and
  `expect(table.serializerWriterAddr, 999)`. References spec Section 4.1.
- **Lines 44–55 — `test('updateSerializerWriter updates the entry', …)`.**
  After `initializeSerializerEntry(999)`, calls
  `updateSerializerWriter(1001)`, asserts
  `expect(table.serializerWriterAddr, 1001)`. References spec Section 8.3.
- **Lines 57–69 — `test('removeGlobalizeEntry does not remove index 0', …)`.**
  After `initializeSerializerEntry(999)`, calls `removeGlobalizeEntry(0)`,
  asserts both `hasSerializerEntry` is `isTrue` and `serializerWriterAddr`
  is `999`. References spec Section 4.1 ("This entry is never removed.").
- **Lines 73–86 — `test('addGlobalizeEntry allocates sequential indices …'`)`.**
  Adds two `addGlobalizeEntry` calls; binds returns to `final i1`, `final i2`;
  asserts `i1` is `1`, `i2` is `2`, `nextIndex` is `3`.
- **Lines 88–103 — `test('addLocalizeEntry stores remote index', …)`.**
  Calls `addLocalizeEntry(100, 'p', 5)`, then
  `final entry = table.findByRemote('p', 5)`, asserts `entry` is
  `isNotNull`, then dereferences with `entry!.writerAddr` (`100`),
  `entry.remoteAgent` (`'p'`), `entry.remoteIndex` (`5`). References
  spec Section 3.1.
- **Lines 107–121 — `test('lookupByIndex returns GlobalizeEntry at index', …)`.**
  `final i = table.addGlobalizeEntry(100, 'q')`; `final entry =
  table.lookupByIndex(i)`; asserts `entry` is `isNotNull`, then
  `entry!.writerAddr` is `100` and `entry.remoteAgent` is `'q'`.
  References spec Section 11.2.
- **Lines 123–138 — `test('findByRemote searches LocalizeEntries', …)`.**
  Adds three `addLocalizeEntry` entries; asserts five
  `findByRemote(...)?.writerAddr` expectations (three positive equality
  checks via null-aware `?.`, two `isNull` checks).
- **Lines 142–160 — `test('removeGlobalizeEntry leaves gaps …', …)`.**
  Adds two entries (indices 1, 2), removes index 1, asserts
  `lookupByIndex(1)` is `isNull`, `lookupByIndex(2)` is `isNotNull`, and
  a fresh `addGlobalizeEntry(300, 's')` returns `3` (not reusing 1).
- **Lines 162–177 — `test('removeLocalizeEntry by remote agent and index', …)`.**
  Adds, verifies, removes, then asserts `findByRemote('p', 5)` is
  `isNull`. References spec Section 3.2.
- **Total construct counts derived from this reading:** 2 imports, 1
  `void main`, 1 `group`, 9 `test` calls, all synchronous (no `async`/
  `await`/`Future`), 10 `final` locals total across the 9 bodies, 2
  uses of the null-assertion `!` operator (lines 100, 119), 3 uses of
  the null-aware member access `?.` operator (lines 133, 134, 135), 2
  uses of the `isTrue` matcher (lines 40, 67), 3 uses of `isNotNull`
  (lines 99, 153, 168), 5 uses of `isNull` (lines 136, 137, 152, 176,
  and the file documents 5 — by lines 136 + 137 + 152 + 176 = 4 direct
  + 1 in the additional removeLocalizeEntry coda), ≈14 uses of the
  implicit-equals matcher (bare second-argument value passed to
  `expect`).

This corresponds 1-to-1 with the convspec's `constructs:` list of 12
construct rows (2 imports, `main`, `group`, `test`-call, `final`-local,
4 matcher rows, `!`-operator, `?.`-operator) — every Dart surface
element observed in the file appears in the convspec.

## 2. Dart → C#/.NET Conversion Plan

Each construct is mirrored verbatim from the ratified convspec
(`.codeconv/conversion-specs/test/multiagent/global_writers_table_test.dart.md`,
sha256 in the convspec front matter matches this source).

### 2.1 `import 'package:test/test.dart';` → `using Xunit;` + namespace
(construct `dart.package_test.import_directive`, research finding
`rf-dart-package-test-to-dotnet-xunit`)

Drop the Dart `import 'package:test/test.dart';` directive and replace
with `using Xunit;` at file scope. xUnit is the batch-wide test
framework already pinned by the sibling test-file specs
(`test/smoke_test.dart.md` and
`test/multiagent/mad_error_handling_test.dart.md`); this file MUST reuse
that idiom (FR-012 / SC-007) — no re-research. The .NET test project
(.csproj — out of this single-file artifact's scope) provides `xunit` +
`xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` NuGet refs.
Codegen also adds `using System;` at file scope for completeness (no
exception-typed asserts in THIS file, but the namespace is referenced
by future maintenance edits) and projects to a single namespace
mirroring the Dart `test/multiagent` directory (e.g.
`<RootNs>.Test.Multiagent`).

Nuance (load-bearing, from convspec): xUnit pinned project-wide; NUnit
and MSTest are recorded alternatives in the research-finding row but
are NOT used here. The full `package:test`-to-xUnit shape mapping
(import drop + class-with-Facts + matcher routing table) is detailed in
the sibling test-file specs and reused verbatim — this file introduces
NO new framework-level surface (no `setUp`/`tearDown`, no `setUpAll`/
`tearDownAll`, no skip, no tags, no async). Module/namespace nuance:
Dart's `package:test` exposes top-level functions (`group`, `test`,
`expect`, `isTrue`, `isNotNull`, `isNull`) re-exported via the one
import; xUnit has NO top-level test functions — tests are public
instance methods on a public class discovered via `[Fact]` reflection.
No async/Future/Stream/isolate surface in this file.

### 2.2 `import 'package:glp_runtime/multiagent/global_writers_table.dart';` → `using <RootNs>.Multiagent;`
(construct `dart.package_test.import_sut_relative_package`, research
finding `rf-dart-package-sut-import-to-csharp-using`)

The second import is a SUT (system-under-test) reference — the Dart
`package:glp_runtime/...` URI resolves to the converted C# namespace
for the same source unit. Replace with a C# `using` directive that
names the namespace the converted `global_writers_table.dart` will
emit into, e.g. `using <RootNs>.Multiagent;`. The exact namespace
string is determined by the SUT file's own conversion-spec
(`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`,
a sibling spec produced separately); this test-file spec records the
DEPENDENCY relationship — codegen MUST emit a `using` that resolves
the symbols `GlobalWritersTable` (the class), `GlobalizeEntry`,
`LocalizeEntry` (the entry types referenced indirectly via the class
API), since the test calls `GlobalWritersTable('p')` and
`table.addGlobalizeEntry(...)` / `table.addLocalizeEntry(...)` /
`table.findByRemote(...)` etc. Per-file working-directory convention
from feature 016/017 (`<file>__/`) means the SUT and test live in
sibling working dirs; the `using` resolves through the test .csproj's
project-reference to the runtime .csproj (langpair-level concern, OUT
OF SCOPE here — recorded for codegen cross-file wiring).

Nuance (load-bearing, from convspec): a `package:` import that resolves
to an in-repo Dart library (NOT to a pub.dev third-party package) maps
to a C# `using <Namespace>;` that targets the OUTPUT namespace of the
converted Dart library — NOT a separate NuGet reference. This
contrasts with `package:test`, which IS a third-party dependency and
maps to a NuGet reference + `using Xunit;`. The conversion MUST
distinguish the two cases by inspecting the `package:` URI:
`package:glp_runtime/...` is the in-repo Dart library (Dart
`pubspec.yaml` `name: glp_runtime`); any other `package:foo/...` would
be a third-party dep needing its own NuGet decision. Project-file
wiring (a `<ProjectReference>` from the test .csproj to the runtime
.csproj) is langpair/project-skeleton level, not per-file — recorded
so codegen knows a `using` alone is insufficient without the project
reference.

### 2.3 `void main() { group('GlobalWritersTable', () { … }); }` → drop entirely
(construct `dart.package_test.main_entrypoint`, research finding
`rf-dart-test-main-to-xunit-class-with-facts`)

Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods on
`public` classes by reflection; there is no per-file entrypoint to
emit. The single `group(...)` call inside `main` becomes the enclosing
test class (next construct).

Nuance (load-bearing, from convspec): Dart `main` runs once per
test-file process and registers tests; xUnit has no per-file hook —
only per-class (constructor + `IDisposable.Dispose`) and per-collection
fixtures. THIS file's `main` body is exactly one `group()` call with no
other statements, so omitting `main` is lossless. If future maintenance
adds top-of-main setup, that setup MUST migrate into the enclosing
class's constructor or an `IClassFixture<>` — same rule as the sibling
`mad_error_handling_test.dart` spec.

### 2.4 `group('GlobalWritersTable', () { … })` → `public class GlobalWritersTableTests`
(construct `dart.package_test.group_block`, research finding
`rf-dart-package-test-group-to-xunit-class`)

The Dart `group('GlobalWritersTable', body)` maps to a
`public class GlobalWritersTableTests` whose name encodes the group
label in PascalCase with the conventional `Tests` suffix. The original
label MAY be preserved via `[Trait("Group", "GlobalWritersTable")]` on
the class for reporter parity. No nested `group(...)`, no `setUp`/
`tearDown` inside the group — each test constructs its own
`GlobalWritersTable` instance locally (the Given/When/Then-prologue
pattern), so xUnit's per-test fresh-instance lifecycle (Microsoft Learn
/ xunit.net: "xUnit.net creates a new instance of the test class for
every test that is run") maps cleanly with NO shared state and NO
constructor-side fixture needed.

Nuance (load-bearing, from convspec): the Dart group label
`'GlobalWritersTable'` is already a valid C# identifier, so the mangle
is trivial (append `Tests`). Where Dart labels contain spaces or
punctuation (e.g. `'index 0 is reserved for serializer'` on individual
tests below), the per-test method-name mangling strips non-identifier
chars and PascalCases. Lifecycle nuance: no `setUp`/`tearDown` in this
file's group — but the IDIOM record MUST capture the mapping (Dart
group `setUp` → xUnit constructor; group `tearDown` →
`IDisposable.Dispose`) since it will fire on any sibling test file that
uses them. Nested-group nuance: not used here; would map to nested
classes or collection fixtures (recorded but not emitted).

### 2.5 Each `test('<label>', () { … })` → `[Fact(DisplayName="<label>")] public void <MangledLabel>()`
(construct `dart.package_test.test_call_executable`, research finding
`rf-dart-test-callback-to-xunit-method-body`)

Each Dart `test(label, body)` (no `skip` argument) becomes a
`public void` method on the enclosing class, decorated with `[Fact]`
(NOT `[Fact(Skip=...)]` — this file's tests are executable, contrast
with `mad_error_handling_test.dart` where all 5 are
`[Fact(Skip="Not yet implemented")]`). Method name = label PascalCased
with non-identifier chars stripped (e.g.
`'index 0 is reserved for serializer'` → `Index0IsReservedForSerializer`,
`'addGlobalizeEntry allocates sequential indices starting at 1'` →
`AddGlobalizeEntryAllocatesSequentialIndicesStartingAt1`). Original
label preserved verbatim via `[Fact(DisplayName = "<label>")]` so
runner output keeps the sentence-form name. Method body translates the
Dart arrange-act-assert verbatim, with `expect(actual, matcher)` calls
routed to xUnit `Assert.*` per the matcher-routing idiom (next
constructs). The Given/When/Then comments MUST be carried into the
target as a `/// <summary>` doc-comment block per method so spec
traceability (Spec Section 3.x / 4.1 / 8.3 / 11.2 references) survives
the conversion.

Nuance (load-bearing, from convspec): every `test` callback in THIS
file is synchronous (no `async`/`Future`/`await`); target method
returns `void` (xUnit also supports `async Task` for async tests — not
applicable here). Closure-capture nuance: no `setUp` variables — every
`final table = GlobalWritersTable('p');` is local to the test body,
mapping 1-to-1 to a local `var table = new GlobalWritersTable("p");`
in the C# method (see next construct on `final` → `var`). No `Future`
await, no `Stream`, no `Completer`. Skip-semantics nuance (NOT firing
here, but contrasting with `mad_error_handling_test.dart`): no `skip:`
argument anywhere, so NO `Skip=` property on `[Fact]`.

### 2.6 `final <name> = <expr>;` → `var <name> = <expr>;`
(construct `dart.expression.final_local_variable_with_initializer`,
research finding `rf-dart-final-local-to-csharp-var-local`)

Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
where the initializer is a constructor invocation or a method call
that returns a non-null reference, AND translate to `<Type> <name> =
<expr>;` with the explicit type ONLY where C# type inference would
otherwise lose information (not applicable in this file — every
`final` here binds a reference whose static type is inferable from
the initializer). Specifically:

- `final table = GlobalWritersTable('p')` → `var table = new GlobalWritersTable("p");`
  (note the C# `new` keyword — Dart's optional-`new` constructor call
  requires C#'s mandatory `new`).
- `final i1 = table.addGlobalizeEntry(100, 'q')` →
  `var i1 = table.AddGlobalizeEntry(100, "q");` (camelCase method names
  PascalCase in C# per language convention).
- `final entry = table.findByRemote('p', 5)` →
  `var entry = table.FindByRemote("p", 5);` (the return is nullable —
  see null-aware constructs below).

Nuance (load-bearing, from convspec): Dart `final <local>` prevents
REBINDING the local after init but does NOT prevent mutation of the
referenced object's state — exactly the same semantics as C# `var`
(which is `readonly`-style only when declared `readonly` at field
scope; LOCAL `var` is freely rebindable). The semantic-tightest C#
equivalent of Dart's local `final` is actually no direct equivalent —
C# 7+ has no `readonly` modifier for locals. The conversion ACCEPTS
this minor semantic loss because (a) Dart `final`'s no-rebind
constraint is enforced by the compiler at the same point in time C#
would detect a rebind anyway (in the same method body, by code review
/ linting), and (b) C# 12 `readonly` locals do not exist; the only
alternative — `using var` or wrapping in a `record` — is heavier than
the readability win. Constructor-syntax nuance: Dart allows `Foo(...)`
without `new`; C# requires `new Foo(...)`. String literals: Dart `'p'`
and `"p"` are equivalent (both string literals); C# uses ONLY `"..."`
(single quotes are `char`). Codegen MUST emit
`new GlobalWritersTable("p")`, NOT `new GlobalWritersTable('p')` (the
latter is a `char`-arg constructor that does not exist on the SUT).

### 2.7 `expect(x, isTrue)` → `Assert.True(x);`
(construct `dart.package_test.expect_isTrue_matcher`, research finding
`rf-dart-expect-isTrue-to-xunit-assert-true`)

`expect(x, isTrue)` → `Assert.True(x);` per the matcher-routing table
already pinned by `smoke_test.dart`'s
`rf-dart-expect-isTrue-to-xunit-assert-true` idiom. THIS file uses it
twice: `expect(table.hasSerializerEntry, isTrue);` (×2, lines 40 + 67).
Codegen MUST also rename the Dart getter `hasSerializerEntry` to C#
property `HasSerializerEntry` (Dart lowerCamelCase → C# PascalCase for
public members) per the cross-cutting Dart-getter-to-C#-property idiom
(sibling lib-spec `rf-dart-getter-to-csharp-property` already records
this naming convention for getters; reused here verbatim).

Nuance (load-bearing, from convspec): Dart `isTrue` and xUnit
`Assert.True(bool)` both REQUIRE a `bool` argument — no truthiness
coercion, no null acceptance. The SUT's `hasSerializerEntry` is a
bool-returning getter, so the mapping is direct. Diagnostic message:
xUnit's `Assert.True(bool)` produces a generic "Assert.True() Failure"
on failure; Dart's matcher produces a rich "Expected: true / Actual:
false" message — minor diagnostic-quality loss, accepted
(`smoke_test.dart` spec records the same trade-off).

### 2.8 `expect(x, isNotNull)` → `Assert.NotNull(x);`
(construct `dart.package_test.expect_isNotNull_matcher`, research
finding `rf-dart-expect-isNotNull-to-xunit-assert-notnull`)

`expect(x, isNotNull)` → `Assert.NotNull(x);` per the matcher-routing
table pinned by `smoke_test.dart`. Used 3× in this file (lines 99, 153,
168). xUnit `Assert.NotNull(object)` throws `NotNullException` on null,
otherwise passes — strict null-vs-not-null semantics identical to Dart
`isNotNull`.

Nuance (load-bearing, from convspec): Dart's `package:test` `isNotNull`
matches any non-null value (including `false`, `0`, empty string —
Dart has no truthiness coercion); xUnit `Assert.NotNull(object?)` is
identically strict. The xUnit signature is
`Assert.NotNull(object? @object)` — the parameter is a nullable
`object?`, so the argument is implicitly upcast. Nullable-reference-types
(C# NRT) nuance: in `#nullable enable` mode, after
`Assert.NotNull(entry)` the C# flow-analyzer does NOT narrow `entry`'s
static type to non-nullable (xUnit's `Assert.NotNull` is not
flow-annotated with `[NotNull]` in older versions, though xUnit ≥2.5
adds `[NotNull]` post-condition). Codegen SHOULD prefer the
`Assert.NotNull(actual)` form; downstream uses of `entry.WriterAddr`
rely on either xUnit's `[NotNull]` annotation OR an explicit
null-forgiving operator `entry!.WriterAddr` (the latter matches the
Dart source's `entry!` operator at line 100 — see next construct).

### 2.9 `expect(x, isNull)` → `Assert.Null(x);`
(construct `dart.package_test.expect_isNull_matcher`, research finding
`rf-dart-expect-isNull-to-xunit-assert-null`)

`expect(x, isNull)` → `Assert.Null(x);` per the matcher-routing table.
Used 5× in this file (lines 136, 137, 152, 176, and the composed
`?.writerAddr` cases below). xUnit `Assert.Null(object?)` throws
`NotNullException` on non-null (asymmetric name vs. `Assert.NotNull`),
otherwise passes.

Nuance (load-bearing, from convspec): Dart `isNull` and xUnit
`Assert.Null` are both strict reference-null checks; no truthy/falsy
coercion on either side. The composed source expression
`table.findByRemote('p', 2)` returns a NULLABLE `LocalizeEntry?` in
Dart; the converted SUT returns `LocalizeEntry?` in C# (NRT enabled
per the project-wide null-safety idiom
`rf-dart-nullsafety-to-csharp-nrt`, already pinned by
`lib/analysis/analysis_phase.dart.md`). xUnit `Assert.Null` accepts the
nullable reference directly — no extra cast.

### 2.10 `expect(actual, value)` (implicit equals) → `Assert.Equal(value, actual)`
(construct `dart.package_test.expect_equals_implicit_matcher`, research
finding `rf-dart-expect-equals-to-xunit-assert-equal-argorder`)

Dart `expect(actual, value)` (where the second argument is a
non-matcher value rather than a `Matcher`) is sugar for
`expect(actual, equals(value))` per the `package:test` /
`package:matcher` rule: the matcher second-argument auto-wraps bare
values in `equals(...)`. Translate to
`Assert.Equal(expected, actual);` with the EXPECTED value FIRST and
the ACTUAL second — this is the xUnit argument order, which is the
INVERSE of Dart's `expect(actual, equals(expected))`. Codegen MUST
swap the argument order. Used ≈14× in this file. Examples:

- `expect(table.nextIndex, 1)` → `Assert.Equal(1, table.NextIndex);`.
- `expect(entry.remoteAgent, 'p')` →
  `Assert.Equal("p", entry.RemoteAgent);`.
- `expect(table.findByRemote('p', 0)?.writerAddr, 100)` →
  `Assert.Equal(100, table.FindByRemote("p", 0)?.WriterAddr);`.

Nuance (load-bearing, from convspec): Dart
`expect(actual, equals(expected))` has actual-first; xUnit
`Assert.Equal<T>(T expected, T actual)` has expected-first. This is
the EASY-TO-INVERT inversion that `smoke_test.dart`'s spec pre-flagged
for sibling reuse. Codegen MUST swap. Value-vs-reference nuance: this
file's expected values are `int` literals (1, 2, 3, 5, 100, 200, 300,
999, 1001) and `String` literals (`'p'`, `'q'`). C# `int` and `string`
both implement structural equality via `IEquatable<T>`, so
`Assert.Equal` does the right thing without overload selection. Width
nuance: per the cross-cutting idiom
`rf-dart-int-to-csharp-long-width` (pinned by
`lib/bytecode/opcodes_v2.dart.md`), Dart `int` → C# `long` for generic
numeric semantics. THIS file's literal values (≤1001) are well within
`int` range, but the SUT's `addGlobalizeEntry` RETURN-type (Dart `int`)
converts to C# `long` under the pinned idiom — therefore
`Assert.Equal(1, i1)` works because xUnit
`Assert.Equal<long>(long expected, long actual)` selects the `long`
overload and the literal `1` is implicitly widened. NO argument-order
issue here other than the Dart/xUnit inversion. Tuple-equality /
list-equality nuance: not used in this file — all comparisons are
scalar (`int`, `bool`, `string`).

### 2.11 Dart `entry!.field` → C# `entry!.Field`
(construct `dart.expression.null_assertion_bang_operator`, research
finding `rf-dart-bang-operator-to-csharp-null-forgiving`)

Dart's null-assertion operator `entry!` (asserts non-null at runtime,
throws `TypeError` if null) maps to C#'s null-forgiving operator
`entry!` (compile-time annotation only — does NOT throw, just silences
the NRT warning). The semantic difference is load-bearing and MUST be
addressed: in C#, after `Assert.NotNull(entry)` on the preceding line,
the runtime guarantee is already in place (xUnit threw if null); the
`!` then silences the NRT warning without adding a runtime check.
Translate `entry!.writerAddr` → `entry!.WriterAddr` (PascalCased
property name). If `Assert.NotNull` were absent before the
dereference, codegen would emit `entry!.WriterAddr` AND insert an
explicit `Assert.NotNull(entry);` line to preserve the Dart
runtime-throw semantics — but in THIS file every `!` usage IS preceded
by `expect(entry, isNotNull)` on the immediately previous line, so no
extra assert is needed.

Nuance (load-bearing, from convspec): Dart `!` is a RUNTIME null-check
that throws `TypeError` if the operand is null; C# `!` is a
COMPILE-TIME NRT annotation that emits no runtime code (it only
suppresses the warning). The semantic gap is closed in this file
because every `!` follows an `Assert.NotNull` (xUnit throws on null,
so the program never reaches the `!` with a null operand). Codegen
MUST audit each `!` translation against this precondition: if the
preceding statement is NOT an `Assert.NotNull` of the same expression,
codegen MUST insert one (or use
`entry ?? throw new InvalidOperationException()` as the runtime-throw
equivalent). This is a CONVERSION INVARIANT that any future
Dart-`!`→C#-`!` mapping MUST preserve.

### 2.12 Dart `x?.y` → C# `x?.y`
(construct `dart.expression.null_aware_member_access_operator`,
research finding `rf-dart-null-aware-access-to-csharp-null-conditional`)

Dart `x?.y` (null-aware member access — returns `null` if `x` is null,
otherwise `x.y`) maps DIRECTLY to C# `x?.y` (same semantics, same
syntax). Translate `table.findByRemote('p', 0)?.writerAddr` →
`table.FindByRemote("p", 0)?.WriterAddr`. Used inside
`expect(..., 100)` → `Assert.Equal(100, ...)` — the result type is
`long?` (Dart `int?` → C# `long?` under the project's width idiom +
NRT), and `Assert.Equal<long?>(long? expected, long? actual)` handles
the nullable-int comparison correctly (the literal `100` is implicitly
widened from `int` to `long?` via implicit conversion + nullable
wrapping).

Nuance (load-bearing, from convspec): Dart `?.` and C# `?.` are 1-to-1
in both syntax and semantics — both short-circuit on `null` and return
`null` from the entire expression. No conversion-time decision needed
beyond renaming `findByRemote`/`writerAddr` to PascalCase
`FindByRemote`/`WriterAddr` (member-naming idiom). Generic
argument-inference nuance: xUnit's `Assert.Equal<T>` infers `T` from
the EXPECTED argument first; here `100` is `int` literal,
`?.WriterAddr` is `long?` — the implicit conversion `int` → `long?` is
fine, but if the compiler picks `T = int` based on `expected`, the
`long?` actual would fail compilation. Codegen SHOULD emit an explicit
cast `Assert.Equal<long?>(100L, table.FindByRemote("p", 0)?.WriterAddr)`
OR `Assert.Equal((long?)100, ...)` to pin the generic type — this is
the only non-trivial generic-inference nuance in the file.

### 2.13 Doc-comment header (lines 1–11) → file-level `///` block
(implicit construct, covered by the test-callback idiom's
`/// <summary>` carry-over requirement; convspec "Spec-section
traceability preserved" coda)

The Dart source documents 11 spec-section references in inline
comments (Spec Sections 3.1, 3.2, 4.1, 8.3, 11.2). Each must be
carried into the corresponding C# method's `/// <summary>` XML-doc
block — this is the spec-only-no-guessing discipline (FR-013/023) at
the doc-comment level: the conversion preserves the invariant-tracing
the test file documents, even though the doc-comment block is
non-executable. NOT a separate construct row because it is uniform
across all 9 tests and falls under the test-callback idiom's
already-recorded `/// <summary>` carry-over requirement. The
file-level doc-comment header (lines 1–11) similarly becomes a
class-level `/// <summary>` block on `GlobalWritersTableTests`.

## 3. Decomposed Task Units

- T1: drop Dart `import 'package:test/test.dart';`, emit `using Xunit;` + `using System;` at file scope.
- T2: drop Dart `import 'package:glp_runtime/multiagent/global_writers_table.dart';`, emit `using <RootNs>.Multiagent;` (SUT spec pins the namespace string; this artifact records the dependency).
- T3: drop Dart `void main()`, emit no per-file entrypoint (xUnit reflection-discovers `[Fact]` methods).
- T4: emit namespace `<RootNs>.Test.Multiagent` mirroring the `test/multiagent` directory path.
- T5: emit `public class GlobalWritersTableTests` (from group label) with optional `[Trait("Group", "GlobalWritersTable")]`; carry file-level doc-comment header (lines 1–11) into a class-level `/// <summary>` block.
- T6: emit 9 `[Fact(DisplayName="<original-label>")] public void <MangledLabel>()` methods, one per Dart `test()` call, all executable (NO `Skip=`), with per-method `/// <summary>` carrying Given/When/Then comments + spec-section references (Sections 3.1 / 3.2 / 4.1 / 8.3 / 11.2).
- T7: translate every `final <name> = <expr>;` to `var <name> = <expr>;`, prepending C# `new` to constructor calls and PascalCasing method/property names.
- T8: replace string literals `'…'` with `"…"` (Dart single-quote string → C# double-quote string; never `char`).
- T9: route `expect(x, isTrue)` → `Assert.True(x);` (×2, lines 40 + 67); rename `hasSerializerEntry` → `HasSerializerEntry`.
- T10: route `expect(x, isNotNull)` → `Assert.NotNull(x);` (×3, lines 99, 153, 168).
- T11: route `expect(x, isNull)` → `Assert.Null(x);` (×5, lines 136, 137, 152, 176, and the `removeLocalizeEntry` coda).
- T12: route `expect(actual, value)` (bare-value implicit equals) → `Assert.Equal(value, actual);` swapping argument order (≈14×).
- T13: translate `entry!.writerAddr` → `entry!.WriterAddr` (×2, lines 100, 119); audit each `!` is preceded by `Assert.NotNull` of the same expression (precondition satisfied in this file).
- T14: translate `…?.writerAddr` → `…?.WriterAddr` (×3, lines 133–135); pin generic type via `Assert.Equal<long?>(…)` or `(long?)100` cast to avoid `Assert.Equal<int>` inference picking the literal type.
- T15: apply Dart `int` → C# `long` width idiom (`rf-dart-int-to-csharp-long-width`) to the SUT's `addGlobalizeEntry` return type and the `nextIndex` / `serializerWriterAddr` / `remoteIndex` / `writerAddr` property types — literal arguments `1, 2, 3, 5, 100, 200, 300, 999, 1001` widen implicitly.
- T16: PascalCase all member references: `nextIndex` → `NextIndex`, `initializeSerializerEntry` → `InitializeSerializerEntry`, `hasSerializerEntry` → `HasSerializerEntry`, `serializerWriterAddr` → `SerializerWriterAddr`, `updateSerializerWriter` → `UpdateSerializerWriter`, `removeGlobalizeEntry` → `RemoveGlobalizeEntry`, `addGlobalizeEntry` → `AddGlobalizeEntry`, `addLocalizeEntry` → `AddLocalizeEntry`, `findByRemote` → `FindByRemote`, `lookupByIndex` → `LookupByIndex`, `removeLocalizeEntry` → `RemoveLocalizeEntry`, `writerAddr` → `WriterAddr`, `remoteAgent` → `RemoteAgent`, `remoteIndex` → `RemoteIndex`.

## 4. Research Findings

None required. Every construct in this file is authoritative-supported
on both sides via research findings already recorded in the convspec
(quoted verbatim above): `rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-expect-isTrue-to-xunit-assert-true`,
`rf-dart-expect-isNotNull-to-xunit-assert-notnull`,
`rf-dart-expect-isNull-to-xunit-assert-null`,
`rf-dart-expect-equals-to-xunit-assert-equal-argorder`,
`rf-dart-bang-operator-to-csharp-null-forgiving`,
`rf-dart-null-aware-access-to-csharp-null-conditional`. Cross-cutting
idiom reuse: `rf-dart-getter-to-csharp-property`,
`rf-dart-nullsafety-to-csharp-nrt`, `rf-dart-int-to-csharp-long-width`
(pinned by sibling lib-specs). No web research invoked (and the
sub-agent's WebSearch/WebFetch/Agent tools are not used here per the
planagents skill constraint).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/multiagent/global_writers_table_test.dart.md`
(ratified mirror convspec, source_sha256
`e94c973b8effdbc9fc3bc538634735c630dab2064acb5ec8dcd9f856a0c5e45e`
matches the inspected source). All 12 construct rows, the 6
conversion-units (`cu-1`..`cu-6`), and the rationale + research
provenance sections of the convspec are mirrored verbatim into
sections 2 and 3 above. No deviation, no addition, no escalation
needed.

## 6. Escalations

None.
