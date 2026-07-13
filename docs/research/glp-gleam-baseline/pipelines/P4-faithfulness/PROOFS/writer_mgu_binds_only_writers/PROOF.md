# PROOF — PI:14 writer-MGU binds only writers

- **Obligation:** PI:14 (`specs/050-full-gleam-combined/contracts/proof-obligations.md`), gates **M1**. Register row: `../INDEX.md` invariant "Writer-MGU binds ONLY writers — never a reader cell, never writer↔writer; includes self-bind→Unbound recognizer" (**RISK-PROOF-writerMGU**; backs PARITY-BAR FB-M1-14, FB-M1-15).
- **Claim (verbatim from the contract):** "the Gleam engine's unification binds only writers — never readers, never writer↔writer — under the immutable-heap/value-copy model, for all three-phase execution paths (tentative HEAD unification included)."
- **Feature / task:** 050-full-gleam-combined, T027 (Lean) + T028 (this dossier).
- **Tool:** Lean 4 (real kernel; oracle = `lake build` exit 0, no `sorry`, no error). Core Lean only, no mathlib — repo convention per `csharp/glp_result_codec/lean/ResultTermRoundTrip/` (029/038 precedent).

## Lean artifact

- **Project:** `glp_gleam/lean/WriterMguBindsOnlyWriters/` (Lake project, toolchain pin `leanprover/lean4:v4.30.0`; model + proof in `WriterMguBindsOnlyWriters/Basic.lean`).
- **Main theorem:** `WriterMguBindsOnlyWriters.writer_mgu_binds_only_writers`
  — for every successful unification step out of a writer-only store, every binding **added** satisfies the per-entry invariant `entryOk` (writer-keyed; payload is a ground-headed value or a writer→reader link), and the writer-only invariant is preserved on the post-store.
- **Tentative-HEAD theorem:** `WriterMguBindsOnlyWriters.head_unify_step_preserves_writer_only`
  — one HEAD-phase tentative unification step (buffered σ̂w extension over the combined view) keeps the view writer-only, keeps the **atomic commit** writer-only, and leaves the committed base untouched; `discard_preserves_writer_only` covers clause abandonment.
- **Corollaries (named):** `readers_never_bound`, `no_writer_to_writer_binding`, `writer_value_binding_never_bare_var`.
- **Reproduce:** `cd glp_gleam/lean/WriterMguBindsOnlyWriters && lake build` (verified native Windows via the elan shim `%USERPROFILE%\.elan\bin\lake.exe`; exit 0, zero `sorry`, zero warnings, 2026-07-11).

## Model ↔ implementation mapping

