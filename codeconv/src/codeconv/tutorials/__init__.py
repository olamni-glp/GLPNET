"""codeconv ``tutorials`` — read-only GLP tutorial browser (feature 022).

PURE, BRIDGE-FREE sub-app (research D1). Nothing in this package imports the
PGLite bridge, DBOS, SQLAlchemy, or psycopg — it is wired into the codeconv
Typer CLI via a direct ``app.add_typer(...)`` (NOT through
``runner.tool_registry()``), exactly like the built-in ``codeconv list``. The
list path is a filesystem walk of the vendored corpus at ``tutorials/olamni/``;
it never spins up the bridge and meets the read-only / <3 s / no-external-deps
contract (FR-010, SC-005). The bridge-free invariant is guarded by
``codeconv/tests/test_tutorials_no_bridge.py``.

Modules:
  * ``corpus``   — walk the vendored tree → Corpus/Tutorial/Exercise/Script (D4, D6).
  * ``describe`` — description extraction precedence (D7).
  * ``match``    — tutorial identifier matching (D5).
  * ``render``   — human-readable + JSON rendering (D8).
  * ``sync``     — build-time vendoring + provenance manifest (D3).
  * ``cli``      — Typer sub-app: ``tutorials list`` (+ supporting ``sync``).
"""

from __future__ import annotations
