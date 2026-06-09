# Seed Reconciliation Brief — authoritative spec for the per-seed sub-agent pass

**Status:** ACCUMULATING (owner is still streaming requirements — last open item: the higher-level IL-verification layer, owner to supply). **Do not launch the 16-agent reconciliation workflow until the owner confirms this brief is complete.**
**Date:** 2026-06-09 · **Feature:** `026-engine-review-dossier` (post-approval reconciliation work) · **Epic:** engine-separation.

This brief is the single durable capture of everything each per-seed sub-agent must produce when reconciling a captured roadmap seed against the design dossier (`../design-dossier.md`). It exists so no streamed requirement is lost and so the workflow has a stable input.

---

## 0. The seeds in scope

15 decomposed successors (dossier §11 #2–#16) + 1 pre-decomposition **monolith** (`repl-engine-split-mvp-binary-wire-format-intermediate-language-c`, the supersession case). All are roadmap state `captured`. Feature #1 (this dossier) is excluded.

## 1. PART A — reconciliation (per seed)

Each sub-agent: read the dossier §refs for its seed; fetch the seed's stored profile (`buildkit-roadmap brief <id>`, read-only); do additional **as-built code checks** (out/csharp, csharp/glp_link, codeconv, glp_runtime, programs/self.glp) confirming/extending dossier claims with current `file:line`; record the **dossier cross-references** (the §-anchors the seed maps to); surface **tensions/contradictions** (seed vs dossier vs code, each with evidence + 2–3 owner options) and **under-specifications** (open questions, each with why-it-matters + options); give a **recommendation** + **owner options** + **open questions**. Read-only w.r.t. code; the only write is the seed's memo.

## 2. PART B — GEPA / DSPy / GEPA-DSPy iterative-refinement approach (per seed)

Most seeds need iterative refinement: a well-defined seed → iterate until the implementation fully fulfils its **function in the architecture** AND fits the **roadmap sequence** (deps satisfied; unblocks dependents).

- **HARD PROJECT RULE:** GEPA/DSPy LM work runs **in Claude via Agent-tool seams — never OpenAI/litellm/OPENAI_API_KEY**. Any "needs an API" line is a defect to delete, not a constraint. In-repo precedent: the `codeconv-codegen-opt` skill is an **offline GEPA/DSPy optimizer** for the codegen prompt — the template. (GEPA = reflective Pareto program evolution; DSPy = the framework; `dspy.GEPA` = the optimizer.)
- **Applicability** must be stated honestly per seed: `direct` (an LM/codegen program GEPA/DSPy literally optimizes — e.g. codec-from-spec generation, grammar synthesis), `methodological` (systems/C# code where GEPA/DSPy is the iterate-against-a-metric discipline), or `low` (poor fit — explain; surface as a tension). No pretending literal LM optimization where the artifact is hand-written systems code.
- **Refinement loop:** seed → candidate → evaluate against the metric combination → GEPA reflective mutation / DSPy compile-time optimization → repeat, terminating when metric thresholds **and** roadmap-sequence fit hold. Claude-run, no external API.

## 3. PART C — METRICS (the core: a combination per refinement step, defined in an interactive initial spec step)

Per the owner: each seed's **specification** must define the **approach for the metrics** — a *combination* that tells each refinement step when it has reached "the right level." The combination **must blend pragmatic + formal**, and the exact combination + tools are **settled interactively with the owner at the start of `/buildkit-specify`** (before refinement begins).

### 3.1 Pragmatic metrics (does it actually work)
REPL test suite (`test/run_all_tests.sh`); execution-equivalence corpora; round-trip identity harness (encode→decode→execute-equivalent); behavioral/play scenarios; cross-process loopback equivalence (split result ≡ in-process result); kill-and-restart correctness; perf/footprint budgets.

### 3.2 Formal metrics (provable criteria) — MANDATORY wherever the seed touches the language or a wire/byte contract
Applies to **three subjects**: the **GLP language**, its **implementation**, and the **intermediate language (IL)**.

- **Front-end / grammar (pragmatic-formal bridge):** define the GLP grammar **once in ANTLR4** and use the generated parser as an **example-coverage verifier** — parse *every* working-definition example to prove the grammar fully accepts the language, **even before a compiler exists**. There is a clear working definition of the language to verify against.
- **Type/mode discipline:** the type-checker (well-typed-clause), **SRSW** validity, mode correctness — already in-repo, usable as formal gates.
- **Mechanized semantics (strategic extension, build on Shapiro/Udi):** mechanize GLP's operational semantics (three-phase HEAD/GUARD/BODY, three-valued unification, suspension/reactivation) in a proof assistant so "meeting our design criteria for the language" becomes a **formal** criterion. Tool exploration required — candidates:
  - **Lean 4** (owner-preferred; modern; strong type system) — [lean4.pdf](https://lean-lang.org/papers/lean4.pdf).
  - **Coq/Rocq** (most PL prior art: Vellvm verified-IR, CompCert verified compilation; Iris/Trillium concurrent & distributed separation logic — the right substrate for GLP's FCP-lineage concurrency).
  - Decision is an **owner option** (see §3.4); start by reproducing a small, decisive property (e.g. SRSW preservation, or unification soundness) in the chosen tool.
- **IL verification (the GLP bytecode + future wire-IL):**
  - **Verified-IL precedent for logic languages:** **TWAM — a certifying abstract machine for logic programs** ([arXiv 1801.00471](https://arxiv.org/pdf/1801.00471)); the classic **verified Prolog→WAM compiler** proving compiled-execution ≡ source-interpretation ([ScienceDirect](https://www.sciencedirect.com/science/article/pii/0743106692900547)). GLP's bytecode is WAM-lineage, so these are the direct models for **"the IL means what the source means."**
  - **The modern LLVM-like, higher-level IR layer = MLIR** (LLVM's multi-level, dialect-based IR). Recent work makes **semantics/verification first-class** in MLIR dialects ([First-Class Verification Dialects for MLIR, PLDI'25](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf)) — i.e. the GLP IL could be an MLIR **dialect with a verifiable semantics**, the "higher level for IL verification." Template for verified IR semantics generally: **Vellvm** (mechanized LLVM IR in Coq).
  - **Byte-contract formal metric:** byte-parity proofs (FR-060/061) + round-trip identity (`decode(encode(p)) ≡ p`) + schema conformance + self-containment/no-heap-leak invariants.
  - **⚠ PENDING owner input:** the owner is supplying additional detail on the *higher-level IL-verification layer* in a forthcoming message. **Hold the workflow for it.**

### 3.3 Per-metric record (each seed memo, as a table)
For each metric: **name · kind (pragmatic|formal) · concrete tool/harness · threshold (the "right level")**.

### 3.4 Interactive spec step (per seed)
At the start of each seed's `/buildkit-specify`, the owner confirms **which metric combination + which verification tools** are adopted (the pragmatic+formal mix, the proof-assistant choice for language-touching seeds, the IL-verification layer for IL-touching seeds). The sub-agent proposes; the owner decides.

## 4. PART D — dossier cross-references (traceability)
Each seed memo records the exact dossier **§-anchors** it maps to. The dossier itself carries the inverse map in-situ (§1–§9 markers) and an **Appendix B — Successor Seed Registry**. Together these give two-way traceability seed ↔ dossier.

## 5. Outputs
Per seed: a memo `./<num>-<id>.md` (sections: Title; Dossier cross-references; Seed-vs-dossier-vs-code; Classification check; Tensions; Under-specifications; **GEPA/DSPy refinement** [Applicability; Seed definition; Metrics combination table; Interactive spec step; Refinement loop]; Recommendation; Owner options; Open questions; External refs). Synthesis: `./README.md` (index), `./DECISIONS-FOR-OWNER.md` (owner-facing decisions + verification/metrics plan), `./REFINEMENT-METHOD.md` (the shared methodology).

## 6. Open requirement (blocking launch)
- [ ] Owner to supply the **higher-level IL-verification layer** detail (forthcoming). Until then the metrics §3.2 IL row is provisional (MLIR + TWAM/Vellvm as researched candidates).

---

### Sources (this brief's research)
- [First-Class Verification Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf)
- [TWAM: A Certifying Abstract Machine for Logic Programs](https://arxiv.org/pdf/1801.00471)
- [A verified Prolog compiler for the WAM (ScienceDirect)](https://www.sciencedirect.com/science/article/pii/0743106692900547)
- [The Lean 4 Theorem Prover and Programming Language](https://lean-lang.org/papers/lean4.pdf)
- Vellvm (mechanized LLVM IR semantics in Coq); Iris/Trillium (concurrent/distributed separation logic in Coq).
