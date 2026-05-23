---
path: test/multiagent/mad_error_handling_test.dart
cycle_group_id: 149
scc_siblings: []
generated_at: 2026-05-21T14:43:10Z
source_sha256: ca5a6a1cb4d3979172f347c655657ba5cab213c030390ad80a23d58023c0e0b4
schema_version: 1
---

# Conversion Plan: test/multiagent/mad_error_handling_test.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/test/multiagent/mad_error_handling_test.dart` (57 lines, sha256 `ca5a6a1cb4d3979172f347c655657ba5cab213c030390ad80a23d58023c0e0b4`):

- **File category**: Dart test file using the `package:test` framework (per convspec: "Dart test file — map to xUnit/NUnit/MSTest"; convspec authoritatively pins **xUnit** as the project-wide test framework).
- **File-level doc comment** (lines 1-6): `///`-style triple-slash doc-comment block describing the file's purpose — "Tests for madGLP error handling, Derived from madGLP-spec.md Section 12: Invariants (negative cases)". Documents that these tests verify invariant-violation / edge-case behaviour.
- **Imports** (line 8): one — `import 'package:test/test.dart';`. No other Dart imports; no project-local imports.
- **Top-level declarations**: exactly one — `void main()` (line 10) — which is the `package:test` runner entrypoint. Body contains exactly one `group(...)` call with no other statements.
- **`group(...)` block** (lines 11-55): single `group('Error Handling', () { ... })` containing five `test(...)` calls.
- **`test(...)` calls** — all five share the same shape: `test('<label>', () { /* Given/When/Then + Spec-section comments */ }, skip: 'Not yet implemented');`. Their labels and spec references:
  1. `'receive for non-existent GlobalizeEntry throws'` → Spec Section 8.3 (StateError on missing GlobalizeEntry)
  2. `'receive for non-existent LocalizeEntry throws'` → Spec Section 8.3 (StateError on missing LocalizeEntry)
  3. `'duplicate LocalizeEntry rejected'` → Spec Section 12 (ArgumentError on duplicate entry; entry-lifecycle invariant)
  4. `'global_send on already-known reader is no-op'` → edge-case (no error; immediate-fire or skip)
  5. `'removing non-existent entry is safe'` → Spec Section 12 (idempotent removal — implementation choice)
- **Executable statements**: zero. Every `test(...)` callback body is comment-only (Given / When / Then + Spec-section references). No `expect(...)`, no `throwsA(...)`, no actual assertions present in source.
- **Async / concurrency**: none — no `async`/`await`/`Future`/`Stream` anywhere.
- **State / setUp / tearDown**: none — no `setUp`, `tearDown`, `setUpAll`, `tearDownAll` calls. The `group` has no shared fixture state.
- **Class declarations / fields**: none.
- **Closure captures**: none — the five `test(...)` body closures capture nothing from `main()` scope (no enclosing variables defined).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec construct-by-construct (the convspec is the authoritative decision record for this file; what follows is the actionable target shape).

