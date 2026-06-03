# `.codeconv/codegen-prompt/` (feature 020)

Checked-in **per-subsystem codegen prompt artifacts** — the durable output of the
OFFLINE `dspy.GEPA` optimizer and the resume point for any re-optimization
(data-model.md; contracts/gepa_optimizer.md).

- `_base.md` — the shared optimized base prompt (the carry-forward seed every
  per-subsystem prompt descends from).
- `heap.md`, `bytecode.md`, `compiler.md`, `runtime-core.md`, `multiagent.md` —
  per-subsystem prompts. Authored/optimized in T036; selected in production by
  `tools/codegen/prompt.py:load(subsystem)` (T034), which imports NO LM/dspy.

Each file carries **provenance front-matter**: optimizer (`dspy.GEPA`), held-out
`metric_score`, dataset/manifest hash, model, generated-at, and the `_base.md`
hash it descends from. THIS file (not in-memory GEPA state) is the durable
artifact — a killed optimizer run resumes from the last frozen prompt here.
