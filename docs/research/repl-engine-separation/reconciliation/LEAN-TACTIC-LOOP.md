# Lean Tactic-Loop — bounded Claude-over-MCP proof loop for `engine-separation`

Architecture artifact of seed **#1a** (`iterative-refinement-and-verification-framework`),
specifying **slot #4** of the six formal-tooling slots in
[`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4 (the **Lean 4 prover** — primary; Rocq the
alternative per §3). Binding owner decisions: **R10, R11, R13** in
[`DECISIONS-LOG.md`](DECISIONS-LOG.md); tooling deferral **DEF-F-tooling** in
[`DEFERRALS.md`](DEFERRALS.md) (created by R10). Satisfies spec FR-030–FR-034
([`spec.md`](../../../../specs/027-refinement-verification-framework/spec.md));
environment resolved in
[`research.md`](../../../../specs/027-refinement-verification-framework/research.md) §1.

This artifact **specifies the loop architecture**. The runnable validation experiment that
empirically demonstrates it (FR-035) is the spike under
[`../spikes/lean/`](../spikes/lean/); its real installed versions are pinned in
[`../spikes/lean/tool-versions.txt`](../spikes/lean/tool-versions.txt).

---

## 1. The bounded tactic loop (FR-030)

A single proof obligation is discharged by a bounded loop in which **Claude is the tactic
driver** and the **Lean 4 kernel is the deterministic oracle** — reached over **Lean-LSP-MCP**.
No LM sits in the verification path; Claude only *proposes* tactics, the kernel *decides*.

```
obligation = one GLP property stated as a Lean 4 theorem (decidable inductive fact)
attempts   = 0
budget     = 20            # tuned starting value (FR-031); not a fixed constant
best       = ⊥             # best-so-far partial proof / proof state

loop:
  tactic       ← Claude proposes the next tactic (or tactic block) from the live goal state
  feedback     ← Lean kernel response over Lean-LSP-MCP:
                   { goals-remaining | type-error | proof-complete }
  attempts     += 1
  if feedback = proof-complete                  → terminate: PROVED
  best         ← update best-so-far from the new proof state
  reflection   ← Claude reads the unmet sub-goals + kernel diagnostics
  repair       ← lemma retrieval / repair:
                   - retrieve candidate lemmas (Mathlib / project lemmas) for the open goal
                   - repair the failing tactic from the kernel diagnostic
  if attempts ≥ budget                          → terminate: SORRY-ISOLATED (§4)
  repeat
```

The loop is the §1-refinement loop of [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md)
specialized to a prover obligation: *candidate → evaluate (kernel) → reflections → mutation
(next tactic) → repeat (budget-capped; capped run yields best-so-far)*. The **generate →
kernel-feedback → lemma-retrieval/repair → repeat** shape is exactly FR-030.

**Kernel feedback is the only source of truth.** A tactic is accepted iff the Lean kernel
accepts it; Claude never self-certifies a proof. This is what makes the driver
**model-agnostic** (§2): any model that can read a goal state and emit a tactic string drives
the identical loop, because correctness is adjudicated outside the model.

---

## 2. Claude as the model-agnostic tactic driver — no external LM API (FR-030)

**Hard rule (FR-073 / [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1).** The driver runs
**in Claude via the Agent-tool / MCP seam — never OpenAI, litellm, or `OPENAI_API_KEY`.**

- **Lean-LSP-MCP is Claude-native and model-agnostic** ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md)
  §3): it exposes the Lean LSP (goal state, diagnostics, `#check`/`#eval`, file edits) as MCP
  tools, so Claude drives the kernel **with zero adaptation** and the no-API rule holds for
  free. Companion tooling named in §3 — **APOLLO** (`sorry`-isolation, §4) and **Lean Copilot**
  — is likewise Claude-native.
- **Model-agnostic** means the *architecture* does not depend on which model emits the tactic:
  the kernel is the deterministic oracle, the model is interchangeable. Claude is the concrete
  driver here; the contract is "any tactic-emitting agent + the Lean kernel," not "Claude
  specifically."
- The MCP wiring is the launch endpoint pinned in
  [`../spikes/lean/tool-versions.txt`](../spikes/lean/tool-versions.txt):
  `wsl -d Ubuntu -- ~/.local/bin/lean-lsp-mcp` registered as an MCP server in Claude Code.
  The Python harness (FR-035) drives **Claude-over-MCP**, not a remote LM.

---

## 3. Attempt budget = 20, tuned; capped run yields best-so-far (FR-031)

**R13: attempt budget = 20 as a tuned starting point.** It is a **tuned experimental
variable**, not a fixed constant — iterated against real attempt counts during the validation
experiment (FR-035), not asserted from the desk.

- The loop **counts tactic attempts** (the `attempts` counter above), mirroring the
  `BudgetCounter(budget)` seam of the in-repo precedent
  `codeconv/src/codeconv/tools/codegen_opt/optimize.py:257-335`
  ([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1).
- **A capped run yields best-so-far.** On budget exhaustion the loop does not discard work: it
  returns the best partial proof state reached (`best`) and the open sub-goals, exactly as
  `run_optimize` returns `OptimizeResult(best_instructions=…)` even when capped (SC-002 /
  research.md §4). The unsolved residue is then `sorry`-isolated (§4).
- **Tuning protocol.** The validation experiment records `(outcome, attempt-count)` per run;
  20 is revised up or down from that empirical record. The number is owned by this artifact as a
  *starting* value (R13), reconsidered when real attempt data exists — never hard-coded as
  final.

---

## 4. `sorry`-isolation + owner-escalation on exhaustion (FR-031)

When the budget is exhausted with sub-goals still open, the loop **does not fail loudly and
does not fabricate a proof.** It applies **APOLLO-style `sorry`-isolation**, the
model-agnostic escalation path (FR-031 / research.md §1):

1. **Isolate.** The proved part of the theorem is retained; each unsolved sub-goal is closed
   with `sorry`, so the file still type-checks and the *proved* lemmas remain usable. The
   `sorry` precisely localizes the open obligation (which sub-goal, which hypotheses).
2. **Record.** The spike's `RESULT.md` (and FR-035 harness output) records the outcome as
   **`sorry`-isolated** together with the attempt-count — a valid, informative spike result, not
   a non-result. Per research.md §5, "a non-converging proof still records a valid spike
   outcome."
3. **Escalate to the owner.** The isolated `sorry` is surfaced as an **open obligation to the
   owner** — an escalation, not a silent skip. The owner decides: raise the budget, supply a
   lemma / proof sketch, restate the property, or carry the obligation forward to the gating
   seed (#4 / #11 / #12, §5 / FR-034).

This makes the loop **terminating and honest**: it always halts (PROVED or
SORRY-ISOLATED-and-escalated), and it never conflates "budget ran out" with "property false."

---

## 5. Lean 4 PRIMARY · Rocq the ALTERNATIVE · DEF-F-tooling pointer (FR-032, FR-034)

**Lean 4 is the epic-primary proof assistant** on all 11 prover-needing seeds
([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §3, §6). The decisive GLP properties are
*clean inductive facts over finite types* — round-trip identity, depth-bounded resolution,
unbound-sentinel correctness, suspension / monotone-binding invariants, fan-in stream-merge —
and **Lean-LSP-MCP + APOLLO + Lean Copilot are Claude-native** (no-API, zero adaptation). The
owner-stated preference is to evaluate Lean 4 first.

**Rocq is never the primary; it is the documented alternative** for **IL/bytecode obligations**
in the **Vellvm / TWAM lineage** (FR-032), genuinely needed only when a property crosses into
([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §3):

- **full verified-compiler bisimulation** (`decoded-execute ≡ direct-compile`) — seeds **#4,
  #11, #14** — TWAM (arXiv 1801.00471), the verified Prolog→WAM compiler (ScienceDirect
  0743106692900547), Vellvm are the Coq/Rocq prior art; or
- **coinductive reasoning over unbounded streams** — seed **#13** (non-terminating
  multi-client sessions) — where Rocq's `cofix` / `pcofix` infrastructure is more mature.

> **DEF-F-tooling pointer (FR-032).** If a later seed selects **Rocq**, **AutoRocq's GPT-4
> dependency MUST be adapted to Claude** — a no-API defect to fix, not a constraint to accept.
> This is the tooling deferral created by **R10** ([`DECISIONS-LOG.md`](DECISIONS-LOG.md)),
> tracked as **DEF-F-tooling** and triggered only on the full-bisimulation (#4/#11/#14) /
> coinductive-stream (#13) conditions above. Out of scope for #1a — recorded here as the pointer
> a Rocq-selecting seed must action **before** its `/buildkit-specify`.

**Full proofs are OFF the MVP critical path (R11, FR-034).** The *full* formal Lean/Rocq proof
suite gates only the **language-touching seeds #4, #11, #12** and starts at **DEF-B1** (Anchor
B, before #4). What runs **in #1a** is the **bounded validation spike** (FR-035 / R13): one GLP
property, real Lean 4, over MCP — a minimal feasibility spike that demonstrates the loop
architecture, **not** the production proof suite. R13 explicitly *extends* R11 / DEF-B1 / DEF-H1
this way: a real spike runs now; full proofs stay at #4/#11/#12.

---

## 6. Windows-11 setup path — WSL2/container, exercised not noted (R10 / FR-033)

**R10 / FR-033.** The Lean toolchain and Lean-LSP-MCP are **Linux/Mac-first**; the cwd is
`D:\` (Windows 11). A **WSL2/container setup path is documented for the `D:\` host and is
*exercised* by the validation experiment (FR-035)** — not left as an untested note.

**Primary path — WSL2 (Ubuntu) via `elan`/`lake`** (research.md §1). Real installed versions,
pinned in [`../spikes/lean/tool-versions.txt`](../spikes/lean/tool-versions.txt) and
**kernel-verified 2026-06-09** (`lean Smoke.lean` proved a trivial theorem + `#eval` ran,
`LEAN_COMPILE_OK`):

| Component | Version (real, installed) |
|---|---|
| host | WSL2 v2.6.3.0, Ubuntu 24.04 |
| `elan` (Lean version manager) | 4.2.3 (b6cec7e10 2026-06-08) |
| `lean` | 4.30.0 (x86_64-unknown-linux-gnu, commit d024af099, Release) |
| `lake` (build tool) | 5.0.0-src+d024af0 (Lean 4.30.0) |
| `lean-lsp-mcp` | 0.26.2 (pipx, Python 3.12.3) → `~/.local/bin/lean-lsp-mcp` |
| harness Python | 3.14.3 (`codeconv/.venv`) |

Toolchain installed at `~/.elan` (sudo-free, `--default-toolchain stable`). `elan` + the
project's `lean-toolchain` pin the version for free (FR-071 reproducibility, research.md §1).
MCP wiring: register `lean-lsp-mcp` as an MCP server in Claude Code so the harness drives
**Claude-over-MCP, no external API** — launch:
`wsl -d Ubuntu -- ~/.local/bin/lean-lsp-mcp`.

**Documented fallback — Docker Linux container** (research.md §1, §5): more hermetic (better
FR-071 reproducibility) but heavier to wire an MCP server out of; kept as the documented
fallback. **Native Windows Lean is rejected** — `elan` supports Windows, but Lean-LSP-MCP and
the broader tactic-tooling ecosystem are Linux/Mac-first (the exact reason R10 exists);
rejected to avoid an untested tooling path.

**The spike property.** One concrete GLP property, per research.md §1: **SRSW preservation on a
toy clause** — *each variable index occurs at most once per clause*, the cleanest "clean
inductive fact over finite types" — with **unification soundness / `decode∘encode = id` on a
toy term** held as the fallback property if SRSW proves awkward to state within the spike
budget. (A *distinct* shape from the MLIR spike's round-trip identity, so the two spikes
exercise different proof shapes.) A **docs-only completion does NOT satisfy R13** — the spike's
`RESULT.md` is the acceptance evidence (research.md §5).
