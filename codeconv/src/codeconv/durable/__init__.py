"""Centralised DBOS activation for the codeconv builder (feature 018, D1=a).

This package is the **single place** DBOS workflow/step activation lives.
The six existing tools' dormant ``register()`` no-ops are superseded by
builder-side step registration here (T015) — each tool's pure entrypoint
is wrapped *verbatim* as a ``@DBOS.step`` (D2: no tool behaviour change;
contract ``dbos_workflow_model.md``).

``__init__`` owns the **deterministic workflow-id derivation** (R9 /
dbos_workflow_model.md § Taxonomy) and a tiny registry surface. It is
import-pure: ``dbos`` is imported lazily (mirroring
``codeconv.runner.workflow``) so the workflow-id determinism tests
(T010) need no bridge and no DBOS.

Workflow-id grammar (R9 — deterministic so a re-run *recovers* the same
workflow rather than starting a new one ⇒ FR-004 / SC-002):

- outer builder : ``builder:{workspace_id}:{run_epoch}``
- per-file child: ``file:{sha(rel_path)}``
- per-SCC child : ``scc:{sha(sorted members)}``

The hash is SHA-256 (NOT Python ``hash()`` — that is per-process salted
and would break cross-process determinism), hex, truncated to 16 chars
(64 bits — collision-negligible at 128-file scale, stable across
processes/inputs as T010 asserts).
"""

from __future__ import annotations

import hashlib
from typing import Any, Callable, Iterable

_HASH_LEN = 16


def _stable_hash(value: str) -> str:
    """Process-independent short hash (SHA-256 hex, 16 chars)."""
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:_HASH_LEN]


def outer_workflow_id(workspace_id: str, run_epoch: int) -> str:
    """Deterministic outer-builder workflow id.

    ``run_epoch`` is the run's start epoch (seconds). ``builder run``
    reuses the most-recent non-terminal outer id (resume, not restart);
    ``--restart-run`` mints a new epoch explicitly (R13).
    """
    return f"builder:{workspace_id}:{int(run_epoch)}"


def file_workflow_id(rel_path: str) -> str:
    """Deterministic per-file child workflow id.

    Keyed on the feature-015 POSIX ``rel_path`` (the file's stable
    identity — never re-derived here, just hashed) so re-running recovers
    the same child workflow (DBOS dedups by workflow id).
    """
    return f"file:{_stable_hash(_norm_rel(rel_path))}"


def scc_workflow_id(members: Iterable[str]) -> str:
    """Deterministic per-SCC child workflow id.

    An SCC (import cycle) is **one indivisible unit** (FR-002): the id is
    derived from the *sorted* member rel-paths so member ordering /
    discovery order does not change the id.
    """
    norm_sorted = sorted(_norm_rel(m) for m in members)
    return f"scc:{_stable_hash(chr(10).join(norm_sorted))}"


def _norm_rel(rel_path: str) -> str:
    """Normalise to POSIX, strip leading/trailing slashes — so the id is
    invariant to incidental separator/affix differences (the 015
    rel_path is already POSIX; this only guards callers)."""
    return rel_path.replace("\\", "/").strip("/")


# --------------------------------------------------------------------------
# Tiny registry surface — populated by T011/T012 (steps/workflows) and
# consumed by T015 (each tool's register() delegates here). Kept minimal
# and DBOS-free at import time.
# --------------------------------------------------------------------------

_registered: dict[str, Callable[..., Any]] = {}


def register_step(name: str, fn: Callable[..., Any]) -> None:
    """Record a stage-step callable under ``name`` (idempotent for the
    same callable; raises on a conflicting re-registration so a wiring
    bug is loud, not silent — DISCIPLINE §1.7)."""
    existing = _registered.get(name)
    if existing is not None and existing is not fn:
        raise RuntimeError(
            f"durable step '{name}' already registered with a different callable"
        )
    _registered[name] = fn


def registered_steps() -> dict[str, Callable[..., Any]]:
    """Return a copy of the step registry (deterministic — dict preserves
    insertion order; callers that need ordering sort by name)."""
    return dict(_registered)


def reset_registry_for_tests() -> None:
    """Test helper — clears the step registry between tests."""
    _registered.clear()


__all__ = [
    "file_workflow_id",
    "outer_workflow_id",
    "register_step",
    "registered_steps",
    "reset_registry_for_tests",
    "scc_workflow_id",
]
