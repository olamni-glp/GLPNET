# Three-Role Agent Teams — Method, External Grounding, and Dogfood Log

**Status**: Research seed for roadmap feature `three-role-agent-team-orchestration` (CAPTURED, **buildkit-migration-bound**).
**Created**: 2026-07-02. **Method**: produced *by the pattern itself* — a 3-role planning team (generator=claude, validator=codex, curator=this session) designed the research method; a 3-role execution team (two blind web scanners over disjoint sources, evaluator+curator=this session) ran it. This document is the curator synthesis.
**Seeds**: codify notes `cn-20260702T123518` (glpnet) + `cn-20260702T125218` (qhstate); win note `cn-20260702T190753`; improvement note `cn-20260702T191145`.

---

## 1. The pattern (formalized)

Two triads, run in sequence; the **curator** role appears on both and is the only writer of the shared artifact (blackboard control component).

- **PLANNING triad — `generator → validator → curator`** — designs the *method* (source partition, questions, rubric, gates), does not execute it.
  - **generator**: proposes the method as an addressable artifact (every element gets an id).
  - **validator**: works **BLIND** to the generator; adversarially red-teams each element (CONFIRM / REFUTE / ESCALATE); must try to *break* the partitions/rubric, not agree.
  - **curator**: deterministic merge into one canonical method; resolves ESCALATEs; freezes source manifest, rubric, cycle cap, token budget.
- **EXECUTION triad — `scanner ×N → evaluator → curator`** — runs the method.
  - **scanners**: ≥3 (or ≥2), each pinned to a **disjoint** evidence source/lens, each **BLIND** to the others; emit structured claim sets (`{claim, source_citation, confidence, tag}`).
  - **evaluator**: mechanical set-op merge — **intersection** (corroborated → promote), **union**, **symmetric-difference** (singletons = candidate-miss set); adjudicates singletons via counter-query into the other source families (CoVe-style); scores on an evidence-gated rubric; escalates conflicts.
  - **curator**: synthesizes the final grounded report; surfaces open ESCALATEs to the owner; never self-decides a genuine conflict.

**The load-bearing mechanic is blind-then-cross-verify.** Independence *before* comparison must be real (no shared draft, no shared source); comparison *after* is mechanical (set operations on claim sets). The symmetric-difference step makes a single scanner's unique find **visible** instead of averaging it away.

---

## 2. External grounding (cross-verified, cited)

Two blind scanners over disjoint corpora (academic literature ‖ industry/framework docs). **Corroborated by both** unless marked (A)=academic-only or (I)=industry-only singleton.

