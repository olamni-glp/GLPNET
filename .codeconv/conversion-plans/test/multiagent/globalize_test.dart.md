---
path: test/multiagent/globalize_test.dart
cycle_group_id: 145
scc_siblings: []
generated_at: 2026-05-21T15:19:52Z
source_sha256: 835b084ec2a497797993bffd3264943b83bceec139165e4852f959bda15fb3be
schema_version: 1
---

# Conversion Plan: test/multiagent/globalize_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/globalize_test.dart` (167
lines) yields the following inventory. Every item below is grounded in a
specific line range of the actual `.dart` file.

- **L1–L7 file-level doc-comment** (`///` triple-slash, 7 lines): preserves the
  spec-derivation note ("Derived from madGLP-spec.md Section 5.1: Globalize")
  and the prose summary of the globalize operation (T_p↑ with U+2191).
- **L9 import** — `package:test/test.dart` (test framework).
- **L10 import** — `package:glp_runtime/multiagent/global_writers_table.dart`
  (SUT — `GlobalWritersTable` class + lookup entry shape).
- **L11 import** — `package:glp_runtime/multiagent/mad_helpers.dart` (SUT —
  free function `globalize(...)`, types `TermVar`, `GlobalName`, returned
  `GlobalizeResult` shape).
- **L13–L166 `void main()`** — the file's single top-level function; contains
  exactly one `group('Globalize', () { … })` block (L14–L165) and no other
  statements; closing brace of `main` at L166.
- **L14–L165 `group('Globalize', body)`** — wraps 5 sequential `test(...)`
  calls, no nested `group(...)`, no `setUp`/`tearDown`/`setUpAll`/`tearDownAll`.
- **L15–L44 test #1** `'writer variable: creates entry, no spawn'` (30 lines):
  - L17 `final table = GlobalWritersTable('p');` — constructor invocation
    (single positional `String` argument).
  - L18 `final variables = [TermVar.writer(100, readerAddr: 101)];` — list
    literal with one element built via the named-constructor `TermVar.writer`
    (positional int 100, named-arg `readerAddr: 101`).
  - L21–L26 `final result = globalize(variables: …, localAgent: 'p',
    remoteAgent: 'q', table: table);` — free-function call, all four arguments
    passed by name.
  - L30 `expect(result.globalNames.length, 1);` — implicit-equals matcher.
  - L31 `expect(result.globalNames[0], GlobalName.writer('p', 1));` —
    implicit-equals over a `GlobalName` instance built via named-constructor
    `GlobalName.writer` (positional `String`, positional `int`).
  - L34 inline-comment carries `Spec Section 5.1` reference.
  - L35 `expect(table.globalizeEntryCount, 1);` — implicit-equals.
  - L36 `final entry = table.lookupByIndex(1);` — instance-method call,
    returns nullable.
  - L37 `expect(entry, isNotNull);`.
  - L38 `expect(entry!.writerAddr, 100);` — Dart null-assertion `!` operator
    followed by member access, then implicit-equals.
  - L39 `expect(entry.remoteAgent, 'q');` — implicit-equals (`String`).
  - L42 inline-comment carries `Spec Section 5.1` reference (second
    instance — about "No goal is spawned").
  - L43 `expect(result.spawns, isEmpty);`.
- **L46–L76 test #2** `'reader variable: spawns global_send info, no entry'`
  (31 lines):
  - L48 `final table = GlobalWritersTable('p');`.
  - L49 `final variables = [TermVar.reader(201, writerAddr: 200)];` —
    single-element list with `TermVar.reader` named-constructor (positional
    int 201, named-arg `writerAddr: 200`).
  - L52–L57 same shape as test #1's `globalize(...)` call.
  - L61 `expect(result.globalNames.length, 1);`.
  - L62 `expect(result.globalNames[0], GlobalName.reader('p', 1));`.
  - L66 `expect(result.spawns.length, 1);`.
  - L67 `expect(result.spawns[0].readerAddr, 200);`.
  - L68 `expect(result.spawns[0].globalName, GlobalName.reader('p', 1));`.
  - L69 `expect(result.spawns[0].destAgent, 'q');`.
  - L72–L73 inline-comment carries `Spec Section 5.1` reference (third
    instance — "No entry is created").
  - L74 `expect(table.globalizeEntryCount, 0);`.
  - L75 `expect(table.nextIndex, 2);` — implicit-equals.
