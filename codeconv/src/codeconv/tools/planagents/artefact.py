"""Conversion-plan artefact path resolution + structural validation.

Implements ``specs/017-conversion-plan-agents/contracts/
conversion_plan_artefact_format.md`` (spec FR-006/FR-007/FR-008/FR-010/
FR-016/FR-017; SC-003/SC-004/SC-005). Artefact *content* is authored by
the planning sub-agent (``agent_orchestration.md``); this module only
resolves paths and validates STRUCTURE — it does NOT judge plan quality.

Artefact path (FR-010 / R7): ``.codeconv/conversion-plans/<rel>.dart.md``
— a parallel mirrored tree of ``.codeconv/tombstones/<rel>.dart.md``,
checked into git. Aggregated escalations report defaults to
``.codeconv/conversion-plans/_escalations-report.md`` (overridable).
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Optional

import yaml


ARTEFACT_ROOT_PARTS = (".codeconv", "conversion-plans")
ESCALATIONS_REPORT_NAME = "_escalations-report.md"

SCHEMA_VERSION = 1

# Mandated front-matter keys (order not significant for the dict, but all
# MUST be present). ``conversion_plan_artefact_format.md`` § "Mandated
# artefact structure".
_REQUIRED_FRONTMATTER_KEYS = (
    "path",
    "cycle_group_id",
    "scc_siblings",
    "generated_at",
    "source_sha256",
    "schema_version",
)

# ``generated_at`` is the SOLE field allowed to vary on an idempotent
# re-plan (SC-003). The structural validator never compares it.
VOLATILE_FRONTMATTER_KEY = "generated_at"

# Mandated top-level section headings, in this exact order (SC-004).
# Sections 1-6 are mandatory; section 7 is mandatory iff scc_siblings
# is non-empty and forbidden otherwise.
_SECTIONS_1_TO_6 = (
    "## 1. Source Analysis",
    "## 2. Dart → C#/.NET Conversion Plan",
    "## 3. Decomposed Task Units",
    "## 4. Research Findings",
    "## 5. Consistency Pass",
    "## 6. Escalations",
)
_SECTION_7 = "## 7. Cycle Siblings"

# Each ``### E<n>`` escalation entry MUST carry these five bullet fields.
_ESCALATION_BULLET_FIELDS = (
    "File(s)",
    "Observed",
    "Why not pre-specified+incremental",
    "Decision required",
    "Status",
)

_ESCALATION_HEADING_RE = re.compile(r"^###\s+E(\d+):\s+.+$", re.MULTILINE)
_OPEN_STATUS_RE = re.compile(r"^-\s+\*\*Status\*\*:\s*open\b", re.MULTILINE)


def _rel_no_md(rel_dart: str) -> str:
    """Normalise a ``<rel>.dart`` (or ``<rel>.dart.md``) to ``<rel>.dart``."""
    r = rel_dart.replace("\\", "/")
    if r.endswith(".dart.md"):
        r = r[: -len(".md")]
    return r


def artefact_root(repo_root: Path, override: Optional[Path] = None) -> Path:
    """Resolve the conversion-plan artefact root directory.

    Default ``<repo_root>/.codeconv/conversion-plans/`` (FR-010); an
    explicit ``override`` (CLI flag) wins.
    """
    if override is not None:
        return Path(override).resolve()
    return (Path(repo_root) / Path(*ARTEFACT_ROOT_PARTS)).resolve()


def artefact_path(
    repo_root: Path,
    rel_dart: str,
    *,
    root_override: Optional[Path] = None,
) -> Path:
    """Return the on-disk artefact path mirroring the tombstone tree.

    ``rel_dart`` is the subtree-relative POSIX ``.dart`` path (same shape
    as ``codeconv.dart_files.path``); the artefact is ``<root>/<rel>.dart.md``.
    """
    rel = _rel_no_md(rel_dart)
    return artefact_root(repo_root, root_override) / Path(rel + ".md")


def artefact_rel_path(rel_dart: str) -> str:
    """The repo-relative POSIX artefact path string for DB/YAML storage.

    Always rooted at the DEFAULT ``.codeconv/conversion-plans/`` location
    (the value persisted to ``dart_plans.plan_path`` / tombstone
    ``plan_path``). A custom root is a transient CLI choice and is not
    persisted (FR-013 stores plan *state*; the canonical artefact path is
    the default-rooted one).
    """
    rel = _rel_no_md(rel_dart)
    return "/".join((*ARTEFACT_ROOT_PARTS, rel + ".md"))


def escalations_report_path(
    repo_root: Path,
    *,
    override: Optional[Path] = None,
) -> Path:
    """Resolve the aggregated escalations-report path (FR-016).

    Default ``<root>/_escalations-report.md`` — the ``_`` prefix sorts
    it first and cannot collide with any ``<rel>.dart.md`` artefact path.
    """
    if override is not None:
        return Path(override).resolve()
    return artefact_root(repo_root) / ESCALATIONS_REPORT_NAME


@dataclass(frozen=True)
class ValidationResult:
    ok: bool
    errors: tuple[str, ...]

    def __bool__(self) -> bool:  # convenience
        return self.ok


def _split_frontmatter(text: str) -> tuple[dict[str, Any], str]:
    if not text.startswith("---"):
        raise ValueError("artefact missing front-matter delimiter")
    parts = text.split("---", 2)
    if len(parts) < 3:
        raise ValueError("artefact has only one '---' delimiter")
    fm = yaml.safe_load(parts[1]) or {}
    if not isinstance(fm, dict):
        raise ValueError("artefact front-matter is not a mapping")
    return fm, parts[2]


def validate(path: Path) -> ValidationResult:
    """Structurally validate a conversion-plan artefact (SC-004).

    Returns ``ok`` iff:

    - front-matter has all required keys (``_REQUIRED_FRONTMATTER_KEYS``);
    - ``schema_version`` == 1;
    - sections 1-6 are present, in order;
    - section 7 is present iff ``scc_siblings`` is non-empty (forbidden
      otherwise);
    - every ``### E<n>`` has all five bullet fields;
    - ``## 6. Escalations`` is either >=1 ``### E`` entry OR the literal
      line ``None.``.

    Does NOT judge plan *quality* (the agent's job) and never compares
    the volatile ``generated_at`` field.
    """
    errors: list[str] = []
    p = Path(path)
    if not p.is_file():
        return ValidationResult(False, (f"artefact not found: {p}",))

    text = p.read_text(encoding="utf-8")
    try:
        fm, body = _split_frontmatter(text)
    except ValueError as exc:
        return ValidationResult(False, (str(exc),))

    # Front-matter keys.
    for k in _REQUIRED_FRONTMATTER_KEYS:
        if k not in fm:
            errors.append(f"missing front-matter key: {k}")
    if fm.get("schema_version") != SCHEMA_VERSION:
        errors.append(
            f"schema_version must be {SCHEMA_VERSION}, "
            f"got {fm.get('schema_version')!r}"
        )

    scc_siblings = fm.get("scc_siblings")
    has_siblings = bool(scc_siblings) if scc_siblings is not None else False

    # Sections 1-6 present and ordered.
    last_idx = -1
    for heading in _SECTIONS_1_TO_6:
        idx = body.find("\n" + heading)
        if idx == -1 and body.startswith(heading):
            idx = 0
        if idx == -1:
            errors.append(f"missing section: {heading!r}")
        elif idx < last_idx:
            errors.append(f"section out of order: {heading!r}")
        else:
            last_idx = idx

    # Section 7 present iff scc_siblings non-empty.
    has_section_7 = ("\n" + _SECTION_7) in body or body.startswith(_SECTION_7)
    if has_siblings and not has_section_7:
        errors.append(
            "section 7 (Cycle Siblings) required when scc_siblings non-empty"
        )
    if not has_siblings and has_section_7:
        errors.append(
            "section 7 (Cycle Siblings) forbidden when scc_siblings empty"
        )

    # Escalations section content.
    esc_body = _section_body(body, _SECTIONS_1_TO_6[5], _SECTION_7)
    headings = _ESCALATION_HEADING_RE.findall(esc_body)
    if not headings:
        if "None." not in esc_body:
            errors.append(
                "section 6 must contain >=1 '### E<n>' entry or the "
                "literal line 'None.'"
            )
    else:
        # Each ### E<n> entry must carry the five bullet fields.
        entries = _split_escalation_entries(esc_body)
        for n, entry_text in entries:
            for field in _ESCALATION_BULLET_FIELDS:
                if f"**{field}**" not in entry_text:
                    errors.append(
                        f"escalation E{n} missing bullet field: {field}"
                    )

    return ValidationResult(not errors, tuple(errors))


def _section_body(body: str, start_heading: str, next_heading: str) -> str:
    start = body.find(start_heading)
    if start == -1:
        return ""
    end = body.find(next_heading, start + len(start_heading))
    return body[start:] if end == -1 else body[start:end]


def _split_escalation_entries(esc_body: str) -> list[tuple[int, str]]:
    matches = list(_ESCALATION_HEADING_RE.finditer(esc_body))
    out: list[tuple[int, str]] = []
    for i, m in enumerate(matches):
        n = int(m.group(1))
        seg_end = (
            matches[i + 1].start() if i + 1 < len(matches) else len(esc_body)
        )
        out.append((n, esc_body[m.start() : seg_end]))
    return out


def count_open_escalations(path: Path) -> int:
    """Number of ``Status: open`` escalation entries in an artefact (FR-017).

    Used by the skill to pass ``plan-completed --escalations <n>`` and by
    ``aggregate-escalations``. Returns 0 for ``None.`` / no entries.
    """
    p = Path(path)
    if not p.is_file():
        return 0
    text = p.read_text(encoding="utf-8")
    try:
        _, body = _split_frontmatter(text)
    except ValueError:
        body = text
    esc_body = _section_body(body, _SECTIONS_1_TO_6[5], _SECTION_7)
    entries = _split_escalation_entries(esc_body)
    return sum(
        1 for _n, seg in entries if _OPEN_STATUS_RE.search(seg) is not None
    )


def iter_open_escalations(path: Path) -> list[dict[str, str]]:
    """Parse the open escalation entries of one artefact (FR-016).

    Each dict has keys: ``e_number`` (int), ``title``, ``files``,
    ``observed``, ``why``, ``decision`` — verbatim text from the entry.
    Only ``Status: open`` entries are returned.
    """
    p = Path(path)
    if not p.is_file():
        return []
    text = p.read_text(encoding="utf-8")
    try:
        fm, body = _split_frontmatter(text)
    except ValueError:
        fm, body = {}, text
    esc_body = _section_body(body, _SECTIONS_1_TO_6[5], _SECTION_7)
    out: list[dict[str, str]] = []
    heading_iter = list(_ESCALATION_HEADING_RE.finditer(esc_body))
    entries = _split_escalation_entries(esc_body)
    for (n, seg), m in zip(entries, heading_iter):
        if _OPEN_STATUS_RE.search(seg) is None:
            continue
        title = m.group(0).split(":", 1)[1].strip()
        out.append(
            {
                "e_number": n,
                "title": title,
                "files": _bullet(seg, "File(s)"),
                "observed": _bullet(seg, "Observed"),
                "why": _bullet(seg, "Why not pre-specified+incremental"),
                "decision": _bullet(seg, "Decision required"),
            }
        )
    return out


def _bullet(seg: str, field: str) -> str:
    m = re.search(
        rf"-\s+\*\*{re.escape(field)}\*\*:\s*(.+?)(?=\n-\s+\*\*|\n###|\Z)",
        seg,
        re.DOTALL,
    )
    return m.group(1).strip() if m else ""


__all__ = [
    "ESCALATIONS_REPORT_NAME",
    "SCHEMA_VERSION",
    "VOLATILE_FRONTMATTER_KEY",
    "ValidationResult",
    "artefact_path",
    "artefact_rel_path",
    "artefact_root",
    "count_open_escalations",
    "escalations_report_path",
    "iter_open_escalations",
    "validate",
]
