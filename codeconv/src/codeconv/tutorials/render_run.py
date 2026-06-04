"""Human + JSON rendering for the run layer (preview/run/explain/propose).

PURE / BRIDGE-FREE (D1). Stdlib ``json`` only. The JSON shape follows
``contracts/tutorials_run_cli.md``; human output is the readable rendering of the
same model. Guarded by ``test_tutorials_no_bridge.py``.
"""

from __future__ import annotations

import json as _json

from . import outcome as _oc


def _outcome_dict(o: _oc.Outcome | None):
    if o is None:
        return None
    return {
        "bindings": [{"name": b.name, "value": b.value} for b in o.bindings],
        "status": o.status.value if o.status else None,
        "kind": o.kind.value,
    }


def _load_target_dict(example):
    if not example.load_targets:
        return None
    t = example.load_targets[0]
    d = {"kind": t.kind.value, "select_path": t.select_path, "exec_path": t.exec_path}
    if len(example.load_targets) > 1:
        d["extra_exec_paths"] = [x.exec_path for x in example.load_targets[1:]]
    return d


def _base_model(example):
    return {
        "chapter": example.chapter_id,
        "exercise": example.exercise_number,
        "shape": example.shape.value,
        "supported": example.supported,
        "unsupported_reason": example.unsupported_reason,
        "load_target": _load_target_dict(example),
        "warnings": list(example.warnings),
    }


# --------------------------------------------------------------------------- #
# preview                                                                      #
# --------------------------------------------------------------------------- #
def _golden_at(example, i):
    return example.golden[i] if i < len(example.golden) else None


def preview_json(example) -> str:
    m = _base_model(example)
    m["goals"] = [
        {
            "text": g.text,
            "source": g.source.value,
            "is_primary": g.is_primary,
            "needs_limit": g.needs_limit,
            "expected": _outcome_dict(_golden_at(example, i)),
        }
        for i, g in enumerate(example.goals)
    ]
    return _json.dumps(m, indent=2)


def preview_human(example) -> str:
    lines = [f"{example.chapter_id}/exercise-{example.exercise_number}  [{example.shape.value}]"]
    if example.guide_path:
        lines.append(f"  guide: {example.guide_path}")
    if example.load_targets:
        for t in example.load_targets:
            lines.append(f"  load ({t.kind.value}): {t.exec_path}")
    if not example.supported:
        lines.append(f"  NOT RUNNABLE: {example.unsupported_reason}")
    if not example.goals:
        lines.append("  goal(s): (none resolvable — supply one with --goal to run)")
    else:
        lines.append("  goal(s) [from the tutorial .md]:")
        for i, g in enumerate(example.goals):
            tag = " (primary)" if g.is_primary else ""
            lim = f"  :limit {g.needs_limit}" if g.needs_limit else ""
            lines.append(f"    - {g.text}{tag}{lim}")
            exp = _golden_at(example, i)
            if exp is not None:
                lines.append(f"        expected: {_outcome_oneline(exp)}")
    for w in example.warnings:
        lines.append(f"  warning: {w}")
    return "\n".join(lines)


def _outcome_oneline(o: _oc.Outcome) -> str:
    if o.kind == _oc.GoldenKind.LOAD_FAILURE:
        return "load failure (diagnostic)"
    parts = [f"{b.name} = {b.value}" for b in o.bindings]
    status = f"→ {o.status.value}" if o.status else ""
    if o.kind == _oc.GoldenKind.SIDE_EFFECT:
        parts.append(f"[{len(o.side_effects)} tagged() lines]")
    return ", ".join(p for p in [*parts, status] if p) or status


# --------------------------------------------------------------------------- #
# run / explain                                                                #
# --------------------------------------------------------------------------- #
def run_json(example, result, verdicts, *, explain: bool) -> str:
    m = _base_model(example)
    m["backend_used"] = result.backend_used.value
    m["p1_notice"] = result.p1_notice
    vmap = {v.goal: v for v in (verdicts or [])}
    goals_out = []
    for goal_text, actual in result.goal_outcomes:
        v = vmap.get(goal_text)
        entry = {
            "text": goal_text,
            "actual": _outcome_dict(actual),
            "golden": _outcome_dict(v.golden) if v else None,
            "verdict": {
                "kind": v.kind.value,
                "diffs": [{"field": d.field, "actual": d.actual, "golden": d.golden} for d in v.diffs],
            } if v else None,
        }
        if explain and v:
            entry["explanation"] = v.explanation
        goals_out.append(entry)
    m["goals"] = goals_out
    return _json.dumps(m, indent=2)


def run_human(example, result, verdicts, *, explain: bool) -> str:
    lines = [f"{example.chapter_id}/exercise-{example.exercise_number}  [{example.shape.value}]  "
             f"backend={result.backend_used.value}"]
    if result.p1_notice:
        lines.append(f"  ⚠ P1: {result.p1_notice}")
    vmap = {v.goal: v for v in (verdicts or [])}
    if not result.goal_outcomes:
        lines.append(f"  (no outcome) {result.error or ''}")
    for goal_text, actual in result.goal_outcomes:
        lines.append(f"  GLP> {goal_text}")
        for b in actual.bindings:
            lines.append(f"    {b.name} = {b.value}")
        for s in actual.side_effects:
            lines.append(f"    {s}")
        if actual.status:
            lines.append(f"    → {actual.status.value}")
        v = vmap.get(goal_text)
        if v is not None:
            mark = {"match": "✓ match", "difference": "✗ DIFFERENCE", "no_golden": "· no golden"}[v.kind.value]
            lines.append(f"    [{mark}]")
            if explain:
                lines.append(f"      {v.explanation}")
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# propose                                                                      #
# --------------------------------------------------------------------------- #
def proposals_json(proposals) -> str:
    return _json.dumps({"proposals": [
        {
            "id": p.id, "kind": p.kind.value, "chapter": p.chapter_id,
            "exercise": p.exercise_number, "rationale": p.rationale,
            "target_sibling_path": p.target_sibling_path, "applied": p.applied,
        } for p in proposals
    ]}, indent=2)


def proposals_human(proposals) -> str:
    if not proposals:
        return "No proposals — corpus is consistent."
    lines = [f"{len(proposals)} proposal(s) (read-only; --apply requires --approve + --rationale):"]
    for p in proposals:
        scope = f"{p.chapter_id}" + (f"/{p.exercise_number}" if p.exercise_number else "")
        lines.append(f"  [{p.kind.value}] {scope} ({p.id})")
        lines.append(f"      {p.rationale}")
    return "\n".join(lines)


__all__ = [
    "preview_json", "preview_human", "run_json", "run_human",
    "proposals_json", "proposals_human",
]