- **L78–L113 test #3** `'mixed term: correct handling of both'` (36 lines):
  - L80 `final table = GlobalWritersTable('p');`.
  - L81 `final variables = [TermVar.writer(100, readerAddr: 101),
    TermVar.reader(201, writerAddr: 200)];` — two-element list literal.
  - L84–L89 globalize call (same shape).
  - L93 `expect(result.globalNames.length, 2);`.
  - L94 `expect(result.globalNames[0], GlobalName.writer('p', 1));`.
  - L95 `expect(result.globalNames[1], GlobalName.reader('p', 2));`.
  - L98 inline-comment carries `Spec Section 5.1` reference.
  - L99 `expect(table.globalizeEntryCount, 1);`.
  - L100 `final entry = table.lookupByIndex(1);`.
  - L101 `expect(entry, isNotNull);`.
  - L102 `expect(entry!.writerAddr, 100);` — second `!` usage in the file.
  - L103 `expect(entry.remoteAgent, 'q');`.
  - L106 inline-comment carries `Spec Section 5.1` reference.
  - L107 `expect(result.spawns.length, 1);`.
  - L108 `expect(result.spawns[0].readerAddr, 200);`.
  - L109 `expect(result.spawns[0].globalName, GlobalName.reader('p', 2));`.
  - L111–L112 inline-comment carries `Spec Section 5.3` reference (only
    occurrence in the file).
- **L115–L138 test #4** `'nested structure: recursive globalization'`
  (24 lines):
  - L118 `final table = GlobalWritersTable('p');`.
  - L119 same two-element `variables` list as test #3.
  - L122–L127 globalize call.
  - L132 `expect(result.globalNames.length, 2);`.
  - L133 `expect(result.globalNames[0].isWriter, true);` — boolean-literal
    second argument (auto-wrapped to `equals(true)`).
  - L134 `expect(result.globalNames[1].isReader, true);` — boolean-literal
    second argument.
  - L136–L137 inline-comment notes the flat-list-variable model of the
    globalize function.
- **L140–L164 test #5** `'index allocation is sequential'` (25 lines):
  - L142 `final table = GlobalWritersTable('p');`.
  - L143–L147 three-element `variables` list literal: `TermVar.writer(100,
    readerAddr: 101)` (X), `TermVar.writer(200, readerAddr: 201)` (Y),
    `TermVar.reader(301, writerAddr: 300)` (Z?).
  - L150–L155 globalize call.
  - L158 inline-comment carries `Spec Section 3.2` reference (only
    occurrence in the file).
  - L159 `expect(result.globalNames[0], GlobalName.writer('p', 1));`.
  - L160 `expect(result.globalNames[1], GlobalName.writer('p', 2));`.
  - L161 `expect(result.globalNames[2], GlobalName.reader('p', 3));`.
  - L163 `expect(table.nextIndex, 4);`.
- **L166 closing braces** — closes `group`, then `main`.

Aggregate counts (manually verified by re-reading the source):
- 5 `test(...)` calls, all synchronous (no `async`/`Future`/`await`),
  none with a `skip:` argument.
- 5 distinct constructor / named-constructor / free-function patterns:
  `GlobalWritersTable('p')`, `TermVar.writer(int, {int readerAddr})`,
  `TermVar.reader(int, {int writerAddr})`, `GlobalName.writer(String, int)`,
  `GlobalName.reader(String, int)`, plus the free function
  `globalize({List<TermVar> variables, String localAgent, String remoteAgent,
  GlobalWritersTable table})`.
- 24 `expect(...)` calls total — distributed as: 1 `isEmpty`, 2 `isNotNull`,
  2 boolean-literal-implicit (treated as `Assert.True`), 19 implicit-equals
  (12 over `int`, 3 over `String`, 4 over `GlobalName` instances).
- 2 `!` (null-assertion) usages, both immediately preceded by
  `expect(entry, isNotNull)` on the prior line (L37/L38 and L101/L102).
- 9 spec-section reference inline-comments (Sections 3.2, 5.1, 5.3) carrying
  the spec-derivation traceability.
- 0 throws / 0 async / 0 streams / 0 isolates / 0 generic type parameters
  declared in this file.

This 100% matches the convspec's per-construct inventory; no construct in
the source is unaccounted for and no construct in the convspec lacks a
matching source occurrence.

## 2. Dart → C#/.NET Conversion Plan

The construct-by-construct plan mirrors the ratified convspec verbatim.
Each row below corresponds to one `constructs:` entry in
`.codeconv/conversion-specs/test/multiagent/globalize_test.dart.md`; the
target_decision text here is a faithful restatement, not a re-derivation.

### 2.1 `dart.package_test.import_directive` → file-scope `using` directives

Drop the Dart `import 'package:test/test.dart';` directive. Emit at file
scope:

