# Parity Evidence — F4 `glp_gleam` runtime kernel vs Dart source-of-truth

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25
**Purpose (T023 / FR-009 / SC-005 / R-010)**: the auditable basis for the claim that the
Gleam `glp/runtime` kernel's **observable outcomes** match the Dart source-of-truth
(`glp_runtime/lib/runtime/{terms,heap_fcp,suspension}.dart`). Each parity scenario in
`test/glp/runtime/parity_test.gleam` has its expected value **cross-validated** here against
the authoritative Dart behaviour, captured from the Dart `runtime` heap unit-test suite.

The Gleam suite stays **hermetic**: expected values are baked into `parity_test.gleam`; the
Gleam test run has **no** Dart dependency (no shell-out, no toolchain coupling — R-010).
Internal heap layout (addresses, tags, cell shapes) is **excluded** from parity — only
observable outcomes (deref result · unify verdict · activation set) are pinned
(Clarification 2026-06-25).

## Cross-validation runs (commands + observed output)

Run from `glp_runtime/` with the repo Dart SDK (`C:/Users/gavri/dart-sdk/bin/dart.exe`,
3.10.1) on 2026-06-25:

```
$ dart test test/heap/binding_pointer_test.dart test/heap/suspension_pointer_test.dart
  → All tests passed!  (36 passed, 0 failed)

$ dart test test/heap/varref_pointer_test.dart test/heap/circular_term_pointer_test.dart
  → All tests passed!  (23 passed, 0 failed)
```

These are the authoritative Dart heap tests for allocation, deref (+ chains, + cycles),
binding (to value / to variable), WxW, and suspension/activation/forwarding. They define the
observable outcomes the Gleam port reproduces.

## Scenario → Dart authority map

Each row: the parity scenario, the Dart test (file · test name) that fixes the observable
outcome, and the expected value baked into `parity_test.gleam`.

| # | Scenario | Dart authority (`glp_runtime/test/heap/…`) | Observable outcome (baked in) |
|---|----------|---------------------------------------------|-------------------------------|
| 1 | allocate → deref a fresh var | `binding_pointer_test.dart` · "unbound chain returns final writer VarRef" (l.167) + `heap_fcp.dart` `allocateVariable` l.85 | `deref` = `Unbound(writer)` (Dart: `derefAddr` returns `VarRef(writerAddr)`) |
| 2 | bind-to-value → deref | `binding_pointer_test.dart` · "bind to ConstTerm integer" (l.19) | after `bind_writer(w, Int 42)`, `deref(reader)` = `Bound(ConstTerm(ConstInt(42)))`; cell → ValueTag (Dart l.25-27) |
| 3 | bind-to-value (struct) → deref | `binding_pointer_test.dart` · "bind to StructTerm" (l.71) / "containing VarRef" (l.98) | `deref` = `Bound(StructTerm("point", [10, 20]))`; struct args preserved incl. a `VarRef` |
| 4 | bind-to-variable → deref through chain | `binding_pointer_test.dart` · "chain of bindings" (l.127) / "long chain dereferences correctly" (l.148) | `w1→r2`, bind `w2` ground → `deref(r1)`=`deref(r2)`=`Bound('end'/'final')` |
| 5 | unbound bind-to-variable chain → deref | `binding_pointer_test.dart` · "unbound chain returns final writer VarRef" (l.167) | `wa→rb`, `wb` unbound → `deref(ra)` = `Unbound(wb)` (Dart: `VarRef(wb)`) |
| 6 | WxW (writer→writer) | `binding_pointer_test.dart` · "bindWriterToWriter throws" (l.195) / "indirect WxW through deref detected" (l.206); `heap_fcp.dart` `bindVariable` l.671-682 | reported loudly: Gleam `Error(WriterToWriter)` (Dart: `StateError`) — never silent (SC-004) |
| 7 | unify truth table (writer-MGU dispatch) | `heap_fcp.dart` `bindVariable` l.671-708 (VarRef→reader ⇒ `bindWriterToReader`; VarRef→writer ⇒ `bindWriterToWriter` throws; else ⇒ `bindWriter`) + `docs/glp-cheat-sheet.md` §8 (three-valued) + `CLAUDE.md` GLP Quick Reference (suspend vs fail) | const=const→`Success`; mismatch→`Fail`; writer/value→`Success`(bind); writer/var→`Success`(bind-to-var); writer/writer→`Error`; needed unbound reader→`Suspend` (never `Fail`) |
| 8 | suspend → activate on bind-to-value | `suspension_pointer_test.dart` · "On wake, activation pc equals kappa" (l.21): `g=77,pc=1`; "Multiple suspensions … all activate" (l.48); `binding_pointer_test.dart` "binding ground value activates all suspensions" (l.225) | `bind_writer` returns one `GoalRef(goal_id, resume_pc)` per armed suspension |
| 9 | disarmed suspension not activated | `suspension_pointer_test.dart` · "Disarmed suspensions do not activate" (l.72); `binding_pointer_test.dart` l.264 | a disarmed record yields no `GoalRef` (double-activation guard) |
| 10 | suspend → forward on bind-to-variable | `suspension_pointer_test.dart` · "Suspension forwarding when binding to another variable" (l.94): `record(42,500)`; "Chain … forwards suspensions correctly" (l.122): `record(99,999)`; `binding_pointer_test.dart` l.241 | `bind_writer_to_var` returns `[]` (nothing fires); the suspension rides to the target writer; binding the target then fires it |
| 11 | self-bind: writer↔own-reader pair derefs UNBOUND | `binding_pointer_test.dart` · "unbound chain returns final writer VarRef" (l.167) + `heap_fcp.dart` `derefAddr` bidirectional recognizer (l.312-323) | `unify(VarRef(w), VarRef(r))` with `r` = `w`'s own paired reader → `Success`; `deref` = `Unbound(w)`. The Dart `derefAddr` recognizes the writer→paired-reader-points-back shape and returns `VarRef(w)` (still unbound), **not** a `StateError`. The pair is formable precisely because there is no occurs-check. (A genuine multi-hop pointer cycle, not this self-pair, would still be reported loudly.) |

