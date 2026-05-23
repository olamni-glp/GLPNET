"""Held-out eval/train dataset over checked-in plans + convspecs.

Implements ``specs/019-codeconv-codegen/contracts/metric_contract.md`` §
"GEPA wiring → Dataset": a held-out eval split over ``(plan, convspec,
dep-interfaces, expected-construct)`` tuples drawn from the ratified
``.codeconv/conversion-plans/<rel>.dart.md`` +
``.codeconv/conversion-specs/<rel>.dart.md`` artifacts.

PURE: filesystem reads only — no dspy, no bridge, no LM (so the split is
unit-testable and the optimizer converts these records to
``dspy.Example`` lazily). The split is deterministic given ``(seed,
eval_size)``: a stable hash orders the examples, then the first
``eval_size`` go to eval and the remainder to train (a ``dataset_hash``
pins provenance into the exported prompt).
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

from codeconv.tools.codegen import artefact as _artefact


PLANS_PARTS = (".codeconv", "conversion-plans")
SPECS_PARTS = (".codeconv", "conversion-specs")


@dataclass(frozen=True)
class Example:
    """One optimizer training/eval record (inputs the sub-agent receives).

    ``expected_units`` are the plan-named top-level constructs the
    generated C# must declare (the structural label
    ``artefact.validate_generated`` checks); the build gate is the real
    label.
    """

    rel_path: str
    plan_text: str
    spec_text: str
    target_cs: str
    expected_units: tuple[str, ...]

    def fingerprint(self) -> str:
        h = hashlib.sha256()
        h.update(self.rel_path.encode("utf-8"))
        h.update(self.plan_text.encode("utf-8"))
        h.update(self.spec_text.encode("utf-8"))
        return h.hexdigest()


def _read(path: Path) -> Optional[str]:
    try:
        return path.read_text(encoding="utf-8")
    except OSError:
        return None


def _expected_units(plan_text: str) -> tuple[str, ...]:
    """Extract plan-named target construct identifiers from a plan.

    Heuristic + deterministic: collect PascalCase identifiers appearing
    after ``target_code_unit`` / in the front-matter, falling back to
    none. The generated C# must contain these (validate_generated's
    ``expected_units``).
    """
    import re

    units: list[str] = []
    for m in re.finditer(r"target_code_unit\s*[:=]\s*([A-Za-z0-9_, ]+)", plan_text):
        for tok in re.split(r"[,\s]+", m.group(1).strip()):
            if re.fullmatch(r"[A-Z][A-Za-z0-9_]+", tok):
                units.append(tok)
    # de-dup, stable order
    seen: set[str] = set()
    out: list[str] = []
    for u in units:
        if u not in seen:
            seen.add(u)
            out.append(u)
    return tuple(out)


def build_examples(repo_root: Path) -> list[Example]:
    """Build the full example set from checked-in plan+spec artifact pairs.

    Only files that have BOTH a plan and a convspec artifact are
    included (a tuple needs both). Sorted by rel_path for determinism.
    """
    repo_root = Path(repo_root)
    plans_root = repo_root.joinpath(*PLANS_PARTS)
    specs_root = repo_root.joinpath(*SPECS_PARTS)
    if not plans_root.is_dir():
        return []

    out: list[Example] = []
    for plan in sorted(plans_root.rglob("*.dart.md")):
        if plan.name.startswith("_"):
            continue
        rel = plan.relative_to(plans_root).as_posix()  # <rel>.dart.md
        rel_dart = rel[: -len(".md")]  # <rel>.dart
        spec = specs_root / rel
        plan_text = _read(plan)
        spec_text = _read(spec)
        if plan_text is None or spec_text is None:
            continue
        out.append(
            Example(
                rel_path=rel_dart,
                plan_text=plan_text,
                spec_text=spec_text,
                target_cs=_artefact.produced_cs_rel(rel_dart),
                expected_units=_expected_units(plan_text),
            )
        )
    return out


def split(
    examples: list[Example],
    *,
    eval_size: int = 10,
    seed: int = 0,
) -> tuple[list[Example], list[Example]]:
    """Deterministic (train, eval) split.

    Orders examples by ``sha256(seed || fingerprint)`` then takes the
    first ``min(eval_size, len)`` as the eval (held-out) set and the rest
    as train. Identical ``(seed, eval_size)`` ⇒ identical split.
    """
    keyed = sorted(
        examples,
        key=lambda e: hashlib.sha256(
            f"{seed}:{e.fingerprint()}".encode("utf-8")
        ).hexdigest(),
    )
    k = max(0, min(eval_size, len(keyed)))
    eval_set = keyed[:k]
    train_set = keyed[k:]
    return train_set, eval_set


def dataset_hash(examples: list[Example]) -> str:
    """Stable hash of the eval/train universe (provenance for the prompt)."""
    h = hashlib.sha256()
    for e in sorted(examples, key=lambda x: x.rel_path):
        h.update(e.fingerprint().encode("utf-8"))
    return h.hexdigest()[:16]


__all__ = [
    "Example",
    "build_examples",
    "dataset_hash",
    "split",
]
