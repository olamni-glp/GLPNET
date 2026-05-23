"""Optimized-prompt artifact (de)serialization + production load.

Implements ``specs/019-codeconv-codegen/contracts/
codegen_artifact_format.md`` § B. The single optimizer→production
handoff is ``.codeconv/codegen-prompt/optimized.md``:

- a fenced ```yaml provenance block (``schema_version``, ``optimizer``,
  ``metric_score``, ``dataset_hash``, ``generated_at``, ``model``), then
- the optimized codegen instructions as markdown prose.

``load()`` reads it for the production codegen sub-agent. If absent, the
sub-agent uses the baseline instructions shipped here (and ``status``
warns). Only ``codegen-opt export-prompt`` writes it (it imports
:func:`serialize` from this module). PURE: text + path; no LLM, no
bridge — and crucially this module is NEVER imported by the optimizer's
LM client path, only by the deterministic production side.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Optional

import yaml


OPTIMIZED_PROMPT_REL = ".codeconv/codegen-prompt/optimized.md"
SCHEMA_VERSION = 1

# Baseline codegen instructions shipped with the tool. Used verbatim when
# no optimized artifact is checked in. Mirrors the discipline in
# ``contracts/agent_orchestration.md`` § Discipline (hard).
BASELINE_INSTRUCTIONS = """\
You are converting one Dart source file to real, compilable C#/.NET 10.

Inputs you are given: the Dart source; its ratified conversion spec
(`.codeconv/conversion-specs/<rel>.dart.md`); its ratified conversion
plan (`.codeconv/conversion-plans/<rel>.dart.md`); the public C#
surfaces of already-generated dependencies (`out/csharp/`); and the
relevant conversion idioms.

Hard rules:
- Emit REAL C# only — a single raw `.cs` source file. No prose, no
  markdown fences, no leftover Dart.
- Produce the top-level construct(s) named in the plan's
  `target_code_unit` / conversion-units.
- Honor recorded conventions and idioms verbatim: keep `*Error` type
  names; apply the `getX` → `LookupX` rename; use dependency APIs
  exactly as generated (never invent a signature).
- Escalate-don't-guess: if a construct cannot be faithfully derived from
  the plan + spec + idioms, emit a structured escalation instead of a
  guessed translation. Never ship a guess or a non-compiling file.
- For an SCC (dependency cycle), the members are one coordinated batch
  with consistent cross-references; none is finished until all build.
"""


@dataclass(frozen=True)
class PromptArtifact:
    """A loaded codegen prompt.

    ``instructions`` is the markdown prose handed to the sub-agent.
    ``provenance`` is the yaml provenance block (empty for baseline).
    ``is_baseline`` is True when no optimized artifact was found.
    """

    instructions: str
    provenance: dict[str, Any] = field(default_factory=dict)
    is_baseline: bool = False


def serialize(instructions: str, provenance: dict[str, Any]) -> str:
    """Render the optimized-prompt artifact text (provenance + prose).

    ``provenance`` is augmented with ``schema_version`` if absent. The
    yaml block is emitted with sorted keys for diff stability.
    """
    prov = dict(provenance)
    prov.setdefault("schema_version", SCHEMA_VERSION)
    block = yaml.safe_dump(prov, sort_keys=True, default_flow_style=False)
    body = instructions.rstrip("\n") + "\n"
    return f"```yaml\n{block}```\n\n{body}"


def parse(text: str) -> PromptArtifact:
    """Parse an optimized-prompt artifact's text into a PromptArtifact.

    The leading fenced ```yaml block is the provenance; everything after
    it is the instructions. A missing/un-parseable block yields empty
    provenance and treats the whole text as instructions (lenient — the
    build gate, not this parser, is the quality arbiter).
    """
    m = re.match(r"\s*```ya?ml\s*\n([\s\S]*?)\n```\s*\n?", text, re.IGNORECASE)
    if not m:
        return PromptArtifact(instructions=text.strip() + "\n", provenance={})
    try:
        prov = yaml.safe_load(m.group(1)) or {}
        if not isinstance(prov, dict):
            prov = {}
    except yaml.YAMLError:
        prov = {}
    instructions = text[m.end():].strip() + "\n"
    return PromptArtifact(instructions=instructions, provenance=prov)


def optimized_prompt_path(repo_root: Path) -> Path:
    """On-disk path of the optimized-prompt artifact."""
    return Path(repo_root) / OPTIMIZED_PROMPT_REL


def load(repo_root: Path) -> PromptArtifact:
    """Load the optimized prompt, or the baseline if absent.

    Production codegen sub-agent entry point. Returns a
    :class:`PromptArtifact`; ``is_baseline=True`` (and empty provenance)
    when no checked-in optimized artifact exists — the caller (``status``)
    warns in that case.
    """
    path = optimized_prompt_path(repo_root)
    if not path.is_file():
        return PromptArtifact(
            instructions=BASELINE_INSTRUCTIONS, provenance={}, is_baseline=True
        )
    art = parse(path.read_text(encoding="utf-8"))
    # An optimized file with empty instructions falls back to baseline.
    if not art.instructions.strip():
        return PromptArtifact(
            instructions=BASELINE_INSTRUCTIONS,
            provenance=art.provenance,
            is_baseline=True,
        )
    return art


def write_optimized(
    repo_root: Path,
    instructions: str,
    provenance: dict[str, Any],
    *,
    out_path: Optional[Path] = None,
) -> Path:
    """Serialize + write the optimized-prompt artifact (used by
    ``codegen-opt export-prompt`` — the ONLY writer). Atomic replace."""
    target = Path(out_path) if out_path else optimized_prompt_path(repo_root)
    target.parent.mkdir(parents=True, exist_ok=True)
    tmp = target.with_suffix(target.suffix + ".tmp")
    tmp.write_text(serialize(instructions, provenance), encoding="utf-8")
    import os

    os.replace(tmp, target)
    return target


__all__ = [
    "BASELINE_INSTRUCTIONS",
    "OPTIMIZED_PROMPT_REL",
    "SCHEMA_VERSION",
    "PromptArtifact",
    "load",
    "optimized_prompt_path",
    "parse",
    "serialize",
    "write_optimized",
]
