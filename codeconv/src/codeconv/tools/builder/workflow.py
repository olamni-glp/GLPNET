"""codeconv-builder workflow registration — feature 018, T021.

Unlike the six pre-existing tools (whose ``register()`` was a no-op
until T015 delegated it to :func:`codeconv.durable.activate`), the
builder's ``register()`` is the tool that *drives* the durable layer:
it activates the outer/child workflows + step bindings via ``durable``
and exposes the bound handles for the CLI (T022) to launch. Behaviour of
every wrapped 015/016/017 entrypoint remains unchanged (D2 hard gate) —
the builder only *orchestrates* them as DBOS steps, never reimplements
them.
"""

from __future__ import annotations

from typing import Any


def register(dbos_app: Any) -> Any:
    """Activate the durable builder workflows. Idempotent (delegates to
    the shared :func:`codeconv.durable.activate`, which all tools share —
    one activation per process). Returns the bound
    ``{"steps": {...}, "workflows": {"outer":..,"child":..}}`` so the
    builder CLI (T022) can launch the outer workflow."""
    from codeconv.durable import activate

    return activate(dbos_app)


__all__ = ["register"]
