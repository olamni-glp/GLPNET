# Phase 0 Research: glp_gleam core terms + heap + unification (F4)

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25
**Spec**: `specs/034-glp-gleam-core-terms-and-heap/spec.md`
**Port basis (source of truth)**: Dart `glp_runtime/lib/runtime/terms.dart` + `heap_fcp.dart` +
`suspension.dart` (ratified by F1 dossier §2.3).
**Normative refs**: `docs/heap/heap-pointer-architecture-spec.md`; `docs/glp-cheat-sheet.md` §2/§8;
`CLAUDE.md` GLP Quick Reference; F1 dossier `docs/research/gleam-atomvm/dossier.md` §4.1/§4.2/§5.

Guidance applied (refine baseline): *prefer the simplest design that satisfies the spec; call out
constraints and rejected alternatives explicitly.*

---

## R-001 — Heap-mutation re-expression: **immutable threaded binding store** (NOT process-cells)

**The spec's one deferred design decision** (Assumptions, spec line 137): GLP's WAM-style heap binds
cells in place; Gleam has no mutable variables. F1 proved **both** re-expressions feasible on BEAM
(dossier §4.1): an *immutable threaded store* (binding produces a new heap value; old cell never
mutated) and a *process-cell heap* (a logic variable = a BEAM process holding `Option(Term)`).

- **Decision**: F4 uses an **immutable threaded binding store** — the heap is a Gleam value
  (an indexed cell store) threaded through `deref` / `bind` / `unify`, each returning an updated heap.
