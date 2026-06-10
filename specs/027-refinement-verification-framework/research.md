# Phase 0 Research: Iterative Refinement & Verification Framework

**Feature**: `027-refinement-verification-framework` | **Date**: 2026-06-09

Resolves the `NEEDS CLARIFICATION` items in plan.md Technical Context. Each section follows
**Decision / Rationale / Alternatives considered**. The dominant research question is *environment*:
the spec (R13/R14) mandates **real installed tools**, and the host is Windows 11 (`D:\`). The secondary
question is *spike scope*: which single GLP property / IL fragment / handshake is minimal-but-sufficient.

---

## §1. Lean 4 toolchain + Lean-LSP-MCP on a Windows host  [ENV-1, PROP-1]

**Decision.** Install the Lean 4 toolchain inside **WSL2 (Ubuntu)** via `elan` (`lake` for the project),
and run **Lean-LSP-MCP** there, exposed to Claude as an MCP server. The Python harness runs in WSL2 (or on
Windows talking to the WSL2 MCP endpoint). The one concrete GLP property for the spike is **SRSW
preservation on a toy clause** — stated as a decidable inductive fact (each variable index occurs at most
once per clause), the cleanest "clean inductive fact over finite types" per REFINEMENT-METHOD §3, with
**unification soundness / `decode∘encode = id` on a toy term** held as the fallback property if SRSW proves
awkward to state in Lean within the spike budget.

**Rationale.**
- R10/FR-033 already ratify that Lean tooling is Linux/Mac-first and a **WSL2/container setup path** is
  required and *exercised* by this spike (not left as a note). WSL2 is already the standard Linux surface on
  this Windows box (CLAUDE.md references WSL2/Linux Dart paths).
- `elan` is the official Lean version manager; `lake` is the official build tool — the reproducible,
  pinnable install (FR-071 wants pinned versions, which `lean-toolchain` + `elan` give for free).
- **Lean-LSP-MCP is Claude-native and model-agnostic** (REFINEMENT-METHOD §3) — it satisfies the no-API
  rule (FR-073) with zero adaptation: Claude is the tactic driver over MCP; the Lean **kernel** is the
  deterministic oracle. APOLLO-style `sorry`-isolation is the model-agnostic escalation path (FR-031).
- SRSW is a first-class GLP invariant (CLAUDE.md "SRSW … Mandatory"), has an in-repo validator to mirror,
  and is small enough to state over a toy clause within a 20-attempt budget.

**Alternatives considered.**
- **Docker Linux container instead of WSL2** — viable and more hermetic (better for FR-071 reproducibility),
  but heavier to wire an MCP server out of; keep as the documented fallback in `LEAN-TACTIC-LOOP.md`.
- **Native Windows Lean** — `elan` has Windows support, but Lean-LSP-MCP and the broader tactic-tooling
  ecosystem are Linux/Mac-first (the exact reason R10 exists); rejected to avoid an untested tooling path.
- **Property = full unification soundness over the real heap** — too large for a *minimal* spike (FR-074);
  rejected in favor of a toy clause. **Property = depth-bounded resolution / round-trip identity** — also
  clean, but round-trip identity is already the MLIR spike's job; pick a distinct property (SRSW) so the two
  spikes exercise different proof shapes.

**Open dependency surfaced:** if a later seed selects **Rocq** for an IL/bytecode obligation, AutoRocq's
GPT-4 dependency must be adapted to Claude (DEF-F-tooling) — out of scope here, recorded as a pointer in
`LEAN-TACTIC-LOOP.md` (FR-032).

---

## §2. MLIR Python bindings on a Windows host + minimal IL fragment  [ENV-2, ILFRAG-1]

**Decision.** Acquire **MLIR Python bindings** via the most reproducible available route, in this
preference order: (1) a **pip-installable MLIR wheel** if one matches the host Python; (2) MLIR Python
bindings from a **prebuilt LLVM** (`-DMLIR_ENABLE_BINDINGS_PYTHON=ON`) inside **WSL2** if no wheel fits.
The minimal GLP IL fragment realizes the **four dialect primitives** `HEAD-unify`, `GUARD-test`,
`BODY-spawn`, `suspend-reactivate` over a **single tiny clause** (one head unify, one guard test, one body
spawn) — enough to demonstrate `decode(encode(p)) ≡ p` non-trivially while staying minimal (FR-074).

**Rationale.**
- FR-040 fixes the four primitives by name + GLP-semantics; the spike only needs *one* clause that touches
  each primitive once to make the round-trip non-trivial (FR-043 "at least one non-trivial fragment").
- The deterministic **round-trip oracle** `decode(encode(p)) ≡ p` is the pass/fail metric (FR-041); **Claude
  is restricted to structural generation** of the dialect ops, mitigating the U4 "LLMs struggle with IR
  control flow" risk. No LM is in the verification path → no-API rule trivially holds.
- WSL2 is the reliable fallback because MLIR's Python bindings are routinely built on Linux; building on
  native Windows is possible but fragile, so wheel-first then WSL2-build.

**Alternatives considered.**
- **`xdsl` (pure-Python MLIR-compatible IR) instead of real MLIR bindings** — far easier to install, but
  R13 demands the spike run against **real MLIR**; `xdsl` would be desk-equivalent for the "is MLIR the right
  substrate" claim. Rejected as primary; may be noted as a future ergonomic option, NOT as the spike tool.
- **Hand-rolled S-expression IR + round-trip** — proves round-trip but not *that MLIR* fits; rejected for the
  same reason (the claim under test is specifically MLIR-as-substrate).
- **Larger fragment (full opcode set)** — that is #4/#11's production job (DEF-B1/H1); rejected as over-scope.

**Open dependency surfaced:** the Typed-Multi-level-Datalog-IR **citation is mis-attributed** (`2502.06854`
is an LLM-comprehension-of-LLVM-IR study); candidate correct ref **LingoDB (VLDB 2022, Jungmair et al.)**.
Recorded as an open item anchored at #4/#12 (DEF-B2, FR-042) — MUST NOT block this feature.

---

## §3. SPIN install on Windows + minimal front↔back handshake  [ENV-3, HANDSHAKE-1]

**Decision.** Install **SPIN** (the `spin` model checker) — native Windows `spin.exe` + a C compiler
(`gcc` via MSYS2/MinGW, or `cl`) for the generated verifier `pan.c`, with **WSL2 `spin`** as the
documented fallback. Model a **minimal synchronous request/response handshake**: a `front` proctype sends
`request` then awaits `response`; a `back` proctype awaits `request` then sends `response`; over rendezvous
or a 1-slot channel. Check **deadlock-freedom** (no invalid end state), **no unspecified receptions**, and a
**progress/liveness** property (`request` is eventually followed by `response`) via an LTL claim + a
`progress` label.

**Rationale.**
- R14/FR-076 adopt SPIN as the REQUIRED pragmatic-tier default; FR-080 wants a **real-SPIN** spike on a
  **minimal handshake** (DEF-A3 explicitly keeps the *full* envelope/protocol model at #5/#6).
- SPIN's verifier is generated C (`spin -a` → `pan.c` → compile → run), so a C toolchain is the only real
  dependency; SPIN runs well both natively on Windows and under WSL2 — pick native first for host-locality,
  WSL2 as the no-friction fallback.
- A two-proctype request/response with a single in-flight message is the smallest model that can actually
  exhibit a deadlock or a lost-progress counterexample, so it genuinely tests the methodology (not a trivial
  always-true model).

**Alternatives considered.**
- **TLA+/TLC for the handshake instead of SPIN** — TLA+ is in the armoury (R15) but is the wrong default for
  a small explicit-state network handshake (it shines for consensus/multi-client, e.g. #13); SPIN is the
  ratified default. Rejected for the spike; documented as an armoury alternative.
- **Model the full envelope now** — over-scope; DEF-A3 defers it to #5/#6. Rejected.
- **Asynchronous (large) channel** — would balloon the state space without adding feasibility signal for a
  *minimal* spike; keep the channel depth at 0–1.

---

## §4. The refinement loop precedent mapping (no new research — confirmation)  [FR-011]

**Decision.** Map the framework's GEPA/DSPy loop one-to-one onto `optimize.py:257–335` (`run_optimize`):

| Framework seam (FR-010) | `run_optimize` element |
|---|---|
| candidate generator | `generate_fn` (Claude-backed, `_require_fn` raises if absent — no API default) |
| proposer (GEPA reflective mutation) | `propose_fn(best_instr, all_refl)` |
| evaluator (metric combination) | `score_instructions(...)` over the seed's metric table |
| hard budget cap | `BudgetCounter(budget)` + `while counter.used < counter.budget and rounds < max_rounds` |
| capped run → best-so-far | returns `OptimizeResult(best_instructions=best_instr, ...)` even when capped |

**Rationale.** Verified by reading the source (lines 257–335): the four seams exist, `_require_fn` enforces
no-API, and a capped run yields best-so-far (SC-002). No fabrication of a new loop; the framework *documents*
this precedent as the canonical shape.

**Alternatives considered.** None — FR-011 names this exact precedent; the task is to confirm, not choose.

---

## §5. Environment-risk summary + sequencing

| Tool | Primary path | Fallback | Risk | Mitigation |
|---|---|---|---|---|
| Lean 4 + Lean-LSP-MCP | WSL2 `elan`/`lake` + MCP server | Docker container | MCP↔Claude wiring | front-load as first implement task; APOLLO `sorry`-isolation means a non-converging proof still records a valid spike outcome |
| MLIR Python bindings | pip wheel | WSL2 LLVM build w/ `-DMLIR_ENABLE_BINDINGS_PYTHON=ON` | wheel/host mismatch | wheel-first; WSL2 build is the proven route; oracle is deterministic so result is unambiguous |
| SPIN | native `spin.exe` + MinGW `gcc` | WSL2 `spin` | C-toolchain on Windows | SPIN is small + well-documented; WSL2 fallback is friction-free |

**Sequencing implication for tasks.md / implement:** the three **environment setup tasks are the critical
path** and must precede their respective harness+run tasks. The docs artifacts (template, sketches, armoury)
are independent of the environment and can be authored in parallel. The spikes' `RESULT.md` files are the
acceptance evidence that closes SC-006/007/011/010 — a docs-only completion does NOT satisfy R13/R14.

**All NEEDS CLARIFICATION items resolved.** No open `NEEDS CLARIFICATION` remains for Phase 1.
