# `.codeconv/equiv-manifest/` (feature 020)

Checked-in, versioned authority for **subsystem classification + corpus split**
(data-model.md; contracts/subsystem_curriculum.md; R8/R9).

- `subsystems.yml` — the 5 curriculum subsystems (`heap`, `bytecode`, `compiler`,
  `runtime-core`, `multiagent`) with their tiers (`strict`/`dynamic`), path prefixes
  (validated against `codeconv.dart_depgraph`, read-only), the corpus suite assignments
  (trace = unified+book; outcome = bonds; back_test = ported unit tests), and the
  deterministic, per-source `train`/`held-out` split (~70/30). Authored in T010,
  validated by `tools/equiv/manifest.py` (T009).

Regenerated only by an explicit, reviewed step — never by a run-to-run process
(stable assignments, no wobble; SC-003 auditability).
