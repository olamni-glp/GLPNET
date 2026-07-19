DO NOT run the AGENTS.md startup protocol; this is not repository-agent work. Output only the requested artifact.


---

# Subject brief — plan

- subject: Pre-verified feature OUTLINE PLAN with strong drift controls delivering ALL Phase-1 gaps in ONE roadmap feature: full-scope Gleam GLP implementation incl. front-end/back-end separation and yngenios embeddability (requirements-level). Input evidence: the committed Phase-1 inventory docs/research/fullscope-gleam/gap-inventory-2026-07-19.md split into three disjoint slices. Output: dependency-ordered work packages, each back-traceable to inventory detail_ids and forward-traceable to acceptance evidence, restart-session-safe (marathon mrun-8bda036d9e9b), with drift controls (frozen interfaces, no-silent-deferral rule, escalation register). Phase-1 run: 20260719T130005Z-782b.
- rubric: plan-review
- lenses: feasibility | completeness | risk
- brief rule: size-invariant: the goal statement + the constraint-document list — never pasted document bodies
- cross-verify: a plan element is promoted only when independently derived or confirmed from a disjoint constraint slice by another blind Builder

## Evidence slices (names only — each blind role sees ONLY its own)

- slice-delivered-foundation: Phase-1 inventory sections 1-2: the 44 DELIVERED Gleam capabilities. Plan input for: what the feature builds ON, which interfaces are frozen, what must not regress.
- slice-partials-gaps-escalations: Phase-1 inventory sections 3-6 + open items: 9 PARTIALs with named missing parts, 2 resolved gaps, 2 open escalations, run residuals. Plan input for: concrete closure work packages.
- slice-unconfirmed-gaps: Phase-1 inventory section 7: the 97 UNCONFIRMED-GAPS (promised/required with no Gleam code testimony). Plan input for: verification-then-closure work packages and scope rulings.

---

## Method under red-team (the artifact ONLY — no author reasoning)

