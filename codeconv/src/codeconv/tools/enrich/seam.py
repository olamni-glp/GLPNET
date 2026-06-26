"""The Claude/Agent inference seam for ``codeconv enrich`` (feature 035).

Per ``specs/035-semantic-tombstone-enrichment/contracts/infer_seam.md`` and
research R-004. **LM inference runs IN CLAUDE — never an external API.**

The tool fills blank tombstone ``purpose``/``key_idea`` by inferring them
from a file's actual Dart source through an *injected* callable
(:data:`InferFn`). The callable MUST be supplied by the caller — the
``/codeconv-enrich`` skill loop injects a Claude-backed one (one sub-agent
per file, reading ``source_text``). A bare in-process run with no injected
``infer_fn`` is a usage error (:func:`_require_fn` raises ``RuntimeError``);
there is **NO** external-LM-API-key / third-party-LM-SDK fallback anywhere
on this path (Constitution V / FR-003 / SC-004 — the SC-004 grep guard
forbids the literal external-LM-provider tokens in this tree, so this
module names them only obliquely). The shape of :func:`_require_fn` mirrors
``tools/codegen_opt/optimize.py:100-117`` — the ratified no-API precedent.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Optional


# Length caps (analyze B1). Concrete, tunable module constants — a guard
# against runaway seam output, NOT a target. A non-empty result whose
# ``purpose`` or ``key_idea`` exceeds its cap is rejected by the tool as
# ``low_confidence`` (tombstone left unchanged), exactly like
# ``grounded == False``. The Claude sub-agent SHOULD aim well under these.
MAX_PURPOSE_CHARS: int = 200    # ≈ one line — the file's role
MAX_KEY_IDEA_CHARS: int = 320   # ≈ two lines — the central mechanism


@dataclass(frozen=True)
class InferRequest:
    """Input to the seam: a file's subtree-relative path + its CURRENT source."""

    rel_path: str        # subtree-relative POSIX path, e.g. "lib/compiler/codegen.dart"
    source_text: str     # the file's ACTUAL current Dart source


@dataclass(frozen=True)
class InferResult:
    """Output of the seam: a grounded purpose + a DISTINCT key_idea."""

    purpose: str         # the file's responsibility/role — concise, bounded
    key_idea: str        # the central algorithm/mechanism — DISTINCT (FR-015)
    grounded: bool       # True ⟺ grounded in source_text (no fabrication)
    reason: str          # short note (esp. when grounded == False)


# An injected, Claude-backed callable. None ⇒ usage error (no API default).
InferFn = Callable[[InferRequest], InferResult]


def _require_fn(fn: Optional[InferFn], name: str) -> InferFn:
    """Require a Claude-backed inference callable — there is no API default.

    ``enrich`` performs its LM inference IN CLAUDE (sub-agents via the
    Agent tool / the ``/codeconv-enrich`` skill loop), never via an
    external LM API. The callable MUST be injected by the caller; a bare
    in-process run (or a bare ``codeconv enrich run`` CLI invocation) with
    no injected ``fn`` is a usage error (NOT a silent OpenAI fallback) —
    the CLI catches this ``RuntimeError`` and exits 2.
    """
    if fn is None:
        raise RuntimeError(
            f"{name} was not provided. codeconv enrich runs its inference "
            "in Claude (sub-agents) — there is NO external-API default "
            "(no external-LM API key, no third-party LM SDK). Drive "
            "enrichment through the /codeconv-enrich skill loop, which "
            f"injects a Claude-backed {name}."
        )
    return fn


__all__ = [
    "InferFn",
    "InferRequest",
    "InferResult",
    "MAX_KEY_IDEA_CHARS",
    "MAX_PURPOSE_CHARS",
    "_require_fn",
]
