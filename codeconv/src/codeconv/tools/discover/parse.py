"""Dart source parsing for `/codeconv-discover` — Phase 6 / US4 / T070.

Per research R11 + R12:

- :func:`extract_leading_doc` — returns the verbatim leading
  ``///``-style or ``/** */``-style doc-comment block at the top of the
  file (after blank lines), with markers stripped per Dart convention.
  Empty string when no leading doc-comment is present. 200-line cap.

- :func:`extract_imports` — returns POSIX-relative paths (relative to
  the subtree root) for every ``import 'X';`` directive that resolves to
  a file INSIDE the subtree. ``package:`` and ``dart:`` targets are
  skipped. Duplicates are deduplicated with a warning per FR-019.

The implementations are deliberately regex-driven (no Dart AST), per
R11/R12: top-of-file doc comments and import directives are regular
enough that a regex pass is correct for the spec's needs.
"""

from __future__ import annotations

import os
import re
import warnings
from pathlib import Path
from typing import List, Optional


_LEADING_DOC_LINE_CAP = 200

# ``import 'foo.dart';`` or ``import "foo.dart";`` — quote pair preserved.
_IMPORT_RE = re.compile(
    r"""^\s*import\s+(?P<q>['"])(?P<target>[^'"]+)(?P=q)\s*(?:show|hide|as|;)""",
    re.MULTILINE,
)


def extract_leading_doc(path: Path) -> str:
    """Return the leading doc-comment block, or ``''`` if absent.

    Reads up to ``_LEADING_DOC_LINE_CAP`` lines (200) — real Dart files
    keep doc comments within the first ~30 lines.
    """
    try:
        lines: List[str] = []
        with path.open("r", encoding="utf-8", errors="replace") as fh:
            for i, line in enumerate(fh):
                if i >= _LEADING_DOC_LINE_CAP:
                    break
                lines.append(line.rstrip("\n"))
    except OSError:
        return ""

    idx = 0
    n = len(lines)
    # Skip blank lines + an optional shebang.
    while idx < n and (
        lines[idx].strip() == "" or lines[idx].lstrip().startswith("#!")
    ):
        idx += 1
    if idx >= n:
        return ""

    first = lines[idx].lstrip()
    if first.startswith("///"):
        return _capture_triple_slash(lines, idx)
    if first.startswith("/**"):
        return _capture_block(lines, idx)
    return ""


def _capture_triple_slash(lines: List[str], idx: int) -> str:
    """Capture a contiguous ``///`` block starting at ``idx``."""
    out: List[str] = []
    n = len(lines)
    while idx < n:
        stripped = lines[idx].lstrip()
        if not stripped.startswith("///"):
            break
        # Strip the ``///`` marker plus exactly one optional space.
        body = stripped[3:]
        if body.startswith(" "):
            body = body[1:]
        out.append(body)
        idx += 1
    return "\n".join(out).rstrip() + ("\n" if out else "")


def _capture_block(lines: List[str], idx: int) -> str:
    """Capture a ``/** ... */`` block starting at ``idx``."""
    out: List[str] = []
    n = len(lines)
    first = True
    while idx < n:
        line = lines[idx]
        stripped = line.lstrip()
        # First line: drop the leading ``/**``.
        if first:
            after_open = stripped[3:]
            first = False
            if "*/" in after_open:
                # Single-line ``/** body */``
                body = after_open[: after_open.index("*/")]
                out.append(_strip_leading_star(body))
                return "\n".join(out).rstrip() + ("\n" if out else "")
            if after_open.strip():
                out.append(_strip_leading_star(after_open))
            idx += 1
            continue
        if "*/" in stripped:
            body = stripped[: stripped.index("*/")]
            cleaned = _strip_leading_star(body)
            if cleaned.strip():
                out.append(cleaned)
            break
        out.append(_strip_leading_star(stripped))
        idx += 1
    return "\n".join(out).rstrip() + ("\n" if out else "")


def _strip_leading_star(s: str) -> str:
    """Strip a single leading ``*`` plus optional space, per JavaDoc/JSDoc convention."""
    s = s.rstrip("\r\n")
    s = s.lstrip()
    if s.startswith("*"):
        s = s[1:]
        if s.startswith(" "):
            s = s[1:]
    return s


def extract_imports(
    file_path: Path,
    subtree_root: Path,
    package_name: Optional[str] = None,
) -> List[str]:
    """Return a deduplicated, lex-sorted list of in-subtree ``import``
    targets as POSIX paths relative to ``subtree_root``.

    Targets starting with ``package:<package_name>/`` are rewritten to
    ``lib/<rest>`` and resolved against ``subtree_root`` per Dart's
    ``package:`` convention (Feature 014 / FR-001..FR-003). Targets that
    rewrite to a path outside ``<subtree>/lib/`` are silently skipped.

    With ``package_name=None`` (the feature-012 fallback) every
    ``package:`` target is skipped silently. ``dart:`` and ``dart-ext:``
    targets are always skipped.

    Targets resolving outside ``subtree_root`` are dropped. Duplicate
    directives in the same file emit a warning (FR-019); the dedup set
    collapses a self-package directive and an equivalent relative
    directive into a single edge (FR-007).
    """
    try:
        text = file_path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    seen: set[str] = set()
    duplicates: list[str] = []
    out: list[str] = []
    file_dir = file_path.resolve().parent
    sub_real = subtree_root.resolve()
    lib_real = (sub_real / "lib").resolve()
    self_prefix = (
        f"package:{package_name}/" if package_name is not None else None
    )

    for m in _IMPORT_RE.finditer(text):
        target = m.group("target").strip()

        # Feature 014: self-package rewrite. Must come BEFORE the
        # external-skip branch so package:<self>/... is captured.
        if (
            self_prefix is not None
            and target.startswith(self_prefix)
            and len(target) > len(self_prefix)
        ):
            rest = target[len(self_prefix):]
            try:
                resolved = (sub_real / "lib" / rest).resolve()
            except (OSError, RuntimeError):
                continue
            # Per Dart convention, package: paths anchor at <pkg>/lib/.
            # A `..` traversal that escapes lib/ is treated as invalid
            # and skipped silently.
            try:
                resolved.relative_to(lib_real)
            except ValueError:
                continue
            try:
                rel_posix = resolved.relative_to(sub_real).as_posix()
            except ValueError:
                continue
            if rel_posix in seen:
                duplicates.append(target)
                continue
            seen.add(rel_posix)
            out.append(rel_posix)
            continue

        if (
            target.startswith("package:")
            or target.startswith("dart:")
            or target.startswith("dart-ext:")
        ):
            continue
        # Resolve relative to the importing file's directory.
        try:
            resolved = (file_dir / target).resolve()
        except (OSError, RuntimeError):
            continue
        try:
            rel = resolved.relative_to(sub_real)
        except ValueError:
            continue  # outside subtree
        rel_posix = rel.as_posix()
        if rel_posix in seen:
            duplicates.append(target)
            continue
        seen.add(rel_posix)
        out.append(rel_posix)

    if duplicates:
        warnings.warn(
            f"duplicate import directive in {file_path}: "
            f"{sorted(set(duplicates))}",
            stacklevel=2,
        )
    return sorted(out)


__all__ = ["extract_imports", "extract_leading_doc"]
