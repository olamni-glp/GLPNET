"""C# target-side hooks for the ``dart_csharp`` language pair.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md``
behavioural requirements 2 & 3 (D2Net.Scaffold ``TargetTreePlanner``
parity):

- :func:`target_for` swaps ONLY the source extension for
  :func:`target_extension` (``.cs``); directory structure is mirrored
  verbatim. ``lib/runtime/heap_fcp.dart`` -> ``lib/runtime/heap_fcp.cs``.
- :func:`workdir_name` returns the per-source-file working directory name
  ``__<basename-without-ext>`` adjacent to each source file (D2NET
  ``"__" + Path.GetFileNameWithoutExtension(...)``).

Pure / side-effect-free (no filesystem, DB, bridge, network) so the
registry unit tests need no ``@needs_bridge``.
"""

from __future__ import annotations

from typing import Optional

# Target extension. Single ``.cs`` for the only production pair.
TARGET_EXTENSION: str = ".cs"


def target_extension() -> str:
    """Return the C# target file extension."""
    return TARGET_EXTENSION


def target_for(source_rel: str) -> str:
    """Map a source-relative POSIX path to the target-relative POSIX path.

    Only the final path component's extension is swapped for
    :func:`target_extension`; every directory segment is mirrored
    verbatim. POSIX separators in / POSIX separators out (the inventory
    stores POSIX rel paths — feature 012 R7).

    Examples
    --------
    ``lib/runtime/heap_fcp.dart`` -> ``lib/runtime/heap_fcp.cs``
    ``bin/main.dart``            -> ``bin/main.cs``
    """
    rel = source_rel.replace("\\", "/")
    slash = rel.rfind("/")
    head, base = (rel[: slash + 1], rel[slash + 1 :]) if slash >= 0 else ("", rel)
    dot = base.rfind(".")
    stem = base[:dot] if dot > 0 else base
    return f"{head}{stem}{TARGET_EXTENSION}"


def workdir_name(source_rel: str) -> Optional[str]:
    """Return the per-source-file working-directory name.

    D2NET ``TargetTreePlanner`` parity: ``__<basename-without-ext>``
    (e.g. ``heap_fcp.dart`` -> ``__heap_fcp``). The working dir is
    created adjacent to each scaffolded source file's target location.
    Never ``None`` for ``dart_csharp`` (the pair always has a workdir
    convention).
    """
    rel = source_rel.replace("\\", "/")
    slash = rel.rfind("/")
    base = rel[slash + 1 :] if slash >= 0 else rel
    dot = base.rfind(".")
    stem = base[:dot] if dot > 0 else base
    return f"__{stem}"


__all__ = [
    "TARGET_EXTENSION",
    "target_extension",
    "target_for",
    "workdir_name",
]
