# Lean 4 tactic-loop spike — RESULT

**Status**: ✅ **PROVED** — recorded against a real Lean 4 toolchain in **T015**, 2026-06-10. This is
the US3 acceptance artifact (R13/R14, FR-035/071, SC-006/009) — the methodology's **highest-risk
formal claim** (a bounded Claude-driven tactic loop discharges a GLP property on real Lean). Desk
research does **not** satisfy this; an executed real-tool proof does.

## What was proved

`SRSWPreservation.lean` — `theorem rename_preserves_SRSW`: on a toy GLP clause (a flat list of
reader/writer variable occurrences with a constant-type relaxation flag), an **injective variable
renaming preserves SRSW-validity** (PROP-1, research §1). The committed source states the obligation
with `sorry`; the bounded loop discharged it.

## Outcome (FR-035, SC-006)

| | |
|---|---|
| **Outcome** | `proved` (Lean kernel: exit 0, **no `sorry`**, no error) |
| **Tactic attempts** | **5 / 20** (budget start 20, R13) |
| **`sorry`-isolation / escalation** | path implemented + budget-enforced; **not triggered** (closed under budget) |
| **Driver** | Claude via the Agent-tool seam (this session) — **no external LM API** (FR-073) |
| **Oracle** | the real `lean` 4.30.0 kernel (deterministic; kernel-equivalent to Lean-LSP-MCP) |

### The bounded loop (real kernel feedback drove each step)

| attempt | verdict | what the kernel feedback taught the driver |
|---|---|---|
| 1 | tactic-error | surfaced the goal `varSRSW (rename ρ c) w`; `simp` no-progress on the `beq` step |
| 2 | tactic-error | `eq_or_ne` is Mathlib, absent — the spike toolchain is **core Lean only** |
| 3 | still-sorry | the key counting lemma `maplen` (injective renaming preserves per-role counts) compiled; tail still `sorry` |
| 4 | tactic-error | `List.mem_map_of_mem` takes `f` implicitly; `w` needed defeq-normalising to `ρ o.var` for `hcount`'s rewrite |
| 5 | **proved** | `List.mem_map.mpr` + a defeq `show` closed it — kernel accepts, no sorry |

A mid-loop **lemma-retrieval** step (a scratch `lean` probe of the core `beq`/`Injective` API, not a
goal attempt) confirmed `beq_eq_false_iff_ne` and `Function.Injective.ne` are core — matching the
"generate tactic → kernel feedback → lemma retrieval/repair → repeat" loop of
[`../../reconciliation/LEAN-TACTIC-LOOP.md`](../../reconciliation/LEAN-TACTIC-LOOP.md).

## No LM on the verification path (FR-073)

Claude proposed the tactics (driver); the **Lean kernel decided** proved/failed (oracle). No external
LM API is reachable — the harness shells out only to `lean`. The no-API rule holds (grep-gated,
T010/T025).

## Reproduction

- Canonical (WSL2): `spikes/lean/run.sh` → resets the budget, drives one harness `attempt` with the
  discovered proof (`proof.lean`) → `proved`, then the un-budgeted `verify` gate. Exit 0.
- Windows wrapper: `spikes/lean/run.ps1` → forwards to `run.sh` via `wsl.exe -d Ubuntu`.
- Obligation: `spikes/lean/SRSWPreservation.lean` (theorem + `sorry` target).
- Discovered proof: `spikes/lean/proof.lean` (the loop's output, spliced by `harness.py`).
- Harness: `spikes/lean/harness.py` (splice → real `lean` → classify → budget/sorry-isolation).
- Tool versions: `spikes/lean/tool-versions.txt` — Lean 4.30.0, elan 4.2.3, lean-lsp-mcp 0.26.2, WSL2.

## Scope (minimal feasibility spike — FR-074)

ONE toy clause, ONE transformation, ONE theorem. The full GLP-semantics proofs (unification soundness,
suspension/monotone-binding invariants, etc.) stay at the prover-needing seeds (REFINEMENT-METHOD §3).

**Conclusion**: the "a bounded Claude-over-kernel Lean tactic loop can discharge a real GLP property,
under budget, with sorry-isolation as the fallback" claim is backed by a recorded real-Lean **proved**
run — not desk research.
