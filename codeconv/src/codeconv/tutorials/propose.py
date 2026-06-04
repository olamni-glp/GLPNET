"""Restructuring proposals — read-only by default, approval-gated apply (D9).

PURE / BRIDGE-FREE (D1). No bridge/DBOS/LM. Generation NEVER mutates a file;
``apply`` requires explicit per-example approval + a recorded rationale, targets
the sibling source-of-truth, re-vendors, preserves semantics/clause-text, and is
revertible (FR-013/015/019). Guarded by ``test_tutorials_no_bridge.py``.

Concrete proposal classes for this corpus (D9 + decision-2, 2026-06-04):
- ``RUN_MANIFEST`` — an explicit ch07+ exercise→(project, goal) manifest.
- ``DRIFT_GAP`` — ``programs/cssg_modules`` is the live ch07 substrate but is
  NOT vendored (``sync --check`` cannot guard it).
- ``STALE_ARTEFACT`` — superseded ch07/08–12, in-corpus ``cssg-modules``/
  ``simple-multimodule``, and goldens that predate a runtime fix (ch04/08, after
  the C# ``is_list`` convergence; re-capture against the live oracle).
- ``LAYOUT_NORMALISE`` — spec-violating exercises (ch04/07 multi-clause guard).
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from pathlib import Path


class ProposalKind(str, Enum):
    RUN_MANIFEST = "run_manifest"
    DRIFT_GAP = "drift_gap"
    STALE_ARTEFACT = "stale_artefact"
    LAYOUT_NORMALISE = "layout_normalise"


@dataclass(frozen=True)
class Proposal:
    id: str
    kind: ProposalKind
    chapter_id: str
    exercise_number: str | None
    rationale: str
    target_sibling_path: str | None  # the source-of-truth file an apply would touch
    applied: bool = False


class ApplyRefused(Exception):
    """Raised when ``apply`` is attempted without approval + rationale (FR-019)."""


def generate_proposals(corpus, *, repo_root: Path) -> list[Proposal]:
    """Read-only scan → normalization proposals (D9). Mutates nothing."""
    proposals: list[Proposal] = []
    ch07 = next((t for t in corpus.chapters if t.id == "ch07"), None)
    if ch07 is not None:
        proposals.append(Proposal(
            id="drift-gap-cssg",
            kind=ProposalKind.DRIFT_GAP,
            chapter_id="ch07", exercise_number=None,
            rationale="ch07 runs the live sibling programs/cssg_modules/, which is NOT vendored; "
                      "`tutorials sync --check` cannot guard it. Vendor cssg_modules/ or record a run-manifest.",
            target_sibling_path="programs/cssg_modules",
        ))
        proposals.append(Proposal(
            id="run-manifest-ch07",
            kind=ProposalKind.RUN_MANIFEST,
            chapter_id="ch07", exercise_number=None,
            rationale="Record an explicit exercise-MM→(programs/cssg_modules, fplayMM, :limit) manifest "
                      "so the use-case mapping is deterministic and drift-checkable.",
            target_sibling_path=None,
        ))
        for ex in ch07.exercises:
            try:
                n = int(ex.number)
            except ValueError:
                continue
            if n >= 8:
                proposals.append(Proposal(
                    id=f"stale-ch07-ex{ex.number}",
                    kind=ProposalKind.STALE_ARTEFACT,
                    chapter_id="ch07", exercise_number=ex.number,
                    rationale="SUPERSEDED 2026-05-04 (cluster-A/B model); references the stale in-corpus "
                              "cssg-modules/. Disposition pending — remove or re-base on the canonical project.",
                    target_sibling_path=ex.dir,
                ))
    # Goldens that predate a runtime-convergence fix (re-capture vs live oracle).
    proposals.append(Proposal(
        id="stale-golden-ch04-ex08",
        kind=ProposalKind.STALE_ARTEFACT,
        chapter_id="ch04", exercise_number="08",
        rationale="flatten golden predates the C# is_list guard fix (shows a [WARN] + un-flattened result); "
                  "the live Dart/C# oracle now yields F=[5,4,3,2,1]. Re-capture the golden.",
        target_sibling_path="olamni/tutorial/ch04/exercise-08",
    ))
    # Spec-violating exercises (manual §8: defined guards must be single-unit-clause).
    proposals.append(Proposal(
        id="spec-violation-ch04-ex07",
        kind=ProposalKind.LAYOUT_NORMALISE,
        chapter_id="ch04", exercise_number="07",
        rationale="Uses multi-clause natural_number/1 as a guard (spec-invalid per manual §8). The current "
                  "runtime correctly rejects it; the golden's ✓Loaded is from a stale build. Fix the exercise "
                  "(single-unit-clause guard) or re-capture the rejection as the golden.",
        target_sibling_path="olamni/tutorial/ch04/exercise-07",
    ))
    return proposals


def apply_proposal(proposal: Proposal, *, approve: str | None, rationale: str | None) -> Proposal:
    """Approval-gated apply (FR-019). Refuses without ``approve`` + ``rationale``.

    The actual mutation (targets the sibling source-of-truth, re-vendors via
    ``tutorials sync``, preserves semantics/clause-text, revertible) is performed
    by the caller; this enforces the gate and records intent.
    """
    if not approve or not rationale:
        raise ApplyRefused(
            "propose --apply requires --approve <EXERCISE> AND --rationale \"<why>\"; "
            "nothing was modified."
        )
    return Proposal(proposal.id, proposal.kind, proposal.chapter_id, proposal.exercise_number,
                    rationale, proposal.target_sibling_path, applied=True)


__all__ = ["ProposalKind", "Proposal", "ApplyRefused", "generate_proposals", "apply_proposal"]
