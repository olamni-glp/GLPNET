# Phase 1 Data Model: Iterative Refinement & Verification Framework

**Feature**: `027-refinement-verification-framework` | **Date**: 2026-06-09

This feature has **no runtime data model** (no DB, no wire types). The "entities" here are the
**conceptual artifacts** of the methodology — the structured documents and spike records the framework
defines and that successor seeds instantiate. Each entity below is drawn verbatim-in-intent from spec.md
*Key Entities*, with its fields, validation rules, and the FR/SC it satisfies.

---

## E1. Metric-combination table  (FR-003, FR-020/021, R8)

The per-seed record of how "done" is measured.

| Field | Type | Rule |
|---|---|---|
| rows | list of metric rows | ≥1; for language/wire seeds ≥1 row with `kind = formal` |
| row.name | string | unique within the table |
| row.kind | enum {`pragmatic`, `formal`} | required |
| row.tool | string (named tool/harness) | concrete — e.g. `bash test/run_all_tests.sh`, `Lean 4`, `SPIN`, byte-parity oracle |
| row.threshold | string (measurable) | concrete + checkable — e.g. `384/384`, `decode(encode(p))≡p`, `no deadlock` |

**Validation rules.**
- A table for a **language- or wire/byte-touching** seed (#2/#4/#5/#6/#11/#12/#13/#14/#15) MUST contain at
  least one `formal` row with a named tool + measurable threshold (FR-021, AC-1).
- A **host/infra** seed (#8, #10) MAY omit the formal tier but MUST record an explicit per-Shapiro-criterion
  N/A justification (FR-021/050, R9, AC-2).
- Every row MUST carry both a concrete tool/harness AND a measurable threshold (FR-020).
- The owner-confirmed table MUST be recorded in the seed's spec **before** task generation (FR-060, AC-3).

**Relationships.** Instantiated once per successor seed; references the *Formal-tooling slot* (E3) for each
formal row and the *Shapiro-criteria mapping* (E6) for its mandatory/advisory justification.

---

## E2. Refinement loop  (FR-010–013)

The bounded, Claude-only iterate-against-a-metric cycle.

| Field | Type | Rule |
|---|---|---|
| candidate_generator_seam | Claude sub-agent (`generate_fn`) | MUST be Claude-backed; no external-API default (FR-012) |
| proposer_seam | Claude sub-agent (`propose_fn`) | GEPA reflective mutation from reflections |
| evaluator | the seed's metric combination (E1) | runs all rows to thresholds |
| budget_cap | int (metric-calls) | HARD; capped run yields best-so-far (FR-013) |
| termination | predicate | thresholds-met **AND** owner confirmation (REFINEMENT-METHOD §1), or budget exhausted |

**Validation rules.**
- Seam structure (generate / propose / evaluate / budget) MUST match `optimize.py:257–335` with zero
  unmatched seams (FR-011, SC-002).
- A budget-capped run MUST return the best-so-far candidate, never run unbounded (FR-013, AC-2).
- Zero occurrences of `OPENAI_API_KEY` / `litellm` / `openai` on any refinement path (FR-012, SC-003, AC-3).

---

## E3. Formal-tooling slot  (FR-022, SC-004)

A named verification capability available to seeds. Exactly **six** slots enumerated.

| Field | Type | Rule |
|---|---|---|
| tool_name | string | one of the six |
| threshold_shape | string | the kind of measurable bar the tool produces |
| dependency_pointer | enum {`available-now`, `pending-#N`} | which feature delivers it |

**The six slots (FR-022).**
1. **ANTLR4 grammar-as-verifier** — threshold: 100% corpus accepted + rejection-preservation — pending **#12**.
2. **In-repo type/SRSW checker** — threshold: REPL suite §B/§C/§D green — **available-now**.
3. **Lean 4 / Rocq mechanized semantics** — threshold: property proved (or `sorry`-isolated) — this feature's
   tactic-loop sketch + Lean validation spike (FR-035).
4. **MLIR logic dialect** — threshold: `decode(encode(p))≡p` — pending **#4** (minimal round-trip spike here).
5. **Byte-parity round-trip oracle** — threshold: golden-file identity — pattern at `FrameCodec.cs`.
6. **Protocol/concurrency verification armoury** — threshold: deadlock-freedom + named liveness — Promela/SPIN
   default (validation spike FR-080), mandatory for #2/#5/#6.

**Validation rule.** All six slots enumerated with name + threshold-shape + dependency-pointer (SC-004).

---

## E4. Lean 4 tactic loop  (FR-030–035)

The bounded proof-search procedure.

| Field | Type | Rule |
|---|---|---|
| tactic_driver | Claude over MCP (Lean-LSP-MCP) | model-agnostic; no fixed GPT-4 API (FR-030/073) |
| tactic_attempt_budget | int, start **20** | tuned experimental variable, not a constant (FR-031) |
| sorry_isolation_path | procedure | on exhaustion → isolate unsolved sub-goal as `sorry`, escalate as owner open obligation (FR-031) |
| windows_setup | WSL2/container note | documented AND exercised by the spike (FR-033) |
| primary_prover | `lean4` | Rocq is the documented alternative for IL/bytecode (FR-032 → DEF-F-tooling) |

**Validation rules.** Loop must: generate tactic → Lean kernel feedback over MCP → lemma retrieval/repair →
repeat (FR-030); never run unbounded; full proofs stay OFF the MVP path and gate only #4/#11/#12 (FR-034, R11).

---

## E5. MLIR/GLP dialect  (FR-040–043)

The IL target description.

| Field | Type | Rule |
|---|---|---|
| primitives | 4 named ops | `HEAD-unify`, `GUARD-test`, `BODY-spawn`, `suspend-reactivate`, each with a GLP-semantic definition (FR-040) |
| lowering_intent | description | progressive lowering (dialect → imperative targets) |
| round_trip_criterion | `decode(encode(p)) ≡ p` | **primary, deterministic** metric for IL-touching seeds (FR-041) |
| llm_restriction | Claude = structural generation only | the deterministic oracle is pass/fail (mitigates U4) |

**Validation rules.** All four primitives defined at name + GLP-semantics level (SC-007); round-trip is the
primary metric, not Claude-judged correctness (FR-041, US4-AC3); citation recorded as open (DEF-B2, FR-042).

---

## E6. Shapiro-criteria mapping  (FR-050–051, R9, SC-005)

The per-criterion mandatory/advisory assignment keyed by successor seed type.

| Criterion | Mandatory for | Advisory for |
|---|---|---|
| Committed-choice concurrency | language/wire/execution seeds (#2,#5,#6,#13) | host/infra (#8,#10) — N/A + justification |
| SRSW | #2,#5,#11,#12 | host/infra |
| Suspension correctness | #2,#5,#7,#9 | host/infra |
| Monotone variable binding | #7,#9 | host/infra |
| Three-valued unification (Success\|Suspend\|Fail) | #2,#5,#6 | host/infra |

**Validation rule.** Every one of the five criteria has an explicit mandatory/advisory mapping (SC-005);
each criterion framed as "preserve the guarantee **while** advancing the embedded-switch role" (FR-051).

---

## E7. Interactive spec step  (FR-060–061)

The owner-confirmation exchange that gates each successor seed's metric combination.

| Field | Type | Rule |
|---|---|---|
| trigger | start of a successor seed's `/buildkit-specify` | agent proposes metric table + verification tools |
| confirmation | owner amend/confirm | recorded in the seed's spec **before** task generation (AC-3) |
| pre_specify_pointer | reference | MUST surface `DECISIONS-LOG.md` + `DEFERRALS.md`; apply every in-scope R-row; action every anchored DEF-row (FR-061) |

---

## E8. Validation experiment (spike)  (FR-035/043/080, FR-070–074, SC-009)

A minimal, reproducible, runnable test that empirically validates a methodology claim against a real tool.
Three concrete instances: **Lean**, **MLIR**, **SPIN**.

| Field | Type | Rule |
|---|---|---|
| harness | runnable script (Python / Promela+run) | drives the **real** tool; LM steps via Claude/MCP only (FR-073) |
| real_tool | Lean 4 \| MLIR \| SPIN | real install, established as a prerequisite of THIS feature (FR-072) |
| subject | GLP property \| IL fragment \| protocol model | minimal (one property / one fragment / one handshake) (FR-074) |
| recorded_measurements | result file | outcome + (tactic count \| pass-fail \| SPIN verdict) |
| reproduction | committed command + pinned tool versions | a reviewer can re-run and get the same result (FR-071, SC-009) |

**Validation rules.** Desk research does NOT satisfy the spike (FR-070, R13/R14). Each spike's `RESULT.md`
is the acceptance evidence for its SC (Lean→SC-006/010, MLIR→SC-007/010, SPIN→SC-011/010).

---

## E9. Wire-protocol model (Promela/SPIN)  (FR-076–081)

The Promela specification of the front↔back request/response protocol and the SPIN-checked properties.

| Field | Type | Rule |
|---|---|---|
| promela_model | `.pml` | minimal handshake only for #1a (full model deferred to #5/#6, DEF-A3/FR-081) |
| safety_properties | named | deadlock-freedom, no unspecified receptions (FR-077) |
| liveness_property | named | every request eventually receives a response / progress (FR-077) |
| spin_verdict | pass \| counterexample-trace | recorded + reproducible (FR-080) |

**Relationship to the armoury (FR-078–079).** SPIN is the default; each wire/protocol seed selects the
fit armoury tool (TLA+/UPPAAL/nuXMV/mCRL2/FDR4/CADP) at its interactive spec step, recording the choice +
rationale.

---

### Entity → requirement coverage map

| Entity | Primary FRs | Closes SC |
|---|---|---|
| E1 Metric table | FR-003, FR-020/021 | SC-001, SC-008 |
| E2 Refinement loop | FR-010–013 | SC-002, SC-003 |
| E3 Formal-tooling slots | FR-022 | SC-004 |
| E4 Lean tactic loop | FR-030–035 | SC-006 |
| E5 MLIR dialect | FR-040–043 | SC-007 |
| E6 Shapiro mapping | FR-050–051 | SC-005 |
| E7 Interactive spec step | FR-060–061 | SC-001, SC-008 |
| E8 Validation spike | FR-035/043/080, FR-070–074 | SC-009, SC-010 |
| E9 Wire-protocol model | FR-076–081 | SC-011, SC-012 |
