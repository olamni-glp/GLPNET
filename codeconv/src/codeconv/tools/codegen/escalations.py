"""Codegen escalation artifact path + parsing (FR-007/FR-009).

A generated ``.cs`` MUST be pure C# (``artefact.validate_generated``), so
codegen escalations cannot be embedded in the deliverable. They live in a
parallel per-file markdown tree
``.codeconv/conversion-code/<rel>.dart.md`` — the codegen analogue of the
plan's ``## 6. Escalations`` section. ``ingest`` reads it to learn
``open_escalation_count``; ``aggregate-escalations`` walks the tree to
write ``_escalations-report.md`` whose total MUST equal
``Σ dart_codegen.open_escalation_count > 0`` (contract
codegen_artifact_format.md § C). PURE: text + path only.

Each escalation entry (``### E<n>: <title>``) carries five bullets:
``Kind`` (``undecidable｜build_unrecoverable｜dependency_missing``),
``File(s)``, ``Detail``, ``Needs``, ``Status`` (``open｜resolved``).
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Optional


ARTEFACT_ROOT_PARTS = (".codeconv", "conversion-code")
ESCALATIONS_REPORT_NAME = "_escalations-report.md"

VALID_KINDS = ("undecidable", "build_unrecoverable", "dependency_missing")

_ESCALATION_HEADING_RE = re.compile(r"^###\s+E(\d+):\s+(.+)$", re.MULTILINE)
_OPEN_STATUS_RE = re.compile(r"^-\s+\*\*Status\*\*:\s*open\b", re.MULTILINE)


def _rel_no_md(rel_dart: str) -> str:
    r = rel_dart.replace("\\", "/").strip("/")
    if r.endswith(".dart.md"):
        r = r[: -len(".md")]
    return r


def artefact_root(repo_root: Path, override: Optional[Path] = None) -> Path:
    """Resolve the codegen escalation-artifact root directory."""
    if override is not None:
        return Path(override).resolve()
    return (Path(repo_root) / Path(*ARTEFACT_ROOT_PARTS)).resolve()


def escalation_artifact_path(repo_root: Path, rel_dart: str) -> Path:
    """``.codeconv/conversion-code/<rel>.dart.md`` (POSIX rel in, OS out)."""
    rel = _rel_no_md(rel_dart)
    return artefact_root(repo_root) / Path(rel + ".md")


def escalations_report_path(
    repo_root: Path, *, override: Optional[Path] = None
) -> Path:
    """Resolve the aggregated report path (default ``_escalations-report.md``)."""
    if override is not None:
        return Path(override).resolve()
    return artefact_root(repo_root) / ESCALATIONS_REPORT_NAME


def _split_entries(text: str) -> list[tuple[int, str, str]]:
    matches = list(_ESCALATION_HEADING_RE.finditer(text))
    out: list[tuple[int, str, str]] = []
    for i, m in enumerate(matches):
        seg_end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        out.append((int(m.group(1)), m.group(2).strip(), text[m.start():seg_end]))
    return out


def _bullet(seg: str, field: str) -> str:
    m = re.search(
        rf"-\s+\*\*{re.escape(field)}\*\*:\s*(.+?)(?=\n-\s+\*\*|\n###|\Z)",
        seg,
        re.DOTALL,
    )
    return m.group(1).strip() if m else ""


def count_open(path: Path) -> int:
    """Number of ``Status: open`` escalation entries in one artifact."""
    p = Path(path)
    if not p.is_file():
        return 0
    text = p.read_text(encoding="utf-8")
    return sum(
        1
        for _n, _t, seg in _split_entries(text)
        if _OPEN_STATUS_RE.search(seg) is not None
    )


def iter_open(path: Path) -> list[dict[str, str]]:
    """Parse the open escalation entries of one artifact.

    Each dict: ``e_number`` (int), ``title``, ``kind``, ``files``,
    ``detail``, ``needs`` — verbatim text. Only ``Status: open`` entries.
    """
    p = Path(path)
    if not p.is_file():
        return []
    text = p.read_text(encoding="utf-8")
    out: list[dict[str, str]] = []
    for n, title, seg in _split_entries(text):
        if _OPEN_STATUS_RE.search(seg) is None:
            continue
        out.append(
            {
                "e_number": n,
                "title": title,
                "kind": _bullet(seg, "Kind"),
                "files": _bullet(seg, "File(s)"),
                "detail": _bullet(seg, "Detail"),
                "needs": _bullet(seg, "Needs"),
            }
        )
    return out


__all__ = [
    "ARTEFACT_ROOT_PARTS",
    "ESCALATIONS_REPORT_NAME",
    "VALID_KINDS",
    "artefact_root",
    "count_open",
    "escalation_artifact_path",
    "escalations_report_path",
    "iter_open",
]
