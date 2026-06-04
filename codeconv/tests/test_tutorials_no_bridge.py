"""Bridge-free invariant guard — US1 (T010, research D1).

The ``tutorials`` list path MUST NOT import the PGLite bridge, DBOS, SQLAlchemy,
or psycopg (it is a pure filesystem walk wired via ``add_typer``, not a
``tool_registry`` tool). Mirrors the equiv suite's
``test_no_lm_on_production_path`` intent: a static import-surface check plus a
clean-subprocess ``sys.modules`` check.
"""

from __future__ import annotations

import ast
import subprocess
import sys
from pathlib import Path

PKG_DIR = Path(__file__).resolve().parents[1] / "src" / "codeconv" / "tutorials"

# Top-level packages that must never be IMPORTED on the list path (DB/DBOS
# stack). Checked against real import statements via AST — not substrings — so
# the modules' own docstrings (which explain "NOT through tool_registry") do
# not false-positive.
_BANNED_TOP = {"dbos", "sqlalchemy", "psycopg", "psycopg2"}

# codeconv internals that would pull the bridge/engine if imported.
_BANNED_CODECONV = {"codeconv.bridge_client", "codeconv.runner", "codeconv.db"}


def _imported_modules(tree: ast.AST) -> set[str]:
    mods: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            mods.update(alias.name for alias in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            mods.add(node.module)
    return mods


def test_package_source_imports_no_bridge_or_dbos() -> None:
    for py in PKG_DIR.glob("*.py"):
        tree = ast.parse(py.read_text(encoding="utf-8"), filename=str(py))
        for mod in _imported_modules(tree):
            root = mod.split(".")[0]
            assert root not in _BANNED_TOP, f"{py.name} imports banned '{mod}'"
            assert mod not in _BANNED_CODECONV, f"{py.name} imports banned '{mod}'"


def test_importing_list_path_loads_no_bridge_modules() -> None:
    """Importing every list-path module pulls in no bridge/DBOS/DB stack."""
    code = (
        "import importlib, sys\n"
        "for m in ('corpus','describe','match','render','cli',\n"
        "          'resolve','backends','outcome','explain','propose'):\n"
        "    importlib.import_module('codeconv.tutorials.' + m)\n"
        "banned = sorted(\n"
        "    m for m in sys.modules\n"
        "    if m.split('.')[0] in %r or 'bridge' in m.split('.')[-1]\n"
        ")\n"
        "print('\\n'.join(banned))\n"
        "sys.exit(1 if banned else 0)\n"
    ) % (_BANNED_TOP,)
    result = subprocess.run(
        [sys.executable, "-c", code],
        capture_output=True,
        text=True,
        cwd=str(Path(__file__).resolve().parents[2]),
    )
    assert result.returncode == 0, (
        "list path imported bridge/DBOS modules:\n" + result.stdout + result.stderr
    )
