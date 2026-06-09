# Seed Reconciliation Brief — authoritative spec for the per-seed sub-agent pass

**Status:** READY pending owner **GO**. The IL-verification layer + formal/pragmatic tooling were delivered by the owner 2026-06-09 (see §3). Launch the 16-agent reconciliation on the owner's word.
**Date:** 2026-06-09 · **Feature:** `026-engine-review-dossier` (post-approval reconciliation work) · **Epic:** engine-separation.

Single durable capture of everything each per-seed sub-agent must produce when reconciling a captured roadmap seed against the design dossier (`../design-dossier.md`). Exists so no streamed requirement is lost and the workflow has a stable input.

---

## 0. The seeds in scope
15 decomposed successors (dossier §11 #2–#16) + 1 pre-decomposition **monolith** (`repl-engine-split-mvp-binary-wire-format-intermediate-language-c`, the supersession case) + the new early-stage **#1a `iterative-refinement-and-verification-framework`** (the methodology feature — **this brief is its de-facto spec**; its reconciliation memo focuses on what the framework itself must deliver). All roadmap state `captured`. Feature #1 (this dossier) excluded. **Total reconciliation units: 17.**

## 1. PART A — reconciliation (per seed)
Read the dossier §refs for the seed; fetch its stored profile (`buildkit-roadmap brief <id>`, read-only); do additional **as-built code checks** (out/csharp, csharp/glp_link, codeconv, glp_runtime, programs/self.glp) confirming/extending dossier claims with current `file:line`; record **dossier cross-references** (§-anchors); surface **tensions/contradictions** (seed vs dossier vs code, each evidence + 2–3 owner options) and **under-specifications** (each why-it-matters + options); give a **recommendation** + **owner options** + **open questions**. Read-only w.r.t. code; only the seed memo is written.

## 2. PART B — GEPA / DSPy / GEPA-DSPy iterative refinement (per seed)
Most seeds need iterative refinement: well-defined seed → iterate until the implementation fully fulfils its **function in the architecture** AND fits the **roadmap sequence** (deps satisfied; unblocks dependents).
- **HARD PROJECT RULE:** GEPA/DSPy + any LLM-in-the-loop verification runs **in Claude via Agent-tool seams / MCP — never OpenAI/litellm/OPENAI_API_KEY**. Any "needs an API" line is a defect to delete. In-repo precedent: the `codeconv-codegen-opt` skill is an offline GEPA/DSPy optimizer (GEPA = reflective Pareto program evolution; DSPy = framework; `dspy.GEPA` = optimizer).
- **Applicability** stated honestly per seed: `direct` (an LM/codegen program GEPA/DSPy literally optimizes), `methodological` (systems/C# code, GEPA/DSPy as iterate-against-a-metric discipline), `low` (poor fit — explain; surface as tension).
- **Loop:** seed → candidate → evaluate against the metric combination → GEPA reflective mutation / DSPy compile-time optimization → repeat; terminate when metric thresholds + roadmap-sequence fit hold. Claude-run, no external API.

## 3. PART C — METRICS (a combination per step, defined in an interactive spec step)
Each seed's **specification** defines the **metrics approach** — a *combination* signalling when a refinement step has reached "the right level." It **must blend pragmatic + formal**; the exact combination + tools are **settled interactively with the owner at the start of `/buildkit-specify`**.

### 3.1 Pragmatic metrics (does it actually work)
REPL suite (`test/run_all_tests.sh`); execution-equivalence corpora; round-trip identity harness; behavioral/play scenarios; cross-process loopback equivalence (split result ≡ in-process result); kill-and-restart correctness; perf/footprint budgets.

### 3.2 Formal metrics (provable criteria) — MANDATORY wherever the seed touches the language or a wire/byte contract
Three subjects: the **GLP language**, its **implementation**, the **intermediate language (IL)**.

- **Front-end / grammar (the pragmatic↔formal bridge):** define the GLP grammar **once in ANTLR4**; use the generated parser as an **example-coverage verifier** — parse *every* working-definition example to prove the grammar accepts the language **before any compiler exists**. (Single-grammar also seeds successor #12.)
- **Type/mode discipline:** type-checker (well-typed-clause), **SRSW** validity, mode correctness — in-repo, usable as formal gates today.
- **Mechanized semantics (strategic extension on Shapiro/Udi):** mechanize GLP's operational semantics (three-phase HEAD/GUARD/BODY, three-valued unification, suspension/reactivation) in a proof assistant so "meets our design criteria for the language" becomes a **formal** criterion. Start with one decisive property (SRSW preservation, or unification soundness). Tool choice = owner option (§3.2a).
- **IL verification (GLP bytecode + future wire-IL):**
  - **Verified-IL precedent for logic languages:** **TWAM — certifying abstract machine for logic programs** ([1801.00471](https://arxiv.org/pdf/1801.00471)); classic **verified Prolog→WAM compiler** (compiled-exec ≡ source-interp) ([ScienceDirect](https://www.sciencedirect.com/science/article/pii/0743106692900547)). GLP bytecode is WAM-lineage → direct models for "the IL means what the source means." General verified-IR template: **Vellvm** (Coq).
  - **Higher-level IR = MLIR (owner-specified, 2026-06-09):** instead of jumping declarative-logic → assembly-like LLVM IR, use **MLIR multi-level IR** with a **logic-native dialect**. Precedent the owner cites: a **Typed Multi-level Datalog IR** capturing relational-algebra primitives (joins, projections, **fixed-point/recursion**) as IR blocks; high-level passes done *natively in the logic dialect* (magic-set transforms, relation-index selection, loop-invariant relation-code motion); then **progressive lowering**, desugaring the dialect step-by-step into imperative loops/arrays/hash-tables. For GLP this maps to a **GLP/FCP dialect** whose primitives are HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate, lowered progressively toward the runtime. [First-Class **Verification** Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf) makes semantics first-class so the dialect is *verifiable*. ⚠ **Citation note:** the owner-supplied link `arxiv.org/html/2502.06854v1` is actually an *empirical study of LLM comprehension of LLVM IR* (not the Typed-Datalog-IR paper) — and it warns **LLMs struggle with IR control flow**, a real risk for a Claude-driven IL codec (#4). The MLIR-Typed-Datalog-IR concept stands; the exact citation is to be pinned during the IL spike.
  - **Byte-contract formal metric:** byte-parity proofs (FR-060/061) + round-trip identity (`decode(encode(p)) ≡ p`) + schema conformance + self-containment / no-heap-leak invariants.

### 3.2a Formal-verification tooling matrix (owner-supplied, 2026-06-09) — and the NO-API resolution
The verified environment prevents AI agents from hallucinating logical steps: treat proof as a *search space with immediate compiler feedback*.

| Class | Tools | Use in this epic |
|---|---|---|
| **Interactive theorem provers (ITP)** | **Lean 4** (+ mathlib; owner-preferred) · **Rocq/Coq** (compiler/critical-software prior art) · Isabelle/HOL | mechanized GLP semantics + IL ⇔ source correctness; verified-compiler-style proofs |
| **Lean agentic connectors** | **Lean-LSP-MCP** (Lean over MCP — Claude-native), **Lean Copilot** (LLM inference inside Lean via FFI: `suggest_tactics`/`select_premises`), **APOLLO** (isolates `sorry` sub-goals, repairs), **Copra** (in-context proving) | the Claude-run tactic loop: generate tactic → Lean/Rocq feedback → retrieve lemmas → repair |
| **Rocq agentic** | **AutoRocq** (iterative LLM↔Rocq tactic loop, verified certificate) | Rocq-side autonomous proof — **but adapt off its GPT-4 dependency (see below)** |
| **ATP / SMT** | **Z3 / CVC5** (SMT: bounds, consistency, SAT/UNSAT), **Vampire / E** (first-order "hammers") | offload sub-goals (e.g. guard-consistency, schema constraints) deterministically |
| **Dynamic / compute** | Python (SymPy/NumPy) | numeric/symbolic ground truth where a property is computational |

**NO-API resolution (a tension surfaced + resolved):** AutoRocq and similar are often demoed on **GPT-4 API**, which violates this project's no-API/Claude-only rule (cost + the rule). **Resolution:** the agentic-ITP loop is **model-agnostic** — APOLLO is explicitly "modular, model-agnostic" (ran with o3-mini/o4-mini/Goedel-Prover); Lean-LSP-MCP and Lean Copilot are model-neutral. So adopt the *architecture* (Lean/Rocq as the verified search space with compiler feedback) and **drive tactic generation with Claude via Agent-tool seams / MCP**, not a fixed API. AutoRocq's GPT-4 reliance is the defect to adapt away, not a constraint to accept. This is itself a per-seed decision where formal verification is in the metric set.

### 3.3 Per-metric record (each seed memo, as a table)
Each metric: **name · kind (pragmatic|formal) · concrete tool/harness · threshold (the "right level")**.

### 3.4 Interactive spec step (per seed)
At the start of each seed's `/buildkit-specify`, the owner confirms **which metric combination + which verification tools** (pragmatic+formal mix; the proof-assistant choice for language-touching seeds; the MLIR-dialect/IL-verification layer for IL-touching seeds). The sub-agent proposes; the owner decides.

### 3.5 Pragmatic anchor — adapt Shapiro's original GLP design criteria to the embedded-switch purpose (owner-specified)
Pragmatic verification, step by step per epoch, must produce **informed, precise criteria that we meet the original GLP design criteria set down by Ehud Shapiro (Grassroots)** — *adapted* to this system's purpose: an **embedded grassroots logic engine acting as a SWITCH** for (a) **connectivity to the outside world** and (b) **internal actions in the operating system**, hosting:
- **hierarchical-state-machine actors** — **QHSM** (Quantum/Samek-style HSM) and **HSM** hierarchical state machines as the actor model;
- **classical OS tasks** in the wider operating system.

So each seed's pragmatic criteria are framed as: *does this step preserve the Shapiro/GLP semantic guarantees (committed-choice concurrency, SRSW, suspension correctness, monotone variable binding) **while** advancing the embedded-switch role* — i.e. correct routing between external connectivity and internal OS/actor (QHSM/HSM) actions. The per-seed memo states which Shapiro criteria its step must preserve and how the pragmatic harness checks them.

## 4. PART D — dossier cross-references (traceability)
Each memo records the exact dossier **§-anchors** it maps to. The dossier carries the inverse map in-situ (§1–§9 markers) + **Appendix B — Successor Seed Registry**. Two-way traceability seed ↔ dossier.

## 5. Outputs
Per seed: memo `./<num>-<id>.md` (sections: Title; Dossier cross-references; Seed-vs-dossier-vs-code; Classification check; Tensions; Under-specifications; **GEPA/DSPy refinement** [Applicability; Seed definition; Metrics combination table incl. the chosen formal tool(s); Interactive spec step; Refinement loop]; **Shapiro-criteria the step preserves**; Recommendation; Owner options; Open questions; External refs). Synthesis: `./README.md` (index), `./DECISIONS-FOR-OWNER.md` (owner decisions + verification/metrics plan + the no-API/tooling decisions), `./REFINEMENT-METHOD.md` (shared methodology).

## 6. Requirement status
- [x] Owner supplied the higher-level IL-verification layer (MLIR multi-level logic dialect + progressive lowering; §3.2) — 2026-06-09.
- [x] Owner supplied the formal-tooling matrix + the no-API resolution (§3.2a) and the Shapiro/embedded-switch pragmatic anchor (§3.5) — 2026-06-09.
- [ ] Pin the correct citation for the Typed-Multi-level-Datalog-IR (the `2502.06854` link was mis-attributed; do during the #4/#12 spike).
- [ ] Owner **GO** to launch the 16-agent reconciliation.

---

### Sources
- [First-Class Verification Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf)
- [TWAM: A Certifying Abstract Machine for Logic Programs](https://arxiv.org/pdf/1801.00471) · [Verified Prolog→WAM compiler](https://www.sciencedirect.com/science/article/pii/0743106692900547)
- [APOLLO — model-agnostic agentic Lean proving (2505.05758)](https://arxiv.org/abs/2505.05758) · [LLM comprehension of LLVM IR (2502.06854) — the mis-attributed link, kept for its "LLMs struggle with IR control flow" finding](https://arxiv.org/html/2502.06854v1)
- [AutoRocq — autonomous Rocq proof agent](https://github.com/NUS-Program-Verification/AutoRocq) (adapt off its GPT-4 API dependency) · [SynVer (C / VST)](https://arxiv.org/html/2410.14835v2)
- [Lean 4](https://lean-lang.org/papers/lean4.pdf) · Lean-LSP-MCP / Lean Copilot / Copra · Vellvm (Coq) · Iris/Trillium (concurrent/distributed separation logic)
- Owner exploration links (to mine during spikes): `share.google/aimode/BMXNyJwRDQMcCyyrk`, `share.google/aimode/9AYccXYjLQz3cGXEW`, `arxiv 2601.14027`, `arxiv 2505.05758v5`.
