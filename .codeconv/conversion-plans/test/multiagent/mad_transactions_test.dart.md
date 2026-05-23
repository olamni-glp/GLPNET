---
path: test/multiagent/mad_transactions_test.dart
cycle_group_id: 151
scc_siblings: []
generated_at: 2026-05-21T16:25:29Z
source_sha256: 6f95521ac3a698eebba120929ac864f47b7195345b088b7cf5c62a8df86a15a0
schema_version: 1
---

# Conversion Plan: test/multiagent/mad_transactions_test.dart

## 1. Source Analysis

The file `test/multiagent/mad_transactions_test.dart` is the transaction-level
test suite for the madGLP multi-agent implementation. Header docstring (lines
1-5) records "See: madGLP-spec.md Sections 8.1-8.4".

Imports (lines 7-13): `package:test/test.dart` plus SIX SUT imports —
`package:glp_runtime/runtime/runtime.dart`, `.../runtime/terms.dart`,
`.../multiagent/mad_context.dart`, `.../multiagent/message_queue.dart`,
`.../multiagent/mad_helpers.dart`, `.../multiagent/global_send.dart`.

`void main()` (line 15) contains FOUR sibling `group(...)` blocks:

- `'Receive Transaction'` (lines 16-136) — FIVE `test(...)` blocks:
  1. `'_w(p,i) message: finds GlobalizeEntry by index, binds writer'`
     (lines 17-47): allocate a heap variable pair, `wp.addGlobalizeEntry`,
     `handleMadAssignment` with `GlobalName.writer('p', index)`, derefs the
     writer (asserts `isA<ConstTerm>` + value 42), asserts entry removed
     via `lookupByIndex(index) == null`.
  2. `'_r(p,i) message: finds LocalizeEntry, binds writer'` (lines 49-78):
     mirrors the above for `LocalizeEntry` (`addLocalizeEntry` + `GlobalName.reader`
     + `findByRemote('p', 3) == null`).
  3. `'receive localizes nested variables'` (lines 80-105): uses the
     `(writerAddr, _)` Dart 3 discard pattern; constructs nested
     `[GlobalName.reader('q', 2)]`; calls
     `handleMadAssignmentWithGlobalNames`; asserts `findByRemote('q', 2)`
     is `isNotNull`.
  4. `'receive for non-existent GlobalizeEntry throws'` (lines 107-120):
     `expect(() => ctx.handleMadAssignment(...), throwsStateError)`.
  5. `'receive for non-existent LocalizeEntry throws'` (lines 122-135):
     same `throwsStateError` assertion for the reader path.
- `'Send Transaction'` (lines 138-161) — ONE `test(...)`:
  `'flushMessages sends queued messages'`. Adds an `OutboundMessage`
  to `ctx.mp`, assigns a statement-body lambda to `ctx.onMessageReady`
  that mutates a `final sent = <(String, OutboundMessage)>[];` local,
  calls `flushMessages`, asserts `count == 1`, `sent.length == 1`,
  `sent[0].$1 == 'q'`, `sent[0].$2.type == MessageType.assignment`.
- `'Direct Communication Scenario'` (lines 163-240) — ONE `test(...)`:
  TWO `GlpRuntime` + TWO `MadContext`. Uses `TermVar.reader(readerXp,
  writerAddr: writerXp)`, `globalize(...)` (reader-side, spawn-not-entry
  path), `localize(...)` returns reader; cross-agent `onMessageReady`
  closure dispatches `handleMadAssignment`; final `isA<ConstTerm>` +
  value-1 assertions on q's heap.
- `'Return Value Scenario'` (lines 242-311) — ONE `test(...)`:
  mirror of the above with `TermVar.writer(writerVp, readerAddr:
  readerVp)` (writer-side globalize, entry-not-spawn path); `localize`
  returns writer; reverse-direction message routing q->p; final
  assertions on p's heap (value 42).

Per-test locals are `final` with initializer (Dart 3 records,
constructor calls, list literals, lambda expressions). No `async` /
`Future` / `await` / `setUp` / `tearDown` / `skip` anywhere.

The in-source comments encode `madGLP-spec.md` mathematical notation
(`X?`, `_w(p,1)`, `_r(p,1)`, `:=`, `Y_q`, `Z_q`) and the Direct
Communication / Return Value tests carry LOAD-BEARING multi-line
"Corrected definitions:" / "REVERSE direction" commentary.

