# Contract — codegen sub-agent + human-review orchestration

Carried by `/codeconv-codegen` (justified deviation, 017/018 precedent). Python stays deterministic; only irreducibly-LLM work is in the agent layer.

## Codegen sub-agent (one per file; SCC = one coordinated batch)
**Inputs** (assembled by the skill): the real `.dart` source + sha; the ratified convspec (`.codeconv/conversion-specs/<rel>.dart.md`); the ratified plan (`.codeconv/conversion-plans/<rel>.dart.md`); the **public C# surfaces of already-generated dependencies** (from `out/csharp/`); the relevant `conversion_idioms` rows; and the **GEPA-optimized prompt** (`prompt.py`).
**Must produce**: the real, compilable C# at `out/csharp/<target>.cs` (per the plan's `target_code_unit`/conversion-units).
**Discipline (hard)**:
- Emit REAL C# (the inverse of convspec's spec-only rule). No prose-only output; no leftover Dart.
- Honor recorded conventions + idioms: `*Error` names retained; `getX→LookupX`; dependency APIs as generated (do not invent signatures).
- **Escalate-don't-guess**: a construct not faithfully derivable ⇒ emit a structured escalation (don't ship a guess) (FR-007).
- SCC members planned as one coordinated batch with sibling cross-references; downstream blocked until all members built.
- Concurrency ≤ the `--limit` (≤7 codegen Agent calls in flight).

## Human-review loop (FR-006)
- After a batch's files build, the skill selects a sample `max(3, 20% of batch)` and requests human review: a 1–5 score + free-text per sampled file (`codeconv codegen record-review`).
- `promote-batch` applies the gate (100% build + median ≥ 4/5). On fail: list blockers; retry or escalate.
- Free-text notes are carried back to the offline optimizer's dataset (reflective GEPA signal) — never used to silently rewrite production code.

## Build-feedback loop
- A `build_status=fail` file is returned to the codegen sub-agent with the parsed compiler errors for one bounded repair attempt; persistent failure ⇒ escalation (no infinite retry, no silent accept).
