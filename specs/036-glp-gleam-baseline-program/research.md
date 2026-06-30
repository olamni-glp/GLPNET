# Phase 0 Research — Machinery Design & Resolved Decisions

This program's "research" is the *design of the machinery* plus the decisions already resolved by
the run-so-far. The big technical unknown (the front/back seam + IL/ML) is **resolved and verified**
— see spec.md §Established Decisions and
`docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/{DOSSIER.md,DECISIONS.md}`. No
`NEEDS CLARIFICATION` remain.

## Decision: multi-agent, multi-stage pipelines with forced anti-bias rules
- **Decision**: each pipeline (P2–P8 + the ANTLR deep-dive) is a Claude Workflow of
  ground → web (where needed) → design → adversarial review → synthesis, returning text artifacts
  (no rigid output schemas).
- **Rationale**: it matches the LEJEPA/Yngenios/Beacon/MSTACK approach the owner uses; text-only
  returns avoided the structured-output failures that silently killed 4 agents in P1.
- **Anti-bias rules (root-cause fix for the P1 failure)**: cite `file:line`/page/URL for every
  claim; **no "fastest-path" rubric**; judge on separability / maintainability / analyzability /
  multi-target / faithfulness, not speed; re-ground web findings to primary sources; **never
  self-cite a synthesized rubric**; present genuine forks as owner options.
- **Alternatives rejected**: single-pass triage against one synthesized rubric (= P1; produced
  confident, ungrounded "drop" verdicts on ANTLR/IL/separation — discarded).

## Decision: corpus is inspected directly + proofs constructed
- **Decision**: agents open the actual papers/source (`GLP_IMPLEMENTATION.pdf`, `formal.tex`, the
  Dart `glp_runtime/`, the link-layer corpus) and cite page/line; load-bearing invariants get a
  constructed proof via the in-repo armoury (`docs/research/repl-engine-separation/spikes/{lean,spin,mlir}`).
- **Rationale**: FR-003/FR-004; "verified, not asserted" is what distinguishes this from a survey.
- **Alternatives rejected**: relying on memorised/second-hand summaries (forbidden by FR-003).

## Decision: the eight pipelines (the machinery)
P2 concerns register · P3 opportunities register · P4 **faithfulness-proofs (M1/M2 parity bar +
constructed proofs)** · P5 IL/ML strategy **[DONE → ED-1…ED-6, spike-verified]** · ANTLR-integration
deep-dive (best verified options; grounded by the P5 spike + qhstate `.g4` work) · P6 Gleam/AtomVM
implementation strategy · P7 QHSM/YngeniOS integration · P8 synthesis → the two epics.
- **Rationale**: covers spec FR-001…FR-009; P4 runs first in Phase B because the parity bar is
  foundational to the feature dispositions and to re-running the corrected P1 realignment.

## Decision: proof harness = reuse the in-repo armoury
- **Decision**: Lean 4 (semantic invariants, e.g. SRSW preservation, writer-MGU soundness), SPIN
  (protocol/linked-distribution), MLIR (IL round-trip), already validated in 027's spikes.
- **Rationale**: real-tool spikes already PASS there; no new external dependency; Claude-only.

## Resolved seam architecture (fixed input — full record in DECISIONS.md)
ED-1 bytecode-on-wire seam + result envelope (identical in/out of process). ED-2 keep+freeze
v2.16.3 ISA. ED-3 lightweight front-end-internal 4-primitive IL. ED-4 compiler in front-end, ANTLR
grammar (parser in C#/Dart; engine pure-Gleam). ED-5 spike PASS (byte-identical + execution-equivalent
+ verifiers fire). ED-6 open obligations (Section-15 codec + AtomVM bit-syntax spike; ISA freeze;
M2 parity ≠ ISA-identity).
