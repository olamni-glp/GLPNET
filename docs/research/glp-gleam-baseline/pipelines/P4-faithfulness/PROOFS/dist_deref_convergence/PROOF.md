# PROOF — PI:17 distributed-dereference termination + convergence

- **Obligation:** PI:17 (`specs/050-full-gleam-combined/contracts/proof-obligations.md`), gates **M2**. Register row: `../INDEX.md` invariant "Distributed deref/unify across two LINKED instances … every cross-instance deref eventually answered" (**RISK-PROOF-distDeref**; backs PARITY-BAR FB-M2-01, FB-M2-04).
- **Claim (verbatim from the contract):** "deref chains crossing instance boundaries terminate and converge to the same binding on all participating instances (deferred-local-assignment; globalize/localize on `known/1`)."
- **Feature / task:** 050-full-gleam-combined T058 (Lean) — driven under feature 059 Wave-3 WP **T083** `close-proofs-proof-dist-deref-convergence`.
- **Tool:** Lean 4 (real kernel; oracle = `lake build` exit 0, no `sorry`, no `admit`, no `axiom` cheat, no error). Core Lean only, no mathlib — repo convention per `WriterMguBindsOnlyWriters/` (PI:14) and `csharp/glp_result_codec/lean/ResultTermRoundTrip/`.
- **Recorded deviation:** P4 originally planned **SPIN** for this obligation; **Lean is owner-directed** (2026-07-10). The SPIN precedent (`docs/research/repl-engine-separation/spikes/spin/front_back.pml`) is retained as supplementary, non-gating evidence (a minimal single-message handshake only — it never modelled distributed-deref semantics).

## Lean artifact

- **Project:** `glp_gleam/lean/DistDerefConvergence/` (Lake project, toolchain pin `leanprover/lean4:v4.30.0`; model + proof in `DistDerefConvergence/Basic.lean`).
- **Theorem 1 (termination):**
  - `DistDerefConvergence.derefDist_terminates` — under an acyclicity certificate `rank` (a `Node → Nat` strictly decreasing across every link hop, local **or** seam-crossing), `derefDist` reaches a definite terminal within `rank n + 1` hops and returns that terminal's binding — never the loud `.stuck` fuel-exhaustion verdict. Supported by `resolveNode_terminates` (bounded resolution), `resolveNode_mono`/`resolveNode_mono_le` (fuel stability), `resolveNode_terminal` (a resolved terminal is genuinely terminal — never mid-chain), and `derefDist_not_stuck` (corollary).
  - **FORK-1 dual:** `DistDerefConvergence.cyc_stuck` — the canonical cross-instance cycle (`cyc`) yields the loud `.stuck` verdict, and hence **no binding**, at **every** fuel (`cyc_never`). Exactly the `<circular>` / "no cycles across the seam" discriminator.
- **Theorem 2 (convergence):**
  - `DistDerefConvergence.dist_deref_converges` — a globalized writer `⟨.B, w⟩` with a value settled by delivery: instance A's globalized reference and instance B's local handle both deref to that one value — the two instances **agree**, on the delivered value `v`. Core lemma `derefDist_eq_of_terminal` (two nodes resolving to the same terminal read the one owner cell); fuel-agnostic form `derefDist_converges`; routing lemma `handles_resolve_to_owner`.
  - **Robustness:** `dist_deref_agrees_pre_quiescence` — even with the assignment still in flight (owner unbound), the two instances agree (both `unbound`). Quiescence is needed only to pin *which* value they agree on, not for agreement itself.
- **Deferred-local-assignment + quiescence:** `deliver` (owner-only binding), `deliver_binds_owner`, `drainN`, and `drainN_quiescent` — a run with a finite in-flight backlog drains to `Quiescent` (the GAP-G6 predicate, `c.chan = []`).
- **Axiom hygiene:** `#print axioms` on `derefDist_terminates`, `cyc_stuck`, `dist_deref_converges`, `drainN_quiescent`, `resolveNode_terminates` reports only `[propext, Quot.sound]` — the standard trusted Lean axioms, **no `sorryAx`** (and not even `Classical.choice`).
- **Reproduce:** `cd glp_gleam/lean/DistDerefConvergence && lake build` (native Windows via the elan shim `%USERPROFILE%\.elan\bin\lake.exe`; exit 0, zero `sorry`, zero warnings, 2026-07-27).