## Notes on faithful divergences (observable-equivalent)

- **`suspend_on_writer` vs Dart `suspendOnReader`.** The Dart tests suspend via
  `suspendOnReader(r, …)`, which immediately routes to the *writer* cell
  (`suspendOnReader` → `suspendOnWriter`, `heap_fcp.dart` l.493-514). F4's public surface
  exposes only `suspend_on_writer` (the reader-routing convenience and the imported-reader
  branch are out of F4 scope — R-008). Suspending on writer `w` is observably identical to
  the Dart `suspendOnReader(r, …)` where `r` is `w`'s paired reader: the activation outcome
  on bind is the same.
- **Path compression.** SC-002 requires it; it shortens chains only and never changes a
  deref's logical value, so it is observable-equivalent to the Dart deref (internal layout
  is excluded from parity).
- **`Suspend` is a verdict, not a record.** `unify` yields `Suspend(heap, on:)` (no
  `goal_id`/`resume_pc` — those are the F5 runner's); the Dart end-to-end behaviour
  ("a suspension is recorded") is met by F4's `unify` verdict + the caller's
  `suspend_on_writer` (F1 reconciliation, data-model §7).
- **No occurs-check — two distinct shapes, both faithful to Dart.** (1) A *structural*
  self-reference `f(X?)` bound into `X`'s writer (`no_occurs_check_struct_test`) derefs to the
  `Bound` struct and terminates — `derefAddr` returns `ValueTag` content without recursing into
  struct args (`heap_fcp.dart` l.331-333; the Dart `circular_term_pointer_test` "dereference
  through circular structure terminates" fixes this). (2) A *pointer* self-bind — a writer bound
  onward to its own paired reader (`no_occurs_check_self_bind_unbound_test` / parity #11) —
  derefs to `Unbound(w)`, because `derefAddr`'s bidirectional recognizer (`heap_fcp.dart`
  l.312-323) reads the writer→paired-reader-points-back shape as still-unbound. Earlier drafts
  conflated these two and mis-asserted a cycle error for (2); the corrected outcome is `Unbound`,
  matching the Dart. A *genuine multi-hop* pointer cycle (distinct from either self-shape) is
  still caught loudly by `deref`'s visited-set guard.