| # | Dart construct | C#/.NET target | Source ref (convspec) |
|---|---|---|---|
| 1 | `import 'package:test/test.dart';` | `using Xunit;` (xUnit chosen project-wide; consistency-pinning idiom). Codegen MUST also add `using System;`. Target file lives in namespace mirroring `test/multiagent` (e.g. `<RootNs>.Test.Multiagent`). | `dart.package_test.import_directive` |
| 2 | `void main() { group('Error Handling', () { ... }); }` | **Omit entirely.** xUnit discovers `[Fact]` methods via reflection on `public` classes — there is no per-file entrypoint. The `group` body content becomes the enclosing test class (row 3). Per-file `main`-time setup is N/A here (`main` body is exactly one `group(...)` call). | `dart.package_test.main_entrypoint` |
| 3 | `group('Error Handling', () { ... })` | `public class ErrorHandlingTests { ... }` — group label `'Error Handling'` becomes PascalCased class name `ErrorHandlingTests` (non-identifier chars stripped, conventional `Tests` suffix). Original label preserved as `[Trait("Group", "Error Handling")]` on the class for reporter parity. | `dart.package_test.group_block` |
| 4a | `test('receive for non-existent GlobalizeEntry throws', () { /* … */ }, skip: 'Not yet implemented');` | `[Fact(Skip = "Not yet implemented", DisplayName = "receive for non-existent GlobalizeEntry throws")] public void ReceiveForNonExistentGlobalizeEntryThrows() { }` — empty body; Given/When/Then + Spec Section 8.3 reference migrated to `/// <summary>` XML-doc. | `dart.package_test.test_call_skipped` + `dart.package_test.test_callback_arrow_or_block` |
| 4b | `test('receive for non-existent LocalizeEntry throws', () { /* … */ }, skip: 'Not yet implemented');` | `[Fact(Skip = "Not yet implemented", DisplayName = "receive for non-existent LocalizeEntry throws")] public void ReceiveForNonExistentLocalizeEntryThrows() { }` — empty body; Given/When/Then + Spec Section 8.3 reference in `/// <summary>`. | same |
| 4c | `test('duplicate LocalizeEntry rejected', () { /* … */ }, skip: 'Not yet implemented');` | `[Fact(Skip = "Not yet implemented", DisplayName = "duplicate LocalizeEntry rejected")] public void DuplicateLocalizeEntryRejected() { }` — empty body; Given/When/Then + Spec Section 12 reference in `/// <summary>`. | same |
| 4d | `test('global_send on already-known reader is no-op', () { /* … */ }, skip: 'Not yet implemented');` | `[Fact(Skip = "Not yet implemented", DisplayName = "global_send on already-known reader is no-op")] public void GlobalSendOnAlreadyKnownReaderIsNoOp() { }` — empty body; Given/When/Then in `/// <summary>`. | same |
| 4e | `test('removing non-existent entry is safe', () { /* … */ }, skip: 'Not yet implemented');` | `[Fact(Skip = "Not yet implemented", DisplayName = "removing non-existent entry is safe")] public void RemovingNonExistentEntryIsSafe() { }` — empty body; Spec Section 12 (idempotent removal) reference in `/// <summary>`. | same |
| 5 | File-level `///` doc-comment header (lines 1-6) | Mirror as a file-top `// ---` banner or as a `/// <summary>` on the `ErrorHandlingTests` class carrying the "Tests for madGLP error handling — Derived from madGLP-spec.md Section 12: Invariants (negative cases)" prose. Codegen choice; preserve the spec link verbatim. | (covered implicitly by `group_block` — class-level doc) |

**Cross-cutting nuances mirrored from convspec:**

- **Skip-semantics fidelity**: `skip: 'Not yet implemented'` (Dart `String` skip-reason) → `[Fact(Skip = "Not yet implemented")]` (xUnit non-empty `Skip` string). Lossless both directions.
- **Lifecycle**: no `setUp`/`tearDown` in source → no constructor / `IDisposable.Dispose` needed in target.
- **Async**: no async callbacks → no `public async Task` methods needed.
- **Closure capture**: callbacks capture nothing → xUnit per-test constructor isolation needs no translation work.
- **Exception-type mappings** (`StateError` → `InvalidOperationException`, `ArgumentError` → `ArgumentException`, `throwsA(isA<T>())` → `Assert.Throws<T>(...)`) are **referenced in source comments only** — not present in executable form. Per convspec rationale §"Exception-type cross-reference (out of scope for this file)" + FR-013/FR-023 spec-only/no-guessing discipline: **do NOT emit** `Assert.Throws<T>` scaffolding in the target. The target methods remain empty bodies; the exception expectations stay in the `/// <summary>` doc-comments until the tests are implemented in a future iteration.

## 3. Decomposed Task Units

