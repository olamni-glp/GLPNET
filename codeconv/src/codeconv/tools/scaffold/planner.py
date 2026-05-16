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
    return plan


__all__ = ["PlannedFile", "plan_target_tree"]