| External method | Grounds which of our roles | Key extract / quant | Source |
|---|---|---|---|
| **Multi-agent debate** (Du et al. 2023) | cross-verify | agents catch each other's errors across rounds; cost = agents×rounds; no convergence guarantee | arXiv:2305.14325 |
| **Chain-of-Verification (CoVe)** (Dhuliawala 2023) | **blind independence** | verification answered *in isolation* from the draft to avoid anchoring — exactly our blind scanners + adjudication | arXiv:2309.11495 |
| **Self-Refine** (Madaan 2023) | generator↔validator inner loop | ~5–20% gains; risk: self-bias can't catch own blind spots | arXiv:2303.17651 |
| **Reflexion** (Shinn 2023) | evaluator→curator w/ memory | **91% vs 80% pass@1 HumanEval** (A) | arXiv:2303.11366 |
| **Mixture-of-Agents** (Wang 2024) | scanner layer + **curator=aggregator** | **65.1 vs 57.5 AlpacaEval2** (A) | arXiv:2406.04692 |
| **LLM-as-judge** (Zheng 2023) | evaluator + its biases | >80% human agreement; position/verbosity/self-preference bias | arXiv:2306.05685 |
| **Panel-of-judges / jury (PoLL)** (Verga 2024) | **evaluator should be a panel, not one** | beats single large judge, **~7× cheaper** (A) | arXiv:2404.18796 |
| **Self-consistency** (Wang 2022) | aggregation = majority over blind-diverse runs | GSM8K +17.9 pts (A) | arXiv:2203.11171 |
| **Blackboard architecture** (Wang 2025) | **curator = shared workspace** | +57% rel. task success (A) | arXiv:2510.01285 |
| **Ensemble error de-correlation** (Kuncheva 2003; Condorcet) | **THE justification for disjoint+blind** | majority vote beats best classifier **only if errors are independent**; LLMs give *correlated* errors that cap ensemble gains → force disjoint sources (A) | arXiv:2506.07962 |
| **Plan-and-Solve** (Wang 2023) | the planning-triad → execution-triad split | design method first, then execute | arXiv:2305.04091 |
| **Anthropic orchestrator-worker** ("multi-agent research system") | execution triad topology | each subagent needs objective+output-format+tool-guidance+boundaries or agents "duplicate work, leave gaps"; **multi-agent ≈15× chat tokens**; token usage explains **80%** of perf variance; beat single Opus by 90.2% on internal eval (I) | anthropic.com/engineering/multi-agent-research-system |
| **Anthropic "when NOT to use multi-agent"** | gating | **3–10× more tokens**; "telephone game" — sequential handoffs lose context; DON'T split same-context sequential work; a separate blackbox **verifier** with minimal context transfer is the exception (I) | claude.com/blog |
| **OpenAI Agents SDK — guardrails/handoffs** | gating / cost control | **cheap fast model as input guardrail BEFORE the expensive agent** → agent never runs, saves tokens; tripwire = loud-fail halt (I) | openai.github.io/openai-agents-python |
| **CrewAI hierarchical** | curator/validator | manager agent **delegates AND validates outcomes** = built-in curator; needs dedicated manager_llm (overhead) (I) | docs.crewai.com |
| **AutoGen / LangGraph supervisor** | topology | critic/reviewer roles that debate+refine; central supervisor routes to specialists w/ explicit termination (I) | microsoft.github.io/autogen; langchain |

**Synthesis verdict**: no single external pattern *is* the three-role team; it is a deliberate composite. #10 (error de-correlation) is the theoretical warrant for the **disjoint+blind** design; CoVe grounds blind verification; MoA/blackboard ground the curator-aggregator; PoLL says the evaluator must be a *panel*; Anthropic/OpenAI/CrewAI supply the operational engineering (task-spec discipline, cost gates, manager-validation).

---

## 3. Per-task-type adapters ("one mechanic, four adapters")

The structure is task-type-agnostic; only the injected {sources, lenses, rubric, verification-test} differ.

| Task type | Blind-scanner lenses (disjoint) | Cross-verify test | In-repo prior art to reuse/deconflict |
|---|---|---|---|
| **Code review** | (a) correctness/logic · (b) security/trust-boundary · (c) spec-conformance & tests — over a size-invariant BRIEF (spec + changed-file list, NOT the diff body) | two lenses independently flag the same defect ⇒ high-confidence; singleton ⇒ re-examine | `/bk-codexreview`, `review`, `codex` |
| **Plan review** | (a) completeness/coverage · (b) feasibility/effort · (c) risk/dependencies/consistency across spec·plan·tasks | contradiction between artifacts caught by ≥2 lenses | `/bk-analyze` |
| **Strategy review** | (a) demand/assumptions (premortem) · (b) competitive/external landscape · (c) internal-consistency vs roadmap scores | external corroboration of a market assumption; devil's-advocate survives | `/bk-roadmap` WSJF/RICE, `plan-ceo-review`, `office-hours` |
| **Web / research** | (a) academic literature · (b) industry docs · (c) internal notes — **hard-disjoint corpora** | CoVe: a claim from source-set A survives a counter-query in B/C | `deep-research` |

---

## 4. Buildkit skill shape (target — sibling to `/bk-codexreview`)