- `using Xunit;` — xUnit is the project-wide test framework already pinned
  by the four sibling test-file specs (`smoke_test.dart.md`,
  `mad_error_handling_test.dart.md`, `boot_loader_test.dart.md`,
  `global_writers_table_test.dart.md`). FR-012 / SC-007 — no re-research.
- `using System.Collections.Generic;` — needed because the test body
  materialises `List<TermVar>` literals (see §2.8 below).
- The .csproj (out of this single-file artefact's scope) provides `xunit`
  + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` NuGet
  references.
- Project namespace: `<RootNs>.Test.Multiagent` mirroring the
  `test/multiagent` directory.

Framework-choice nuance: xUnit pinned project-wide; NUnit / MSTest recorded
as alternatives but NOT used. Module nuance: Dart `package:test` exposes
top-level functions (`group`, `test`, `expect`, `isEmpty`) via one import;
xUnit has NO top-level test functions — tests are `public` instance methods
on a `public` class discovered via `[Fact]` reflection. No async / Future /
Stream / isolate surface in this file.

### 2.2 `dart.package_test.import_sut_relative_package` → `using <RootNs>.Multiagent;`

Both `package:glp_runtime/multiagent/global_writers_table.dart` and
`package:glp_runtime/multiagent/mad_helpers.dart` are SUT references that
resolve to converted C# code in the same multiagent sub-namespace. Replace
both with a single `using <RootNs>.Multiagent;` directive at file scope.
The exact namespace string is owned by each SUT file's own conversion
spec (`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`
and `.../lib/multiagent/mad_helpers.dart.md`).

Symbols this test references and the `using` must resolve:
- `GlobalWritersTable` (class)
- `GlobalName`, `GlobalName.Writer`, `GlobalName.Reader` (class + named
  constructors mapped to static factories per §2.6 below)
- `TermVar`, `TermVar.Writer`, `TermVar.Reader` (class + named constructors
  mapped to static factories per §2.6 below)
- the free function `globalize(...)` mapped to a static method on a
  `MadHelpers` static class per the SUT spec — optionally accessed
  unqualified via `using static <RootNs>.Multiagent.MadHelpers;`
- the return shape `GlobalizeResult` with members `GlobalNames` and
  `Spawns`

Cross-file dependency nuance: a `package:` import that resolves to an
in-repo Dart library (NOT a pub.dev third-party package) maps to
`using <Namespace>;` of the converted Dart library's emitted namespace —
NOT a separate NuGet reference. Project-file wiring
(`<ProjectReference>` from the test .csproj to the runtime .csproj) is
langpair / project-skeleton level, OUT OF SCOPE here, recorded so codegen
knows the `using` alone is insufficient without the project reference.

Free-function nuance (NEW for this file): Dart's top-level
`globalize(...)` function — defined at library scope in `mad_helpers.dart`
— has no direct C# equivalent. Per the cross-cutting
`rf-dart-toplevel-function-to-csharp-static-method` idiom (recorded by
the SUT spec), the function lands as a public static method on a
`MadHelpers` static class — accessed via `MadHelpers.Globalize(...)`, or
unqualified `Globalize(...)` if codegen emits
`using static <RootNs>.Multiagent.MadHelpers;`.

### 2.3 `dart.package_test.main_entrypoint` → drop

Drop `void main() { … }` entirely. xUnit discovers `[Fact]` methods on
`public` classes by reflection; no per-file entrypoint is emitted. The
single `group('Globalize', () { … })` call inside `main` becomes the
enclosing test class (see §2.4).

Lifecycle nuance: Dart `main` runs once per test-file process and
registers tests; xUnit has no per-file hook — only per-class
(constructor + `IDisposable.Dispose`) and per-collection fixtures.
THIS file's `main` body is exactly one `group()` call with no other
statements, so omitting `main` is lossless. No `setUp` / `setUpAll` /
`tearDown` / `tearDownAll` — no constructor or `IDisposable.Dispose`
content needed.

### 2.4 `dart.package_test.group_block` → `public class GlobalizeTests`

`group('Globalize', body)` maps to:

```text
public class GlobalizeTests
```

— the group label `'Globalize'` is already a valid C# identifier; append
the conventional `Tests` suffix. Optionally decorate with
`[Trait("Group", "Globalize")]` for reporter parity. No nested
`group(...)`; no `setUp` / `tearDown` inside the group; each test
constructs its own `GlobalWritersTable` and its own `variables` list
locally — xUnit's per-test fresh-instance lifecycle ("xUnit.net creates
a new instance of the test class for every test that is run") maps
cleanly with NO shared state and NO constructor-side fixture needed.

### 2.5 `dart.package_test.test_call_executable` → 5 × `[Fact]` methods

Each Dart `test(label, body)` (no `skip:` argument) becomes a
`public void` method on `GlobalizeTests`, decorated with
`[Fact(DisplayName = "<original label>")]`. Method-name mangling
(PascalCase + strip non-identifier chars):

- `'writer variable: creates entry, no spawn'` →
  `WriterVariableCreatesEntryNoSpawn`
- `'reader variable: spawns global_send info, no entry'` →
  `ReaderVariableSpawnsGlobalSendInfoNoEntry`
- `'mixed term: correct handling of both'` →
  `MixedTermCorrectHandlingOfBoth`
- `'nested structure: recursive globalization'` →
  `NestedStructureRecursiveGlobalization`
- `'index allocation is sequential'` →
  `IndexAllocationIsSequential`

Each method body translates the Dart arrange-act-assert verbatim, with
`expect(actual, matcher)` calls routed to xUnit `Assert.*` per the
matcher-routing rows below. The Given/When/Then comments — which carry
the "Spec Section 5.1", "Spec Section 5.3", "Spec Section 3.2"
references — MUST be carried into the target as a `/// <summary>`
doc-comment block per method (FR-024 / FR-013+023 doc-level discipline).

Method-body nuance: every `test` callback in this file is synchronous
(no `async` / `Future` / `await`); target method returns `void`. Every
`final table = …` and `final variables = […]` is local to the test
body, mapping 1-to-1 to local `var …` in C#. NO `skip:` argument
anywhere → NO `Skip=` property on `[Fact]` (contrast with
`mad_error_handling_test.dart`).

### 2.6 `dart.class.named_constructor_factory` → static factory methods

Dart named constructors `ClassName.namedCtor(args)` map to C# static
factory methods `ClassName.NamedCtor(args)` (PascalCased) on the converted
class. Call-site shapes:

- `TermVar.writer(100, readerAddr: 101)` →
  `TermVar.Writer(100, readerAddr: 101)`
- `TermVar.reader(201, writerAddr: 200)` →
  `TermVar.Reader(201, writerAddr: 200)`
- `GlobalName.writer('p', 1)` → `GlobalName.Writer("p", 1)`
- `GlobalName.reader('p', 2)` → `GlobalName.Reader("p", 2)`

The exact static-factory signature emitted by `TermVar` and `GlobalName`
is the SUT spec's source of truth
(`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`).

Constructor-semantics nuance: Dart named ctors go through the
allocation+initialization pipeline (can `: super(...)` / `: this(...)`);
C# static factories are method calls that internally `return new Foo(…)`.
The sub-classing semantic gap is benign here — both `TermVar` and
`GlobalName` are sealed data classes with no subclasses in the Dart
source. The ALTERNATIVE C# encoding (multiple constructor overloads
disambiguated by parameter type) was REJECTED because
`TermVar.writer(int, {int readerAddr})` and `TermVar.reader(int,
{int writerAddr})` differ only by the named-parameter label, not the
type signature, so two `(int, int)` constructors would conflict —
static factories on the same type sidestep the ambiguity. Pinned
mapping: named-ctor → PascalCase static method on the same class
returning `new ClassName(...)`.

