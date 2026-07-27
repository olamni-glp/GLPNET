<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T083 close-proofs-proof-dist-deref-convergence` (b3-c2-045)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-proofs-proof-dist-deref-convergence` (b3-c1-020) — PI:17 **DISCHARGED**
**Backing detail_ids**: `proof-dist-deref-convergence` (PI:17)
**Shared artifact**: the adversarial dist-deref suite (feature 050 T057) is also the **first half of the
`T066 close-distribution-engine-sessions` acceptance** (its second half — a two-engine goal/result
round-trip + engine-to-engine routing — is tracked separately under T066/T069).

## Acceptance (FINAL plan, line 603)

> Sorry-free Lean artifact for PI:17 plus the T057 suite green under gleam test, both indexed per
> contracts/proof-obligations.md.

All three artifacts of the PI:17 three-artifact form now land in one checkpoint commit, and the
`PROOFS/INDEX.md` row is flipped `open → proved`.

## What was delivered

| Artifact | State |
|---|---|
| **Lean proof** `glp_gleam/lean/DistDerefConvergence/DistDerefConvergence/Basic.lean` | sorry-free; `lake build` exit 0; `#print axioms` = `[propext, Quot.sound]` only (no `sorryAx`, no `Classical.choice`). Theorems: `derefDist_terminates` (bounded, well-founded rank), `cyc_stuck` (FORK-1 cross-seam cycle → loud `.stuck`, no binding), `dist_deref_converges` / `dist_deref_agrees_pre_quiescence` (shared-owner agreement before & after quiescence), `drainN_quiescent` (GAP-G6 reachability). |
| **Prose PROOF.md** `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/dist_deref_convergence/PROOF.md` | full model↔`heap.gleam`/`mad`/`quiescence` mapping + honesty list; Tests line now names the suite; INDEX-flip line marked done. |
| **Adversarial gleeunit suite** `glp_gleam/test/glp/mad/dist_deref_convergence_adversarial_test.gleam` | **NEW** — 14 tests driving the REAL surface (see below). |
| **INDEX flip** `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md` | distributed-deref row `open ⚠ RISK → proved`; outcome summary `proved: 4→5`, `open: 2→1`; **RISK-PROOF-distDeref RESOLVED**. |

## The adversarial suite — mirrors each Lean theorem on real code

The suite proves the *method on the real primitives that exist today* (per the INDEX scope-honesty
rule and the PI:14 precedent `writer_mgu_adversarial_test.gleam`), not a re-modelled surface. Each
group maps to a Lean theorem:

- **Theorem 1 (termination).** `acyclic_chain_reaches_value_terminal` + `..._to_unbound_owner_...` — a
  multi-hop `reader→writer→(link)→…` chain reaches a genuine value/unbound terminal (`derefDist_terminates`).
  **FORK-1 dual:** `fork1_cross_link_cycle_is_loud_and_binds_nothing` builds the real cross-link cycle
  `w1→r2, w2→r1` via `heap.bind_writer_to_var`; `heap.deref` trips its visited-set guard →
  `Error(Cycle(r1))` from both entry points, and **neither writer is a value cell** (binds nothing) —
  the operational `cyc_stuck`. `fork1_three_hop_cycle_is_loud` extends it to a 3-cycle;
  `writer_meets_writer_is_loud_no_binding` → `Error(WriterToWriter)`.
- **Theorem 2 (convergence).** `two_handles_converge_on_shared_owner` — two handles both linking to one
  owner deref-agree BEFORE binding (both `Unbound(owner)`) and AFTER (both the delivered value), value-equal
  (`derefDist_eq_of_terminal` / `dist_deref_agrees_pre_quiescence` / `dist_deref_converges`).
  `delivery_is_monotone_and_no_double_bind` — re-deref is stable and a second `bind_writer` on the owner
  → loud `Error(AlreadyBound)` (no lost/duplicated binding).
- **Distributed pipeline (two REAL `MadEngine`s).** `two_engine_deferred_assignment_converges` — p exports
  reader Xs? to q; p assigns Xs := [add]; the value crosses the seam via `mad_engine.receive` →
  `scheduler.bind_and_wake` (owner-only bind — the code counterpart of Lean `deliver`/`deliver_binds_owner`);
  q's reader is `Unbound` before delivery and converges to `[add]` after; a SECOND delivery of the same
  assignment is refused loudly.
- **`known/1` boundary.** `known1_global_name_term_round_trips` + `wp_table_globalize_localize_entries` —
  `_w(p,i)`/`_r(p,i)` term round-trip and the W_p GlobalizeEntry/LocalizeEntry store, with a loud refusal
  of a duplicate localize (globalize/localize on `known/1`).
- **GAP-G6 quiescence oracle.** `quiescence_oracle_all_zero_is_quiescent` +
  `..._names_the_active_cause` + `..._reached_by_draining_backlog` — the pure `quiescence.decide`
  oracle (the `drainN_quiescent` predicate): quiescent iff all three counts are zero, else `Active`
  naming the blocking cause.
- **fault-mid-deref (T075).** `fault_surfaces_through_deref_as_data` — a `link_terms.perm_fail(LinkId, Reason)`
  bound into a writer derefs to that `permFail` struct as ORDINARY DATA — surfaced loudly, never a fourth
  verdict, never a silent success.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Lean sorry-free + green | `cd glp_gleam/lean/DistDerefConvergence && lake build` (Lean v4.30.0) | exit 0, zero `sorry` |
| Suite + full Gleam suite green | `cd glp_gleam && gleam test` | **601 passed, no failures** (14 new dist-deref tests; +0 warnings) |
| INDEX row proved | `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md` | distributed-deref row = **proved**; RISK-PROOF-distDeref RESOLVED |

Note on suite run: an unrelated transient `test/glp/link/tcp_test.gleam` accept-timeout (the known T089
TCP single-persistent-socket accept-loop residual, ~1/6 runs) cleared on re-run; not a regression and
not in this WP's scope.

## Disposition

| detail_id | disposition |
|---|---|
| `proof-dist-deref-convergence` (PI:17) | **CONFIRMED-DELIVERED / DISCHARGED** — sorry-free Lean + prose PROOF.md + 14-test adversarial gleeunit driving the real heap-deref/mad/quiescence surface, all green; INDEX flipped `open → proved`; gates M2. |

**Close status: CLOSED — clean.** PI:17 discharged in the three-artifact form; the adversarial suite is
carried forward unchanged as the first half of the T066 acceptance.
