<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-proofs-proof-dist-deref-convergence` (b3-c1-020)

**Feature**: 059 · **Wave**: 2 (verify) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27 · **Backing detail_ids**: `proof-dist-deref-convergence` (PI:17), `proof-writer-mgu-value-copy` (PI:14)

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Obligations spec | `specs/050-full-gleam-combined/contracts/proof-obligations.md:5-20` | PI:14 (gates M1) + PI:17 (gates M2) defined |
| PI:14 Lean sorry-free | `rg -w 'sorry\|admit' glp_gleam/lean/WriterMguBindsOnlyWriters` | **0** |
| PI:14 Lean builds | `cd glp_gleam/lean/WriterMguBindsOnlyWriters && lake build` (Lean 4.30.0) | **Build completed successfully** (rc=0) |
| PI:14 prose + suite | `.../PROOFS/writer_mgu_binds_only_writers/PROOF.md`; adversarial gleeunit in `gleam test` | present + green (508/0) |
| PI:17 Lean state | `glp_gleam/lean/DistDerefConvergence/DistDerefConvergence/Basic.lean:1` | **scaffold** — "PI:17 model + theorem land in T058" |
| PI:17 tasks state | `specs/050-full-gleam-combined/tasks.md:144-145` (T057, T058) | both **unchecked** `- [ ]` |

## Verdict

| detail_id | verdict | basis |
|---|---|---|
| `proof-writer-mgu-value-copy` (PI:14) | **DELIVERED / DISCHARGED** | sorry-free Lean, `lake build` green, prose PROOF.md, adversarial suite green. Gates M1 (LOCKED). |
| `proof-dist-deref-convergence` (PI:17) | **ABSENT / UNDISCHARGED** — expected | Lean is a scaffold; T057 (adversarial dist-deref suite) + T058 (Lean proof + prose + INDEX flip) unchecked. Gates M2 (deferred). |

**Overall: DELIVERED-as-verify** — PI:14 confirmed discharged-and-green; PI:17 confirmed undischarged.

## Scope this surfaces (feeds the close WP)

PI:17 discharge = the M2 distributed-acceptance gate. Its close WP must, per the owner-directed form
(Lean, **not** SPIN — 2026-07-10; SPIN precedent retained as supplementary/non-gating): (a) author the
adversarial dist-deref gleeunit suite (FORK-1 circular cross-instance shapes, interleaved bind/deref
races, fault-mid-deref); (b) complete the `DistDerefConvergence` Lean model + theorem (no infinite deref
chain; eventual agreement); (c) prose PROOF.md + INDEX flip. The GAP-G6 quiescence oracle (see
`verify-transports-…`, ABSENT) must exist before distributed verdicts are judged.
