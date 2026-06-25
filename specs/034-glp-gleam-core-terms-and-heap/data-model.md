# Phase 1 Data Model: glp_gleam core terms + heap + unification (F4)

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25
Derived from spec Key Entities + `research.md` (R-001..R-010). Dart source-of-truth:
`glp_runtime/lib/runtime/{terms,heap_fcp,suspension}.dart`.

All types live under `glp/runtime/`. Types are Gleam custom types (structural equality derived). The
heap is an **immutable value** (R-001); every mutating operation returns a new `Heap`.

---

## 1. Term (`glp/runtime/terms.gleam`) — ← `terms.dart`

```gleam
pub type Constant {
  ConstAtom(String)     // GLP atom (incl. the nil atom "nil")
  ConstInt(Int)
  ConstReal(Float)
  ConstString(String)
}

pub type Term {
  ConstTerm(value: Constant)                    // ← Dart ConstTerm(Object? value)  [R-002]
  StructTerm(functor: String, args: List(Term)) // ← Dart StructTerm(functor, args)
  VarRef(addr: Int)                             // ← Dart VarRef(addr) — heap address only
}
```

- **Validation rules**: `StructTerm.args` length = the structure's arity (zero-arity allowed →
  zero-argument compound). `VarRef.addr` ≥ 0 and indexes a heap cell (enforced by the heap, not the
  term).
- **List encoding (R-003)** — helpers in `terms.gleam`, no new Term variant:
  - `nil() -> Term` = `ConstTerm(ConstAtom("nil"))`
  - `cons(head: Term, tail: Term) -> Term` = `StructTerm(".", [head, tail])`
- **Equality**: derived structural equality. `VarRef(a) == VarRef(b)` iff `a == b` (matches Dart's
  overridden `==`/`hashCode` on `addr`).