- **Rationale**:
  1. **Faithfulness to the source of truth.** The Dart source does **not** model one-process-per-variable:
     `HeapFCP` is a single `List<HeapCell>` threaded through the runner (`RunnerContext` holds
     `sigmaHat`/`clauseVars`; dossier §4.1). The threaded store is the direct functional transliteration
     of that data structure; the process-cell model would be a *re-architecture*, which FR-012 ("faithful
     port … no … re-design") and FR-009 (observable parity) argue against for the kernel.
  2. **Smallest runnable kernel.** F4 explicitly owns **no scheduler/runner** (Clarification 2026-06-24;
     spec line 22). A pure, deterministic data+binding layer is exactly "the smallest runnable kernel" the
     brief calls for — no process lifecycle, no message races, no timing.
  3. **Deterministic parity & testing.** Pure functions yield deterministic deref results, unify verdicts,
     and activation lists — trivially asserted in synchronous gleeunit tests against the Dart corpus
     (SC-003/SC-005). A process-cell heap injects message-passing nondeterminism into a layer with no
     scheduling responsibility.
  4. **Scope containment.** The spec (Assumptions line 142) states the AtomVM raw-`erlang:spawn`
     concern "only matters if the *process-cell* mechanism is chosen at plan time." Choosing the threaded
     store keeps F4 on **plain BEAM**, AtomVM-concern-free, and `gleam_erlang`-spawn-free.
  5. **Does not foreclose F5.** A threaded store is the *more* composable substrate: F5 (the runner) can
     thread it directly, or later wrap cells in processes if true concurrency is wanted — so this choice
     does **not** pre-decide F5's mechanism (dossier §5 leaves F5 open); it gives F5 the lower-commitment
     foundation.
- **Rejected alternative — process-cell heap** (variable = BEAM process): F1-proven and the natural fit
  for the *concurrent engine*, but premature for a single-runtime data/binding kernel. It imports process
  lifecycle + message-passing nondeterminism + the AtomVM raw-spawn constraint into a layer that does no
  scheduling, and is a re-architecture rather than a port. **Deferred to F5**, where concurrency actually
  lives — and where the threaded store can remain the cell substrate.

> ⚠️ **This is the cascade-bearing decision** (it shapes F5). It is recorded here for owner review before
> `/bk-implement`. It is the conservative/faithful choice and is revisable at the plan level.

## R-002 — Term model: type-safe re-expression of Dart's dynamic `ConstTerm.value`

Dart `ConstTerm` holds `Object? value` (untyped; atom/int/real/string erased to `Object?`). Gleam is
statically typed and cannot hold a heterogeneous `Object?`.

- **Decision**: model the constant payload as a tagged union
  `Constant { ConstAtom(String) | ConstInt(Int) | ConstReal(Float) | ConstString(String) }`, with
  `ConstTerm(Constant)`. The `Term` ADT mirrors `terms.dart`:
  `Term { ConstTerm(Constant) | StructTerm(functor: String, args: List(Term)) | VarRef(addr: Int) }`.
- **Rationale**: faithful to the four constant kinds FR-001 enumerates while satisfying Gleam's type
  system; observable-equivalent to the Dart model (the Dart `value` is always one of those four at the
  heap level). Gleam derives structural equality for custom types → FR-001 "comparable for equality" is
  free (and `VarRef(addr)` compares by `addr`, matching Dart's overridden `==`).
- **Excluded from the F4 core term set** (spec Assumptions line 141): `MutualRefTerm` (O(1) stream-append
  optimization — not in the brief's core list) and `ModuleTerm` (module-dispatch → F6). Recorded so a
  later add is a faithful extension, not a surprise.

## R-003 — List (cons/nil) encoding: faithful to the Dart heap lowering

`terms.dart` defines **no** `ListTerm`; at the *heap* level lists are a cons/nil structure (the parse-layer
`ListTerm` in `glp_engine.dart` is lowered to heap terms). Confirmed from
`glp_engine.dart:1052,1196,1261`:

- **Decision**: `nil` = `ConstTerm(ConstAtom("nil"))`; `cons(H, T)` = `StructTerm(".", [H, T])`. Provide
  `nil` and `cons` constructor helpers in the terms module.
- **Rationale**: this is exactly the Dart engine's heap lowering (`bindWriterConst(w, 'nil')` for nil;
  `StructTerm('.', [head, tail])` for cons). Keeping the same functor/atom makes list *observable shape*
  identical to Dart on the parity corpus (SC-001/SC-005). The parse→heap lowering of *source programs* is
  F5/F6's concern; F4 only needs the faithful heap encoding + constructors.

## R-004 — Module decomposition within the `runtime` subsystem

Spec Assumptions line 143 explicitly makes this a plan-time detail, requiring only F3's
1:1-with-Dart-subsystem rule + legal Gleam module paths.

- **Decision**: fill the `runtime` subsystem as sub-modules under `glp/runtime/`, mirroring the Dart
  `runtime/` files:
  - `glp/runtime/terms.gleam` — the `Term` + `Constant` ADTs + `nil`/`cons` helpers (← `terms.dart`)
  - `glp/runtime/suspension.gleam` — `SuspensionRecord`, `GoalRef`, armed/activation (← `suspension.dart`)
  - `glp/runtime/heap.gleam` — the threaded store: `CellTag`, allocate, deref (+path-compress), bind,
    suspend, WxW/single-assignment detection (← `heap_fcp.dart`)
  - `glp/runtime/unify.gleam` — writer-MGU three-valued unification (← the runner's HEAD-phase unify,
    reduced to the heap-level term×term form FR-007 specifies)
  - `glp/runtime.gleam` — the existing F3 placeholder, kept as the **subsystem umbrella**: re-exports the
    public surface via Gleam type aliases (`pub type Term = terms.Term`, …) + thin wrapper functions, so
    F5+ have a stable `glp/runtime` entry. (If a re-export form proves awkward in Gleam 1.17, fall back to
    a documented umbrella that points callers at the sub-modules — decided at implement, not a spec issue.)
- **Rationale**: preserves F3's rule (the `runtime` subsystem stays one subsystem, now with a `runtime/`
  dir, exactly as `glp_runtime/lib/runtime/` is one subsystem with many files), keeps modules small and
  single-responsibility, and gives the heaviest module (`heap`) its own file.

## R-005 — Outcomes, WxW, single-assignment: explicit `Result` + tagged error union (no silent paths)

FR-004/FR-005 require WxW and double-bind to be "reported loudly … never silent". Gleam has no exceptions
for control flow; `panic` is opaque and untestable.

- **Decision**: heap operations return `Result(_, HeapError)` where
  `HeapError { WriterToWriter(w1, w2) | AlreadyBound(addr) | NotAWriter(addr) | … }`; three-valued
  unification returns a dedicated outcome type
  `UnifyOutcome { Success(Heap) | Suspend(Heap, on: Int) | Fail }` (with `bind`/`deref` errors surfaced as
  `Result(UnifyOutcome, HeapError)` only for the *structural-violation* cases, never for ordinary `Fail`).
- **Rationale**: a tagged error is both "loud" and **testable** (SC-004 asserts 0 silent WxW; the only way
  to assert "detected and reported" is an observable error value). `Fail` (ordinary unification mismatch)
  is a normal verdict, kept distinct from `HeapError` (a structural violation) so the truth-table tests
  (SC-003) and the WxW test (SC-004) never conflate the two — matching the Dart split between a returned
  result and a thrown `StateError` (`heap_fcp.dart:274,366,455`).

## R-006 — Path compression in an immutable store

SC-002 requires "a repeated dereference of the same reference is constant-time (no re-traversal)". With an
immutable store there is no in-place mutation, so compression must be threaded.

- **Decision**: `deref(Heap, Int) -> #(Heap, DerefResult)` where the returned heap has the traversed
  reader→writer chain shortcut (compressed); callers thread the returned heap, so a subsequent `deref` of
  the same reference is O(1). `DerefResult { Bound(Term) | Unbound(writer: Int) }`.
- **Rationale**: faithful re-expression of the Dart in-place path-compressing deref (`heap_fcp.dart:259`)
  — same logical value, shorter chain (spec Edge Case "path compression is read-only-safe"). Tests observe
  the value AND that re-deref on the returned heap does not re-traverse (assert chain length / a
  compression marker). Pure: deref never changes any variable's *logical* value, only chain length.

## R-007 — Suspension storage + activation-list production (F4 owns storage + production, NOT scheduling)

Per Clarification 2026-06-24 + FR-008: F4 records a suspension on an unbound writer and *produces* the
activation list on binding; the scheduler that *consumes* it is F5.

- **Decision**: mirror `suspension.dart` — `SuspensionRecord(goal_id: Int, resume_pc: Int)` with an
  `armed` flag; `GoalRef(goal_id: Int, resume_pc: Int)`. A writer cell carries an optional suspension
  list (the FCP `WriterContent` that preserves the reader pairing while suspensions are attached,
  `heap_fcp.dart:48`). `bind`-to-ground returns `#(Heap, List(GoalRef))` containing one `GoalRef` per
  armed suspension (and disarms them); `bind`-to-variable forwards armed suspensions to the target writer
  and returns `[]`.
- **Rationale**: directly ports `bindWriter`/`bindWriterToReader`/`suspendOnWriter`/`_walkAndActivate`/
  `_forwardSuspensions`. The shared-record/double-activation guard (`disarm()`) is preserved because
  var-to-var forwarding shares the record (FR-008 "forward the suspensions to the target"). F4 produces
  the list; it never runs the goals.

## R-008 — Explicit scope exclusions (recorded so later adds are faithful, not surprises)

Out of F4, per the spec's Clarifications/Assumptions — the Gleam port simply omits them:

- **Imported readers / `VariableEntry`** (multiagent, dossier §5 F9+): omit `allocateImportedReader/Writer`,
  `bindImportedReader`, `bindAny`, `isImportedReader`, `suspendOnReader`'s `VariableEntry` branch. F4 is
  single-runtime; the kernel "must not assume a cross-agent reader will ever appear" (spec Edge Case) — and
  it does not, because the type simply has no imported-reader variant.
- **Scheduler / runner** (F5): no goal queue, no reduction loop.
- **`MutualRefTerm`, `ModuleTerm`** (R-002).
- **No occurs-check** (spec Edge Case): consistent with FCP/Dart — an explicit non-behaviour, recorded.

## R-009 — Toolchain, build & test runtime (reuse F1/F3 pins)

- **Decision**: build/test on **Gleam 1.17.0 / Erlang OTP 25.3.2.8 / rebar3 3.19.0** under **WSL Ubuntu**,
  deps `gleam_stdlib 1.0.3` + `gleam_erlang 1.3.0`, dev `gleeunit 1.11.0`, **no `gleam_otp`** (its
  `proc_lib` use is outside AtomVM's subset; F1 §3). `gleam build --target erlang`; `gleam test`. The
  existing `glp_gleam/manifest.toml` already pins exactly these — F4 adds no runtime dependency (the
  threaded store needs only `gleam_stdlib`; `gleam_erlang` stays an unused-but-pinned dep from F3).
- **Rationale**: F4 is additive inside F3's already-green subtree (FR-010/FR-011). The WSL `smoke.sh`
  gate (F3, additive, separate from `test/run_all_tests.sh`) re-runs `gleam build` + `gleam test`.

## R-010 — Parity-corpus mechanism (Dart baseline, hermetic tests, Claude-only)

FR-009/SC-005 pin observable outcomes to the Dart source-of-truth across a micro-scenario corpus; the
constitution (V) forbids any external API and the project runs LM work in Claude only.

- **Decision**: the parity corpus is a Gleam test module (`glp_gleam/test/…parity…`) whose **expected
  values are the Dart source-of-truth outcomes**, derived by reading the authoritative Dart
  (`heap_fcp.dart` + the heap spec) and encoded as gleeunit assertions. The corpus covers exactly the
  spec's micro-scenarios: allocate / deref / bind-to-value / bind-to-variable / unify (the SC-003 truth
  table) / suspend-and-activate. Parity is asserted on **observable outcomes only** — deref result,
  three-valued verdict, activation set — **never** internal heap layout (Clarification 2026-06-25 / FR-009).
- **Cross-validation (REQUIRED + evidenced — F3 remediation)**: during implement, each corpus
  expected-value is cross-checked against the corresponding Dart behaviour (the existing Dart `runtime`
  unit tests / REPL), and the **command + observed output is recorded per scenario** (F1-dossier evidence
  convention) into a checked-in `parity-evidence.md` — this is the auditable basis for the FR-009/SC-005
  "matches Dart" claim, not a soft SHOULD. The Gleam test suite still stays **hermetic** (expected values
  are baked in; no Dart toolchain dependency in `gleam test`, no shelling out, no external API).
- **Rejected alternative**: shell out to the Dart REPL from the Gleam test run to compute expectations
  live — rejected: it makes `gleam test` depend on the Dart toolchain + working dir, breaks the
  AtomVM/WSL-clean-checkout story (SC-006), and adds a cross-runtime coupling F4 explicitly avoids.

---

## Resolved unknowns summary

| # | Unknown (Technical Context / spec) | Resolution |
|---|---|---|
| R-001 | Heap-mutation mechanism (spec's deferred decision) | Immutable threaded binding store; process-cells deferred to F5 |
| R-002 | Dart dynamic `ConstTerm.value` in a typed language | Tagged `Constant` union (atom/int/real/string) |
| R-003 | List cons/nil encoding | `cons` = `StructTerm(".", [H,T])`; `nil` = `ConstTerm(ConstAtom("nil"))` |
| R-004 | Module layout in `runtime` subsystem | `glp/runtime/{terms,suspension,heap,unify}.gleam` + `glp/runtime.gleam` umbrella |
| R-005 | "Report loudly" in Gleam (no exceptions) | `Result(_, HeapError)` tagged union; `Fail` kept distinct from `HeapError` |
| R-006 | Path compression without mutation | `deref` returns `#(Heap, DerefResult)` with compressed chain |
| R-007 | Suspension/activation without a scheduler | Armed `SuspensionRecord`; bind produces `List(GoalRef)`; var-bind forwards |
| R-008 | Scope boundaries | Imported readers / scheduler / MutualRef / Module / occurs-check all excluded |
| R-009 | Toolchain/build/test | F1/F3 pins; no `gleam_otp`; `gleam test` on BEAM under WSL + `smoke.sh` |
| R-010 | Parity-corpus mechanism (Claude-only, hermetic) | Dart-derived expected values, hand-encoded in gleeunit; cross-validated once (SHOULD) |

**No `NEEDS CLARIFICATION` remains.** All Technical Context unknowns are resolved above.