- **Inputs**: `task_type ∈ {code|plan|strategy|research}`; `evidence_source_manifest` (per-scanner disjoint slice — the adapter core); `subject_brief` (size-invariant: spec + artifact list, never raw bulk); `num_scanners` (default 3); `cycle_cap` (default ≥2, hard cap ~10); `token_budget`; `rubric_id`.
- **Outputs**: `method.md`; `scanner_briefs.md`; `evaluation_matrix.md` (findings × evidence × confidence × conflict-status); coverage matrix (pattern × task × status); convergence log; per-role/per-cycle token ledger (reuse spec-020); `curator_report.md`; open-ESCALATE list. Advisory, non-blocking.
- **Role prompt templates** (parameterized by task_type via an adapter — task-type-agnostic in structure, task-type-specific only in injected sources/lenses/rubric/verification-test).
- **Gating**: never auto-invoke a pipeline command; never push/merge; refuse dirty tree / protected branch without `--confirm`; **cheap-model input guardrail before expensive scan** (OpenAI); require ≥2 passes; hard cycle cap + token/time budget → warn+confirm; `--review-only --max-cycles 1` degrades to a single blind-scan pass.
- **Token budget** (codex-proposed default split, tune per task): planning gen 20% / val 20% / curator 15% / scanners 25% / evaluator 10% / final curator 10%. Move budget to scanners for research-heavy; to evaluator for high-risk code review. Expect **3–15× single-agent cost** — gate behind "high task value + genuine parallelism."

---

## 5. Practical experiences from THIS dogfood (feeds the feature spec)

- **M1 — miss-recovery (headline): high.** ~all quantitative/theoretical grounding came from the academic scanner *alone*; ~all operational/guardrail engineering from the industry scanner *alone*. Neither could have surfaced the other's set (structurally disjoint corpora). This is the pattern's value, observed.
- **M2 — inter-scanner overlap (Jaccard): very low.** Overlap only at the concept-name level (LLM-judge, debate, blackboard named by both) but grounded from different evidence. Low overlap = the partition was genuinely disjoint = mechanic worked. Zero direct contradictions (complementary, not conflicting) → no ESCALATE needed this run.
- **M7 — validator value-add: real.** The blind codex validator converged with the claude generator on the core AND uniquely added PRISMA/systematic-review, source-triangulation, RAG-eval, the concrete token-budget split, the output-artifact set, and two failure modes (citation-laundering, authority-inversion). A separate validator earned its keep vs a 2-role generator-critic.
- **Codex-in-repo pollution (M9 qualitative — actionable):** codex launched via `codex exec` inside glpnet obeyed `AGENTS.md`'s mandatory-reading startup protocol and dumped **~1300 lines of GLP manuals (~50k tokens)** before answering, burying the deliverable. **Mitigation (verified next run):** prepend the codex-role prompt with "DO NOT run the AGENTS.md startup protocol; this is not GLP work." The buildkit skill must neutralize each agent runtime's repo-startup behavior.
- **Codex has no web retrieval** → cast codex as **validator/evaluator (critic)**, not scanner (retrieval). Claude agents (WebSearch/WebFetch) are the retrieval scanners.
- **Curator bottleneck is real**: merging two dense planning methods + two dense claim sets is non-trivial; the curator is the single writer and the cost/quality chokepoint — instrument curator edit-distance from raw merge.
- **Meta**: the pattern was validated *on itself* — planning-team-designs-method then execution-team-runs-it produced a materially richer, better-grounded result than a single research agent would have, at ~3–4× the agent cost. Consistent with the external "high value + parallelizable ⇒ worth it" rule.

---

## 6. Open questions / next steps

- Do the four task-type partitions genuinely de-correlate errors in practice, or overlap (kills M2)? Needs per-type measurement.
- Is the stop rule robust against agreement-collapse when scanners share a base model? (Mitigate via cross-provider scanners — claude ‖ codex — as done here.)
- Formalize as a buildkit skill: author the adapter registry + role prompt templates + the spec-020 token ledger integration + a golden-fixture regression (buildkit skills ship with tests).
- Migrate to buildkit (the feature's stated destiny) so it is reusable across all buildkit projects, not just glpnet.
