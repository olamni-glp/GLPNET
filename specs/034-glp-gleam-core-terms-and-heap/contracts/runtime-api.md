# Contract: `glp/runtime` public API (F4)

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25
The public surface F4 exposes from the `glp_gleam/` `runtime` subsystem — the contract every later
feature (F5 runner, F6 compiler/loader, F8 corpus) builds on. Re-exported from the `glp/runtime.gleam`
umbrella (R-004); defined in the `glp/runtime/{terms,suspension,heap,unify}.gleam` sub-modules.

This is a **CLI/library contract** (Gleam module API), not a network API. Each operation lists its
preconditions, the success result, and the defined error/verdict outcomes — these are the assertions
the test modules encode (SC-001..SC-007).

---

## Terms — `glp/runtime/terms`

| Function | Signature | Contract |
|---|---|---|
| construct const | `ConstTerm(Constant)` | atom/int/real/string via `Constant` (R-002) |
| construct struct | `StructTerm(String, List(Term))` | functor + ordered args; arity = `list.length(args)`; zero-arity allowed |
| construct var | `VarRef(Int)` | holds a heap address only; role is **not** in the term |
| `nil` | `fn() -> Term` | `ConstTerm(ConstAtom("nil"))` |
| `cons` | `fn(Term, Term) -> Term` | `StructTerm(".", [head, tail])` |
| equality | derived | structural; `VarRef(a) == VarRef(b)` iff `a == b` |

**Guarantees (SC-001)**: all 9 term kinds (atom, int, real, string, compound, empty list, non-empty
list, nested struct, var) construct, structurally inspect, and equality-compare; results match the Dart
model's observable shape.

## Heap — `glp/runtime/heap`

| Function | Signature | Pre / Post / Errors |
|---|---|---|
| `new` | `fn() -> Heap` | empty store, `hp = 0` |
| `allocate_variable` | `fn(Heap) -> #(Heap, Int, Int)` | post: returns `(heap', writer, reader)`; `is_writer(writer)` ∧ `is_reader(reader)` ∧ writer↔reader paired (FCP); deref(writer) = `Unbound(writer)` |
| `is_writer` / `is_reader` / `is_value` | `fn(Heap, Int) -> Bool` | role read from the **cell tag** only (FR-002); no address arithmetic |
| `deref` | `fn(Heap, Int) -> Result(#(Heap, DerefResult), HeapError)` | follows chain; **path-compresses into the returned heap** (R-006/SC-002); `Ok(Unbound(w))` for an unbound writer, `Ok(Bound(t))` for ground; `Error(WriterToWriter \| Cycle)` on a violating chain |
| `bind_writer` | `fn(Heap, Int, Term) -> Result(#(Heap, List(GoalRef)), HeapError)` | pre: `is_writer` ∧ unbound. post: cell→`ValueCell(value)`; returns armed activations (FR-005/FR-008). `Error(AlreadyBound)` if already a value; `Error(NotAWriter)` otherwise |
| `bind_writer_to_var` | `fn(Heap, Int, Int) -> Result(#(Heap, List(GoalRef)), HeapError)` | pre: arg1 unbound writer, arg2 a reader. post: writer→`WriterBound(reader)`; pending suspensions forwarded to the target writer; returns `[]` (FR-006/FR-008). `Error(WriterToWriter)` if the target resolves to a writer |
| `suspend_on_writer` | `fn(Heap, Int, Suspension) -> Result(Heap, HeapError)` | pre: `is_writer` ∧ unbound. post: suspension attached, **reader pairing preserved** (FR-008). `Error(NotAWriter)` otherwise |

**Guarantees**:
- **SC-002**: fresh var derefs to `Unbound`; after `bind_writer` derefs to the value; repeated deref on
  the returned heap is O(1) (compressed — no re-traversal).
- **SC-004**: every WxW situation (bind or deref) yields `Error(WriterToWriter)` — **0** silent
  writer→writer chains.

## Unification — `glp/runtime/unify`

| Function | Signature | Contract |
|---|---|---|
| `unify` | `fn(Heap, Term, Term) -> Result(UnifyOutcome, HeapError)` | three-valued (FR-007) |

`UnifyOutcome` = `Success(Heap)` | `Suspend(Heap, on: Int)` | `Fail`. Defined outcomes (the SC-003 truth
table):

| a | b | outcome |
|---|---|---|
| ground X | ground X (equal) | `Success` (heap unchanged) |
| ground | ground (value/functor/arity differ) | `Fail` |
| unbound writer | ground | `Success` (writer bound) |
| ground | unbound writer | `Success` (writer bound) |
| unbound writer | unbound variable | `Success` (writer→var) |
| struct/N | struct/N same functor | unify args pairwise; first non-Success short-circuits |
| needs an unbound **reader** | … | `Suspend(on:)` — **never `Fail`** |
| any bind that would be writer→writer | … | `Error(WriterToWriter)` (not `Fail`) |

**Invariants**: binds **only writers** (never a reader, never writer-to-writer); **no occurs-check**
(R-008). `Fail` (mismatch) and `HeapError` (structural violation) are distinct outcomes (R-005).

**`Suspend` is a verdict, not a recording (F1).** `Suspend(heap, on:)` reports *that* unification needs
an unbound reader and *which* writer (`on`) a suspension would attach to. `unify` does **not** build or
store a `SuspensionRecord` — it has no goal context (`goal_id`/`resume_pc` are the F5 runner's). The
caller records via `suspend_on_writer` (below) with that context. Spec US2 AS#4 ("a suspension is
recorded") is met end-to-end (F4 verdict + F5 record), not by `unify` alone.

## Suspension — `glp/runtime/suspension`

| Type | Shape | Contract |
|---|---|---|
| `Suspension` | `Suspension(goal_id: Int, resume_pc: Int, armed: Bool)` | armed→activatable once, then disarmed |
| `GoalRef` | `GoalRef(goal_id: Int, resume_pc: Int)` | the activation hand-off to F5 |

**Guarantees (FR-008 / US3)**: suspend on unbound writer → bind-to-value returns an activation list
containing exactly the armed suspension(s); bind-to-variable forwards them to the target and fires
nothing yet. F4 **produces** the list; consuming/scheduling it is F5 — explicitly out of contract.

## Parity (FR-009 / SC-005)

The `parity_test` corpus asserts every operation above against the **Dart source-of-truth** observable
outcome for the same micro-scenario (deref result · unify verdict · activation set). Internal heap
representation (addresses, tags, layout) is **not** part of this contract — it may legitimately differ
from Dart (Clarification 2026-06-25).

## Non-goals (out of this contract — recorded so F5+ add them faithfully)

Imported readers / `VariableEntry` / cross-agent binding (F9+); the goal scheduler / reduction loop
(F5); `MutualRefTerm`, `ModuleTerm` (F6); `gleam_otp`/process-cells; occurs-check.
