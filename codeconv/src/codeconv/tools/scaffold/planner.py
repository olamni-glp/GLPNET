"""``codeconv scaffold`` target-tree planner — Feature 016 / US2.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_scaffold_cli.md``
behaviour step 4–5 (spec FR-013/FR-014).

Pure (no filesystem mutation, no DB write) planner — the D2Net.Scaffold
``TargetTreePlanner`` parity. Reads the in-scope source file set from
``codeconv.dart_files`` minus ``codeconv.excluded_directories`` (NOT from
any ``public.dart_files``), and produces one :class:`PlannedFile` per
in-scope source file using the selected pair's ``target_for()`` /
``workdir_name()`` hooks. The workflow stages/copies according to the
plan; the planner itself only computes it.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from sqlalchemy import text


class TargetCollisionError(Exception):
    """Two distinct source files map to the same scaffold target path.

    Generic, pair-agnostic guard (feature-032 R-003 owner ruling **R3-b**):
    a normalizing ``target_for`` — e.g. the Dart->Gleam pair's Gleam
    module-segment normalization — is **not injective**, so two distinct
    sources can collide onto one ``target_rel`` (or ``workdir_rel``).
    :func:`plan_target_tree` raises this with an actionable message naming
    the colliding sources. The planner is pure and runs BEFORE any staging
    write (``scaffold.workflow`` stages only after it has the plan), so
    **nothing is produced** on collision (spec-032 FR-008 — a colliding
    target is never silently merged or overwritten). This check is generic
    (it benefits every present/future normalizing pair) and is NOT
    pair-specific logic — the SC-003 carve-out the owner blessed permits
    exactly this one uniqueness assertion in the scaffold planner.
    """

    def __init__(
        self, kind: str, target: str, first: str, second: str
    ) -> None:
        self.kind = kind
        self.target = target
        self.first = first
        self.second = second
        super().__init__(
            f"scaffold {kind} collision: source files {first!r} and "
            f"{second!r} both map to {kind} {target!r}. The selected "
            f"language pair's target mapping is not injective for this "
            f"source set; rename one source or adjust the pair's "
            f"normalization. No output was produced."
        )


@dataclass(frozen=True)
class PlannedFile:
    """One in-scope source file's scaffold plan entry.

    ``source_rel`` / ``target_rel`` are subtree-relative POSIX paths
    (the inventory stores POSIX rel paths — feature 012 R7).
    ``workdir_rel`` is the per-file working directory POSIX rel-path
    adjacent to ``target_rel`` (``None`` if the pair has no workdir
    convention).
    """

    source_rel: str
    target_rel: str
    workdir_rel: str | None


def _is_excluded(rel: str, excluded_dirs: list[str]) -> bool:
    """True iff ``rel`` is under (or equals) any excluded directory.

    Directory-boundary check (matches D2Net ``PathValidator.IsUnder``):
    ``lib/generated`` excludes ``lib/generated`` and
    ``lib/generated/x.dart`` but NOT ``lib/generated_other``.
    """
    for d in excluded_dirs:
        d = d.strip("/")
        if not d:
            continue
        if rel == d or rel.startswith(d + "/"):
            return True
    return False


def plan_target_tree(
    engine: Any,
    pair: Any,
) -> list[PlannedFile]:
    """Build the target-tree plan from the codeconv inventory + exclusions.

    Per contract step 4: read ``codeconv.dart_files`` minus
    ``codeconv.excluded_directories`` (directory-form rows only;
    filename-suffix exclusions like ``*.g.dart`` were already pruned by
    discover's walker). Per step 5: map each in-scope source rel-path via
    the pair's ``target_for`` (extension swap, mirrored dirs) and
    ``workdir_name`` (per-file working dir).

    Returns the plan sorted by ``source_rel`` for deterministic staging.
    """
    with engine.connect() as conn:
        source_rels = sorted(
            r[0]
            for r in conn.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
        excluded_dirs = [
            r[0]
            for r in conn.execute(
                text(
                    "SELECT path FROM codeconv.excluded_directories "
                    "WHERE path NOT LIKE '*%'"
                )
            ).all()
        ]

    plan: list[PlannedFile] = []
    for src in source_rels:
        if _is_excluded(src, excluded_dirs):
            continue
        target_rel = pair.target_for(src).replace("\\", "/")
        wd = pair.workdir_name(src)
        if wd:
            slash = target_rel.rfind("/")
            parent = target_rel[: slash + 1] if slash >= 0 else ""
            workdir_rel = f"{parent}{wd}"
        else:
            workdir_rel = None
        plan.append(
            PlannedFile(
                source_rel=src,
                target_rel=target_rel,
                workdir_rel=workdir_rel,
            )
        )
    _assert_no_target_collisions(plan)
    return plan


def _assert_no_target_collisions(plan: list[PlannedFile]) -> None:
    """Generic uniqueness guard (feature-032 R-003 / R3-b).

    Raise :class:`TargetCollisionError` if two distinct sources share a
    ``target_rel`` (or a non-``None`` ``workdir_rel``). Pair-agnostic: the
    default ``dart_csharp`` ``target_for`` is injective on ``.dart`` inputs
    so this never fires for it; a normalizing pair (e.g. Dart->Gleam) can
    collide, and detecting it here — before any filesystem write — is the
    only place an aggregate (all-target-paths) check can live without
    making the per-file ``target_for`` hook stateful (spec-032 FR-008/
    FR-009).
    """
    seen_target: dict[str, str] = {}
    seen_workdir: dict[str, str] = {}
    for pf in plan:
        prior = seen_target.get(pf.target_rel)
        if prior is not None:
            raise TargetCollisionError(
                "target", pf.target_rel, prior, pf.source_rel
            )
        seen_target[pf.target_rel] = pf.source_rel
        if pf.workdir_rel is not None:
            prior_wd = seen_workdir.get(pf.workdir_rel)
            if prior_wd is not None:
                raise TargetCollisionError(
                    "workdir", pf.workdir_rel, prior_wd, pf.source_rel
                )
            seen_workdir[pf.workdir_rel] = pf.source_rel


__all__ = ["PlannedFile", "TargetCollisionError", "plan_target_tree"]
