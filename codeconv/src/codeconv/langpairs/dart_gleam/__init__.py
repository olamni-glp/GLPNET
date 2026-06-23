"""``dart_gleam`` — the Dart -> Gleam production language pair (feature 032).

Master registration: binds the **shared Dart** source-side hooks
(:mod:`source_dart`, identical-in-result to ``dart_csharp``'s Dart source
side — FR-002) and the **Gleam** target/mirror-side hooks
(:mod:`target_gleam`, :mod:`mirror_gleam`) into one
:class:`~codeconv.langpairs.base.LangPair`, then registers it.

Importing this package registers the pair (contract § Registry surface:
"``langpairs/__init__.py`` MUST auto-import every production pair package
so ``list_pairs()`` / ``get()`` work without the caller importing the
pair"). Adding this pair is the 016 Extensibility proof obligation: a new
``langpairs/<source>_<target>/`` package + exactly one auto-import line in
``langpairs/__init__.py``, zero inventory/structure stage-tool edits
(FR-005). Under the R-003 owner ruling (R3-b) one *generic, pair-agnostic*
target-uniqueness assertion is additionally permitted in
``tools/scaffold/planner.py`` — see that module and the SC-003 carve-out.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

from codeconv.langpairs import register
from codeconv.langpairs.base import LangPair

from . import mirror_gleam, source_dart, target_gleam


class DartGleam(LangPair):
    """Dart -> Gleam language pair (spec FR-001; second production pair)."""

    # --- identity ---
    def key(self) -> tuple[str, str]:
        return ("dart", "gleam")

    # --- source side (init / discover) — shared Dart, delegates ---
    def source_extensions(self) -> tuple[str, ...]:
        return source_dart.source_extensions()

    def tool_exclusion_globs(self) -> tuple[str, ...]:
        return source_dart.tool_exclusion_globs()

    def read_package_name(
        self, subtree_root: Path
    ) -> tuple[Optional[str], Optional[dict]]:
        return source_dart.read_package_name(subtree_root)

    def extract_imports(
        self,
        file_path: Path,
        subtree_root: Path,
        package_name: Optional[str],
    ) -> list[str]:
        return source_dart.extract_imports(
            file_path, subtree_root, package_name
        )

    def extract_leading_doc(self, file_path: Path) -> str:
        return source_dart.extract_leading_doc(file_path)

    # --- target side (scaffold) — Gleam ---
    def target_extension(self) -> str:
        return target_gleam.target_extension()

    def target_for(self, source_rel: str) -> str:
        return target_gleam.target_for(source_rel)

    def workdir_name(self, source_rel: str) -> Optional[str]:
        return target_gleam.workdir_name(source_rel)

    # --- mirror side — Gleam companions/tracker ---
    def mirror_prune_segments(self) -> tuple[str, ...]:
        return mirror_gleam.mirror_prune_segments()

    def preserved_source_suffix(self) -> str:
        return mirror_gleam.preserved_source_suffix()

    def companion_extensions(self) -> tuple[str, ...]:
        return mirror_gleam.companion_extensions()

    def companion_stub_comment(
        self, companion_ext: str, source_basename: str
    ) -> str:
        return mirror_gleam.companion_stub_comment(
            companion_ext, source_basename
        )

    def tracker_filename(self) -> str:
        return mirror_gleam.tracker_filename()


# Register at import (idempotent — see langpairs.register).
register(DartGleam())


__all__ = ["DartGleam"]
