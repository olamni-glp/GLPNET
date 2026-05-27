"""Subsystem manifest loader + validator (FR R8/R9).

PURE, no I/O beyond reading the checked-in YAML; no bridge, no LM. The
authoritative classification + corpus split lives in
``.codeconv/equiv-manifest/subsystems.yml`` (checked in, versioned). This
module parses it and validates it against an INJECTED inventory (the Dart
files from ``codeconv.dart_depgraph`` and the in-scope GLP sources) — the
caller supplies those read-only sets, so validation is unit-testable
without a runtime (contracts/subsystem_curriculum.md; data-model.md).

Validation obligations (subsystem_curriculum.md):
  1. every ``path_prefix`` resolves to ≥1 inventoried Dart file;
  2. every in-scope Dart file is classified by exactly ONE subsystem;
  3. every in-scope GLP source is assigned to a split (train|held-out) exactly once;
  4. the realized train/held-out ratio is within tolerance of the declared ratio.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional

import yaml


# Default tolerance on the realized train/held-out split ratio (R9 — the
# split is deterministic per-source, so this only guards gross mis-authoring).
DEFAULT_RATIO_TOLERANCE: float = 0.05


@dataclass(frozen=True)
class Subsystem:
    name: str
    tier: str  # 'strict' | 'dynamic'
    path_prefixes: tuple[str, ...]


@dataclass(frozen=True)
class Manifest:
    version: int
    subsystems: tuple[Subsystem, ...]
    corpus: dict
    split_ratio: dict  # {'train': float, 'held_out': float}
    assignments: dict[str, str]  # source_path -> 'train' | 'held-out'

    def classify(self, dart_path: str) -> tuple[Optional[str], bool]:
        """Resolve ``dart_path`` to a subsystem by LONGEST matching prefix.

        Prefixes overlap by design (``heap`` = ``lib/runtime/heap_fcp*`` lives
        inside ``runtime-core`` = ``lib/runtime/``); the most specific prefix
        wins. Returns ``(subsystem_name | None, tied)`` where ``tied`` is True
        iff two DIFFERENT subsystems share the same longest matching prefix
        length (a genuine ambiguity → mis-authoring).
        """
        best_len = -1
        winners: set[str] = set()
        for sub in self.subsystems:
            for pre in sub.path_prefixes:
                if dart_path.startswith(pre):
                    if len(pre) > best_len:
                        best_len, winners = len(pre), {sub.name}
                    elif len(pre) == best_len:
                        winners.add(sub.name)
        if not winners:
            return None, False
        if len(winners) > 1:
            return sorted(winners)[0], True
        return next(iter(winners)), False

    def subsystem_for(self, dart_path: str) -> Optional[str]:
        """The subsystem owning ``dart_path`` (longest-prefix wins; None if unclassified)."""
        return self.classify(dart_path)[0]


def load(path: Path | str) -> Manifest:
    """Parse ``subsystems.yml`` into a :class:`Manifest`. Raises on malformed
    structure (a missing required key is mis-authoring, not a tolerated case)."""
    raw = yaml.safe_load(Path(path).read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"manifest {path}: top level must be a mapping")
    subs_raw = raw.get("subsystems") or {}
    subsystems = tuple(
        Subsystem(
            name=name,
            tier=str(spec["tier"]),
            path_prefixes=tuple(spec.get("path_prefixes") or ()),
        )
        for name, spec in subs_raw.items()
    )
    split = raw.get("split") or {}
    return Manifest(
        version=int(raw.get("version", 0)),
        subsystems=subsystems,
        corpus=raw.get("corpus") or {},
        split_ratio=dict(split.get("ratio") or {}),
        assignments=dict(split.get("assignments") or {}),
    )


def validate(
    manifest: Manifest,
    *,
    dart_inventory: Optional[Iterable[str]] = None,
    glp_sources: Optional[Iterable[str]] = None,
    ratio_tolerance: float = DEFAULT_RATIO_TOLERANCE,
) -> list[str]:
    """Return a list of validation error strings (empty ⇒ valid).

    ``dart_inventory`` = the in-scope Dart file paths (from ``dart_depgraph``);
    ``glp_sources`` = the in-scope GLP source paths (the corpus). Both are
    injected read-only; when ``None`` the corresponding checks are skipped
    (so the structural checks still run in a pure unit test).
    """
    errors: list[str] = []

    # Structural: known tiers.
    for sub in manifest.subsystems:
        if sub.tier not in ("strict", "dynamic"):
            errors.append(f"subsystem {sub.name!r}: unknown tier {sub.tier!r}")
        if not sub.path_prefixes:
            errors.append(f"subsystem {sub.name!r}: no path_prefixes")

    if dart_inventory is not None:
        inv = list(dart_inventory)
        # (1) every prefix resolves to ≥1 inventoried Dart file.
        for sub in manifest.subsystems:
            for pre in sub.path_prefixes:
                if not any(p.startswith(pre) for p in inv):
                    errors.append(
                        f"subsystem {sub.name!r}: prefix {pre!r} resolves to 0 files"
                    )
        # (2) every in-scope Dart file classified by exactly one subsystem.
        for p in inv:
            matches = [
                sub.name
                for sub in manifest.subsystems
                if any(p.startswith(pre) for pre in sub.path_prefixes)
            ]
            if len(matches) == 0:
                errors.append(f"dart file {p!r}: classified by no subsystem")
            elif len(set(matches)) > 1:
                errors.append(
                    f"dart file {p!r}: classified by multiple subsystems {sorted(set(matches))}"
                )

    if glp_sources is not None:
        srcs = list(glp_sources)
        # (3) every in-scope GLP source assigned to a split exactly once.
        for s in srcs:
            if s not in manifest.assignments:
                errors.append(f"glp source {s!r}: no split assignment")
        for s, val in manifest.assignments.items():
            if val not in ("train", "held-out"):
                errors.append(f"assignment {s!r}: invalid split {val!r}")
        # (4) realized ratio within tolerance of the declared ratio.
        assigned = [v for v in manifest.assignments.values() if v in ("train", "held-out")]
        if assigned and "train" in manifest.split_ratio:
            realized_train = sum(1 for v in assigned if v == "train") / len(assigned)
            declared_train = float(manifest.split_ratio["train"])
            if abs(realized_train - declared_train) > ratio_tolerance:
                errors.append(
                    f"split ratio: realized train={realized_train:.3f} "
                    f"deviates >{ratio_tolerance} from declared {declared_train:.3f}"
                )

    return errors
