# Proof Harness — how to construct a proof for a faithfulness invariant (T002)

**Feature** `036-glp-gleam-baseline-program` · **Marathon run** `mrun-5611c436ba95` · **Authored** 2026-06-29 (T002, Phase A).

How to invoke the in-repo verification armoury for a **new** load-bearing invariant, and how to
record the outcome. P4 (T004–T005, FR-004) constructs a proof for each load-bearing invariant and
records `proved | refuted | open` — **never silently skipped**. This doc is the wiring; the armoury
itself was validated green in feature 027 and re-used here (no new external dependency, Claude-only).

> 🔴 **No language model on the verification path.** Claude *authors* the obligation / model /
> structural encoder and *drives* the tactic loop via the Agent-tool seam — but the **tool is the
> oracle** (the Lean kernel, SPIN, MLIR parser, or the real `glp_runtime` runner decides
> pass/fail). No external LM API is ever called (grep-gated). This is what makes an outcome
> *verified*, not asserted.

## Canonical run path: WSL2

The Lean/SPIN/MLIR spikes run under **WSL2 (Ubuntu 24.04)** — each has a `run.sh` (canonical) and a
`run.ps1` Windows wrapper that forwards via `wsl.exe -d Ubuntu`. The Dart exec-equivalence harness
runs **native Windows** (Dart SDK 3.10.1 at `C:/Users/gavri/dart-sdk/bin/dart.exe`).

## The four tools — pick by invariant kind

| Invariant kind | Tool | Location | Oracle |
|---|---|---|---|
| **Semantic** property of terms/clauses (SRSW preservation, writer-MGU soundness, unify truth-table, monotone binding) | **Lean 4** | `docs/research/repl-engine-separation/spikes/lean/` | real `lean` kernel (proved = exit 0, no `sorry`) |
| **Protocol / concurrency / linked-distribution** (front↔back handshake, M2 deref protocol, deadlock-freedom, progress) | **SPIN/Promela** | `…/spikes/spin/` | real `spin` model checker (`errors: 0`) |
| **IL round-trip / lowering** (encode→print→reparse→decode identity; IL-as-substrate) | **MLIR** | `…/spikes/mlir/` | real compiled-LLVM `mlir.ir` (`decode(encode(p))==p`) |
| **Runtime behavioural equivalence** (Suspend-not-Fail, reactivate+commit, byte-identical bytecode vs stock codegen) | **exec-equivalence** | `spike/p5-il-merge/lib/exec.dart` | real `glp_runtime` `BytecodeRunner` + `Scheduler` |

---

### 1. Lean 4 — semantic invariants

**Reproduce (canonical):** `bash docs/research/repl-engine-separation/spikes/lean/run.sh`
(Windows: `pwsh docs/research/repl-engine-separation/spikes/lean/run.ps1`).

**Mechanism** (`run.sh`): `python3 harness.py reset --budget 20` → `attempt --proof proof.lean` →
`verify --proof proof.lean`. The harness splices the discovered tactic block in place of a `sorry`
in the obligation `.lean` and checks the **real kernel** accepts it (no `sorry`, no error). A bounded
Claude-over-kernel tactic loop (generate tactic → kernel feedback → lemma retrieval → repeat, budget
20) finds the proof; **sorry-isolation** is the budget-exhaustion fallback (an honest `open`).

**To add an invariant:** write the obligation as a `theorem … := by sorry` in a new `.lean` (core
Lean only — Mathlib is absent), drive the bounded loop to discharge it, commit the discovered
`proof.lean`, record the outcome.

**Tool versions:** Lean 4.30.0, elan 4.2.3, lake 5.0.0 (WSL2 Ubuntu 24.04, sudo-free `~/.elan`).
Harness baseline Python 3.14.3 (`codeconv/.venv`). Precedent (`spikes/lean/RESULT.md`):
`rename_preserves_SRSW` **PROVED**, 5/20 attempts, no external LM.

### 2. SPIN/Promela — protocol & linked-distribution

**Reproduce (canonical):** `bash docs/research/repl-engine-separation/spikes/spin/run.sh`
(Windows: `…/spin/run.ps1`).

