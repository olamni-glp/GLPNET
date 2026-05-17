"""Deterministic frontier driver — feature 018, T020 (US1).

Consumes feature-015 ``codeconv.dart_depgraph`` **read-only** (it MUST
NOT recompute order/SCC/status — 015 is the backbone, FR-002) and emits
the ordered list of work units in topo + SCC order via the pure,
already-tested :func:`codeconv.durable.workflows.plan_units`. SCC = one
indivisible unit (FR-002). Pure-shaped: the DB read is one function;
the ordering itself is delegated to the tested pure planner so this
module has no bespoke ordering logic to get wrong.
"""

from __future__ import annotations

from typing import Any, Optional

from codeconv.durable.workflows import plan_units


def read_depgraph_order(engine: Any) -> tuple[list[str], dict[str, list[str]]]:
    """Read the 015 canonical order + SCC groups, READ-ONLY.

    Returns ``(topo_order, scc_groups)`` where ``topo_order`` is the
    dependency order (``ORDER BY topo_level, path``) and ``scc_groups``
    maps ``cycle_group_id`` (as str) → member rel-paths for groups with
    >1 member (singletons are plain files). NEVER recomputes — a single
    ``SELECT`` over ``codeconv.dart_depgraph`` (same columns the
    feature-015/-017 readers use: ``path, topo_level, cycle_group_id``).
    """
    from sqlalchemy import text

    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT path, topo_level, cycle_group_id "
                "FROM codeconv.dart_depgraph "
                "ORDER BY topo_level, path"
            )
        ).all()
    topo_order = [r[0] for r in rows]
    by_gid: dict[str, list[str]] = {}
    for path, _lvl, gid in rows:
        by_gid.setdefault(str(gid), []).append(path)
    scc_groups = {g: sorted(m) for g, m in by_gid.items() if len(m) > 1}
    return topo_order, scc_groups


def compute_frontier(engine: Any) -> list[dict[str, Any]]:
    """The ordered units the outer workflow will launch (FR-002).

    Pure planner over the read-only 015 order — identical inputs ⇒
    identical units (determinism underpins FR-004/SC-002 resume).
    """
    topo_order, scc_groups = read_depgraph_order(engine)
    return plan_units(topo_order, scc_groups)


def is_empty_subtree(engine: Any) -> bool:
    """True iff there is no in-scope work (drives the FR-020
    'nothing to convert', exit 0 path)."""
    from sqlalchemy import text

    with engine.connect() as conn:
        n = conn.execute(
            text("SELECT count(*) FROM codeconv.dart_depgraph")
        ).scalar()
    return not (n and n > 0)


__all__ = ["compute_frontier", "is_empty_subtree", "read_depgraph_order"]
