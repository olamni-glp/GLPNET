# Contract: Proof obligations (FR-017, SC-006)

Two OPEN obligations from the P4 faithfulness register (`docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md`, `PROOF-HARNESS.md`). Discharge form fixed by owner clarification 2026-07-10: **Lean (mechanized) + prose (dossier) + targeted adversarial tests** — for each.

## PI:14 — writer-MGU under value-copy semantics (gates M1)

- **Claim**: the Gleam engine's unification binds only writers — never readers, never writer↔writer — under the immutable-heap/value-copy model, for all three-phase execution paths (tentative HEAD unification included).
- **Lean**: `glp_gleam/lean/WriterMguBindsOnlyWriters/` (Lake project; repo convention per `csharp/glp_result_codec/lean/ResultTermRoundTrip/`). Model: heap+binding-step semantics abstracted from `glp/runtime/{heap,unify}.gleam`; theorem: binding-step preserves the writer-only invariant.
- **Prose**: `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/writer_mgu_binds_only_writers/PROOF.md`, INDEX status OPEN → discharged with links.
- **Tests**: adversarial gleeunit suite driving `unify` with reader/reader, writer/writer, nested-structure, and tentative-HEAD cases asserting the invariant (and asserting rejection paths).
- **Gate**: M1 may not be declared (corpus parity notwithstanding) until all three artifacts exist and pass.

## PI:17 — distributed-dereference convergence (gates M2)

- **Claim**: deref chains crossing instance boundaries terminate and converge to the same binding on all participating instances (deferred-local-assignment; globalize/localize on `known/1`).
- **Lean**: `glp_gleam/lean/DistDerefConvergence/` — model: two-instance binding stores + message-mediated assignment; theorem: no infinite deref chain; eventual agreement on resolved terms.
- **Prose**: `.../PROOFS/dist_deref_convergence/PROOF.md`, INDEX flip as above.
- **Tests**: adversarial link-layer suite — circular cross-instance references (FORK-1 shapes), interleaved bind/deref races, fault-mid-deref.
- **Recorded deviation**: P4 originally planned SPIN for this obligation; Lean is owner-directed (2026-07-10). The SPIN precedent (`docs/research/repl-engine-separation/spikes/spin/`) may be retained as supplementary, non-gating evidence.
- **Gate**: M2 / distributed acceptance may not be declared until discharged; the GAP-G6 quiescence oracle must exist before distributed verdicts are judged.

## Bookkeeping

- Each discharge is recorded as: Lean project green (`lake build` exit 0), prose PROOF.md merged, test suite green, INDEX row updated — all four in one checkpointed commit, traceable from tasks.md.
