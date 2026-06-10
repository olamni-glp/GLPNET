# No-API Gate — recorded result (FR-012, SC-003, US2-AC3)

The HARD project rule (`project_gepa_no_api_claude_only`; [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md)
§1; `optimize.py:6-11`): every LM-backed refinement/verification step runs **in Claude via Agent-tool
seams / MCP — never OpenAI, litellm, or `OPENAI_API_KEY`**. This file records the grep gate that
enforces it. T010 records the first pass; **T025** re-runs the same gate over all artifacts + every
spike harness at Polish and updates this file.

## Gate command

```
grep -rEi 'OPENAI_API_KEY|litellm|(^|[^a-z])openai' docs/research/repl-engine-separation/
```

## Result — T010 (block 05, 2026-06-10): **PASS**

**Executable verification paths — literally zero.** The spike harnesses, subjects, and reproduction
scripts (`spikes/{mlir,lean,spin}/`) carry **zero** matches — no API is reachable on any
refinement/verification path:

```
grep -rEi 'OPENAI_API_KEY|litellm|(^|[^a-z])openai' docs/research/repl-engine-separation/spikes/
→ (no matches)
```

**Documentation matches — all prohibitive, none an API path.** Every textual match in the framework
docs is a statement of the rule *forbidding* the API (or a metric row asserting its absence), not a
use of it. Classified, all benign:

| file:line | nature of match |
|---|---|
| `feature-definition.md:345` | states "never OpenAI / litellm" (rule) |
| `reconciliation/14-cpp-engine-feasibility.md:207` | "no OpenAI/litellm/API" (rule) |
| `reconciliation/1a-…framework.md:41,56,180` | precedent description + the `optimize.py:6-11` no-API quote (rule) |
| `reconciliation/1a-…framework.md:194` | metric row: "`OPENAI_API_KEY`, `litellm`, `openai` absent … 0 occurrences" (the gate itself) |
| `reconciliation/4-il-codec-spike.md:244` | "without any OpenAI API dependency" (rule) |
| `reconciliation/9-restore-and-resume…md:192` | "no OpenAI/litellm/OPENAI_API_KEY" (rule) |
| `reconciliation/LEAN-TACTIC-LOOP.md:62` | "never OpenAI, litellm, or `OPENAI_API_KEY`" (rule) |
| `reconciliation/REFINEMENT-METHOD.md:15` | "never OpenAI, litellm, or `OPENAI_API_KEY`" (rule) |
| `reconciliation/SEED-RECONCILIATION-BRIEF.md:18` | "never OpenAI/litellm/OPENAI_API_KEY" (rule) |
| `reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md` | "no LM sits on the verification path" (rule) |

**Verdict**: zero matches on any refinement/verification *path*; all remaining occurrences are the
rule declaring its own prohibition. No external-LM API is imported, configured, or reachable anywhere
in the framework or its spikes. Gate **PASS** (FR-012 / SC-003 / US2-AC3).
