"""Gleam target-side hooks for the ``dart_gleam`` language pair.

Source of truth:
``specs/032-codeconv-gleam-langpair/contracts/dart_gleam_hooks.md`` +
``data-model.md`` (the Gleam module-segment normalization rule) over the
016 base contract (``langpair_plugin_contract.md`` behavioural
requirements 2 & 3).

- :func:`target_extension` is ``.gleam`` (the Gleam source extension).
- :func:`target_for` mirrors the Dart directory structure **verbatim** and
  swaps the basename extension to ``.gleam``, additionally **normalizing
  every path segment** to a legal Gleam module-path segment
  (``^[a-z][a-z0-9_]*$``, non-reserved). A Dart name that is already a
  legal Gleam segment is preserved byte-identically (FR-003 AS-2); an
  illegal segment is mapped deterministically (FR-008 / SC-004). No Gleam
  project-layout prefix is added — that is F3's concern.
- :func:`workdir_name` returns ``__<basename-without-ext>`` (D2NET parity;
  same convention as ``dart_csharp``, verbatim stem).

Pure / side-effect-free (no filesystem, DB, bridge, network) so the unit
tests need no ``@needs_bridge`` (FR-009). :func:`target_for` is a pure
function of ``source_rel`` (FR-010 determinism). It is **NOT injective**
(research.md R-003 pigeonhole — an illegal segment can normalize onto an
already-legal sibling), so cross-file collision detection is a
scaffold-planner concern: the R-003 owner ruling (R3-b) adds one generic
uniqueness assertion in ``tools/scaffold/planner.py``.
"""

from __future__ import annotations

import re
from typing import Optional

# Target extension — the Gleam source file extension (Gleam 1.17.0).
TARGET_EXTENSION: str = ".gleam"

# A legal Gleam module-path segment: a lowercase-led snake identifier.
_GLEAM_SEGMENT_RE = re.compile(r"^[a-z][a-z0-9_]*$")

# Gleam 1.17.0 reserved words. A reserved word is a syntactically legal
# snake identifier but cannot be used as a module-path segment as-is, so
# it is escaped with a trailing ``_``. Pinned to the Gleam 1.17.0 language
# reference reserved-words list (the toolchain version recorded in
# ``docs/research/gleam-atomvm/dossier.md``); keep this in lockstep with
# that pinned version. Gleam reserves some words for future use (``auto``,
# ``delegate``, ``derive``, ``implement``, ``macro``, ``test``) — included
# so a Dart file named after one still yields a forward-compatible module.
_GLEAM_RESERVED: frozenset[str] = frozenset(
    {
        "as",
        "assert",
        "auto",
        "case",
        "const",
        "delegate",
        "derive",
        "echo",
        "else",
        "fn",
        "if",
        "implement",
        "import",
        "let",
        "macro",
        "opaque",
        "panic",
        "pub",
        "test",
        "todo",
        "type",
        "use",
    }
)


def target_extension() -> str:
    """Return the Gleam target file extension (``.gleam``)."""
    return TARGET_EXTENSION


def normalize_segment(seg: str) -> str:
    """Map one path segment to a legal Gleam module-path segment.

    Deterministic and pure (FR-009/FR-010). The output always matches
    ``^[a-z][a-z0-9_]*$`` and is non-reserved (SC-004). An already-legal,
    non-reserved segment is returned byte-identically (FR-003 AS-2).

    Rule (data-model.md § "Gleam module-segment normalization rule"):

    1. legal (``_GLEAM_SEGMENT_RE``) **and** non-reserved -> unchanged;
    2. else lowercase ASCII letters and map every char not in
       ``[a-z0-9_]`` to ``_`` (a 1:1 char map — deterministic);
    3. if the result is empty or does not start with ``[a-z]`` -> prefix
       ``g_``;
    4. if the result is a reserved word -> append ``_``.

    NOT injective (research.md R-003 pigeonhole): distinct illegal inputs
    can collapse onto the same legal segment (e.g. ``Runner`` and
    ``runner`` both -> ``runner``). Cross-file collision is the planner's
    concern (R-003 ruling R3-b).
    """
    if _GLEAM_SEGMENT_RE.match(seg) and seg not in _GLEAM_RESERVED:
        return seg
    s = "".join(
        c.lower() if (c.isascii() and c.isalnum()) else "_" for c in seg
    )
    if s == "" or not s[0:1].isalpha():
        s = "g_" + s
    if s in _GLEAM_RESERVED:
        s = s + "_"
    return s


def target_for(source_rel: str) -> str:
    """Map a source-relative POSIX path to the target-relative POSIX path.

    The directory structure is mirrored **verbatim** (POSIX separators in
    / POSIX separators out — the inventory stores POSIX rel paths, feature
    012 R7); the basename extension is swapped to ``.gleam``; and **every**
    path segment (directories and the basename stem) is normalized to a
    legal Gleam module-path segment via :func:`normalize_segment`. No Gleam
    project-layout prefix is added (F3's concern).

    Examples (data-model.md worked examples)
    ----------------------------------------
    ``lib/runtime/heap_fcp.dart`` -> ``lib/runtime/heap_fcp.gleam``
    ``lib/Foo.dart``             -> ``lib/foo.gleam`` (uppercase normalized)
    ``lib/2d_grid.dart``         -> ``lib/g_2d_grid.gleam`` (leading digit)
    ``lib/type.dart``            -> ``lib/type_.gleam`` (reserved word)
    ``README``                   -> ``readme.gleam`` (no source ext)
    """
    rel = source_rel.replace("\\", "/")
    segments = rel.split("/")
    *dirs, base = segments
    dot = base.rfind(".")
    stem = base[:dot] if dot > 0 else base
    out = [normalize_segment(d) for d in dirs]
    out.append(normalize_segment(stem) + TARGET_EXTENSION)
    return "/".join(out)


def workdir_name(source_rel: str) -> Optional[str]:
    """Return the per-source-file working-directory name.

    D2NET ``TargetTreePlanner`` parity (same convention as
    ``dart_csharp``): ``__<basename-without-ext>`` with the stem taken
    **verbatim** (the ``__`` prefix already makes the working directory a
    non-module path, so it is not Gleam-normalized). Never ``None`` for
    ``dart_gleam`` (the pair always has a workdir convention).
    """
    rel = source_rel.replace("\\", "/")
    slash = rel.rfind("/")
    base = rel[slash + 1 :] if slash >= 0 else rel
    dot = base.rfind(".")
    stem = base[:dot] if dot > 0 else base
    return f"__{stem}"


__all__ = [
    "TARGET_EXTENSION",
    "normalize_segment",
    "target_extension",
    "target_for",
    "workdir_name",
]