- **T1**: Emit file-scope `using Xunit;` + `using System;` directives.
- **T2**: Emit namespace declaration mirroring `test/multiagent` subtree (e.g. `<RootNs>.Test.Multiagent`).
- **T3**: Emit `public class ErrorHandlingTests` with `[Trait("Group", "Error Handling")]` and `/// <summary>` carrying the file-level doc-comment (spec link to madGLP-spec.md §12).
- **T4**: Emit `ReceiveForNonExistentGlobalizeEntryThrows()` — `[Fact(Skip="Not yet implemented", DisplayName="receive for non-existent GlobalizeEntry throws")]`, empty body, `/// <summary>` carrying Given/When/Then + Spec Section 8.3.
- **T5**: Emit `ReceiveForNonExistentLocalizeEntryThrows()` — `[Fact(Skip="Not yet implemented", DisplayName="receive for non-existent LocalizeEntry throws")]`, empty body, `/// <summary>` carrying Given/When/Then + Spec Section 8.3.
- **T6**: Emit `DuplicateLocalizeEntryRejected()` — `[Fact(Skip="Not yet implemented", DisplayName="duplicate LocalizeEntry rejected")]`, empty body, `/// <summary>` carrying Given/When/Then + Spec Section 12.
- **T7**: Emit `GlobalSendOnAlreadyKnownReaderIsNoOp()` — `[Fact(Skip="Not yet implemented", DisplayName="global_send on already-known reader is no-op")]`, empty body, `/// <summary>` carrying Given/When/Then edge-case description.
- **T8**: Emit `RemovingNonExistentEntryIsSafe()` — `[Fact(Skip="Not yet implemented", DisplayName="removing non-existent entry is safe")]`, empty body, `/// <summary>` carrying Spec Section 12 + idempotent-removal note.
- **T9**: Verify target file `test/multiagent/MadErrorHandlingTest.cs` (per convspec `target_code_unit`) compiles under xUnit + restores no exception-throwing scaffolding (bodies remain empty).

## 4. Research Findings

none required (convspec already pins the xUnit framework decision with documented authoritative sources — `https://xunit.net/docs/getting-started/v3/getting-started`, `https://pub.dev/packages/test`, `https://xunit.net/docs/comparisons#skip` — and all five constructs have idiom-mappings recorded in the convspec's `research_finding_id` slots `rf-dart-package-test-*`).

## 5. Consistency Pass

Cross-check of the plan against (a) source file, (b) tombstone, (c) convspec, (d) CLAUDE.md / FR-023 / FR-013 discipline:

- **vs source**: 5 test methods (T4–T8) match exactly the 5 `test(...)` calls at lines 12, 21, 30, 39, 48. Skip-string `'Not yet implemented'` matches all 5 occurrences. Group label `'Error Handling'` matches line 11. No source construct missed; no target-side fabrication.
- **vs tombstone**: tombstone `target_path` = `test/multiagent/mad_error_handling_test.cs`; convspec `target_code_unit` = `test/multiagent/MadErrorHandlingTest.cs`. Casing differs — tombstone uses snake_case filename, convspec uses PascalCase. **Fixed — derived from convspec**: the convspec is the ratified per-file authority and explicitly names `MadErrorHandlingTest.cs`; PascalCase is also the standard C# filename convention. T9 references the convspec spelling; tombstone-key normalisation is a downstream codegen concern (the tombstone records the slot, the convspec records the on-disk emitted casing).
- **vs convspec construct list**: all 5 convspec `construct_key` entries are addressed in §2 (rows 1, 2, 3, 4a-e — which fold `dart.package_test.test_call_skipped` and `dart.package_test.test_callback_arrow_or_block` together because each `test(...)` call IS its callback in this file). No convspec construct unmapped; no extra construct invented.
- **vs convspec `conversion_units` cu-1..cu-4**: T1–T2 = cu-1 + cu-2; T3 = cu-3; T4–T8 = cu-4 (5 [Fact(Skip=...)] methods). 1:1 mapping verified.
- **vs convspec `escalations: []`**: this plan inherits the empty escalation list. No new escalation surfaces because no executable assertion mappings (StateError→InvalidOperationException, etc.) are required for this file's source — those mappings are explicitly recorded as future-file concerns per convspec rationale §"Exception-type cross-reference".
- **vs FR-023 / FR-013 (spec-only, no guessing)**: T4–T8 emit empty method bodies, no speculative `Assert.Throws<T>` scaffolding — fidelity-preserving.
- **vs CLAUDE.md "do exactly what is asked"**: scope is one file, comment-preservation is load-bearing (Given/When/Then + Spec-section references documented in convspec rationale), no extra refactoring proposed.

Gaps detected: one (tombstone vs convspec target filename casing) — **fixed — derived from convspec (`target_code_unit` authoritative; downstream-codegen tombstone normalisation handled outside this artefact)**.

## 6. Escalations

None.