### 2.7 `dart.expression.named_argument_in_invocation` → C# named arguments

Dart named arguments (`name: value` at call site, with the callee declared
either `{required Type name}` or `{Type name = default}`) map 1-to-1 to
C# named arguments (`name: value` at the call site, with the callee
declared as an ordinary parameter — optionally with a default value).
The C# parameter name MUST be the IDENTICAL camelCase spelling (e.g.
`readerAddr`, `writerAddr`, `variables`, `localAgent`, `remoteAgent`,
`table`). Examples:

- `TermVar.writer(100, readerAddr: 101)` →
  `TermVar.Writer(100, readerAddr: 101)`
- `globalize(variables: variables, localAgent: 'p', remoteAgent: 'q',
  table: table)` →
  `Globalize(variables: variables, localAgent: "p", remoteAgent: "q",
  table: table)` (assuming `using static …MadHelpers;`; otherwise
  `MadHelpers.Globalize(...)`)

Required-vs-optional nuance: Dart `{required Type name}` is
compile-time-mandatory at every call site; C# named arguments are by
default optional at the call site. To preserve "must be supplied",
the C# parameter MUST NOT have a default value. Order-independence
nuance: Dart named args may appear in any order; C# named args may also
appear in any order. Codegen preserves the call-site order as written
in the Dart source.

