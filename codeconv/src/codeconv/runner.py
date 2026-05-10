"""codeconv runner: DBOS singleton + tool registry.

Per ``specs/012-codeconv-runner/contracts/codeconv_runner_cli.md`` the
runner discovers tools at startup via ``pkgutil.iter_modules`` against
``codeconv.tools.__path__``. Adding a new tool requires no edits to
``runner.py`` or ``cli.py`` (FR-016) — the tool is a Python subpackage
under ``codeconv/src/codeconv/tools/<name>/`` exporting at least
``app: typer.Typer``, optionally ``register_workflows(dbos_app)``.

The DBOS instance is a process-singleton initialised by the CLI at
startup; tools obtain it via :func:`get_dbos`. ``workflow`` is re-exported
from ``dbos`` for convenience.
"""

from __future__ import annotations

import importlib
import logging
import pkgutil
import warnings
from dataclasses import dataclass
from typing import Any, Callable, Optional

import codeconv.tools as _tools_pkg


_LOG = logging.getLogger("codeconv.runner")
_DBOS_SINGLETON: Optional[Any] = None


# Re-export of dbos.workflow for tool authors. Lazy because ``dbos`` is
# heavy and tools that don't run workflows shouldn't pay the import cost.
def workflow(*args: Any, **kwargs: Any) -> Any:
    """Re-export of :func:`dbos.workflow` per contract.

    Tool authors use ``from codeconv.runner import workflow`` so they
    don't need to import ``dbos`` directly in their tool surface.
    """
    from dbos import DBOS as _DBOS  # type: ignore
    return _DBOS.workflow(*args, **kwargs)


def set_dbos(dbos: Any) -> None:
    """Install the process-wide DBOS singleton.

    Called once by the CLI bootstrap (or by tests). Idempotent for the
    same instance; raises if a different instance is set.
    """
    global _DBOS_SINGLETON
    if _DBOS_SINGLETON is not None and _DBOS_SINGLETON is not dbos:
        raise RuntimeError(
            "DBOS singleton already initialised with a different instance"
        )
    _DBOS_SINGLETON = dbos


def get_dbos() -> Any:
    """Return the process-wide DBOS singleton.

    Raises ``RuntimeError`` if the CLI did not initialise it — tools that
    need DBOS must be invoked through the CLI, not imported standalone.
    """
    if _DBOS_SINGLETON is None:
        raise RuntimeError(
            "DBOS singleton not initialised. "
            "codeconv tools must be invoked via the `codeconv` CLI."
        )
    return _DBOS_SINGLETON


def reset_dbos_for_tests() -> None:
    """Test helper — clears the DBOS singleton between tests."""
    global _DBOS_SINGLETON
    _DBOS_SINGLETON = None


@dataclass
class RegisteredTool:
    name: str
    app: Any  # typer.Typer
    register_workflows: Optional[Callable[[Any], None]]
    description: str = ""


def tool_registry() -> list[RegisteredTool]:
    """Discover tool subpackages under ``codeconv.tools``.

    Each tool MUST export ``app: typer.Typer``. ``register_workflows`` is
    optional. Malformed packages (missing ``app``) emit a warning and are
    skipped — the runner does not fail because one tool is broken.

    Returns the registry in deterministic name order.
    """
    found: list[RegisteredTool] = []
    package_path = list(getattr(_tools_pkg, "__path__", []))
    for module_info in sorted(
        pkgutil.iter_modules(package_path),
        key=lambda m: m.name,
    ):
        if not module_info.ispkg:
            # Tools are subpackages; loose .py modules under tools/ are
            # ignored. (Tooling helpers may still import them directly.)
            continue
        name = module_info.name
        if name.startswith("_"):
            # Convention: leading underscore = private / internal package.
            continue
        try:
            module = importlib.import_module(f"codeconv.tools.{name}")
        except Exception as exc:
            warnings.warn(
                f"codeconv tool '{name}' failed to import: {exc!r}",
                stacklevel=2,
            )
            continue
        app = getattr(module, "app", None)
        if app is None:
            warnings.warn(
                f"codeconv tool '{name}' has no `app: typer.Typer`; skipped",
                stacklevel=2,
            )
            continue
        register = getattr(module, "register_workflows", None)
        description = (getattr(app, "info", None) and getattr(app.info, "help", "")) or ""
        found.append(
            RegisteredTool(
                name=name,
                app=app,
                register_workflows=register if callable(register) else None,
                description=description,
            )
        )
    return found


__all__ = [
    "RegisteredTool",
    "get_dbos",
    "reset_dbos_for_tests",
    "set_dbos",
    "tool_registry",
    "workflow",
]
