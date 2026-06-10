# Feature Specification: Iterative Refinement & Verification Framework

**Feature Branch**: `027-refinement-verification-framework`
**Created**: 2026-06-09
**Status**: Draft
**Input**: User description: "Iterative refinement & verification framework (GEPA/DSPy + formal + pragmatic)"

**Seed**: `iterative-refinement-and-verification-framework` (dossier §11 #1a) · **Kind**: PREP · **Depends on**: #1 (`026-engine-review-dossier`)
**De-facto upstream spec**: `docs/research/repl-engine-separation/reconciliation/SEED-RECONCILIATION-BRIEF.md` §2–§3.5
**Seed memo**: `docs/research/repl-engine-separation/reconciliation/1a-iterative-refinement-and-verification-framework.md`
**Owner deliverable-scope decision (2026-06-09)**: **Option D** — the three framework artifacts **plus** the Lean 4 tactic-loop architecture sketch **plus** the MLIR dialect primitive specification, **extended by the owner's same-day directive that the Lean and MLIR approaches each be validated by a runnable real-tool experiment (not desk research)** — see Clarifications.

> This is a **PREP / methodology** feature. It produces no shippable runtime; it produces a shared specification + framework skeleton that every successor seed (#2–#16) of the engine-separation epic instantiates. Because the subject matter *is* a verification methodology, named tools (GEPA, DSPy, Lean 4, Rocq, ANTLR4, MLIR, Z3/CVC5) appear as domain entities, not as premature implementation choices — see the requirements checklist note.

---

## Clarifications

### Session 2026-06-09

- Q: Must the Lean-risk evaluation and the MLIR/GLP-dialect approach be desk research, or real verification? → A: **Real verification experimentation — NOT desk research.** #1a MUST include runnable feasibility-validation spikes: Python harness code that drives the *actual* tools (a real Lean 4 install over MCP; real MLIR via its Python bindings) and empirically validates (a) the bounded Claude-driven Lean tactic loop discharges a concrete GLP property, and (b) the MLIR/GLP-dialect round-trip `decode(encode(p)) ≡ p` is feasible. Re-evaluation on paper is insufficient.
- Q: Feasibility-spike vs full implementation boundary for #1a? → A: #1a delivers **validation spikes** sized to prove the approach works (one small GLP property for Lean; a minimal IL fragment for MLIR round-trip). The *full* mechanized proofs and production MLIR infrastructure remain owned by #4/#11/#12. This **extends R11 / DEF-B1 / DEF-H1**: a real validation spike runs now; full proofs stay gated to the language-touching seeds.
- Q: Lean tactic-loop attempt budget before `sorry`-isolation/escalation? → A: **20 attempts as the research-grounded starting point, then empirically iterated** against real attempts/tests during the validation spike — the budget is itself a tuned experimental variable, not a fixed constant.
- Q: Any required tool for validating the front-end↔back-end wire protocol? → A: **Yes — Promela/SPIN is REQUIRED** for pragmatic protocol validation of the front↔back wire protocol (deadlock-freedom, message-ordering, progress/liveness). Mandatory for the wire-protocol seeds (#2, #5, #6). Like Lean/MLIR, the framework MUST validate it with a real-tool spike: a minimal Promela model of the front↔back request/response protocol checked with real SPIN — not desk research.
- Q: Any further protocol/concurrency verification tools to stock in the armoury? → A: **Yes — add a documented armoury** with SPIN/Promela as the required default plus alternatives selected by protocol type: **TLA+** (distributed consensus/high-level), **UPPAAL** (real-time/timed), **NuSMV/nuXMV** (symbolic, large state spaces), **mCRL2** (process algebra/concurrent), **FDR4** (CSP refinement/deadlock-livelock), **CADP** (asynchronous/large-scale distributed). Each seed picks the fit tool at its interactive spec step (FR-078/FR-079).

## User Scenarios & Testing *(mandatory)*

The "users" of this framework are (a) the **epic owner** (Gabi) who confirms each successor seed's verification approach at its `/buildkit-specify`, and (b) the **engineer/agent** who drives a successor seed's refinement loop and authors its proofs.

### User Story 1 - Specify a successor seed's metric combination from a shared template (Priority: P1)

When an engineer opens `/buildkit-specify` for any engine-separation successor seed (#2–#16), they reach for the framework's shared **metric-combination template** instead of inventing a format. They fill one table — `name | kind (pragmatic|formal) | tool | threshold` — proposing the pragmatic+formal blend for that seed; the owner confirms or amends it; the accepted table is recorded in that seed's spec.

**Why this priority**: This is the load-bearing deliverable. Without one reusable format, each of the 15 successor seeds reinvents how it states "done," and the framework fails its only purpose. This alone is a viable MVP of the framework.

**Independent Test**: Take the template, instantiate it for one already-reconciled seed (e.g., #5 result codec) end-to-end, and confirm the resulting table is well-formed (every metric has a kind, a concrete tool/harness, and a measurable threshold) and that the formal tier is present because the seed touches a wire/byte contract.

**Acceptance Scenarios**:

1. **Given** the metric-combination template, **When** an engineer instantiates it for a language- or wire-touching seed, **Then** the table MUST contain at least one `formal` metric with a named tool and a measurable threshold.
2. **Given** a host/infra seed (#8, #10), **When** an engineer instantiates the template, **Then** the table MAY omit formal metrics but MUST record an explicit Shapiro-criteria N/A justification (per R9).
3. **Given** a completed metric table, **When** the owner reviews it at the interactive spec step, **Then** the owner's confirmation (or amendment) is recorded in the seed's spec before any implementation task is generated.

---

### User Story 2 - Run a Claude-only GEPA/DSPy refinement loop against a metric combination (Priority: P1)

An engineer takes a defined seed, drafts a candidate (instruction set, codec, or design artifact), evaluates it against the seed's metric combination, and iterates: GEPA reflective mutation / DSPy compile-time optimization → re-evaluate → repeat until thresholds hold or the budget cap is reached. Every LM step runs **in Claude via Agent-tool seams / MCP** — never OpenAI, litellm, or `OPENAI_API_KEY`.

**Why this priority**: The refinement loop is the second half of the framework's name and the discipline every successor inherits. It must be specified so each seed loops the same way, against the same kind of metric, under the same hard budget cap.

**Independent Test**: Trace the loop description against the in-repo precedent `codeconv/src/codeconv/tools/codegen_opt/optimize.py` `run_optimize` (lines ~257–335) and confirm the generate / propose / evaluate / budget seams map one-to-one, and that no external-API path is reachable (`_require_fn` raises if a Claude-backed callable is absent).

**Acceptance Scenarios**:

1. **Given** the framework's loop description, **When** it is compared to `optimize.py:257-335`, **Then** the generate/propose/evaluate/budget seam structure MUST match.
2. **Given** a refinement run, **When** the budget cap is reached before all thresholds hold, **Then** the loop MUST terminate and yield the best-so-far candidate (no unbounded run).
3. **Given** any new framework code or example, **When** scanned for `OPENAI_API_KEY` / `litellm` / `openai`, **Then** there MUST be zero occurrences.

---

### User Story 3 - Validate the bounded Lean 4 tactic loop with a real experiment (Priority: P1)

For a seed that touches the GLP language or a wire/byte contract, the engineer needs a repeatable way to discharge a formal proof obligation. The framework supplies a **Lean 4 tactic-loop architecture sketch** — Claude generates a tactic → the Lean kernel returns feedback over MCP (Lean-LSP-MCP) → Claude retrieves lemmas / repairs → repeat, up to a bounded tactic budget; unsolved sub-goals are isolated as `sorry` (APOLLO-style) and escalated as open obligations — **and validates it with a runnable experiment against a real Lean 4 install**: a Python harness drives the loop on one concrete GLP property and measures whether it discharges within budget. The sketch is not accepted until the experiment empirically demonstrates it.

**Why this priority**: Formal proofs are mandatory wherever a seed touches the language or a byte contract (brief §3.2), but the *full* proofs are OFF the MVP critical path (R11) and gated to #4/#11/#12 (DEF-B1). The *validation spike* runs here, in #1a, because the methodology must be proven to work before #2–#16 depend on it — desk research alone is insufficient (owner, 2026-06-09). It must exist before #4/#11/#12 enter the pipeline.

**Independent Test**: Run the Python harness against a real Lean 4 toolchain (WSL2/container per R10) and confirm it drives Claude as the model-agnostic tactic driver (no fixed GPT-4 API), enforces the tactic-attempt budget, exercises the `sorry`-isolation + escalation path, and produces a recorded result (proved / sorry-isolated) for the chosen GLP property — i.e., the loop is demonstrated empirically, not described.

**Acceptance Scenarios**:

1. **Given** the validation experiment, **When** the harness runs against a real Lean 4 install, **Then** it MUST discharge (or `sorry`-isolate and escalate) the chosen concrete GLP property and record the outcome plus the number of tactic attempts used.
2. **Given** the tactic loop, **When** a proof exceeds the tactic-attempt budget (starting value 20, empirically tuned during the experiment), **Then** the unsolved sub-goal MUST be recorded as a `sorry` and surfaced as an owner open obligation — not silently dropped, not run unbounded.
3. **Given** the Windows-11 host (`D:\`), **When** an engineer runs the experiment, **Then** a working setup path for the Linux-first Lean toolchain (WSL2/container) MUST be documented and used by the harness.
4. **Given** a seed that selects Rocq instead of Lean 4 for an IL/bytecode obligation, **Then** the framework MUST point to the documented Rocq alternative and the AutoRocq GPT-4-dependency-removal deferral (DEF-F-tooling).

---

### User Story 4 - Validate the MLIR/GLP-dialect round-trip with a real experiment (Priority: P2)

The IL-codec spike (#4) and compiler-factor-out (#11) need a precise IR target. The framework supplies an **MLIR dialect primitive specification** — the GLP/FCP dialect's primitives `HEAD-unify`, `GUARD-test`, `BODY-spawn`, `suspend-reactivate`, each with its GLP-semantic meaning, plus the round-trip criterion `decode(encode(p)) ≡ p` as the primary (deterministic) metric for IL-touching seeds — **and validates the approach with a runnable experiment against real MLIR**: a Python harness (MLIR Python bindings) builds the dialect for a minimal GLP IL fragment and demonstrates round-trip identity empirically. The approach is not accepted until the experiment runs.

**Why this priority**: The full MLIR infrastructure is consumed by IL-touching seeds (#4, #11, #12) and stays owned there (DEF-H1). But the methodology asserts MLIR is the right substrate for the GLP IL — a claim the owner requires be *demonstrated*, not desk-argued (2026-06-09). A minimal feasibility spike here de-risks #4 before it commits.

**Independent Test**: Run the Python/MLIR harness on a minimal GLP IL fragment and confirm it (a) realizes the four primitives in a real MLIR dialect, (b) demonstrates `decode(encode(p)) ≡ p` on at least one non-trivial fragment, and (c) records the result — with Claude restricted to structural generation while the deterministic round-trip oracle is the pass/fail metric (mitigates the "LLMs struggle with IR control flow" risk, U4).

**Acceptance Scenarios**:

1. **Given** the MLIR validation experiment, **When** the harness runs against real MLIR, **Then** it MUST construct the four primitives and demonstrate round-trip identity `decode(encode(p)) ≡ p` on a minimal GLP IL fragment, recording pass/fail.
2. **Given** the MLIR dialect spec, **When** #4 is specified, **Then** all four primitives MUST be defined at the name + GLP-semantics level with the round-trip criterion stated and the experiment's evidence cited.
3. **Given** an IL-touching seed's refinement loop, **When** the primary metric is chosen, **Then** it MUST be the deterministic round-trip oracle, not Claude-judged correctness.
4. **Given** the mis-attributed `2502.06854` citation, **Then** the framework MUST record the citation as an open item anchored to the #4/#12 spike (DEF-B2; candidate: LingoDB, VLDB 2022) and MUST NOT block this feature on it.

---

### User Story 5 - Validate the front↔back wire protocol with Promela/SPIN (Priority: P1)

The engine-separation epic splits a single REPL into a front-end (thin client) and a back-end (engine) talking over a wire protocol. Before successor seeds build that protocol, the framework REQUIRES **Promela/SPIN** as the pragmatic protocol-validation tool: model the front↔back request/response protocol in Promela, run SPIN to check deadlock-freedom, absence of unspecified receptions, and progress/liveness. The framework validates the approach with a runnable spike against **real SPIN** on a minimal model of the protocol.

**Why this priority**: The front↔back wire protocol is the heart of the MVP (#6) and the result/wire codec seeds (#2, #5) — the next features in the queue. Concurrency/protocol bugs (deadlock, lost messages, stuck progress) are exactly what model checking catches and tests miss, so this is mandatory pragmatic validation for those seeds and P1.

**Independent Test**: Run real SPIN on the minimal Promela model of the front↔back request/response protocol and confirm it reports deadlock-freedom and progress (no invalid end states), with the checked properties named and the run reproducible.

**Acceptance Scenarios**:

1. **Given** the Promela model of the front↔back protocol, **When** SPIN is run, **Then** it MUST report deadlock-freedom and satisfaction of the named progress/liveness property (or surface a counterexample trace), with the result recorded.
2. **Given** a wire-protocol successor seed (#2, #5, #6), **When** its metric table is instantiated, **Then** Promela/SPIN protocol validation MUST appear as a required pragmatic-tier metric with named safety/liveness properties.
3. **Given** the minimal spike model, **When** the full envelope/protocol is later designed, **Then** the complete protocol model is deferred to #5/#6 (DEF-A3) — the #1a spike covers a minimal handshake only.

---

### Edge Cases

- **A successor seed touches no language/wire/semantics surface** (pure host/infra, #8/#10): the metric table omits the formal tier but MUST carry an explicit N/A justification for each Shapiro criterion (R9).
- **A Lean proof never converges within budget**: isolated as `sorry`, escalated as an owner open obligation; the seed still proceeds with its pragmatic metrics (R11 keeps proofs off the blocking path for non-language seeds).
- **An engineer attempts to wire an external LM API into a refinement loop**: prohibited; the no-API rule (FR) makes any such line a defect to delete, not a configuration to accept.
- **The Lean toolchain is unavailable on the Windows host**: the WSL2/container setup path (R10) MUST be stood up because this feature's validation spike (FR-035) requires a real Lean 4 install; for *successor* seeds, a missing Lean install still does not block non-language seeds.
- **A seed's owner-confirmed metric table is later found inconsistent** (e.g., a wire-touching seed with no formal metric): the inconsistency is a spec defect surfaced at `/buildkit-analyze`, fixed by amending the table — not by relaxing the formal-tier mandate.

## Requirements *(mandatory)*

### Functional Requirements — Framework artifacts (Option D scope)

- **FR-001**: The framework MUST deliver `docs/research/repl-engine-separation/reconciliation/REFINEMENT-METHOD.md` documenting the shared GEPA/DSPy refinement-loop architecture, the metric-combination model, the no-API rule, and the hard budget-cap discipline.
- **FR-002**: The framework MUST deliver `docs/research/repl-engine-separation/reconciliation/DECISIONS-FOR-OWNER.md` recording the proof-assistant choice (Lean 4 primary / Rocq alternative), the MLIR dialect primitive spec, the Shapiro mandatory/advisory mapping, and the tactic-loop depth limit. *(Both files exist from the 026 reconciliation pass; this feature finalizes them as the framework's authoritative artifacts.)*
- **FR-003**: The framework MUST deliver a reusable **metric-combination template** — a Markdown table with columns `name | kind (pragmatic|formal) | tool | threshold` (R8) — that every successor seed instantiates at its `/buildkit-specify`.

### Functional Requirements — GEPA/DSPy refinement loop

- **FR-010**: The framework MUST specify the refinement loop as: seed → candidate → evaluate against the metric combination → GEPA reflective mutation / DSPy compile-time optimization → repeat until thresholds hold.
- **FR-011**: The specified loop's seam structure (generate / propose / evaluate / budget) MUST match the in-repo precedent `codeconv/src/codeconv/tools/codegen_opt/optimize.py` (`run_optimize`, ~lines 257–335).
- **FR-012**: All LM-in-the-loop steps (generation, proposal, LLM-assisted verification) MUST run in Claude via Agent-tool seams / MCP. The framework MUST forbid OpenAI / litellm / `OPENAI_API_KEY` on any refinement or verification path; any "needs an API" requirement is a defect to delete.
- **FR-013**: The loop MUST be bounded by a hard budget cap (inherited from the `codegen_opt` `BudgetCounter` pattern); a capped run MUST yield the best-so-far candidate rather than running unbounded.

### Functional Requirements — Metric-combination model

- **FR-020**: Each successor seed's metric table MUST blend pragmatic and formal metrics; each row MUST carry a concrete tool/harness and a measurable threshold (brief §3.3).
- **FR-021**: The formal tier MUST be mandatory for any seed that touches the GLP language or a wire/byte contract; it MAY be omitted only for host/infra seeds, with an explicit justification.
- **FR-022**: The framework MUST enumerate the available verification tooling slots with a tool name, a threshold shape, and a dependency pointer for each: (a) ANTLR4 grammar-as-verifier (pending #12); (b) in-repo type/SRSW checker (available today); (c) Lean 4 / Rocq mechanized semantics (this feature's tactic-loop sketch + validation spike, FR-035); (d) MLIR logic dialect (pending #4); (e) byte-parity round-trip oracle (FR-060/061 pattern, `csharp/glp_link/reliability/FrameCodec.cs`); (f) **protocol/concurrency verification armoury** — Promela/SPIN (default) + alternatives TLA+, UPPAAL, nuXMV, mCRL2, FDR4, CADP (pragmatic protocol validation — deadlock/liveness — of the front↔back protocol; armoury FR-078, validation spike FR-080; mandatory for #2/#5/#6).

### Functional Requirements — Lean 4 tactic-loop sketch (Option B)

- **FR-030**: The framework MUST specify a bounded Lean 4 tactic loop with Claude as the model-agnostic tactic driver: generate tactic → Lean kernel feedback over MCP (Lean-LSP-MCP) → lemma retrieval / repair → repeat.
- **FR-031**: The tactic loop MUST define a maximum tactic-attempt budget with a starting value of **20**, treated as a tuned experimental variable (iterated against real attempts during the validation experiment, FR-035) rather than a fixed constant; on exhaustion, the unsolved sub-goal MUST be isolated as a `sorry` (APOLLO-style) and escalated to the owner as an open obligation.
- **FR-032**: The framework MUST document that Lean 4 is the epic-primary proof assistant and Rocq the documented alternative for IL/bytecode obligations (Vellvm/TWAM lineage), and MUST point to the AutoRocq GPT-4-dependency-removal deferral (DEF-F-tooling) if Rocq is selected.
- **FR-033**: The framework MUST include a Windows-11 tooling note (R10): the Lean toolchain and Lean-LSP-MCP are Linux/Mac-first; a WSL2/container setup path is documented for the `D:\` host **and is exercised by the validation experiment (FR-035)** — not left as an untested note.
- **FR-034**: The framework MUST state that the *full* formal Lean/Rocq proofs are OFF the MVP critical path (R11) and gate only language-touching seeds (#4, #11, #12); the *bounded validation spike* (FR-035) runs in this feature.
- **FR-035**: The framework MUST deliver a **runnable Lean validation experiment**: a Python harness that drives the bounded Claude-over-MCP tactic loop against a **real Lean 4 install** on one concrete GLP property (e.g., SRSW preservation or unification soundness on a toy clause), records the outcome (proved / `sorry`-isolated) and the tactic-attempt count, and thereby empirically demonstrates the loop architecture. Desk re-evaluation does NOT satisfy this requirement.

### Functional Requirements — MLIR dialect primitive spec (Option C)

- **FR-040**: The framework MUST specify the GLP/FCP MLIR dialect primitives `HEAD-unify`, `GUARD-test`, `BODY-spawn`, `suspend-reactivate`, each with a GLP-semantic definition, plus the progressive-lowering intent (dialect → imperative targets).
- **FR-041**: The framework MUST state the round-trip identity criterion `decode(encode(p)) ≡ p` as the **primary, deterministic** metric for IL-touching seeds, with Claude restricted to structural generation (mitigating the IR-control-flow risk, U4).
- **FR-042**: The framework MUST record the Typed-Multi-level-Datalog-IR citation as an open item anchored to the #4/#12 spike (DEF-B2; candidate LingoDB, VLDB 2022) and MUST NOT block this feature on pinning it.
- **FR-043**: The framework MUST deliver a **runnable MLIR validation experiment**: a Python harness (using MLIR's Python bindings) that realizes the four GLP/FCP dialect primitives for a **minimal GLP IL fragment** and empirically demonstrates round-trip identity `decode(encode(p)) ≡ p`, recording pass/fail. Claude is restricted to structural generation; the deterministic round-trip oracle is the pass/fail metric. Desk argument does NOT satisfy this requirement.

### Functional Requirements — Validation experimentation (real tools, not desk research)

- **FR-070**: This feature MUST NOT accept the Lean tactic-loop approach or the MLIR/GLP-dialect approach on desk research alone; each MUST be validated by the runnable experiment (FR-035, FR-043) executing against the real tool before its corresponding artifact is marked complete (owner directive, 2026-06-09).
- **FR-071**: Each validation experiment MUST be **reproducible**: a recorded command/script, the tool versions used, and the captured result (pass/fail + measurements) MUST be committed so a reviewer can re-run it and so #4/#11/#12 inherit a working starting point.
- **FR-072**: The experiments MUST run against **real installed tools** — a working Lean 4 toolchain (via WSL2/container per R10) and real MLIR (Python bindings) — establishing the environment as a prerequisite of THIS feature, not deferred to later seeds.
- **FR-073**: The no-API rule (FR-012) MUST still hold inside the experiments: LM-in-the-loop steps (Lean tactic generation) run in Claude via Agent-tool seams / MCP; deterministic tooling (the Lean/MLIR kernels, the round-trip oracle, Z3/CVC5 if used) is permitted as ordinary local tooling.
- **FR-074**: The experiments MUST be scoped as **minimal feasibility spikes** (one small GLP property; one minimal IL fragment; one minimal protocol handshake — FR-080) sufficient to validate the approach; full mechanized proofs, production MLIR infrastructure, and the complete protocol model remain out of scope here and owned by #4/#11/#12 (proofs/MLIR) and #5/#6 (full protocol model).

### Functional Requirements — Wire-protocol & concurrency verification armoury (SPIN default + alternatives)

- **FR-076**: The framework MUST adopt **Promela/SPIN** as the REQUIRED pragmatic-tier default tool for validating the front-end↔back-end wire protocol, checking at minimum deadlock-freedom, absence of unspecified receptions, and a progress/liveness property; protocol validation is **mandatory** in the metric table of every wire-protocol seed (#2, #5, #6).
- **FR-077**: Each wire-protocol seed's metric table MUST name the specific safety and liveness properties its model check covers (e.g., "no deadlock", "every request eventually receives a response or a typed error", "no message reordering observable to the client").
- **FR-078**: The framework MUST document a **protocol/concurrency verification armoury** — a tool matrix giving, for each tool, its modeling paradigm, verification engine, primary strength, and best-for use case — comprising at least: **SPIN/Promela** (explicit-state, LTL — default, network protocols & algorithms); **TLA+/PlusCal** (TLC; high-level distributed systems & consensus, e.g. Raft/Paxos); **UPPAAL** (timed automata; real-time/timed protocols, timeouts & clock constraints); **NuSMV/nuXMV** (symbolic BDD/SAT; large state spaces, synchronous/state-machine protocols); **mCRL2** (process algebra + abstract data types; complex concurrent communication); **FDR4** (CSP refinement; deadlock/livelock in communicating processes); **CADP** (asynchronous, large-scale distributed protocols).
- **FR-079**: At a wire-protocol seed's interactive spec step (FR-060), the agent MUST select the armoury tool fit to that seed's protocol type — SPIN as the default, escalating to TLA+ (consensus/multi-client, e.g. #13), UPPAAL (timer/escrow/timeout logic, e.g. #7/#8), nuXMV (large synchronous state spaces), or mCRL2/FDR4/CADP (rich process-algebra/asynchronous needs) — recording the choice and its rationale.
- **FR-080**: The framework MUST deliver a **runnable Promela/SPIN validation spike**: a minimal Promela model of the front↔back request/response protocol, checked with a **real SPIN** install, demonstrating deadlock-freedom + progress (or surfacing a counterexample), with the result recorded and the run reproducible (FR-071). Desk argument does NOT satisfy this requirement.
- **FR-081**: The full Promela model of the complete wire protocol / result envelope is OUT of scope for #1a and deferred to the wire-protocol seeds (DEF-A3, anchored at #5/#6); the #1a spike covers a minimal handshake only.

### Functional Requirements — Shapiro / embedded-switch pragmatic anchor

- **FR-050**: The framework MUST define each Shapiro/GLP semantic guarantee — committed-choice concurrency, SRSW, suspension correctness, monotone variable binding, three-valued unification — and map each to the successor seed types for which checking it is **mandatory** vs **advisory** (R9).
- **FR-051**: The framework MUST frame each seed's pragmatic criteria as: does this step preserve the named Shapiro guarantees **while** advancing the engine's embedded-switch role (routing between external connectivity and internal OS/actor — QHSM/HSM — actions)?

### Functional Requirements — Interactive spec step protocol

- **FR-060**: The framework MUST define the interactive spec-step protocol: at the start of each successor seed's `/buildkit-specify`, the agent proposes the metric combination + verification tools; the owner confirms or amends; the confirmed result is recorded in that seed's spec before task generation.
- **FR-061**: The framework MUST require that the per-seed `PRE-SPECIFY` pointer surfaces the ratified decisions log (`DECISIONS-LOG.md`) and the deferral register (`DEFERRALS.md`), so each seed applies every `R`-row whose scope includes it and actions every `DEF`-row anchored at it.

### Key Entities

- **Metric-combination table**: the per-seed record of how "done" is measured. Attributes: a set of rows, each `name`, `kind` ∈ {pragmatic, formal}, `tool`/harness, `threshold`. Mandatory formal tier for language/wire seeds.
- **Refinement loop**: the bounded, Claude-only iterate-against-a-metric cycle. Attributes: candidate generator seam, proposer seam, evaluator (metric combination), budget cap, termination condition (thresholds met or budget exhausted).
- **Formal tooling slot**: a named verification capability available to seeds. Attributes: tool name, threshold shape, dependency pointer (available-now vs pending-feature).
- **Lean 4 tactic loop**: the bounded proof-search procedure. Attributes: tactic driver (Claude over MCP), tactic-attempt budget, sorry-isolation + owner-escalation path, Windows setup note.
- **MLIR/GLP dialect**: the IL target description. Attributes: four primitives with GLP-semantic definitions, progressive-lowering intent, round-trip identity criterion.
- **Shapiro-criteria mapping**: the per-criterion mandatory/advisory assignment keyed by successor seed type.
- **Interactive spec step**: the owner-confirmation exchange that gates each successor seed's metric combination.
- **Validation experiment (spike)**: a minimal, reproducible, runnable test that empirically validates a methodology claim against the real tool. Attributes: a harness, the real tool it drives (Lean 4 / MLIR / SPIN), the GLP property, IL fragment, or protocol model under test, recorded measurements (outcome + tactic count / pass-fail / SPIN verdict), committed reproduction command + tool versions.
- **Wire-protocol model (Promela/SPIN)**: the Promela specification of the front↔back request/response protocol and the SPIN-checked properties. Attributes: the Promela model, named safety properties (deadlock-freedom, no unspecified receptions), named liveness/progress property, the SPIN verdict (or counterexample trace).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of engine-separation successor seeds (#2–#16) that subsequently enter `/buildkit-specify` produce a well-formed metric-combination table using the shared template, with owner confirmation recorded.
- **SC-002**: The framework's described refinement loop matches the `optimize.py` generate/propose/evaluate/budget seam structure with zero unmatched seams.
- **SC-003**: A scan of all framework artifacts and any example code finds zero occurrences of `OPENAI_API_KEY`, `litellm`, or `openai` on a refinement/verification path.
- **SC-004**: All six verification tooling slots (ANTLR4, type/SRSW, Lean 4/Rocq, MLIR, byte-parity, Promela/SPIN) are enumerated with a tool name, a threshold shape, and a dependency pointer.
- **SC-005**: Every Shapiro criterion (committed-choice concurrency, SRSW, suspension correctness, monotone binding, three-valued unification) has an explicit mandatory/advisory mapping to successor seed types.
- **SC-006**: The Lean 4 tactic-loop sketch defines a bounded tactic budget (start 20, empirically tuned), a sorry-isolation + owner-escalation path, the Claude-as-driver (no fixed API) seam, and a Windows (WSL2/container) setup path — **and the validation experiment (FR-035) has been run against a real Lean 4 install, with a recorded outcome and tactic-attempt count for one concrete GLP property.**
- **SC-007**: The MLIR dialect spec defines all four primitives with GLP-semantic meaning and states the `decode(encode(p)) ≡ p` round-trip criterion as the primary metric for IL-touching seeds — **and the validation experiment (FR-043) has been run against real MLIR, demonstrating round-trip identity on a minimal GLP IL fragment with a recorded pass result.**
- **SC-008**: An independent reviewer can take the framework and instantiate a complete, accepted metric table for one reconciled successor seed without inventing any new format or asking how the loop or the proof obligation works.
- **SC-009**: Both validation experiments are reproducible: a reviewer can re-run each from a committed command/script against the stated tool versions and obtain the same recorded result.
- **SC-010**: The methodology's three highest-risk claims (Claude-driven bounded Lean proving; MLIR/GLP-dialect round-trip; sound front↔back wire protocol) are each backed by empirical evidence from a real-tool run, not by desk research alone.
- **SC-011**: The Promela/SPIN validation spike (FR-080) has been run against real SPIN on a minimal front↔back protocol model, reporting deadlock-freedom + progress (or a counterexample), with the checked properties named and the run reproducible.
- **SC-012**: The protocol/concurrency verification armoury (FR-078) documents at least seven tools (SPIN, TLA+, UPPAAL, nuXMV, mCRL2, FDR4, CADP), each with its paradigm, verification engine, primary strength, and best-for use case, plus seed-type selection guidance.

## Assumptions

- The 026 dossier owner-approval gate has passed (2026-06-09); the reconciliation artifacts (`SEED-RECONCILIATION-BRIEF.md`, `DECISIONS-LOG.md`, `DEFERRALS.md`, the 17 seed memos) are present on this branch and authoritative inputs.
- Ratified decisions **R8, R9, R10, R11** (and R12 for #2) bind this feature's spec; they resolve under-specifications U1 (template format) and U2 (Shapiro scope) and fix the Windows note. **Option D** (owner, 2026-06-09) sets the deliverable scope to all three artifacts + Lean 4 tactic-loop sketch + MLIR primitive spec; the owner's 2026-06-09 experimentation directive **extends** that scope with two runnable validation spikes (FR-035, FR-043) and correspondingly extends R11/DEF-B1/DEF-H1 (a real spike runs now; full proofs/infra stay at #4/#11/#12).
- This feature delivers **specification artifacts + a framework skeleton + three minimal validation spikes (Lean, MLIR, Promela/SPIN)**. It does NOT implement the ANTLR4 grammar (#12), the production MLIR infrastructure (#4), the full mechanized GLP-semantics proofs, or the complete protocol model; those remain owned by the seeds that consume the framework.
- A working **Lean 4 toolchain (WSL2/container per R10)**, **real MLIR (Python bindings)**, and **real SPIN** are prerequisites of THIS feature (the spikes need them), not only of later seeds.
- GEPA/DSPy and all LLM-in-the-loop verification run in Claude via Agent-tool seams / MCP, consistent with the project-wide no-API rule and the `codeconv-codegen-opt` precedent.

## Dependencies

- **Depends on #1** (`026-engine-review-dossier`) — this feature is branched off `026` because its inputs (the reconciliation artifacts) live there and are not yet on `develop`.
- **Pre-conditions** seeds #2–#16: each consumes the metric template, the loop discipline, the formal tooling slots, the Shapiro mapping, and (for #4/#11/#12) the Lean tactic-loop sketch and MLIR primitive spec.

## Out of Scope (deferred to anchored seeds)

> In scope here: the two **minimal validation spikes** (FR-035 Lean, FR-043 MLIR). Out of scope: everything below — the *full* implementations the spikes de-risk.

- Implementing the ANTLR4 grammar-as-verifier — owned by #12 (DEF-H1).
- Implementing the production MLIR dialect / IL codec infrastructure — owned by #4 (DEF-B1/B2/B3). *(A minimal round-trip spike is in scope; the production codec is not.)*
- The full mechanized GLP-semantics proofs in Lean/Rocq — first full instantiation before #4 (DEF-B1). *(A single-property validation proof is in scope; the full proof suite is not.)*
- The complete Promela/SPIN model of the full wire protocol / result envelope — owned by #5/#6 (DEF-A3). *(A minimal front↔back handshake model is in scope; the full protocol model is not.)*
- AutoRocq GPT-4-dependency removal — only if Rocq is chosen for an IL obligation (DEF-F-tooling).
- Pinning the Typed-Multi-level-Datalog-IR citation — at the #4/#12 spike (DEF-B2).
