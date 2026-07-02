# Contract — Research Pipeline + Discharge Gate

The program exposes no software API; its "contracts" are the rules every pipeline run must satisfy
and the gate that governs the only mutation.

## Pipeline contract (every P2…P8 + the ANTLR deep-dive)

**Inputs (required grounding):** the named primary sources for the pipeline's question — the GLP
corpus (`GLP_IMPLEMENTATION.pdf`, `Art-of-GLP-2025/formal.tex`, the Dart `glp_runtime/`), the
in-repo research corpus, and (read-only) the relevant sibling repo. Web research only where on-disk
material is insufficient.

**Process (forced):**
- Multi-stage: ground → web(if needed) → ≥2 independent design/analysis lenses → adversarial review
  → synthesis. Text-only agent returns (no rigid schemas).
- **Cite `file:line` / page / URL for every claim.** If unread, do not assert.
- **No "fastest-path" rubric.** Judge on separability, maintainability, analyzability, multi-target
  reach, faithfulness, Gleam/AtomVM fit.
- Re-ground every web finding against a primary source. Never self-cite a synthesized rubric.
- Construct a **proof** (Lean/SPIN/MLIR/exec-equivalence) for each load-bearing invariant; record
  outcome proved | refuted | open — never silently skipped.
- Respect the Established Decisions (ED-1…ED-6) as fixed inputs.

**Output:** a verified artifact under `docs/research/glp-gleam-baseline/pipelines/<P>/`: every claim
cited; genuine forks presented as owner options (not self-decided); for the synthesis pipeline,
scored + topologically-ordered features each tied to ≥1 faithfulness criterion.

**Verification gate:** the artifact is accepted only when the above hold. A pipeline that surfaces a
faithfulness gap (refuted/unprovable invariant) raises it as a first-class finding, not an omission.

## Discharge-gate contract (FR-011 — the only mutation)

The actual epic migration (create *Optional features* + *Full Gleam implementation*; move the
recombined features) happens **only after explicit owner approval** of the P8 synthesis, as the
marathon discharge action. Before approval the program is read-only on the target roadmap, specs,
code, and all sibling repos (FR-010). The discharge is recorded with the owner's informed consent.
