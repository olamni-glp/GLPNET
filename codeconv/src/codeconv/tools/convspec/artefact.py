"""convspec artifact format + validation (feature 018, T032).

Per ``contracts/convspec_artifact_format.md`` (FR-011/FR-023/SC-006).
One checked-in markdown per file
``.codeconv/conversion-specs/<rel>.dart.md``:

- (A) a fenced ``yaml`` structured block (machine-consumable —
  schema_version, source_path, source_sha256, target_code_unit,
  constructs[], conversion_units[], escalations[]) the later codegen
  stage parses deterministically; and
- (B) embedded human-readable rationale + research provenance prose.

**Spec-only (FR-023):** the artifact describes the conversion; it MUST
NOT contain compilable C#. PURE: path + parse + validate only; no
bridge, no LLM. The agent (skill) writes the artifact; this module is
the deterministic gate the ``@DBOS.step`` uses to accept/reject it.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any

import yaml

SCHEMA_VERSION = 1
_SPEC_SUBDIR = ("conversion-specs",)

# A fenced C#/.cs code block with real C# syntax ⇒ FR-023 violation
# (convspec is spec-only; codegen is a later stage).
_CSHARP_FENCE = re.compile(
    r"```(c#|cs|csharp)\b[\s\S]*?```", re.IGNORECASE
)
_CSHARP_TOKENS = re.compile(
    r"\b(namespace|using\s+System|public\s+(class|interface|record|"
    r"struct|enum)|void\s+Main)\b"
)


def artifact_path(repo_root: Path, rel_dart_path: str) -> Path:
    """``.codeconv/conversion-specs/<rel>.dart.md`` (POSIX rel in, OS
    path out). Linked from the tombstone ``spec_path``."""
    rel = rel_dart_path.replace("\\", "/").strip("/")
    return repo_root.joinpath(".codeconv", *_SPEC_SUBDIR, rel + ".md")


def parse_artifact(text: str) -> dict[str, Any]:
    """Extract the first fenced ``yaml`` block as the structured spec.
    Raises ``ValueError`` if absent/malformed (the step treats that as
    'artifact not yet valid' ⇒ needs_agent_work)."""
    m = re.search(r"```ya?ml\s*\n([\s\S]*?)\n```", text, re.IGNORECASE)
    if not m:
        raise ValueError("no fenced ```yaml structured block")
    data = yaml.safe_load(m.group(1))
    if not isinstance(data, dict):
        raise ValueError("structured block is not a mapping")
    return data


def emits_csharp(text: str) -> bool:
    """True iff the artifact embeds compilable C# (FR-023 violation)."""
    for block in _CSHARP_FENCE.finditer(text):
        if _CSHARP_TOKENS.search(block.group(0)):
            return True
    return False


def validate_artifact(text: str, *, source_sha256: str | None = None) -> list[str]:
    """Return a list of validation errors ([] ⇒ acceptable).

    Enforces: structured block present + schema_version; spec-only
    (no C# — FR-023); every non-trivial construct records BOTH a
    deep-analysis basis AND a research basis (research_finding_id) OR an
    idiom_id (SC-006 — no unjustified decision); a nuance note on
    non-trivial constructs; optional source_sha256 match (drift).
    """
    errs: list[str] = []
    if emits_csharp(text):
        errs.append("FR-023: artifact emits compilable C# (spec-only)")
    try:
        spec = parse_artifact(text)
    except ValueError as exc:
        errs.append(f"structured block: {exc}")
        return errs

    if spec.get("schema_version") != SCHEMA_VERSION:
        errs.append(
            f"schema_version must be {SCHEMA_VERSION}, got "
            f"{spec.get('schema_version')!r}"
        )
    if source_sha256 is not None and spec.get("source_sha256") not in (
        None,
        source_sha256,
    ):
        errs.append(
            "source_sha256 drift: artifact was written against a "
            "different source revision (FR-019 — re-spec required)"
        )

    constructs = spec.get("constructs") or []
    if not isinstance(constructs, list):
        errs.append("constructs must be a list")
        constructs = []
    for i, c in enumerate(constructs):
        if not isinstance(c, dict):
            errs.append(f"constructs[{i}] not a mapping")
            continue
        if not c.get("construct_key"):
            errs.append(f"constructs[{i}] missing construct_key")
        trivial = bool(c.get("trivial"))
        if trivial:
            continue
        # SC-006: a non-trivial construct needs BOTH bases, or an
        # idiom_id pointing at an already-decided one.
        has_idiom = c.get("idiom_id") is not None
        has_analysis = bool(c.get("source_form")) and bool(
            c.get("target_decision")
        )
        has_research = c.get("research_finding_id") is not None
        if not has_idiom and not (has_analysis and has_research):
            errs.append(
                f"constructs[{i}] ({c.get('construct_key')}): non-trivial "
                "construct lacks BOTH a deep-analysis basis and a "
                "research basis (and no idiom_id) — SC-006 / 0 "
                "unjustified decisions"
            )
        if not c.get("nuance"):
            errs.append(
                f"constructs[{i}] ({c.get('construct_key')}): non-trivial "
                "construct missing the mandatory `nuance` note (US2 AS4)"
            )
    return errs


def open_escalations(text: str) -> list[dict]:
    """Escalations recorded in the artifact (FR-013/014). Empty ⇒ none.
    Any escalation ⇒ ``open_escalation_count`` > 0 ⇒ conversion blocked
    for that file (NOT specing)."""
    try:
        spec = parse_artifact(text)
    except ValueError:
        return []
    esc = spec.get("escalations") or []
    return [e for e in esc if isinstance(e, dict)]


__all__ = [
    "SCHEMA_VERSION",
    "artifact_path",
    "emits_csharp",
    "open_escalations",
    "parse_artifact",
    "validate_artifact",
]
