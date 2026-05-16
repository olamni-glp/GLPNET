"""SQL-safety static scan — Feature 016 / T035.

The feature-012/015 SQL-safety carry-forward (DISCIPLINE / plan
Constraints): no ``COPY ... FROM STDIN`` and no client-side
prepared-statement cache anywhere in the codeconv SQL surface. This
extends that guarantee to the feature-016 source added by this branch:
``codeconv/src/codeconv/tools/init/``,
``codeconv/src/codeconv/tools/scaffold/``, and
``codeconv/src/codeconv/langpairs/``.

NOTE (recorded in the final report): T035 as written says "extend
``test_phase7_verifications.py``'s SQL-safety grep", but no such grep
exists anywhere in ``codeconv/tests/`` on this branch (the
feature-012/015 carry-forward was never landed as a test). This file
delivers the *intent* of T035 — the scoped static assertion over the
feature-016 SQL surface — as a standalone, no-bridge test (the natural
home; ``test_phase7_verifications`` is ``@needs_bridge``).

Pure static — NO bridge.
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest


SRC_ROOT = Path(__file__).resolve().parent.parent / "src" / "codeconv"

_SCAN_DIRS = (
    SRC_ROOT / "tools" / "init",
    SRC_ROOT / "tools" / "scaffold",
    SRC_ROOT / "langpairs",
)

# Forbidden patterns (feature-012/015 SQL-safety carry-forward):
#  - ``COPY ... FROM STDIN`` (server-side bulk path; not allowed over the
#    PGLite bridge).
#  - ``copy_expert`` (psycopg COPY helper).
#  - ``prepared_statement_cache_size`` set to anything non-zero (the
#    client-side prepared-statement cache must stay disabled — it breaks
#    the PGLite single-session bridge under cross-stack contention).
_COPY_FROM_STDIN = re.compile(r"COPY\b[^;]*\bFROM\s+STDIN", re.IGNORECASE)
_COPY_EXPERT = re.compile(r"\bcopy_expert\b")
_PSC_NONZERO = re.compile(
    r"prepared_statement_cache_size\s*=\s*(?!0\b)\d+"
)


def _py_files() -> list[Path]:
    out: list[Path] = []
    for d in _SCAN_DIRS:
        if d.is_dir():
            out.extend(sorted(d.rglob("*.py")))
    return out


def test_feature016_sql_surface_has_no_copy_from_stdin() -> None:
    offenders: list[str] = []
    for f in _py_files():
        text = f.read_text(encoding="utf-8", errors="replace")
        if _COPY_FROM_STDIN.search(text) or _COPY_EXPERT.search(text):
            offenders.append(str(f))
    assert not offenders, (
        "feature-012/015 SQL-safety carry-forward violated — "
        f"COPY ... FROM STDIN / copy_expert found in: {offenders}"
    )


def test_feature016_sql_surface_no_clientside_prepared_cache() -> None:
    offenders: list[str] = []
    for f in _py_files():
        text = f.read_text(encoding="utf-8", errors="replace")
        if _PSC_NONZERO.search(text):
            offenders.append(str(f))
    assert not offenders, (
        "feature-012/015 SQL-safety carry-forward violated — "
        f"non-zero prepared_statement_cache_size in: {offenders}"
    )


def test_scan_dirs_exist() -> None:
    """Guard: the feature-016 source dirs this scan targets must exist
    (so a future move doesn't silently make the scan vacuous)."""
    for d in _SCAN_DIRS:
        assert d.is_dir(), f"feature-016 SQL-scan target missing: {d}"
    assert _py_files(), "feature-016 SQL scan matched zero .py files"