{
 "elements": [
  {
   "id": "E1",
   "kind": "procedure",
   "text": "WP claim schema. Every claim is one work package (WP) with: wp_id — kebab-case, deterministic naming rule '<kind-prefix>-<subsystem-noun>' where kind-prefix maps freeze→'freeze', guard→'guard', verify→'verify', close→'close', build→'build', rule-request→'rule'; subsystem-noun is the inventory's subsystem name lowercased/kebabed (e.g. 'verify-module-system', 'close-bytecode-runner-opcodes', 'build-fe-be-process-split', 'freeze-frame-codec-interface', 'guard-unified-repl-suite', 'rule-mesh-ring-escalation'). Determinism matters: two blind builders naming the same plausible WP must collide on the same wp_id. Fields: kind ∈ {freeze,guard,verify,close,build,rule-request}; backing_detail_ids[] — ≥1 inventory detail_id per WP (back-traceability is mandatory; a WP with zero detail_ids is invalid); deliverable — one sentence stating the artifact/behavior produced; acceptance_evidence — the exact test path, command, or checked-in artifact a fresh restart session runs/inspects to PROVE the WP done (never 'as discussed' or conversational state); depends_on[] — wp_ids, which MAY reference plausible WPs another builder should emit, constructed via the same naming rule (dangling refs are surfaced at merge, not fabricated around); wave 1–5 — suggested semantics: wave 1 = freeze + guard (frozen interfaces registered, existing suites pinned green), wave 2 = verify (unconfirmed gaps confirmed present/absent) + rule-request filings, wave 3 = close (PARTIAL missing parts + confirmed gaps on the critical path), wave 4 = build (FE/BE front-end/back-end process split + yngenios embeddability requirements-level packages), wave 5 = integration + whole-feature acceptance evidence; effort ∈ {S,M,L}; risk — one-sentence note on what could invalidate the WP or force re-scoping."
  },
  {
   "id": "E2",
   "kind": "procedure",
   "text": "Coverage rule (no-silent-deferral). Each builder must account for 100% of the detail_ids in its own slice: every detail_id appears in ≥1 WP's backing_detail_ids OR in an explicit 'out-of-scope-proposed' entry with a stated reason (duplicate-of:<detail_id>, superseded, post-feature-follow-on, external-dependency, or already-covered-by:<wp_id>). The coverage_map {detail_id → wp_ids | 'out-of-scope: <reason>'} is a mandatory top-level output every cycle; a missing or partial coverage_map fails the cycle. Out-of-scope is always a PROPOSAL for the engineer's ruling, never a decision — the merge carries proposals forward verbatim."
  },
  {
   "id": "E3",
   "kind": "procedure",
   "text": "Drift controls the merged plan must carry as first-class artifacts: (a) frozen-interface register — builder-1 derives from the 44 DELIVERED capabilities the list of interfaces/contracts (codecs, wire formats, module boundaries, test-visible APIs) that the feature builds ON and must not change; each register entry becomes or backs a freeze-kind WP; (b) regression-guard WPs — builder-1 emits guard-kind WPs pinning every existing test suite that exercises delivered capabilities to stay green across all waves, with the suite's invocation command as acceptance evidence; (c) escalation register — the 2 open escalations (multiagent-runtime, mesh-ring) become rule-request WPs owned by builder-2, carried forward in an escalation register until the engineer rules; no builder may resolve, absorb, or drop an escalation silently; new conflicts found at merge append to the same register; (d) restart-safety — every WP's acceptance_evidence must be checkable from a fresh session with zero conversational memory (a command to run, a file/artifact path to inspect, a marathon-tracked checkpoint row); any WP failing this test is invalid and must be tightened in cycle 2; (e) single-feature constraint — all WPs compose exactly ONE roadmap feature with internal waves 1–5; no WP may propose splitting into multiple roadmap features; work judged too large for the feature goes to out-of-scope-proposed with reason 'post-feature-follow-on', never to a phantom second feature."
  },
  {
   "id": "E4",
   "kind": "procedure",
   "text": "Builder role emphasis by slice (blind — each builder sees ONLY its own slice, never another slice or merge output): builder-1 (slice-delivered-foundation, sections 1–2, 44 DELIVERED) emits primarily freeze + guard WPs, the frozen-interface register, and reuse notes identifying which delivered capabilities are the dependency spine for downstream build WPs; builder-2 (slice-partials-gaps-escalations, sections 3–6 + open items) emits close WPs for each of the 9 PARTIALs' named missing parts, confirms the 2 resolved gaps need no WP (or a guard WP if regression-prone), and emits exactly 2 rule-request WPs for the open escalations; builder-3 (slice-unconfirmed-gaps, section 7, 97 UNCONFIRMED-GAPS) emits verify-first WPs (cheap existence checks batched by subsystem), paired conditional close WPs ('close-<subsystem>' depends_on 'verify-<subsystem>'), rule-request WPs where scope is genuinely ambiguous, and the bulk of out-of-scope proposals. Builders may reference plausible cross-builder WPs in depends_on via the E1 naming rule; they must never invent detail_ids outside their slice."
  },
  {
   "id": "E5",
   "kind": "procedure",
   "text": "Verification cycle (cycle 2). Each builder re-checks its OWN cycle-1 output with no merge-derived information: self-select the weakest WPs — any WP missing concrete acceptance_evidence, any slice detail_id absent from the coverage_map, any depends_on naming a WP that the E1 naming rule says should not exist (wrong prefix, wrong subsystem noun), any wave/kind mismatch per E1 semantics, any WP violating restart-safety (E3d) or the single-feature constraint (E3e). Actions: tighten (rewrite acceptance_evidence to a runnable command/path), retract (list retracted claim_ids in 'retractions' with reason), re-map (complete the coverage_map to 100%), and re-issue corrected claims with new claim_ids. Cycle-2 output uses the same E7 contract with cycle:2; unchanged claims are re-stated by claim_id reference only to conserve budget."
  },
  {
   "id": "E6",
   "kind": "procedure",
   "text": "Merge contract (mechanical, no judgment). (1) Join all builders' final claims by exact wp_id: identical wp_id from different builders → merge-candidate pair surfaced side-by-side, never silently unified. (2) Join by backing_detail_id overlap: two WPs from different builders backing the same detail_id → overlap-candidate, surfaced with both claims verbatim. (3) Resolve every cross-builder depends_on by exact wp_id match against the union; unmatched refs → 'dangling-deps' list (not an error — evidence of a planning gap for synthesis). (4) Coverage union computed mechanically across the three coverage_maps; any inventory detail_id absent from the union → 'uncovered' list. (5) Conflicts — same detail_id planned as close/build by one builder and out-of-scope-proposed by another, or a freeze WP colliding with a build WP on the same interface → ESCALATE: append to the escalation register for the engineer, never auto-resolve. (6) Wave ordering: final wave assignment = max over duplicate proposals unless a depends_on edge forces later; cycles in the dependency graph → ESCALATE. Synthesis then produces the single dependency-ordered wave plan, the frozen-interface register, the escalation register, the out-of-scope-proposed list, and the full detail_id→WP traceability table."
  },
  {
   "id": "E7",
   "kind": "output-contract",
   "text": "Per-builder output, one JSON object per cycle, persisted to the run dir as builder-N.cycle-n.json: {\"builder\":\"builder-N\",\"cycle\":n,\"claims\":[{\"claim_id\":\"bN-cM-###\" (zero-padded, unique per builder+cycle),\"wp_id\":\"...\",\"kind\":\"freeze|guard|verify|close|build|rule-request\",\"backing_detail_ids\":[\"...\"],\"deliverable\":\"one sentence\",\"acceptance_evidence\":\"test path/command/artifact checkable from a fresh session\",\"depends_on\":[\"wp_id\"],\"wave\":1,\"effort\":\"S|M|L\",\"risk\":\"one sentence\",\"statement\":\"one sentence\",\"claim\":\"<wp_id> — <one sentence>\",\"source_citation\":\"<slice file section/detail_id anchors>\",\"confidence\":0.0,\"builder_id\":\"builder-N\",\"slice_id\":\"slice-...\"}],\"coverage_map\":{\"<detail_id>\":\"<wp_id[,wp_id]> | out-of-scope: <reason>\"},\"retractions\":[{\"claim_id\":\"...\",\"reason\":\"...\"}]}. Claims not conforming to the schema are rejected at merge and listed as malformed, not repaired."
  },
  {
   "id": "E8",
   "kind": "rubric",
   "text": "plan-review rubric applied at merge/synthesis and self-applied by builders in cycle 2. Per-WP: (R1) back-traceable — ≥1 valid slice detail_id; (R2) forward-traceable — acceptance_evidence is a concrete runnable command, test path, or artifact path; (R3) restart-safe — evidence checkable with zero conversational memory, marathon-trackable; (R4) dependency-sound — depends_on refs conform to the E1 naming rule and form no cycle; (R5) wave-coherent — kind↔wave per E1 semantics (freeze/guard early, verify before its paired close, build after its spine dependencies); (R6) right-sized — S/M/L honest, L WPs carry a risk note naming the split point if they slip. Plan-level: (R7) 100% coverage union, (R8) zero silent deferrals (every unplanned detail_id has an explicit out-of-scope proposal), (R9) both escalations present as rule-request WPs, (R10) single-feature composition, (R11) FE/BE split + yngenios embeddability reachable via an explicit dependency spine from wave-1 frozen foundation."
  },
  {
   "id": "E9",
   "kind": "budget",
   "text": "Budget: 3 builders, exactly 2 cycles, 350k-token hard cap for the whole run. Allocation: 90k per builder in cycle 1, 20k per builder in cycle 2, 30k for merge + synthesis. Each cycle's outputs are persisted to the run dir before the next role starts (restart-safe: a fresh session resumes from the last persisted cycle file, never from memory). At the cap: hard stop, persist whatever is complete, and record unprocessed detail_ids and unmerged claims in an 'open-items' file appended to the escalation register — never silently truncate coverage."
  }
 ],
 "source_partition": {
  "slice-delivered-foundation": "builder-1",
  "slice-partials-gaps-escalations": "builder-2",
  "slice-unconfirmed-gaps": "builder-3"
 },
 "questions": [
  "What is the minimal dependency spine from the 44 delivered capabilities to the FE/BE process-split and yngenios-embeddability build WPs — which frozen interfaces lie on that spine and must be registered in wave 1?",
  "For each of the 97 unconfirmed gaps, what cheap existence check decides verify-first (capability may already exist undocumented) versus build-first (known absent) — and which subsystem batches let one verify WP cover many detail_ids?",
  "What concrete acceptance evidence proves each wave complete from a fresh restart session — which test suites, commands, or checked-in artifacts, and where do marathon checkpoints record them?",
  "Which detail_ids should be proposed out-of-scope of this single feature, and under which stated reason (duplicate, superseded, post-feature-follow-on, external-dependency) — given the no-silent-deferral rule forbids simply omitting them?",
  "Which of the 9 PARTIALs' named missing parts sit on the FE/BE-split critical path (must close in wave 3) versus which can run parallel or late without blocking wave-4 build WPs?",
  "What exactly do the two open escalations (multiagent-runtime, mesh-ring) block in this plan, and what is the minimal engineer ruling each rule-request WP must obtain to unblock its dependents?",
  "Which delivered capabilities need dedicated regression-guard WPs versus which are already exercised by existing green suites that a single pinned guard WP can cover?"
 ],
 "rubric_id": "plan-review"
}