| Lean definition (namespace `WriterMguBindsOnlyWriters`) | Gleam implementation (function / case) |
|---|---|
| `Addr` (`w n` / `r n`, role in the constructor tag; shared index = the FCP pairing) | `heap.gleam` `CellTag` (`WrtTag`/`RoTag`), read from the cell only (FR-002); `allocate_variable` writer↔reader mutual pointers (`WriterCell(reader_addr)` / `ReaderCell(writer_addr)`) |
| `Term` (`const`, `struct`, `wref`, `rref`) | `terms.gleam` `Term` (`ConstTerm`, `StructTerm`, `VarRef`); `wref`/`rref` = a `VarRef` whose heap cell is writer-/reader-tagged (the tag stands for the `heap.is_writer` lookup in `unify.gleam` `resolve`) |
| `Term.isValue` | the "ground value (const or struct)" side-condition of `unify.gleam` `Resolved.RValue` |
| `Store` = `List (Addr × Binding)`; absence = unbound | `heap.gleam` `Heap` cells dict restricted to its **bound** cells; unbound `WriterCell`/`ReaderCell` = absent key. Extension by cons = every mutating op returns a new `Heap` (R-001, value-copy) |
| `Binding.toValue t` | `heap.gleam` `ValueCell(term)` — written only by `bind_writer` |
| `Binding.toVar a` | `heap.gleam` `WriterBound(target)` — written only by `bind_writer_to_var` |
| `entryOk` / `WriterOnly` | the invariant under proof (readers never re-tagged/bound: `bind_writer` heap.gleam:213 and `bind_writer_to_var` heap.gleam:242 return `NotAWriter` on a reader; no `WriterBound` at a writer target: heap.gleam:236-237) |
| `lookup` | `heap.gleam` `cell_at` / `dict.get` (bound cells only) |
| `Deref` (`value`/`unbound`/`stuck`) | `heap.gleam` `DerefResult` (`Bound`/`Unbound`) + the loud `HeapError` channel (`Cycle`, traversal `WriterToWriter`), kept distinct from unify `Fail` (R-005) |
| `derefW` — no binding → `unbound w` | `deref_walk` `WriterCell` arm (heap.gleam:176-177) |
| `derefW` — `toValue v` → `value v` | `deref_walk` `ValueCell` arm (heap.gleam:178) |
| `derefW` — `toVar (.r n)`, `n = w` → `unbound w` | the **self-bind→Unbound recognizer** (`WriterBound` to its own paired reader, heap.gleam:165-173) — INDEX construction step (c) |
| `derefW` — `toVar (.r n)`, `n ≠ w` → recurse at `n` | `WriterBound(target)` → `ReaderCell(writer_addr)` hop (heap.gleam:156-164,174) |
| `derefW` — `toVar (.w _)` → `stuck` | WxW during traversal (heap.gleam:151-153, FR-004) |
| `derefW` fuel exhaustion → `stuck` | visited-set `Cycle` error (heap.gleam:146-147, FR-003) |
| `Resolved` (`value`/`writer`/`reader`/`stuck`) | `unify.gleam` `Resolved` (`RValue` / `RWriter(terminal)` / `RReader(original, terminal_paired_writer)`) |
| `resolve` | `unify.gleam` `resolve` (deref, then role from the ORIGINAL address's tag, unify.gleam:53-67) |
| `Outcome` (`success added`/`suspend`/`fail`/`error`) | `unify.gleam` `UnifyOutcome` (`Success(heap)`/`Suspend(heap, on)`/`Fail`) + the `Result(_, HeapError)` error channel collapsed into one type; `success` carries the **added** bindings so the post-store is `added ++ pre` |
| `bindWriterValue` | `heap.gleam` `bind_writer` (unbound writer → `ValueCell`; `AlreadyBound` on `ValueCell`/`WriterBound` — including a self-bound writer — heap.gleam:208-214) |
| `bindWriterToVar` | `heap.gleam` `bind_writer_to_var` (unbound writer → `WriterBound(reader)`; writer-tagged target = `WriterToWriter`, heap.gleam:236-237; bound source = `AlreadyBound`, heap.gleam:240-241) |
| `unify` (the 10-arm dispatch) | `unify.gleam` `unify` + `unify_resolved` (value×value → structural, :76; writer×value → `bind_writer`, :79-80; writer×reader → `bind_writer_to_var`, :83-86; **writer×writer → loud error, never bound**, :89 / SC-004; reader×value & reader×reader → `Suspend`, never `Fail`, :93-95) |
| `unifyValues` | `unify.gleam` `unify_values` (constant structural equality; functor + arity agreement before args; mismatch → `Fail`, :113-127) |
| `unifyArgs` | `unify.gleam` `unify_args` (pairwise, threading the extended heap; first non-`Success` short-circuits, :131-145) |
| `Tentative` (`buf` = σ̂w, `base`), `view`/`commit`/`discard`, `headUnifyStep` | the three-phase tentative-HEAD discipline: writer occurrences "tentatively bind in σ̂w" (`opcodes.gleam` §6.2 `HeadVariable`), GUARD pure, clause commit adopts σ̂w atomically, abandonment discards it. The full HEAD-phase runner is T021 (see Scope) |

## Prose proof

**Definitions.** A *store* is a finite sequence of (address, binding) entries — the bound cells of the immutable heap; unbound writer/reader cells are exactly the absent addresses. Addresses carry their FCP role tag (writer `w n` / reader `r n`; `r n` is paired with `w n`). A binding is either `toValue t` (a `ValueCell`) or `toVar a` (a `WriterBound` link). An entry is *good* (`entryOk`) iff its key is a **writer** address and its payload is either a ground-headed value (const- or struct-headed — never a bare variable reference) or a link to a **reader** address. A store is *writer-only* (`WriterOnly`) iff all its entries are good. Unification returns one of four verdicts: `success added` (the extension), `suspend w`, `fail`, or `error` (the loud `HeapError` channel — WxW, cycle, double-bind). Only `success` extends the store.

**Lemma 1 (deref groundedness — `derefW_value_isValue`).** In a writer-only store, if dereferencing terminates on a value, that value is ground-headed. *Proof:* induction on the deref fuel. The `value` verdict arises only (i) directly from a `toValue v` entry — good by the invariant, so `v` is ground-headed — or (ii) through a `toVar (r n)` hop, closed by the induction hypothesis at the paired writer `n`. A `toVar (w _)` hop is `stuck` (traversal WxW), the self-bind case yields `unbound`, and fuel exhaustion is `stuck` (cycle); none yields a value. ∎

**Lemma 2 (resolve groundedness — `resolve_value_isValue`).** In a writer-only store, `resolve` classifies a term as `RValue v` only for ground-headed `v`: directly for const/struct terms, and via Lemma 1 for dereferenced variable references. ∎

**Lemma 3 (`bind_writer` adds only good entries — `bindWriterValue_ok`).** A successful `bind_writer` step adds exactly one entry, keyed by a writer address, with payload `toValue v`; given `v` ground-headed (Lemma 2 at the call sites), the entry is good. An already-bound writer is rejected (`AlreadyBound` → error, no binding). ∎

**Lemma 4 (`bind_writer_to_var` adds only good entries — `bindWriterToVar_ok`).** A successful `bind_writer_to_var` step adds exactly one entry, keyed by a writer address, with payload `toVar (r n)` — a link to a *reader* address. A writer-tagged target is rejected as `WriterToWriter` before any binding; an already-bound source is rejected as `AlreadyBound`. ∎

**Main theorem (`writer_mgu_binds_only_writers`, via `unify_family_ok`).** *For every store `s` with `WriterOnly s`, every fuel, and all terms `a`, `b`: if `unify s fuel a b = success added` then every entry of `added` is good, and `WriterOnly (added ++ s)`.*

*Proof:* joint induction on fuel over the three mutually recursive functions (`unify`, `unify_values`, `unify_args`), mirroring the Gleam recursion structure exactly.

- *Base (fuel = 0):* all three return `error`; the success hypothesis is vacuous (and, semantically, divergence yields no binding).
- *Step — `unify`:* case analysis on the resolved roles of the two sides (the writer-MGU dispatch):
  - **value × value** → `unify_values` at smaller fuel; induction hypothesis.
  - **writer × value** (either order) → Lemma 3, with the payload ground-headed by Lemma 2.
  - **writer × reader** (either order) → Lemma 4.
  - **writer × writer** → `error`; **no binding is ever produced on this path** — the success hypothesis is contradictory. (This is the mechanized content of "never writer↔writer".)
  - **reader × value, reader × reader** → `suspend`; no binding (the success hypothesis is contradictory). Readers are *waited on*, never bound — the canonical GLP suspend-not-fail point.
  - any `stuck` resolve → `error`; no binding.
- *Step — `unify_values`:* equal constants add nothing (`added = []`); mismatched constants, functors, arities, or shapes `fail` and add nothing; matching structs recurse into `unify_args` at smaller fuel.
- *Step — `unify_args`:* the empty pair adds nothing. For `x::xr` vs `y::yr`: `unify x y` yields good additions `d1` (IH); the tail runs against the **threaded extended store** `d1 ++ s`, which is writer-only by `writerOnly_append` — this is where invariant *preservation* is load-bearing, not just a conclusion — and yields good additions `d2` (IH); the total addition `d2 ++ d1` is good, membership distributing over the append. Any non-success in either position short-circuits with no addition.

Every added entry is therefore good, and `added ++ s` is writer-only. ∎

**Corollaries.** `readers_never_bound` (no reader address ever enters the binding domain), `no_writer_to_writer_binding` (no added `WriterBound` targets a writer), `writer_value_binding_never_bare_var` (no added `ValueCell` holds a bare variable reference — writers are never bound to bare writers).

**Tentative-HEAD path (`head_unify_step_preserves_writer_only`).** The HEAD phase runs the *same* unification algorithm; it differs only in where additions land: the buffered tentative set σ̂w (`Tentative.buf`) extending the committed heap (`Tentative.base`). One tentative step unifies against the combined view `buf ++ base`; by the main theorem the extended view `(added ++ buf) ++ base` is writer-only. Clause commit adopts the buffer **atomically** — since the store is an immutable value, the commit is the adoption of the already-writer-only extended value, and no partial interleaving is representable — so the committed heap is writer-only. Clause abandonment (`discard_preserves_writer_only`) keeps the untouched `base`, writer-only as a sub-store. Hence the invariant holds on all three-phase execution paths: tentative HEAD extension, atomic commit, and discard. ∎

**Non-vacuity.** The model is executable and exercised in-file by `example` evaluations pinned by `rfl`: writer×constant binds (the one added entry, writer-keyed), writer×reader links writer→reader, writer×writer errors with **no** binding, reader×value suspends on the paired writer, constant mismatch fails, struct decomposition reaches a nested writer, the self-bind→Unbound recognizer derefs `unbound` yet re-binding is `AlreadyBound`, and the invariant itself is falsifiable (a reader-keyed entry and a writer→writer link each violate it).

## Scope and assumptions

What the abstraction **captures**: the full verdict-level case analysis of `unify.gleam` (`resolve` role dispatch, structural decomposition with heap threading, WxW rejection, suspend-not-fail on readers) and the binding guards of `heap.gleam` (`bind_writer`, `bind_writer_to_var`, single-assignment `AlreadyBound`, traversal WxW/cycle, the self-bind→Unbound recognizer — INDEX construction steps (a), (b), (c)).

What it does **not** capture:

1. **Path compression** (`heap.gleam` `compress`): deref retargets traversed `ReaderCell`s to the chain terminal. This is not a binding — the cell stays reader-tagged and dereferences to the same result — and is internal layout excluded from parity (heap.gleam FR-009). The model treats deref as read-only.
2. **Suspension records** (`suspend_on_writer`, `forward_to_terminal`, `forward_suspensions`): these mutate only the suspension list of *unbound* `WriterCell`s, never the binding domain; activation lists are the F5 caller's concern (unify.gleam:99-101). Omitted.
3. **Scheduling and the engine loop**: goal queues, reduction budgets, generation-scoped wakes (engine/types.gleam) are outside the binding step. The theorem is per-unification-step; the scheduler composes steps but adds no bindings itself.
4. **Distribution**: cross-instance deref/binding is PI:17 (`glp_gleam/lean/DistDerefConvergence/`), not this obligation.
5. **Termination**: GLP has no occurs-check (R-008), so unification over cyclic heap shapes can diverge; the model's fuel maps divergence/cycles to the loud error verdict. The claim proved is: *whenever unification returns success, only writers were bound* — no claim about termination.
6. **Direct heap-API misuse**: a hypothetical non-unify caller passing a bare `VarRef` as a `bind_writer` value is outside the engine's unification paths and outside this theorem; the writer-only precondition and the T026 adversarial suite guard the engine-level entry points.
7. **The T021 runner**: at authoring time the three-phase HEAD-phase runner (`glp_gleam/src/glp/engine/runner.gleam`, task T021) is not yet implemented; the tentative-HEAD model follows the σ̂w discipline fixed by `opcodes.gleam` §6.2 (`HeadVariable`: writer occurrences "tentatively bind in σ̂w") and the Dart reference semantics (`RunnerContext.sigmaHat`). The model's `headUnifyStep` is the discipline T021 must implement: HEAD additions land in the buffer, commit is atomic value adoption, discard leaves the base untouched. The T026 adversarial suite pins this on the delivered runner.

No mismatch between the contract claim and the delivered `unify.gleam`/`heap.gleam` was found while constructing the model: every binding write in `heap.gleam` is guarded exactly as the claim requires (readers `NotAWriter`, writer targets `WriterToWriter`, double-binds `AlreadyBound`), and `unify.gleam` rejects writer×writer before reaching the heap.

## Status

- **Lean:** green — `lake build` exit 0, zero `sorry`, zero warnings (toolchain `leanprover/lean4:v4.30.0`, 2026-07-11, T027). **Re-verified 2026-07-13 on Olamnit** (elan 4.2.3 installed fresh, Lean v4.30.0 auto-fetched, `lake build` exit 0, `Built WriterMguBindsOnlyWriters.Basic` in 2.2s, no warnings) — kernel-checked on the delivery machine, not a relayed status.
- **Prose:** this dossier (T028).
- **Tests:** the discharge is completed by the **T026 adversarial gleeunit suite** (`glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam`: reader/reader, writer/writer, nested-structure, tentative-HEAD cases asserting the invariant and the rejection paths).
- **INDEX flip:** the `../INDEX.md` row OPEN → discharged is **deliberately not performed here** — per the contract's bookkeeping rule, all four artifacts (Lean green, this PROOF.md, test suite green, INDEX row update) land in **one checkpointed commit**, traceable from `specs/050-full-gleam-combined/tasks.md`; the flip happens at that checkpoint.