## Model ↔ implementation mapping

| Lean definition (namespace `DistDerefConvergence`) | Gleam / spec source |
|---|---|
| `Inst` (`A` / `B`) | the two participating `instance id`s of the seam (data-model.md:49); theorems are stated over arbitrary `Node`s, so `Inst` is only the concrete carrier |
| `Node` (`inst`, `wid`) = a `RemoteVarRef` | `glp/link/` `RemoteVarRef` = `(instance id, writer id)` (data-model.md:49-50) |
| `Cell` (`value` / `link`); absence = unbound | the bound half of a per-instance immutable heap; a same-inst `link` = local `WriterBound`, a cross-inst `link` = a globalized `RemoteVarRef` |
| `Store` = `List (Nat × Cell)`; extension by cons | per-instance bound-cells store; immutable value-copy (R-001), first-match `lookup` (a later cons shadows) as in PI:14 |
| `Config` (`storeA`, `storeB`, `chan`) | two-instance binding stores + the in-flight message channel (link-parity.md §"Distributed unification") |
| `cellAt` | cross-instance cell fetch: select owning instance's store, then `lookup` |
| `globalize` / `localize` | the `known/1` boundary: globalize on export (tag a local writer with its owner instance), localize on import (a ref back to self becomes a local writer; a ref to the far instance stays remote) — link-parity.md:18 |
| `resolveNode` (fuel-bounded terminal walk; every hop costs one fuel) | cross-instance deref following link hops; fuel is exactly the "local-hop + message-hop union" measure; fuel exhaustion = the loud cyclic verdict (no terminal) |
| `Deref` (`value` / `unbound` / `stuck`) | the deref verdict (mirrors PI:14 `Deref` / heap.gleam `DerefResult` + the loud `HeapError` / `<circular>` channel) |
| `derefDist` | resolve to terminal, then read the terminal cell; no terminal → `.stuck` |
| `Msg.assign` / `deliver` / `drainN` | deferred-local-assignment: only the owning instance binds its writer, then the message leaves the channel (link-parity.md:17 "remote requests queue") |
| `Quiescent` (`c.chan = []`) | the GAP-G6 quiescence oracle predicate (data-model.md:66-67): no in-flight frames |
| `cyc` / `cyc_never` / `cyc_stuck` | the FORK-1 cross-seam cycle — the `<circular>` discriminator (data-model.md:50) |

## Prose proof

**Definitions.** A *node* is a `RemoteVarRef` `(inst, wid)`. A per-instance *store* holds the bound writer cells; an absent writer is unbound. A cell is a `value v` (ground, `Nat`-abstracted) or a `link m` (a forward pointer). A `link` whose target is on the *same* instance is a local writer-bound link; a `link` whose target is on the *other* instance is a **globalized** cross-instance reference, and dereferencing it hops the seam. `resolveNode c fuel n` follows link hops from `n` to the terminal of its chain: it stops (returns `some n`) at an unbound writer, a value cell, or a self-link (`link n`); it hops to `m` on a proper `link m` (`m ≠ n`), decrementing fuel; and it returns `none` (no terminal) when fuel is exhausted. Every hop — local or seam-crossing — costs one unit of the same fuel, so **fuel is precisely the local-hop + message-hop union measure** the contract names. `derefDist` resolves the terminal then reads its cell; a `none` resolution is the loud `.stuck` verdict.

### Theorem 1 — termination

**Fuel stability (`resolveNode_mono`, `resolveNode_mono_le`).** If `resolveNode c f n = some t`, then `resolveNode c g n = some t` for every `g ≥ f`. *Proof:* induction on fuel via the per-arm reduction lemmas (`resolveNode_none/value/self/link`); a value/unbound/self terminal is unchanged by extra fuel, and a link hop delegates to the induction hypothesis at the successor node. ∎

