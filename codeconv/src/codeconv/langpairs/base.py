"""Language-pair plugin protocol + registry exceptions.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md``
(spec FR-001..FR-005, D6). Any deviation from the contract is a bug.

A ``LangPair`` aggregates the per-stage hooks for one ``(source, target)``
identity. Hooks are grouped by stage so each stage depends only on the
slice it needs:

- **source** (init / discover): ``source_extensions``,
  ``tool_exclusion_globs``, ``read_package_name``, ``extract_imports``,
  ``extract_leading_doc``.
- **target** (scaffold): ``target_extension``, ``target_for``,
  ``workdir_name``.
- **identity**: ``key`` -> ``(source, target)``.

Hooks MUST be pure / side-effect-free (filesystem read at most: no DB, no
bridge, no network) so they are unit-testable without ``@needs_bridge``
(contract § "Pure & side-effect-free").
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional, Protocol, runtime_checkable


@runtime_checkable
class LangPair(Protocol):
    """Per-stage hook bundle for one ``(source, target)`` language pair.

    See ``contracts/langpair_plugin_contract.md`` § "``LangPair`` protocol".
    """

    # --- identity ---
    def key(self) -> tuple[str, str]:
        """Return the ``(source_id, target_id)`` identity tuple."""
        ...

    # --- source side (init / discover) ---
    def source_extensions(self) -> tuple[str, ...]:
        """File extensions that mark a source file (e.g. ``(".dart",)``)."""
        ...

    def tool_exclusion_globs(self) -> tuple[str, ...]:
        """Recommended source tool-exclusion globs (e.g. ``build/``).

        Used by ``init`` to seed ``codeconv.excluded_directories`` and by
        ``discover``'s walker to prune generated/tool subtrees.
        """
        ...

    def read_package_name(
        self, subtree_root: Path
    ) -> tuple[Optional[str], Optional[dict]]:
        """Return ``(package_name, warning_or_None)`` for the subtree."""
        ...

    def extract_imports(
        self,
        file_path: Path,
        subtree_root: Path,
        package_name: Optional[str],
    ) -> list[str]:
        """Return the in-subtree import targets (POSIX rel paths)."""
        ...

    def extract_leading_doc(self, file_path: Path) -> str:
        """Return the leading doc-comment block, or ``''`` if absent."""
        ...

    # --- target side (scaffold) ---
    def target_extension(self) -> str:
        """Target file extension (e.g. ``".cs"``)."""
        ...

    def target_for(self, source_rel: str) -> str:
        """Map a source-relative POSIX path to the target-relative path.

        Only the extension is swapped for :meth:`target_extension`; the
        directory structure is mirrored verbatim.
        """
        ...

    def workdir_name(self, source_rel: str) -> Optional[str]:
        """Per-source-file working-directory name, or ``None``.

        ``dart_csharp`` returns ``__<basename-without-ext>`` (D2NET
        ``TargetTreePlanner`` parity).
        """
        ...


class UnknownLangPair(Exception):
    """Raised when a requested ``(source, target)`` pair is not registered.

    The message is actionable: it always names the requested pair and the
    registered pairs (contract § Registry surface ``get`` /
    "Stage enforcement").
    """

    def __init__(
        self,
        source: str,
        target: str,
        known: list[tuple[str, str]],
    ) -> None:
        self.source = source
        self.target = target
        self.known = list(known)
        known_str = (
            ", ".join(f"{s}->{t}" for s, t in self.known)
            if self.known
            else "(none registered)"
        )
        super().__init__(
            f"unregistered language pair {source!r}->{target!r}. "
            f"Registered pairs: {known_str}."
        )


__all__ = ["LangPair", "UnknownLangPair"]