## 2. Dart → C#/.NET Conversion Plan

The convspec at
`.codeconv/conversion-specs/test/multiagent/mad_transactions_test.dart.md`
is the authoritative source. The plan mirrors each construct row verbatim.

- **`dart.package_test.import_directive`** → drop `import 'package:test/test.dart';`,
  emit `using Xunit;` at file scope. Also emit `using System;` (needed
  for `InvalidOperationException` + `ValueTuple`) and `using
  System.Collections.Generic;` (for `List<T>`). Idiom
  `rf-dart-package-test-to-dotnet-xunit`.

- **`dart.package_test.import_sut_relative_package`** → collapse SIX
  SUT imports into TWO `using` directives plus one optional `using
  static`:
  - `using <RootNs>.Runtime;` (covers `runtime.dart` + `terms.dart`).
  - `using <RootNs>.Multiagent;` (covers `mad_context.dart`,
    `message_queue.dart`, `mad_helpers.dart`, `global_send.dart`).
  - `using static <RootNs>.Multiagent.MadHelpers;` so the end-to-end
    test bodies call `Globalize(...)` / `Localize(...)` unqualified.
  Idiom `rf-dart-package-sut-import-to-csharp-using`.

- **`dart.package_test.main_entrypoint`** → drop `void main()` entirely;
  xUnit discovers `[Fact]` methods by reflection. Idiom
  `rf-dart-test-main-to-xunit-class-with-facts`.

- **`dart.package_test.group_block`** → FOUR sibling `group(...)` calls
  become FOUR sibling public test classes:
  - `'Receive Transaction'` → `ReceiveTransactionTests`
  - `'Send Transaction'` → `SendTransactionTests`
  - `'Direct Communication Scenario'` → `DirectCommunicationScenarioTests`
  - `'Return Value Scenario'` → `ReturnValueScenarioTests`

  Each class carries `[Trait("Group", "<original label>")]` and
  `[Trait("SpecSection", "8.1-8.4")]`. Idiom
  `rf-dart-package-test-group-to-xunit-class`.

- **`dart.package_test.test_call_executable`** → eight `test(...)`
  callbacks become eight `public void` methods decorated with
  `[Fact(DisplayName = "<original label>")]`. Method-name mangling:
  - `'_w(p,i) message: finds GlobalizeEntry by index, binds writer'` →
    `WPiMessageFindsGlobalizeEntryByIndexBindsWriter`
  - `'_r(p,i) message: finds LocalizeEntry, binds writer'` →
    `RPiMessageFindsLocalizeEntryBindsWriter`
  - `'receive localizes nested variables'` →
    `ReceiveLocalizesNestedVariables`
  - `'receive for non-existent GlobalizeEntry throws'` →
    `ReceiveForNonExistentGlobalizeEntryThrows`
  - `'receive for non-existent LocalizeEntry throws'` →
    `ReceiveForNonExistentLocalizeEntryThrows`
  - `'flushMessages sends queued messages'` →
    `FlushMessagesSendsQueuedMessages`
  - `'p sends X to q, p assigns X := 1, q receives value'` →
    `PSendsXToQPAssignsX1QReceivesValue`
  - `'p sends V? to q, q assigns V := result, p receives result'` →
    `PSendsVToQQAssignsVResultPReceivesResult`

  Given/When/Then + "Corrected definitions:" + "REVERSE direction"
  comments carry verbatim into `/// <summary>` doc-comment blocks per
  method. Idiom `rf-dart-test-callback-to-xunit-method-body`.

- **`dart.expression.final_local_variable_with_initializer`** →
  `final <name> = <expr>;` translates to `var <name> = <expr>;`. Dart's
  optional-`new` becomes mandatory C# `new`. Per-call-site mappings
  pinned in convspec (e.g. `final ctx = MadContext(agentId: 'p',
  runtime: runtime)` → `var ctx = new MadContext("p", runtime);`).
  Idiom `rf-dart-final-local-to-csharp-var-local`.

- **`dart.expression.record_destructuring_pattern_assignment`** →
  `final (a, b) = expr;` → `var (a, b) = expr;` for FIVE occurrences;
  ONE uses `_` discard pattern (line 89: `final (writerAddr, _) =
  runtime.heap.allocateVariable()` → `var (writerAddr, _) =
  runtime.Heap.AllocateVariable();`). Idiom
  `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`.