**Terminal well-formedness (`resolveNode_terminal`).** If `resolveNode c f n = some t`, then `cellAt c t` is unbound, a value, or a self-link — never a proper link onward to a *different* node. Resolution never stops mid-chain. *Proof:* induction on fuel; the three terminal arms establish the disjunction directly, the link arm delegates to the IH. ∎

**Bounded termination (`resolveNode_terminates`), the well-founded core.** Suppose the configuration carries an *acyclicity certificate*: a `rank : Node → Nat` such that for every link edge `cellAt c n = some (link m)` with `m ≠ n`, `rank m < rank n`. Then for every `n`, resolution reaches a terminal within `rank n + 1` hops: `∃ t, resolveNode c (rank n + 1) n = some t`. *Proof:* induction on a fuel budget `k` with `rank n ≤ k`. At a terminal cell we are done in one step. At a proper link hop to `m`, `rank m < rank n ≤ k` gives `rank m ≤ k − 1`, and the induction hypothesis resolves `m`; the link reduction lemma lifts it back to `n`. The base case `k = 0` forces `rank n = 0`, so a proper outward edge would need `rank m < 0` — impossible — and only terminal cells remain. This is the strictly-decreasing well-founded measure across the local-hop + message-hop union. ∎

**Headline (`derefDist_terminates`) + corollary (`derefDist_not_stuck`).** Under the acyclicity certificate, `derefDist c (rank n + 1) n` returns the terminal's binding (a `value` or an `unbound`) and is never `.stuck`. ∎

**FORK-1 dual (`cyc_never`, `cyc_stuck`).** GLP has **no occurs-check**, so a cross-instance cycle can exist: `cyc` binds `⟨A,0⟩ → link ⟨B,0⟩` and `⟨B,0⟩ → link ⟨A,0⟩`. Then `resolveNode cyc f ⟨A,0⟩ = none` and `resolveNode cyc f ⟨B,0⟩ = none` for **every** `f`. *Proof:* induction on `f`, proving both directions jointly; the single-step case goes through because one hop crosses the seam A→B (respectively B→A), swapping the pair, so `resolveNode (f+1) ⟨A,0⟩` reduces to `resolveNode f ⟨B,0⟩`, which is `none` by the IH. Hence `derefDist cyc f ⟨A,0⟩ = .stuck` at every fuel — a loud verdict, **no binding**. This is the mechanized content of "deref chains crossing instances terminate (FORK-1 discriminator honoured)": either they reach a genuine terminal (acyclic case) or they halt loudly with no binding (cyclic case); they never silently loop or fabricate an answer. ∎

### Theorem 2 — convergence

**Shared-terminal agreement (`derefDist_eq_of_terminal`).** If `resolveNode c f n1 = some t` and `resolveNode c f n2 = some t`, then `derefDist c f n1 = derefDist c f n2`. *Proof:* both deref verdicts are `readCell c t` — the one owner cell. ∎ Fuel-agnostic form `derefDist_converges` lifts two resolutions at different fuels `fa`, `fb` to the common fuel `fa + fb` via `resolveNode_mono_le`.

**Routing (`handles_resolve_to_owner`).** For an owner writer `⟨B, w⟩`, instance A's handle `⟨A, ha⟩ → link ⟨B, w⟩` and instance B's handle `⟨B, hb⟩ → link ⟨B, w⟩` both resolve (in one hop) to the owner terminal. The A-hop crosses the seam (owner instance B ≠ A, discharged by the instance tags); the B-hop is a local link (`⟨B,w⟩ ≠ ⟨B,hb⟩` supplied). ∎

**Headline (`dist_deref_converges`).** With the owner cell settled to a value, `cellAt c ⟨B,w⟩ = some (value v)`, both `derefDist c 2 ⟨A,ha⟩` and `derefDist c 2 ⟨B,hb⟩` equal `.value v`. Hence the two instances **agree**, and on the delivered value `v`. *Proof:* both handles resolve to the owner terminal (routing); shared-terminal agreement gives equality; the owner cell reads `.value v`. ∎