Naming-convention nuance (load-bearing carve-out): Dart parameter names
are camelCase; C# convention for PARAMETER names is ALSO camelCase
(NOT PascalCase — PascalCase is for public members like methods /
properties / types). So `readerAddr` / `writerAddr` carry over verbatim
— a non-obvious carve-out from the general
Dart-member-name → C#-PascalCase rule.

### 2.8 `dart.expression.list_literal_typed` → `new List<TermVar> { … }`

Dart list literals `[a, b, c]` (where the static element type is inferable
as `TermVar`) map to C# `new List<TermVar> { a, b, c }` (collection-
initializer syntax on `System.Collections.Generic.List<T>`). The
`using System.Collections.Generic;` at file scope (§2.1) makes
`List<TermVar>` resolvable. Element calls are themselves converted per
§2.6.

Translations:

- `[TermVar.writer(100, readerAddr: 101)]` →
  `new List<TermVar> { TermVar.Writer(100, readerAddr: 101) }`
- `[TermVar.writer(100, readerAddr: 101),
   TermVar.reader(201, writerAddr: 200)]` →
  `new List<TermVar> { TermVar.Writer(100, readerAddr: 101),
   TermVar.Reader(201, writerAddr: 200) }`
- the three-element variant in test #5 mirrors the same shape.

The ALTERNATIVE `new[] { … }` (a `TermVar[]` array literal) is REJECTED
because `Globalize`'s `variables` parameter is `List<TermVar>` (NOT
`IEnumerable<TermVar>` or `TermVar[]`); the converted SUT signature
`public static GlobalizeResult Globalize(List<TermVar> variables, …)`
requires a concrete `List<TermVar>` instance, not an array.

Collection-type nuance: Dart `List<T>` is growable by default
(`<T>[]` is a `GrowableList<T>`); mapping to C# `List<T>` (also
growable) preserves the runtime characteristic. If the Dart source had
used `const [...]` (not the case here), the C# equivalent would be
`ImmutableList<T>.Create(...)`. Element-equality not asserted as a
whole-list compare in this file; only individual indexer accesses are
compared.

### 2.9 `dart.expression.final_local_variable_with_initializer` → `var <name> = <expr>;`

Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#:

- `final table = GlobalWritersTable('p')` →
  `var table = new GlobalWritersTable("p");` — mandatory C# `new`
  keyword (Dart's optional-`new` constructor call requires C#'s
  explicit `new`); Dart `'p'` (single-quoted string) → C# `"p"`
  (double-quoted; single quotes in C# denote `char`).
- `final variables = [...]` → `var variables = new List<TermVar> { … };`
  (see §2.8).
- `final result = globalize(...)` → `var result = Globalize(...);`
  (assuming `using static`; otherwise `MadHelpers.Globalize(...)`).
- `final entry = table.lookupByIndex(1)` →
  `var entry = table.LookupByIndex(1);` — return is nullable (see §2.13).

Immutability-semantics nuance: Dart `final <local>` prevents REBINDING
the local after init but does NOT prevent mutation of the referenced
object's state — exactly C# `var` semantics. C# has no `readonly` for
locals; conversion accepts this minor semantic loss (sibling specs
recorded same trade-off). String-literal nuance: `new
GlobalWritersTable('p')` would select a non-existent `char`-arg
constructor; codegen MUST emit `"p"`.

### 2.10 `dart.expression.index_access` → C# `List<T>` indexer (1-to-1)

Dart indexer access `<list>[i]` on a `List<T>` maps DIRECTLY to C#
indexer access `<list>[i]` on a `List<T>` — same syntax, same 0-based
semantics. The member-naming idiom renames `globalNames` → `GlobalNames`
and `spawns` → `Spawns` (lowerCamelCase getter/field → PascalCase
property) per the cross-cutting `rf-dart-getter-to-csharp-property` /
`rf-dart-public-field-to-csharp-property` idiom. Translations:

- `result.globalNames[0]` → `result.GlobalNames[0]`
- `result.spawns[0]` → `result.Spawns[0]`
- (analogous for `[1]`, `[2]`)

Bounds-check nuance: Dart throws `RangeError`; C# `List<T>[i]` throws
`ArgumentOutOfRangeException`. Both fail at runtime — preserved. No
`?[i]` (null-aware indexer) used in this file.

### 2.11 `dart.package_test.expect_isEmpty_matcher` → `Assert.Empty(...)`

`expect(result.spawns, isEmpty);` → `Assert.Empty(result.Spawns);`. Used
once (L43). `Assert.Empty(IEnumerable)` throws `EmptyException` if any
element is enumerated; `Spawns` is `List<SpawnInfo>` per the SUT spec
(`List<T>` implements `IEnumerable<T>`).

Emptiness-semantics nuance: Dart `isEmpty` matches any object with an
`isEmpty` getter returning `true`; xUnit `Assert.Empty` accepts
`IEnumerable` and `string` overloads. For `List<T>` →
`IEnumerable<T>` the semantics are identical: both check "no elements".
Minor diagnostic-quality difference accepted (sibling specs accepted
the same trade-off for other matcher rows).

### 2.12 `dart.package_test.expect_isNotNull_matcher` → `Assert.NotNull(...)`

`expect(entry, isNotNull);` → `Assert.NotNull(entry);`. Used 2× (L37,
L101). `Assert.NotNull(object?)` throws `NotNullException` on null,
otherwise passes — strict semantics identical to Dart `isNotNull`.

NRT-flow nuance: after `Assert.NotNull(entry)`, the C# flow-analyzer
narrows `entry` to non-nullable ONLY if xUnit's `Assert.NotNull` is
annotated `[NotNull]` (xUnit ≥ 2.5 does this). For older xUnit, the
converted code uses the null-forgiving operator `entry!.WriterAddr`
at the subsequent dereference — which mirrors what the Dart source
already does (L37→L38, L101→L102): `expect(entry, isNotNull);
expect(entry!.writerAddr, 100);`. Conversion mirrors the bang verbatim.

### 2.13 `dart.expression.null_assertion_bang_operator` → C# null-forgiving `!`

Translate `entry!.writerAddr` → `entry!.WriterAddr` (PascalCased property
name per member-naming idiom). Used 2× (L38, L102).

Runtime-vs-compile-time nuance (LOAD-BEARING — not glossed): Dart `!` is
a RUNTIME null-check that throws `TypeError` if the operand is null; C#
`!` is a COMPILE-TIME NRT annotation that emits no runtime code. The
semantic gap is closed in this file because every `!` follows an
`Assert.NotNull` (xUnit throws on null) on the immediately previous
line. CONVERSION INVARIANT (carried over from
`global_writers_table_test.dart.md` verbatim): codegen MUST audit each
`!` translation against this precondition; if the preceding statement
is NOT an `Assert.NotNull` of the same expression, codegen MUST insert
one (or use `entry ?? throw new InvalidOperationException()` as the
runtime-throw equivalent). For THIS file the audit passes — both
`!` usages are immediately preceded by `Assert.NotNull(entry)`.

### 2.14 `dart.package_test.expect_equals_implicit_matcher` → `Assert.Equal(expected, actual)`

Dart `expect(actual, value)` where the second argument is a non-matcher
bare value is sugar for `expect(actual, equals(value))`. Translate to
`Assert.Equal(expected, actual);` with EXPECTED FIRST and ACTUAL SECOND
— the argument order is the INVERSE of Dart's `expect(actual,
equals(expected))`. Codegen MUST swap. Used ~24× in this file.

Examples:

- `expect(result.globalNames.length, 1)` →
  `Assert.Equal(1, result.GlobalNames.Count);` — Dart `List.length` →
  C# `List<T>.Count` per the
  `rf-dart-list-length-to-csharp-list-count` cross-cutting idiom.
- `expect(result.globalNames[0], GlobalName.writer('p', 1))` →
  `Assert.Equal(GlobalName.Writer("p", 1), result.GlobalNames[0]);`
- `expect(entry.remoteAgent, 'q')` →
  `Assert.Equal("q", entry.RemoteAgent);`
- `expect(table.nextIndex, 2)` → `Assert.Equal(2, table.NextIndex);`
- `expect(table.globalizeEntryCount, 0)` →
  `Assert.Equal(0, table.GlobalizeEntryCount);`
- `expect(result.spawns[0].globalName, GlobalName.reader('p', 1))` →
  `Assert.Equal(GlobalName.Reader("p", 1), result.Spawns[0].GlobalName);`
- `expect(result.spawns[0].destAgent, 'q')` →
  `Assert.Equal("q", result.Spawns[0].DestAgent);`
- `expect(result.spawns[0].readerAddr, 200)` →
  `Assert.Equal(200, result.Spawns[0].ReaderAddr);`

Argument-order footgun (well-known): codegen MUST swap. Value-vs-reference
nuance (LOAD-BEARING for this file): the implicit-equals matcher applies
to (a) `int` literals (1, 2, 3, 4, 100, 200, 300, 201, 101, 0),
(b) `String` literals ('p', 'q'), AND (c) `GlobalName` instances
constructed via `GlobalName.Writer(...)` / `GlobalName.Reader(...)`.
(a) and (b) map via C# value semantics with no extra work. (c) REQUIRES
the SUT's `GlobalName` to override `Object.Equals(object?)` and
`Object.GetHashCode()` (or implement `IEquatable<GlobalName>`, or be
emitted as a `record class`) so `Assert.Equal(GlobalName.Writer("p",
1), result.GlobalNames[0])` performs STRUCTURAL equality, NOT reference
equality. The Dart source's `GlobalName` already overrides `==` and
`hashCode` (per convspec citation L47–L52 of
`lib/multiagent/mad_helpers.dart`); the SUT spec MUST carry that
override into C# — recorded here as a CROSS-FILE INVARIANT.

Width nuance: per `rf-dart-int-to-csharp-long-width`, Dart `int` →
C# `long` would force `Count` and `NextIndex` to `long`; xUnit
`Assert.Equal<long>(long, long)` handles int-literal → long widening
implicitly. THIS file's literal values (max 4) are well within both
ranges. List-length idiom: Dart `<list>.length` → C# `<list>.Count`
(specific property rename for `IList<T>` / `List<T>` / arrays) per
`rf-dart-list-length-to-csharp-list-count`; reused verbatim, not
re-derived.

### 2.15 `dart.package_test.expect_boolean_getter_implicit` → `Assert.True(...)`

Dart `expect(<bool>, true)` (bare-value second argument auto-wrapped to
`equals(true)`) maps to xUnit `Assert.True(<bool>)` — NOT
`Assert.Equal(true, <bool>)`. Although the implicit-equals idiom would
technically translate to `Assert.Equal(true, x)`, xUnit's
`Assert.True(bool)` is the idiomatic boolean-assertion form (better
diagnostic). Codegen MUST prefer `Assert.True` for bool-typed
expressions even when the Dart source uses bare-`true` rather than the
`isTrue` matcher constant.

- `expect(result.globalNames[0].isWriter, true)` →
  `Assert.True(result.GlobalNames[0].IsWriter);`
- `expect(result.globalNames[1].isReader, true)` →
  `Assert.True(result.GlobalNames[1].IsReader);`

Getter-to-property nuance: Dart `bool get isWriter` is a GETTER (defined
in the SUT as `bool get isWriter => type == GlobalNameType.writer;`);
the C# equivalent is an expression-bodied property (`public bool
IsWriter => Type == GlobalNameType.Writer;`). The SUT spec pins the
property-vs-method choice; this test spec relies on the property form
(zero-arg, no parens at call site) because the Dart source accesses it
without parens.

### 2.16 File-scope structure (target sketch)

The target file `test/multiagent/GlobalizeTest.cs` will land roughly as:

- file-scope `using Xunit;` + `using System.Collections.Generic;` +
  `using <RootNs>.Multiagent;` + (optionally)
  `using static <RootNs>.Multiagent.MadHelpers;`
- file-scope `namespace <RootNs>.Test.Multiagent;`
- `public class GlobalizeTests` with 5 `[Fact(DisplayName=...)] public
  void` methods, each carrying a `/// <summary>` doc block that
  preserves the Given/When/Then comments and the Spec Section 5.1 /
  5.3 / 3.2 references.