- **`dart.class.named_constructor_factory`** → `GlobalName.writer/reader`
  and `TermVar.reader/writer` → `GlobalName.Writer/Reader(...)` and
  `TermVar.Reader/Writer(...)` PascalCased static factories on the
  converted class. Idiom
  `rf-dart-named-constructor-to-csharp-static-factory`.

- **`dart.class.named_required_parameter_constructor_invocation`** →
  SUT-spec-determined call shape:
  - `MadContext(agentId: ..., runtime: ...)` → `new MadContext(<agentId>,
    <runtime>)` (positional, per `mad_context.dart.md`).
  - `OutboundMessage(destination: ..., type: ..., payload: ...)` →
    `new OutboundMessage(<destination>, <type>, <payload>)` (positional,
    per `message_queue.dart.md`).
  - `globalize(...)`, `localize(...)`, `handleMadAssignment(...)`,
    `handleMadAssignmentWithGlobalNames(...)` preserve named arguments
    (PascalCased method name, camelCase named-arg labels).
  Idiom `rf-dart-named-argument-to-csharp-named-argument`.

- **`dart.expression.enum_dotted_member_access`** →
  `MessageType.assignment` → `MessageType.Assignment` (PascalCased per
  `message_queue.dart.md` SUT). Research finding
  `rf-dart-enum-member-access-pascalcase`.

- **`dart.expression.list_literal_typed_polymorphic`** → typed
  single-element list literals:
  - `[TermVar.reader(readerXp, writerAddr: writerXp)]` →
    `new List<TermVar> { TermVar.Reader(readerXp, writerAddr: writerXp) }`
  - `[TermVar.writer(writerVp, readerAddr: readerVp)]` →
    `new List<TermVar> { TermVar.Writer(writerVp, readerAddr: readerVp) }`
  - `[GlobalName.reader('q', 2)]` →
    `new List<GlobalName> { GlobalName.Reader("q", 2) }`
  Idiom `rf-dart-list-literal-to-csharp-list-initializer`.

- **`dart.expression.list_literal_int_collection_initializer`** →
  `[1, 2, 3]` → `new List<int> { 1, 2, 3 }` (subject to the
  `message_queue.dart.md` SUT decision between `List<int>` and
  `List<byte>`). Idiom `rf-dart-list-literal-to-csharp-list-initializer`.

