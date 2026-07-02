# Quickstart — Run / Resume the 036 Machinery (under the marathon)

## Where things are
- Program source of truth: `docs/research/glp-gleam-baseline/feature-definition.md`.
- Ratified architecture (fixed input): `docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/DECISIONS.md`.
- Per-pipeline artifacts: `docs/research/glp-gleam-baseline/pipelines/<P>/`.
- Verification spikes: `spike/<name>/` (e.g. `spike/p5-il-merge/`).

## Resume protocol (after any new session / compaction / crash)
1. `buildkit-roadmap next` / `status` → confirms 036 is the active feature.
2. Read its pipeline state + `tasks.md` (the WIP position).
3. `codeconv/.venv/Scripts/python.exe -m codeconv.cli marathon resume --run <run-id>` (the durable
   position derives from rows, not a summary). The marathon harness owns the cross-session checkpoint.
4. Continue from the next not-done pipeline. Phase-B order: **P4 → corrected-P1 realignment →
   (ANTLR deep-dive, P2, P3, P6, P7 in parallel where independent) → P8 synthesis → discharge gate.**

## Run a pipeline (the unit of work)
Each pipeline is a Claude **Workflow** (ground → web? → design → adversarial review → synthesis),
text-only returns, honoring `contracts/pipeline-contract.md`. After it completes, extract its
result object's fields to readable artifacts under `pipelines/<P>/` (the P1/P5 extraction is the
precedent), and record the verification outcome.

## Verification gate (per pipeline)
A pipeline is "done" only when its artifact: cites primary sources for every claim; has its
load-bearing invariants proved/refuted/open (never silently skipped); and (for design/strategy
pipelines) leaves genuine forks as owner options. Spikes additionally show real run output.

## Discharge gate (the only mutation point — FR-011)
After P8 produces the verified two-epic reconfiguration + advisory migration plan, **present to the
owner for approval**. Only on explicit approval: create the two epics and migrate the recombined
features (via `buildkit-roadmap`). Nothing on the live roadmap moves before then.

## Guardrails
Read-only on the target roadmap/specs/code and on sibling repos (`GLP`, `qhstate*`, `MSTACK`,
`olamnit`) until approval. Corpus inspected directly. Claude-only LM (no external API). Park stray
untracked files before any scoped commit (avoid `git add -A`).
