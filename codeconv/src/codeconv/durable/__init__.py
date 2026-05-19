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
- per-file child: ``file:{sha(rel_path)}``      (logical unit identity)
- per-SCC child : ``scc:{sha(sorted members)}`` (logical unit identity)

Amendment 2 (facet-3 — agent-gate split, FR-044). The single per-unit
child is decomposed into two distinct DBOS workflows so the agent gate
is traversable (a child that returns on the ``needs_agent_work``
sentinel is terminally SUCCESS and its checkpointed step never re-runs;
the old "re-drive recovers the same child and the step now finds the
artifact" was infeasible):

- pre-agent  : ``file:pre:{sha(rel)}``  / ``scc:pre:{sha(members)}``
- post-agent : ``file:post:{sha(rel)}:{art_digest}`` /
  ``scc:post:{sha(members)}:{art_digest}``

The post-agent id is **content-addressed** by the agent-written
artifact (``art_digest`` = the same stable hash over the artifact
text; for an SCC, over the sorted members' per-artifact digests). Thus
the post wf does not exist until the spec does, a re-spec ⇒ a new id
⇒ a fresh ingest, and resume within the same artifact recovers
identically (SC-002). R3/FR-003/FR-004 preserved (pre id deterministic
and recovered; each sub-wf's completed steps still skipped).

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


def artifact_digest(text: str) -> str:
    """Stable content digest of an agent-written convspec artifact —
    the post-agent workflow's content-address (Amendment 2 / FR-044).
    Same SHA-256/16-hex rule as the id hashes so it is process- and
    input-stable (T060 asserts)."""
    return _stable_hash(text)


def combined_artifact_digest(digests: Iterable[str]) -> str:
    """Content-address for an SCC post-agent wf: a single digest over
    the *sorted* member artifact digests (FR-002 — the SCC is one
    indivisible unit; member discovery order must not change the id)."""
    return _stable_hash(chr(10).join(sorted(digests)))


def pre_file_workflow_id(rel_path: str) -> str:
    """Pre-agent per-file wf id — deterministic, recovered on resume
    (R9/FR-003/FR-004). Stages: scaffold + convspec(analysis/started).
    Completes terminally; the ``needs_agent_work`` sentinel is surfaced
    by the outer aggregation, not by leaving this wf non-terminal."""
    return f"file:pre:{_stable_hash(_norm_rel(rel_path))}"


def pre_scc_workflow_id(members: Iterable[str]) -> str:
    """Pre-agent per-SCC wf id (one indivisible unit, FR-002)."""
    norm_sorted = sorted(_norm_rel(m) for m in members)
    return f"scc:pre:{_stable_hash(chr(10).join(norm_sorted))}"


def post_file_workflow_id(rel_path: str, art_digest: str) -> str:
    """Post-agent per-file wf id — content-addressed by the artifact
    (Amendment 2 / FR-044). Launched by the outer ONLY once the
    artifact exists; a re-spec ⇒ different ``art_digest`` ⇒ a fresh
    deterministic wf that ingests the new spec and runs plan."""
    return f"file:post:{_stable_hash(_norm_rel(rel_path))}:{art_digest}"


def post_scc_workflow_id(members: Iterable[str], art_digest: str) -> str:
    """Post-agent per-SCC wf id — content-addressed by the SCC's
    combined artifact digest; launched only when EVERY member's
    artifact exists (FR-002)."""
    norm_sorted = sorted(_norm_rel(m) for m in members)
    return f"scc:post:{_stable_hash(chr(10).join(norm_sorted))}:{art_digest}"


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
    global _activated
    _activated = None


# --------------------------------------------------------------------------
# Activation entrypoint — the single delegation target for every tool's
# register() (T015). Idempotent; binds steps + workflows ONCE per process
# after the CLI has launched DBOS. Importing this does NOT change any
# tool's run_* behaviour (D2 hard gate) — it only wires the builder-side
# durable layer; the tools' own CLI paths still call run_* directly.
# --------------------------------------------------------------------------

_activated: Any = None


def activate(dbos_app: Any) -> Any:
    """Bind the durable step + workflow layer to the launched DBOS.

    Called by every tool's ``register(dbos_app)`` (T015). The first call
    imports :mod:`codeconv.durable.steps` (which registers the stage
    wrappers), decorates them with ``dbos_app.step()``, then decorates
    the outer/child bodies with ``dbos_app.workflow()``. Subsequent calls
    return the cached binding (idempotent — DBOS dedups by qualified
    name; six tools all delegating here is one activation, not six).
    Returns ``{"steps": {...}, "workflows": {...}}`` for the builder
    tool (T021) to drive.
    """
    global _activated
    if _activated is not None:
        return _activated
    from codeconv.durable import steps as _steps  # registers wrappers
    from codeconv.durable import workflows as _workflows

    bound_steps = _steps.bind_dbos_steps(dbos_app)
    bound_workflows = _workflows.bind_dbos_workflows(dbos_app, bound_steps)
    _activated = {"steps": bound_steps, "workflows": bound_workflows}
    return _activated


__all__ = [
    "activate",
    "artifact_digest",
    "combined_artifact_digest",
    "file_workflow_id",
    "outer_workflow_id",
    "post_file_workflow_id",
    "post_scc_workflow_id",
    "pre_file_workflow_id",
    "pre_scc_workflow_id",
    "register_step",
    "registered_steps",
    "reset_registry_for_tests",
    "scc_workflow_id",
]