- **`dart.expression.generic_list_of_tuple_local_variable_with_collection_add`** →
  `final sent = <(String, OutboundMessage)>[];` →
  `var sent = new List<(string, OutboundMessage)>();`; subsequent
  `sent.add((dest, msg))` → `sent.Add((dest, msg));`. Research finding
  `rf-dart-record-typed-list-and-tuple-add-to-csharp-valuetuple-list`
  (newly registered for the KB by this file's convspec).

- **`dart.expression.record_positional_getter_dollar_n`** →
  `sent[0].$1` → `sent[0].Item1`; `sent[0].$2` → `sent[0].Item2`;
  `sent[0].$2.type` → `sent[0].Item2.Type`. Idiom
  `rf-dart-record-positional-getter-to-csharp-valuetuple-itemn`.

- **`dart.expression.lambda_zero_arg_arrow`** →
  `() => runtimeQ.heap.allocateVariable()` →
  `() => runtimeQ.Heap.AllocateVariable()` (assigned to
  `freshAddrAllocator` parameter declared as `Func<(int, int)>`).
  Idiom `rf-dart-arrow-lambda-to-csharp-lambda`.

- **`dart.expression.statement_bodied_lambda_assigned_to_delegate_field`** →
  THREE occurrences of `ctx.onMessageReady = (dest, msg) { ... };` →
  `ctx.OnMessageReady = (dest, msg) => { ... };`. The Send Transaction
  variant captures-and-mutates `sent`; the Direct Communication /
  Return Value variants dispatch to the other agent's
  `handleMadAssignment`. Idiom
  `rf-dart-statement-body-lambda-to-csharp-statement-body-lambda`.

- **`dart.package_test.expect_throwsStateError`** → TWO occurrences
  (lines 112-119, 127-134) of `expect(() => ..., throwsStateError)` →
  `Assert.Throws<InvalidOperationException>(() => ...);`. Idiom
  `rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe`.

- **`dart.expression.expect_isA_to_xunit_assert_istype`** → FOUR uses
  of `expect(derefed, isA<ConstTerm>())` → `Assert.IsType<ConstTerm>(derefed);`
  (stylistically preferred folded form: `var ct =
  Assert.IsType<ConstTerm>(derefed);` followed by `Assert.Equal(<value>,
  ct.Value);`). Idiom `rf-dart-expect-isA-to-xunit-assert-istype`.

- **`dart.expression.as_cast_after_isA_assertion`** →
  `(derefed as ConstTerm).value` → `((ConstTerm)derefed).Value` OR
  folded with the preceding `Assert.IsType<T>` (recommended). Idiom
  `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.

- **`dart.expression.expect_equals_to_xunit_assertequal`** →
  `expect(actual, expected)` → `Assert.Equal(expected, actual)`
  (argument order flips). Boolean special cases:
  `expect(x, true)` → `Assert.True(x);`,
  `expect(x, false)` → `Assert.False(x);`. Specific call-site
  emissions pinned in convspec. Idiom
  `rf-dart-expect-equals-to-xunit-assertequal`.

- **`dart.expression.expect_isnull_isnotnull`** →
  `expect(x, isNull)` → `Assert.Null(x);`;
  `expect(x, isNotNull)` → `Assert.NotNull(x);`. New rf-id
  `rf-dart-expect-isnull-to-xunit-assertnull` registered for the
  KB (first-recorded for multiagent test convspec layer); reuses
  the pinned `rf-dart-expect-isnotnull-to-xunit-assertnotnull`.

- **`dart.expression.method_invocation_on_owned_madcontext`** →
  ordinary instance-method calls PascalCased:
  `handleMadAssignment` → `HandleMadAssignment`,
  `handleMadAssignmentWithGlobalNames` → `HandleMadAssignmentWithGlobalNames`,
  `flushMessages` → `FlushMessages`,
  `registerGlobalSendSpawns` → `RegisterGlobalSendSpawns`,
  `onWriterBound` → `OnWriterBound`,
  `addGlobalizeEntry` → `AddGlobalizeEntry`,
  `addLocalizeEntry` → `AddLocalizeEntry`,
  `lookupByIndex` → `LookupByIndex`,
  `findByRemote` → `FindByRemote`,
  `bindVariable` → `BindVariable`,
  `derefAddr` → `DerefAddr`,
  `mp.add` → `Mp.Add`. Research finding
  `rf-dart-instance-method-call-to-csharp-pascalcase-call`.

- **`dart.expression.indexed_property_access`** →
  `localizeResult.freshPairs[0].writerAddr` →
  `localizeResult.FreshPairs[0].WriterAddr`;
  `globalizeResult.globalNames[0]` → `globalizeResult.GlobalNames[0]`;
  `localizeResult.useReader[0]` → `localizeResult.UseReader[0]`;
  `sent[0]` → `sent[0]`. Research finding
  `rf-dart-list-indexer-to-csharp-list-indexer`.

- **`dart.expression.identifier_spec_notation_in_comments_preserved`** →
  no executable translation; the spec-notation comments and
  "Corrected definitions:" / "REVERSE direction" blocks survive
  verbatim in `///` doc-comments. Research finding
  `rf-dart-identifier-spec-notation-in-comments-preserved`.

**File-level emission outline** (per convspec `conversion_units`):

1. File header + using directives block
2. `namespace <RootNs>.Test.Multiagent;` (file-scoped)
3. `public class ReceiveTransactionTests` (5 `[Fact]` methods)
4. `public class SendTransactionTests` (1 `[Fact]` method)
5. `public class DirectCommunicationScenarioTests` (1 `[Fact]` method)
6. `public class ReturnValueScenarioTests` (1 `[Fact]` method)

## 3. Decomposed Task Units

- **T1**: emit `using` directives block + file-scoped namespace
  declaration. — done by `file_header_and_using_directives_block` +
  `namespace_declaration` conversion units.
- **T2**: emit `public class ReceiveTransactionTests` with class-level
  `[Trait("Group", "Receive Transaction")]` +
  `[Trait("SpecSection", "8.1-8.4")]`. — done by `class_ReceiveTransactionTests`.
- **T3**: emit `[Fact] public void WPiMessageFindsGlobalizeEntryByIndexBindsWriter()`
  arrange + act + assert per convspec. — done.
- **T4**: emit `[Fact] public void RPiMessageFindsLocalizeEntryBindsWriter()`. — done.
- **T5**: emit `[Fact] public void ReceiveLocalizesNestedVariables()`
  with `var (writerAddr, _) = ...` discard form +
  `HandleMadAssignmentWithGlobalNames` four-named-arg call. — done.
- **T6**: emit `[Fact] public void ReceiveForNonExistentGlobalizeEntryThrows()`
  using `Assert.Throws<InvalidOperationException>(() => ...)`. — done.
- **T7**: emit `[Fact] public void ReceiveForNonExistentLocalizeEntryThrows()`. — done.
- **T8**: emit `public class SendTransactionTests` with
  `[Trait("Group", "Send Transaction")]` +
  `[Fact] public void FlushMessagesSendsQueuedMessages()` body
  including `var sent = new List<(string, OutboundMessage)>();`,
  `ctx.OnMessageReady = (dest, msg) => { sent.Add((dest, msg)); };`,
  `Assert.Equal(1, count)` + `Assert.Equal(1, sent.Count)` +
  `Assert.Equal("q", sent[0].Item1)` +
  `Assert.Equal(MessageType.Assignment, sent[0].Item2.Type)`. — done.
- **T9**: emit `public class DirectCommunicationScenarioTests` with
  `[Fact] public void PSendsXToQPAssignsX1QReceivesValue()` —
  TWO-agent arrange (TermVar.Reader globalize); cross-agent dispatch
  closure; Assert.IsType<ConstTerm> + Assert.Equal(1, ct.Value). — done.
- **T10**: emit `public class ReturnValueScenarioTests` with
  `[Fact] public void PSendsVToQQAssignsVResultPReceivesResult()` —
  TWO-agent arrange (TermVar.Writer globalize); reverse-direction
  dispatch; Assert.IsType<ConstTerm> + Assert.Equal(42, ct.Value). — done.

## 4. Research Findings

none required — every construct row in the convspec is backed either
by a KB-cached pinned `idiom_id` (22 reuses) or by a newly-recorded
`research_finding_id` with authoritative Dart + .NET citations
already inlined in the convspec (FR-024 reproducibility-offline
rule):

- `rf-dart-record-typed-list-and-tuple-add-to-csharp-valuetuple-list`
  (Dart `Records` + .NET `Tuple types`).
- `rf-dart-expect-isnull-to-xunit-assertnull`
  (`matcher` package `isNull` + xUnit `Assert.Null`).

All other idioms (`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`,
`rf-dart-named-constructor-to-csharp-static-factory`,
`rf-dart-named-argument-to-csharp-named-argument`,
`rf-dart-arrow-lambda-to-csharp-lambda`,
`rf-dart-statement-body-lambda-to-csharp-statement-body-lambda`,
`rf-dart-list-literal-to-csharp-list-initializer`,
`rf-dart-record-positional-getter-to-csharp-valuetuple-itemn`,
`rf-dart-expect-isA-to-xunit-assert-istype`,
`rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`,
`rf-dart-expect-equals-to-xunit-assertequal`,
`rf-dart-expect-isnotnull-to-xunit-assertnotnull`,
`rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe`,
`rf-dart-instance-method-call-to-csharp-pascalcase-call`,
`rf-dart-list-indexer-to-csharp-list-indexer`,
`rf-dart-identifier-spec-notation-in-comments-preserved`,
`rf-dart-enum-member-access-pascalcase`) were KB cache hits.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/multiagent/mad_transactions_test.dart.md`
(ratified mirror). Every Dart construct in this file maps to a single
C#/.NET emission via either a pinned `idiom_id` (KB cache hit) or a
newly-recorded `research_finding_id` whose authoritative Dart + .NET
citations are inlined in the convspec. The cross-file SUT-spec
dependencies — `lib/multiagent/mad_context.dart.md`,
`lib/multiagent/message_queue.dart.md`,
`lib/multiagent/mad_helpers.dart.md`,
`lib/multiagent/global_send.dart.md`,
`lib/multiagent/global_writers_table.dart.md`,
`lib/runtime/runtime.dart.md`,
`lib/runtime/terms.dart.md`,
`lib/runtime/heap_fcp.dart.md` — are all referenced by the convspec
construct rows; codegen must honour their pinned C# signatures.
Convspec declares `escalations: []` (intentional). No
`idiom_vs_research`, no `idiom_vs_idiom`, no undecidable points.

## 6. Escalations

None.
