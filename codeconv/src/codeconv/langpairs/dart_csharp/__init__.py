"""``dart_csharp`` — the only production language pair (Dart -> C#).

Master registration: binds the source-side hooks (:mod:`source_dart`,
byte-faithful to the pre-016 Dart path) and the target-side hooks
(:mod:`target_csharp`, D2Net.Scaffold ``TargetTreePlanner`` parity) into
one :class:`~codeconv.langpairs.base.LangPair`, then registers it.

Importing this package registers the pair (contract § Registry surface:
"``langpairs/__init__.py`` MUST auto-import every production pair package
so ``list_pairs()`` / ``get()`` work without the caller importing the
pair").
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

from codeconv.langpairs import register
from codeconv.langpairs.base import LangPair

from . import mirror_dart, source_dart, target_csharp


class DartCSharp(LangPair):
    """Dart -> C# language pair (spec FR-001; the only production pair)."""

    # --- identity ---
    def key(self) -> tuple[str, str]:
        return ("dart", "csharp")

    # --- source side (init / discover) — byte-faithful delegates ---
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

    # --- target side (scaffold) ---
    def target_extension(self) -> str:
        return target_csharp.target_extension()

    def target_for(self, source_rel: str) -> str:
        return target_csharp.target_for(source_rel)

    def workdir_name(self, source_rel: str) -> Optional[str]:
        return target_csharp.workdir_name(source_rel)

    # --- mirror side (spec Amendment 1) — byte-faithful delegates ---
    def mirror_prune_segments(self) -> tuple[str, ...]:
        return mirror_dart.mirror_prune_segments()

    def preserved_source_suffix(self) -> str:
        return mirror_dart.preserved_source_suffix()

    def companion_extensions(self) -> tuple[str, ...]:
        return mirror_dart.companion_extensions()

    def companion_stub_comment(
        self, companion_ext: str, source_basename: str
    ) -> str:
        return mirror_dart.companion_stub_comment(
            companion_ext, source_basename
        )

    def tracker_filename(self) -> str:
        return mirror_dart.tracker_filename()


# Register at import (idempotent — see langpairs.register).
register(DartCSharp())


__all__ = ["DartCSharp"]