**Robustness to non-quiescence (`dist_deref_agrees_pre_quiescence`).** If the owner is still unbound (assignment in flight), both handles deref to `.unbound ⟨B,w⟩` — the instances still **agree**. Agreement is invariant across delivery; quiescence pins *which* value, not *whether* they agree. This is the deferred-local-assignment discipline made precise: before the owner delivers, no instance observes a binding, so no divergence is possible; after delivery every instance that references the writer reads the one owner cell. ∎

### Deferred-local-assignment, delivery, and quiescence

`deliver` applies one in-flight `assign ⟨inst,w⟩ v`: **only** the owning instance's store gains `(w, value v)` (cons-extension), and the message leaves the channel (`deliver_chan`). `deliver_binds_owner` shows the delivered owner cell then reads `some (value v)`. `drainN fuel` delivers up to `fuel` messages; `drainN_quiescent` proves that with `fuel ≥ chan.length` the result is `Quiescent` (`chan = []`) — a run with a finite backlog quiesces. This is the reachability half of the GAP-G6 oracle assumed by Theorem 2.

**Non-vacuity.** The model is executable and the full pipeline is pinned by `rfl`/`decide` `example`s over a worked configuration `demo` (A's globalized ref `wid 0 → ⟨B,7⟩`, B's local handle `wid 1 → ⟨B,7⟩`, one in-flight `assign ⟨B,7⟩ 99`):

- **before quiescence:** `derefDist demo 3 ⟨A,0⟩ = .unbound ⟨B,7⟩ = derefDist demo 3 ⟨B,1⟩` (agree; owner unbound), and `¬ Quiescent demo`;
- **delivery:** `Quiescent (drainN 1 demo)`;
- **after quiescence:** `derefDist (drainN 1 demo) 3 ⟨A,0⟩ = .value 99 = derefDist (drainN 1 demo) 3 ⟨B,1⟩` (converge to the delivered value);
- **FORK-1:** `derefDist cyc 10 ⟨A,0⟩ = .stuck` (loud, no binding);
- **known/1 boundary:** `localize B (globalize B 7) = some 7` (owner re-localizes its export), `localize A (globalize B 7) = none` (the far instance keeps it a remote ref).

## Quiescence assumption (GAP-G6)

Theorem 2's "once messages quiesce" hypothesis is discharged against an explicit **quiescence predicate**: `Quiescent c := (c.chan = [])` — no in-flight frames, matching the GAP-G6 distributed-run termination detector (data-model.md:66-67, "reports quiescent (no runnable goals, no in-flight frames)"). The model assumes the oracle's *empty-channel* condition; the *no-runnable-goals* condition is outside the per-deref binding step modelled here (it belongs to the engine loop / scheduler, as in PI:14 §Scope). `drainN_quiescent` proves this predicate is *reachable* from any finite backlog, so the hypothesis is not vacuous. The convergence theorem is stated so that agreement holds *regardless* of quiescence (`dist_deref_agrees_pre_quiescence`); quiescence is invoked only to fix the settled value the instances converge to.

## Scope and assumptions

What the abstraction **captures**: the cross-instance deref hop (local + seam-crossing under one fuel measure), the loud termination verdict on both the acyclic (bounded, well-founded rank) and cyclic (FORK-1, no binding) paths, deferred-local-assignment (owner-only delivery), the `known/1` globalize/localize boundary, the GAP-G6 quiescence predicate and its reachability, and convergence of every reference terminating at a shared owner.

What it does **not** capture (honesty list):