**Mechanism** (`run.sh`): two verifier runs on the `.pml` model — **(1) liveness** with the LTL
claim active + fairness (`spin -a` → `gcc -O2 -o pan pan.c` → `./pan -a -f`), asserting `errors: 0`;
**(2) safety/deadlock** with the `ltl` line stripped (an active never-claim disables invalid-end-state
detection) → invalid-end-states + assertions + `xs`/`xr` unspecified-reception checks, asserting
`errors: 0`. PASS = `errors: 0` on **both**; exit 0.

**To add an invariant:** model the protocol/distribution mechanism in a `.pml` with an LTL property,
run both passes, record. Keep the committed model claim-bearing; `run.sh` derives the safety variant
in a temp dir.

**Tool versions:** SPIN 6.5.1, gcc 13.3.0 (WSL2 Ubuntu 24.04, prebuilt `~/.local/bin/spin`).
Precedent (`spikes/spin/RESULT.md`): front↔back handshake **PASS** (deadlock-free + no unspecified
receptions + `request_eventually_answered`). Armoury menu:
`docs/research/repl-engine-separation/reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md`.

### 3. MLIR — IL round-trip

**Reproduce (canonical):** `bash docs/research/repl-engine-separation/spikes/mlir/run.sh`
(Windows: `…/mlir/run.ps1`). Requires the WSL venv at `~/mlir-spike/wheel-venv` (real compiled-LLVM
bindings; **Linux-only — no Windows cp314 wheel**).

**Mechanism** (`run.sh` → `harness.py`): encode the IL fragment into a real MLIR module (the four
GLP/FCP primitives as ops in an unregistered `glp` dialect) → print → re-parse in a fresh `Context`
→ decode → assert `decode(encode(p)) == p` (structural) and `str(m1)==str(m2)` (textual). Claude
authors the fixed structural encoder; the MLIR parser/printer is the deterministic oracle.

**Tool versions:** `mlir-python-bindings 22.0.0.2025112901` (makslevental find-links,
`https://makslevental.github.io/wheels`), Python 3.12.3 (WSL2). Precedent (`spikes/mlir/RESULT.md`):
ILFRAG-1 (`p(X,Y?) :- ground(X?) | q(Y).`, four primitives once each) round-trip **PASS**. The four
primitives + op-verifier obligations: `…/reconciliation/MLIR-GLP-DIALECT.md:22-31,46-48`.

### 4. exec-equivalence — real-runner behavioural parity

**Reproduce (native Windows):**
```
cd spike/p5-il-merge
C:/Users/gavri/dart-sdk/bin/dart.exe pub get
C:/Users/gavri/dart-sdk/bin/dart.exe run bin/phase_a.dart    # core path (no ANTLR)
C:/Users/gavri/dart-sdk/bin/dart.exe run bin/phase_b.dart    # ANTLR path
```
**Mechanism:** `lib/exec.dart` runs a goal on the real `glp_runtime` `BytecodeRunner` + `Scheduler`
(mirroring `GlpEngine._runSingleGoal`) for both the stock-codegen and IL-derived programs and diffs
the observable behaviour — **Suspend (not Fail)** on an unbound reader, then **reactivate + commit**
(head-constructed output) on bind. `lib/lowering.dart`'s field-level disassembler additionally
asserts the two bytecode programs are **byte-identical**.

**Tool versions:** Dart SDK 3.10.1, ANTLR 4.13.2 (antlr4-tools, Java 17), `antlr4` pub 4.13.2.
Precedent (`spike/p5-il-merge/SPIKE-RESULT.md`): `merge/3` clause 1 byte-identical + execution-equivalent
+ verifiers fire on real SRSW/phase-order violations — **PASS** (ED-5).

---

## Recording an outcome (data-model `Proof Artifact`)

Each constructed proof is recorded under `pipelines/P4-faithfulness/PROOFS/` with the fields from
`data-model.md` — `invariant`, `tool` ∈ {Lean, SPIN, MLIR, exec-equivalence}, `outcome` ∈
{proved, refuted, open}, `path`, `reproduce_cmd`. A **refuted or unprovable** invariant is surfaced
as a first-class **faithfulness risk** affecting the relevant feature (FR-004), never omitted.

## Scope honesty

Each spike is a **minimal feasibility** artifact (one clause / one theorem / one fragment / one
handshake). The full GLP-semantics proof suite (unification soundness, suspension/monotone-binding,
the complete wire-protocol model, the full opcode lowering) is the work P4 schedules — these spikes
prove the *method* works on the real tools, which is the bar FR-004 sets for "verified, not asserted".
