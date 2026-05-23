"""Generated-code artefact path resolution + real-C# validation.

Implements ``specs/019-codeconv-codegen/contracts/
codegen_artifact_format.md`` § A. The generated unit is
``<target_root>/<rel>.cs`` (default ``out/csharp/<rel>.cs``) — real,
compilable C#/.NET 10 for one source file at its scaffolded target path
(``.dart`` → ``.cs`` via the ``dart_csharp`` langpair).

**Validation is the INVERSE of the convspec rule** (see
``tools.convspec.artefact``): convspec is spec-only and REJECTS C#;
codegen REQUIRES real C# and REJECTS:

- spec-style / prose-only files (markdown headings, fenced code blocks);
- leftover Dart (Dart-style ``import '…';`` / ``library`` / ``part of`` /
  lowercase ``void main(``);
- empty stubs (no top-level C# construct).

The build gate (``buildgate.py``) is the ultimate validator; this module
is the cheap deterministic structural pre-gate. PURE: path + text only;
no bridge, no LLM, no ``dotnet`` invocation.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Callable, Iterable, Optional

from codeconv.langpairs.dart_csharp.target_csharp import target_for as _dart_target_for


# Default target tree root (R11: committed, reviewable product).
DEFAULT_TARGET_ROOT = "out/csharp"

# A top-level C# construct (the file must declare at least one).
_CSHARP_CONSTRUCT = re.compile(
    r"\b(namespace|public\s+(class|interface|record|struct|enum)|"
    r"internal\s+(class|interface|record|struct|enum)|"
    r"(public|internal|static)\s+class|void\s+Main)\b"
)
# Markdown evidence ⇒ prose / spec file, not raw C# source.
_MD_FENCE = re.compile(r"^```", re.MULTILINE)
_MD_HEADING = re.compile(r"^#{1,6}\s+\S", re.MULTILINE)
# Dart-specific syntax ⇒ leftover Dart, never valid C#.
_DART_IMPORT = re.compile(r"^\s*import\s+'(package:|dart:|[^']+\.dart)'", re.MULTILINE)
_DART_LIBRARY = re.compile(r"^\s*(library|part\s+of|part)\s+", re.MULTILINE)
_DART_MAIN = re.compile(r"\bvoid\s+main\s*\(")  # lowercase main = Dart


def produced_cs_rel(
    source_rel: str,
    target_root_rel: str = DEFAULT_TARGET_ROOT,
    *,
    target_for: Callable[[str], str] = _dart_target_for,
) -> str:
    """Repo-relative POSIX path of the produced ``.cs`` for ``source_rel``.

    ``<target_root>/<target_for(source_rel)>`` — e.g.
    ``runtime/cell.dart`` → ``out/csharp/runtime/cell.cs``. This is the
    value persisted to ``dart_codegen.target_cs_path`` / the tombstone
    ``target_cs_path``. The ``target_for`` mapping is injected so the
    workspace-resolved langpair drives it (default = ``dart_csharp``).
    """
    root = target_root_rel.replace("\\", "/").strip("/")
    tgt = target_for(source_rel).replace("\\", "/").strip("/")
    return f"{root}/{tgt}" if root else tgt


def produced_cs_path(
    repo_root: Path,
    source_rel: str,
    target_root_rel: str = DEFAULT_TARGET_ROOT,
    *,
    target_for: Callable[[str], str] = _dart_target_for,
) -> Path:
    """On-disk path of the produced ``.cs`` (OS separators)."""
    return Path(repo_root) / Path(
        produced_cs_rel(source_rel, target_root_rel, target_for=target_for)
    )


def is_real_csharp(text: str) -> bool:
    """True iff ``text`` looks like raw C# source with a top-level construct.

    Convenience over :func:`validate_generated` (which returns the
    specific errors).
    """
    return not validate_generated(text)


def validate_generated(
    text: str,
    *,
    expected_units: Optional[Iterable[str]] = None,
) -> list[str]:
    """Return a list of validation errors ([] ⇒ acceptable real C#).

    Enforces (inverse of convspec ``validate_artifact``):

    - non-empty;
    - NOT prose/spec-style (no markdown headings or fenced code blocks);
    - NOT leftover Dart (no Dart ``import '…';`` / ``library`` / ``part`` /
      lowercase ``void main(``);
    - declares ≥1 top-level C# construct;
    - (optional) every name in ``expected_units`` (from the plan's
      ``target_code_unit`` / conversion-units) appears as a declared
      identifier.
    """
    errs: list[str] = []
    if not text or not text.strip():
        return ["empty generated file (no C#)"]

    if _MD_FENCE.search(text) or _MD_HEADING.search(text):
        errs.append(
            "prose/spec-style file: contains markdown headings or fenced "
            "code blocks (a generated .cs must be raw C# source)"
        )
    if _DART_IMPORT.search(text) or _DART_LIBRARY.search(text):
        errs.append("leftover Dart: Dart-style import/library/part directive")
    if _DART_MAIN.search(text):
        errs.append("leftover Dart: lowercase `void main(` (C# uses `Main`)")
    if not _CSHARP_CONSTRUCT.search(text):
        errs.append(
            "no top-level C# construct (namespace/class/interface/record/"
            "struct/enum/Main) — empty stub or not C#"
        )

    for unit in expected_units or ():
        name = str(unit).strip()
        if not name:
            continue
        # The plan-named construct must appear as a declared identifier.
        if not re.search(rf"\b{re.escape(name)}\b", text):
            errs.append(
                f"plan-named construct {name!r} not present in generated C#"
            )
    return errs


__all__ = [
    "DEFAULT_TARGET_ROOT",
    "is_real_csharp",
    "produced_cs_path",
    "produced_cs_rel",
    "validate_generated",
]