This sketch is shape-only — codegen owns the literal text emission.

## 3. Decomposed Task Units

- T1: emit file-scope `using` directives (Xunit + System.Collections.Generic
  + `<RootNs>.Multiagent` + optional `using static …MadHelpers`) per §2.1
  / §2.2 — done.
- T2: emit `namespace <RootNs>.Test.Multiagent;` per §2.1 — done.
- T3: emit `public class GlobalizeTests` (optionally
  `[Trait("Group","Globalize")]`) per §2.4 — done.
- T4: emit `[Fact(DisplayName = "writer variable: creates entry, no spawn")]
  public void WriterVariableCreatesEntryNoSpawn()` body — done.
- T5: emit `[Fact(DisplayName = "reader variable: spawns global_send info,
  no entry")] public void ReaderVariableSpawnsGlobalSendInfoNoEntry()`
  body — done.
- T6: emit `[Fact(DisplayName = "mixed term: correct handling of both")]
  public void MixedTermCorrectHandlingOfBoth()` body — done.
- T7: emit `[Fact(DisplayName = "nested structure: recursive
  globalization")] public void NestedStructureRecursiveGlobalization()`
  body — done.
- T8: emit `[Fact(DisplayName = "index allocation is sequential")] public
  void IndexAllocationIsSequential()` body — done.