1. **The concrete wire codec.** Byte-for-byte term/frame encoding (038 TLV + FrameCodec CRC32) is a separate parity concern (link-parity.md §Wire, and the exec-equivalence / codec spikes); this proof is at the term/binding-store seam (ED-1), not the byte seam.
2. **Path compression / suspension records.** As in PI:14, deref is modelled read-only; retargeting traversed reader cells and forwarding suspension lists mutate layout/wake-lists, never the binding domain, and are omitted.
3. **The scheduler / engine loop.** Goal queues, reduction budgets, and the *no-runnable-goals* half of the GAP-G6 oracle are outside the per-deref step; the theorem composes with, but does not re-prove, the scheduler.
4. **Writer-MGU on the wire.** *Which* value the owner binds (the unification that produces it) is PI:14's obligation (`WriterMguBindsOnlyWriters/`); here the owner cell's value is a given, and convergence is about all instances reading it consistently.
5. **Acyclicity certificate provenance.** Theorem 1's bounded-termination branch assumes a `rank` witness exists (the acyclic case). The model does not *decide* acyclicity at runtime; instead the cyclic case is handled by the complementary loud-`.stuck` result (`cyc_stuck`) — together they cover both dispositions the contract requires (terminate, or halt loudly with no binding). The engine's runtime cycle detection (visited-set / fuel, heap.gleam FR-003) is the operational counterpart of these two branches.
6. **N > 2 instances.** The carrier `Inst` is two-point (the seam under test), but every theorem except the concrete `cyc`/`demo` examples is stated over arbitrary `Node`s and an arbitrary `Config`, so the convergence and termination arguments are instance-count-agnostic; only the worked cross-seam-cycle and demo fix `A`/`B`.

No mismatch between the contract claim and the modelled semantics was found while constructing the model: cross-instance deref either reaches a genuine terminal (acyclic, bounded by a strictly-decreasing rank) or halts loudly with no binding (cyclic / fuel-exhausted), and any two references to the same owner writer converge — before quiescence on the unbound owner, after quiescence on the delivered value.

## Status

- **Lean:** green — `lake build` exit 0, zero `sorry`, zero `admit`, zero `axiom` cheat, zero warnings; `#print axioms` = `[propext, Quot.sound]` only (toolchain `leanprover/lean4:v4.30.0`, native Windows / Olamnit, 2026-07-27, T058 under 059 T083).
- **Prose:** this dossier.
- **Tests:** green — the adversarial suite `glp_gleam/test/glp/mad/dist_deref_convergence_adversarial_test.gleam` (14 tests, `gleam test` 601/0, native Windows / Olamnit, 2026-07-27) drives the REAL surface and mirrors each Lean theorem: acyclic chain → genuine terminal (`derefDist_terminates`); the FORK-1 cross-link cycle `w1→r2, w2→r1` (and a 3-hop cycle) → `Error(Cycle)`, binding nothing, plus writer-meets-writer → `Error(WriterToWriter)` (`cyc_stuck`); two handles to a shared owner agree before (both `Unbound`) and after (both the delivered value) delivery, with monotone re-deref and a loud `AlreadyBound` on a second delivery (`dist_deref_converges` / `dist_deref_agrees_pre_quiescence` + no lost/duplicated binding); two real `MadEngine`s routing a deferred-local-assignment through `mad_engine.receive` → owner-only `bind_and_wake`, q's reader converging to the delivered value (the `deliver`/`deliver_binds_owner` pipeline); the `known/1` globalize/localize `_w`/`_r` round-trip + W_p entries; the GAP-G6 `quiescence.decide` oracle (all-zero Quiescent, each non-zero cause named, drain-to-quiescent); and fault-mid-deref (a T075 `permFail` term surfaces through `deref` as ordinary bound data, never a fourth verdict). The Lean `example` block additionally pins the FORK-1-stuck, pre/post-quiescence agreement, and known/1 cases in-kernel.
- **INDEX flip:** **done** — the `../INDEX.md` row is flipped `open → proved` in the same checkpoint commit as the Lean artifact, this PROOF.md, and the green suite, traceable from `specs/050-full-gleam-combined/tasks.md` and feature 059 T083. **Faithfulness risk RISK-PROOF-distDeref RESOLVED** (Lean + prose + adversarial suite).
