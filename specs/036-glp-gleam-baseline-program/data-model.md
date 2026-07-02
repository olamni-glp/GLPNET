# Phase 1 Data Model — Program Entities

These are documentation/record entities (no database; artifacts on disk). Authoritative field
detail for the seam decisions lives in
`docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/DECISIONS.md`.

- **Feature Disposition** — per not-completed feature. Fields: `id`, `name`, `epic`,
  `classification` ∈ {aligned, realign, supersede-by-beam, fold-into-gleam, keep-cross-runtime,
  drop}, `target_epic` ∈ {optional, full-gleam}, `is_critical_milestone`, `milestone` ∈
  {M1, M2, cross-runtime, harness, neither}, `contribution`, `improvement`, `blocked_by[]`,
  `enables[]`, `sequence_position`, `confidence`, `evidence[]` (file:line/page/URL). Produced by the
  re-run realignment (corrected P1); consumed by P8 synthesis.
- **Concern** — Fields: `id`, `description`, `severity`, `evidence[]`, `affected_features[]`,
  `status`. Produced by P2 (loop-until-dry).
- **Opportunity** — Fields: `id`, `beam_atomvm_capability`, `enables/simplifies`, `evidence[]`,
  `affected_features[]`. Produced by P3.
- **Faithfulness Criterion** — Fields: `id`, `level` ∈ {M1, M2}, `statement` (testable),
  `primary_source` (page/file:line), `proof_status` ∈ {proved, refuted, open}, `proof_artifact?`.
  Produced by P4.
- **Proof Artifact** — Fields: `invariant`, `tool` ∈ {Lean, SPIN, MLIR, exec-equivalence},
  `outcome`, `path`, `reproduce_cmd`. Produced by P4 (and the P5 spike precedent).
- **Established Decision (ED-n)** — a ratified, owner-approved design decision (seam/IL/ML/compiler
  placement). Fields: `id`, `statement`, `forks_resolved`, `verification`, `obligations[]`. Source:
  DECISIONS.md (ED-1…ED-6).
- **Target Epic** — {*Optional features*, *Full Gleam implementation*}. The Full-Gleam epic carries
  a valid topological order + scores; each member feature links ≥1 Faithfulness Criterion.
- **Migration Mapping** — `existing_feature_id → target_epic` + `rescope_note`. Advisory until the
  discharge gate (FR-011).
- **Research Pipeline** — Fields: `id` (P2…P8 / ANTLR-deep-dive), `phase` ∈ {A, B}, `status`,
  `script_path`, `artifact_path`, `verification_gate`. Run as a Workflow.
- **Marathon Run** — the durable, restart-safe run state (off-repo per-run store) tracking pipeline
  progress, checkpoints, and the discharge gate.