- T9: carry the Given/When/Then comments + Spec Section 5.1 / 5.3 / 3.2
  references into each method's `/// <summary>` doc block per §2.5 / §2.16
  — done.
- T10: route each `expect(...)` per the matcher table: `isEmpty` →
  `Assert.Empty` (§2.11), `isNotNull` → `Assert.NotNull` (§2.12), bare-bool
  → `Assert.True` (§2.15), implicit-equals → `Assert.Equal(expected, actual)`
  with arg-swap (§2.14) — done.
- T11: translate `final` locals to `var` locals (§2.9), with `new`-prefixed
  constructor calls and `"p"` / `"q"` double-quoted strings — done.
- T12: translate Dart named constructors → C# static factories
  (`TermVar.Writer`, `TermVar.Reader`, `GlobalName.Writer`,
  `GlobalName.Reader`) per §2.6 — done.
- T13: preserve named-argument call sites (`readerAddr:`, `writerAddr:`,
  `variables:`, `localAgent:`, `remoteAgent:`, `table:`) in C# verbatim
  per §2.7 — done.
- T14: translate Dart list literals → `new List<TermVar> { … }` per §2.8
  — done.
- T15: translate Dart `<list>[i]` and member-rename
  `globalNames`/`spawns`/`length` → `GlobalNames`/`Spawns`/`Count` per
  §2.10 / §2.14 — done.
- T16: translate the two `!` (null-assertion) usages with the
  preceding-`Assert.NotNull` invariant audited per §2.13 — done.
- T17: record the cross-file invariant that `GlobalName` MUST be emitted
  with structural equality (`IEquatable<GlobalName>` +
  `Object.Equals`/`GetHashCode` overrides, OR `record class`) so
  `Assert.Equal` over `GlobalName` performs value-equality, NOT reference-
  equality — recorded as conversion-unit cu-7 in the convspec; this plan
  inherits it — done.

## 4. Research Findings

None required. The ratified convspec
(`.codeconv/conversion-specs/test/multiagent/globalize_test.dart.md`)
already cites authoritative provenance on both Dart and .NET sides for
every construct row (the "Rationale + research provenance" section
enumerates 5 newly recorded idioms — `rf-dart-named-constructor-to-csharp-
static-factory`, `rf-dart-named-argument-to-csharp-named-argument`,
`rf-dart-list-literal-to-csharp-list-initializer`,
`rf-dart-list-indexer-to-csharp-list-indexer`,
`rf-dart-expect-isEmpty-to-xunit-assert-empty` — and 8 reused idioms from
sibling test-file specs). This plan inherits all citations from the
convspec and from the cross-cutting idiom KB (FR-012 / SC-007). No new
research was performed for this plan; none is needed.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/multiagent/
globalize_test.dart.md` (ratified convspec; `source_sha256`
`835b084ec2a497797993bffd3264943b83bceec139165e4852f959bda15fb3be`
matches the source's SHA exactly — confirmed at plan-generation time).

Cross-file consistency:
- All four sibling test-file specs (`smoke_test.dart.md`,
  `mad_error_handling_test.dart.md`, `boot_loader_test.dart.md`,
  `global_writers_table_test.dart.md`) pin the SAME framework (xUnit) and
  the SAME `expect(...)` → `Assert.*` matcher-routing table. This plan
  reuses those rows verbatim. NO conflict.
- The SUT spec dependency
  (`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md` and
  `.../lib/multiagent/global_writers_table.dart.md`) is recorded as
  cross-file invariants (free-function → static method; named-ctor →
  static factory; `GlobalName` structural equality). This plan does NOT
  re-derive the SUT-side shape; it records the call-site shape the test
  relies on. NO conflict.
- Convspec `escalations: []` is intentional, not a placeholder
  (rationale section explicitly says so). This plan agrees and emits
  `None.` in §6.

Internal consistency:
- The 5 `[Fact]` method-name manglings in §2.5 match the convspec verbatim.
- The matcher-routing table in §2.11–§2.15 covers all 24 `expect(...)`
  calls counted in §1; no `expect` left unmapped.
- The `!`-translation audit in §2.13 confirms both `!` usages (L38, L102)
  are immediately preceded by `expect(entry, isNotNull)` on the prior line
  (L37, L101); the CONVERSION INVARIANT holds for this file with no
  additional `Assert.NotNull` insertion needed.
- Named-arg parameter-name carve-out in §2.7 (camelCase preserved verbatim,
  NOT PascalCased) is consistent across the convspec and this plan.

## 6. Escalations

None.