- **Excluded** (R-002/R-008): `MutualRefTerm`, `ModuleTerm` — not in the F4 core term set.
- **Role rule (FR-002, spec AS US1#4)**: a `VarRef`'s reader/writer role is **NOT** encoded in the
  term — it is determined solely by the *heap cell's tag* at `addr` (`heap.is_writer`/`heap.is_reader`).
  No `reader == writer + 1` arithmetic anywhere.

## 2. Cell + Tag (`glp/runtime/heap.gleam`) — ← `heap_fcp.dart` `HeapCell`/`CellTag`

```gleam
pub type Cell {                                  // ← Dart HeapCell(content, tag), typed — the union IS the tag
  WriterCell(reader_addr: Int, suspensions: List(Suspension))  // unbound writer; preserves paired reader (FCP WriterContent) [R-007]
  WriterBound(target: Int)                        // writer bound to another variable (Pointer chain) — still writer-tagged
  ReaderCell(writer_addr: Int)                    // reader → its paired writer (FCP bidirectional)
  ValueCell(term: Term)                           // bound to a ground value (ValueTag)
}

pub type CellTag { WrtTag  RoTag  ValueTag }     // ← Dart CellTag — but DERIVED, never stored (F2/R-005)
pub fn tag(cell: Cell) -> CellTag                // single source of truth: WriterCell|WriterBound→WrtTag, ReaderCell→RoTag, ValueCell→ValueTag
```

- Faithful to the FCP two-cell architecture (heap spec): writer and reader each reference the other;
  an unbound writer holds its `reader_addr` (so the pairing survives while suspensions are attached —
  Dart `WriterContent`, `heap_fcp.dart:48`); binding to a value replaces the writer cell with
  `ValueCell`; binding to a variable makes the writer a `WriterBound(target)` chain.
- **`CellTag` is derived, not stored** (F2): the Dart needed a separate `tag` field because `content`
  was `dynamic`; Gleam's typed `Cell` union already encodes the tag, so a *stored* `CellTag` alongside
  the union would be a second source that can drift. The `tag(cell)` function derives it from the
  constructor — preserving FR-002's "role determined by the cell tag" vocabulary with one source of truth.
- **Role determination**: `is_writer(addr)` = cell is `WriterCell|WriterBound`; `is_reader(addr)` =
  `ReaderCell`; `is_value(addr)` = `ValueCell` (each = `tag(cell) == …`). Constructor/tag-driven, never
  address arithmetic (FR-002).
- **WxW invariant (FR-004)**: no `WriterBound.target` may resolve to a writer cell; deref/bind detect
  and return `WriterToWriter` (R-005), never produce a writer→writer chain.

## 3. Heap / store (`glp/runtime/heap.gleam`) — ← `heap_fcp.dart` `HeapFCP`

```gleam
pub opaque type Heap          // holds the indexed cell store + next-free address (HP)

pub fn new() -> Heap
pub fn allocate_variable(heap: Heap) -> #(Heap, Int, Int)   // -> (heap', writer_addr, reader_addr) [← allocateVariable]
```

- **State**: an indexed store of `Cell` (a `dict.Dict(Int, Cell)` or a growable structure) + `hp`
  (next free address). Immutable: each op returns a new `Heap`. Internal representation is **opaque**
  and explicitly **excluded from parity** (FR-009 / Clarification 2026-06-25) — only observable
  outcomes are pinned.
- **Allocation** mirrors Dart: `writer_addr = hp`, `reader_addr = hp+1`, `hp += 2`; writer cell =
  `WriterCell(reader_addr, [])`, reader cell = `ReaderCell(writer_addr)`. (The `+1` adjacency is an
  *allocation* convenience; role is still read from the tag, never assumed by callers — FR-002.)

### State transitions (a writer cell's lifecycle)

```
WriterCell(reader, [])                      ── suspend_on_writer ──▶ WriterCell(reader, [s, …])
WriterCell(reader, susp)  ── bind_writer(value) ──▶ ValueCell(value)     + activations(susp)      [FR-005/FR-008]
WriterCell(reader, susp)  ── bind_to_var(tgt)  ──▶ WriterBound(tgt_reader) + forward(susp → tgt)  [FR-006/FR-008]
ValueCell(_)              ── bind_* ──────────────▶ Error AlreadyBound (single-assignment)         [FR-005]
WriterCell ──(target would be a writer)──────────▶ Error WriterToWriter                            [FR-004]
```

## 4. Suspension record + activation (`glp/runtime/suspension.gleam`) — ← `suspension.dart`

```gleam
pub type Suspension {                  // ← SuspensionRecord + SuspensionListNode, collapsed
  Suspension(goal_id: Int, resume_pc: Int, armed: Bool)
}

pub type GoalRef { GoalRef(goal_id: Int, resume_pc: Int) }   // ← machine_state.dart GoalRef
```

- A writer cell carries `List(Suspension)` (R-007). `armed` records whether a suspension may activate;
  binding-to-value emits one `GoalRef` per armed suspension and then replaces the cell with the value
  (the records cannot re-fire); binding-to-variable forwards armed suspensions to the target chain's
  **terminal** writer. **Recorded divergence (2026-06-25):** because suspensions are immutable VALUES
  (not the Dart's shared mutable record), F4 does NOT preserve the *cross-writer* single-fire guard for
  one goal suspended on multiple distinct writers — unreachable through F4's API, but **F5 must dedupe
  activations by `goal_id`** when it suspends a goal on multiple writers (R-007).
- **F4 owns storage + activation-list *production* only** — never consumes/schedules (Clarification
  2026-06-24; FR-008). The activation list is the hand-off to the future F5 scheduler.

## 5. Dereference result (`glp/runtime/heap.gleam`)

```gleam
pub type DerefResult {
  Bound(term: Term)        // chain ends at a ground value
  Unbound(writer: Int)     // chain ends at an unbound writer (← Dart returns VarRef)
}

pub fn deref(heap: Heap, addr: Int) -> Result(#(Heap, DerefResult), HeapError)   // [R-006]
```

- Follows reader→writer→(value|unbound) with **path compression** threaded into the returned `Heap`
  (R-006): subsequent `deref` of the same `addr` on the returned heap is O(1) (SC-002). Read-only-safe:
  logical value unchanged, only chain length (spec Edge Case). Cycle / WxW during traversal →
  `Error(WriterToWriter|Cycle)` (← Dart `StateError`, `heap_fcp.dart:265,274`).

## 6. Binding (`glp/runtime/heap.gleam`)

```gleam
pub fn bind_writer(heap: Heap, writer: Int, value: Term) -> Result(#(Heap, List(GoalRef)), HeapError)        // ← bindWriter
pub fn bind_writer_to_var(heap: Heap, writer: Int, reader: Int) -> Result(#(Heap, List(GoalRef)), HeapError)  // ← bindWriterToReader
pub fn suspend_on_writer(heap: Heap, writer: Int, susp: Suspension) -> Result(Heap, HeapError)                // ← suspendOnWriter
```

- `bind_writer`: writer must be `WriterCell` (else `AlreadyBound`/`NotAWriter`); → `ValueCell(value)`;
  returns armed activations (FR-005/FR-008).
- `bind_writer_to_var`: writer→target's reader (`WriterBound`); forwards suspensions to the target
  chain's terminal writer; returns `[]`; target-is-a-writer ⇒ `WriterToWriter` (FR-004/FR-006).

## 7. Unification outcome (`glp/runtime/unify.gleam`)

```gleam
pub type UnifyOutcome {
  Success(heap: Heap)          // σ̂w extended/verified — heap may carry new bindings
  Suspend(heap: Heap, on: Int) // a needed unbound reader — a suspension is recordable on `on`'s writer
  Fail                         // value/functor/arity mismatch
}

pub fn unify(heap: Heap, a: Term, b: Term) -> Result(UnifyOutcome, HeapError)   // [FR-007, R-005]
```

- Three-valued (CLAUDE.md GLP Quick Reference; cheat-sheet §8). Algorithm (faithful to writer-MGU):
  deref both; then —
  - both ground & equal → `Success`; ground & differ (value | functor | arity) → `Fail`
  - unbound writer vs ground → `bind_writer` → `Success`
  - unbound writer vs unbound variable → `bind_writer_to_var` → `Success`
  - struct vs struct, same functor/arity → unify args pairwise, threading the heap; first `Fail`/`Suspend`
    short-circuits
  - a needed **unbound reader** (no local writer reachable to bind) → `Suspend(on:)`, **never `Fail`**
    (spec Edge Case "suspend vs fail" — the most common GLP correctness error)
  - any binding binds **only a writer** — never a reader, never writer-to-writer (→ `HeapError`
    `WriterToWriter`, surfaced as `Error`, not `Fail`) (FR-007 / SC-004)
- **No occurs-check** (R-008 / spec Edge Case) — explicit non-behaviour.

> **F1 — `Suspend` verdict vs suspension *recording* (reconciles spec US2 AS#4).** `unify` has **no goal
> context** — `SuspensionRecord`'s `goal_id`/`resume_pc` belong to the F5 runner, not the F4 kernel.
> So `unify` *produces the verdict* `Suspend(heap, on:)` (the address whose writer a suspension would
> attach to); it does **not** itself build/store a `SuspensionRecord`. Spec US2 AS#4 ("the outcome is
> suspend … and a suspension is recorded against the relevant writer") is satisfied **end-to-end**: F4's
> `unify` yields the suspend verdict + address, and the caller (F5, which owns goal context) records it
> via `suspend_on_writer` (§6, FR-008). The F4 unify test (T015) therefore asserts the **verdict +
> `on` address only** — never a stored record, which `unify` cannot construct. *(Surfaced for owner
> review: this reads AS#4 as the integrated behaviour, not as `unify`'s sole responsibility — consistent
> with the 2026-06-24 clarification that F4 owns suspension storage but not the scheduler/goal context.)*

## 8. Errors (`glp/runtime/heap.gleam`)

```gleam
pub type HeapError {
  WriterToWriter(w1: Int, w2: Int)   // ← Dart "WxW violation" StateError       [FR-004/SC-004]
  AlreadyBound(addr: Int)            // single-assignment second-bind            [FR-005]
  NotAWriter(addr: Int)              // bind/suspend on a non-writer cell
  Cycle(addr: Int)                   // SRSW-violating pointer cycle in deref     [Edge Case]
}
```

`HeapError` (a structural-invariant violation, "reported loudly") is **kept distinct** from the normal
`Fail` verdict (an ordinary unification mismatch) — R-005 — so SC-003 (truth table) and SC-004 (zero
silent WxW) never conflate them.

## 9. Parity corpus (`test/glp/runtime/parity_test.gleam`) — ← FR-009 / SC-005 / R-010

A fixed set of micro-scenarios, each `#(setup, operation, expected_observable_outcome)` where the
expected value is the **Dart source-of-truth** outcome (deref result | unify verdict | activation set),
hand-encoded and cross-validated once against the Dart (R-010). Scenarios: allocate · deref-unbound ·
bind-to-value+deref · bind-to-variable+deref · the SC-003 unify truth table · suspend-then-activate ·
suspend-then-forward-on-var-bind. **Internal heap layout is excluded** from every assertion.

---

### Entity → spec-requirement traceability

| Entity | FR / SC |
|---|---|
| Term / Constant / list helpers | FR-001 · SC-001 |
| Cell + Tag · Heap · allocate | FR-002 · SC-002 |
| deref + DerefResult (path compression) | FR-003 · SC-002 |
| WxW detection · HeapError.WriterToWriter | FR-004 · SC-004 |
| bind_writer (single-assignment) | FR-005 |
| bind_writer_to_var (forward suspensions) | FR-006 · FR-008 |
| unify · UnifyOutcome | FR-007 · SC-003 |
| Suspension · GoalRef · activation list | FR-008 |
| parity corpus | FR-009 · SC-005 |
| (build/test/additive — plan-level) | FR-010 · FR-011 · SC-006 · SC-007 |
| (no language change — port discipline) | FR-012 |
