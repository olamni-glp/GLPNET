# Seed Reconciliation Memo — #1a `iterative-refinement-and-verification-framework`

**Date:** 2026-06-09
**Feature:** `026-engine-review-dossier` (post-approval reconciliation pass)
**Seed:** `iterative-refinement-and-verification-framework` (dossier §11 entry #1a)
**Kind:** PREP (early-stage methodology feature)
**Depends on:** #1 (`engine-review-and-design-dossier`, this dossier)
**Memo author:** sub-agent pass, reconciliation/SEED-RECONCILIATION-BRIEF.md methodology

---

## Dossier cross-references

| Anchor | Content addressed |
|---|---|
| `§0.4` | Classification table — every row is reused/refactored/net-new; this seed defines the shared framework that governs how each row is verified |
| `§11 #1a` | Seed breakdown entry: kind, scope, why, depends_on, §refs |
| `Appendix B #1a` | Two-way traceability: reconciliation memo pointer (this file) |
| `reconciliation/SEED-RECONCILIATION-BRIEF.md §2–§3.5` | De-facto spec for this framework feature; §2 (GEPA/DSPy), §3 (metrics), §3.1–§3.5 (pragmatic + formal + Shapiro anchor) |

The brief is the seed's de-facto spec. This memo surfaces its remaining under-specifications and owner options; it does not supersede the brief.

---

## Seed-vs-dossier-vs-code

### Stored roadmap state (from marathon, sequence_no=0)

The seed has no marathon checkpoint — `marathon resume` returns `found: False, sequence_no: 0`. This is expected: state `captured`, no pipeline stage entered yet.

### Dossier §11 #1a profile

The dossier entry is verbatim:

> Kind: PREP. Scope: "Shared GEPA/DSPy refinement loop (Claude-run, no API) + dual formal (ANTLR4 grammar-as-verifier, type/SRSW, mechanized GLP semantics via model-agnostic agentic Lean/Rocq, MLIR IL-dialect) + pragmatic (testing policy + pluggable per-domain strategies, Shapiro-criteria preservation) verification strategy that every later feature instantiates as its metric combination." Why: "every successor's refinement + metrics plan depends on it; avoids per-feature reinvention." depends_on: 1. §ref: §0.4, Appendix B + `reconciliation/SEED-RECONCILIATION-BRIEF.md`.

### What the brief says this seed must deliver

The brief (§2–§3.5) specifies:

1. **GEPA/DSPy loop architecture** — Claude-via-Agent-seams/MCP, no OpenAI/litellm/API. Precedent: `codeconv/src/codeconv/tools/codegen_opt/optimize.py` (the `run_optimize` loop) and `metric.py` (`make_gepa_metric`). The brief maps precisely to the existing `generate_fn`/`propose_fn`/`oracle_fn`/`build_fn` seam structure and `BudgetCounter` hard cap.

2. **Dual formal+pragmatic metric combination model** — each seed specifies: name, kind (pragmatic|formal), tool, threshold. The formal tier is MANDATORY for any seed touching the language or a wire/byte contract.

3. **Formal tooling slots** — ANTLR4 grammar-as-verifier (brief §3.2); type/SRSW checker (already in-repo); mechanized semantics via Lean 4 or Rocq (brief §3.2a); MLIR logic dialect + progressive lowering (brief §3.2); byte-parity/round-trip proofs (brief §3.2, FR-060/FR-061 pattern from `csharp/glp_link/reliability/FrameCodec.cs:31-32`).

4. **Interactive spec step** — at the start of each seed's `/buildkit-specify`, owner confirms the metric combination + tooling. Sub-agent proposes; owner decides (brief §3.4).

5. **Shapiro/embedded-switch pragmatic anchor** — per-epoch criteria framed as: does this step preserve committed-choice concurrency, SRSW, suspension correctness, monotone binding, three-valued unification while advancing the engine's role as a switch between external connectivity and internal OS/actor (QHSM/HSM) actions (brief §3.5)?

### Code checks

This seed has no implementation (it IS the methodology), so "code checks" mean verifying the claimed precedents exist:

- **GEPA/DSPy loop:** `codeconv/src/codeconv/tools/codegen_opt/optimize.py:257-335` (`run_optimize`) — confirmed. `generate_fn`/`propose_fn` are injected Claude-backed callables with no external API default (`_require_fn` at `:100-117` raises `RuntimeError` if `None`). `BudgetCounter` hard cap at `:217-229`. The `dspy.GEPA` metric wrapper is in `metric.py:268-299` (`make_gepa_metric`).
- **No OpenAI/API:** the hard comment at `optimize.py:6-11` is explicit: "LM steps run IN CLAUDE — never an external API. There is NO OPENAI_API_KEY, NO litellm, NO openai anywhere on this path."
- **Fidelity metric:** `metric.py:203-250` (`fidelity_metric_result`) — the same scorer the production gate uses, imported (not re-implemented). `composite_score` at `:56-87` is the pre-REPL build-only fallback.
- **ANTLR4 grammar-as-verifier:** referenced as a proposed new artifact (brief §3.2, dossier §10.10, §11 #12). Zero code exists today — it IS net-new.
- **Lean/Rocq agentic connectors:** referenced in brief §3.2a tooling matrix. Zero code in-repo today. All net-new.
- **MLIR dialect:** described in brief §3.2, citing PLDI'25 verification-dialects paper. Zero code in-repo today. Net-new.
- **Type/SRSW checker:** exists in `glp_runtime/` (loaded by the REPL pipeline). The dossier's framing of it as a "formal gate today" is accurate.
- **FrameCodec byte-parity invariant:** `csharp/glp_link/reliability/FrameCodec.cs:31-32` — confirmed, the "Dart mirror is byte-identical (FR-060/061)" remark is the in-repo precedent for the byte-contract metric.
- **MarathonStore shape:** `codeconv/src/codeconv/marathon/store.py:1-27` — confirmed PGLite-primary + JSON-fallback with strict-monotonic `sequence_no`.

### Divergence vs stored profile

The stored roadmap state is WSJF=-, RICE=- (unscored, captured-only). The dossier §11 entry accurately describes the scope. There is no divergence between the dossier entry and the brief — the brief IS the expanded spec for this entry. The only profile gap is that WSJF/RICE scoring is deferred (expected for `captured` state).

---

## Classification check

**Kind: PREP.** This is correct — the seed produces no shippable feature; it produces a shared methodology artifact (the metric-combination model, the verification-tooling integration plan, the interactive spec step protocol, and the pluggable-strategy registry) that every later seed (#2–#16) instantiates. There is no executable code to verify; the code-side precedent (GEPA loop in `codegen_opt/`) is in-repo but belongs to a different subsystem. Classification `PREP` matches reality.

**Scope support from code:**
- GEPA/DSPy loop: `codeconv/src/codeconv/tools/codegen_opt/optimize.py:257-335` is the exact precedent the brief invokes. The architecture (seam injection, no-API, budget cap) maps directly.
- Formal tooling matrix: brief §3.2a — the tools (Lean-LSP-MCP, APOLLO, AutoRocq-adapted, Z3/CVC5) are named but have zero in-repo implementation. The classification is therefore "this seed must specify and integrate these tools" — a real PREP scope.
- ANTLR4 grammar: zero code; the seed must specify the spike (later realized as #12).
- MLIR dialect: zero code; the seed must specify the IL verification layer (later realized in #4/#12 spikes).

The scope is wide (it covers the methodology for 15 successor seeds) but the deliverable is a specification artifact + framework skeleton, not a running system. PREP is correct.

---

## Tensions

### T1 — Framework scope vs deliverable tangibility

**Evidence:** The seed is described as delivering "the shared GEPA/DSPy refinement loop + dual formal/pragmatic verification strategy" — but these are methodology artifacts (docs + protocol + tool-integration specs), not runnable code. The successor seeds (#4 IL-codec-spike, #12 ANTLR4-spike) will produce the first tangible verification artifacts. There is a risk that "the framework" remains perpetually unfinished because each piece of it is owned by a later seed.

**Options:**
1. Declare the framework's deliverable as: (a) a shared `REFINEMENT-METHOD.md` + `DECISIONS-FOR-OWNER.md`; (b) the per-seed interactive spec step protocol (codified as a checklist/template); (c) the formal tooling matrix with owner choices filled in. Tangible, completable, pre-conditions subsequent seeds.
2. Scope it narrower: the framework only specifies the GEPA/DSPy loop and the metric combination model. Leave tool selection to each seed's spec step.
3. Scope it wider: actually implement the Lean 4 tactic loop (via Lean-LSP-MCP) and the ANTLR4 grammar harness as deliverables of THIS seed.

*Recommendation:* Option 1 — the three artifacts are completable and load-bearing for #2–#16 without pulling #4/#12 implementation work into this seed.

### T2 — "Claude-run, no API" for Lean/Rocq vs Claude's own token limits + latency

**Evidence:** The brief §3.2a specifies Lean-LSP-MCP, APOLLO, Lean Copilot, AutoRocq (adapted). All of these are tactic-generation loops that call the Lean/Rocq kernel for feedback. Driving them "in Claude via Agent-tool seams/MCP" means Claude generates tactics, sends them to Lean/Rocq over MCP, reads back errors, iterates. This works at small proof sizes but proof search is iterative and can span hundreds of tactic calls — a real context-window and latency concern.

**Options:**
1. Accept the constraint and architect for shallow first-pass proofs + escalation: Claude drives a bounded tactic loop (say, 20 tactic attempts); if unsolved, escalate to the owner as an open question in the spec step. This keeps the no-API rule and is honest about depth.
2. Specify a "proof sketch" mode: Claude writes a Lean 4 / Rocq skeleton with `sorry`s at hard sub-goals; a separate (offline, not Claude-live) run fills the sorrys. The APOLLO architecture explicitly handles this.
3. Accept deeper proofs are out-of-scope for the iterative-refinement loop; formal verification deliverables are verified once at spec time, not re-verified on every GEPA iteration.

*Recommendation:* Options 1 + 3 combined — the GEPA loop uses pragmatic metrics for iteration speed; formal proofs are one-time deliverables at the spec/design step, verified in a bounded tactic loop with APOLLO-style sorry isolation.

### T3 — ANTLR4 grammar: framework deliverable vs spike (#12)

**Evidence:** The brief §3.2 states "define the GLP grammar once in ANTLR4; use the generated parser as an example-coverage verifier — parse every working-definition example to prove the grammar accepts the language before any compiler exists." Dossier §11 #12 (`antlr4-shared-grammar-spike`) is a distinct later feature that depends on #11. There is a tension: if the framework (#1a) claims the ANTLR4 grammar-as-verifier as a deliverable, it overlaps with #12.

**Options:**
1. #1a specifies the ANTLR4 grammar-as-verifier STRATEGY (what it must do, what the acceptance threshold is, how it slots into the metric combination) but does NOT implement the grammar. #12 implements it.
2. #1a delivers a minimal proof-of-concept ANTLR4 grammar covering a GLP subset as a worked example of the framework. This makes #1a larger but gives #2–#16 a concrete harness sooner.
3. Defer the ANTLR4 grammar entirely to #12; in #1a, specify it as "formal metric, kind=formal, tool=ANTLR4 (pending #12), threshold=all working-definition examples parse without error."

*Recommendation:* Option 3 — keeps #1a focused on the methodology spec, lets #12 own the grammar work, and the placeholder entry in the metric table is sufficient for #1a's purpose.

---

## Under-specifications

### U1 — What is the concrete format of the per-seed "metric combination" specification?

**Why it matters:** Every seed's `/buildkit-specify` step is supposed to "confirm which metric combination + which verification tools." Without a concrete format (a template, a table structure, a checklist), each seed will invent its own format and the framework will not be reusable.

**Options:**
1. Define a YAML or Markdown table template: `name | kind (pragmatic|formal) | tool | threshold`. This maps directly to the brief §3.3 per-metric record. The #1a deliverable includes this template.
2. Define it as a section in the spec template (`/buildkit-specify` auto-inserts a metrics block). Requires modifying the buildkit template.
3. Leave it informal: each seed's spec step just records the metrics in free text. Simple but defeats the "framework" purpose.

*Recommendation:* Option 1. The template is a one-page artifact; trivial to author, high payoff for #2–#16.

### U2 — Which Shapiro criteria are REQUIRED (mandatory check) vs advisory for which seed types?

**Why it matters:** The brief §3.5 says "each seed's pragmatic criteria are framed as: does this step preserve the Shapiro/GLP semantic guarantees." But not every seed touches the language semantics directly (e.g., #8 liveness/crash/restart host, #10 multi-accept transport extension). If the Shapiro criteria are mandatory for every seed, most will require boilerplate that adds no signal. If advisory, the framework loses precision.

**Options:**
1. Mandatory for seeds that touch: the GLP language (parser, type-checker, compiler — #11, #12); execution semantics (scheduler, heap, runner — #2, #4, #5); or the wire/byte contract (#3, #4, #5). Advisory (record and justify N/A) for host/infra seeds (#8, #10).
2. Mandatory for ALL seeds, with a standard N/A justification pattern for host/infra seeds.
3. Each seed's spec step decides entirely; no framework mandate.

*Recommendation:* Option 1 — semantically grounded, avoids boilerplate, preserves precision.

### U3 — What is "the MLIR dialect" for GLP — who specifies its primitives and when?

**Why it matters:** The brief §3.2 describes a "GLP/FCP dialect whose primitives are HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate, lowered progressively toward the runtime." This is a non-trivial IR design requiring its own specification. The brief says "pin the correct citation during the #4/#12 spike" (§6 requirement) — meaning it's under-specified right now. A framework that names MLIR IL verification but doesn't specify the dialect is a placeholder, not a deliverable.

**Options:**
1. #1a specifies the MLIR dialect at the level of primitive names + their GLP-semantic meaning (HEAD-unify, GUARD-test, BODY-spawn, suspend-reactivate). The round-trip correctness criterion is specified formally (brief §3.2: "decode(encode(p)) ≡ p"). The actual MLIR infrastructure (generating the dialect, running verification) is #4's responsibility.
2. Defer the entire MLIR specification to #4. #1a only names it as a formal metric slot.
3. #1a commissions a mini-spike (a research note, not a feature) to pin the correct Typed-Datalog-IR citation and confirm the MLIR approach is feasible before specifying it.

*Recommendation:* Option 1 — specifying the primitive names and round-trip criterion is a one-pager; it gives #4 a precise target and does not require implementing anything.

### U4 — How does the framework handle the "LLMs struggle with IR control flow" risk (the mis-attributed `2502.06854` finding)?

**Why it matters:** The brief §3.2 explicitly flags: "it warns LLMs struggle with IR control flow, a real risk for a Claude-driven IL codec (#4)." If the GEPA/DSPy loop asks Claude to generate IL codec code, this is a known failure mode. The framework must either mitigate it (e.g., constrain Claude's role to structural generation + have a deterministic checker verify round-trip) or acknowledge it as a residual risk.

**Options:**
1. Specify that for IL-touching seeds (#4, #5, #11), the GEPA loop uses the round-trip identity check (`decode(encode(p)) ≡ p`) as the PRIMARY metric, not Claude-judged correctness. Claude generates the codec structure; the metric is a deterministic oracle. This is the right separation.
2. Flag it as a risk in the framework but leave mitigation to each IL seed's spec step.
3. Prohibit Claude from generating opcode-level codec code; require human authorship for the discriminant table; Claude only generates the serialization/deserialization wrappers.

*Recommendation:* Option 1 — the deterministic round-trip oracle is already specified in the brief (byte-parity proofs, FR-060/061 pattern); making it the primary metric for IL seeds is a framework-level decision that pre-empts the IR control flow risk.

---

## GEPA/DSPy refinement plan

### Applicability

**`methodological`** — this seed IS the methodology. GEPA/DSPy literally optimizes prompts/instruction sets for LM-based code generation (the in-repo precedent is `codegen_opt/`). For a methodology seed, GEPA/DSPy applies as a discipline (the iterate-against-a-metric model is what the framework specifies for others) rather than as a direct optimization target. The seed does not generate code; it specifies how other seeds will run GEPA/DSPy. Hence `methodological`, not `direct`.

### Seed definition

The `iterative-refinement-and-verification-framework` seed defines a shared specification artifact that:

1. **Specifies the GEPA/DSPy loop architecture** for the engine-separation epic: seed → candidate → evaluate against (pragmatic+formal) metric combination → GEPA reflective mutation / DSPy compile-time optimization → repeat until thresholds hold. Claude-run via Agent-seams/MCP, no OpenAI/litellm/OPENAI_API_KEY. Precedent: `codeconv/src/codeconv/tools/codegen_opt/optimize.py:257-335`.
2. **Specifies the metric combination model** — a Markdown table template (name, kind, tool, threshold) that every later seed instantiates. Mandatory blend of pragmatic + formal; formal mandatory for language/wire-touching seeds.
3. **Specifies the formal tooling integration** — ANTLR4 grammar-as-verifier (pending #12), in-repo type/SRSW checker, Lean 4 / Rocq tactic loop (Claude-driven, APOLLO-style), MLIR logic dialect (pending #4), Z3/CVC5 SMT sub-goal offload, byte-parity round-trip oracle (FR-060/061 pattern).
4. **Specifies the interactive spec step protocol** — the owner confirmation exchange at the start of each seed's `/buildkit-specify`.
5. **Specifies the Shapiro/embedded-switch pragmatic anchor** — which criteria are mandatory per seed type, what "preserves Shapiro" means for an embedded-switch engine.

### Metrics combination

The framework seed's own "does it work" criteria are structural, not executable:

| Name | Kind | Tool | Threshold |
|---|---|---|---|
| Metric-table template completeness | pragmatic | human checklist: does each of #2–#16 have a well-formed metric table? | 100% of successor specs have an accepted metric table at the end of their `/buildkit-specify` |
| GEPA loop architecture coverage | pragmatic | review: does the framework description cover generate/propose/evaluate/budget seams? | matches `optimize.py:257-335` seam structure exactly |
| No-API rule enforcement | pragmatic | grep: `OPENAI_API_KEY`, `litellm`, `openai` absent from all new framework code | 0 occurrences |
| Shapiro criteria mapping | pragmatic | review: are mandatory vs advisory criteria specified per seed type? | framework doc explicitly maps each Shapiro criterion to the seed types for which it is mandatory |
| Formal tooling slots specified | formal | review: are ANTLR4, Lean/Rocq, MLIR, byte-parity, Z3 slots named with pending-feature pointers? | all 5 formal metric slots have a tool name + threshold + dependency pointer |
| Lean 4 / Rocq tactic loop architecture | formal | design review + Lean-LSP-MCP availability check | the bounded tactic loop (max-N tactics, APOLLO sorry-isolation) is described with Claude-as-driver seam |
| MLIR dialect primitive specification | formal | design review: are HEAD-unify/GUARD-test/BODY-spawn/suspend-reactivate named with GLP-semantic definitions? | specification exists at primitive-name + semantics level |

### Interactive spec step

At the start of the `iterative-refinement-and-verification-framework` `/buildkit-specify`, the sub-agent proposes and the owner confirms:

1. **Deliverable format:** is the deliverable (a) `REFINEMENT-METHOD.md` + `DECISIONS-FOR-OWNER.md` + metric-combination template, (b) plus a Lean 4 tactic loop architecture sketch, (c) plus the MLIR primitive specification?
2. **Proof assistant primary choice:** Lean 4 or Rocq as the primary for this epic? (See formal tooling section below.)
3. **Shapiro criteria scope:** which criteria are mandatory for which seed types (language/semantics vs host/infra)?
4. **MLIR specification depth:** primitive names + round-trip criterion only (#1a), or full dialect spec (#4)?
5. **Tactic loop depth limit:** what is the maximum tactic-attempt budget before escalating to owner as an open sub-goal?

### Refinement loop

The GEPA/DSPy refinement loop for this seed runs as follows (Claude-run, no API):

```
seed = initial methodology draft (this memo + brief)
loop:
  candidate = Claude sub-agent drafts the framework artifact
    (REFINEMENT-METHOD.md / metric-combination template / tooling-integration spec)
  evaluate = check against metric combination table above
    (structural completeness, GEPA seam coverage, no-API rule, Shapiro mapping,
     formal tooling slots, Lean/Rocq architecture, MLIR primitives)
  if all thresholds met: terminate, artifact accepted
  reflections = list of unmet criteria + feedback
  GEPA mutation = Claude sub-agent proposes revised artifact based on reflections
  DSPy optimization = refine the instruction set that drives the artifact-drafting agent
  repeat
budget cap: hard (inherited from codegen_opt pattern); capped run yields best-so-far
```

Termination: all 7 metric thresholds met AND the interactive spec step owner confirmation is recorded.

---

## Formal tooling

### Lean 4 vs Rocq evaluation for this seed

**Lean 4 fit:** This seed produces a methodology specification (documents + templates), not a mechanized proof. However, when the framework specifies mechanized GLP semantics, Lean 4 is the stated owner preference (brief §3.2a: "Lean 4 may suffice as the primary across the board"). Lean 4 has `mathlib` for algebraic/logical foundations; the `Lean-LSP-MCP` connector is Claude-native; APOLLO is explicitly model-agnostic and ran on o3-mini/o4-mini/Goedel-Prover (not GPT-4-only). For GLP's three-phase operational semantics (HEAD/GUARD/BODY), three-valued unification, and suspension/reactivation, Lean 4's dependent-type system and `mathlib` tactics are well-suited.

**Rocq fit:** Rocq (Coq) has the strongest verified-compiler prior art (`csharp/glp_link/reliability/FrameCodec.cs:31-32` byte-parity paradigm maps to Vellvm which is Coq-based; the Verified Prolog-to-WAM compiler paper uses Coq-lineage verification). AutoRocq is the agentic Rocq driver but requires adapting away from its GPT-4 dependency. Rocq is a stronger default for the IL/bytecode verification sub-problem (TWAM, Vellvm precedent).

**Primary for this epic:** `lean4`. Rationale: the brief names it as the owner-preferred option to evaluate first; Lean-LSP-MCP is Claude-native; APOLLO's model-agnosticism makes the no-API constraint easy to satisfy; Lean 4 covers the operational semantics and type/SRSW properties well.

**Alternative when:** Rocq is the documented alternative specifically for the IL/bytecode verification component (#4, #5, #11) where the Vellvm/TWAM Coq prior art is most directly applicable. At the start of #4's `/buildkit-specify`, the sub-agent re-evaluates whether Lean 4's `LeanWAM` / logic-machine libraries cover the bytecode verification need or whether the Rocq/Vellvm lineage is a better substrate.

### IL verification approach

`n/a` for this seed directly (it specifies the framework, not the IL). However, the framework must specify the IL verification layer for IL-touching seeds:

- **MLIR logic dialect:** primitives HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate, progressively lowered to imperative targets. Verified using MLIR's first-class verification dialects (PLDI'25: `users.cs.utah.edu/~regehr/papers/pldi25.pdf`).
- **Byte-parity / round-trip:** `decode(encode(p)) ≡ p` (FR-060/061 pattern, `FrameCodec.cs:31-32`). Verified as a Lean 4 / Rocq round-trip theorem for the IL codec (#4).
- **WAM-lineage verified-compiler:** TWAM (`arxiv 1801.00471`) and the Verified Prolog-to-WAM compiler (`ScienceDirect 0743106692900547`) as formal models for "compiled-execution ≡ source-interpretation." These are the templates for GLP bytecode verification.
- **Citation gap:** the `arxiv 2502.06854v1` link is mis-attributed (it is an LLM-comprehension-of-LLVM-IR study, not the Typed-Datalog-IR paper). The Typed-Multi-level-Datalog-IR citation must be pinned during the #4/#12 spike (brief §6 open requirement).

---

## Shapiro criteria preserved

This seed, as a methodology framework, must specify how each of the following criteria is preserved by the engine-separation epic as a whole — and must make these framings available as the Shapiro-criteria column in each successor seed's metric table:

1. **Committed-choice concurrency** — the engine's committed-choice resolution (once a clause's HEAD succeeds and GUARDs pass, the choice is committed; no backtracking) must hold across the process split. The wire protocol must not introduce a race condition where a remote client can re-invoke a committed goal. Mandatory for: #2, #5, #6, #13.

2. **SRSW (Single-Reader / Single-Writer)** — each variable occurs at most once per clause as a writer and at most once as a reader. The result-envelope codec (#2, #5) must encode the var-name→writer-id map in a way that the SRSW invariant can be re-checked by a remote client. Mandatory for: #2, #5, #11, #12.

3. **Suspension correctness** — a goal suspending on an unbound reader must resume exactly when the writer binds it, with no spurious or missed reactivations. The persistence/restore (#7, #9) path must reconstruct suspension chains so reactivation is identical to in-process behavior (`heap_fcp.cs:730-742`). Mandatory for: #2, #5, #7, #9.

4. **Monotone variable binding** — a writer variable is bound at most once; binding is permanent. The snapshot/restore path must not re-bind already-bound variables. Mandatory for: #7, #9.

5. **Three-valued unification** — unification succeeds, suspends, or fails; no fourth outcome. The result envelope's status encoding (Succeeded | Failed | Suspended) must be a faithful projection of this three-valued result. Mandatory for: #2, #5, #6.

The embedded-switch framing (per brief §3.5): each criterion above applies not just to in-process GLP but to the engine acting as a switch between (a) external connectivity (GLP link layer, `csharp/glp_link/`) and (b) internal OS actions (QHSM/HSM actors, classical OS tasks). The pragmatic test for each criterion is: run a scenario where a GLP goal spanning an external link (e.g., `link_recv`, `self.glp:548`) transitions correctly through the embedded-switch seam without violating the criterion.

---

## Recommendation

The seed is correctly classified (PREP), accurately scoped, and its dossier entry is consistent with the brief (which is its de-facto spec). The primary recommendation is:

**Proceed to `/buildkit-specify` for this seed after the dossier owner-approval gate, with the interactive spec step confirming the four under-specified decisions (U1–U4) and the formal-tooling primary (Lean 4).**

The framework deliverable is three artifacts:
1. `docs/research/repl-engine-separation/reconciliation/REFINEMENT-METHOD.md` — shared GEPA/DSPy loop architecture, metric-combination table template, no-API rule, budget-cap spec.
2. `docs/research/repl-engine-separation/reconciliation/DECISIONS-FOR-OWNER.md` — proof-assistant choice (Lean 4 primary / Rocq IL alternative), MLIR dialect primitive spec, Shapiro criteria mandatory/advisory mapping, tactic loop depth limit.
3. The metric-combination template (a Markdown table structure reusable by each successor's `/buildkit-specify`).

These are completable before any successor seed enters the pipeline and are sufficient to pre-condition #2–#16.

---

## Options for owner

| Label | Consequence |
|---|---|
| Option A: deliver only the three framework artifacts (REFINEMENT-METHOD.md + DECISIONS-FOR-OWNER.md + metric template) | Completable in one implementation session; sufficient for #2–#16; defers Lean/Rocq tactic loop and MLIR work to the seeds that use them (#4, #11, #12) |
| Option B: additionally deliver a Lean 4 tactic loop architecture sketch (Claude-as-driver seam, APOLLO sorry-isolation, bounded budget) | Adds 1–2 days work; gives #4 and #11 a concrete tactic-loop spec to implement against; reduces open question U-Lean4TacticLoop |
| Option C: additionally deliver the MLIR dialect primitive specification (HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate names + GLP-semantic definitions + round-trip criterion) | Adds 1 day work; gives #4 a precise MLIR target; resolves U3 |
| Option D: all of A + B + C | Full framework; largest scope; recommended if #4 is next in the queue |

---

## Open questions

1. **Citation gap:** The `arxiv 2502.06854v1` link is mis-attributed (brief §6). The Typed-Multi-level-Datalog-IR citation for the MLIR precedent must be pinned. Should this be a blocking precondition for the framework spec, or a tracked open item?

2. **Lean 4 ecosystem on Windows:** Lean 4 is primarily developed for Linux/Mac. `Lean-LSP-MCP` and `Lean Copilot` may require a Linux container or WSL2 on the Windows-11 host (`D:\bstdev\research\glp\glpnet`). Should the framework spec include a Windows tooling setup note?

3. **AutoRocq GPT-4 adaptation:** AutoRocq's code uses GPT-4 API calls. Has the adaptation to Claude-driven tactic generation been scoped? The brief says "adapt off its GPT-4 dependency" but no spike exists.

4. **APOLLO availability:** APOLLO (`arxiv 2505.05758`) is a research paper; is the code publicly available (GitHub) and installable, or does the framework need to implement the sorry-isolation architecture from scratch?

5. **Proof scope for the MVP path:** The MVP (#6) is a process split with source-text wire. Lean 4 proof obligations for the MVP are minimal (transport correctness is already covered by `FrameCodec.cs:31-32` byte-parity). Should the framework clarify that formal proofs are NOT on the MVP critical path — only on language-touching seeds (#11, #12)?

---

## External refs

- [First-Class Verification Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf) — MLIR semantics-first verification dialects; the GLP/FCP dialect model
- [TWAM: A Certifying Abstract Machine for Logic Programs (arxiv 1801.00471)](https://arxiv.org/pdf/1801.00471) — verified WAM-lineage IL; the GLP bytecode verification template
- [Verified Prolog→WAM compiler (ScienceDirect 0743106692900547)](https://www.sciencedirect.com/science/article/pii/0743106692900547) — compiled-execution ≡ source-interpretation; formal model for bytecode correctness
- [APOLLO — model-agnostic agentic Lean proving (arxiv 2505.05758)](https://arxiv.org/abs/2505.05758) — sorry isolation + repair; the Claude-as-driver tactic loop model
- [LLM comprehension of LLVM IR (arxiv 2502.06854v1)](https://arxiv.org/html/2502.06854v1) — "LLMs struggle with IR control flow"; risk signal for Claude-driven IL codec
- [AutoRocq — autonomous Rocq proof agent](https://github.com/NUS-Program-Verification/AutoRocq) — Rocq alternative; adapt away from GPT-4 dependency
- [Lean 4](https://lean-lang.org/papers/lean4.pdf) — primary proof assistant; `mathlib` for algebraic foundations
- Owner exploration links (to mine during spikes): `arxiv 2601.14027`, `arxiv 2505.05758v5`, `share.google/aimode/BMXNyJwRDQMcCyyrk`, `share.google/aimode/9AYccXYjLQz3cGXEW`
- In-repo precedent files: `codeconv/src/codeconv/tools/codegen_opt/optimize.py` (GEPA loop), `codeconv/src/codeconv/tools/codegen_opt/metric.py` (fidelity metric + make_gepa_metric), `csharp/glp_link/reliability/FrameCodec.cs:31-32` (byte-parity precedent)
