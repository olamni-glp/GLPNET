"""Language-pair registry.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md``
§ "Registry surface" + "Stage enforcement" (spec FR-001..FR-005, D6).

Process-wide map ``(source, target) -> LangPair``:

- :func:`register` — idempotent on an identical key; raises on a
  conflicting re-register.
- :func:`get` — raises :class:`UnknownLangPair` (actionable, lists known)
  when absent.
- :func:`list_pairs` — sorted ``(source, target)`` id tuples.
- :func:`resolve_workspace_pair` — reads ``codeconv.workspace_settings``
  and returns the bound pair (stage-enforcement rules below).

``langpairs/__init__.py`` auto-imports every production pair package so
``list_pairs()`` / ``get()`` work without the caller importing the pair.
The only production pair registered by this feature is
``("dart", "csharp")`` (contract § Registry surface).
"""

from __future__ import annotations

from typing import Any, Optional

from .base import LangPair, UnknownLangPair

# (source, target) -> LangPair
_REGISTRY: dict[tuple[str, str], LangPair] = {}

# Production pair packages auto-imported at module import so the registry
# is populated without the caller importing the pair. Adding a new pair =
# adding its package here (the ONLY edit outside langpairs/<pair>/ — and
# explicitly NOT a stage-tool edit; contract § "Extensibility proof").
_PRODUCTION_PAIR_MODULES: tuple[str, ...] = (
    "codeconv.langpairs.dart_csharp",
)


def register(pair: LangPair) -> None:
    """Register ``pair`` under its ``key()`` identity.

    Idempotent if the *same instance* (or an equal-key instance of the
    same type) is re-registered; raises ``ValueError`` on a conflicting
    re-register (same key, different implementation) — contract
    § Registry surface ``register``.
    """
    key = pair.key()
    existing = _REGISTRY.get(key)
    if existing is not None:
        if existing is pair or type(existing) is type(pair):
            # Idempotent: identical key + identical implementation type.
            _REGISTRY[key] = pair
            return
        raise ValueError(
            f"conflicting re-register for language pair {key[0]!r}->{key[1]!r}: "
            f"{type(existing).__name__} already registered, "
            f"refusing to replace with {type(pair).__name__}"
        )
    _REGISTRY[key] = pair


def get(source: str, target: str) -> LangPair:
    """Return the registered pair for ``(source, target)``.

    Raises :class:`UnknownLangPair` (message names the registered pairs)
    when no pair is registered for that identity.
    """
    _ensure_production_pairs_loaded()
    pair = _REGISTRY.get((source, target))
    if pair is None:
        raise UnknownLangPair(source, target, list_pairs())
    return pair


def list_pairs() -> list[tuple[str, str]]:
    """Return the registered ``(source, target)`` identities, sorted."""
    _ensure_production_pairs_loaded()
    return sorted(_REGISTRY.keys())


def resolve_workspace_pair(
    engine: Any,
    *,
    require_workspace: bool = True,
    override: Optional[tuple[str, str]] = None,
) -> LangPair:
    """Resolve the effective pair for a stage from ``workspace_settings``.

    Per contract § "Stage enforcement" (FR-004 / FR-018 / SC-008):

    - Workspace pair unset AND ``require_workspace`` (scaffold) ->
      :class:`UnknownLangPair`-shaped refusal (caller maps to non-zero
      exit, no output).
    - Workspace pair unset AND NOT ``require_workspace`` (discover) ->
      default ``("dart", "csharp")`` (preserves pre-016 behaviour).
    - Workspace pair set but not a registered pair -> actionable error
      (lists known pairs), no mutation.
    - Workspace pair set and an explicit ``override`` disagrees ->
      refuse (no mixed-pair output).

    ``engine`` is a SQLAlchemy engine; the read is a single SELECT
    against ``codeconv.workspace_settings``.
    """
    from sqlalchemy import text

    src: Optional[str] = None
    tgt: Optional[str] = None
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT key, value FROM codeconv.workspace_settings "
                "WHERE key IN ('source_lang', 'target_lang')"
            )
        ).all()
    kv = {k: v for k, v in rows}
    src = kv.get("source_lang")
    tgt = kv.get("target_lang")

    if not src or not tgt:
        if require_workspace:
            raise UnknownLangPair("", "", list_pairs())
        # discover back-compat default (FR-004 carve-out).
        src, tgt = _DEFAULT_PAIR

    if override is not None and (override[0], override[1]) != (src, tgt):
        raise PairMismatch(
            requested=override,
            workspace=(src, tgt),
        )

    return get(src, tgt)


class PairMismatch(Exception):
    """An explicit per-invocation pair override disagrees with the
    workspace-recorded pair (FR-004 / FR-018 / SC-008 — no mixed-pair
    output)."""

    def __init__(
        self,
        requested: tuple[str, str],
        workspace: tuple[str, str],
    ) -> None:
        self.requested = requested
        self.workspace = workspace
        super().__init__(
            f"language-pair mismatch: workspace is bound to "
            f"{workspace[0]!r}->{workspace[1]!r} but "
            f"{requested[0]!r}->{requested[1]!r} was requested. "
            f"Refusing to produce mixed-pair output."
        )


_DEFAULT_PAIR: tuple[str, str] = ("dart", "csharp")

_production_pairs_loaded = False


def _ensure_production_pairs_loaded() -> None:
    """Import every production pair package exactly once (idempotent).

    Deferred to first registry access (not module import) to avoid an
    import cycle: a pair package imports :func:`register` from this
    module.
    """
    global _production_pairs_loaded
    if _production_pairs_loaded:
        return
    _production_pairs_loaded = True
    import importlib

    for mod in _PRODUCTION_PAIR_MODULES:
        importlib.import_module(mod)


__all__ = [
    "LangPair",
    "PairMismatch",
    "UnknownLangPair",
    "get",
    "list_pairs",
    "register",
    "resolve_workspace_pair",
]
