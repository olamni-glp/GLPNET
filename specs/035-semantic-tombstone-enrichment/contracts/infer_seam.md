# Contract: the Claude/Agent inference seam (`InferFn`)

**Feature**: 035 | Precedent: `codegen-opt` (`tools/codegen_opt/optimize.py`)
**Constitution**: V (Claude-only LM / no external API) — machine-checkable.

## Type

```python
@dataclass(frozen=True)
class InferRequest:
    rel_path: str        # subtree-relative POSIX path (e.g. "lib/compiler/codegen.dart")
    source_text: str     # the file's ACTUAL current Dart source

@dataclass(frozen=True)
class InferResult:
    purpose: str         # the file's responsibility/role — concise, bounded
    key_idea: str        # the central algorithm/mechanism — DISTINCT from purpose (FR-015)
    grounded: bool       # True ⟺ description grounded in source_text (no fabrication)
    reason: str          # short note (esp. when grounded == False)

InferFn = Callable[[InferRequest], InferResult]
```

## Injection & no-API enforcement (mirror `_require_fn`)

```python
def run_enrich(repo_root: Path, *, infer_fn: Optional[InferFn] = None, …) -> dict:
    infer = _require_fn(infer_fn, "infer_fn")   # raises RuntimeError if None
    …
```
`_require_fn` is copied in spirit from `codegen_opt/optimize.py:100-117`: when
`infer_fn is None` it raises `RuntimeError` with a message telling the operator
to drive enrichment through the `/codeconv-enrich` skill loop — it does **NOT**
fall back to any external LM API. There is no `OPENAI_API_KEY`/`litellm`/`openai`
import anywhere on this path (FR-003, SC-004).

## Seam obligations (the driving skill / Claude sub-agent)
- One Claude sub-agent per file (or a bounded batch), reading `source_text`.
- MUST ground `purpose`/`key_idea` in the actual source; MUST set
  `grounded=False` (not fabricate) when the source is trivial/generated/empty
  or it cannot describe it confidently (FR-009, edge cases).
- `key_idea` MUST be genuinely distinct from `purpose` (FR-015 / SC-005 ≥90%).
- Length-bounded output. Concrete defaults (tunable module constants in
  `tools/enrich/seam.py`, resolves B1): **`MAX_PURPOSE_CHARS = 200`** (≈ one
  line — the file's role), **`MAX_KEY_IDEA_CHARS = 320`** (≈ two lines — the
  mechanism). A non-empty result whose `purpose` or `key_idea` exceeds its cap,
  or is whitespace-only, is rejected by the tool as `low_confidence` (tombstone
  unchanged), exactly like `grounded == False`. The Claude sub-agent SHOULD aim
  well under the caps; the caps are a guard against runaway output, not a target.

## Tool-side handling of results
| Result | Tool action |
|---|---|
| `grounded=True`, non-empty, within bounds | write values, `*_source: inferred` (`enriched`) |
| `grounded=False` OR empty OR over-long | leave tombstone unchanged, record `low_confidence` + reason |
| `infer_fn` raises | leave tombstone unchanged, record `failed` + reason; continue |

## Test seam
Unit/integration tests inject a **fake** `infer_fn` (deterministic stub) — never
a network call — proving SC-004 structurally and enabling idempotence/failure
tests (`test_enrich_*`). A forced-raise stub exercises FR-010 fault isolation.
